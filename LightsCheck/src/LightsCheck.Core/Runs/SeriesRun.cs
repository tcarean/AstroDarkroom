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
internal sealed record SeriesOptions(double PixelSizeUm = SeriesOptions.D3300PixelUm, int Threads = 0)
{
    /// <summary>The Nikon D3300's pixel pitch: 23.5 mm over 6000 pixels.</summary>
    internal const double D3300PixelUm = 3.92;
}

/// <summary>
/// The whole series measured: the session, every frame, the cadence, the dithering, the
/// series analysis, and the pixel scale in arcseconds per pixel when the focal length is known.
/// </summary>
internal sealed record SeriesResult(
    SessionFolder Session,
    IReadOnlyList<AnalyzedFrame> Frames,
    Cadence Cadence,
    DitherAnalysis Dither,
    SeriesAnalysis Series,
    double PixelSizeUm,
    string PixelSizeSource,
    double? ArcsecPerPixel,
    int ReferenceIndex)
{
    internal bool HasErrors => Frames.Any(f => f.Error is not null);
}

/// <summary>
/// Chains the whole analysis (R-026): opens the session, measures every light in parallel,
/// registers each to the reference (the first measured frame), reads the read noise from
/// the first two biases, and runs the cadence, dithering and series analyzers. Nothing is
/// written here; SeriesOutputs does that on request.
/// </summary>
internal static class SeriesRun
{
    internal static SeriesResult Run(string folder, SeriesOptions options, IProgress<(int Done, int Total)>? progress = null, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        SessionFolder session = SessionFolder.Open(folder);
        int n = session.Lights.Count;
        AnalyzedFrame[] frames = new AnalyzedFrame[n];
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

        IReadOnlyList<ReadNoise>? readNoise = ReadNoise(session, cancel);
        Axis wcs = WcsRaAxis(session.Root);
        SeriesAnalysis series = SeriesAnalyzer.Analyze(frames, cadence, dither, readNoise, wcs);

        // The pixel scale: the file's pixel size when it says it, else the option's.
        double? filePixel = infos.Select(f => f.PixelSizeUm).FirstOrDefault(p => p is not null);
        double pixel = filePixel ?? options.PixelSizeUm;
        string pixelSource = filePixel is not null ? "din fisier (XPIXSZ)" : "introdus";
        double? focal = infos.Select(f => f.FocalMm).FirstOrDefault(f => f is not null);
        double? scale = focal is double fm && fm > 0 ? 206.265 * pixel / fm : null;

        return new SeriesResult(session, frames, cadence, dither, series, pixel, pixelSource, scale, refIndex + 1);
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

    /// <summary>
    /// Which image axis RA runs along, from a plate-solved FITS at the session root (Siril's
    /// stacked result): the PC matrix row of RA (world axis 1) is dominated by the pixel axis
    /// RA varies along. Unknown without such a file.
    /// </summary>
    internal static Axis WcsRaAxis(string root)
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

                double pc11 = FitsReader.HDbl(h, "PC1_1", FitsReader.HDbl(h, "CD1_1", 0));
                double pc12 = FitsReader.HDbl(h, "PC1_2", FitsReader.HDbl(h, "CD1_2", 0));
                if (pc11 == 0 && pc12 == 0)
                {
                    continue;
                }

                return Math.Abs(pc12) > Math.Abs(pc11) ? Axis.Y : Axis.X;
            }
            catch (Exception e) when (e is IOException or InvalidDataException)
            {
                continue;
            }
        }

        return Axis.Unknown;
    }
}
