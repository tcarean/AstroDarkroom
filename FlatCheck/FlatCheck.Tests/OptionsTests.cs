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

namespace FlatCheck.Tests;

/// <summary>
/// CLI parsing. Written in phase 2 to pin the behaviour through the extraction, then
/// updated in phase 8, which made flags position-independent and added validation.
/// Parse never throws: it returns an Options carrying Error.
/// </summary>
public class OptionsTests
{
    [Fact]
    public void NoArgs_RequestsHelp()
    {
        var o = Options.Parse(Array.Empty<string>());

        Assert.True(o.Help);
        Assert.Null(o.Image);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void HelpFlag_RequestsHelp_EvenAlongsideOtherArgs(string flag)
    {
        var o = Options.Parse(new[] { "flat.fit", flag, "--expo" });

        Assert.True(o.Help);
    }

    [Fact]
    public void PathOnly_SelectsStructureModeWithDefaults()
    {
        var o = Options.Parse(new[] { "flat.fit" });

        Assert.False(o.Help);
        Assert.Equal("flat.fit", o.Image);
        Assert.Null(o.Ratio);
        Assert.False(o.Light);
        Assert.False(o.Expo);
        Assert.False(o.Sky);
        Assert.Equal("flatcheck.png", o.OutPng);
        Assert.Null(o.Sat);
        Assert.Null(o.Black);
    }

    [Fact]
    public void RatioFlag_CapturesTheSecondPath()
    {
        var o = Options.Parse(new[] { "a.fit", "--ratio", "b.fit" });

        Assert.Equal("a.fit", o.Image);
        Assert.Equal("b.fit", o.Ratio);
    }

    [Fact]
    public void LightFlag_IsRecognised()
    {
        var o = Options.Parse(new[] { "light.fit", "--light" });

        Assert.True(o.Light);
        Assert.Equal("light.fit", o.Image);
    }

    [Fact]
    public void ExpoAndSky_AreRecognisedTogether()
    {
        var o = Options.Parse(new[] { "raw.fit", "--expo", "--sky" });

        Assert.True(o.Expo);
        Assert.True(o.Sky);
    }

    [Theory]
    [InlineData("-o")]
    [InlineData("--out")]
    public void OutputFlag_OverridesTheDefaultPngPath(string flag)
    {
        var o = Options.Parse(new[] { "flat.fit", flag, "harta.png" });

        Assert.Equal("harta.png", o.OutPng);
    }

    [Fact]
    public void SatAndBlack_AreParsedAsNumbers()
    {
        var o = Options.Parse(new[] { "raw.fit", "--expo", "--sat", "4095", "--black", "150" });

        Assert.Equal(4095.0, o.Sat);
        Assert.Equal(150.0, o.Black);
    }

    [Fact]
    public void SatAndBlack_UseInvariantCulture_SoADotIsAlwaysTheDecimalSeparator()
    {
        var o = Options.Parse(new[] { "raw.fit", "--expo", "--sat", "4095.5" });

        Assert.Equal(4095.5, o.Sat);
    }

    // ------------------------------------------------------- flags are positional-free

    /// <summary>
    /// Phase 8. Before it, args[0] was unconditionally the image path, so
    /// "flatcheck --expo f.fit" died with "Could not find file '--expo'".
    /// </summary>
    [Fact]
    public void AFlagBeforeThePath_ParsesTheSameAsAFlagAfterIt()
    {
        var before = Options.Parse(new[] { "--expo", "raw.fit" });
        var after = Options.Parse(new[] { "raw.fit", "--expo" });

        Assert.Null(before.Error);
        Assert.Equal("raw.fit", before.Image);
        Assert.True(before.Expo);
        Assert.Equal(after.Image, before.Image);
        Assert.Equal(after.Expo, before.Expo);
    }

    [Fact]
    public void ValueFlagsAlsoWorkBeforeThePath()
    {
        var o = Options.Parse(new[] { "--sat", "4095", "--ratio", "b.fit", "a.fit" });

        Assert.Null(o.Error);
        Assert.Equal("a.fit", o.Image);
        Assert.Equal("b.fit", o.Ratio);
        Assert.Equal(4095.0, o.Sat);
    }

    [Fact]
    public void ANegativeNumberIsStillAcceptedAsAValue()
    {
        var o = Options.Parse(new[] { "raw.fit", "--expo", "--sat", "4095", "--black", "-50" });

        Assert.Null(o.Error);
        Assert.Equal(-50.0, o.Black);
    }

    // ------------------------------------------------------------------ validation

    [Fact]
    public void AMissingValue_IsAnError()
    {
        var o = Options.Parse(new[] { "a.fit", "--ratio" });

        Assert.Contains("lipseste valoarea", o.Error);
    }

    [Fact]
    public void AFlagUsedAsAValue_IsAnError()
    {
        var o = Options.Parse(new[] { "a.fit", "-o", "--light" });

        Assert.Contains("asteapta o valoare", o.Error);
    }

    [Fact]
    public void ANonNumericSat_IsAnError()
    {
        var o = Options.Parse(new[] { "raw.fit", "--expo", "--sat", "abc" });

        Assert.Contains("--sat asteapta un numar", o.Error);
    }

    [Fact]
    public void AnUnknownOption_IsAnError()
    {
        var o = Options.Parse(new[] { "a.fit", "--nope" });

        Assert.Contains("optiune necunoscuta", o.Error);
    }

    [Fact]
    public void NoInputFile_IsAnError()
    {
        var o = Options.Parse(new[] { "--light" });

        Assert.Contains("lipseste fisierul", o.Error);
    }

    [Fact]
    public void TwoInputFiles_AreAnError()
    {
        var o = Options.Parse(new[] { "a.fit", "b.fit" });

        Assert.Contains("un singur fisier", o.Error);
    }

    /// <summary>--sat 0 used to make the range 0, so every percentage came out NaN.</summary>
    [Fact]
    public void ASaturationAtOrBelowBlack_IsAnError()
    {
        Assert.Contains("mai mare decat", Options.Parse(new[] { "raw.fit", "--sat", "0" }).Error);
        Assert.Contains("mai mare decat",
            Options.Parse(new[] { "raw.fit", "--sat", "100", "--black", "150" }).Error);
    }

    [Fact]
    public void AValidCommandLineHasNoError()
    {
        Assert.Null(Options.Parse(new[] { "raw.fit", "--expo", "--sat", "4095", "--black", "150" }).Error);
    }
}
