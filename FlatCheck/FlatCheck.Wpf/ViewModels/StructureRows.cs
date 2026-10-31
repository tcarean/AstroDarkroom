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

// StructureRows.cs - the five test rows of the structure battery, built once, in one place.
//
// WHY IT EXISTS. Three tabs of the time showed THE SAME battery - Structure, the Ratio tab and
// the Netezire tab, the last two since removed (ADR 0067, ADR 0080) - and the figures, the
// labels and the verdicts were the same, because it is literally the same analyzer. Before this
// file each tab composed them by itself: Structure from an ItemsControl over MetricRow, the
// Ratio tab from five Grid blocks hand-written in XAML, with the labels as literals. The
// consequence was exactly what could be expected - when Structure got descriptions under every
// row, Ratio was left without, and when the fifth test appeared it had to be added in two places.
//
// So the rows are built here, and the tabs only show them. The text is still not written here:
// the labels belong to the rows, but the explanations come from StructureReport.TestNote, in the
// core, so they are the same if tomorrow the console asks for them too.

using System.Collections.Generic;
using static FlatCheck.Ui;

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
                Label = T("spread of the 4 corners", "imprastiere 4 colturi"),
                Value = Pct(r.CornerSpreadPct),
                Verdict = r.CornerVerdict,
                // the only row with figures of its own under it: the four corners, through the
                // same ratio as in the report
                Note = Note(nameof(r.CornerSpreadPct)) + "\n" + StructureReport.CornerShares(r),
            },
            new MetricRow
            {
                Label = T("max azimuthal asymmetry", "asimetrie azimutala max"),
                Value = Pct(r.AzimuthalMaxPct),
                Verdict = r.AzimuthalVerdict,
                Note = Note(nameof(r.AzimuthalMaxPct)),
            },
            new MetricRow
            {
                Label = T("residual after the radial model", "reziduu dupa model radial"),
                Value = Pct(r.ResidualPtpPct),
                Verdict = r.ResidualVerdict,
                IsKey = true,
                Note = Note(nameof(r.ResidualPtpPct)),
            },
            new MetricRow
            {
                Label = T("tilt (gradient)", "inclinare (gradient)"),
                Value = Pct(r.TiltPct),
                Verdict = r.TiltVerdict,
                Note = Note(nameof(r.TiltPct)),
            },
            new MetricRow
            {
                Label = T("worst spot (one tile)", "pata maxima (o dala)"),
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
        /// levels - that one sits in the structure mode's header, not in the body, because on a
        /// raster normalised to median 1 it would mean nothing. That is why the levels stay in
        /// StructureTabViewModel, apart from the rows built here.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <returns>Three rows without a verdict: vignetting in stops, the centre offset in
        /// percent (or "nedeterminat"), the local spots.</returns>
        internal static IReadOnlyList<MetricRow> Informative(StructureResult r) => new[]
        {
            new MetricRow
            {
                Label = T("vignetting centre -> corner", "vignetare centru -> colt"),
                Value = T($"{r.VignetteStops:F2} stops", $"{r.VignetteStops:F2} stopuri"),
                Note = StructureReport.VignetteNote(r),
            },
            new MetricRow
            {
                Label = T("illumination centre offset", "offset centru iluminare"),
                Value = r.CentroidValid ? Pct(r.CentroidOffsetPct) : T("undetermined", "nedeterminat"),
                Note = StructureReport.CentroidNote(r),
            },
            new MetricRow
            {
                Label = T("local spots (dust etc.)", "pete locale (praf etc.)"),
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

        /// <summary>NaN is written "n/a", not "NaN %": it is a degenerate input, not a
        /// measurement.</summary>
        /// <param name="v">The value, in percent.</param>
        /// <returns>The value with two decimals and " %", or "n/a".</returns>
        static string Pct(double v) => double.IsNaN(v) ? "n/a" : $"{v:F2} %";
    }
}
