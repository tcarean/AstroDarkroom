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

namespace LightsCheck.Core;

/// <summary>One light with everything measured on it; Measurement and Registration are null where a step failed, Error says why.</summary>
internal sealed record AnalyzedFrame(FrameInfo Info, FrameMeasurement? Measurement, RegistrationResult? Registration, string? Error);

/// <summary>The class of a frame for the stack (R-022).</summary>
internal enum FrameClass
{
    Good,
    Marginal,
    Bad,
    Unknown,
}

/// <summary>What a flag is about; the report turns it into words.</summary>
internal enum FlagKind
{
    Trailed,
    MarginalTrail,
    Soft,
    Clouds,
    BackgroundJump,
    Trails,
    Unreadable,
    Unregistered,
}

/// <summary>
/// A reason attached to a frame: its kind, the measured value and the threshold it was judged
/// against; for a trailing flag, the angle as Detail and the mount axis the trail runs along (R-033).
/// </summary>
internal sealed record FrameFlag(FlagKind Kind, double Value, double Threshold, string? Detail = null, TrailAxis On = TrailAxis.Unknown);

/// <summary>The mount axis a trail runs along, against the RA axis of the image (R-033).</summary>
internal enum TrailAxis
{
    Unknown,
    Ra,
    Dec,
    Oblique,
}

/// <summary>
/// The mount's move behind a dither, in pixels of the plate scale: RA positive to the east
/// and DEC to the north when Signed (a WCS was at hand), the magnitudes along the two axes
/// when not (the axis came from the trails) (R-038).
/// </summary>
internal sealed record MountMove(double RaPx, double DecPx, bool Signed);

/// <summary>Medians of the first and the last <see cref="Count"/> good frames, for the trends (R-023).</summary>
internal sealed record Trend(double? First, double? Last, int Count)
{
    internal double? Change => First is double f && Last is double l ? l - f : null;
    internal double? RelativeChange => First is double f && Last is double l && f != 0 ? (l - f) / f : null;
}

/// <summary>Which image axis is RA, and how it was decided.</summary>
internal enum Axis
{
    Unknown,
    X,
    Y,
}

/// <summary>
/// The walking-noise verdict (R-043): Ok by the dither rule of R-021; Static when that rule
/// fails but the dash the drift draws across DitherAnalysis.WalkingStretch (the longest
/// stretch of two or more consecutive frames within Th.CoverageLoose) is under
/// Th.WalkingStreakVisible, so the pattern does not move between the frames that share a
/// spot; Insufficient otherwise; Unknown with fewer than two registered frames.
/// </summary>
internal enum WalkingNoise
{
    Unknown,
    Ok,
    Static,
    Insufficient,
}

/// <summary>
/// The series as a whole (R-022 to R-025, R-043): every frame's class and flags, the
/// session's medians, the trends, the drift and the field rotation, the drift per exposure
/// with the walking-noise dash and verdict it gives, the RA axis, and the noise against the
/// read noise when the biases gave one.
/// </summary>
internal sealed record SeriesAnalysis(
    IReadOnlyList<FrameClass> Classes,
    IReadOnlyList<IReadOnlyList<FrameFlag>> Flags,
    int GoodCount,
    int MarginalCount,
    int BadCount,
    int UnknownCount,
    double? SessionFwhm,
    double? SessionStars,
    Trend Fwhm,
    Trend Halo,
    Trend Background,
    Trend Stars,
    bool? FocusHeld,
    bool? DewSuspected,
    double? DriftXPerMin,
    double? DriftYPerMin,
    int DriftPairs,
    double? MedianExposureSeconds,
    double? DriftPerExposurePx,
    Axis DriftAxis,
    double? WalkingStreakPx,
    WalkingNoise WalkingNoise,
    double? RotationTotalDeg,
    double? RotationPerHourDeg,
    double? CornerShiftPerExposurePx,
    Axis RaAxis,
    string AxisSource,
    double? TrailAngleDeg,
    TrailAxis TrailOn,
    IReadOnlyList<MountMove?> MountMoves,
    IReadOnlyList<ReadNoise>? ReadNoise,
    IReadOnlyList<IReadOnlyList<ChannelNoise>>? Swamping,
    IReadOnlyList<int> SwampingFrames);

internal static class SeriesAnalyzer
{
    internal static SeriesAnalysis Analyze(IReadOnlyList<AnalyzedFrame> frames, Cadence cadence, DitherAnalysis dither, IReadOnlyList<ReadNoise>? readNoise, Wcs? wcs)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(cadence);
        ArgumentNullException.ThrowIfNull(dither);
        int n = frames.Count;
        FrameClass[] classes = new FrameClass[n];
        List<FrameFlag>[] flags = new List<FrameFlag>[n];
        for (int i = 0; i < n; i++)
        {
            flags[i] = [];
        }

