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

// LogText.cs - how a value is written in the diagnostic log: English, invariant numbers, a missing
// value as "none", sizes in KiB and MiB, an enum by its name. The log's lines are composed at the
// seams (the readers, Session/, Runs/, the applications) from these pieces, never through
// T(english, romanian), and no piece writes a text the report words in the run's language (an
// assumed scale's source, the RA axis's source, an unreadable frame's error): the log reads the
// same whatever --lang says (R-071). Nothing here decides anything; ADR 0029, after FlatCheck's
// LogText (its ADR 0070).

using System.Globalization;

namespace LightsCheck.Core;

/// <summary>The diagnostic log's way of writing values.</summary>
internal static class LogText
{
    /// <summary>The invariant culture, for every number in the log.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A number with six significant digits, invariant: <c>0.123457</c>, <c>30000</c>, <c>NaN</c>.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The text.</returns>
    internal static string Num(double v) => v.ToString("G6", Inv);

    /// <summary>A number that may be missing: <see cref="Num(double)"/>, or <c>none</c>.</summary>
    /// <param name="v">The value, or null.</param>
    /// <returns>The text.</returns>
    internal static string Num(double? v) => v is double d ? Num(d) : "none";

    /// <summary>An integer, invariant.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The text.</returns>
    internal static string Int(long v) => v.ToString(Inv);

    /// <summary>An elapsed time in milliseconds: <c>1141 ms</c>. The log's short form; the long one is <see cref="Seconds"/>.</summary>
    /// <param name="ms">The milliseconds.</param>
    /// <returns>The text.</returns>
    internal static string Ms(long ms) => Int(ms) + " ms";

    /// <summary>
    /// An elapsed time in seconds with one decimal: <c>12.9 s</c>, never whole seconds, so
    /// <c>0.8 s</c> does not read as nothing.
    /// </summary>
    /// <param name="elapsed">The time.</param>
    /// <returns>The text.</returns>
    internal static string Seconds(TimeSpan elapsed) => elapsed.TotalSeconds.ToString("F1", Inv) + " s";

    /// <summary>A text that may be missing or empty: itself, or <c>none</c>.</summary>
    /// <param name="s">The text, or null.</param>
    /// <returns>The text.</returns>
    internal static string Str(string? s) => string.IsNullOrEmpty(s) ? "none" : s;

    /// <summary>A flag as <c>yes</c> or <c>no</c>.</summary>
    /// <param name="b">The flag.</param>
    /// <returns>The text.</returns>
    internal static string Yes(bool b) => b ? "yes" : "no";

    /// <summary>A flag that may be missing: <see cref="Yes(bool)"/>, or <c>none</c>.</summary>
    /// <param name="b">The flag, or null.</param>
    /// <returns>The text.</returns>
    internal static string Yes(bool? b) => b is bool x ? Yes(x) : "none";

    /// <summary>A time as <c>2026-10-06 23:35:03</c>, invariant, or <c>none</c>.</summary>
    /// <param name="t">The time, as the file gave it (UTC for a FITS, the camera's clock for a raw), or null.</param>
    /// <returns>The text.</returns>
    internal static string Time(DateTime? t) => t is DateTime d ? d.ToString("yyyy-MM-dd HH:mm:ss", Inv) : "none";

