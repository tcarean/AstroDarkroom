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
/// A synthetic dark frame with known answers: a pedestal (the camera's black, which a FITS
/// does not declare), Gaussian noise from a seeded generator, hot pixels of known amplitude
/// scaled by a factor (the sensor's temperature), and any extra field per pixel (an edge, a
/// banding). 16-bit FITS with the cards Siril writes and BAYERPAT RGGB.
/// </summary>
internal sealed class SyntheticDark
{
    internal int Width { get; init; } = 256;
    internal int Height { get; init; } = 192;
    internal double Pedestal { get; init; } = 200;
    internal double Noise { get; init; } = 3;
    internal int Seed { get; init; } = 1;
    internal double HotFactor { get; init; } = 1;
    internal IReadOnlyList<(int X, int Y, double Adu)> Hot { get; init; } = [];
    internal Func<int, int, double>? Extra { get; init; }

    /// <summary>The INSTRUME card; null leaves it out, as a FITS that does not name its camera.</summary>
    internal string? Camera { get; init; } = "NIKON D3300";

    /// <summary>Five hot pixels, one per level of <c>Th.HotLevelsAdu</c>: 15, 30, 60, 150 and 600 ADU above the pedestal.</summary>
    internal static readonly IReadOnlyList<(int X, int Y, double Adu)> OnePerLevel = [(10, 10, 15), (21, 20, 30), (30, 31, 60), (41, 41, 150), (50, 50, 600)];

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

        string[] cards = [.. SessionFixture.Cards(start, exposure, iso, Camera), .. FitsTestData.BayerCards("RGGB")];
        return FitsTestData.BuildFits(Width, Height, (x, y) => Math.Clamp(Math.Round(Pedestal + field[y * Width + x] + (Extra?.Invoke(x, y) ?? 0)), 0, 65535), bitpix: 16, extraCards: cards);
    }
}
