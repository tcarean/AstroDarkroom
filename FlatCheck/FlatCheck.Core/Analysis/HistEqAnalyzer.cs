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

// HistEqAnalyzer.cs - modul 5: cadrul vazut prin egalizare de histograma, adica exact
// ce arata Siril 1.4.4 cand alegi "Histogram" la modul de afisare.
//
// CE FACE SIRIL, citit din sursa (src/gui/image_display.c, remap(), ramura
// HISTEQ_DISPLAY; src/gui/histogram.c, computeHisto()):
//
//   - o histograma pe STRAT, cu 65536 de cosuri, din care pixelii egali cu zero lipsesc;
//   - LUT[i] = round_to_BYTE(cumulat(i) / nr_pixeli * 255), cu cumulatul incluzand cosul
//     i, LUT[0] = 0 si nr_pixeli = TOTI pixelii, cei zero inclusiv;
//   - pixelul afisat = LUT[valoare]; float-ul e intai cuantizat cu round(v * 65535);
//   - cursoarele lo/hi sunt stinse in modul asta.
//
// Deci nu e un grafic: e imaginea insasi, trecuta prin functia ei de repartitie. Fiecare
// nivel de gri primeste acelasi numar de pixeli. Pe un flat - o distributie ingusta fata
// de scala fisierului - functia e abrupta exact pe intervalul vignetarii: masurat pe un
// sintetic cu 5 % vignetare, o unda de 0.3 % e o treime de nivel in afisarea liniara si
// ~15 niveluri egalizata (ATinyRippleInvisibleInTheLinearViewBecomesManyGreyLevels).
// Pretul: AMPLITUDINEA DISPARE prin constructie. O unda de 0.5 % si
// un gradient de 10 % cu aceeasi forma ies la fel, iar un flat perfect isi arata propriul
// zgomot la contrast maxim. Modul arata FORMA; cifrele sunt in modul de structura. Nu
// voteaza niciodata si nu are prag.
//
// CE DIFERA fata de Siril, si de ce:
//
//   - Cosurile sunt pe [min, max] ale pixelilor nenuli ai canalului, nu pe 0..65535
//     absolut: rasterele noastre sunt double la scala oricare a fisierului (0..4095,
//     0..65535, 0..1). Pe date intregi maparea e identica - doua valori ADU diferite nu
//     cad niciodata in acelasi cos, iar repartitia la o valoare e numarul de pixeli pana
//     la ea inclusiv, in ambele feluri. Pe float e mai fina decat cuantizarea lui Siril la
//     65535 de trepte, ceea ce e varianta mai exacta. Invariant la castig prin constructie,
//     ca orice cifra din unealta.
//   - Egalizarea se face la rezolutie intreaga, PE CANAL, apoi rasterul pe 8 biti se
//     micsoreaza pentru afisare cu aceeasi regula k ca Img.Prepare. Alternativa - block-mean
//     intai, egalizare dupa - da o intindere mai puternica (zgomotul se mediaza inainte de
//     repartitie), dar nu e ce arata Siril, si cerinta era poza lui Siril.
//   - Rotunjirea e cea a lui round_to_BYTE: (byte)(x + 0.5), nu Math.Round, care rotunjeste
//     la par. Un vector calculat de mana o fixeaza in teste.
//
// VEDERILE. Pe un cadru CFA, cele patru pozitii se egalizeaza fiecare pe repartitia ei
// (Siril pe o imagine debayerizata: fiecare strat pe histograma lui), si se ofera
// compozitul RGB (R = R, G = media celor doi verzi egalizati, B = B), fiecare pozitie ca
// gri, si "mozaicul" - tot cadrul egalizat ca un singur strat, adica literal Siril pe un
// fisier nedebayerizat, cum deschide el un raw implicit. Pe mono, o singura vedere.
//
// DOUA REZOLUTII. Fiecare raster exista si intreg (Full, MosaicFull, CompositeFullRgb),
// pentru fereastra, care il arata direct ca bitmap - la 100 % un pixel de ecran e un
// pixel de senzor - si micsorat cu regula lui Img.Prepare, pentru PNG-ul consolei si
// pentru teste. Cel micsorat e media blocurilor celui intreg, nu o a doua socoteala.
//
// MASURAT (Release, D3300, 6016 x 4016, pe aceleasi fisiere ca modurile de comparatie):
//   - master float FITS: ~2-3 s si 690 MB varf, fata de 3.3 s / 611 MB la modul de structura;
//   - NEF: ~2-3 s si 516 MB varf, fata de 3.2 s / 1.1 GB la --expo.
// Rasterele intregi pastrate costa ~18 MB pe un cadru de 24 MP (au fost 672 / 498 MB
// fara ele). Incarcarea domina (~0.5 s FITS, ~1 s libraw); analiza in sine e ~1 s: doua
// treceri peste date pe pozitie pentru histograma si tabel, una pentru mozaic, si
// micsorarea. Doua lucruri au fost scumpe in prima varianta si sunt notate la locul lor:
// mediana prin sortare (HistEqLut.Median) si micsorarea prin rastere double (Shrink).

