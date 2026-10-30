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

using System.Diagnostics;
using NLog;
using static LightsCheck.Core.Ui;

namespace LightsCheck.Core;

/// <summary>
/// What a run of the whole series takes from the user: the pixel size when the files do not say
/// it, the parallelism.
/// </summary>
/// <param name="PixelSizeUm">
/// The pixel size in microns the user typed; null when none was typed, and then the run takes
/// the files' XPIXSZ, else the raw's own figure, else <see cref="D3300PixelUm"/> (R-066).
/// </param>
/// <param name="Threads">
/// How many frames are measured at once; 0 or less means half the processors, at least one.
/// </param>
/// <param name="Arrows">
/// Which SynScan arrows move east and north; null means <see cref="ArrowKeys.Default"/>.
/// </param>
internal sealed record SeriesOptions(double? PixelSizeUm = null, int Threads = 0, ArrowKeys? Arrows = null)
{
    /// <summary>
    /// The Nikon D3300's pixel pitch, 23.5 mm over 6000 pixels: the last fallback, when no file
    /// says the pixel size, none was typed and no raw gives a 35 mm-equivalent focal length.
    /// </summary>
    internal const double D3300PixelUm = 3.92;

    /// <summary>
    /// Whether a pixel size typed by the user can be used: the console and the window both ask
    /// here (R-004, R-029), so neither decides on its own.
    /// </summary>
    /// <param name="um">The pixel size in microns, as parsed.</param>
    /// <returns>True for a finite number greater than 0; false for 0, a negative, NaN or an infinity.</returns>
    internal static bool PixelSizeIsValid(double um) => double.IsFinite(um) && um > 0;
}

/// <summary>A direction key of the SynScan app (R-040).</summary>
internal enum Arrow
{
    Left,
    Right,
    Up,
    Down,
}

/// <summary>
/// Which SynScan arrow moves the mount east and which north; the other two follow. The
/// default is the owner's app, right east and up north (R-040, ADR 0014).
/// </summary>
/// <param name="East">The arrow that moves the mount east.</param>
/// <param name="North">The arrow that moves the mount north.</param>
internal sealed record ArrowKeys(Arrow East, Arrow North)
{
    internal static readonly ArrowKeys Default = new(Arrow.Right, Arrow.Up);

    internal Arrow West => East == Arrow.Left ? Arrow.Right : Arrow.Left;

    internal Arrow South => North == Arrow.Up ? Arrow.Down : Arrow.Up;

    /// <summary>The symbol the reports print for a key.</summary>
    /// <param name="a">The key.</param>
    /// <returns>One arrow character: left, right, up or down.</returns>
    internal static string Symbol(Arrow a) => a switch
    {
        Arrow.Left => "←",
        Arrow.Right => "→",
        Arrow.Up => "↑",
        _ => "↓",
    };

    /// <summary>The key a symbol stands for, null for anything else.</summary>
    /// <param name="symbol">
    /// One arrow character as <see cref="Symbol"/> prints it; null allowed.
    /// </param>
    /// <returns>The key, or null when the text is not one of the four symbols.</returns>
    internal static Arrow? FromSymbol(string? symbol) => symbol switch
    {
        "←" => Arrow.Left,
        "→" => Arrow.Right,
        "↑" => Arrow.Up,
        "↓" => Arrow.Down,
        _ => null,
    };
}

