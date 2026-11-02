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

using System.Globalization;
using static LightsCheck.Core.Ui;

namespace LightsCheck.Core;

/// <summary>
/// One frame's measurements as the console prints them (R-017), in the run's language. Words
/// only.
/// </summary>
internal static class FrameReport
{
    /// <summary>
    /// Writes the block of one measured frame: a blank line, the heading with its index and file
    /// name, then one line each for the size and ADU scale, the CFA levels, the green
    /// background and noise, the star counts, the FWHM, the elongation, the halo, the trails
    /// and the registration stars. A missing figure is written as "-".
    /// </summary>
    /// <param name="frame">The frame and its measurement; not null.</param>
    /// <param name="w">Where the lines go; not null.</param>
    internal static void Write(MeasuredFrame frame, TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(w);
        FrameMeasurement m = frame.Measurement;
        w.WriteLine();
        w.WriteLine(T($"=== FRAME {frame.Info.Index}: {frame.Info.FileName} ===", $"=== CADRU {frame.Info.Index}: {frame.Info.FileName} ==="));
        string size = $"{m.Width} x {m.Height} px", black = RunReport.F(m.Scale.Black, 0), white = RunReport.F(m.Scale.White, 0);
        w.WriteLine(T(
            $"    size               : {size}; scale {black} .. {white} ADU ({m.Scale.Source})",
            $"    dimensiune         : {size}; scara {black} .. {white} ADU ({m.Scale.Source})"));
        string levels = string.Join("  ", m.Channels.Select(c => T(
            $"{c.Name} {RunReport.F(c.Median, 0)} (saturated {c.Saturated.ToString(CultureInfo.InvariantCulture)}, min {RunReport.F(c.Min, 0)})",
            $"{c.Name} {RunReport.F(c.Median, 0)} (saturati {c.Saturated.ToString(CultureInfo.InvariantCulture)}, min {RunReport.F(c.Min, 0)})")));
        w.WriteLine(T($"    CFA levels         : {levels}  [ADU above black]", $"    nivele CFA         : {levels}  [ADU peste black]"));
        string median = RunReport.F(m.BackgroundMedian, 0), centre = RunReport.F(m.BackgroundCentre, 0);
        string corners = $"{RunReport.F(m.BackgroundCorners.Min(), 0)} .. {RunReport.F(m.BackgroundCorners.Max(), 0)}", tiles = $"{RunReport.F(m.BackgroundMin, 0)} .. {RunReport.F(m.BackgroundMax, 0)}", sigma = RunReport.F(m.NoiseSigma, 1);
        w.WriteLine(T(
            $"    green background   : median {median}, centre {centre}, corners {corners} (tiles {tiles}); noise sigma {sigma}",
            $"    fond verde         : median {median}, centru {centre}, colturi {corners} (dale {tiles}); zgomot sigma {sigma}"));
        string detected = m.Detected.ToString(CultureInfo.InvariantCulture), bright = m.DetectedBright.ToString(CultureInfo.InvariantCulture);
        string saturated = m.SaturatedStars.ToString(CultureInfo.InvariantCulture), measured = m.Shapes.Count.ToString(CultureInfo.InvariantCulture);
        w.WriteLine(T(
            $"    stars              : {detected} detected, {bright} bright, {saturated} saturated; {measured} measured",
            $"    stele              : {RoCount(detected)}detectate, {RoCount(bright)}luminoase, {RoCount(saturated)}saturate; {RoCount(measured)}masurate"));
        string fwhm = Opt(m.FwhmMedian, 2, " px"), fwhm90 = Opt(m.FwhmP90, 2);
        string fc = Zone(m.Centre, z => z.Fwhm), fm = Zone(m.Mid, z => z.Fwhm), fe = Zone(m.Edge, z => z.Fwhm);
        w.WriteLine(T(
            $"    FWHM               : {fwhm} median (p90 {fwhm90}); centre {fc}, middle {fm}, edge {fe}",
            $"    FWHM               : {fwhm} median (p90 {fwhm90}); centru {fc}, mijloc {fm}, margine {fe}"));
        string elong = Opt(m.ElongationMedian, 2), elong90 = Opt(m.ElongationP90, 2), share = m.ElongatedFraction is double ef ? RunReport.F(100 * ef, 0) : "-";
        string ratio = RunReport.F(Th.ElongatedRatio, 1), coherence = Opt(m.AngleCoherence, 2), ec = Zone(m.Centre, z => z.Elongation), ee = Zone(m.Edge, z => z.Elongation);
        w.WriteLine(T(
            $"    elongation         : {elong} median (p90 {elong90}); {share} % above {ratio}; angle {Opt(m.AngleDeg, 0, " degrees")}, coherence {coherence}; centre {ec}, edge {ee}",
            $"    alungire           : {elong} mediana (p90 {elong90}); {share} % peste {ratio}; unghi {(m.AngleDeg is double a ? RoCount(RunReport.F(a, 0)) + "grade" : "-")}, coerenta {coherence}; centru {ec}, margine {ee}"));
        string halo = Opt(m.Halo, 3), haloStars = Th.HaloStars.ToString(CultureInfo.InvariantCulture);
        w.WriteLine(T(
            $"    halo               : {halo} (ring 5-10 px / core 3 px, over the {haloStars} brightest stars)",
            $"    halo               : {halo} (inel 5-10 px / nucleu 3 px, la cele mai luminoase {RoCount(haloStars)}stele)"));
        string trails = m.Trails.Count.ToString(CultureInfo.InvariantCulture) + (m.Trails.Count > 0
            ? " (" + string.Join(", ", m.Trails.Select(t => $"{RunReport.F(t.Length, 0)} px {T("at", "la")} ({t.X.ToString(CultureInfo.InvariantCulture)},{t.Y.ToString(CultureInfo.InvariantCulture)})")) + ")"
            : "");
        w.WriteLine(T($"    trails             : {trails}", $"    dare               : {trails}"));
        string kept = m.RegistrationStars.Count.ToString(CultureInfo.InvariantCulture);
        w.WriteLine(T($"    registration       : {kept} stars kept", $"    inregistrare       : {RoCount(kept)}stele retinute"));
    }

    /// <summary>An optional figure with a fixed number of decimals and its unit, or "-".</summary>
    /// <param name="v">The figure; null when it could not be measured.</param>
    /// <param name="decimals">How many decimals to write.</param>
    /// <param name="unit">
    /// The text appended after the number (" px", " degrees"); none by default.
    /// </param>
    /// <returns>
    /// The formatted figure with its unit, or "-" when <paramref name="v"/> is null.
    /// </returns>
    private static string Opt(double? v, int decimals, string unit = "") => v is double d ? RunReport.F(d, decimals) + unit : "-";

    /// <summary>
    /// One zone's figure with two decimals, followed by its star count in brackets.
    /// </summary>
    /// <param name="z">The zone (centre, middle or edge) of the frame.</param>
    /// <param name="pick">Which figure of the zone to write (its FWHM or its elongation).</param>
    /// <returns>
    /// "2.31 (140)", or "-" with the zone's star count, "- (3)", when the zone has no figure.
    /// </returns>
    private static string Zone(ZoneShape z, Func<ZoneShape, double?> pick)
        => (pick(z) is double v ? RunReport.F(v, 2) : "-") + " (" + z.Stars.ToString(CultureInfo.InvariantCulture) + ")";
}
