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

public class StarAnalyzerTests
{
    const float Sky = 0.08f;

    /// <summary>Un cer de 512x512 cu stele pe o grila de 8x8, la 55 px una de alta; jumatate au centrul intre pixeli.</summary>
    static Raster StarField(double fwhm, float amplitude = 0.3f, int count = 64)
    {
        Raster r = SyntheticRasters.Flat(512, 512, Sky);
        int placed = 0;
        for (int j = 0; j < 8 && placed < count; j++)
            for (int i = 0; i < 8 && placed < count; i++, placed++)
            {
                double cx = 40 + i * 55 + ((i + j) % 2 == 0 ? 0 : 0.5);
                r = SyntheticRasters.AddStar(r, cx, 40 + j * 55, fwhm, amplitude);
            }
        return SyntheticRasters.AddNoise(r, 0.5 / 255, 11);
    }

    [Fact]
    public void RecoversTheFwhmOfSyntheticStars()
    {
        var s = StarAnalyzer.Analyze(StarField(3.0));

        Assert.False(s.Insufficient);
        Assert.InRange(s.Count, 55, 70);
        // Interpolarea liniara la jumatate supraestimeaza o gaussiana esantionata cu
        // ~2.5 % (intre 1 si 2 px curba e convexa). E bias-ul metodei din scriptul de
        // acceptanta, pastrat pentru paritate: 3.0 real iese ~3.08.
        Assert.InRange(s.FwhmMedian, 2.95, 3.2);
        Assert.InRange(s.FwhmQ1, 2.8, 3.2);
        Assert.InRange(s.FwhmQ3, 2.9, 3.3);
        Assert.Equal(512 / s.FwhmMedian, s.EffectiveDetailPx(512), 6);
        Assert.False(s.SoftCorners);
    }

    [Fact]
    public void WiderStarsGiveAWiderFwhm()
    {
        var s = StarAnalyzer.Analyze(StarField(5.0));

        Assert.InRange(s.FwhmMedian, 4.9, 5.3);
    }

    [Fact]
    public void SoftCornersAreSeenAgainstTheCentre()
    {
        // Centru (40 % din latura, 154..358): 16 stele de 3.0. Colturi (20 %, 102 px):
        // cate 9 stele de 4.6. Raportul 1.53 trece de 1.4.
        Raster r = SyntheticRasters.Flat(512, 512, Sky);
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                r = SyntheticRasters.AddStar(r, 175 + i * 50, 175 + j * 50, 3.0, 0.3f);
        foreach (int ox in new[] { 0, 410 })
            foreach (int oy in new[] { 0, 410 })
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        r = SyntheticRasters.AddStar(r, ox + 20 + i * 30, oy + 20 + j * 30, 4.6, 0.3f);

        var s = StarAnalyzer.Analyze(r);

        Assert.Equal("centru", s.Regions[0].Name);
        Assert.InRange(s.Regions[0].FwhmMedian, 2.95, 3.2);
        Assert.All(s.Regions.Skip(1), c => Assert.InRange(c.FwhmMedian, 4.5, 4.9));
        Assert.InRange(s.CornerToCenterRatio, 1.42, 1.62);
        Assert.True(s.SoftCorners);
    }

    [Fact]
    public void SaturatedAndFaintStarsAreNotMeasured()
    {
        Raster r = SyntheticRasters.Flat(256, 256, Sky);
        r = SyntheticRasters.AddStar(r, 60, 60, 3.0, 0.95f);    // varf la 1.0: saturat
        r = SyntheticRasters.AddStar(r, 180, 180, 3.0, 0.03f);  // sub amplitudinea minima

        var s = StarAnalyzer.Analyze(r);

        Assert.Equal(0, s.Count);
        Assert.True(s.Insufficient);
        Assert.True(double.IsNaN(s.FwhmMedian));
        Assert.True(double.IsNaN(s.CornerToCenterRatio));
        Assert.False(s.SoftCorners);
    }

    [Fact]
    public void AnEmptySkyIsInsufficientNotAnError()
    {
        var s = StarAnalyzer.Analyze(SyntheticRasters.AddNoise(SyntheticRasters.Flat(200, 200, Sky), 1.0 / 255, 2));

        Assert.True(s.Insufficient);
        Assert.Equal(5, s.Regions.Count);
        Assert.All(s.Regions, reg => Assert.False(reg.Known));
    }

    [Fact]
    public void TheHalfWidthOfATriangleIsExact()
    {
        // Triunghi cu varful 1 la centru si panta 1/3: la jumatate e lat exact 3.0.
        var line = new double[25];
        for (int i = 0; i < 25; i++) line[i] = Math.Max(0, 1 - Math.Abs(i - 12) / 3.0);

        Assert.Equal(3.0, StarAnalyzer.WidthAtHalf(line, 0.5), 3);
    }
}
