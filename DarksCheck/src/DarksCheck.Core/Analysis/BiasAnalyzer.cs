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

using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// One bias frame: its metadata and figures per plane, or why it has none: an error, or skipped by
/// the frame cap (not an error).
/// </summary>
/// <param name="Info">The frame's metadata.</param>
/// <param name="Error">Why it has no figures, in the run's language; null when it decoded.</param>
/// <param name="Skipped">True when the frame cap left it out.</param>
/// <param name="Planes">The four positions' figures in raw ADU (normalisation 1); empty
/// without a mosaic.</param>
internal sealed record BiasFrameFigures(FrameInfo Info, string? Error, bool Skipped, IReadOnlyList<PlaneFigures> Planes);

/// <summary>
/// Whether the noise around black is clipped (R-041): the minimum, the share of pixels below black,
/// and the share below black minus two sigma against the 2.28 % of a Gaussian.
/// </summary>
/// <param name="Min">The smallest value, in raw ADU, the black included.</param>
/// <param name="FractionBelowBlack">The share of pixels under the black, 0..1.</param>
/// <param name="FractionBelowMinusTwoSigma">The share under the black minus two clipped sigmas,
/// 0..1.</param>
internal sealed record ClipCheck(int Min, double FractionBelowBlack, double FractionBelowMinusTwoSigma);

/// <summary>
/// The biases measured (R-041): every frame's figures, the read noise per plane from the
/// pairs of consecutive frames, the mean master's level and noise per plane, the binned
/// master maps, and the clipping check on the first frame's reference plane.
/// </summary>
/// <param name="Width">The mosaic's width, in pixels.</param>
/// <param name="Height">The mosaic's height, in pixels.</param>
/// <param name="Black">The black level, in raw ADU.</param>
/// <param name="White">The white level, in raw ADU.</param>
/// <param name="Frames">Every bias file, in file order.</param>
/// <param name="Used">How many decoded.</param>
/// <param name="PlaneNames">The four positions' names.</param>
/// <param name="ReadNoise">Per position, the median over the consecutive pairs of the clipped
/// sigma of their difference over sqrt(2), in raw ADU; NaN under two frames.</param>
/// <param name="Master">The mean master's level and noise per position (no counts).</param>
/// <param name="MeanMaster">The mean master, row-major, in raw ADU with the black.</param>
/// <param name="MeanMaps">The binned mean master per position, above black.</param>
/// <param name="Clip">The clipping check.</param>
internal sealed record BiasAnalysis(
    int Width,
    int Height,
    double Black,
    double White,
    IReadOnlyList<BiasFrameFigures> Frames,
    int Used,
    IReadOnlyList<string> PlaneNames,
    IReadOnlyList<double> ReadNoise,
    IReadOnlyList<MasterFigures> Master,
    IReadOnlyList<float> MeanMaster,
    IReadOnlyList<BinnedMap> MeanMaps,
    ClipCheck Clip);

/// <summary>
/// The dark current (R-021, R-040): the dark's mean master minus the bias' mean master, binned per
/// plane, with the regions and the spread of the reference plane.
/// </summary>
/// <param name="Maps">The four positions' maps, in raw ADU above the bias.</param>
/// <param name="Median">The reference plane map's median, in raw ADU.</param>
/// <param name="P1">Its 1st percentile, in raw ADU.</param>
/// <param name="P99">Its 99th percentile, in raw ADU.</param>
/// <param name="ReferencePlane">The reference CFA position.</param>
internal sealed record DarkCurrent(IReadOnlyList<BinnedMap> Maps, double Median, double P1, double P99, int ReferencePlane);

