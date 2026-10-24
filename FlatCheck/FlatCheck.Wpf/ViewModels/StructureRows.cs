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

// StructureRows.cs - the five test rows of the structure battery, built once for the three tabs
// that show them.
//
// WHY IT EXISTS. The Structure, Ratio and Netezire tabs show THE SAME battery - Ratio runs it on
// the combined master (A+B)/2, Netezire on the master before and after smoothing, but the figures,
// the labels and the verdicts are the same, because it is literally the same analyzer. (The
// informative rows go to Structure and Ratio.) Before this file each tab composed them by itself:
// Structure from an ItemsControl over MetricRow, Ratio from five Grid blocks hand-written in XAML,
// with the labels as literals. The consequence was exactly what could be expected - when
// Structure got descriptions under every row, Ratio was left without, and when the fifth test
// appeared it had to be added in two places.
//
// So the rows are built here, and the tabs only show them. The text is still not written here:
// the labels belong to the rows, but the explanations come from StructureReport.TestNote, in the
// core, so they are the same if tomorrow the console asks for them too.

using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>The structure battery's rows - the five tests and the informative quantities -
    /// built from a <c>StructureResult</c> for every tab that shows them.</summary>
    internal static class StructureRows
    {
        /// <summary>
        /// The five deviation tests, in the order of the console report.
        ///
        /// The order matters: the report has this one, and whoever compares the screen with a
        /// report pasted on a forum must find the rows the same. The tests check it.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <returns>Five rows, each with its value in percent, its verdict and its note; the
        /// residual is the key row.</returns>
        internal static IReadOnlyList<MetricRow> Tests(StructureResult r) => new[]
        {
            new MetricRow
            {
                Label = "imprastiere 4 colturi",
                Value = Pct(r.CornerSpreadPct),
                Verdict = r.CornerVerdict,
                // the only row with figures of its own under it: the four corners, through the
                // same ratio as in the report
                Note = Note(nameof(r.CornerSpreadPct)) + "\n" + CornerNote(r),
            },
            new MetricRow
            {
                Label = "asimetrie azimutala max",
                Value = Pct(r.AzimuthalMaxPct),
                Verdict = r.AzimuthalVerdict,
                Note = Note(nameof(r.AzimuthalMaxPct)),
            },
            new MetricRow
            {
                Label = "reziduu dupa model radial",
                Value = Pct(r.ResidualPtpPct),
                Verdict = r.ResidualVerdict,
                IsKey = true,
                Note = Note(nameof(r.ResidualPtpPct)),
            },
            new MetricRow
            {
                Label = "inclinare (gradient)",
                Value = Pct(r.TiltPct),
                Verdict = r.TiltVerdict,
                Note = Note(nameof(r.TiltPct)),
            },
            new MetricRow
            {
                Label = "pata maxima (o dala)",
                Value = Pct(r.MaxTilePct),
                Verdict = r.MaxTileVerdict,
                Note = Note(nameof(r.MaxTilePct)),
            },
        };

        /// <summary>
        /// The quantities that are NOT tests, in the report's order: the vignetting, the offset of
        /// the illumination centre, the local spots. No verdict, so no pill.
        ///
        /// Everything the console prints under "INFORMATIV" in WriteBody, MINUS the line of ADU
        /// levels - that one sits in the structure mode's header, not in the body, because on the
        /// Ratio mode's combined master (normalised to median 1) it would mean nothing. That is
        /// why the levels stay in StructureTabViewModel, and the rows here go to both tabs.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <returns>Three rows without a verdict: vignetting in stops, the centre offset in
        /// percent (or "nedeterminat"), the local spots.</returns>
        internal static IReadOnlyList<MetricRow> Informative(StructureResult r) => new[]
        {
            new MetricRow
            {
                Label = "vignetare centru -> colt",
                Value = $"{r.VignetteStops:F2} stopuri",
                Note = $"coltul e {r.CornerMean / r.CentreMedian * 100:F0} % din centru; "
                     + "e o proprietate a obiectivului, nu o eroare",
            },
            new MetricRow
            {
                Label = "offset centru iluminare",
                Value = r.CentroidValid ? Pct(r.CentroidOffsetPct) : "nedeterminat",
                Note = r.CentroidValid
                    ? $"varf ({r.FitCx:F0}, {r.FitCy:F0}) vs centru senzor ({r.SensorCx:F0}, {r.SensorCy:F0}); "
                      + "raport gradient/curbura, deci NU e scale-invariant - amplitudinea o da inclinarea"
                    : "varf in afara cadrului sau fit degenerat",
            },
            new MetricRow
            {
                Label = "pete locale (praf etc.)",
                Value = StructureReport.LocalSpotValue(r),
                // The explanation comes from the core, so it is the same as the one in the saved
                // report - the threshold included, which is read from Th and not written here.
                Note = StructureReport.LocalSpotNote().Replace('\n', ' '),
            },
        };

        /// <summary>
        /// A row's explanation. It goes through the core, so there is no second wording in the
        /// window; if one were missing, the row would stay without a description, which did
        /// happen with the azimuthal asymmetry and with the tilt.
        /// </summary>
        /// <param name="metric">The <c>StructureResult</c> field's name, as
        /// <c>nameof</c> gives it.</param>
        /// <returns>The core's note for that test.</returns>
        static string Note(string metric) => StructureReport.TestNote(metric);

        /// <summary>The four corners, as a percentage of their mean - the same as in the
        /// report.</summary>
        /// <param name="r">The structure result.</param>
        /// <returns>One line, each corner's name and percentage separated by three
        /// spaces.</returns>
        static string CornerNote(StructureResult r)
            => string.Join("   ", r.Corners.Select(
                c => $"{c.Key} {c.Value / r.CornerMean * 100:F1} %"));

        /// <summary>NaN is written "n/a", not "NaN %": it is a degenerate input, not a
        /// measurement.</summary>
        /// <param name="v">The value, in percent.</param>
        /// <returns>The value with two decimals and " %", or "n/a".</returns>
        internal static string Pct(double v) => double.IsNaN(v) ? "n/a" : $"{v:F2} %";
    }
}
