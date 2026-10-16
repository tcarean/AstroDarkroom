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

namespace DarksCheck.Tests;

/// <summary>
/// A synthetic light with known answers: the dark's pedestal, a flat sky, the sky's noise, the
/// sensor's hot pixels at the dark's positions scaled by a factor (the night's temperature),
/// Gaussian stars (clusters a hot-pixel detector must not count) and rogue pixels present in
/// this light only. Same size and cards as <see cref="SyntheticDark"/>.
/// </summary>
internal sealed class SyntheticLight
{
    internal int Width { get; init; } = 256;
    internal int Height { get; init; } = 192;
    internal double Pedestal { get; init; } = 200;
    internal double Sky { get; init; } = 500;
    internal double Noise { get; init; } = 6;
    internal int Seed { get; init; } = 1;
    internal double HotFactor { get; init; } = 1;
    internal IReadOnlyList<(int X, int Y, double Adu)> Hot { get; init; } = [];
    internal IReadOnlyList<(int X, int Y, double Peak, double SigmaPx)> Stars { get; init; } = [];
    internal IReadOnlyList<(int X, int Y, double Adu)> Rogue { get; init; } = [];

    /// <summary>Twenty hot pixels of 120 ADU on a lattice, far from the edges and from each other.</summary>
    internal static readonly IReadOnlyList<(int X, int Y, double Adu)> Lattice =
        [.. Enumerable.Range(0, 20).Select(k => (40 + 36 * (k % 5), 40 + 28 * (k / 5), 120.0))];

    /// <summary>Three stars: a bright one, a faint one and a saturating one.</summary>
    internal static readonly IReadOnlyList<(int X, int Y, double Peak, double SigmaPx)> ThreeStars =
        [(100, 60, 3000, 1.5), (200, 150, 300, 1.2), (60, 150, 70000, 2.0)];

    internal byte[] Build(DateTime start, double exposure = 120, double iso = 400)
    {
        double[] field = new double[Width * Height];
        Random rng = new(Seed);
        for (int i = 0; i < field.Length; i += 2)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            double m = Math.Sqrt(-2.0 * Math.Log(u1));
            field[i] = m * Math.Cos(2 * Math.PI * u2) * Noise;
            if (i + 1 < field.Length)
            {
                field[i + 1] = m * Math.Sin(2 * Math.PI * u2) * Noise;
            }
        }

        foreach ((int x, int y, double adu) in Hot)
        {
            field[y * Width + x] += adu * HotFactor;
        }

        foreach ((int x, int y, double adu) in Rogue)
        {
            field[y * Width + x] += adu;
        }

        foreach ((int sx, int sy, double peak, double sigma) in Stars)
        {
            int r = (int)Math.Ceiling(4 * sigma);
            for (int y = Math.Max(0, sy - r); y <= Math.Min(Height - 1, sy + r); y++)
            {
                for (int x = Math.Max(0, sx - r); x <= Math.Min(Width - 1, sx + r); x++)
                {
                    double d2 = (x - sx) * (x - sx) + (y - sy) * (y - sy);
                    field[y * Width + x] += peak * Math.Exp(-d2 / (2 * sigma * sigma));
                }
            }
        }

        string[] cards = [.. SessionFixture.Cards(start, exposure, iso), .. FitsTestData.BayerCards("RGGB")];
        return FitsTestData.BuildFits(Width, Height, (x, y) => Math.Clamp(Math.Round(Pedestal + Sky + field[y * Width + x]), 0, 65535), bitpix: 16, extraCards: cards);
    }
}
