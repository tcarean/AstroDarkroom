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

namespace CopyWcs.Tests;

public class OptionsTests
{
    [Fact]
    public void Parse_TwoPositionalArgs_AssignsSourceAndDestInOrder()
    {
        var opt = Options.Parse(["src.fit", "dest.fit"]);

        Assert.Equal("src.fit", opt.Source);
        Assert.Equal("dest.fit", opt.Dest);
    }

    [Fact]
    public void Parse_NoPositionalArgs_Throws()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(Array.Empty<string>()));
    }

    [Fact]
    public void Parse_OnePositionalArg_Throws()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(["only-one.fit"]));
    }

    [Fact]
    public void Parse_ThreePositionalArgs_Throws()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(["a.fit", "b.fit", "c.fit"]));
    }

    [Fact]
    public void Parse_UnknownFlag_Throws()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(["src.fit", "dest.fit", "--bogus"]));
    }

    [Theory]
    [InlineData("-o")]
    [InlineData("--output")]
    public void Parse_OutputFlagMissingValue_Throws(string flag)
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "src.fit", "dest.fit", flag }));
    }

    [Theory]
    [InlineData("-o")]
    [InlineData("--output")]
    public void Parse_OutputFlagWithValue_SetsOutput(string flag)
    {
        var opt = Options.Parse(new[] { "src.fit", "dest.fit", flag, "out.fit" });

        Assert.Equal("out.fit", opt.Output);
    }

    [Fact]
    public void Parse_OutputValueLooksLikeAFlag_ConsumedVerbatimAsFilename()
    {
        // Comportament existent: valoarea de dupa -o e consumata ca atare, chiar daca incepe cu '-'.
        var opt = Options.Parse(["src.fit", "dest.fit", "-o", "-x"]);

        Assert.Equal("-x", opt.Output);
    }

    [Fact]
    public void Parse_DryRunFlag_SetsDryRun()
    {
        var opt = Options.Parse(["src.fit", "dest.fit", "--dry-run"]);

        Assert.True(opt.DryRun);
    }

    [Fact]
    public void Parse_ForceFlag_SetsForce()
    {
        var opt = Options.Parse(["src.fit", "dest.fit", "--force"]);

        Assert.True(opt.Force);
    }

    [Fact]
    public void Parse_NoOpticalFlag_SetsNoOptical()
    {
        var opt = Options.Parse(["src.fit", "dest.fit", "--no-optical"]);

        Assert.True(opt.NoOptical);
    }

    [Fact]
    public void Parse_AllBooleanFlagsTogether_AllSet()
    {
        var opt = Options.Parse(["src.fit", "dest.fit", "--dry-run", "--force", "--no-optical"]);

        Assert.True(opt.DryRun);
        Assert.True(opt.Force);
        Assert.True(opt.NoOptical);
    }

    [Fact]
    public void Parse_HelpFlag_ShortCircuitsBeforeOtherValidation()
    {
        // Fara -h, un singur argument pozitional ar arunca - dar -h se intoarce imediat.
        var opt = Options.Parse(["-h", "only-one-arg"]);

        Assert.True(opt.ShowHelp);
    }

    [Fact]
    public void Parse_HelpFlagMidArgv_StopsParsingImmediatelyWithoutAssigningPositionals()
    {
        var opt = Options.Parse(["src.fit", "-h", "dest.fit"]);

        Assert.True(opt.ShowHelp);
        // Source/Dest se asigneaza doar dupa bucla; -h iese din bucla mai devreme, deci raman "".
        Assert.Equal("", opt.Source);
        Assert.Equal("", opt.Dest);
    }

    [Fact]
    public void Parse_FlagsInterleavedWithPositionals_StillParsedCorrectly()
    {
        var opt = Options.Parse(["--force", "src.fit", "--dry-run", "dest.fit"]);

        Assert.Equal("src.fit", opt.Source);
        Assert.Equal("dest.fit", opt.Dest);
        Assert.True(opt.Force);
        Assert.True(opt.DryRun);
    }

    [Fact]
    public void PrintUsage_WritesToGivenWriterAndMentionsKeyFlags()
    {
        var writer = new StringWriter();

        Options.PrintUsage(writer);

        var text = writer.ToString();
        Assert.Contains("--dry-run", text);
        Assert.Contains("--force", text);
        Assert.Contains("--no-optical", text);
        Assert.Contains("--output", text);
    }
}
