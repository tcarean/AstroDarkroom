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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>A star to draw: centre, peak above the background, the two Gaussian sigmas and the angle of the major axis from the x axis, in the image frame (y down).</summary>
internal sealed record SyntheticStar(double X, double Y, double Peak, double SigmaMajor, double SigmaMinor, double AngleDeg)
{
    internal static SyntheticStar Round(double x, double y, double peak, double fwhm) =>
        new(x, y, peak, fwhm / 2.3548, fwhm / 2.3548, 0);

    internal static SyntheticStar Elongated(double x, double y, double peak, double fwhmMinor, double elongation, double angleDeg) =>
        new(x, y, peak, fwhmMinor / 2.3548 * elongation, fwhmMinor / 2.3548, angleDeg);
}

/// <summary>
/// A synthetic light frame with known answers: an RGGB mosaic (or mono) over a flat
/// background per colour, Gaussian stars of known shape, Gaussian noise from a seeded
/// generator, optionally a straight trail and hot pixels. Values are ADU above black, clipped
/// to the scale; the scale is black 0, white as given.
/// </summary>
internal sealed class SyntheticFrame
{
    internal int Width { get; init; } = 1200;
    internal int Height { get; init; } = 800;
    internal double White { get; init; } = 4095;
    internal double BackgroundR { get; init; } = 30;
    internal double BackgroundG { get; init; } = 150;
    internal double BackgroundB { get; init; } = 170;
    internal double NoiseSigma { get; init; } = 12;
    internal int Seed { get; init; } = 7;
    internal bool Mosaic { get; init; } = true;
    internal List<SyntheticStar> Stars { get; } = [];
    internal (double X0, double Y0, double X1, double Y1, double Peak)? TrailLine { get; init; }

    internal static CfaPattern Rggb => CfaPattern.Parse("RGGB", "test")!;

    internal AduScale Scale => new(White, 0, "test");

    /// <summary>A grid of <paramref name="count"/> round stars of the same FWHM, peaks spread between the two given, away from the borders.</summary>
    internal SyntheticFrame WithStarGrid(int count, double fwhm, double peakMin, double peakMax)
    {
        int cols = (int)Math.Ceiling(Math.Sqrt(count * (double)Width / Height));
        int rows = (int)Math.Ceiling(count / (double)cols);
        int n = 0;
        for (int r = 0; r < rows && n < count; r++)
        {
            for (int c = 0; c < cols && n < count; c++, n++)
            {
                double x = 40 + (Width - 80) * (c + 0.5) / cols + 3.3 * ((n * 7) % 5) - 6;
                double y = 40 + (Height - 80) * (r + 0.5) / rows + 2.1 * ((n * 3) % 7) - 6;
                double peak = peakMin + (peakMax - peakMin) * ((n * 37) % 101) / 100.0;
                Stars.Add(SyntheticStar.Round(x, y, peak, fwhm));
            }
        }

        return this;
    }

    /// <summary>The same grid, every star elongated by <paramref name="elongation"/> at <paramref name="angleDeg"/>.</summary>
    internal SyntheticFrame WithElongatedGrid(int count, double fwhmMinor, double elongation, double angleDeg, double peakMin, double peakMax)
    {
        WithStarGrid(count, fwhmMinor, peakMin, peakMax);
        for (int i = 0; i < Stars.Count; i++)
        {
            SyntheticStar s = Stars[i];
            Stars[i] = SyntheticStar.Elongated(s.X, s.Y, s.Peak, fwhmMinor, elongation, angleDeg);
        }

        return this;
    }

    /// <summary>The pixel function, for FitsTestData or for <see cref="Render"/>.</summary>
    internal Func<int, int, double> Pixel()
    {
        double[] field = new double[Width * Height];
        foreach (SyntheticStar s in Stars)
        {
            double ca = Math.Cos(s.AngleDeg * Math.PI / 180), sa = Math.Sin(s.AngleDeg * Math.PI / 180);
            int r = (int)Math.Ceiling(4 * s.SigmaMajor) + 1;
            for (int y = Math.Max(0, (int)s.Y - r); y <= Math.Min(Height - 1, (int)s.Y + r); y++)
            {
                for (int x = Math.Max(0, (int)s.X - r); x <= Math.Min(Width - 1, (int)s.X + r); x++)
                {
                    double dx = x - s.X, dy = y - s.Y;
                    double u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                    field[y * Width + x] += s.Peak * Math.Exp(-0.5 * (u * u / (s.SigmaMajor * s.SigmaMajor) + v * v / (s.SigmaMinor * s.SigmaMinor)));
                }
            }
        }

        if (TrailLine is (double x0, double y0, double x1, double y1, double tp))
        {
            int steps = (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0))) * 2;
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                int x = (int)Math.Round(x0 + (x1 - x0) * t), y = (int)Math.Round(y0 + (y1 - y0) * t);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && xx < Width && yy >= 0 && yy < Height)
                        {
                            field[yy * Width + xx] = Math.Max(field[yy * Width + xx], tp);
                        }
                    }
                }
            }
        }

        Random rng = new(Seed);
        double[] noise = new double[Width * Height];
        for (int i = 0; i < noise.Length; i += 2)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            double m = Math.Sqrt(-2.0 * Math.Log(u1));
            noise[i] = m * Math.Cos(2 * Math.PI * u2) * NoiseSigma;
            if (i + 1 < noise.Length)
            {
                noise[i + 1] = m * Math.Sin(2 * Math.PI * u2) * NoiseSigma;
            }
        }

        return (x, y) =>
        {
            double bg = BackgroundG;
            if (Mosaic)
            {
                int p = (y & 1) * 2 + (x & 1);
                bg = p == 0 ? BackgroundR : p == 3 ? BackgroundB : BackgroundG;
            }

            double v = bg + field[y * Width + x] + noise[y * Width + x];
            return Math.Clamp(Math.Round(v), 0, White);
        };
    }

    /// <summary>The frame as an Img above black, ready for the analyzer.</summary>
    internal Img Render()
    {
        Func<int, int, double> px = Pixel();
        Img img = new(Height, Width);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                img.Set(y, x, px(x, y));
            }
        }

        return img;
    }
}
