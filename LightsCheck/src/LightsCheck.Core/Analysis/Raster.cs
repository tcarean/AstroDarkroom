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

/// <summary>
/// A single-precision raster, row-major, and the pure operations the frame analysis needs
/// on it. Single precision because a frame is 24 MP and several rasters live at once; the
/// figures that come out are medians and ratios, nowhere near float's limits.
/// </summary>
internal sealed class Raster
{
    internal int H { get; }
    internal int W { get; }
    internal float[] D { get; }

    internal Raster(int h, int w)
    {
        H = h;
        W = w;
        D = new float[h * w];
    }

    internal float At(int y, int x) => D[y * W + x];

    /// <summary>The mosaic above its black level, as floats; the double raster is not kept.</summary>
    internal static Raster FromImg(Img img, double black)
    {
        ArgumentNullException.ThrowIfNull(img);
        Raster r = new(img.H, img.W);
        double[] d = img.D;
        for (int i = 0; i < d.Length; i++)
        {
            r.D[i] = (float)(d[i] - black);
        }

        return r;
    }

    /// <summary>
    /// The green channel at full resolution: the green positions as they are, the other two
    /// positions as the mean of their four green neighbours (fewer at the edges). Which
    /// positions are green comes from the CFA order; with none, the raster is returned as it
    /// is (a mono or debayered frame).
    /// </summary>
    internal static Raster InterpolateGreen(Raster a, CfaPattern? cfa)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (cfa is null)
        {
            return a;
        }

        bool[] green = new bool[4];
        for (int p = 0; p < 4; p++)
        {
            green[p] = cfa.Colours[p] == 'G';
        }

        int h = a.H, w = a.W;
        Raster g = new(h, w);
        float[] s = a.D, d = g.D;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (green[Mosaic.PositionOf(y, x)])
                {
                    d[i] = s[i];
                    continue;
                }

