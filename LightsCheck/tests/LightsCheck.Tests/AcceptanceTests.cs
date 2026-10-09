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

using LightsCheck.Cli;

namespace LightsCheck.Tests;

/// <summary>
/// Black-box tests of the console: arguments in, text and exit code out. They know nothing
/// of the core's types, so they survive a rewrite of the implementation unchanged.
/// </summary>
public class AcceptanceTests
{
    [Fact]
    public void R004_NoArguments_PrintsUsageAndWarranty_Exit0()
    {
        (int code, string stdout, string stderr) = Cli.Run();
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("Folosire: lightscheck <folder sesiune>", stdout, StringComparison.Ordinal);
        Assert.Contains("FARA NICIO GARANTIE", stdout, StringComparison.Ordinal);
        Assert.Contains("LICENSE", stdout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void R004_HelpFlag_PrintsUsage_Exit0(string flag)
    {
        (int code, string stdout, _) = Cli.Run(flag);
        Assert.Equal(0, code);
        Assert.Contains("Folosire:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R004_UnknownOption_NamesIt_Exit1()
    {
        (int code, _, string stderr) = Cli.Run("--inventat");
        Assert.Equal(1, code);
        Assert.Contains("--inventat", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void R004_TwoFolders_NamesTheSecond_Exit1()
    {
        (int code, _, string stderr) = Cli.Run("unu", "doi");
        Assert.Equal(1, code);
        Assert.Contains("doi", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void R002_MissingFolder_NamesIt_Exit1()
    {
        using TempWorkspace ws = new();
        string missing = ws.PathTo("nu-exista");
        (int code, _, string stderr) = Cli.Run(missing);
        Assert.Equal(1, code);
        Assert.StartsWith("EROARE:", stderr, StringComparison.Ordinal);
        Assert.Contains("nu-exista", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void R002_FolderWithoutFrames_Exit1()
    {
        using TempWorkspace ws = new();
        File.WriteAllText(ws.PathTo("notite.txt"), "nimic");
        (int code, _, string stderr) = Cli.Run(ws.Dir);
        Assert.Equal(1, code);
        Assert.Contains("niciun cadru", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void R002_LightsSubfolderIsReadAndBiasesCounted_Exit0()
    {
        using SessionFixture s = new SessionFixture().WithLights(3, biases: 2);
        (int code, string stdout, string stderr) = Cli.Run(s.Root);
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("lights             : lights\\ (3 cadre .fit)", stdout, StringComparison.Ordinal);
        Assert.Contains("biases             : biases\\ (2 cadre .fit)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R002_FramesDirectlyInTheFolderAreTheLights()
    {
        using SessionFixture s = new SessionFixture().WithLights(2, inSubfolder: false);
        (int code, string stdout, _) = Cli.Run(s.Root);
        Assert.Equal(0, code);
        Assert.Contains("lights             : . (2 cadre .fit)", stdout, StringComparison.Ordinal);
        Assert.Contains("biases             : (lipsa)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R003_EveryFrameRowCarriesItsMetadata()
    {
        using SessionFixture s = new SessionFixture().WithLights(2);
        (_, string stdout, _) = Cli.Run(s.Root);
        Assert.Contains("aparat             : NIKON D3300", stdout, StringComparison.Ordinal);
        Assert.Contains("expunere           : 120.2 s la ISO 1600 (toate cadrele)", stdout, StringComparison.Ordinal);
        Assert.Contains("  1  DSC_0001.fit          20:35:03     120.2   1600     -   135 mm       -", stdout, StringComparison.Ordinal);
        Assert.Contains("  2  DSC_0002.fit          20:37:24     120.2   1600     -   135 mm    21 s", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R003_AFrameThatCannotBeReadIsListedWithItsError_Exit1()
    {
        using SessionFixture s = new SessionFixture().WithLights(2);
        File.WriteAllBytes(Path.Combine(s.LightsDir, "DSC_0003.fit"), new byte[100]);
        (int code, string stdout, _) = Cli.Run(s.Root);
        Assert.Equal(1, code);
        Assert.Contains("  3  DSC_0003.fit          eroare:", stdout, StringComparison.Ordinal);
        Assert.Contains("ATENTIE            : 1 cadru nu s-a putut citi", stdout, StringComparison.Ordinal);
    }

    /// <summary>A raw libraw cannot open is this frame's error, not the run's: libraw's own exception type is caught too.</summary>
    [Fact]
    public void R003_ACorruptRawIsListedWithItsErrorAndTheRunGoesOn_Exit1()
    {
        using SessionFixture s = new SessionFixture().WithLights(1);
        File.WriteAllBytes(Path.Combine(s.LightsDir, "DSC_0002.NEF"), new byte[4096]);
        (int code, string stdout, string stderr) = Cli.Run(s.LightsDir);
        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("  1  DSC_0002.NEF          eroare:", stdout, StringComparison.Ordinal);
        Assert.Contains("lights             : . (1 cadru .NEF)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R004_AnEmptyArgument_IsAnErrorNotACrash_Exit1()
    {
        (int code, _, string stderr) = Cli.Run("");
        Assert.Equal(1, code);
        Assert.StartsWith("EROARE:", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void R017_FrameOptionMeasuresOneLight_Exit0()
    {
        using SessionFixture s = new SessionFixture().WithLights(2, withStars: true);
        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--frame", "2");
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("=== CADRU 2: DSC_0002.fit ===", stdout, StringComparison.Ordinal);
        Assert.Contains("640 x 480 px; scara 0 .. 65535 ADU (FITS (presupus din BITPIX 16))", stdout, StringComparison.Ordinal);
        Assert.Contains("nivele CFA         : R 400", stdout, StringComparison.Ordinal);
        Assert.Contains("stele              : 40 detectate", stdout, StringComparison.Ordinal);
        Assert.Contains("inregistrare       : 40 stele retinute", stdout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("x")]
    public void R017_FrameOutsideTheSession_Exit1(string n)
    {
        using SessionFixture s = new SessionFixture().WithLights(2);
        (int code, _, string stderr) = Cli.Run(s.Root, "--frame", n);
        Assert.Equal(1, code);
        Assert.StartsWith("EROARE:", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void R006_IntervalLineGivesSpanIntegrationAndDuty()
    {
        using SessionFixture s = new SessionFixture().WithLights(3, exposure: 120, cadence: 150);
        (_, string stdout, _) = Cli.Run(s.Root);
        // 3 frames: starts at 0, 150, 300 s; the last ends at 420 s; 360 s of shutter over 420 = 86 %.
        Assert.Contains("interval           : 2026-10-06 20:35:03 -> 2026-10-06 20:42:03 (7 min); integrare 6 min din 3 cadre (86 % din interval)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R005_NumbersUseTheDotWhateverTheCulture()
    {
        using SessionFixture s = new SessionFixture().WithLights(1, exposure: 0.5);
        (_, string stdout, _) = Cli.Run(s.Root);
        Assert.Contains("0.5 s la ISO 1600", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("0,5", stdout, StringComparison.Ordinal);
    }
}

/// <summary>Runs the console in process and captures both streams.</summary>
internal static class Cli
{
    internal static (int Code, string Stdout, string Stderr) Run(params string[] args)
    {
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        int code = Program.Run(args, stdout, stderr);
        return (code, stdout.ToString().ReplaceLineEndings("\n"), stderr.ToString().ReplaceLineEndings("\n"));
    }
}