/// <summary>
/// The whole series measured: the session, every frame, the cadence, the dithering, the
/// series analysis, the sensor's pattern, and the pixel scale in arcseconds per pixel when
/// the focal length is known.
/// </summary>
/// <param name="Session">The session's folders and frame lists.</param>
/// <param name="Frames">
/// Every light, in the session's order; one that could not be measured carries its error.
/// </param>
/// <param name="Cadence">The timing of the series.</param>
/// <param name="Dither">The dithering, from the registration offsets.</param>
/// <param name="Series">The classes, trends, drift and verdicts of the series.</param>
/// <param name="Pattern">The sensor's fixed pattern read from the lights' central crops.</param>
/// <param name="PixelSizeUm">The pixel size used, in microns.</param>
/// <param name="PixelSizeSource">
/// Where the pixel size came from, as the report prints it (Romanian).
/// </param>
/// <param name="ArcsecPerPixel">
/// The pixel scale in arcseconds per pixel; null when no file gives a focal length.
/// </param>
/// <param name="ReferenceIndex">
/// The 1-based index of the registration's reference frame; 0 when no frame was measured.
/// </param>
/// <param name="Arrows">The SynScan arrows the report names the moves with.</param>
internal sealed record SeriesResult(
    SessionFolder Session,
    IReadOnlyList<AnalyzedFrame> Frames,
    Cadence Cadence,
    DitherAnalysis Dither,
    SeriesAnalysis Series,
    SensorPattern Pattern,
    double PixelSizeUm,
    string PixelSizeSource,
    double? ArcsecPerPixel,
    int ReferenceIndex,
    ArrowKeys Arrows)
{
    internal bool HasErrors => Frames.Any(f => f.Error is not null);
}

