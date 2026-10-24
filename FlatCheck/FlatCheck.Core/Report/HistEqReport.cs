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

// HistEqReport.cs - the text of mode 5. There is no verdict, so there is no scale and no
// advice either: the report says what was equalised, at what size, and - the sentence that
// matters - what CANNOT be read from the picture. That is Caveat(), exposed as a value because
// the window puts it under the image, and the rule is that no sentence for the user is written
// in a ViewModel.

using System.IO;

namespace FlatCheck
{
    /// <summary>
    /// The words of <c>--histeq</c>: the console report of a <see cref="HistEqResult"/> and the
    /// captions the window puts under its pictures.
    /// </summary>
    internal static class HistEqReport
    {
        /// <summary>
        /// Writes the console report: the file, the frame, the display size, one row per
        /// channel, and the caveat.
        /// </summary>
        /// <param name="r">The equalised result.</param>
        /// <param name="path">The file's path, as the report prints it.</param>
        /// <param name="w">Where to write.</param>
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

        /// <summary>
        /// "6000 x 4000, CFA (ordine RGGB, din libraw)" / "..., CFA (ordine necunoscuta)" / "...,
        /// mono/procesat".
        /// </summary>
        /// <param name="r">The equalised result.</param>
        /// <returns>The frame's size and kind.</returns>
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
        /// <param name="r">The equalised result.</param>
        /// <returns>The shrunk size, the block and the bin count.</returns>
        internal static string DisplayLine(HistEqResult r)
        {
            string shrink = r.Shrink > 1 ? $"bloc {r.Shrink}x{r.Shrink} pe canal" : "nemicsorat";
            return $"{r.W} x {r.H} ({shrink}), {HistEqLut.Bins} cosuri pe canal";
        }

        /// <summary>
        /// A row of the table: the label, the input interval, how many output levels were used.
        /// </summary>
        /// <param name="r">The equalised result.</param>
        /// <param name="i">The channel's index in <see cref="HistEqResult.Channels"/>.</param>
        /// <returns>The row, in fixed-width columns.</returns>
        internal static string ChannelLine(HistEqResult r, int i)
        {
            HistEqChannel c = r.Channels[i];
            string range = c.NonZero == 0 ? "(numai zerouri)" : $"{c.Min:0.###} .. {c.Max:0.###}";
            return $"{c.Label,-9}  {range,-30} {c.LevelsUsed,3} / 256";
        }

        /// <summary>
        /// The mode's only sentence of MEANING. It sits under the picture and in the report.
        /// </summary>
        /// <returns>The caveat: the picture shows the shape, not the size.</returns>
        internal static string Caveat()
            => "modul nu da verdict. Poza arata FORMA a ceea ce nu e uniform - nu si cat de mare e: "
             + "fiecare nivel de gri primeste acelasi numar de pixeli, deci o unda de 0.5 % si un "
             + "gradient de 10 % ies la fel, iar un flat perfect isi arata propriul zgomot la contrast "
             + "maxim. Cifrele sunt in modul de structura.";

        /// <summary>
        /// What it means to put the master beside a frame it was made from. Screen text; the
        /// console has nowhere to put it, because it takes a single file.
        /// </summary>
        /// <returns>One paragraph.</returns>
        internal static string MasterVersusFrame()
            => "Masterul e stiva cadrelor de mai jos. O forma care se vede pe un cadru si lipseste "
             + "de pe master a fost trecatoare si s-a mediat; una care se vede pe amandoua e fixa pe "
             + "montaj, si pe ea o masoara cifrele din tabul Structura. Egalizarea fiind pe ranguri, "
             + "zgomotul mai mic al masterului face o forma comuna sa para acolo mai CLARA, nu mai "
             + "slaba.";

        /// <summary>
        /// What it means to put the smoothed flat beside the master it was made from. Screen
        /// text too. The last sentence is the one people ask about: the smoothed one has its
        /// noise removed, so the rank equalisation stretches what is LEFT - however small - over
        /// the whole grey scale, and a cleaner flat looks noisier, not the other way round.
        /// </summary>
        /// <returns>One paragraph.</returns>
        internal static string SmoothedVersusMaster()
            => "Flat-ul netezit e masterul de deasupra cu textura, praful si petele compacte "
             + "scoase. O forma care se vede pe master si lipseste de aici a fost scoasa de "
             + "netezire; una care se vede pe amandoua e neteda la scara aleasa si a ramas - "
             + "obiectiv sau sursa, poza nu spune; --ratio spune. Egalizarea fiind pe ranguri, "
             + "ce a ramas pe netezit, oricat de mic in amplitudine, iese la contrast maxim: un "
             + "netezit care pare mai ZGOMOTOS decat masterul e poza asteptata, nu un defect.";

        /// <summary>
        /// Which view is on screen, as the caption under the picture. The size is the
        /// full-resolution one, because that is the one the window shows: the mosaic the size
        /// of the sensor, a position or the composite at half on each side.
        /// </summary>
        /// <param name="r">The equalised result.</param>
        /// <param name="viewLabel">The view's label: "RGB", "Mozaic", "mono" or a channel's
        /// label.</param>
        /// <returns>What the view is, and its size in pixels.</returns>
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