/// <summary>Measures the biases (R-041) and the dark current against them.</summary>
internal static class BiasAnalyzer
{
    /// <summary>Measures a stack of biases, in raw ADU.</summary>
    /// <param name="stack">The decoded biases; not null; at least one must have decoded.</param>
    /// <param name="threads">How many threads; below 1 counts as 1.</param>
    /// <returns>The biases' figures.</returns>
    /// <exception cref="InvalidOperationException">No bias decoded.</exception>
    internal static BiasAnalysis Analyze(FrameStack stack, int threads)
    {
        ArgumentNullException.ThrowIfNull(stack);
        IReadOnlyList<StackFrame> decoded = stack.Decoded;
        if (decoded.Count == 0)
        {
            throw new InvalidOperationException(T("no bias could be decoded", "niciun bias nu s-a putut decoda") + FrameStack.FirstErrors(stack));
        }

        int w = stack.Width, h = stack.Height, n = decoded.Count;
        double black = stack.Black;
        ParallelOptions po = new() { MaxDegreeOfParallelism = Math.Max(1, threads) };
        string[] names = Enumerable.Range(0, 4).Select(stack.PlaneName).ToArray();

        BiasFrameFigures[] frames = new BiasFrameFigures[stack.Frames.Count];
        Parallel.For(0, stack.Frames.Count, po, i =>
        {
            StackFrame f = stack.Frames[i];
            if (f.Pixels is null)
            {
                frames[i] = new BiasFrameFigures(f.Info, f.Error, f.Skipped, []);
                return;
            }

            // Raw ADU, not normalised: a bias has no dark current to scale, and its own
            // 1/4000 s exposure would make every level a fraction of an ADU.
            PlaneFigures[] planes = new PlaneFigures[4];
            for (int p = 0; p < 4; p++)
            {
                planes[p] = DarkSeriesAnalyzer.PlaneFiguresOf(names[p], Planes.Extract(f.Pixels, w, h, p), black, stack.White, 1.0);
            }

            frames[i] = new BiasFrameFigures(f.Info, null, false, planes);
        });

        // read noise: consecutive pairs, sigma of the difference over sqrt(2), the median over the
        // pairs
        double[] readNoise = new double[4];
        Parallel.For(0, 4, po, p =>
        {
            List<float> sigmas = [];
            float[] previous = Planes.Extract(decoded[0].Pixels!, w, h, p);
            for (int k = 1; k < n; k++)
            {
                // each plane is extracted once and kept as the previous of the next pair
                float[] current = Planes.Extract(decoded[k].Pixels!, w, h, p);
                float[] diff = new float[current.Length];
                for (int i = 0; i < diff.Length; i++)
                {
                    diff[i] = current[i] - previous[i];
                }

                (double s, _) = Planes.ClippedSigma(diff);
                sigmas.Add((float)(s / Math.Sqrt(2)));
                previous = current;
            }

            readNoise[p] = sigmas.Count == 0 ? double.NaN : Planes.Median([.. sigmas]);
        });

        // the mean master, its figures and maps
        float[] mean = new float[w * h];
        ushort[][] px = decoded.Select(f => f.Pixels!).ToArray();
        Parallel.For(0, h, po, y =>
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                double s = 0;
                foreach (ushort[] f in px)
                {
                    s += f[row + x];
                }

                mean[row + x] = (float)(s / n);
            }
        });

        MasterFigures[] master = new MasterFigures[4];
        BinnedMap[] maps = new BinnedMap[4];
        Parallel.For(0, 4, po, p =>
        {
            float[] plane = Planes.Extract(mean, w, h, p);
            for (int i = 0; i < plane.Length; i++)
            {
                plane[i] -= (float)black;
            }

            (double sigma, _) = Planes.ClippedSigma(plane);
            master[p] = new MasterFigures(names[p], Planes.Median(plane), sigma, [], 0, 0);
            int pw = Planes.PlaneWidth(w, p), ph = Planes.PlaneHeight(h, p);
            float[] bm = Planes.BlockMean(plane, pw, ph, Th.BinPx, out int bw, out int bh);
            maps[p] = new BinnedMap(names[p], bm, bw, bh, Regions.Of(bm, bw, bh));
        });

        // clipping around black: the first frame's reference plane
        float[] g = Planes.Extract(decoded[0].Pixels!, w, h, stack.ReferencePlane);
        (double sig, _) = Planes.ClippedSigma(g);
        int min = int.MaxValue, below = 0, below2 = 0;
        double limit = black - 2 * sig;
        foreach (float v in g)
        {
            if (v < min)
            {
                min = (int)v;
            }

            if (v < black)
            {
                below++;
            }

            if (v < limit)
            {
                below2++;
            }
        }

        ClipCheck clip = new(min, (double)below / g.Length, (double)below2 / g.Length);
        return new BiasAnalysis(w, h, black, stack.White, frames, n, names, readNoise, master, mean, maps, clip);
    }

    /// <summary>
    /// The dark's mean master minus the bias' mean master, both binned: the dark current per block,
    /// in ADU above the bias.
    /// </summary>
    /// <param name="darks">The darks' figures; not null.</param>
    /// <param name="biases">The biases' figures; not null.</param>
    /// <param name="referencePlane">The reference CFA position, 0..3.</param>
    /// <returns>The dark current.</returns>
    /// <exception cref="InvalidOperationException">The two have different sizes.</exception>
    internal static DarkCurrent DarkMinusBias(DarkSeriesAnalysis darks, BiasAnalysis biases, int referencePlane)
    {
        ArgumentNullException.ThrowIfNull(darks);
        ArgumentNullException.ThrowIfNull(biases);
        if (darks.Width != biases.Width || darks.Height != biases.Height)
        {
            throw new InvalidOperationException(T($"the biases are {biases.Width} x {biases.Height} px, the darks {darks.Width} x {darks.Height}", $"bias-urile au {biases.Width} x {biases.Height} px, dark-urile {darks.Width} x {darks.Height}"));
        }

        BinnedMap[] maps = new BinnedMap[4];
        for (int p = 0; p < 4; p++)
        {
            BinnedMap d = darks.MeanMaps[p], b = biases.MeanMaps[p];
            float[] m = new float[d.Values.Count];
            for (int i = 0; i < m.Length; i++)
            {
                m[i] = d.Values[i] - b.Values[i];
            }

            maps[p] = new BinnedMap(d.Name, m, d.W, d.H, Regions.Of(m, d.W, d.H));
        }

        float[] r = [.. maps[referencePlane].Values];
        return new DarkCurrent(maps, Planes.Median(r), Planes.Percentile(r, 1), Planes.Percentile(r, 99), referencePlane);
    }
}
