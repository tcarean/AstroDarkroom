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

public class ParametersTests
{
    // Enum-ul e internal, deci testele publice il primesc pe nume, nu ca tip.
    [Theory]
    [InlineData("baryta", 2.0)]
    [InlineData("canvas", 1.5)]
    [InlineData("acrilic", 2.2)]
    public void EachMediumHasItsDmax(string medium, double dmax)
    {
        Assert.Equal(dmax, new Parameters { Medium = Parameters.MediumFromName(medium) }.Dmax);
    }

    [Fact]
    public void AnExplicitDmaxBeatsTheMedium()
    {
        Assert.Equal(1.8, new Parameters { Medium = PrintMedium.Baryta, DmaxOverride = 1.8 }.Dmax);
    }

    [Fact]
    public void TheDefaultsAreTheOnesFromTheConversation()
    {
        var p = new Parameters();

        Assert.Equal(PrintMedium.Baryta, p.Medium);
        Assert.Equal(new double[] { 50, 60, 70, 80, 90, 100 }, p.CandidateWidthsCm);
        Assert.Null(p.ViewingDistanceM);
        Assert.Equal(7, p.PassepartoutCm);
        Assert.Equal(3, p.FrameCm);
        Assert.Null(p.TargetAspect);
        Assert.Equal(0.19, p.TargetBackground);
        Assert.Equal(0, p.Seed);
    }

    [Theory]
    [InlineData("baryta", "baryta")]
    [InlineData("Canvas", "canvas")]
    [InlineData("ACRILIC", "acrilic")]
    public void MediumNamesRoundTripCaseInsensitively(string typed, string canonical)
    {
        Assert.Equal(canonical, Parameters.NameOf(Parameters.MediumFromName(typed)));
    }

    [Fact]
    public void TheThreeMediaAreDistinct()
    {
        Assert.NotEqual(Parameters.MediumFromName("baryta"), Parameters.MediumFromName("canvas"));
        Assert.NotEqual(Parameters.MediumFromName("canvas"), Parameters.MediumFromName("acrilic"));
    }

    [Fact]
    public void AnUnknownMediumNameIsAnArgumentError()
    {
        Assert.Throws<ArgumentException>(() => Parameters.MediumFromName("hartie"));
    }
}