        // First pass: the classes that need no session figure (trailing), and the medians.
        List<double> fwhmGood = [];
        List<double> starsAll = [];
        for (int i = 0; i < n; i++)
        {
            AnalyzedFrame f = frames[i];
            if (f.Measurement is null)
            {
                classes[i] = FrameClass.Unknown;
                flags[i].Add(new FrameFlag(FlagKind.Unreadable, 0, 0, f.Error ?? f.Info.Error));
                continue;
            }

            FrameMeasurement m = f.Measurement;
            starsAll.Add(m.Detected);
            double el = m.ElongationMedian ?? 1;
            string? angle = m.AngleDeg is double a ? a.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) : null;
            if (el >= Th.ElongationBad)
            {
                classes[i] = FrameClass.Bad;
                flags[i].Add(new FrameFlag(FlagKind.Trailed, el, Th.ElongationBad, angle));
            }
            else if (el >= Th.ElongationMarginal)
            {
                classes[i] = FrameClass.Marginal;
                flags[i].Add(new FrameFlag(FlagKind.MarginalTrail, el, Th.ElongationMarginal, angle));
            }
            else
            {
                classes[i] = FrameClass.Good;
                if (m.FwhmMedian is double fw)
                {
                    fwhmGood.Add(fw);
                }
            }

            if (m.Trails.Count > 0)
            {
                flags[i].Add(new FrameFlag(FlagKind.Trails, m.Trails.Count, 0));
            }

