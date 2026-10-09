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

/// <summary>A cut-out: where it was taken (a position word the report turns into a caption), the star it is centred on, the stretched grey bytes.</summary>
internal sealed record StarCrop(string Where, StarShape Star, byte[] Grey, int Width, int Height);

/// <summary>
/// Cut-outs of the measured stars of one frame, so the eye can see the trail the elongation
/// figure reports (R-031): the two brightest measured stars near the centre and the brightest
/// in each corner region. The frame is decoded again (a few seconds); the green channel is
/// shown, each crop stretched from its 5th percentile to its own maximum.
/// </summary>
internal static class StarCrops
{
    internal const int Radius = 12;

    internal static IReadOnlyList<StarCrop> Build(string path, FrameMeasurement m)
    {
        ArgumentNullException.ThrowIfNull(m);
        LoadedFrame f = ImageLoader.Load(path);
        AduScale scale = f.Scale ?? FrameRun.AssumedScale(f.Bitpix);
        Raster raw = Raster.FromImg(f.Image, scale.Black);
        f.Image = null;
        Raster g = Raster.InterpolateGreen(raw, f.Cfa);
        return Build(g, m);
    }

    internal static IReadOnlyList<StarCrop> Build(Raster g, FrameMeasurement m)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(m);
        int h = g.H, w = g.W;
        List<StarShape> byFlux = m.Shapes.OrderByDescending(s => s.Flux).ToList();
        StarShape? Pick(Func<StarShape, bool> where) => byFlux.FirstOrDefault(where);
        double cx = w / 2.0, cy = h / 2.0;
        List<(string, StarShape?)> picks =
        [
            ("centru", Pick(s => Math.Abs(s.Y - cy) < h / 5.0 && Math.Abs(s.X - cx) < w / 5.0)),
            ("centru, a doua", byFlux.Where(s => Math.Abs(s.Y - cy) < h / 5.0 && Math.Abs(s.X - cx) < w / 5.0).Skip(1).FirstOrDefault()),
            ("stanga sus", Pick(s => s.Y < h / 4.0 && s.X < w / 4.0)),
            ("dreapta sus", Pick(s => s.Y < h / 4.0 && s.X > 3 * w / 4.0)),
            ("stanga jos", Pick(s => s.Y > 3 * h / 4.0 && s.X < w / 4.0)),
            ("dreapta jos", Pick(s => s.Y > 3 * h / 4.0 && s.X > 3 * w / 4.0)),
        ];

        List<StarCrop> crops = [];
        foreach ((string where, StarShape? s) in picks)
        {
            if (s is null)
            {
                continue;
            }

            int x0 = (int)Math.Round(s.X) - Radius, y0 = (int)Math.Round(s.Y) - Radius;
            int size = 2 * Radius + 1;
            if (x0 < 0 || y0 < 0 || x0 + size > w || y0 + size > h)
            {
                continue;
            }

            float[] v = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    v[y * size + x] = g.D[(y0 + y) * w + x0 + x];
                }
            }

            double lo = Raster.Percentile(v.Select(x => (double)x).ToList(), 5), hi = v.Max();
            byte[] grey = new byte[size * size];
            for (int i = 0; i < v.Length; i++)
            {
                grey[i] = (byte)Math.Clamp((v[i] - lo) / Math.Max(hi - lo, 1e-6) * 255, 0, 255);
            }

            crops.Add(new StarCrop(where, s, grey, size, size));
        }

        return crops;
    }
}