/// <summary>
/// Chains the whole analysis (R-026): opens the session, measures every light in parallel,
/// registers each to the reference (the first measured frame), reads the read noise from
/// the first two biases, and runs the cadence, dithering, pattern and series analyzers.
/// Nothing is written here; SeriesOutputs does that on request.
/// </summary>
internal static class SeriesRun
{
    /// <summary>
    /// Measures and analyses the whole series. A frame that cannot be decoded or measured keeps
    /// its metadata and error and is left out of the figures; the folder errors of
    /// <see cref="SessionFolder.Open"/> propagate, and a cancel throws
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="folder">The session folder.</param>
    /// <param name="options">The pixel size, the parallelism and the arrow keys; not null.</param>
    /// <param name="progress">
    /// Told (frames done, frames in all) after each frame, from the worker threads; null for none.
    /// </param>
    /// <param name="cancel">
    /// Stops the measuring and the read noise; the frames already started finish.
    /// </param>
    /// <returns>The whole series measured.</returns>
    internal static SeriesResult Run(string folder, SeriesOptions options, IProgress<(int Done, int Total)>? progress = null, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        int threads = options.Threads > 0 ? options.Threads : Math.Max(1, Environment.ProcessorCount / 2);
        ArrowKeys arrows = options.Arrows ?? ArrowKeys.Default;
        using RunClock clock = RunClock.Start(Log, "series", folder + " pixel=" + LogText.Num(options.PixelSizeUm)
            + " threads=" + LogText.Int(threads) + " east=" + arrows.East + " north=" + arrows.North);
        SessionFolder session = SessionFolder.Open(folder);
        clock.Lap("open");
        int n = session.Lights.Count;
        AnalyzedFrame[] frames = new AnalyzedFrame[n];
        PatternCrop?[] crops = new PatternCrop?[n];
        int done = 0;
        ParallelOptions po = new() { MaxDegreeOfParallelism = threads, CancellationToken = cancel };
        Parallel.For(0, n, po, i =>
        {
            string path = session.Lights[i];
            Stopwatch one = Stopwatch.StartNew();
            try
            {
                MeasuredFrame m = FrameRun.Measure(i + 1, path);
                frames[i] = new AnalyzedFrame(m.Info, m.Measurement, null, null);
                crops[i] = m.Crop;
                Log.Info("frame " + LogText.Int(i + 1) + "/" + LogText.Int(n) + " " + m.Info.FileName
                    + " detected=" + LogText.Int(m.Measurement.Detected) + " fwhm=" + LogText.Num(m.Measurement.FwhmMedian)
                    + " elongation=" + LogText.Num(m.Measurement.ElongationMedian) + " in " + LogText.Ms(one.ElapsedMilliseconds));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // One line per frame, with the measurement's exception: the metadata read that
                // follows meets the same file, so its outcome is said here, not in a second stack.
                long ms = one.ElapsedMilliseconds;
                FrameInfo info = FrameInfo.Read(i + 1, path, logFailure: false);
                Log.Warn(e, "frame " + LogText.Int(i + 1) + "/" + LogText.Int(n) + " " + path + " could not be measured after "
                    + LogText.Ms(ms) + "; its metadata " + (info.Error is null ? "was read" : "could not be read either") + "; its row carries the error");
                frames[i] = new AnalyzedFrame(info, null, null, e.Message);
            }

            progress?.Report((Interlocked.Increment(ref done), n));
        });
        clock.Lap("measure");

        // Registration against the first measured frame.
        int refIndex = Array.FindIndex(frames, f => f.Measurement is not null);
        if (refIndex >= 0)
        {
            IReadOnlyList<RegistrationStar> reference = frames[refIndex].Measurement!.RegistrationStars;
            int w = frames[refIndex].Measurement!.Width, h = frames[refIndex].Measurement!.Height;
            Parallel.For(0, n, po, i =>
            {
                if (frames[i].Measurement is FrameMeasurement m)
                {
                    frames[i] = frames[i] with { Registration = Registration.Register(reference, m.RegistrationStars, w, h) };
                }
            });
        }

        clock.Lap("register");
        if (Log.IsDebugEnabled)
        {
            Log.Debug("reference frame=" + (refIndex >= 0 ? LogText.Int(refIndex + 1) : "none"));
            for (int i = 0; i < n; i++)
            {
                if (frames[i].Registration is RegistrationResult r)
                {
                    Log.Debug("registered frame " + LogText.Int(i + 1) + ": " + LogText.Registered(r));
                }
            }
        }

        List<FrameInfo> infos = frames.Select(f => f.Info).ToList();
        Cadence cadence = CadenceAnalyzer.Analyze(infos);
        double?[] x = frames.Select(f => f.Registration is { Ok: true } r ? r.Dx : (double?)null).ToArray();
        double?[] y = frames.Select(f => f.Registration is { Ok: true } r ? r.Dy : (double?)null).ToArray();
        DitherAnalysis dither = DitherAnalyzer.Analyze(x, y, cadence.PauseSeconds);
        clock.Lap("dither");
        // The crops (1 MB apiece) are dropped once the pattern is read.
        SensorPattern pattern = PatternAnalyzer.Analyze(crops, frames.Select(f => f.Measurement?.Channels).ToList(), dither);
        Array.Clear(crops);
        clock.Lap("pattern");

        // The pixel scale: the files' XPIXSZ, else the typed size, else the raw's own, else the
        // D3300's (R-066); the words are the report's, the choice is PixelPitch's.
        (double pixel, PixelPitchSource source) = PixelPitch.Choose(infos, options.PixelSizeUm);
        string pixelSource = source switch
        {
            PixelPitchSource.File => T("from the file (XPIXSZ)", "din fisier (XPIXSZ)"),
            PixelPitchSource.Entered => T("entered", "introdus"),
            PixelPitchSource.Raw => T("from the raw (35 mm-equivalent focal length)", "din raw (focala echivalenta 35 mm)"),
            _ => T("default, Nikon D3300", "implicit, Nikon D3300"),
        };
        double? focal = infos.Select(f => f.FocalMm).FirstOrDefault(f => f is not null);
        double? scale = focal is double fm && fm > 0 ? 206.265 * pixel / fm : null;

        IReadOnlyList<ReadNoise>? readNoise = ReadNoise(session, cancel);
        clock.Lap("noise");
        // Raws are read top-down against Siril's bottom-up solve; FITS lights are stored as the
        // solve is (ADR 0023).
        Wcs? wcs = ReadWcs(session.Root) is Wcs solve ? solve with { FramesFlipped = session.LightsAreRaws } : null;
        SeriesAnalysis series = SeriesAnalyzer.Analyze(frames, cadence, dither, readNoise, wcs, scale);
        clock.Lap("analyze");
        if (Log.IsDebugEnabled)
        {
            LogFigures(frames, cadence, dither, pattern, pixel, source, focal, scale, readNoise, wcs, series);
        }

        clock.Done("frames=" + LogText.Int(n) + " measured=" + LogText.Int(frames.Count(f => f.Measurement is not null))
            + " errors=" + LogText.Int(frames.Count(f => f.Error is not null)) + " good=" + LogText.Int(series.GoodCount)
            + " marginal=" + LogText.Int(series.MarginalCount) + " bad=" + LogText.Int(series.BadCount)
            + " unknown=" + LogText.Int(series.UnknownCount) + " walking=" + series.WalkingNoise + " polar=" + series.PolarAlignment);
        return new SeriesResult(session, frames, cadence, dither, series, pattern, pixel, pixelSource, scale, refIndex + 1, arrows);
    }

