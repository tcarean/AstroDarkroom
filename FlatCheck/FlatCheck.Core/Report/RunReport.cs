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

// RunReport.cs - the text that frames the report of every mode: the header (version +
// copyright), the line that announces the saved map, and the wording that goes with the map.
//
// They sit here, not in FlatCheckCli, because they no longer belong to the console. The
// requirement for the WPF application is that the file "Rezultat testare.txt" hold EXACTLY what
// the console would have written - header included, so that a report copied somewhere says by
// itself which version and whose tool produced it. Two implementations of the same two lines
// would have diverged at the first rewording, and the divergence would have shown only in a
// report pasted on a forum.
//
// The scale label and the map legend have no caller in the console today - only the window
// uses them. They sit here all the same, because the rule is that no sentence for the user is
// written in a ViewModel: if the console gets a --scala tomorrow, it takes the same text, not a
// rewritten one. Both are pure functions, they do not write to a TextWriter, exactly like
// StructureReport.Advice - a value the console formats in columns and the window puts in a
// label.

using System.IO;

namespace FlatCheck
{
    /// <summary>
    /// The text every mode's report shares: the banner, the warranty notice, the line that
    /// announces a saved map, and the map's scale label and legend.
    /// </summary>
    internal static class RunReport
    {
        /// <summary>
        /// The first two lines of every run: the version and the copyright notice.
        ///
        /// The banner on the first line means a copied report says by itself which version
        /// produced it - that is why it appears at --help too, and on failed runs.
        /// The copyright on the second is required by the GPLv3 (see LICENSE).
        /// </summary>
        /// <param name="w">Where to write.</param>
        internal static void WriteBanner(TextWriter w)
        {
            w.WriteLine(VersionInfo.Banner);
            w.WriteLine(VersionInfo.Copyright);
        }

        /// <summary>
        /// The warranty and licence notice - GPLv3 section 5(d). The console prints it
        /// at --help (the banner carries only the copyright; five more lines on every
        /// report would drown the figures), the window shows it in "Despre". A single text for
        /// both, so that they cannot say two different things about the same licence.
        ///
        /// The lines are broken at ~70 columns for the console; the window joins them with a
        /// space and wraps them itself. The text is checked by Golden/help.txt, so a
        /// rewording here shows there too.
        /// </summary>
        internal static readonly string[] WarrantyLines =
        {
            "Acest program vine FARA NICIO GARANTIE, nici macar cea implicita de",
            "vandabilitate sau potrivire pentru un scop anume. E software liber:",
            "il poti redistribui si modifica in termenii GPLv3 (sau, la alegerea",
            "ta, orice versiune ulterioara). Textul complet e in fisierul LICENSE,",
            "sau la <" + LicenseUrl + ">.",
        };

        /// <summary>
        /// Where the full text of the licence is, for whoever does not have the LICENSE file at
        /// hand.
        /// </summary>
        internal const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";

        /// <summary>
        /// The announcement of the residual map. The caller knows the path - it chose where to
        /// write - so it cannot be generated inside the mode; but the wording is the same
        /// everywhere, and that is why it sits here.
        /// </summary>
        /// <param name="path">The path the map was written to, as it should be printed.</param>
        /// <param name="w">Where to write.</param>
        internal static void WriteMapSaved(string path, TextWriter w)
            => w.WriteLine($"\n    -> harta salvata: {path}");

        /// <summary>
        /// The label of a map scale: "auto", or the cap when the scale is fixed.
        /// <paramref name="capPct"/> null means the automatic scale.
        /// </summary>
        /// <param name="capPct">The fixed cap, percent either side of the model; null for
        /// automatic.</param>
        /// <returns>"auto", or "+/-N %" with at most one decimal.</returns>
        internal static string MapScaleLabel(double? capPct)
            => capPct is null ? "auto" : $"+/-{capPct.Value.ToString("0.#")} %";

        /// <summary>
        /// The legend under the map: what the ends of the colours mean, in percent against the
        /// model. The interval ACTUALLY DISPLAYED is given, because on the automatic scale the
        /// ends differ from one image to another - and that was exactly what could not be told
        /// on screen.
        /// </summary>
        /// <param name="r">The range the map was drawn with, in the raster's units around
        /// 1.0.</param>
        /// <returns>One line naming blue, white and red with their percentages.</returns>
        internal static string MapLegend(PngWriter.MapRange r)
        {
            string lo = ((r.Lo - PngWriter.Mid) * 100).ToString("+0.0;-0.0");
            string hi = ((r.Hi - PngWriter.Mid) * 100).ToString("+0.0;-0.0");
            return $"albastru = {lo} %   alb = 0 (exact pe model)   rosu = {hi} %";
        }
    }
}