            if (f.Registration is null || !f.Registration.Ok)
            {
                flags[i].Add(new FrameFlag(FlagKind.Unregistered, 0, 0));
            }
        }

        double? sessionFwhm = fwhmGood.Count > 0 ? Raster.Percentile(fwhmGood, 50) : null;
        double? sessionStars = starsAll.Count > 0 ? Raster.Percentile(starsAll, 50) : null;

        // Second pass: the flags against the session: soft focus, clouds, background jumps.
        for (int i = 0; i < n; i++)
        {
            FrameMeasurement? m = frames[i].Measurement;
            if (m is null)
            {
                continue;
            }

            if (sessionFwhm is double sf && m.FwhmMedian is double fw && classes[i] == FrameClass.Good && fw > Th.SoftFwhmFactor * sf)
            {
                classes[i] = FrameClass.Marginal;
                flags[i].Add(new FrameFlag(FlagKind.Soft, fw, Th.SoftFwhmFactor * sf));
            }

            if (sessionStars is double ss && m.Detected < Th.CloudStarFraction * ss)
            {
                if (classes[i] == FrameClass.Good)
                {
                    classes[i] = FrameClass.Marginal;
                }

                flags[i].Add(new FrameFlag(FlagKind.Clouds, m.Detected, Th.CloudStarFraction * ss));
            }

            List<double> neighbours = [];
            for (int j = Math.Max(0, i - Th.BackgroundNeighbours); j <= Math.Min(n - 1, i + Th.BackgroundNeighbours); j++)
            {
                if (j != i && frames[j].Measurement is FrameMeasurement mj)
                {
                    neighbours.Add(mj.BackgroundMedian);
                }
            }

            if (neighbours.Count >= 2)
            {
                double med = Raster.Percentile(neighbours, 50);
                double pct = med > 0 ? 100 * (m.BackgroundMedian - med) / med : 0;
                if (Math.Abs(pct) > Th.BackgroundJumpPct)
                {
                    flags[i].Add(new FrameFlag(FlagKind.BackgroundJump, pct, Th.BackgroundJumpPct));
                }
            }
        }

        // Trends on the good frames: the first and the last TrendFrames of them.
        List<FrameMeasurement> good = Enumerable.Range(0, n).Where(i => classes[i] == FrameClass.Good).Select(i => frames[i].Measurement!).ToList();
        Trend fwhmTrend = TrendOf(good, m => m.FwhmMedian);
        Trend haloTrend = TrendOf(good, m => m.Halo);
        Trend bgTrend = TrendOf(good, m => m.BackgroundMedian);
        Trend starsTrend = TrendOf(good, m => m.Detected);
        bool? focusHeld = fwhmTrend.RelativeChange is double rc ? Math.Abs(rc) < Th.FocusHeldFraction : null;
        bool? dew = fwhmTrend.RelativeChange is double fr && haloTrend.Change is double hc ? fr > Th.DewFwhmRise && hc > Th.DewHaloRise : null;

        // Drift: the mean step of consecutive registered frames that were not a dither move, per minute.
        double sumX = 0, sumY = 0, minutes = 0;
        int pairs = 0;
        for (int i = 1; i < n; i++)
        {
            if (dither.Step[i] is double && !dither.IsMove[i] && frames[i].Info.Timestamp is DateTime t && frames[i - 1].Info.Timestamp is DateTime tp && t > tp)
            {
                sumX += dither.StepX[i]!.Value;
                sumY += dither.StepY[i]!.Value;
                minutes += (t - tp).TotalMinutes;
                pairs++;
            }
        }

        double? driftX = minutes > 0 ? sumX / minutes : null, driftY = minutes > 0 ? sumY / minutes : null;

        // The drift over one exposure (the median exposure of the session) and the image axis
        // it mostly runs along; the dash is that drift times the frames minus one of the
        // longest stretch of consecutive frames at one spot (DitherAnalyzer.WalkingStretch,
        // two frames or more), and the walking-noise verdict reads it (R-043, ADR 0015).
        List<double> exposures = frames.Select(f => f.Info.ExposureSeconds).Where(e => e is > 0).Select(e => e!.Value).ToList();
        double? medianExposure = exposures.Count > 0 ? Raster.Percentile(exposures, 50) : null;
        double? driftPerExposure = driftX is double dx && driftY is double dy && medianExposure is double me ? Math.Sqrt(dx * dx + dy * dy) * me / 60 : null;
        Axis driftAxis = driftX is double ax && driftY is double ay ? (Math.Abs(ax) >= Math.Abs(ay) ? Axis.X : Axis.Y) : Axis.Unknown;
        double? streak = driftPerExposure is double dpe ? dpe * ((dither.WalkingStretch?.Frames ?? 1) - 1) : null;
        WalkingNoise walking = dither.Registered < 2 ? WalkingNoise.Unknown
            : dither.WalkingNoiseOk ? WalkingNoise.Ok
            : streak is double st && st < Th.WalkingStreakVisible ? WalkingNoise.Static
            : WalkingNoise.Insufficient;

        // Rotation between the first and the last registered frame.
        AnalyzedFrame? firstReg = frames.FirstOrDefault(f => f.Registration is { Ok: true }), lastReg = frames.LastOrDefault(f => f.Registration is { Ok: true });
        double? rotTotal = null, rotPerHour = null, corner = null;
        if (firstReg is not null && lastReg is not null && firstReg != lastReg)
        {
            rotTotal = lastReg.Registration!.RotationDeg - firstReg.Registration!.RotationDeg;
            if (firstReg.Info.Timestamp is DateTime t0 && lastReg.Info.Timestamp is DateTime t1 && t1 > t0)
            {
                rotPerHour = rotTotal / (t1 - t0).TotalHours;
                FrameMeasurement? m0 = firstReg.Measurement;
                double exposure = firstReg.Info.ExposureSeconds ?? 0;
                if (m0 is not null)
                {
                    double halfDiag = Math.Sqrt(m0.Height * m0.Height / 4.0 + m0.Width * m0.Width / 4.0);
                    corner = Math.Abs(rotPerHour.Value) * exposure / 3600 * Math.PI / 180 * halfDiag;
                }
            }
        }

        // The RA axis: from a plate solve when one is at hand, else from the trails of the bad frames.
        Axis ra = wcs?.RaAxis ?? Axis.Unknown;
        string source = wcs is null ? "necunoscut" : "plate solve (WCS)";
        double? trailAngle = null;
        List<double> angles = Enumerable.Range(0, n)
            .Where(i => classes[i] == FrameClass.Bad && frames[i].Measurement?.AngleDeg is not null && frames[i].Measurement!.AngleCoherence is double c && c > Th.TrailAxisCoherence)
            .Select(i => frames[i].Measurement!.AngleDeg!.Value).ToList();
        if (angles.Count >= Th.TrailAxisMinFrames)
        {
            double sc = angles.Average(a => Math.Cos(2 * a * Math.PI / 180)), ss = angles.Average(a => Math.Sin(2 * a * Math.PI / 180));
            trailAngle = 0.5 * Math.Atan2(ss, sc) * 180 / Math.PI;
            if (ra == Axis.Unknown)
            {
                double fromY = Math.Abs(90 - Math.Abs(trailAngle.Value));
                if (fromY < Th.TrailAxisTolerance)
                {
                    ra = Axis.Y;
                    source = "directia darelor (presupus: urmarirea e pe RA)";
                }
                else if (Math.Abs(trailAngle.Value) < Th.TrailAxisTolerance)
                {
                    ra = Axis.X;
                    source = "directia darelor (presupus: urmarirea e pe RA)";
                }
            }
        }

        // The axis of each flagged frame's trail, against the RA axis just decided, when the
        // frame's stars agree on a direction as well as the voters do (R-033).
        TrailAxis trailOn = trailAngle is double meanAngle ? TrailAxisOf(meanAngle, ra) : TrailAxis.Unknown;
        if (ra != Axis.Unknown)
        {
            for (int i = 0; i < n; i++)
            {
                FrameMeasurement? fm = frames[i].Measurement;
                if (fm?.AngleDeg is double fa && fm.AngleCoherence is double fc && fc > Th.TrailAxisCoherence)
                {
                    for (int k = 0; k < flags[i].Count; k++)
                    {
                        if (flags[i][k].Kind is FlagKind.Trailed or FlagKind.MarginalTrail)
                        {
                            flags[i][k] = flags[i][k] with { On = TrailAxisOf(fa, ra) };
                        }
                    }
                }
            }
        }

        // The mount's move behind each dither (R-038): the pointing moved by minus the stars'
        // shift, and Siril's solved FITS has its rows flipped against the raws, so the WCS is
        // applied to (-StepX, +StepY) (ADR 0013). Without a WCS, the magnitudes along the axes.
        MountMove?[] moves = new MountMove?[n];
        for (int i = 0; i < n; i++)
        {
            if (!dither.IsMove[i] || dither.StepX[i] is not double sx || dither.StepY[i] is not double sy)
            {
                continue;
            }

            if (wcs is not null)
            {
                (double raDeg, double decDeg) = wcs.SkyOffsetDeg(-sx, sy);
                moves[i] = new MountMove(raDeg / wcs.RaScaleDeg, decDeg / wcs.DecScaleDeg, true);
            }
            else if (ra == Axis.Y)
            {
                moves[i] = new MountMove(Math.Abs(sy), Math.Abs(sx), false);
            }
            else if (ra == Axis.X)
            {
                moves[i] = new MountMove(Math.Abs(sx), Math.Abs(sy), false);
            }
        }

        // Swamping on the first, the middle and the last measured frames.
        List<IReadOnlyList<ChannelNoise>>? swamping = null;
        List<int> swampingFrames = [];
        if (readNoise is not null)
        {
            swamping = [];
            List<int> measured = Enumerable.Range(0, n).Where(i => frames[i].Measurement is not null).ToList();
            foreach (int i in new[] { 0, measured.Count / 2, measured.Count - 1 }.Distinct())
            {
                if (i >= 0 && i < measured.Count)
                {
                    swampingFrames.Add(frames[measured[i]].Info.Index);
                    swamping.Add(NoiseAnalyzer.Swamping(frames[measured[i]].Measurement!.Channels, readNoise));
                }
            }
        }

        return new SeriesAnalysis(classes, flags.Select(l => (IReadOnlyList<FrameFlag>)l).ToList(),
            classes.Count(c => c == FrameClass.Good), classes.Count(c => c == FrameClass.Marginal), classes.Count(c => c == FrameClass.Bad), classes.Count(c => c == FrameClass.Unknown),
            sessionFwhm, sessionStars, fwhmTrend, haloTrend, bgTrend, starsTrend, focusHeld, dew,
            driftX, driftY, pairs, medianExposure, driftPerExposure, driftAxis, streak, walking, rotTotal, rotPerHour, corner, ra, source, trailAngle, trailOn, moves, readNoise, swamping, swampingFrames);
    }

    /// <summary>
    /// The mount axis a trail at <paramref name="angleDeg"/> (from the image's horizontal) runs
    /// along: RA within Th.TrailAxisTolerance of the image axis that is RA, DEC within it of the
    /// other axis, oblique otherwise; unknown without an RA axis (R-033).
    /// </summary>
    internal static TrailAxis TrailAxisOf(double angleDeg, Axis ra)
    {
        if (ra == Axis.Unknown)
        {
            return TrailAxis.Unknown;
        }

        double fromX = Math.Abs(angleDeg), fromY = Math.Abs(90 - Math.Abs(angleDeg));
        (double fromRa, double fromDec) = ra == Axis.X ? (fromX, fromY) : (fromY, fromX);
        return fromRa < Th.TrailAxisTolerance ? TrailAxis.Ra : fromDec < Th.TrailAxisTolerance ? TrailAxis.Dec : TrailAxis.Oblique;
    }

    private static Trend TrendOf(List<FrameMeasurement> good, Func<FrameMeasurement, double?> pick)
    {
        if (good.Count < 2)
        {
            return new Trend(null, null, 0);
        }

        int k = Math.Min(Th.TrendFrames, good.Count / 2);
        List<double> first = good.Take(k).Select(pick).Where(v => v is not null).Select(v => v!.Value).ToList();
        List<double> last = good.Skip(good.Count - k).Select(pick).Where(v => v is not null).Select(v => v!.Value).ToList();
        return new Trend(first.Count > 0 ? Raster.Percentile(first, 50) : null, last.Count > 0 ? Raster.Percentile(last, 50) : null, k);
    }
}
