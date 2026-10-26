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
using static FlatCheck.Ui;

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
        /// <param name="rainbow">True when the picture written is in false colours: the display
        /// line says so and the legend is printed.</param>
        internal static void Write(HistEqResult r, string path, TextWriter w, bool rainbow = false)
        {
            w.WriteLine(T("== HISTOGRAM EQUALISATION (Siril's \"Histogram\" view) ==",
                          "== EGALIZARE DE HISTOGRAMA (vederea \"Histogram\" din Siril) =="));
            w.WriteLine(T($"file:       {path}", $"fisier:     {path}"));
            w.WriteLine(T($"frame:      {FrameLine(r)}", $"cadru:      {FrameLine(r)}"));
            w.WriteLine(T($"display:    {DisplayLine(r, rainbow)}", $"afisare:    {DisplayLine(r, rainbow)}"));
            w.WriteLine();
            w.WriteLine(T("channel    input range                    levels used",
                          "canal      interval intrare               niveluri folosite"));
            for (int i = 0; i < r.Channels.Length; i++)
                w.WriteLine("  " + ChannelLine(r, i));
            w.WriteLine();
            if (rainbow)
                w.WriteLine(T("LEGEND: ", "LEGENDA: ") + RainbowLegend());
            w.WriteLine(T("INFORMATIVE: ", "INFORMATIV: ") + Caveat());
        }

        /// <summary>
        /// The legend of the false colours, under the picture and in the report: which end of
        /// Siril's table is which, and that the RGB view is not coloured (ADR 0063).
        /// </summary>
        /// <returns>One sentence, in the run's language.</returns>
        internal static string RainbowLegend()
            => T("false colours, Siril's rainbow table on one layer: magenta is the lowest level, then "
                 + "blue, cyan, green, yellow, and red the highest; the colours are ranks, not values. The "
                 + "RGB view keeps its own colours - the table applies to one layer only.",
                 "culori false, tabelul rainbow din Siril pe un singur strat: magenta e nivelul cel mai jos, apoi "
                 + "albastru, cyan, verde, galben, iar rosu cel mai sus; culorile sunt ranguri, nu valori. "
                 + "Vederea RGB isi pastreaza culorile ei - tabelul se aplica pe un singur strat.");

        /// <summary>
        /// "6000 x 4000, CFA (ordine RGGB, din libraw)" / "..., CFA (ordine necunoscuta)" / "...,
        /// mono/procesat".
        /// </summary>
        /// <param name="r">The equalised result.</param>
        /// <returns>The frame's size and kind.</returns>
        internal static string FrameLine(HistEqResult r)
        {
            string kind = !r.IsCfa
                ? T("mono/processed (no CFA mosaic)", "mono/procesat (fara mozaic CFA)")
                : r.Cfa != null
                    ? T($"CFA (order {r.Cfa.Letters}, from {r.Cfa.Source})", $"CFA (ordine {r.Cfa.Letters}, din {r.Cfa.Source})")
                    : T("CFA (order unknown - the file does not say)", "CFA (ordine necunoscuta - fisierul n-o spune)");
            return $"{r.SourceW} x {r.SourceH}, {kind}";
        }

        /// <summary>
        /// "1500 x 1000 (bloc 2x2 pe canal), 65536 cosuri pe canal", plus ", culori false
        /// (tabelul rainbow din Siril)" when the picture is coloured.
        /// </summary>
        /// <param name="r">The equalised result.</param>
        /// <param name="rainbow">True when the picture written is in false colours.</param>
        /// <returns>The shrunk size, the block, the bin count and, when coloured, the table.</returns>
        internal static string DisplayLine(HistEqResult r, bool rainbow = false)
        {
            string shrink = r.Shrink > 1
                ? T($"{r.Shrink}x{r.Shrink} block per channel", $"bloc {r.Shrink}x{r.Shrink} pe canal")
                : T("not shrunk", "nemicsorat");
            string colours = rainbow
                ? T(", false colours (Siril's rainbow table)", ", culori false (tabelul rainbow din Siril)")
                : "";
            return T($"{r.W} x {r.H} ({shrink}), {HistEqLut.Bins} bins per channel{colours}",
                     $"{r.W} x {r.H} ({shrink}), {HistEqLut.Bins} cosuri pe canal{colours}");
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
            string range = c.NonZero == 0 ? T("(zeros only)", "(numai zerouri)") : $"{c.Min:0.###} .. {c.Max:0.###}";
            return $"{c.Label,-9}  {range,-30} {c.LevelsUsed,3} / 256";
        }

        /// <summary>
        /// The mode's only sentence of MEANING. It sits under the picture and in the report.
        /// </summary>
        /// <returns>The caveat: the picture shows the shape, not the size.</returns>
        internal static string Caveat()
            => T("the mode gives no verdict. The picture shows the SHAPE of what is not uniform - not how large it is: "
                 + "every grey level gets the same number of pixels, so a ripple of 0.5 % and a "
                 + "gradient of 10 % come out the same, and a perfect flat shows its own noise at maximum "
                 + "contrast. The figures are in the structure mode.",
                 "modul nu da verdict. Poza arata FORMA a ceea ce nu e uniform - nu si cat de mare e: "
                 + "fiecare nivel de gri primeste acelasi numar de pixeli, deci o unda de 0.5 % si un "
                 + "gradient de 10 % ies la fel, iar un flat perfect isi arata propriul zgomot la contrast "
                 + "maxim. Cifrele sunt in modul de structura.");

        /// <summary>
        /// What it means to put the master beside a frame it was made from. Screen text; the
        /// console has nowhere to put it, because it takes a single file.
        /// </summary>
        /// <returns>One paragraph.</returns>
        internal static string MasterVersusFrame()
            => T("The master is the stack of the frames below. A shape that shows on a frame and is missing "
                 + "from the master was transient and was averaged out; one that shows on both is fixed to the "
                 + "setup, and that is the one the figures of the Structure tab measure. The equalisation being by ranks, "
                 + "the master's lower noise makes a shared shape look CLEARER there, not "
                 + "weaker.",
                 "Masterul e stiva cadrelor de mai jos. O forma care se vede pe un cadru si lipseste "
                 + "de pe master a fost trecatoare si s-a mediat; una care se vede pe amandoua e fixa pe "
                 + "montaj, si pe ea o masoara cifrele din tabul Structura. Egalizarea fiind pe ranguri, "
                 + "zgomotul mai mic al masterului face o forma comuna sa para acolo mai CLARA, nu mai "
                 + "slaba.");

        /// <summary>
        /// What it means to put the smoothed flat beside the master it was made from. Screen
        /// text too. The last sentence is the one people ask about: the smoothed one has its
        /// noise removed, so the rank equalisation stretches what is LEFT - however small - over
        /// the whole grey scale, and a cleaner flat looks noisier, not the other way round.
        /// </summary>
        /// <returns>One paragraph.</returns>
        internal static string SmoothedVersusMaster()
            => T("The smoothed flat is the master above with the texture, the dust and the compact spots "
                 + "removed. A shape that shows on the master and is missing here was removed by the "
                 + "smoothing; one that shows on both is smooth at the chosen scale and stayed - "
                 + "lens or source, the picture does not say; --ratio does. The equalisation being by ranks, "
                 + "what is left on the smoothed one, however small in amplitude, comes out at maximum contrast: a "
                 + "smoothed flat that looks NOISIER than the master is the expected picture, not a defect.",
                 "Flat-ul netezit e masterul de deasupra cu textura, praful si petele compacte "
                 + "scoase. O forma care se vede pe master si lipseste de aici a fost scoasa de "
                 + "netezire; una care se vede pe amandoua e neteda la scara aleasa si a ramas - "
                 + "obiectiv sau sursa, poza nu spune; --ratio spune. Egalizarea fiind pe ranguri, "
                 + "ce a ramas pe netezit, oricat de mic in amplitudine, iese la contrast maxim: un "
                 + "netezit care pare mai ZGOMOTOS decat masterul e poza asteptata, nu un defect.");

        /// <summary>
        /// Which view is on screen, as the caption under the picture. The size is the
        /// full-resolution one, because that is the one the window shows: the mosaic the size
        /// of the sensor, a position or the composite at half on each side.
        /// </summary>
        /// <param name="r">The equalised result.</param>
        /// <param name="viewLabel">The view's key, the same in both languages: "RGB", "Mozaic",
        /// "mono" or a channel's label.</param>
        /// <returns>What the view is, and its size in pixels.</returns>
        internal static string ViewCaption(HistEqResult r, string viewLabel)
        {
            string what = viewLabel switch
            {
                "RGB" when r.Cfa != null => T("the four positions, each equalised on its own distribution, put in their colours",
                                              "cele patru pozitii egalizate fiecare pe repartitia ei, puse in culorile lor"),
                "RGB" => T("the mean of the four positions, each equalised on its own distribution (the order of the colours is not known, so no colour)",
                           "media celor patru pozitii egalizate fiecare pe repartitia ei (ordinea culorilor nu se stie, deci fara culoare)"),
                "Mozaic" => T("the whole mosaic equalised as a single layer - exactly what Siril shows on an undebayered file",
                              "tot mozaicul egalizat ca un singur strat - exact ce arata Siril pe un fisier nedebayerizat"),
                "mono" => T("the frame equalised on its own distribution", "cadrul egalizat pe repartitia lui"),
                _ => T($"the position {viewLabel} equalised on its own distribution", $"pozitia {viewLabel} egalizata pe repartitia ei"),
            };
            bool mosaic = viewLabel == "Mozaic";
            int w = mosaic ? r.SourceW : r.FullW, h = mosaic ? r.SourceH : r.FullH;
            return $"{what}; {w} x {h} px.";
        }
    }
}
