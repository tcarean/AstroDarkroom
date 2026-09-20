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

public class BackgroundAnalyzerTests
{
    const float Sky = 0.08f;

    [Fact]
    public void ANeutralSkyHasNoCastAndNoFlaggedRegion()
    {
        var b = BackgroundAnalyzer.Analyze(SyntheticRasters.Flat(120, 120, Sky));

        Assert.True(b.ColorApplicable);
        Assert.All(b.Levels, l => Assert.Equal(Sky * 255, l, 3));
        Assert.Equal(0, b.GreenExcess, 6);
        Assert.Equal(0, b.RedMinusBlue, 6);
        Assert.False(b.HasCast);
        Assert.Equal(8, b.Regions.Count);
        Assert.Empty(b.FlaggedRegions);
    }

    [Fact]
    public void AGreenTintOfTwoLevelsIsMeasuredAsSuch()
    {
        var r = SyntheticRasters.Flat(120, 120, Sky, Sky + 2f / 255, Sky);
        var b = BackgroundAnalyzer.Analyze(r);

        Assert.Equal(2.0, b.GreenExcess, 3);
        Assert.Equal(0, b.RedMinusBlue, 3);
        Assert.True(b.HasCast);
    }

    [Fact]
    public void AHalfLevelTintIsBelowTheWarning()
    {
        var r = SyntheticRasters.Flat(120, 120, Sky, Sky + 0.5f / 255, Sky);

        Assert.False(BackgroundAnalyzer.Analyze(r).HasCast);
    }

    [Fact]
    public void AWarmCornerIsFlaggedByItsChannelsNotItsBrightness()
    {
        // Coltul dreapta-jos (ultimele 15 % randuri, 20 % coloane): R +10, B -8. Luminanta
        // medie abia se misca; canalele, da. E cazul imaginii de acceptanta.
        var r = SyntheticRasters.From(200, 200, 3, (x, y, p) =>
        {
            bool corner = y >= 170 && x >= 160;
            if (!corner) return Sky;
            return p == 0 ? Sky + 10f / 255 : p == 2 ? Sky - 8f / 255 : Sky;
        });
        var b = BackgroundAnalyzer.Analyze(r);

        // Fundalul nu e contaminat: banda de jos e exclusa din el.
        Assert.All(b.Levels, l => Assert.Equal(Sky * 255, l, 2));
        Assert.False(b.HasCast);

        var flagged = Assert.Single(b.FlaggedRegions);
        Assert.Equal("colt dreapta-jos", flagged.Name);
        Assert.Equal(10, flagged.Deltas[0], 1);
        Assert.Equal(0, flagged.Deltas[1], 1);
        Assert.Equal(-8, flagged.Deltas[2], 1);
        Assert.Equal(10, flagged.MaxAbsDelta, 1);
        Assert.True(flagged.Coloured);
    }

    /// <summary>
    /// Coltul in care intra Calea Lactee (imaginea de referinta: +11 / +10 / +16) e o
    /// abatere NEUTRA - canalele urca impreuna. Se semnaleaza, dar nu ca gradient.
    /// </summary>
    [Fact]
    public void ANeutralBrighteningIsFlaggedButNotColoured()
    {
        var r = SyntheticRasters.From(200, 200, 3, (x, y, p) =>
            y < 30 && x < 40 ? Sky + (p == 0 ? 11f : p == 1 ? 10f : 16f) / 255 : Sky);
        var b = BackgroundAnalyzer.Analyze(r);

        var flagged = Assert.Single(b.FlaggedRegions);
        Assert.Equal("colt stanga-sus", flagged.Name);
        Assert.False(flagged.Coloured);
    }

    [Fact]
    public void ABrightEdgeIsFlaggedToo()
    {
        var r = SyntheticRasters.From(200, 200, 3, (x, y, p) => y < 30 ? Sky + 5f / 255 : Sky);
        var b = BackgroundAnalyzer.Analyze(r);

        var names = b.FlaggedRegions.Select(x => x.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "colt dreapta-sus", "colt stanga-sus", "latura de sus" }, names);
        Assert.All(b.FlaggedRegions, x => Assert.Equal(5, x.MaxAbsDelta, 1));
        Assert.All(b.FlaggedRegions, x => Assert.False(x.Coloured));
    }

    [Fact]
    public void MonoHasLevelsButNoCastMetrics()
    {
        var b = BackgroundAnalyzer.Analyze(SyntheticRasters.Flat(120, 120, Sky, planes: 1));

        Assert.False(b.ColorApplicable);
        Assert.False(b.HasCast);
        Assert.Single(b.Levels);
        Assert.Equal(8, b.Regions.Count);
    }

    [Fact]
    public void TheBackgroundIsTheDarkestQuarterNotTheAverage()
    {
        // Un nucleu luminos pe jumatate din imagine nu trebuie sa intre in "fundal".
        var r = SyntheticRasters.From(120, 120, 3, (x, y, p) => x < 60 ? Sky : 0.6f);
        var b = BackgroundAnalyzer.Analyze(r);

        Assert.All(b.Levels, l => Assert.Equal(Sky * 255, l, 2));
    }
}
