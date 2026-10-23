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
/// The polar alignment's verdict (R-050): by the drift the alignment can cause in one exposure
/// against Th.PolarDriftFwhmFraction of the session FWHM: Attention when even the lower bound's
/// drift reaches it, Ok when the upper bound's stays under it, Marginal when the bracket
/// straddles it, Unknown without the figures (no clean drift, no axis, no scale, or no upper
/// bound and a harmless lower one).
/// </summary>
internal enum PolarAlignment
{
    Unknown,
    Ok,
    Marginal,
    Attention,
}

/// <summary>
/// The series as a whole (R-022 to R-025, R-043, R-049, R-050): every frame's class and flags,
/// the session's medians, the trends, the drift and the field rotation, the drift per exposure
/// with the walking-noise dash and verdict it gives, the RA axis, the noise against the read
/// noise when the biases gave one, and the polar alignment read from the drift and the rotation:
/// DriftApproximate when the drift fell back to the pairs with a pause; StarDriftPerMin the
/// stars' motion per minute along RA and DEC in pixels (signed east and north with a solve);
/// DeclinationDeg the solve's; PolarErrorMinArcmin from the DEC drift alone, PolarErrorMaxArcmin
/// adding the rotation; PolarDriftMinPx and PolarDriftMaxPx the drift those bounds cause in one
/// median exposure; PolarElongation what the upper one adds to a star of the session FWHM;
/// PolarRaDriftMaxArcsecPerMin how much of the RA drift the alignment can explain.
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
    IReadOnlyList<int> SwampingFrames,
    bool DriftApproximate,
    MountMove? StarDriftPerMin,
    double? DeclinationDeg,
    double? PolarErrorMinArcmin,
    double? PolarErrorMaxArcmin,
    double? PolarDriftMinPx,
    double? PolarDriftMaxPx,
    double? PolarElongation,
    double? PolarRaDriftMaxArcsecPerMin,
    PolarAlignment PolarAlignment);

internal static class SeriesAnalyzer
{
    /// <summary>The sidereal rate in arcseconds per minute: a full turn in 23 h 56 min 4.09 s.</summary>
    private const double SiderealArcsecPerMin = 360.0 * 3600 / (23 * 60 + 56 + 4.0905 / 60);

    /// <summary>Arcminutes in a radian.</summary>
    private const double RadToArcmin = 180 / Math.PI * 60;

    /// <summary>
    /// Reads the series: the classes and flags of every frame, the session medians and trends,
    /// the drift and the rotation, the walking-noise dash, the RA axis, the mount's moves, the
    /// polar alignment and the swamping (R-022 to R-025, R-033, R-038, R-043, R-049, R-050).
    /// </summary>
    /// <param name="frames">Every light with its measurement and registration, in file order; a frame without a measurement is classed unknown.</param>
    /// <param name="cadence">The pauses before each frame, one per entry of <paramref name="frames"/>.</param>
    /// <param name="dither">The steps and moves read from the registered positions of the same frames.</param>
    /// <param name="readNoise">The read noise per CFA position from the biases, or null without two of them.</param>
    /// <param name="wcs">The solve at the session root, or null; it gives the RA axis, the senses and the declination.</param>
    /// <param name="arcsecPerPixel">The plate scale in arcseconds per pixel, or null when the focal length is unknown; without it the polar figures stay null.</param>
    /// <returns>The analysis; every figure a session cannot give is null and every verdict without figures is unknown.</returns>
    internal static SeriesAnalysis Analyze(IReadOnlyList<AnalyzedFrame> frames, Cadence cadence, DitherAnalysis dither, IReadOnlyList<ReadNoise>? readNoise, Wcs? wcs, double? arcsecPerPixel = null)
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

