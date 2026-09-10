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

// HistEqReport.cs - textul modului 5. Nu exista verdict, deci nu exista nici scara si
// nici sfat: raportul spune ce s-a egalizat, la ce marime, si - propozitia care conteaza -
// ce NU se poate citi din poza. Aia e Caveat(), expusa ca valoare fiindca fereastra o
// pune sub imagine, iar regula e ca nicio propozitie pentru utilizator nu se scrie intr-un
// ViewModel.

using System.IO;
using System.Linq;

namespace FlatCheck
{
    internal static class HistEqReport
    {
        internal static void Write(HistEqResult r, string path, TextWriter w)
        {
            w.WriteLine("== EGALIZARE DE HISTOGRAMA (vederea \"Histogram\" din Siril) ==");
            w.WriteLine($"fisier:     {path}");
            w.WriteLine($"cadru:      {FrameLine(r)}");
            w.WriteLine($"afisare:    {DisplayLine(r)}");
            w.WriteLine();
            w.WriteLine("canal      interval intrare               niveluri folosite");
            for (int i = 0; i < r.Channels.Length; i++)
                w.WriteLine("  " + ChannelLine(r, i));
            w.WriteLine();
            w.WriteLine("INFORMATIV: " + Caveat());
        }

        /// <summary>"6000 x 4000, CFA (ordine RGGB, din libraw)" / "..., CFA (ordine necunoscuta)" / "..., mono/procesat".</summary>
        internal static string FrameLine(HistEqResult r)
        {
            string kind = !r.IsCfa
                ? "mono/procesat (fara mozaic CFA)"
                : r.Cfa != null
                    ? $"CFA (ordine {r.Cfa.Letters}, din {r.Cfa.Source})"
                    : "CFA (ordine necunoscuta - fisierul n-o spune)";
            return $"{r.SourceW} x {r.SourceH}, {kind}";
        }

        /// <summary>"1500 x 1000 (bloc 2x2 pe canal), 65536 cosuri pe canal".</summary>
        internal static string DisplayLine(HistEqResult r)
        {
            string shrink = r.Shrink > 1 ? $"bloc {r.Shrink}x{r.Shrink} pe canal" : "nemicsorat";
            return $"{r.W} x {r.H} ({shrink}), {HistEqLut.Bins} cosuri pe canal";
        }

        /// <summary>Un rand din tabel: eticheta, intervalul de intrare, cate niveluri de iesire s-au folosit.</summary>
        internal static string ChannelLine(HistEqResult r, int i)
        {
            HistEqChannel c = r.Channels[i];
            string range = c.NonZero == 0 ? "(numai zerouri)" : $"{c.Min:0.###} .. {c.Max:0.###}";
            return $"{c.Label,-9}  {range,-30} {c.LevelsUsed,3} / 256";
        }

        /// <summary>Singura propozitie de INTELES a modului. Sta sub poza si in raport.</summary>
        internal static string Caveat()
            => "modul nu da verdict. Poza arata FORMA a ceea ce nu e uniform - nu si cat de mare e: "
             + "fiecare nivel de gri primeste acelasi numar de pixeli, deci o unda de 0.5 % si un "
             + "gradient de 10 % ies la fel, iar un flat perfect isi arata propriul zgomot la contrast "
             + "maxim. Cifrele sunt in modul de structura.";

        /// <summary>
        /// Ce inseamna sa pui masterul langa un cadru din care s-a facut. Text de ecran;
        /// consola n-are unde sa-l puna, fiindca primeste un singur fisier.
        /// </summary>
        internal static string MasterVersusFrame()
            => "Masterul e stiva cadrelor de mai jos. O forma care se vede pe un cadru si lipseste "
             + "de pe master a fost trecatoare si s-a mediat; una care se vede pe amandoua e fixa pe "
             + "montaj, si pe ea o masoara cifrele din tabul Structura. Egalizarea fiind pe ranguri, "
             + "zgomotul mai mic al masterului face o forma comuna sa para acolo mai CLARA, nu mai "
             + "slaba.";

        /// <summary>
        /// Ce vedere e pe ecran, ca legenda sub poza. Marimea e cea la rezolutie intreaga,
        /// fiindca pe aia o arata fereastra: mozaicul cat senzorul, o pozitie sau
        /// compozitul la jumatate pe fiecare latura.
        /// </summary>
        internal static string ViewCaption(HistEqResult r, string viewLabel)
        {
            string what = viewLabel switch
            {
                "RGB" when r.Cfa != null => "cele patru pozitii egalizate fiecare pe repartitia ei, puse in culorile lor",
                "RGB" => "media celor patru pozitii egalizate fiecare pe repartitia ei (ordinea culorilor nu se stie, deci fara culoare)",
                "Mozaic" => "tot mozaicul egalizat ca un singur strat - exact ce arata Siril pe un fisier nedebayerizat",
                "mono" => "cadrul egalizat pe repartitia lui",
                _ => $"pozitia {viewLabel} egalizata pe repartitia ei",
            };
            bool mosaic = viewLabel == "Mozaic";
            int w = mosaic ? r.SourceW : r.FullW, h = mosaic ? r.SourceH : r.FullH;
            return $"{what}; {w} x {h} px.";
        }
    }
}