    /// <summary>
    /// Writes, at Debug, the figures every verdict of the series rests on (R-071): the cadence, the
    /// dithering, the sensor's pattern, the pixel size and its source, the read noise, the WCS, the
    /// series analysis, and each frame's class with its flags.
    /// </summary>
    /// <param name="frames">The frames, in the session's order.</param>
    /// <param name="cadence">The timing of the series.</param>
    /// <param name="dither">The dithering.</param>
    /// <param name="pattern">The sensor's pattern.</param>
    /// <param name="pixel">The pixel size used, in microns.</param>
    /// <param name="source">Where the pixel size came from.</param>
    /// <param name="focal">The focal length the scale was computed with, in mm; null when no file gives one.</param>
    /// <param name="scale">The pixel scale in arcseconds per pixel; null without a focal length.</param>
    /// <param name="readNoise">The read noise from the biases; null without two readable ones.</param>
    /// <param name="wcs">The plate solve's WCS as the analysis read it; null without one.</param>
    /// <param name="series">The series analysis.</param>
    private static void LogFigures(AnalyzedFrame[] frames, Cadence cadence, DitherAnalysis dither, SensorPattern pattern, double pixel,
        PixelPitchSource source, double? focal, double? scale, IReadOnlyList<ReadNoise>? readNoise, Wcs? wcs, SeriesAnalysis series)
    {
        Log.Debug("cadence: " + LogText.Timing(cadence));
        Log.Debug("dither: " + LogText.Dither(dither));
        Log.Debug("pattern: " + LogText.Pattern(pattern));
        Log.Debug("pixel size " + LogText.Num(pixel) + " um source=" + source + " focal=" + LogText.Num(focal) + " mm scale="
            + LogText.Num(scale) + " arcsec/px");
        Log.Debug("read noise: " + LogText.Noise(readNoise));
        Log.Debug("wcs: " + LogText.Solve(wcs));
        Log.Debug("series: " + LogText.Series(series));
        for (int i = 0; i < frames.Length; i++)
        {
            Log.Debug("classified frame " + LogText.Int(i + 1) + ": " + LogText.Classified(series.Classes[i], series.Flags[i],
                i < series.MountMoves.Count ? series.MountMoves[i] : null));
        }
    }

    /// <summary>The diagnostic log: the run's lines, each frame's, the figures behind the verdicts (R-071).</summary>
    private static readonly Logger Log = LogManager.GetLogger(nameof(SeriesRun));

