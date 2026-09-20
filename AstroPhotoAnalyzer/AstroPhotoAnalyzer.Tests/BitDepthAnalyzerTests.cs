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

public class BitDepthAnalyzerTests
{
    static Raster Sixteen(int h = 64, int w = 64)
    {
        var rng = new SeededRandom(3);
        var r = SyntheticRasters.From(h, w, 3, (x, y, p) => rng.Next(65536) / 65535f);
        r.Source = new SourceInfo { BitDepth = 16, IsFloat = false, PlanesInFile = 3 };
        return r;
    }

    [Fact]
    public void GenuineSixteenBitDataHasManyLevelsAndFewCoincidences()
    {
        var res = BitDepthAnalyzer.Analyze(Sixteen());

        Assert.True(res.Applicable);
        Assert.All(res.DistinctLevels, n => Assert.True(n > 3500, "niveluri distincte: " + n));
        // Coincidenta v == hi*257 se intampla la 1 din 256 pe date uniforme.
        Assert.InRange(res.UpscaledFraction, 0.001, 0.01);
        Assert.False(res.IsUpscaled);
    }

    [Fact]
    public void AnEightBitUpscaleIsCaught()
    {
        Raster r = SyntheticRasters.Upscaled8(Sixteen());
        r.Source = new SourceInfo { BitDepth = 16, IsFloat = false, PlanesInFile = 3 };

        var res = BitDepthAnalyzer.Analyze(r);

        Assert.Equal(1.0, res.UpscaledFraction);
        Assert.True(res.IsUpscaled);
        Assert.All(res.DistinctLevels, n => Assert.True(n <= 256));
    }

    [Fact]
    public void NotApplicableOnEightBitSourcesFloatFitsOrNoSource()
    {
        Raster eight = Sixteen(); eight.Source = new SourceInfo { BitDepth = 8, IsFloat = false };
        Raster flt = Sixteen(); flt.Source = new SourceInfo { BitDepth = 32, IsFloat = true };
        Raster none = Sixteen(); none.Source = null;

        Assert.False(BitDepthAnalyzer.Analyze(eight).Applicable);
        Assert.False(BitDepthAnalyzer.Analyze(flt).Applicable);
        Assert.False(BitDepthAnalyzer.Analyze(none).Applicable);
        Assert.False(BitDepthAnalyzer.Analyze(eight).IsUpscaled);
    }
}