                float sum = 0;
                int n = 0;
                if (y > 0) { sum += s[i - w]; n++; }
                if (y < h - 1) { sum += s[i + w]; n++; }
                if (x > 0) { sum += s[i - 1]; n++; }
                if (x < w - 1) { sum += s[i + 1]; n++; }
                d[i] = n > 0 ? sum / n : 0;
            }
        }

        return g;
    }

    /// <summary>
    /// The medians of the tiles of <paramref name="tile"/> pixels (partial tiles at the end
    /// dropped), each tile read at every <paramref name="step"/>-th pixel. A raster smaller
    /// than a tile in either direction is one tile: the grid is never empty.
    /// </summary>
    internal static Raster TileMedians(Raster a, int tile, int step)
    {
        ArgumentNullException.ThrowIfNull(a);
        tile = Math.Max(1, Math.Min(tile, Math.Min(a.H, a.W)));
        int th = a.H / tile, tw = a.W / tile;
        Raster t = new(th, tw);
        int per = (tile + step - 1) / step;
        float[] buf = new float[per * per];
        for (int j = 0; j < th; j++)
        {
            for (int i = 0; i < tw; i++)
            {
                int n = 0;
                for (int y = j * tile; y < (j + 1) * tile; y += step)
                {
                    for (int x = i * tile; x < (i + 1) * tile; x += step)
                    {
                        buf[n++] = a.D[y * a.W + x];
                    }
                }

                t.D[j * tw + i] = Median(buf, n);
            }
        }

        return t;
    }

    /// <summary>
    /// The tile grid stretched back to <paramref name="h"/> x <paramref name="w"/> by bilinear
    /// interpolation, the grid's corners mapped onto the raster's corners (scipy's zoom).
    /// </summary>
    internal static Raster BilinearResize(Raster t, int h, int w)
    {
        ArgumentNullException.ThrowIfNull(t);
        Raster o = new(h, w);
        double sy = t.H > 1 ? (t.H - 1) / (double)(h - 1) : 0;
        double sx = t.W > 1 ? (t.W - 1) / (double)(w - 1) : 0;
        int[] x0 = new int[w];
        float[] fx = new float[w];
        for (int x = 0; x < w; x++)
        {
            double u = x * sx;
            int i = Math.Min((int)u, t.W - 1);
            x0[x] = i;
            fx[x] = (float)(u - i);
        }

        for (int y = 0; y < h; y++)
        {
            double v = y * sy;
            int j = Math.Min((int)v, t.H - 1);
            float fy = (float)(v - j);
            int j1 = Math.Min(j + 1, t.H - 1);
            for (int x = 0; x < w; x++)
            {
                int i = x0[x], i1 = Math.Min(i + 1, t.W - 1);
                float a = t.D[j * t.W + i], b = t.D[j * t.W + i1];
                float c = t.D[j1 * t.W + i], d = t.D[j1 * t.W + i1];
                float top = a + (b - a) * fx[x], bottom = c + (d - c) * fx[x];
                o.D[y * w + x] = top + (bottom - top) * fy;
            }
        }

        return o;
    }

    /// <summary>a - b, in place in a.</summary>
    internal static void SubtractInPlace(Raster a, Raster b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        for (int i = 0; i < a.D.Length; i++)
        {
            a.D[i] -= b.D[i];
        }
    }

    /// <summary>
    /// A Gaussian blur of <paramref name="sigma"/> pixels, separable, the kernel truncated at
    /// four sigmas and normalised, the edges reflected (scipy's gaussian_filter defaults).
    /// </summary>
    internal static Raster Gaussian(Raster a, double sigma)
    {
        ArgumentNullException.ThrowIfNull(a);
        int radius = (int)(4 * sigma + 0.5);
        float[] k = new float[2 * radius + 1];
        double sum = 0;
        for (int i = -radius; i <= radius; i++)
        {
            k[i + radius] = (float)Math.Exp(-0.5 * i * i / (sigma * sigma));
            sum += k[i + radius];
        }

        for (int i = 0; i < k.Length; i++)
        {
            k[i] = (float)(k[i] / sum);
        }

        int h = a.H, w = a.W;
        Raster tmp = new(h, w);
        float[] s = a.D, t = tmp.D;
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                float acc = 0;
                for (int i = -radius; i <= radius; i++)
                {
                    acc += k[i + radius] * s[row + Reflect(x + i, w)];
                }

                t[row + x] = acc;
            }
        }

        Raster o = new(h, w);
        float[] d = o.D;
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int i = -radius; i <= radius; i++)
            {
                int src = Reflect(y + i, h) * w;
                float kk = k[i + radius];
                for (int x = 0; x < w; x++)
                {
                    d[row + x] += kk * t[src + x];
                }
            }
        }

        return o;
    }

    /// <summary>scipy's "reflect" border: d c b a | a b c d | d c b a.</summary>
    internal static int Reflect(int i, int n)
    {
        if (n == 1)
        {
            return 0;
        }

        int period = 2 * n;
        i %= period;
        if (i < 0)
        {
            i += period;
        }

        return i < n ? i : period - 1 - i;
    }

    /// <summary>
    /// The robust sigma, 1.4826 x the median absolute deviation, of the central window of
    /// half-side <paramref name="half"/> read at every <paramref name="step"/>-th pixel; the
    /// whole raster when it is smaller than the window.
    /// </summary>
    internal static double CentralMadSigma(Raster a, int half, int step)
    {
        ArgumentNullException.ThrowIfNull(a);
        int y0 = Math.Max(0, a.H / 2 - half), y1 = Math.Min(a.H, a.H / 2 + half);
        int x0 = Math.Max(0, a.W / 2 - half), x1 = Math.Min(a.W, a.W / 2 + half);
        List<float> v = new(((y1 - y0) / step + 1) * ((x1 - x0) / step + 1));
        for (int y = y0; y < y1; y += step)
        {
            for (int x = x0; x < x1; x += step)
            {
                v.Add(a.D[y * a.W + x]);
            }
        }

        float[] arr = v.ToArray();
        float med = Median(arr, arr.Length);
        for (int i = 0; i < arr.Length; i++)
        {
            arr[i] = Math.Abs(arr[i] - med);
        }

        return 1.4826 * Median(arr, arr.Length);
    }

    /// <summary>The median of the first <paramref name="n"/> values, the array reordered.</summary>
    internal static float Median(float[] v, int n)
    {
        if (n == 0)
        {
            return float.NaN;
        }

        Array.Sort(v, 0, n);
        return n % 2 == 1 ? v[n / 2] : (v[n / 2 - 1] + v[n / 2]) / 2;
    }

    /// <summary>numpy's percentile: linear interpolation between the sorted neighbours.</summary>
    internal static double Percentile(IReadOnlyList<double> values, double p)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            return double.NaN;
        }

        double[] s = values.ToArray();
        Array.Sort(s);
        double pos = (s.Length - 1) * p / 100.0;
        int lo = (int)Math.Floor(pos);
        int hi = Math.Min(lo + 1, s.Length - 1);
        return s[lo] + (s[hi] - s[lo]) * (pos - lo);
    }
}