using System;
using System.Linq;

namespace FlatCheck
{
    /// <summary>Un canal egalizat: rasterul de afisat si de unde a venit.</summary>
    internal sealed class HistEqChannel
    {
        /// <summary>0..3 = pozitia Bayer, in ordinea Mosaic.PositionOf; 0 pe mono.</summary>
        internal int Position;

        /// <summary>"R", "G1", "G2", "B" cand se stie ordinea; "CFA[0,1]" cand nu; "mono" pe mono.</summary>
        internal string Label;

        /// <summary>'R', 'G' sau 'B'; '\0' cand fisierul n-a spus ordinea sau pe mono.</summary>
        internal char Colour;

        /// <summary>Marimea rasterului micsorat - cel pe care il scrie consola.</summary>
        internal int H, W;

        /// <summary>H*W niveluri de gri, rand cu rand; media blocurilor din Full.</summary>
        internal byte[] Equalised;

        /// <summary>Marimea rasterului la rezolutie intreaga (a pozitiei, adica H/2 x W/2 din senzor pe CFA).</summary>
        internal int FullH, FullW;

        /// <summary>
        /// Rasterul egalizat la rezolutie intreaga, FullH*FullW. Fereastra il arata pe
        /// acesta, direct ca bitmap, ca la 100 % un pixel de pe ecran sa fie un pixel de
        /// senzor; Equalised e derivat din el.
        /// </summary>
        internal byte[] Full;

        /// <summary>Intervalul de intrare al pixelilor nenuli, in unitatile fisierului.</summary>
        internal double Min, Max;

        /// <summary>Cati pixeli au intrat in histograma (Siril sare zerourile; noi la fel).</summary>
        internal long NonZero;

        /// <summary>Cate din cele 256 de niveluri de iesire au primit macar un pixel, inainte de micsorare.</summary>
        internal int LevelsUsed;
    }

    internal sealed class HistEqResult
    {
        internal bool IsCfa;
        internal double CfaSpread;
        internal CfaPattern Cfa;
        internal int SourceH, SourceW;

        /// <summary>Factorul de micsorare al fiecarui canal (1 = nemicsorat).</summary>
        internal int Shrink;

        /// <summary>4 pe un cadru CFA, 1 pe unul mono. Niciodata null.</summary>
        internal HistEqChannel[] Channels;

        /// <summary>Marimea comuna a tuturor vederilor micsorate.</summary>
        internal int H, W;

        /// <summary>H*W*3. RGB pe CFA cu ordine cunoscuta; gri (triplat) pe mono sau ordine necunoscuta.</summary>
        internal byte[] CompositeRgb;

