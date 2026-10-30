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
    /// <summary>R-003: the mode is decided once, in Parse, by the precedence of the flags;
    /// the dispatch and the log read the same value (the review of 2026-10-27, D1).</summary>
    [Theory]
    [InlineData("--lenses --synthetic s.fit --histeq", "Lenses")]
    [InlineData("--synthetic s.fit --against b.fit", "Synthetic")]
    [InlineData("--against b.fit --smooth s.fit", "Against")]
    [InlineData("--smooth s.fit --histeq", "Smooth")]
    [InlineData("--histeq --expo", "Histeq")]
    [InlineData("--expo --light", "Expo")]
    [InlineData("--light", "Light")]
    [InlineData("", "Structure")]
    public void R003_TheModeIsDecidedOnceByThePrecedenceOfTheFlags(string flags, string expected)
    {
        var o = Options.Parse(("flat.fit " + flags).Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Null(o.Error);
        Assert.Equal(Enum.Parse<Mode>(expected), o.Mode);
    }

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
        Assert.False(o.Light);
        Assert.False(o.Expo);
        Assert.False(o.Sky);
        Assert.False(o.Histeq);
        Assert.Equal("flatcheck.png", o.OutPng);
        Assert.Null(o.Sat);
        Assert.Null(o.Black);
    }

    [Fact]
    public void HisteqFlag_IsParsed_OnEitherSideOfThePath()
    {
        var after = Options.Parse(new[] { "flat.NEF", "--histeq", "-o", "poza.png" });
        var before = Options.Parse(new[] { "--histeq", "flat.NEF", "-o", "poza.png" });

        Assert.True(after.Histeq);
        Assert.True(before.Histeq);
        Assert.Equal("flat.NEF", after.Image);
        Assert.Equal("flat.NEF", before.Image);
        Assert.Equal("poza.png", after.OutPng);
        Assert.Null(after.Error);
    }

    /// <summary>
    /// Mode flags are not mutually validated here; dispatch order in the CLI decides.
    /// </summary>
    [Fact]
    public void HisteqAndExpoTogether_BothStaySet()
    {
        var o = Options.Parse(new[] { "--histeq", "--expo", "flat.NEF" });

        Assert.True(o.Histeq);
        Assert.True(o.Expo);
        Assert.Null(o.Error);
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

    [Fact]
    public void R104_RainbowFlag_IsRecognisedWithHisteq()
    {
        var o = Options.Parse(new[] { "--rainbow", "flat.fit", "--histeq" });

        Assert.Null(o.Error);
        Assert.True(o.Histeq);
        Assert.True(o.Rainbow);
        Assert.False(Options.Parse(new[] { "flat.fit", "--histeq" }).Rainbow);
    }

    /// <summary>A modifier of one mode's picture: alone it is a typo, not a silent plain picture.</summary>
    [Fact]
    public void R104_RainbowWithoutHisteq_IsAnError()
    {
        var o = Options.Parse(new[] { "flat.fit", "--rainbow" });

        Assert.NotNull(o.Error);
        Assert.Contains("--rainbow", o.Error);
        Assert.Contains("--histeq", o.Error);
    }

    [Theory]
    [InlineData("-o")]
    [InlineData("--out")]
    public void R006_OutputFlag_OverridesTheDefaultPngPath(string flag)
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
        var o = Options.Parse(new[] { "--sat", "4095", "--against", "b.fit", "a.fit" });

        Assert.Null(o.Error);
        Assert.Equal("a.fit", o.Image);
        Assert.Equal("b.fit", o.Against);
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
        var o = Options.Parse(new[] { "a.fit", "--against" });

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

    // ---------------------------------------------------------------- --light on a series

    /// <summary>
    /// The default is Siril's convention for CALIBRATED but UNREGISTERED frames. It is not
    /// changed without reading the comment beside it in Options.cs: "r_pp_light_*" is
    /// left out on purpose.
    /// </summary>
    [Fact]
    public void ThePatternDefaultsToTheSirilCalibratedNaming()
    {
        Assert.Equal("pp_light_*.fit", Options.Parse(new[] { "process" }).Pattern);
    }

    [Fact]
    public void ThePatternCanBeOverridden()
    {
        var o = Options.Parse(new[] { "process", "--light", "--pattern", "sub_*.fits" });

        Assert.Null(o.Error);
        Assert.Equal("sub_*.fits", o.Pattern);
    }

    [Fact]
    public void AnEmptyPatternIsAnError()
    {
        Assert.Contains("sirul gol", Options.Parse(new[] { "process", "--pattern", "" }).Error);
    }

    [Fact]
    public void TheFieldOfViewIsUnsetUnlessGiven()
    {
        Assert.Null(Options.Parse(new[] { "light.fit", "--light" }).Fov);
    }

    [Fact]
    public void TheFieldOfViewIsParsedAsDegrees()
    {
        var o = Options.Parse(new[] { "light.fit", "--light", "--fov", "11.7" });

        Assert.Null(o.Error);
        Assert.Equal(11.7, o.Fov);
    }

    [Theory]
    [InlineData("abc", "asteapta un numar")]
    [InlineData("0", "intre 0 si 180")]
    [InlineData("-5", "intre 0 si 180")]
    [InlineData("180", "intre 0 si 180")]
    [InlineData("400", "intre 0 si 180")]
    public void ANonsensicalFieldOfViewIsAnError(string value, string expected)
    {
        Assert.Contains(expected, Options.Parse(new[] { "light.fit", "--fov", value }).Error);
    }

    /// <summary>A folder is a path like any other at parsing; the dispatch decides what to
    /// do with it.</summary>
    [Fact]
    public void ADirectoryIsAcceptedAsThePositional()
    {
        var o = Options.Parse(new[] { "process", "--light" });

        Assert.Null(o.Error);
        Assert.Equal("process", o.Image);
        Assert.True(o.Light);
    }

    // ---------------------------------------------------------------- --smooth

    [Fact]
    public void SmoothFlag_TakesTheOutputPath_OnEitherSideOfTheInput()
    {
        var after = Options.Parse(new[] { "flat.fit", "--smooth", "netezit.fit" });
        var before = Options.Parse(new[] { "--smooth", "netezit.fit", "flat.fit" });

        Assert.Null(after.Error);
        Assert.Null(before.Error);
        Assert.Equal("flat.fit", after.Image);
        Assert.Equal("flat.fit", before.Image);
        Assert.Equal("netezit.fit", after.Smooth);
        Assert.Equal("netezit.fit", before.Smooth);
        Assert.Equal(SmoothAnalyzer.DefaultScalePx, after.ScalePx);
    }

    [Fact]
    public void SmoothFlag_RefusesToWriteOverTheInput()
    {
        var o = Options.Parse(new[] { "flat.fit", "--smooth", "FLAT.FIT" });

        Assert.NotNull(o.Error);
        Assert.Contains("peste fisierul de intrare", o.Error);
    }

    [Fact]
    public void SmoothFlag_NeedsAValue()
    {
        Assert.Contains("lipseste valoarea", Options.Parse(new[] { "flat.fit", "--smooth" }).Error);
        Assert.Contains("asteapta o valoare", Options.Parse(new[] { "flat.fit", "--smooth", "--expo" }).Error);
    }

    [Theory]
    [InlineData("150", 150.0)]
    [InlineData("600", 600.0)]
    public void ScaleFlag_SetsTheSmoothingScale(string value, double expected)
    {
        var o = Options.Parse(new[] { "flat.fit", "--smooth", "s.fit", "--scale", value });

        Assert.Null(o.Error);
        Assert.Equal(expected, o.ScalePx);
    }

    [Theory]
    [InlineData("10")]
    [InlineData("5000")]
    [InlineData("abc")]
    public void ScaleFlag_RefusesNonsense(string value)
    {
        var o = Options.Parse(new[] { "flat.fit", "--smooth", "s.fit", "--scale", value });

        Assert.NotNull(o.Error);
        Assert.Contains("--scale", o.Error);
    }

    [Fact]
    public void ScaleFlag_AcceptsAuto_InAnyCase()
    {
        var o = Options.Parse(new[] { "flat.fit", "--smooth", "s.fit", "--scale", "Auto" });

        Assert.Null(o.Error);
        Assert.True(o.ScaleAuto);
        Assert.Equal(SmoothAnalyzer.DefaultScalePx, o.ScalePx);
        Assert.False(Options.Parse(new[] { "flat.fit", "--smooth", "s.fit", "--scale", "300" }).ScaleAuto);
    }

    /// <summary>
    /// --scale without --smooth parses and is simply unused; dispatch never reads it.
    /// </summary>
    [Fact]
    public void ScaleFlag_WithoutSmooth_IsAccepted()
    {
        var o = Options.Parse(new[] { "flat.fit", "--scale", "200" });

        Assert.Null(o.Error);
        Assert.Null(o.Smooth);
        Assert.Equal(200, o.ScalePx);
    }
}
