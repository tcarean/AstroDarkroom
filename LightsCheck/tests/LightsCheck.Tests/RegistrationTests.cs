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

/// <summary>Registration of synthetic star lists under known transforms (R-018).</summary>
public class RegistrationTests
{
    private const int W = 6016, H = 4016;

    private static List<RegistrationStar> Field(int count, int seed)
    {
        Random rng = new(seed);
        List<RegistrationStar> stars = [];
        for (int i = 0; i < count; i++)
        {
            stars.Add(new RegistrationStar(100 + rng.NextDouble() * (W - 200), 100 + rng.NextDouble() * (H - 200), 100000 - i * 100));
        }

        return stars;
    }

    /// <summary>The same field moved by (dx, dy) and rotated by theta about the centre, with jitter and a few stars dropped or added.</summary>
    private static List<RegistrationStar> Moved(List<RegistrationStar> reference, double dx, double dy, double thetaDeg, double jitter, int seed)
    {
        Random rng = new(seed);
        double c = Math.Cos(thetaDeg * Math.PI / 180), s = Math.Sin(thetaDeg * Math.PI / 180);
        List<RegistrationStar> frame = [];
        foreach (RegistrationStar r in reference)
        {
            if (rng.NextDouble() < 0.1)
            {
                continue;   // not detected in this frame
            }

            double x = r.X - W / 2.0, y = r.Y - H / 2.0;
            double u = c * x - s * y + W / 2.0 + dx + (rng.NextDouble() - 0.5) * jitter;
            double v = s * x + c * y + H / 2.0 + dy + (rng.NextDouble() - 0.5) * jitter;
            frame.Add(new RegistrationStar(u, v, r.Flux * (0.9 + 0.2 * rng.NextDouble())));
        }

        for (int i = 0; i < 40; i++)
        {
            frame.Add(new RegistrationStar(rng.NextDouble() * W, rng.NextDouble() * H, 5000));   // extra stars
        }

        return frame.OrderByDescending(f => f.Flux).ToList();
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(-33.4, 47.2, 0.03)]
    [InlineData(155.9, -180.4, 0.14)]
    [InlineData(1200, -900, -0.2)]
    public void R018_RecoversShiftAndRotationWithinATenthOfAPixel(double dx, double dy, double theta)
    {
        List<RegistrationStar> reference = Field(600, 1);
        List<RegistrationStar> frame = Moved(reference, dx, dy, theta, 0.2, 2);

        RegistrationResult r = Registration.Register(reference, frame, W, H);

        Assert.True(r.Ok);
        Assert.True(r.Matches > 400, $"matches {r.Matches}");
        Assert.Equal(dx, r.Dx, 0.1);
        Assert.Equal(dy, r.Dy, 0.1);
        Assert.Equal(theta, r.RotationDeg, 0.002);
        Assert.Equal(1.0, r.Scale, 0.0005);
        Assert.InRange(r.Rms, 0, 0.2);
    }

    [Fact]
    public void R018_TooFewStarsOrAnUnrelatedFieldFails()
    {
        List<RegistrationStar> reference = Field(600, 1);
        Assert.False(Registration.Register(reference, Field(5, 3), W, H).Ok);
        Assert.False(Registration.Register(Field(5, 3), reference, W, H).Ok);
        RegistrationResult unrelated = Registration.Register(reference, Field(600, 9), W, H);
        Assert.False(unrelated.Ok);
    }

    [Fact]
    public void R018_TheSimilarityFitIsExactOnAnExactTransform()
    {
        List<(double, double, double, double)> pairs = [];
        double a = Math.Cos(0.01), b = Math.Sin(0.01);
        for (int i = 0; i < 20; i++)
        {
            double x = i * 37.0 % 500, y = i * 91.0 % 400;
            pairs.Add((x, y, a * x - b * y + 12.5, b * x + a * y - 7.25));
        }

        double[] sol = Registration.FitSimilarity(pairs, out double rms);
        Assert.Equal(a, sol[0], 1e-9);
        Assert.Equal(b, sol[1], 1e-9);
        Assert.Equal(12.5, sol[2], 1e-9);
        Assert.Equal(-7.25, sol[3], 1e-9);
        Assert.Equal(0, rms, 1e-9);
    }
}
