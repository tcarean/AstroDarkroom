// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

#nullable disable // copied from FlatCheck 2.3.0 unchanged, which compiles without nullable annotations (ADR 0003)

// CfaPattern.cs - ce culoare are fiecare din cele patru pozitii Bayer.
//
// DE CE NU E O CONSTANTA. RGGB e cea mai des intalnita ordine, dar nu e singura: exista
// aparate cu GBRG, GRBG si BGGR. Ordinea se citeste din fisier - din libraw pe un raw,
// din cardul BAYERPAT pe un FITS scos prin tab-ul Conversion din Siril - iar cand
// fisierul n-o spune ramane null si pozitiile raman anonime. Unealta nu ghiceste: o
// eticheta "R" pusa pe canalul gresit ar fi mai rea decat lipsa ei, fiindca ar parea
// masurata.
//
// ATENTIE LA INDEXUL 3. Culoarea unei pozitii se ia din cdesc, nu direct din codul FC:
// libraw pune uneori al doilea verde pe indexul 3 in loc de 1 (cdesc = "RGBG"). Masurat
// pe un NEF de D3300: FC da 0/1/3/2, iar prin cdesc iese RGGB. Un decodor care presupune
// ca 1 = G ar citi al doilea verde ca pe altceva.

namespace DarksCheck.Core
{
    /// <summary>
    /// Culoarea celor patru pozitii Bayer ale rasterului INCARCAT - [0,0], [0,1], [1,0],
    /// [1,1] - in ordinea in care le numeroteaza si ExposureAnalyzer.
    /// </summary>
    internal sealed class CfaPattern
    {
        /// <summary>Exact 4 litere din {R,G,B}, dintre care exact doua G.</summary>
        internal readonly char[] Colours;

        /// <summary>De unde s-a citit ordinea: "libraw" sau "BAYERPAT". Se arata in raport.</summary>
        internal readonly string Source;

        CfaPattern(char[] colours, string source)
        {
            Colours = colours;
            Source = source;
        }

        /// <summary>Cele patru litere ca sir, in ordinea rasterului: "RGGB".</summary>
        internal string Letters => new string(Colours);

        /// <summary>
        /// Din cele patru litere, daca sunt un mozaic Bayer valid: patru caractere din
        /// {R,G,B} cu exact doua G. Orice altceva - alta lungime, litere straine, trei
        /// verzi, un CFA cu alb - intoarce null, adica "nu stiu", care e raspunsul corect.
        /// </summary>
        internal static CfaPattern Parse(string letters, string source)
        {
            if (string.IsNullOrEmpty(letters)) return null;

            string s = letters.Trim().ToUpperInvariant();
            if (s.Length != 4) return null;

            int greens = 0;
            foreach (char c in s)
            {
                if (c == 'G') greens++;
                else if (c != 'R' && c != 'B') return null;
            }
            if (greens != 2) return null;

            return new CfaPattern(s.ToCharArray(), source);
        }

        /// <summary>
        /// Din libraw: masca <paramref name="filters"/> plus <paramref name="cdesc"/>,
        /// evaluate la originea zonei VIZIBILE. Offset-urile sunt TopMargin/LeftMargin,
        /// fiindca RawReader decupeaza marginile mascate - iar un offset impar muta faza
        /// mozaicului cu o pozitie, deci ar da o ordine gresita cu aerul unei masuratori.
        ///
        /// FC(row, col) e macroul din dcraw, si intoarce un INDEX in cdesc, nu o culoare.
        /// Valorile speciale ale mastii se resping: 0 = fara CFA (mono, Foveon),
        /// 1 = Leaf Catchlight, 9 = X-Trans (mozaic 6x6, nu 2x2).
        /// </summary>
        internal static CfaPattern FromLibRaw(uint filters, string cdesc, int rowOffset, int colOffset)
        {
            if (filters == 0 || filters == 1 || filters == 9) return null;
            if (string.IsNullOrEmpty(cdesc) || cdesc.Length < 4) return null;

            var c = new char[4];
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                {
                    int row = rowOffset + y, col = colOffset + x;
                    int shift = ((((row << 1) & 14) + (col & 1)) << 1);
                    int idx = (int)((filters >> shift) & 3);
                    c[y * 2 + x] = char.ToUpperInvariant(cdesc[idx]);
                }

            return Parse(new string(c), "libraw");
        }

        /// <summary>
        /// Acelasi mozaic privit de la (dy, dx). Pentru XBAYROFF / YBAYROFF din FITS, pe
        /// care Siril le scrie cand cadrul a fost decupat fata de senzor.
        /// </summary>
        internal CfaPattern Shift(int dy, int dx)
        {
            if ((dy & 1) == 0 && (dx & 1) == 0) return this;

            var c = new char[4];
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    c[y * 2 + x] = Colours[(((y + dy) & 1) * 2) + ((x + dx) & 1)];

            return new CfaPattern(c, Source);
        }

        /// <summary>
        /// Acelasi mozaic citit in ordinea RANDURILOR DIN FISIER. BAYERPAT descrie coltul
        /// din stanga-sus al senzorului, privit de sus in jos; Siril insa scrie randurile
        /// de jos in sus (ROWORDER = 'BOTTOM-UP'), iar FitsReader le citeste in ordinea
        /// fisierului. Pe o inaltime PARA, primul rand din fisier e un rand impar al
        /// senzorului, deci faza mozaicului se muta cu un rand: RGGB devine GBRG. Pe o
        /// inaltime impara randul de jos are aceeasi paritate ca cel de sus si nu se
        /// schimba nimic. TOP-DOWN, lipsa cardului, orice altceva: nimic.
        ///
        /// Masurat pe pp_flat_stacked.fit (6016x4016, Tokina, cer albastru de amurg):
        /// fara corectie --histeq numea "G2" pozitia [1,0], care statea la o treime din
        /// verde - adica exact rosul pe care --expo il vede pe NEF-ul sursa, prin libraw.
        /// </summary>
        internal CfaPattern ForRowOrder(string rowOrder, int height)
        {
            if (string.IsNullOrEmpty(rowOrder)) return this;
            if (!string.Equals(rowOrder.Trim(), "BOTTOM-UP", StringComparison.OrdinalIgnoreCase)) return this;
            return (height & 1) == 0 ? Shift(1, 0) : this;
        }

        /// <summary>
        /// Numele pozitiei: "R", "G1", "G2", "B". Verzii se numeroteaza in ordinea
        /// rasterului, fiindca sunt doi si trebuie sa se poata deosebi pe ecran - pe un
        /// mozaic corect au aproape acelasi nivel, si tocmai "aproape" e ce se citeste.
        /// </summary>
        internal string Name(int i)
        {
            char c = Colours[i];
            if (c != 'G') return c.ToString();
            return i < IndexOfSecondGreen ? "G1" : "G2";
        }

        int IndexOfSecondGreen
        {
            get
            {
                int seen = 0;
                for (int i = 0; i < 4; i++)
                    if (Colours[i] == 'G' && ++seen == 2) return i;
                return 4;   // nu se poate: Parse garanteaza doi
            }
        }
    }
}
