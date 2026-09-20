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

/// <summary>
/// Parsarea argumentelor. Parse nu arunca niciodata: fiecare greseala de tastare trebuie
/// sa iasa ca un Error citibil, nu ca o exceptie cu stack trace.
/// </summary>
public class OptionsTests
{
    [Fact]
    public void TheImageIsTheOnlyPositionalArgument()
    {
        var o = Options.Parse(new[] { "poza.tif" });

        Assert.Null(o.Error);
        Assert.Equal("poza.tif", o.Image);
        Assert.Null(o.Medium);
        Assert.Null(o.Sizes);
    }

    [Fact]
    public void FlagsMayComeBeforeOrAfterTheImage()
    {
        var before = Options.Parse(new[] { "--medium", "canvas", "poza.tif" });
        var after = Options.Parse(new[] { "poza.tif", "--medium", "canvas" });

        Assert.Equal("poza.tif", before.Image);
        Assert.Equal("poza.tif", after.Image);
        Assert.Equal("canvas", before.Medium);
        Assert.Equal("canvas", after.Medium);
    }

    [Fact]
    public void NumbersAreParsedWithTheInvariantCulture()
    {
        var o = Options.Parse(new[] { "poza.tif", "--distance", "1.5", "--target-bg", "0.19", "--dmax", "2.2" });

        Assert.Null(o.Error);
        Assert.Equal(1.5, o.Distance);
        Assert.Equal(0.19, o.TargetBg);
        Assert.Equal(2.2, o.Dmax);
    }

    [Fact]
    public void SizesAreACommaSeparatedListOfWidths()
    {
        var o = Options.Parse(new[] { "poza.tif", "--sizes", "60,70, 80" });

        Assert.Null(o.Error);
        Assert.Equal(new[] { 60.0, 70.0, 80.0 }, o.Sizes);
    }

    [Theory]
    [InlineData("50:35")]
    [InlineData("50x35")]
    public void AspectAcceptsColonAndX(string text)
    {
        var o = Options.Parse(new[] { "poza.tif", "--aspect", text });

        Assert.Null(o.Error);
        Assert.Equal(50.0 / 35.0, o.Aspect!.Value, 9);
    }

    [Theory]
    [InlineData("--medium", "hartie")]
    [InlineData("--dmax", "abc")]
    [InlineData("--dmax", "9")]
    [InlineData("--distance", "-1")]
    [InlineData("--sizes", "a,b")]
    [InlineData("--aspect", "50")]
    [InlineData("--target-bg", "1.5")]
    public void ABadValueIsAnErrorNotAnException(string flag, string value)
    {
        var o = Options.Parse(new[] { "poza.tif", flag, value });

        Assert.NotNull(o.Error);
        Assert.Contains(flag, o.Error);
    }

    [Fact]
    public void AMissingValueIsReported()
    {
        Assert.Contains("lipseste valoarea", Options.Parse(new[] { "poza.tif", "--distance" }).Error);
        Assert.Contains("asteapta o valoare", Options.Parse(new[] { "poza.tif", "--distance", "--dmax", "2" }).Error);
    }

    [Fact]
    public void AnUnknownOptionIsReported()
    {
        Assert.Contains("optiune necunoscuta", Options.Parse(new[] { "poza.tif", "--version" }).Error);
    }

    [Fact]
    public void NoImageOrTwoImagesIsAnError()
    {
        Assert.Contains("lipseste fisierul", Options.Parse(new[] { "--medium", "baryta" }).Error);
        Assert.Contains("un singur fisier", Options.Parse(new[] { "a.tif", "b.tif" }).Error);
    }

    [Fact]
    public void NoArgumentsOrHelpAsksForUsage()
    {
        Assert.True(Options.Parse(Array.Empty<string>()).Help);
        Assert.True(Options.Parse(new[] { "poza.tif", "-h" }).Help);
        Assert.True(Options.Parse(new[] { "--help" }).Help);
    }
}