        /// <summary>Mozaicul egalizat ca un singur strat, H*W gri; null pe mono (acolo canalul E mozaicul).</summary>
        internal byte[] Mosaic;

        /// <summary>Marimea la rezolutie intreaga a canalelor si a compozitului.</summary>
        internal int FullH, FullW;

        /// <summary>Compozitul la rezolutie intreaga, FullH*FullW*3.</summary>
        internal byte[] CompositeFullRgb;

        /// <summary>Mozaicul la rezolutie intreaga, SourceH*SourceW; null pe mono.</summary>
        internal byte[] MosaicFull;

        internal int ChannelCount => Channels.Length;
    }

    /// <summary>
    /// Tabelul de transfer al unui canal, construit exact ca in Siril. Se aplica pe
    /// valori double: zero ramane zero (Siril il sare la histograma), restul trece prin
    /// cosul lui.
    /// </summary>
    internal sealed class HistEqLut
    {
        internal const int Bins = 65536;

        internal readonly byte[] Table = new byte[Bins];
        internal readonly double Min, Max;
        internal readonly long NonZero, Total;

        /// <summary>Histograma din care s-a construit tabelul; pastrata pentru Median().</summary>
        internal readonly long[] Counts = new long[Bins];

        HistEqLut(double min, double max, long nonZero, long total)
        {
            Min = min; Max = max; NonZero = nonZero; Total = total;
        }

        /// <summary>
        /// Mediana pixelilor nenuli, citita din histograma, la latimea unui cos (sub un
        /// ADU pe orice fisier pe 12 sau 16 biti). Exista fiindca decizia CFA are nevoie de
        /// patru mediane pe poziție, iar Stats.Percentile le-ar fi sortat pe fiecare - patru
        /// copii de 6 M de double pe un cadru de 24 MP, masurat la ~2 s din cele ~2.5 s ale
        /// analizei. Histograma e oricum construita; mediana din ea costa o trecere peste
        /// 65536 de coșuri.
        /// </summary>
        internal double Median()
        {
            if (NonZero == 0) return 0;
            long half = (NonZero + 1) / 2, cum = 0;
            for (int b = 0; b < Bins; b++)
            {
                cum += Counts[b];
                if (cum >= half) return Min + b * (Max - Min) / (Bins - 1);
            }
            return Max;
        }

        /// <summary>round_to_BYTE din Siril: (BYTE)(x + 0.5), taiat la 255.</summary>
        internal static byte RoundToByte(double x) => (byte)Math.Min(255, (int)(x + 0.5));

        internal int BinOf(double v)
        {
            if (Max <= Min) return 0;
            int b = (int)((v - Min) / (Max - Min) * (Bins - 1));
            return b < 0 ? 0 : b >= Bins ? Bins - 1 : b;
        }

        internal byte Apply(double v) => v == 0.0 ? (byte)0 : Table[BinOf(v)];

        internal static HistEqLut Build(double[] values)
        {
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            long nonZero = 0;
            foreach (double v in values)
            {
                if (v == 0.0) continue;
                nonZero++;
                if (v < min) min = v;
                if (v > max) max = v;
            }
            if (nonZero == 0) return new HistEqLut(0, 0, 0, values.Length);

            var lut = new HistEqLut(min, max, nonZero, values.Length);
            long[] counts = lut.Counts;
            foreach (double v in values)
                if (v != 0.0) counts[lut.BinOf(v)]++;

            // Cumulatul include cosul curent, ca in bucla lui Siril; numitorul e TOTI
            // pixelii, zerourile inclusiv - de asta un cadru cu multi pixeli morti nu
            // ajunge niciodata la 255, si nici noi.
            long cum = 0;
            double n = values.Length;
            for (int i = 0; i < Bins; i++)
            {
                cum += counts[i];
                lut.Table[i] = RoundToByte(cum / n * 255.0);
            }
            return lut;
        }
    }

