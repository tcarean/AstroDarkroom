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

/// <summary>What a run of the whole series takes from the user: the pixel size when the files do not say it, the parallelism.</summary>
internal sealed record SeriesOptions(double PixelSizeUm = SeriesOptions.D3300PixelUm, int Threads = 0, ArrowKeys? Arrows = null)
{
    /// <summary>The Nikon D3300's pixel pitch: 23.5 mm over 6000 pixels.</summary>
    internal const double D3300PixelUm = 3.92;
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
internal sealed record ArrowKeys(Arrow East, Arrow North)
{
    internal static readonly ArrowKeys Default = new(Arrow.Right, Arrow.Up);

    internal Arrow West => East == Arrow.Left ? Arrow.Right : Arrow.Left;

    internal Arrow South => North == Arrow.Up ? Arrow.Down : Arrow.Up;

    /// <summary>The symbol the reports print for a key.</summary>
    internal static string Symbol(Arrow a) => a switch
    {
        Arrow.Left => "←",
        Arrow.Right => "→",
        Arrow.Up => "↑",
        _ => "↓",
    };

    /// <summary>The key a symbol stands for, null for anything else.</summary>
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
    internal static SeriesResult Run(string folder, SeriesOptions options, IProgress<(int Done, int Total)>? progress = null, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        SessionFolder session = SessionFolder.Open(folder);
        int n = session.Lights.Count;
        AnalyzedFrame[] frames = new AnalyzedFrame[n];
        PatternCrop?[] crops = new PatternCrop?[n];
        int done = 0;
        int threads = options.Threads > 0 ? options.Threads : Math.Max(1, Environment.ProcessorCount / 2);
        ParallelOptions po = new() { MaxDegreeOfParallelism = threads, CancellationToken = cancel };
        Parallel.For(0, n, po, i =>
        {
            string path = session.Lights[i];
            try
            {
                MeasuredFrame m = FrameRun.Measure(i + 1, path);
                frames[i] = new AnalyzedFrame(m.Info, m.Measurement, null, null);
                crops[i] = m.Crop;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                frames[i] = new AnalyzedFrame(FrameInfo.Read(i + 1, path), null, null, e.Message);
            }

            progress?.Report((Interlocked.Increment(ref done), n));
        });

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

        List<FrameInfo> infos = frames.Select(f => f.Info).ToList();
        Cadence cadence = CadenceAnalyzer.Analyze(infos);
        double?[] x = frames.Select(f => f.Registration is { Ok: true } r ? r.Dx : (double?)null).ToArray();
        double?[] y = frames.Select(f => f.Registration is { Ok: true } r ? r.Dy : (double?)null).ToArray();
        DitherAnalysis dither = DitherAnalyzer.Analyze(x, y, cadence.PauseSeconds);
        // The crops (1 MB apiece) are dropped once the pattern is read.
        SensorPattern pattern = PatternAnalyzer.Analyze(crops, frames.Select(f => f.Measurement?.Channels).ToList(), dither);
        Array.Clear(crops);

        // The pixel scale: the file's pixel size when it says it, else the option's.
        double? filePixel = infos.Select(f => f.PixelSizeUm).FirstOrDefault(p => p is not null);
        double pixel = filePixel ?? options.PixelSizeUm;
        string pixelSource = filePixel is not null ? "din fisier (XPIXSZ)" : "introdus";
        double? focal = infos.Select(f => f.FocalMm).FirstOrDefault(f => f is not null);
        double? scale = focal is double fm && fm > 0 ? 206.265 * pixel / fm : null;

        IReadOnlyList<ReadNoise>? readNoise = ReadNoise(session, cancel);
        Wcs? wcs = ReadWcs(session.Root);
        SeriesAnalysis series = SeriesAnalyzer.Analyze(frames, cadence, dither, readNoise, wcs, scale);

        return new SeriesResult(session, frames, cadence, dither, series, pattern, pixel, pixelSource, scale, refIndex + 1, options.Arrows ?? ArrowKeys.Default);
    }

    /// <summary>The read noise from the first two biases, or null without two readable ones.</summary>
    private static IReadOnlyList<ReadNoise>? ReadNoise(SessionFolder session, CancellationToken cancel)
    {
        if (session.Biases.Count < 2)
        {
            return null;
        }

        try
        {
            cancel.ThrowIfCancellationRequested();
            // Each double mosaic is dropped as soon as its float raster exists (193 MB apiece on 24 MP).
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
            return null;
        }
    }

    /// <summary>Which image axis RA runs along, from the plate-solved FITS <see cref="ReadWcs"/> finds; unknown without one (R-025).</summary>
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
                    continue;
                }

                bool pc = FitsReader.HDbl(h, "PC1_1", double.NaN) is double p11 && !double.IsNaN(p11) || FitsReader.HDbl(h, "PC1_2", double.NaN) is double p12 && !double.IsNaN(p12);
                Wcs w = pc
                    ? new Wcs(FitsReader.HDbl(h, "CDELT1", 1), FitsReader.HDbl(h, "CDELT2", 1), FitsReader.HDbl(h, "PC1_1", 0), FitsReader.HDbl(h, "PC1_2", 0), FitsReader.HDbl(h, "PC2_1", 0), FitsReader.HDbl(h, "PC2_2", 0))
                    : new Wcs(1, 1, FitsReader.HDbl(h, "CD1_1", 0), FitsReader.HDbl(h, "CD1_2", 0), FitsReader.HDbl(h, "CD2_1", 0), FitsReader.HDbl(h, "CD2_2", 0));
                if (w.Pc11 == 0 && w.Pc12 == 0)
                {
                    continue;
                }

                double crval2 = FitsReader.HDbl(h, "CRVAL2", double.NaN);
                return double.IsNaN(crval2) ? w : w with { DecDeg = crval2 };
            }
            catch (Exception e) when (e is IOException or InvalidDataException)
            {
                continue;
            }
        }

        return null;
    }
}