    /// <summary>A size in mebibytes with one decimal: <c>23.9 MiB</c>.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The text.</returns>
    internal static string MiB(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F1", Inv) + " MiB";

    /// <summary>A size in kibibytes with one decimal: <c>12.3 KiB</c>.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The text.</returns>
    internal static string KiB(long bytes) => (bytes / 1024.0).ToString("F1", Inv) + " KiB";

    /// <summary>A file's size in mebibytes, or a question mark when it cannot be read.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The text.</returns>
    internal static string FileMiB(string path) => FileSize(path, MiB);

    /// <summary>A file's size in kibibytes, or a question mark when it cannot be read.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The text.</returns>
    internal static string FileKiB(string path) => FileSize(path, KiB);

    /// <summary>A list of values as <c>[a, b, c]</c>.</summary>
    /// <param name="items">The values; null gives <c>[]</c>.</param>
    /// <returns>The text.</returns>
    internal static string List(IEnumerable<string>? items) => "[" + string.Join(", ", items ?? []) + "]";

    /// <summary>
    /// What a loaded frame is, in one line: its size and reader, the raster, the planes, BITPIX,
    /// the CFA order and where it was read, the scale the file carries, the camera, the shot, the
    /// pixel size and the lens.
    /// </summary>
    /// <param name="path">The file read.</param>
    /// <param name="f">The frame; its image is null after a metadata read.</param>
    /// <param name="reader"><c>raw</c> or <c>fits</c>.</param>
    /// <returns>The text, without the leading verb.</returns>
    internal static string Frame(string path, LoadedFrame f, string reader)
    {
        ArgumentNullException.ThrowIfNull(f);
        string raster = f.Image is not null ? Int(f.Image.W) + "x" + Int(f.Image.H)
            : f.Width > 0 ? Int(f.Width) + "x" + Int(f.Height) + " (no pixels)"
            : "no pixels";
        string cfa = f.Cfa is not null ? f.Cfa.Letters + " (" + f.Cfa.Source + ")" : "none";
        // A FITS carries no scale (FrameRun assumes one from BITPIX, in the run's language); a raw
        // carries libraw's, whose source is not translated.
        string scale = f.Scale is not null ? Num(f.Scale.White) + "/" + Num(f.Scale.Black) + " (" + f.Scale.Source + ")" : "none";
        return path + " (" + FileMiB(path) + ") reader=" + reader + " " + raster
            + " planes=" + Int(f.Planes) + " bitpix=" + Int(f.Bitpix) + " cfa=" + cfa + " scale=" + scale
            + " camera=" + Str(f.Camera) + " focal=" + Num(f.FocalMm) + " fnumber=" + Num(f.FNumber)
            + " exposure=" + Num(f.ExposureSeconds) + " iso=" + Num(f.Iso) + " time=" + Time(f.Timestamp)
            + " pixel=" + Num(f.PixelSizeUm) + " focal35=" + Num(f.FocalLength35Mm)
            + " lens=" + Lens(f) + " range=" + LensRange(f);
    }

    /// <summary>
    /// The FITS keywords that decide how a frame is read and what the run knows of its shot, as
    /// <c>KEY=value</c>, <c>none</c> for the missing ones.
    /// </summary>
    /// <param name="header">The header's keywords; null gives every key as <c>none</c>.</param>
    /// <returns>The text.</returns>
    internal static string FitsKeys(IReadOnlyDictionary<string, string>? header)
    {
        string[] keys =
        [
            "XTENSION", "NAXIS", "NAXIS1", "NAXIS2", "NAXIS3", "BITPIX", "BZERO", "BSCALE", "BAYERPAT", "ROWORDER",
            "XBAYROFF", "YBAYROFF", "INSTRUME", "FOCALLEN", "XPIXSZ", "XBINNING", "EXPTIME", "ISOSPEED", "DATE-OBS",
            "STACKCNT", "CTYPE1", "CRVAL2",
        ];
        return string.Join(" ", keys.Select(k => k + "=" + (header is not null && header.TryGetValue(k, out string? v) ? Str(v?.Trim()) : "none")));
    }

    /// <summary>A session as it was found: the root, the lights' folder and count, the biases and the darks.</summary>
    /// <param name="s">The session.</param>
    /// <returns>The text.</returns>
    internal static string Session(SessionFolder s)
    {
        ArgumentNullException.ThrowIfNull(s);
        string kind = s.Lights.Count > 0 ? Path.GetExtension(s.Lights[0]) : "none";
        return "root=" + s.Root + " lights=" + s.LightsDir + " (" + Int(s.Lights.Count) + " frames, " + kind + ")"
            + " biases=" + Str(s.BiasesDir) + " (" + Int(s.Biases.Count) + ")"
            + " darks=" + Str(s.DarksDir) + " (" + Int(s.Darks.Count) + ")";
    }

    /// <summary>
    /// Every figure of one frame's measurement: the raster and its scale, each channel, the
    /// background, the noise, the stars, their shape in the whole frame and per zone, the halo, the
    /// satellite trails, the registration stars. Never a pixel and never the list of shapes.
    /// </summary>
    /// <param name="m">The measurement.</param>
    /// <returns>The text.</returns>
    internal static string Measured(FrameMeasurement m)
    {
        ArgumentNullException.ThrowIfNull(m);
        return Int(m.Width) + "x" + Int(m.Height) + " scale=" + Num(m.Scale.White) + "/" + Num(m.Scale.Black)
            + " channels=" + List(m.Channels.Select(c => c.Name + ": median " + Num(c.Median) + " saturated " + Int(c.Saturated)
                + " min " + Num(c.Min) + " hp " + Num(c.HighPassSigma)))
            + " background median=" + Num(m.BackgroundMedian) + " min=" + Num(m.BackgroundMin) + " max=" + Num(m.BackgroundMax)
            + " centre=" + Num(m.BackgroundCentre) + " corners=" + List(m.BackgroundCorners.Select(c => Num(c)))
            + " noiseSigma=" + Num(m.NoiseSigma)
            + " stars detected=" + Int(m.Detected) + " bright=" + Int(m.DetectedBright) + " saturated=" + Int(m.SaturatedStars)
            + " shapes=" + Int(m.Shapes.Count)
            + " fwhm=" + Num(m.FwhmMedian) + " p90 " + Num(m.FwhmP90)
            + " elongation=" + Num(m.ElongationMedian) + " p90 " + Num(m.ElongationP90) + " elongatedFraction=" + Num(m.ElongatedFraction)
            + " angle=" + Num(m.AngleDeg) + " coherence=" + Num(m.AngleCoherence)
            + " zones centre " + ZoneOf(m.Centre) + " mid " + ZoneOf(m.Mid) + " edge " + ZoneOf(m.Edge)
            + " halo=" + Num(m.Halo)
            + " trails=" + Int(m.Trails.Count) + " " + List(m.Trails.Take(5).Select(t => Num(t.Length) + " px at (" + Int(t.X) + ", " + Int(t.Y) + ")"))
            + " registrationStars=" + Int(m.RegistrationStars.Count);
    }

    /// <summary>One frame's registration against the reference.</summary>
    /// <param name="r">The registration.</param>
    /// <returns>The text.</returns>
    internal static string Registered(RegistrationResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return (r.Ok ? "ok" : "failed") + " matches=" + Int(r.Matches) + " votes=" + Int(r.Votes) + " dx=" + Num(r.Dx) + " dy=" + Num(r.Dy)
            + " rotation=" + Num(r.RotationDeg) + " scale=" + Num(r.Scale) + " rms=" + Num(r.Rms);
    }

    /// <summary>The timing of the series: its start and end, the span, the integration, the duty.</summary>
    /// <param name="c">The cadence.</param>
    /// <returns>The text.</returns>
    internal static string Timing(Cadence c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return "start=" + Time(c.Start) + " end=" + Time(c.End) + " span=" + Num(c.SpanSeconds) + " s integration="
            + Num(c.IntegrationSeconds) + " s timedFrames=" + Int(c.TimedFrames) + " dutyPct=" + Num(c.DutyPct);
    }

    /// <summary>Every figure of the dithering the verdicts and the tables rest on; the per-frame lists are in the report.</summary>
    /// <param name="d">The dithering.</param>
    /// <returns>The text.</returns>
    internal static string Dither(DitherAnalysis d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return "registered=" + Int(d.Registered) + " moves=" + Int(d.Moves) + " pauses=" + Int(d.Pauses)
            + " pausesWithoutMove=" + Int(d.PausesWithoutMove) + " movesWithoutPause=" + Int(d.MovesWithoutPause)
            + " framesBetweenMoves=" + List(d.FramesBetweenMoves.OrderBy(k => k.Key).Select(k => Int(k.Key) + ": " + Int(k.Value)))
            + " longestStretch=" + Int(d.LongestStretchFrames) + " frames (" + Int(d.LongestStretchFirst) + "-" + Int(d.LongestStretchLast) + ")"
            + " medianMove=" + Num(d.MedianMove)
            + " sizes=" + List(d.Sizes.Select(s => Num(s.Lower) + "-" + Num(s.Upper) + ": " + Int(s.Count)))
            + " axes x=" + Int(d.MovesOnXOnly) + " y=" + Int(d.MovesOnYOnly) + " both=" + Int(d.MovesOnBoth)
            + " sides x+=" + Int(d.MovesXPlus) + " x-=" + Int(d.MovesXMinus) + " y+=" + Int(d.MovesYPlus) + " y-=" + Int(d.MovesYMinus)
            + " within tight=" + Int(d.WithinTight) + " mid=" + Int(d.WithinMid) + " loose=" + Int(d.WithinLoose)
            + " isolatedLoose=" + Int(d.IsolatedLoose) + " distinct mid=" + Int(d.DistinctMid) + " loose=" + Int(d.DistinctLoose)
            + " largestClusterTight=" + Int(d.LargestClusterTight) + " sharingPairs=" + Int(d.SharingPairs)
            + " patternSurviving=" + Num(d.PatternSurviving) + " walkingStretch=" + StretchOf(d.WalkingStretch)
            + " runsMid=" + Int(d.RunsMid.Count) + " runsLoose=" + Int(d.RunsLoose.Count)
            + " span x=" + Num(d.SpanX) + " y=" + Num(d.SpanY)
            + " hotPixelsOk=" + Yes(d.HotPixelsOk) + " walkingStretches=" + Int(d.WalkingStretches.Count);
    }

    /// <summary>The sensor's fixed pattern read from the lights' central crops.</summary>
    /// <param name="p">The pattern.</param>
    /// <returns>The text.</returns>
    internal static string Pattern(SensorPattern p)
    {
        ArgumentNullException.ThrowIfNull(p);
        IEnumerable<string> positions = Enumerable.Range(0, Math.Min(p.Names.Count, Math.Min(p.PatternSigma.Count, p.FrameSigma.Count)))
            .Select(i => p.Names[i] + ": pattern " + Num(p.PatternSigma[i]) + " frame " + Num(p.FrameSigma[i]));
        return "status=" + p.Status + " frames=" + Int(p.Frames) + " cropSize=" + Int(p.CropSize) + " pixels=" + Int(p.Pixels)
            + " hot=" + Int(p.Hot) + " hotPerMpx=" + Num(p.HotPerMpx) + " sigma=" + List(positions);
    }

    /// <summary>The read noise per CFA position, from the pair of biases.</summary>
    /// <param name="noise">The read noise; null when none was measured.</param>
    /// <returns>The text.</returns>
    internal static string Noise(IReadOnlyList<ReadNoise>? noise) =>
        noise is null ? "none" : List(noise.Select(n => n.Name + ": pair " + Num(n.PairSigma) + " hp " + Num(n.HighPassSigma)));

    /// <summary>The plate solve's WCS as the series reads it.</summary>
    /// <param name="w">The WCS; null without one.</param>
    /// <returns>The text.</returns>
    internal static string Solve(Wcs? w) =>
        w is null ? "none"
        : "cdelt=" + Num(w.Cdelt1) + "/" + Num(w.Cdelt2) + " pc=" + List([Num(w.Pc11), Num(w.Pc12), Num(w.Pc21), Num(w.Pc22)])
            + " dec=" + Num(w.DecDeg) + " framesFlipped=" + Yes(w.FramesFlipped) + " raAxis=" + w.RaAxis;

    /// <summary>
    /// Every figure of the series analysis the verdicts rest on: the class counts, the session's
    /// FWHM and stars, the trends, focus and dew, the drift, walking noise, the rotation, the RA axis
    /// and the trails' axis, the stars' drift, the polar alignment, the read noise, the swamping;
    /// each verdict by its key. The RA axis's source is left out: it is the report's words.
    /// </summary>
    /// <param name="s">The analysis.</param>
    /// <returns>The text.</returns>
    internal static string Series(SeriesAnalysis s)
    {
        ArgumentNullException.ThrowIfNull(s);
        string starDrift = s.StarDriftPerMin is StarDrift d
            ? "ra " + Num(d.RaPxPerMin) + " dec " + Num(d.DecPxPerMin) + " px/min signed=" + Yes(d.Signed)
            : "none";
        IEnumerable<string> swamping = s.Swamping is null ? []
            : s.Swamping.Select((channels, i) => "frame " + (i < s.SwampingFrames.Count ? Int(s.SwampingFrames[i]) : "?") + " "
                + List(channels.Select(c => c.Name + ": sky " + Num(c.Sky) + " total " + Num(c.TotalSigma) + " shot " + Num(c.ShotSigma)
                    + " gain " + Num(c.GainEPerAdu) + " ratio " + Num(c.SwampingRatio) + " penaltyPct " + Num(c.PenaltyPct))));
        return "classes good=" + Int(s.GoodCount) + " marginal=" + Int(s.MarginalCount) + " bad=" + Int(s.BadCount) + " unknown=" + Int(s.UnknownCount)
            + " sessionFwhm=" + Num(s.SessionFwhm) + " sessionStars=" + Num(s.SessionStars)
            + " trends fwhm " + TrendOf(s.Fwhm) + " halo " + TrendOf(s.Halo) + " background " + TrendOf(s.Background) + " stars " + TrendOf(s.Stars)
            + " focusHeld=" + Yes(s.FocusHeld) + " dewSuspected=" + Yes(s.DewSuspected)
            + " drift x=" + Num(s.DriftXPerMin) + " y=" + Num(s.DriftYPerMin) + " px/min pairs=" + Int(s.DriftPairs) + " approximate=" + Yes(s.DriftApproximate)
            + " medianExposure=" + Num(s.MedianExposureSeconds) + " s usualExposure=" + Num(s.UsualExposureSeconds) + " s driftPerExposure=" + Num(s.DriftPerExposurePx) + " px axis=" + s.DriftAxis
            + " walking streak=" + Num(s.WalkingStreakPx) + " px sharedFrames=" + Int(s.WalkingSharedFrames) + " patternPiles=" + Yes(s.PatternPiles) + " noise=" + s.WalkingNoise
            + " rotation total=" + Num(s.RotationTotalDeg) + " deg perHour=" + Num(s.RotationPerHourDeg)
            + " cornerShiftPerExposure=" + Num(s.CornerShiftPerExposurePx) + " px"
            + " raAxis=" + s.RaAxis + " trailAngle=" + Num(s.TrailAngleDeg) + " trailOn=" + s.TrailOn
            + " starDrift=" + starDrift + " declination=" + Num(s.DeclinationDeg)
            + " polar rotationPerHour=" + Num(s.PolarRotationPerHourDeg) + " error=" + Num(s.PolarErrorMinArcmin) + ".." + Num(s.PolarErrorMaxArcmin)
            + " arcmin drift=" + Num(s.PolarDriftMinPx) + ".." + Num(s.PolarDriftMaxPx) + " px elongation=" + Num(s.PolarElongation)
            + " raDriftMax=" + Num(s.PolarRaDriftMaxArcsecPerMin) + " arcsec/min verdict=" + s.PolarAlignment
            + " readNoise=" + Noise(s.ReadNoise) + " swamping=" + List(swamping);
    }

    /// <summary>
    /// One frame's class in the series, with the flags behind it and the mount's move behind its
    /// dither: <c>Bad flags=[Trailed value 1.98 threshold 1.5 angle 90 on Ra] move=none</c>. A
    /// trailing flag's detail is its angle; an unreadable frame's detail, its error, is left out:
    /// the frame's own line has it with its exception.
    /// </summary>
    /// <param name="c">The frame's class.</param>
    /// <param name="flags">The frame's flags.</param>
    /// <param name="move">The mount's move behind the frame's dither; null for none.</param>
    /// <returns>The text.</returns>
    internal static string Classified(FrameClass c, IReadOnlyList<FrameFlag> flags, MountMove? move)
    {
        ArgumentNullException.ThrowIfNull(flags);
        string moved = move is null ? "none" : "ra " + Num(move.RaPx) + " dec " + Num(move.DecPx) + " px signed=" + Yes(move.Signed);
        return c + " flags=" + List(flags.Select(f => f.Kind + " value " + Num(f.Value) + " threshold " + Num(f.Threshold)
            + (f.Kind is FlagKind.Trailed or FlagKind.MarginalTrail ? " angle " + Str(f.Detail) + " on " + f.On : "")))
            + " move=" + moved;
    }

    /// <summary>A file's size in the given unit, or a question mark when it cannot be read: the log never throws for a size.</summary>
    /// <param name="path">The file.</param>
    /// <param name="unit">How the bytes are written: <see cref="KiB"/> or <see cref="MiB"/>.</param>
    /// <returns>The text.</returns>
    private static string FileSize(string path, Func<long, string> unit)
    {
        try
        {
            return unit(new FileInfo(path).Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return "?";
        }
    }

    /// <summary>
    /// A lens as a frame names it: <c>id/mount/name</c>, each <c>none</c> when missing; <c>none</c>
    /// alone when the frame names no lens at all.
    /// </summary>
    /// <param name="f">The frame.</param>
    /// <returns>The text.</returns>
    private static string Lens(LoadedFrame f) =>
        f.LensId is not null || f.LensMount is not null || f.LensName is not null
            ? (f.LensId is long id ? Int(id) : "none") + "/" + Str(f.LensMount) + "/" + Str(f.LensName)
            : "none";

    /// <summary>A lens's range as a frame carries it: <c>18-55 mm f/3.5</c>; <c>none</c> when the frame carries no range.</summary>
    /// <param name="f">The frame.</param>
    /// <returns>The text.</returns>
    private static string LensRange(LoadedFrame f) =>
        f.LensMinFocalMm is not null || f.LensMaxFocalMm is not null
            ? Num(f.LensMinFocalMm) + "-" + Num(f.LensMaxFocalMm) + " mm f/" + Num(f.LensMaxAperture)
            : "none";

    /// <summary>One zone's star shape: <c>(fwhm 3.01, elongation 1.05, stars 12)</c>.</summary>
    /// <param name="z">The zone.</param>
    /// <returns>The text.</returns>
    private static string ZoneOf(ZoneShape z) =>
        "(fwhm " + Num(z.Fwhm) + ", elongation " + Num(z.Elongation) + ", stars " + Int(z.Stars) + ")";

    /// <summary>A trend over the series: <c>(3.01 to 3.42, 87)</c>, its first and last values and how many frames it spans.</summary>
    /// <param name="t">The trend.</param>
    /// <returns>The text.</returns>
    private static string TrendOf(Trend t) => "(" + Num(t.First) + " to " + Num(t.Last) + ", " + Int(t.Count) + ")";

    /// <summary>A stretch of frames: <c>frames 12-19 (8)</c>, or <c>none</c>.</summary>
    /// <param name="s">The stretch, or null.</param>
    /// <returns>The text.</returns>
    private static string StretchOf(FrameStretch? s) =>
        s is null ? "none" : "frames " + Int(s.FirstIndex) + "-" + Int(s.LastIndex) + " (" + Int(s.Frames) + ")";
}
