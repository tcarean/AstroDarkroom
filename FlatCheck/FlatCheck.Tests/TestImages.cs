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

using SixLabors.ImageSharp;

namespace FlatCheck.Tests;

/// <summary>Builds in-memory rasters directly, skipping the FITS round trip.</summary>
internal static class TestImages
{
    /// <summary>
    /// A real JPEG, encoded with ImageSharp's own encoder, wrapped as the undecoded
    /// preview a raw would carry. This is what lets the preview path be tested end to end
    /// without a NEF: the same package decodes it back, and the WPF tests reuse it.
    /// Quality 95 keeps a flat field within 1/255 of its input.
    /// </summary>
    internal static EmbeddedPreview JpegOf(int w, int h, Func<int, int, (byte R, byte G, byte B)> pixel,
                                           int quality = 95)
    {
        using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var (r, g, b) = pixel(x, y);
                img[x, y] = new SixLabors.ImageSharp.PixelFormats.Rgb24(r, g, b);
            }
        using var ms = new MemoryStream();
        img.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality });
        return new EmbeddedPreview(true, ms.ToArray(), w, h, 0, $"previzualizare JPEG {w}x{h} din test");
    }

    /// <summary>An already-decoded RGB preview, the bitmap fallback path.</summary>
    internal static EmbeddedPreview RgbOf(int w, int h, Func<int, int, (byte R, byte G, byte B)> pixel)
    {
        var rgb = new byte[w * h * 3];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var (r, g, b) = pixel(x, y);
                int o = (y * w + x) * 3;
                rgb[o] = r; rgb[o + 1] = g; rgb[o + 2] = b;
            }
        return new EmbeddedPreview(false, rgb, w, h, 0, $"miniatura RGB {w}x{h} din test");
    }
    internal static Img From(int w, int h, Func<int, int, double> f)
    {
        var img = new Img(h, w);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                img.Set(y, x, f(x, y));
        return img;
    }

    /// <summary>
    /// A CFA frame whose brightest channel (the two "green" positions) sits at exactly
    /// <paramref name="bright"/> ADU everywhere, so the central-window median is known
    /// exactly and the verdict bands can be dialled in. The other two positions are
    /// scaled down enough to keep the CFA spread well clear of the 1.02 sanity gate.
    /// </summary>
    internal static Img FlatCfa(int w, int h, double bright, double rGain = 0.62, double bGain = 0.48)
        => From(w, h, (x, y) =>
        {
            if (y % 2 == 0 && x % 2 == 0) return bright * rGain;
            if (y % 2 == 1 && x % 2 == 1) return bright * bGain;
            return bright;
        });

    /// <summary>Background that RISES toward the edges - the over-correction signature.</summary>
    internal static Func<int, int, double> InvertedRadial(int w, int h, double center = 30000, double k = 0.45)
    {
        double cx = (w - 1) / 2.0, cy = (h - 1) / 2.0;
        double rmax = Math.Sqrt(cx * cx + cy * cy);
        return (x, y) =>
        {
            double dx = x - cx, dy = y - cy;
            double rn = Math.Sqrt(dx * dx + dy * dy) / rmax;
            return center * (1.0 + k * rn * rn);
        };
    }
}
