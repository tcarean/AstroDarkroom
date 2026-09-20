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

namespace AstroPhotoAnalyzer.Wpf.Tests;

public class ParameterFieldsTests
{
    [Fact]
    public void TheDefaultsBuildTheDefaultParameters()
    {
        Parameters d = new();
        Parameters? p = new ParameterFields().TryBuild(out string? error);

        Assert.Null(error);
        Assert.Equal(d.Medium, p!.Medium);
        Assert.Null(p.DmaxOverride);
        Assert.Null(p.ViewingDistanceM);
        Assert.Equal(d.CandidateWidthsCm, p.CandidateWidthsCm);
        Assert.Null(p.TargetAspect);
        Assert.Equal(d.TargetBackground, p.TargetBackground);
        Assert.Equal(d.PassepartoutCm, p.PassepartoutCm);
        Assert.Equal(d.FrameCm, p.FrameCm);
    }

    [Fact]
    public void EveryFieldIsHonoured()
    {
        var f = new ParameterFields { Medium = "canvas", Dmax = "1.7", Distance = "2", Sizes = "60, 70;80", Aspect = "50x35", TargetBg = "0.2", Passepartout = "6", Frame = "4" };
        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(error);
        Assert.Equal(PrintMedium.Canvas, p!.Medium);
        Assert.Equal(1.7, p.Dmax);
        Assert.Equal(2, p.ViewingDistanceM);
        Assert.Equal(new double[] { 60, 70, 80 }, p.CandidateWidthsCm);
        Assert.Equal(50.0 / 35.0, p.TargetAspect!.Value, 9);
        Assert.Equal(0.2, p.TargetBackground);
        Assert.Equal(6, p.PassepartoutCm);
        Assert.Equal(4, p.FrameCm);
    }

    [Theory]
    [InlineData("Dmax", "1,5", "punctul")]
    [InlineData("Dmax", "9", "intervalului")]
    [InlineData("Distance", "abc", "numar")]
    [InlineData("Sizes", "", "cel putin o latime")]
    [InlineData("Sizes", "60,x", "latime")]
    [InlineData("Aspect", "50", "latime:inaltime")]
    [InlineData("TargetBg", "1.5", "intervalului")]
    public void ABadFieldGivesAMessageNotAnException(string field, string value, string fragment)
    {
        var f = new ParameterFields();
        typeof(ParameterFields).GetProperty(field)!.SetValue(f, value);

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(p);
        Assert.NotNull(error);
        Assert.Contains(fragment, error);
    }

    [Fact]
    public void TheMediaListIsTheCoreOne()
    {
        Assert.Equal(new[] { "baryta", "canvas", "acrilic" }, new ParameterFields().Media);
    }
}