    internal static class HistEqAnalyzer
    {
        /// <summary>Latura lunga tinta a rasterului afisat: aceeasi ca la Img.Prepare.</summary>
        internal const int TargetLong = 1200;

        /// <param name="cfa">
        /// Ordinea culorilor citita din fisier, sau null cand fisierul n-o spune. Numeste
        /// canalele si decide daca compozitul poate fi color; nu schimba niciun raster.
        /// </param>
        internal static HistEqResult Analyze(Img data, CfaPattern cfa, int targetLong = TargetLong)
        {
            bool canSplit = data.H >= 2 && data.W >= 2;
            Img[] positions = canSplit ? Mosaic.Split(data) : null;

            // Tabelele pe pozitie se construiesc intai, fiindca decizia CFA se ia din
            // medianele lor - aceeasi regula ca la --expo (Mosaic.LooksCfa pe max/min al
            // celor patru), cu mediana citita din histograma in loc de sortare. Pe CFA
            // aceleasi tabele egalizeaza apoi canalele; pe mono se arunca, si au costat
            // doua treceri peste date.
            double spread = 1.0;
            HistEqLut[] positionLuts = null;
            if (canSplit)
            {
                positionLuts = positions.Select(p => HistEqLut.Build(p.D)).ToArray();
                var meds = positionLuts.Select(l => l.Median()).ToArray();
                spread = meds.Max() / Math.Max(meds.Min(), 1e-9);
            }
            bool isCfa = canSplit && Mosaic.LooksCfa(spread);

            Img[] sources = isCfa ? positions : new[] { data };
            int k = ShrinkFactor(sources[0].H, sources[0].W, targetLong);

            var channels = new HistEqChannel[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                int h = sources[i].H, w = sources[i].W;
                HistEqLut lut = isCfa ? positionLuts[i] : HistEqLut.Build(sources[i].D);
                byte[] full = ApplyLut(lut, sources[i].D);
                // Rasterul pozitiei nu mai e nevoie dupa asta; pe CFA e o copie a datelor,
                // si patru copii tinute pana la sfarsit ar sta degeaba langa mozaic.
                if (isCfa) sources[i] = null;
                channels[i] = new HistEqChannel
                {
                    Position = i,
                    Label = isCfa ? Mosaic.Label(cfa, i) : "mono",
                    Colour = isCfa ? Mosaic.ColourOf(cfa, i) : '\0',
                    Min = lut.Min,
                    Max = lut.Max,
                    NonZero = lut.NonZero,
                    LevelsUsed = LevelsUsed(full),
                    Full = full,
                    FullH = h,
                    FullW = w,
                };
                (channels[i].Equalised, channels[i].H, channels[i].W) = Shrink(full, h, w, k);
            }

            var r = new HistEqResult
            {
                IsCfa = isCfa,
                CfaSpread = spread,
                Cfa = cfa,
                SourceH = data.H,
                SourceW = data.W,
                Shrink = k,
                Channels = channels,
                H = channels[0].H,
                W = channels[0].W,
                FullH = channels[0].FullH,
                FullW = channels[0].FullW,
            };
            r.CompositeRgb = Composite(r, full: false);
            r.CompositeFullRgb = Composite(r, full: true);

            if (isCfa)
            {
                // Tot cadrul ca un singur strat, micsorat cu 2k ca sa iasa cat canalele:
                // floor(floor(H/2)/k) == floor(H/(2k)), deci marimile chiar coincid.
                var lut = HistEqLut.Build(data.D);
                r.MosaicFull = ApplyLut(lut, data.D);
                (r.Mosaic, _, _) = Shrink(r.MosaicFull, data.H, data.W, 2 * k);
            }
            return r;
        }

        /// <summary>Regula lui Img.Prepare, cu trunchierea ei: 3000 -> 2, 2399 -> 1.</summary>
        internal static int ShrinkFactor(int h, int w, int targetLong)
            => Math.Max(1, (int)(Math.Max(h, w) / (double)targetLong));

