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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// Rasters built from formulas, with the answer known in advance. No real image enters
/// the repo: every scene is generated, so every test knows exactly what the analyzer
/// must measure.
/// </summary>
internal static class SyntheticRasters
{
    internal static Raster Flat(int h, int w, float value, int planes = 3)
    {
        var r = new Raster(h, w, planes);
        for (int p = 0; p < planes; p++) Array.Fill(r.Planes[p], value);
        return r;
    }

    internal static Raster Flat(int h, int w, float red, float green, float blue)
    {
        var r = new Raster(h, w, 3);
        Array.Fill(r.Planes[0], red);
        Array.Fill(r.Planes[1], green);
        Array.Fill(r.Planes[2], blue);
        return r;
    }

    /// <summary>
    /// A linear horizontal ramp from <paramref name="from"/> (left) to <paramref name="to"/>
    /// (right), on every plane.
    /// </summary>
    internal static Raster Ramp(int h, int w, float from, float to, int planes = 3)
    {
        var r = new Raster(h, w, planes);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = from + (to - from) * x / Math.Max(1, w - 1);
                for (int p = 0; p < planes; p++) r.Set(p, y, x, v);
            }
        return r;
    }

    /// <summary>An arbitrary raster, from a formula, on every plane.</summary>
    internal static Raster From(int h, int w, int planes, Func<int, int, int, float> value)
    {
        var r = new Raster(h, w, planes);
        for (int p = 0; p < planes; p++)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    r.Set(p, y, x, value(x, y, p));
        return r;
    }

    /// <summary>
    /// Additive Gaussian noise, the same realisation on every plane? No - independent on
    /// each, as on a sensor.
    /// </summary>
    internal static Raster AddNoise(Raster src, double sigma, int seed)
    {
        var rng = new SeededRandom(seed);
        var r = new Raster(src.H, src.W, src.Planes.Length) { Source = src.Source };
        for (int p = 0; p < src.Planes.Length; p++)
            for (int i = 0; i < src.Planes[p].Length; i++)
                r.Planes[p][i] = (float)Math.Clamp(src.Planes[p][i] + sigma * rng.NextGaussian(), 0, 1);
        return r;
    }

    /// <summary>
    /// A Gaussian star, added over what is already there, on every plane. FWHM is in
    /// pixels; sigma = FWHM / 2.3548. The amplitude is the peak over the background.
    /// </summary>
    internal static Raster AddStar(Raster src, double cx, double cy, double fwhm, float amplitude)
    {
        double sigma = fwhm / 2.354820045;
        var r = new Raster(src.H, src.W, src.Planes.Length) { Source = src.Source };
        for (int p = 0; p < src.Planes.Length; p++) Array.Copy(src.Planes[p], r.Planes[p], src.Planes[p].Length);
        int rad = (int)Math.Ceiling(4 * sigma) + 1;
        for (int y = Math.Max(0, (int)cy - rad); y <= Math.Min(src.H - 1, (int)cy + rad); y++)
            for (int x = Math.Max(0, (int)cx - rad); x <= Math.Min(src.W - 1, (int)cx + rad); x++)
            {
                double d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                float g = (float)(amplitude * Math.Exp(-d2 / (2 * sigma * sigma)));
                for (int p = 0; p < r.Planes.Length; p++)
                    r.Set(p, y, x, Math.Min(1f, r.At(p, y, x) + g));
            }
        return r;
    }

    /// <summary>
    /// An 8-bit disguised as 16: every value rounded to the 256 levels 0..255 and scaled by 257.
    /// </summary>
    internal static Raster Upscaled8(Raster src)
    {
        var r = new Raster(src.H, src.W, src.Planes.Length) { Source = src.Source };
        for (int p = 0; p < src.Planes.Length; p++)
            for (int i = 0; i < src.Planes[p].Length; i++)
                r.Planes[p][i] = RasterOps.Level8(src.Planes[p][i]) * 257 / 65535f;
        return r;
    }
}
