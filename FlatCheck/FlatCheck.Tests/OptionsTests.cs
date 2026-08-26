namespace FlatCheck.Tests;

/// <summary>
/// Phase 2: CLI parsing. These pin *current* behaviour, quirks included - the point is
/// that the extraction of <see cref="Options"/> out of Main changed nothing. Tests that
/// document a known bug say so, and name the phase that is meant to flip them.
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

    [Fact]
    public void NonNumericSat_IsIgnoredRatherThanFailing()
    {
        var o = Options.Parse(new[] { "raw.fit", "--expo", "--sat", "abc" });

        Assert.Null(o.Sat);
    }

    [Fact]
    public void FlagAtTheEndWithNoValue_YieldsNull()
    {
        var o = Options.Parse(new[] { "a.fit", "--ratio" });

        Assert.Null(o.Ratio);
    }

    [Fact]
    public void GetOpt_ReturnsNullForAnAbsentKey()
    {
        Assert.Null(Options.GetOpt(new[] { "a.fit" }, "--ratio"));
    }

    // ------------------------------------------------------------- known quirks

    /// <summary>
    /// Known issue 1: args[0] is unconditionally the image path, so a flag placed first
    /// becomes the filename. Phase 8 fixes this; this test must then be inverted.
    /// </summary>
    [Fact]
    public void KnownIssue_FlagBeforePath_IsSwallowedAsTheImagePath()
    {
        var o = Options.Parse(new[] { "--expo", "raw.fit" });

        Assert.Equal("--expo", o.Image);
        Assert.True(o.Expo);
    }

    /// <summary>
    /// Known issue 1 (same root cause): a flag's "value" is never validated, so the next
    /// flag is happily consumed as the value. Phase 8 should reject this.
    /// </summary>
    [Fact]
    public void KnownIssue_FlagValue_IsNotValidated_SoAFlagCanBecomeAValue()
    {
        var o = Options.Parse(new[] { "a.fit", "-o", "--light" });

        Assert.Equal("--light", o.OutPng);
        Assert.True(o.Light);
    }
}
