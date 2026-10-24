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
/// Parsing the arguments. Parse never throws: every typing mistake must come out as a
/// readable Error, not as an exception with a stack trace.
/// </summary>
public class OptionsTests
{
    [Fact]
    public void R003_TheImageIsTheOnlyPositionalArgument()
    {
        var o = Options.Parse(new[] { "poza.tif" });

        Assert.Null(o.Error);
        Assert.Equal("poza.tif", o.Image);
        Assert.Null(o.Medium);
        Assert.Null(o.Sizes);
    }

    [Fact]
    public void R003_FlagsMayComeBeforeOrAfterTheImage()
    {
        var before = Options.Parse(new[] { "--medium", "canvas", "poza.tif" });
        var after = Options.Parse(new[] { "poza.tif", "--medium", "canvas" });

        Assert.Equal("poza.tif", before.Image);
        Assert.Equal("poza.tif", after.Image);
        Assert.Equal("canvas", before.Medium);
        Assert.Equal("canvas", after.Medium);
    }

    [Fact]
    public void R006_NumbersAreParsedWithTheInvariantCulture()
    {
        var o = Options.Parse(new[] { "poza.tif", "--distance", "1.5", "--target-bg", "0.19", "--dmax", "2.2" });
        // The interval and the rounding are the core's, with its message behind the flag's name (R-023).
        Assert.Equal("--target-bg: " + Parameters.TargetBackgroundError(1), Options.Parse(new[] { "poza.tif", "--target-bg", "1" }).Error);
        Assert.Equal(0.1234567, Options.Parse(new[] { "poza.tif", "--target-bg", "0.1234567" }).TargetBg);

        Assert.Null(o.Error);
        Assert.Equal(1.5, o.Distance);
        Assert.Equal(0.19, o.TargetBg);
        Assert.Equal(2.2, o.Dmax);
    }

    [Fact]
    public void R021_SizesAreACommaSeparatedListOfWidths()
    {
        var o = Options.Parse(new[] { "poza.tif", "--sizes", "60,70, 80" });

        Assert.Null(o.Error);
        Assert.Equal(new[] { 60.0, 70.0, 80.0 }, o.Sizes);
    }

    [Theory]
    [InlineData("50:35")]
    [InlineData("50x35")]
    public void R022_AspectAcceptsColonAndX(string text)
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
    [InlineData("--target-bg", "1")]
    [InlineData("--target-bg", "0")]
    [InlineData("--target-bg", "0.0004")]
    [InlineData("--target-bg", "NaN")]
    [InlineData("--target-bg", "Infinity")]
    public void R003_ABadValueIsAnErrorNotAnException(string flag, string value)
    {
        var o = Options.Parse(new[] { "poza.tif", flag, value });

        Assert.NotNull(o.Error);
        Assert.Contains(flag, o.Error);
    }

    [Fact]
    public void R003_AMissingValueIsReported()
    {
        Assert.Contains("lipseste valoarea", Options.Parse(new[] { "poza.tif", "--distance" }).Error);
        Assert.Contains("asteapta o valoare", Options.Parse(new[] { "poza.tif", "--distance", "--dmax", "2" }).Error);
    }

    [Fact]
    public void R003_AnUnknownOptionIsReported()
    {
        Assert.Contains("optiune necunoscuta", Options.Parse(new[] { "poza.tif", "--version" }).Error);
    }

    [Fact]
    public void R003_NoImageOrTwoImagesIsAnError()
    {
        Assert.Contains("lipseste fisierul", Options.Parse(new[] { "--medium", "baryta" }).Error);
        Assert.Contains("un singur fisier", Options.Parse(new[] { "a.tif", "b.tif" }).Error);
    }

    [Fact]
    public void R004_NoArgumentsOrHelpAsksForUsage()
    {
        Assert.True(Options.Parse(Array.Empty<string>()).Help);
        Assert.True(Options.Parse(new[] { "poza.tif", "-h" }).Help);
        Assert.True(Options.Parse(new[] { "--help" }).Help);
    }
}
