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

namespace LightsCheck.Core;

/// <summary>
/// One frame's measurements as the console prints them (Romanian, R-017). Words only.
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
        w.WriteLine($"=== CADRU {frame.Info.Index}: {frame.Info.FileName} ===");
        w.WriteLine($"    dimensiune         : {m.Width} x {m.Height} px; scara {RunReport.F(m.Scale.Black, 0)} .. {RunReport.F(m.Scale.White, 0)} ADU ({m.Scale.Source})");
        w.WriteLine("    nivele CFA         : " + string.Join("  ", m.Channels.Select(c => $"{c.Name} {RunReport.F(c.Median, 0)} (saturati {c.Saturated.ToString(CultureInfo.InvariantCulture)}, min {RunReport.F(c.Min, 0)})")) + "  [ADU peste black]");
        w.WriteLine($"    fond verde         : median {RunReport.F(m.BackgroundMedian, 0)}, centru {RunReport.F(m.BackgroundCentre, 0)}, colturi {RunReport.F(m.BackgroundCorners.Min(), 0)} .. {RunReport.F(m.BackgroundCorners.Max(), 0)} (dale {RunReport.F(m.BackgroundMin, 0)} .. {RunReport.F(m.BackgroundMax, 0)}); zgomot sigma {RunReport.F(m.NoiseSigma, 1)}");
        w.WriteLine($"    stele              : {m.Detected.ToString(CultureInfo.InvariantCulture)} detectate, {m.DetectedBright.ToString(CultureInfo.InvariantCulture)} luminoase, {m.SaturatedStars.ToString(CultureInfo.InvariantCulture)} saturate; {m.Shapes.Count.ToString(CultureInfo.InvariantCulture)} masurate");
        w.WriteLine($"    FWHM               : {Opt(m.FwhmMedian, 2, " px")} median (p90 {Opt(m.FwhmP90, 2)}); centru {Zone(m.Centre, z => z.Fwhm)}, mijloc {Zone(m.Mid, z => z.Fwhm)}, margine {Zone(m.Edge, z => z.Fwhm)}");
        w.WriteLine($"    alungire           : {Opt(m.ElongationMedian, 2)} mediana (p90 {Opt(m.ElongationP90, 2)}); {(m.ElongatedFraction is double ef ? RunReport.F(100 * ef, 0) : "-")} % peste {RunReport.F(Th.ElongatedRatio, 1)}; unghi {Opt(m.AngleDeg, 0, " grade")}, coerenta {Opt(m.AngleCoherence, 2)}; centru {Zone(m.Centre, z => z.Elongation)}, margine {Zone(m.Edge, z => z.Elongation)}");
        w.WriteLine($"    halo               : {Opt(m.Halo, 3)} (inel 5-10 px / nucleu 3 px, la cele mai luminoase {Th.HaloStars.ToString(CultureInfo.InvariantCulture)} stele)");
        w.WriteLine($"    dare               : {m.Trails.Count.ToString(CultureInfo.InvariantCulture)}" + (m.Trails.Count > 0 ? " (" + string.Join(", ", m.Trails.Select(t => $"{RunReport.F(t.Length, 0)} px la ({t.X.ToString(CultureInfo.InvariantCulture)},{t.Y.ToString(CultureInfo.InvariantCulture)})")) + ")" : ""));
        w.WriteLine($"    inregistrare       : {m.RegistrationStars.Count.ToString(CultureInfo.InvariantCulture)} stele retinute");
    }

    /// <summary>An optional figure with a fixed number of decimals and its unit, or "-".</summary>
    /// <param name="v">The figure; null when it could not be measured.</param>
    /// <param name="decimals">How many decimals to write.</param>
    /// <param name="unit">
    /// The text appended after the number (" px", " grade"); none by default.
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