        static byte[] ApplyLut(HistEqLut lut, double[] values)
        {
            var o = new byte[values.Length];
            for (int i = 0; i < values.Length; i++) o[i] = lut.Apply(values[i]);
            return o;
        }

        static int LevelsUsed(byte[] grey)
        {
            var seen = new bool[256];
            foreach (byte b in grey) seen[b] = true;
            return seen.Count(s => s);
        }

        /// <summary>
        /// Micsorare prin media blocurilor k x k, direct pe octeti - aceeasi aritmetica ca
        /// Img.BlockMean (marimi trunchiate, suma / k^2, sumele de intregi sunt exacte in
        /// double), fara rasterul double temporar. Prima varianta trecea prin Img si a fost
        /// masurata: pe masterul de 24 MP varful de memorie era 1.1 GB, fata de 611 MB la
        /// modul de structura pe acelasi fisier, fiindca mozaicul intreg ajungea un raster
        /// de 24 M de double pe langa datele incarcate. Pe octeti nu mai exista.
        /// </summary>
        static (byte[] Grey, int H, int W) Shrink(byte[] grey, int h, int w, int k)
        {
            if (k <= 1) return (grey, h, w);

            int oh = h / k, ow = w / k;
            var o = new byte[oh * ow];
            double inv = 1.0 / (k * k);
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    int s = 0;
                    for (int dy = 0; dy < k; dy++)
                    {
                        int row = (y * k + dy) * w + x * k;
                        for (int dx = 0; dx < k; dx++) s += grey[row + dx];
                    }
                    o[y * ow + x] = HistEqLut.RoundToByte(s * inv);
                }
            return (o, oh, ow);
        }

        /// <summary>
        /// Compozitul: R din pozitia rosie, G media celor doua verzi, B din cea albastra -
        /// cautate DUPA CULOARE, nu dupa pozitie, fiindca RGGB e doar ordinea cea mai
        /// frecventa. Fara ordine cunoscuta, media celor patru: o atribuire de culori ar fi
        /// o ghicire, si unealta nu ghiceste mozaicuri. Pe mono, griul triplat.
        /// </summary>
        static byte[] Composite(HistEqResult r, bool full)
        {
            // Aceeasi socoteala pe rasterele micsorate (consola) si pe cele intregi
            // (fereastra); alegerea sursei e singura diferenta.
            byte[] Of(HistEqChannel c) => full ? c.Full : c.Equalised;
            int n = full ? r.FullH * r.FullW : r.H * r.W;
            var rgb = new byte[n * 3];

            if (!r.IsCfa)
            {
                byte[] g = Of(r.Channels[0]);
                for (int i = 0; i < n; i++) { rgb[i * 3] = g[i]; rgb[i * 3 + 1] = g[i]; rgb[i * 3 + 2] = g[i]; }
                return rgb;
            }

            if (r.Cfa == null)
            {
                var all = r.Channels.Select(Of).ToArray();
                for (int i = 0; i < n; i++)
                {
                    int s = 0;
                    foreach (byte[] c in all) s += c[i];
                    byte m = HistEqLut.RoundToByte(s / 4.0);
                    rgb[i * 3] = m; rgb[i * 3 + 1] = m; rgb[i * 3 + 2] = m;
                }
                return rgb;
            }

            byte[] red = Of(r.Channels.First(c => c.Colour == 'R'));
            byte[] blue = Of(r.Channels.First(c => c.Colour == 'B'));
            var greens = r.Channels.Where(c => c.Colour == 'G').Select(Of).ToArray();
            for (int i = 0; i < n; i++)
            {
                rgb[i * 3] = red[i];
                rgb[i * 3 + 1] = HistEqLut.RoundToByte((greens[0][i] + greens[1][i]) / 2.0);
                rgb[i * 3 + 2] = blue[i];
            }
            return rgb;
        }
    }
}
