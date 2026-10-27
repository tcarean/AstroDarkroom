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

using Xunit.Abstractions;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// The star rule on the real frames of testdata/large, with and without stars (ADR 0054): a
/// starless frame keeps under StarMinCount, a frame with stars keeps them, and on the aligned
/// Rosetta pair the stars kept on the frame with stars sit where the two frames differ. Not in
/// the gate: trait Category=Validation, seconds per frame; a frame absent on this machine is
/// reported and skipped (the pattern of ValidationTests, xUnit 2 having no dynamic skip).
/// </summary>
[Trait("Category", "Validation")]
public class StarPairTests
{
    readonly ITestOutputHelper _out;

    public StarPairTests(ITestOutputHelper output) => _out = output;

    /// <summary>The stars of a large frame, or null with a message when the frame is absent.</summary>
    StarResult? Stars(string name)
    {
        string? path = TestData.Large(name);
        if (path == null)
        {
            _out.WriteLine($"NOT MEASURED: {name} is absent under {TestData.LargeDir()}");
            return null;
        }
        StarResult s = StarAnalyzer.Analyze(ImageLoader.Load(path));
        _out.WriteLine($"{name}: {s.Count} stars, FWHM median {s.FwhmMedian:F3}");
        return s;
    }

    [Theory]
    [InlineData("starless_rbf0.5_stretch0.17.fit")]
    [InlineData("starless_rbf0.5_stretch0.1.fit")]
    [InlineData("Rosetta_starless_no_WCS.fit")]
    [InlineData("Veil_Starless.fit")]
    [InlineData("Andromeda_starless_WithWCS.fit")]
    [InlineData("Orion_with_WCS.fit")]
    public void R070_AStarlessFrameHasTooFewStars(string name)
    {
        StarResult? s = Stars(name);
        if (s == null) return;

        Assert.True(s.Insufficient, $"{name}: {s.Count} bumps passed as stars");
    }

    [Theory]
    [InlineData("Rosetta_withStars_no_WCS.fit", 500)]
    [InlineData("Andromeda_WithStars_andWCS.fit", 2500)]
    [InlineData("Orion_WithStars_and_WCS.fit", 1000)]
    [InlineData("de_printat.png", 4000)]
    public void R070_AFrameWithStarsKeepsThem(string name, int atLeast)
    {
        StarResult? s = Stars(name);
        if (s == null) return;

        Assert.False(s.Insufficient);
        Assert.True(s.Count >= atLeast, $"{name}: {s.Count} stars, under {atLeast}");
    }

    /// <summary>
    /// The Rosetta pair is pixel-aligned (measured 2026-10-27: the best shift is 0,0 and 96.9 %
    /// of the pixels differ by under 0.005): a star kept on the frame with stars is a star when
    /// the frame without them is lower at its peak by at least half of StarMinAmplitude16, half
    /// the smallest amplitude a kept star can have (0.023); grain is where the two frames agree.
    /// </summary>
    [Fact]
    public void R070_OnTheRosettaPairTheKeptStarsAreInTheDifference()
    {
        string? with = TestData.Large("Rosetta_withStars_no_WCS.fit");
        string? without = TestData.Large("Rosetta_starless_no_WCS.fit");
        if (with == null || without == null)
        {
            _out.WriteLine($"NOT MEASURED: the Rosetta pair is absent under {TestData.LargeDir()}");
            return;
        }

        Raster a = ImageLoader.Load(with), b = ImageLoader.Load(without);
        Assert.Equal((a.W, a.H), (b.W, b.H));
        StarResult s = StarAnalyzer.Analyze(a);
        float atLeast = (float)(Thresholds.StarMinAmplitude16 / 65535 / 2);
        int inDifference = s.Stars.Count(st => a.Reference[st.Y * a.W + st.X] - b.Reference[st.Y * b.W + st.X] >= atLeast);
        _out.WriteLine($"Rosetta with stars: {s.Count} kept, {inDifference} in the difference image");

        Assert.False(s.Insufficient);
        Assert.True(inDifference >= 0.99 * s.Count, $"{s.Count - inDifference} of {s.Count} kept stars are not in the difference image");
    }
}