    /// <summary>
    /// The read noise from the first two biases, or null without two readable ones.
    /// </summary>
    /// <param name="session">The session whose biases are read.</param>
    /// <param name="cancel">Checked once before the biases are loaded; a cancel throws.</param>
    /// <returns>The read noise per CFA position, or null.</returns>
    private static IReadOnlyList<ReadNoise>? ReadNoise(SessionFolder session, CancellationToken cancel)
    {
        if (session.Biases.Count < 2)
        {
            Log.Debug("read noise: " + LogText.Int(session.Biases.Count) + " bias(es), two are needed; none measured");
            return null;
        }

        try
        {
            cancel.ThrowIfCancellationRequested();
            // Each double mosaic is dropped as soon as its float raster exists (193 MB apiece on
            // 24 MP).
            LoadedFrame a = ImageLoader.Load(session.Biases[0]);
            Raster ra = Raster.FromImg(a.Image, a.Scale?.Black ?? 0);
            a.Image = null;
            LoadedFrame b = ImageLoader.Load(session.Biases[1]);
            Raster rb = Raster.FromImg(b.Image, b.Scale?.Black ?? 0);
            b.Image = null;
            return NoiseAnalyzer.ReadNoiseFromPair(ra, rb, a.Cfa);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Log.Warn(e, "the read noise could not be measured from " + session.Biases[0] + " and " + session.Biases[1] + "; the report has none");
            return null;
        }
    }

    /// <summary>
    /// Which image axis RA runs along, from the plate-solved FITS <see cref="ReadWcs"/> finds;
    /// unknown without one (R-025).
    /// </summary>
    /// <param name="root">The session folder.</param>
    /// <returns>The RA axis, or <see cref="Axis.Unknown"/>.</returns>
    internal static Axis WcsRaAxis(string root) => ReadWcs(root)?.RaAxis ?? Axis.Unknown;

    /// <summary>
    /// The WCS of the first plate-solved FITS at the session root (Siril's stacked result, by
    /// name): the scale and the PC matrix, or the CD matrix when the file carries that form
    /// (then the scale is 1 and the matrix holds it), with the declination of CRVAL2 when the
    /// header has it (R-050). Null without such a file (R-025, R-038).
    /// </summary>
    /// <param name="root">The session folder.</param>
    /// <returns>The WCS of the first solved FITS by name, or null.</returns>
    internal static Wcs? ReadWcs(string root)
    {
        foreach (string f in Directory.GetFiles(root).Where(FitsReader.IsFitsPath).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                IReadOnlyDictionary<string, string> h = FitsReader.LoadHeader(f);
                if (!FitsReader.HStr(h, "CTYPE1", "").StartsWith("RA", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug(f + " skipped as a WCS source: CTYPE1=" + LogText.Str(FitsReader.HStr(h, "CTYPE1", "")) + " is no RA axis");
                    continue;
                }

                bool pc = FitsReader.HDbl(h, "PC1_1", double.NaN) is double p11 && !double.IsNaN(p11) || FitsReader.HDbl(h, "PC1_2", double.NaN) is double p12 && !double.IsNaN(p12);
                Wcs w = pc
                    ? new Wcs(FitsReader.HDbl(h, "CDELT1", 1), FitsReader.HDbl(h, "CDELT2", 1), FitsReader.HDbl(h, "PC1_1", 0), FitsReader.HDbl(h, "PC1_2", 0), FitsReader.HDbl(h, "PC2_1", 0), FitsReader.HDbl(h, "PC2_2", 0))
                    : new Wcs(1, 1, FitsReader.HDbl(h, "CD1_1", 0), FitsReader.HDbl(h, "CD1_2", 0), FitsReader.HDbl(h, "CD2_1", 0), FitsReader.HDbl(h, "CD2_2", 0));
                if (w.Pc11 == 0 && w.Pc12 == 0)
                {
                    Log.Debug(f + " skipped as a WCS source: its first PC or CD row is zero");
                    continue;
                }

                // The file alone: the flip of the lights against the solve is the caller's to set
                // (ADR 0023), and the run's own "wcs:" line has the values with it.
                Log.Debug("wcs read from " + f);
                double crval2 = FitsReader.HDbl(h, "CRVAL2", double.NaN);
                return double.IsNaN(crval2) ? w : w with { DecDeg = crval2 };
            }
            catch (Exception e) when (e is IOException or InvalidDataException)
            {
                Log.Debug(e, f + " could not be read; skipped as a WCS source");
                continue;
            }
        }

        return null;
    }
}