        // Drift: the mean step per minute over the drift pairs, the consecutive registered frames
        // with no pause above Th.DitherPause between them, where nothing was pressed (R-049, ADR
        // 0016); with fewer than Th.DriftMinPairs of them, over the pairs that were not a move,
        // where a press of 3 to 5 px still counts, so the figure is approximate.
        (double sumX, double sumY, double minutes, int pairs) = Drift(frames, dither, i => cadence.PauseSeconds[i] is double p && p <= Th.DitherPause);
        bool approximate = false;
        if (pairs < Th.DriftMinPairs)
        {
            (sumX, sumY, minutes, pairs) = Drift(frames, dither, i => !dither.IsMove[i]);
            approximate = pairs > 0;
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

        // The drift as the stars move on the sky (R-050): the moves' mapping on the drift
        // vector, the stars being the negative of the pointing; a clean drift only, since the
        // presses inside an approximate one are not the mount's tracking.
        MountMove? starDrift = null;
        if (!approximate && driftX is double sdx && driftY is double sdy)
        {
            if (wcs is not null)
            {
                (double raDeg, double decDeg) = wcs.SkyOffsetDeg(-sdx, sdy);
                starDrift = new MountMove(-raDeg / wcs.RaScaleDeg, -decDeg / wcs.DecScaleDeg, true);
            }
            else if (ra == Axis.Y)
            {
                starDrift = new MountMove(Math.Abs(sdy), Math.Abs(sdx), false);
            }
            else if (ra == Axis.X)
            {
                starDrift = new MountMove(Math.Abs(sdx), Math.Abs(sdy), false);
            }
        }

        // The polar alignment (R-050, ADR 0016). A pole off by e makes the stars drift in DEC at
        // w (e_alt sin H - e_az cos H), in RA at w sin d (e_alt cos H + e_az sin H) and the field
        // turn at w cos d (e_alt cos H + e_az sin H), w the sidereal rate; the squares of the two
        // brackets add to |e|^2 whatever the hour angle. The DEC drift alone bounds |e| from below;
        // the rotation, read as the alignment's, bounds it from above, and bounds the RA drift the
        // alignment can explain at rotation x tan d. The cost is the drift those bounds make in
        // one exposure (the rotation itself moves no star at the centre) and the elongation it adds.
        double? polarMin = null, polarMax = null, polarDriftMin = null, polarDriftMax = null, polarElongation = null, polarRaMax = null;
        PolarAlignment polar = PolarAlignment.Unknown;
        if (starDrift is not null && arcsecPerPixel is double scale && scale > 0 && medianExposure is double exposureSeconds)
        {
            double exposureMin = exposureSeconds / 60;
            double decArcsec = Math.Abs(starDrift.DecPx) * scale;
            polarMin = decArcsec / SiderealArcsecPerMin * RadToArcmin;
            polarDriftMin = Math.Abs(starDrift.DecPx) * exposureMin;
            if (wcs?.DecDeg is double dec && rotPerHour is double rph && Math.Cos(dec * Math.PI / 180) is double cosDec && cosDec > 0.01)
            {
                double rho = Math.Abs(rph) * 60;   // degrees per hour to arcseconds per minute
                double a = rho / (SiderealArcsecPerMin * cosDec) * RadToArcmin;
                polarMax = Math.Sqrt(polarMin.Value * polarMin.Value + a * a);
                polarRaMax = rho * Math.Abs(Math.Tan(dec * Math.PI / 180));
                polarDriftMax = Math.Sqrt(decArcsec * decArcsec + polarRaMax.Value * polarRaMax.Value) * exposureMin / scale;
                if (sessionFwhm is double fw)
                {
                    double sg = fw / 2.355;
                    polarElongation = Math.Sqrt(sg * sg + polarDriftMax.Value * polarDriftMax.Value / 12) / sg;
                }
            }

            if (sessionFwhm is double fwhm)
            {
                double threshold = Th.PolarDriftFwhmFraction * fwhm;
                polar = polarDriftMin >= threshold ? PolarAlignment.Attention
                    : polarDriftMax is double dm ? (dm < threshold ? PolarAlignment.Ok : PolarAlignment.Marginal)
                    : PolarAlignment.Unknown;
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
            driftX, driftY, pairs, medianExposure, driftPerExposure, driftAxis, streak, walking, rotTotal, rotPerHour, corner, ra, source, trailAngle, trailOn, moves, readNoise, swamping, swampingFrames,
            approximate, starDrift, wcs?.DecDeg, polarMin, polarMax, polarDriftMin, polarDriftMax, polarElongation, polarRaMax, polar);
    }

    /// <summary>
    /// Sums the steps and the minutes of the consecutive registered pairs <paramref name="take"/>
    /// accepts (by the index of the pair's second frame), both frames timed in order.
    /// </summary>
    /// <param name="frames">The frames, for their timestamps.</param>
    /// <param name="dither">The steps between consecutive frames.</param>
    /// <param name="take">Whether the pair ending at the given index counts.</param>
    /// <returns>The summed X and Y steps in pixels, the summed minutes and the pairs counted.</returns>
    private static (double SumX, double SumY, double Minutes, int Pairs) Drift(IReadOnlyList<AnalyzedFrame> frames, DitherAnalysis dither, Func<int, bool> take)
    {
        double sumX = 0, sumY = 0, minutes = 0;
        int pairs = 0;
        for (int i = 1; i < frames.Count; i++)
        {
            if (dither.Step[i] is double && take(i) && frames[i].Info.Timestamp is DateTime t && frames[i - 1].Info.Timestamp is DateTime tp && t > tp)
            {
                sumX += dither.StepX[i]!.Value;
                sumY += dither.StepY[i]!.Value;
                minutes += (t - tp).TotalMinutes;
                pairs++;
            }
        }

        return (sumX, sumY, minutes, pairs);
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
