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

// LogText.cs - how a value is written in the diagnostic log: English, invariant numbers, a
// missing value as "none", sizes in KiB and MiB. The log's lines are composed at the seams
// (the readers, Runs/, the applications) from these pieces, never through T(english, romanian):
// the log reads the same whatever --lang says (R-110). Nothing here decides anything.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FlatCheck
{
    /// <summary>The diagnostic log's way of writing values.</summary>
    internal static class LogText
    {
        /// <summary>The invariant culture, for every number in the log.</summary>
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A number with six significant digits, invariant: <c>0.123457</c>,
        /// <c>30000</c>, <c>NaN</c>.</summary>
        /// <param name="v">The value.</param>
        /// <returns>The text.</returns>
        internal static string Num(double v) => v.ToString("G6", Inv);

        /// <summary>A number that may be missing: <see cref="Num(double)"/>, or
        /// <c>none</c>.</summary>
        /// <param name="v">The value, or null.</param>
        /// <returns>The text.</returns>
        internal static string Num(double? v) => v.HasValue ? Num(v.Value) : "none";

        /// <summary>An integer, invariant.</summary>
        /// <param name="v">The value.</param>
        /// <returns>The text.</returns>
        internal static string Int(long v) => v.ToString(Inv);

        /// <summary>An elapsed time in milliseconds: <c>1141 ms</c>. The log's short form; the
        /// long one is <see cref="Seconds"/>.</summary>
        /// <param name="ms">The milliseconds.</param>
        /// <returns>The text.</returns>
        internal static string Ms(long ms) => Int(ms) + " ms";

        /// <summary>An elapsed time in seconds with one decimal: <c>12.9 s</c>, never whole
        /// seconds, so <c>0.8 s</c> does not read as nothing.</summary>
        /// <param name="elapsed">The time.</param>
        /// <returns>The text.</returns>
        internal static string Seconds(TimeSpan elapsed) => elapsed.TotalSeconds.ToString("F1", Inv) + " s";

        /// <summary>A text that may be missing or empty: itself, or <c>none</c>.</summary>
        /// <param name="s">The text, or null.</param>
        /// <returns>The text.</returns>
        internal static string Str(string s) => string.IsNullOrEmpty(s) ? "none" : s;

        /// <summary>A size in mebibytes with one decimal: <c>0.1 MiB</c>.</summary>
        /// <param name="bytes">The size in bytes.</param>
        /// <returns>The text.</returns>
        internal static string MiB(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F1", Inv) + " MiB";

        /// <summary>A size in kibibytes with one decimal: <c>12.3 KiB</c>.</summary>
        /// <param name="bytes">The size in bytes.</param>
        /// <returns>The text.</returns>
        internal static string KiB(long bytes) => (bytes / 1024.0).ToString("F1", Inv) + " KiB";

        /// <summary>A file's size in mebibytes, or a question mark when it cannot be
        /// read.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The text.</returns>
        internal static string FileMiB(string path) => FileSize(path, MiB);

        /// <summary>A file's size in kibibytes, or a question mark when it cannot be
        /// read.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The text.</returns>
        internal static string FileKiB(string path) => FileSize(path, KiB);

        /// <summary>A file's size in the given unit, or a question mark when it cannot be
        /// read: the log never throws for a size.</summary>
        /// <param name="path">The file.</param>
        /// <param name="unit">How the bytes are written: <see cref="KiB"/> or <see cref="MiB"/>.</param>
        /// <returns>The text.</returns>
        static string FileSize(string path, Func<long, string> unit)
        {
            try { return unit(new FileInfo(path).Length); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            { return "?"; }
        }

        /// <summary>A lens as a frame names it: <c>id/mount/name</c>, each <c>none</c> when
        /// missing; <c>none</c> alone when the frame names no lens at all.</summary>
        /// <param name="f">The frame.</param>
        /// <returns>The text.</returns>
        static string Lens(LoadedFrame f)
            => f.LensId.HasValue || f.LensMount != null || f.LensName != null
                ? (f.LensId.HasValue ? Int(f.LensId.Value) : "none") + "/" + Str(f.LensMount) + "/" + Str(f.LensName)
                : "none";

        /// <summary>A lens's range as a frame carries it: <c>18-55 mm f/3.5</c>; <c>none</c>
        /// when the frame carries no range.</summary>
        /// <param name="f">The frame.</param>
        /// <returns>The text.</returns>
        static string Range(LoadedFrame f)
            => f.LensMinFocalMm.HasValue || f.LensMaxFocalMm.HasValue
                ? Num(f.LensMinFocalMm) + "-" + Num(f.LensMaxFocalMm) + " mm f/" + Num(f.LensMaxAperture)
                : "none";

        /// <summary>A list of values as <c>[a, b, c]</c>.</summary>
        /// <param name="items">The values; null gives <c>[]</c>.</param>
        /// <returns>The text.</returns>
        internal static string List(IEnumerable<string> items) => "[" + string.Join(", ", items ?? Enumerable.Empty<string>()) + "]";

        /// <summary>
        /// What a loaded frame is, in one line: its size and reader, the raster, the CFA order,
        /// the scale, the camera, the shot and the lens, the preview.
        /// </summary>
        /// <param name="path">The file read.</param>
        /// <param name="f">The frame; its image may be null (a metadata read).</param>
        /// <param name="reader"><c>fits</c> or <c>raw</c>.</param>
        /// <returns>The text, without the leading verb.</returns>
        internal static string Frame(string path, LoadedFrame f, string reader)
        {
            string raster = f.Image != null ? Int(f.Image.W) + "x" + Int(f.Image.H) : "no pixels";
            string cfa = f.Cfa != null ? f.Cfa.Letters + " (" + f.Cfa.Source + ")" : "none";
            string scale = f.Scale != null ? Num(f.Scale.White) + "/" + Num(f.Scale.Black) + " (" + f.Scale.Source + ")" : "none";
            string preview = f.Preview != null
                ? (f.Preview.IsJpeg ? "jpeg " : "rgb ") + Int(f.Preview.W) + "x" + Int(f.Preview.H) + " " + KiB(f.Preview.Data?.Length ?? 0)
                : "none";
            return path + " (" + FileMiB(path) + ") reader=" + reader + " " + raster
                 + " planes=" + Int(f.Planes) + " bitpix=" + Int(f.Bitpix) + " cfa=" + cfa + " scale=" + scale
                 + " camera=" + Str(f.Camera) + " focal=" + Num(f.FocalMm) + " fnumber=" + Num(f.FNumber)
                 + " lens=" + Lens(f) + " range=" + Range(f) + " preview=" + preview;
        }

        /// <summary>
        /// What the identification concluded about a shot: the focal length, the f-number and
        /// the crop with where each came from, the camera, the raw's lens, the candidates by
        /// their catalogue key.
        /// </summary>
        /// <param name="id">The identity; null gives <c>none</c>.</param>
        /// <returns>The text.</returns>
        internal static string Shot(ShotIdentity id)
        {
            if (id == null) return "none";
            string cropFrom = id.CropSource == "--crop" ? "--crop" : id.CropFromTable ? "lensfun table" : "unknown";
            LoadedFrame raw = id.Raw;
            string rawLens = raw == null ? "none" : Lens(raw) + " range " + Range(raw);
            return "focal=" + Num(id.FocalMm) + " (" + Str(id.FocalSource) + ") fstop=" + Num(id.FStop) + " (" + Str(id.FStopSource)
                 + ") crop=" + Num(id.Crop) + " (" + cropFrom + ") camera=" + Str(id.Camera) + " raw=" + Str(id.RawName)
                 + " rawLens=" + rawLens + " candidates=" + Int(id.Candidates?.Count ?? 0) + " "
                 + List(id.Candidates?.Select(c => c.Key)) + (id.CandidatesByRangeOnly ? " (by range only)" : "");
        }

        /// <summary>A flag as <c>yes</c> or <c>no</c>.</summary>
        /// <param name="b">The flag.</param>
        /// <returns>The text.</returns>
        internal static string Yes(bool b) => b ? "yes" : "no";

        // ------------------------------------------------------------------ the results
        // One line per result, with every figure a verdict rests on; the words of the reports
        // are not repeated (they are localized, and the report is beside the log anyway).

        /// <summary>The five tests of a structure run and their verdicts.</summary>
        /// <param name="r">The result.</param>
        /// <returns>The text.</returns>
        internal static string Structure(StructureResult r)
            => "raster " + Int(r.Width) + "x" + Int(r.Height) + " rawMedian=" + Num(r.RawMedian) + " rawMax=" + Num(r.RawMax)
             + " fitCentre=(" + Num(r.FitCx) + ", " + Num(r.FitCy) + ") sensorCentre=(" + Num(r.SensorCx) + ", " + Num(r.SensorCy) + ")"
             + " centroidOffsetPct=" + Num(r.CentroidOffsetPct) + " centroidValid=" + Yes(r.CentroidValid) + " tiltPct=" + Num(r.TiltPct)
             + " corners=" + List(r.Corners?.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => StructureReport.EnglishCorner(c.Key) + "=" + Num(c.Value)))
             + " cornerMean=" + Num(r.CornerMean) + " centreMedian=" + Num(r.CentreMedian) + " vignetteStops=" + Num(r.VignetteStops)
             + " cornerSpreadPct=" + Num(r.CornerSpreadPct) + " azimuthalMaxPct=" + Num(r.AzimuthalMaxPct)
             + " residualPtpPct=" + Num(r.ResidualPtpPct) + " maxTilePct=" + Num(r.MaxTilePct)
             + " localSpotPct=" + Num(r.LocalSpotPct) + " at (" + Int(r.LocalSpotX) + ", " + Int(r.LocalSpotY) + ") flagged=" + Yes(r.LocalSpotFlagged)
             + " verdicts corner=" + Str(r.CornerVerdict) + " azimuthal=" + Str(r.AzimuthalVerdict) + " residual=" + Str(r.ResidualVerdict)
             + " tilt=" + Str(r.TiltVerdict) + " maxTile=" + Str(r.MaxTileVerdict) + " worst=" + Str(r.Verdict);

        /// <summary>A lens profile by its catalogue key and its settings.</summary>
        /// <param name="p">The profile; null gives <c>none</c>.</param>
        /// <returns>The text.</returns>
        internal static string Profile(LensProfile p)
            => p == null ? "none"
             : p.Lens.Key + " at " + Num(p.FocalMm) + " mm f/" + Num(p.FStop) + " distance " + Num(p.DistanceM) + " m (" + Str(p.File) + ")";

        /// <summary>One profile's depth against the master.</summary>
        /// <param name="d">The row; null gives <c>none</c>.</param>
        /// <returns>The text.</returns>
        internal static string Depth(ProfileDepth d)
            => d == null ? "none"
             : Profile(d.Profile) + ": cornerStops=" + Num(d.CornerStops) + " diffStops=" + Num(d.DiffStops) + " errorPct=" + Num(d.ErrorPct)
             + " verdict=" + Str(d.Verdict) + " cameraCrop=" + Num(d.CameraCrop) + " extrapolation=" + Num(d.Extrapolation);

        /// <summary>The comparison of a master's vignetting with the lens profiles.</summary>
        /// <param name="c">The comparison; null gives <c>none</c>.</param>
        /// <returns>The text.</returns>
        internal static string Comparison(VignettingComparison c)
            => c == null ? "none"
             : "masterStops=" + Num(c.MasterStops) + " gap=" + c.Gap + " spreadStops=" + Num(c.SpreadStops) + " verdict=" + Str(c.Verdict)
             + " nearest=" + Depth(c.Nearest) + " skippedForCrop=" + List(c.SkippedForCrop?.Select(l => l.Key))
             + " profiles=" + List(c.Profiles?.Select(Depth));

        /// <summary>The field-of-view check of a <c>--light</c> run.</summary>
        /// <param name="s">The check.</param>
        /// <returns>The text.</returns>
        internal static string Suitability(SuitabilityResult s)
            => "level=" + s.Level + " fovDeg=" + Num(s.FovDeg) + " focalMm=" + Num(s.FocalMm) + " canRun=" + Yes(s.CanRun);

        /// <summary>One light's plane and radial fit.</summary>
        /// <param name="r">The result.</param>
        /// <returns>The text.</returns>
        internal static string Light(LightResult r)
            => "radPct=" + Num(r.RadPct) + " planePct=" + Num(r.PlanePct) + " tiles kept=" + Int(r.KeptTiles) + "/" + Int(r.TotalTiles)
             + " grid=" + Int(r.TileRows) + "x" + Int(r.TileCols)
             + " a..e=" + List(new[] { r.A, r.B, r.C, r.D, r.E }.Select(Num)) + " verdict=" + Str(r.Verdict);

        /// <summary>The synthesis of a <c>--light</c> series.</summary>
        /// <param name="s">The result.</param>
        /// <returns>The text.</returns>
        internal static string LightSeries(LightSeriesResult s)
            => "frames=" + Int(s.Count) + " meanRad=" + Num(s.MeanRad) + " minRad=" + Num(s.MinRad) + " maxRad=" + Num(s.MaxRad)
             + " stdDev=" + Num(s.StdDev) + " sem=" + Num(s.Sem) + " skyCorr=" + Num(s.SkyCorr) + " skySpan=" + Num(s.SkySpan)
             + " corrApplies=" + Yes(s.CorrApplies) + " conclusive=" + Yes(s.Conclusive) + " reasons=" + Int(s.Reasons?.Length ?? 0)
             + " verdict=" + Str(s.Verdict);

        /// <summary>The exposure of a raw flat.</summary>
        /// <param name="r">The result.</param>
        /// <returns>The text.</returns>
        internal static string Exposure(ExposureResult r)
            => "white=" + Num(r.White) + " black=" + Num(r.Black) + " range=" + Num(r.Range)
             + " channelMedians=" + List(r.ChannelMedians?.Select(Num)) + " cfaSpread=" + Num(r.CfaSpread) + " isCfa=" + Yes(r.IsCfa)
             + " cfa=" + (r.Cfa != null ? r.Cfa.Letters : "none") + " brightest=" + Int(r.BrightestChannel)
             // The label, not the colour: a channel without a colour (no BAYERPAT, or mono)
             // has '\0' for it, which would put a NUL byte in the file.
             + " channels=" + List(r.Channels?.Select(c => Int(c.Position) + ":" + c.Label + "=" + Num(c.MedianPct) + "%"))
             + " centreMedianPct=" + Num(r.CentreMedianPct) + " globalMedianPct=" + Num(r.GlobalMedianPct) + " centreClipPct=" + Num(r.CentreClipPct)
             + " levelTooLow=" + Yes(r.LevelTooLow) + " looksDebayered=" + Yes(r.LooksDebayered) + " sky=" + Yes(r.IsSky)
             + " preview=" + Yes(r.Preview != null) + " verdict=" + r.Verdict;

        /// <summary>The equalisation's input and output sizes.</summary>
        /// <param name="r">The result.</param>
        /// <returns>The text.</returns>
        internal static string HistEq(HistEqResult r)
            => "isCfa=" + Yes(r.IsCfa) + " cfaSpread=" + Num(r.CfaSpread) + " cfa=" + (r.Cfa != null ? r.Cfa.Letters : "none")
             + " source " + Int(r.SourceW) + "x" + Int(r.SourceH) + " shrink=" + Int(r.Shrink) + " view " + Int(r.W) + "x" + Int(r.H)
             + " full " + Int(r.FullW) + "x" + Int(r.FullH) + " channels=" + Int(r.ChannelCount);

        /// <summary>What a smoothing removed, and the structure before and after.</summary>
        /// <param name="r">The result.</param>
        /// <returns>The text.</returns>
        internal static string Smooth(SmoothResult r)
            => "scalePx=" + Num(r.ScalePx) + " tilePx=" + Int(r.TilePx) + " sigmaTiles=" + Num(r.SigmaTiles)
             + " isCfa=" + Yes(r.IsCfa) + " cfaSpread=" + Num(r.CfaSpread) + " source " + Int(r.SourceW) + "x" + Int(r.SourceH)
             + " positions=" + List(r.Positions?.Select(p => "tiles " + Int(p.Tiles) + " rejected " + Int(p.Rejected)
                                                          + " rms " + Num(p.TileRms) + " fitFailed " + Int(p.FitFailed)))
             + " removed p1=" + Num(r.RemovedP1Pct) + "% p99=" + Num(r.RemovedP99Pct) + "% ptp=" + Num(r.RemovedPtpPct)
             + "% maxTile=" + Num(r.RemovedMaxTilePct) + "% spot=" + Num(r.RemovedSpotPct) + "% at (" + Int(r.RemovedSpotX) + ", "
             + Int(r.RemovedSpotY) + ") flagged=" + Yes(r.RemovedFlagged)
             + " texture in=" + Num(r.InputTexturePct) + "% out=" + Num(r.OutputTexturePct) + "%"
             + " before {" + (r.Before != null ? Structure(r.Before) : "none") + "} after {" + (r.After != null ? Structure(r.After) : "none") + "}";

        /// <summary>The scale advisor's trials and its proposal.</summary>
        /// <param name="a">The advice.</param>
        /// <returns>The text.</returns>
        internal static string Advice(SmoothScaleAdvice a)
            => "kind=" + a.Kind + " inputTexturePct=" + Num(a.InputTexturePct) + " scalePx=" + Num(a.ScalePx)
             + " trials=" + List(a.Trials?.Select(t => Num(t.ScalePx) + " px: texture " + Num(t.TextureAfterPct)
                                                     + "% azimuthalDelta " + Num(t.AzimuthalDelta) + " tiltDelta " + Num(t.TiltDelta)
                                                     + " cornerDelta " + Num(t.CornerDelta) + " gone=" + Yes(t.TextureGone)
                                                     + " lost=" + Yes(t.StructureLost)));

        /// <summary>A synthetic flat's request as completed, with where each value came
        /// from.</summary>
        /// <param name="q">The request.</param>
        /// <returns>The text.</returns>
        internal static string Request(SyntheticRequest q)
            => "lensText=" + Str(q.LensText) + " source=" + (q.Source.HasValue ? q.Source.Value.ToString() : "both")
             + " lens=" + (q.Lens != null ? q.Lens.Key : "none") + " focal=" + Num(q.FocalMm) + " (" + Str(q.FocalSource) + ")"
             + " fstop=" + Num(q.FStop) + " (" + Str(q.FStopSource) + ") crop=" + Num(q.CameraCrop);

        /// <summary>The synthetic flat built.</summary>
        /// <param name="r">The result.</param>
        /// <returns>The text.</returns>
        internal static string Synthetic(SyntheticFlatResult r)
            => "profile=" + Profile(r.Profile) + " raster " + Int(r.Width) + "x" + Int(r.Height) + " cameraCrop=" + Num(r.CameraCrop)
             + " stops corner=" + Num(r.CornerStops) + " longEdge=" + Num(r.LongEdgeStops) + " shortEdge=" + Num(r.ShortEdgeStops)
             + " halfDiagonal=" + Num(r.HalfDiagonalStops) + " extrapolation=" + Num(r.Extrapolation)
             + " profilesOfTheLens=" + Int(r.LensProfiles?.Count ?? 0);

        /// <summary>Two flats compared: the depth difference and the ratio's
        /// structure.</summary>
        /// <param name="r">The result.</param>
        /// <returns>The text.</returns>
        internal static string Against(AgainstResult r)
            => "raster " + Int(r.Width) + "x" + Int(r.Height) + " stops A=" + Num(r.A.VignetteStops) + " B=" + Num(r.B.VignetteStops)
             + " depthDiffStops=" + Num(r.DepthDiffStops) + " depthErrorPct=" + Num(r.DepthErrorPct) + " depthVerdict=" + Str(r.DepthVerdict)
             + " verdict=" + Str(r.Verdict) + " ratio {" + Structure(r.Structure) + "}";

        /// <summary>A split run: each part against the whole's nearest profile.</summary>
        /// <param name="r">The result.</param>
        /// <returns>The text.</returns>
        internal static string Split(SplitResult r)
            => "parts=" + Int(r.Parts.Count) + " framesPerPart=" + Int(r.FramesPerPart) + " reference=" + Profile(r.ReferenceProfile)
             + " map " + Num(r.MapRange.Lo) + ".." + Num(r.MapRange.Hi)
             + " columns=" + List(r.Parts.Append(r.Whole).Select(c => c.Path + " verdict=" + Str(c.Verdict)
                                                                    + " structure=" + Str(c.Outcome.Structure.Verdict)
                                                                    + " depth=" + Depth(c.Reference)));

        /// <summary>The header keywords that decide how a FITS is read, as
        /// <c>KEY=value</c>, <c>none</c> for the missing ones.</summary>
        /// <param name="header">The header's keywords.</param>
        /// <returns>The text.</returns>
        internal static string FitsKeys(IReadOnlyDictionary<string, string> header)
        {
            string[] keys =
            {
                "XTENSION", "NAXIS", "NAXIS1", "NAXIS2", "NAXIS3", "BITPIX", "BZERO", "BSCALE", "BAYERPAT", "ROWORDER",
                "XBAYROFF", "YBAYROFF", "INSTRUME", "FOCALLEN", "XPIXSZ", "XBINNING", "EXPTIME", "STACKCNT",
            };
            return string.Join(" ", keys.Select(k => k + "=" + (header != null && header.TryGetValue(k, out string v) ? Str(v?.Trim()) : "none")));
        }
    }
}
