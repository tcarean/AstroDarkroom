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

using DarksCheck.Cli;

namespace DarksCheck.Tests;

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
        Assert.Contains("Folosire: darkscheck <folder sesiune sau folder de dark-uri>", stdout, StringComparison.Ordinal);
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
    public void R004_AnEmptyArgument_IsAnErrorNotACrash_Exit1()
    {
        (int code, _, string stderr) = Cli.Run("");
        Assert.Equal(1, code);
        Assert.StartsWith("EROARE:", stderr, StringComparison.Ordinal);
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
    public void R002_ASessionWithLightsButNoDarks_Exit1()
    {
        using TempWorkspace ws = new();
        Directory.CreateDirectory(ws.PathTo("lights"));
        File.WriteAllBytes(ws.PathTo("result.fit"), SessionFixture.Frame(SessionFixture.FirstStart, 10, 400));
        (int code, _, string stderr) = Cli.Run(ws.Dir);
        Assert.Equal(1, code);
        Assert.Contains("nu are darks", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void R002_DarksSubfolderIsReadAndBiasesAndLightsCounted_Exit0()
    {
        using SessionFixture s = new SessionFixture().WithDarks(3, biases: 2, lights: 4);
        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--inventory");
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("darks              : darks\\ (3 cadre .fit)", stdout, StringComparison.Ordinal);
        Assert.Contains("biases             : biases\\ (2 cadre .fit)", stdout, StringComparison.Ordinal);
        Assert.Contains("lights             : lights\\ (4 cadre .fit)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R002_FramesDirectlyInTheFolderAreTheDarks()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, inSubfolder: false);
        (int code, string stdout, _) = Cli.Run(s.Root);
        Assert.Equal(0, code);
        Assert.Contains("darks              : . (2 cadre .fit)", stdout, StringComparison.Ordinal);
        Assert.Contains("biases             : (lipsa)", stdout, StringComparison.Ordinal);
        Assert.Contains("lights             : (lipsa)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R003_EveryFrameRowCarriesItsMetadata()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, biases: 1, lights: 1);
        (_, string stdout, _) = Cli.Run(s.Root, "--inventory");
        Assert.Contains("aparat             : NIKON D3300", stdout, StringComparison.Ordinal);
        Assert.Contains("expunere darks     : 119.2 s la ISO 400 (toate cadrele)", stdout, StringComparison.Ordinal);
        Assert.Contains("expunere biases    : 0.0002 s la ISO 400 (toate cadrele)", stdout, StringComparison.Ordinal);
        Assert.Contains("  DARKS\n     nr  fisier                ora       expunere   ISO   pauza\n", stdout, StringComparison.Ordinal);
        Assert.Contains("  1  DSC_0001.fit          00:48:53     119.2    400       -", stdout, StringComparison.Ordinal);
        Assert.Contains("  2  DSC_0002.fit          00:50:53     119.2    400     1 s", stdout, StringComparison.Ordinal);
        Assert.Contains("  BIASES\n     nr  fisier                ora       expunere   ISO\n", stdout, StringComparison.Ordinal);
        Assert.Contains("  1  bias_0001.fit         22:48:53    0.0002    400", stdout, StringComparison.Ordinal);
        Assert.Contains("  LIGHTS\n", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R003_AFrameThatCannotBeReadIsListedWithItsError_Exit1()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, biases: 1);
        File.WriteAllBytes(Path.Combine(s.BiasesDir, "bias_0002.fit"), new byte[100]);
        (int code, string stdout, _) = Cli.Run(s.Root);
        Assert.Equal(1, code);
        Assert.Contains("  2  bias_0002.fit         eroare:", stdout, StringComparison.Ordinal);
        Assert.Contains("ATENTIE            : 1 cadru nu s-a putut citi", stdout, StringComparison.Ordinal);
    }

    /// <summary>A raw libraw cannot open is this frame's error, not the run's: libraw's own exception type is caught too.</summary>
    [Fact]
    public void R003_ACorruptRawIsListedWithItsErrorAndTheRunGoesOn_Exit1()
    {
        using SessionFixture s = new SessionFixture().WithDarks(1);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0002.NEF"), new byte[4096]);
        (int code, string stdout, string stderr) = Cli.Run(s.DarksDir);
        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("  1  DSC_0002.NEF          eroare:", stdout, StringComparison.Ordinal);
        Assert.Contains("darks              : . (1 cadru .NEF)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R005_NumbersUseTheDotWhateverTheCulture()
    {
        using SessionFixture s = new SessionFixture().WithDarks(1, exposure: 0.5);
        (_, string stdout, _) = Cli.Run(s.Root);
        Assert.Contains("0.5 s la ISO 400", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("0,5", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R006_IntervalLineGivesSpanCadenceAndPauses()
    {
        using SessionFixture s = new SessionFixture().WithDarks(3, exposure: 119.2, cadence: 120);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0004.fit"), SessionFixture.Frame(SessionFixture.FirstStart.AddSeconds(600), 119.2, 400));
        (_, string stdout, _) = Cli.Run(s.Root);
        // Starts at 0, 120, 240 and 600 s; the last ends at 719.2 s (11 whole minutes); the pause before frame 4 is 600 - 359.2 = 241 s.
        Assert.Contains("interval darks     : 2026-08-13 00:48:53 -> 2026-08-13 01:00:52 (11 min); 4 cadre la 120.0 s; 1 pauza peste 5 s (241 s inainte de cadrul 4)", stdout, StringComparison.Ordinal);
        Assert.Contains("  4  DSC_0004.fit          00:58:53     119.2    400   241 s", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R006_BackToBackDarksHaveNoLongPause()
    {
        using SessionFixture s = new SessionFixture().WithDarks(3, lights: 2, lightsCadence: 150);
        (_, string stdout, _) = Cli.Run(s.Root);
        // Three darks of 119.2 s started 120 s apart span 359.2 s: 5 whole minutes.
        Assert.Contains("interval darks     : 2026-08-13 00:48:53 -> 2026-08-13 00:54:52 (5 min); 3 cadre la 120.0 s; fara pauze peste 5 s", stdout, StringComparison.Ordinal);
        Assert.Contains("interval lights    : 2026-08-12 23:43:53 -> 2026-08-12 23:48:22 (4 min); 2 cadre la 150.0 s; 1 pauza peste 5 s (31 s inainte de cadrul 2)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_DarksThatMatchTheLightsAreOk()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, lights: 2, biases: 1);
        (_, string stdout, _) = Cli.Run(s.Root);
        Assert.Contains("potrivire          : OK: dark-urile au expunerea (119.2 s) si ISO-ul (400) lights-urilor", stdout, StringComparison.Ordinal);
        Assert.Contains("bias               : OK: 0.0002 s", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_DarksAtAnotherIsoOrExposureAreAWarning()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, exposure: 120.2, iso: 1600, lights: 2, lightsExposure: 119.2, lightsIso: 400);
        (int code, string stdout, _) = Cli.Run(s.Root);
        Assert.Equal(0, code);
        Assert.Contains("potrivire          : ATENTIE: dark-uri de 120.2 s la ISO 1600, lights de 119.2 s la ISO 400", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_WithoutLightsTheMatchIsUnmeasuredAndTheBiasAbsent()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2);
        (_, string stdout, _) = Cli.Run(s.Root);
        Assert.Contains("potrivire          : NEMASURAT: sesiunea nu are lights", stdout, StringComparison.Ordinal);
        Assert.Contains("bias               : (lipsa)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_ABiasLongerThanASecondIsAWarning()
    {
        using SessionFixture s = new SessionFixture().WithDarks(1, biases: 2, biasExposure: 2);
        (_, string stdout, _) = Cli.Run(s.Root);
        Assert.Contains("bias               : ATENTIE: 2.0 s nu e un bias (peste 1 s)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_AGroupWithMixedExposuresSaysSo()
    {
        using SessionFixture s = new SessionFixture().WithDarks(3);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0004.fit"), SessionFixture.Frame(SessionFixture.FirstStart.AddSeconds(360), 60, 400));
        (_, string stdout, _) = Cli.Run(s.Root);
        Assert.Contains("expunere darks     : 119.2 s la ISO 400 (3 din 4 cadre; expuneri sau ISO diferite)", stdout, StringComparison.Ordinal);
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
