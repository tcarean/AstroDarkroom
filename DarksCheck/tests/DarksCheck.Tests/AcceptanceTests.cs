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
        (int code, _, string stderr) = Cli.Run(ws.Dir, "--inventory");
        Assert.Equal(1, code);
        Assert.Contains("niciun cadru", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void R002_ASessionWithLightsButNoDarks_Exit1()
    {
        // an empty lights folder, or biases alone: a session without darks, an error
        using TempWorkspace ws = new();
        Directory.CreateDirectory(ws.PathTo("lights"));
        File.WriteAllBytes(ws.PathTo("result.fit"), SessionFixture.Frame(SessionFixture.FirstStart, 10, 400));
        (int code, _, string stderr) = Cli.Run(ws.Dir, "--inventory");
        Assert.Equal(1, code);
        Assert.Contains("nu are darks", stderr, StringComparison.Ordinal);
        using TempWorkspace biasesOnly = new();
        Directory.CreateDirectory(biasesOnly.PathTo("biases"));
        (int code1, _, string stderr1) = Cli.Run(biasesOnly.Dir);
        Assert.Equal(1, code1);
        Assert.Contains("are biases\\ dar nu are darks\\", stderr1, StringComparison.Ordinal);

        // lights with frames: the inventory lists them, the analysis asks for a partner
        using SessionFixture s = new SessionFixture().WithLights(2, i => new SyntheticLight { Seed = 300 + i });
        (int code2, string stdout2, _) = Cli.Run(s.Root, "--inventory");
        Assert.Equal(0, code2);
        Assert.Contains("    darks              : (lipsa)", stdout2, StringComparison.Ordinal);
        Assert.Contains("    expunere darks     : (lipsa)", stdout2, StringComparison.Ordinal);
        Assert.Contains("    interval darks     : (lipsa)", stdout2, StringComparison.Ordinal);
        Assert.Contains("    potrivire          : NEMASURAT: nu exista darks", stdout2, StringComparison.Ordinal);
        Assert.Contains("\n  LIGHTS\n", stdout2, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  DARKS\n", stdout2, StringComparison.Ordinal);
        (int code3, _, string stderr3) = Cli.Run(s.Root);
        Assert.Equal(1, code3);
        Assert.Contains("EROARE: sesiunea " + s.Root + " are lights\\ dar nu are darks\\; da un partener de dark-uri (o biblioteca) ca sa compari lights-urile cu el: --against DIR in consola, \"Alege partener...\" in fereastra", stderr3, StringComparison.Ordinal);
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
        (int code, string stdout, _) = Cli.Run(s.Root, "--inventory");
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
        (int code, string stdout, _) = Cli.Run(s.Root, "--inventory");
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
        (int code, string stdout, string stderr) = Cli.Run(s.DarksDir, "--inventory");
        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("  1  DSC_0002.NEF          eroare:", stdout, StringComparison.Ordinal);
        Assert.Contains("darks              : . (1 cadru .NEF)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R025_TheDefaultRunPrintsTheDarksReportWithItsSectionsAndTable_Exit0()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticDark.OnePerLevel }, biases: 2);
        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--threads", "2");
        Assert.Equal(0, code);
        Assert.Contains("masurare dark-uri: 4 / 4", stderr, StringComparison.Ordinal);
        Assert.Contains("=== DARK-URI: cadre citite 4 din 4, 256 x 192 px, valori de la 200 (negru) la 65535 ADU (alb) (FITS pe 16 biti, albul dedus din fisier, negrul luat din bias-uri), filtre de culoare RGGB ===", stdout, StringComparison.Ordinal);
        foreach (string section in new[] { "  2. SCURGERI DE LUMINA SI DIFERENTE PE CADRU", "  3. INCALZIREA CAMEREI IN TIMPUL SERIEI", "  4. ZGOMOT PE CADRU", "  5. PIXELI FIERBINTI", "  6. MODEL FIX", "  7. PIXELI MORTI SI SATURATI", "  8. CATE DARK-URI AJUNG", "  TABEL PE CADRE" })
        {
            Assert.Contains(section + "\n", stdout, StringComparison.Ordinal);
        }

        Assert.Contains("master peste nivel : 10: 5; 20: 4; 50: 3; 100: 2; 500: 1  [ADU peste negru, mediana seriei, toate planurile]", stdout, StringComparison.Ordinal);
        Assert.Contains("verdict            : OK: niciun pixel mort sau saturat pe master", stdout, StringComparison.Ordinal);
        Assert.Contains("  1  DSC_0001.fit          00:48:53 ", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R025_ADarkThatCannotBeDecodedIsListedAndTheRunExits1()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i });
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0004.fit"), new byte[100]);
        (int code, string stdout, _) = Cli.Run(s.Root, "--threads", "2");
        Assert.Equal(1, code);
        Assert.Contains("=== DARK-URI: cadre citite 3 din 4", stdout, StringComparison.Ordinal);
        Assert.Contains("  4  DSC_0004.fit          eroare:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R026_OutWritesTheReportTheCsvFilesAndTheMaps()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i }, biases: 2);
        using TempWorkspace outDir = new();
        (int code, string stdout, _) = Cli.Run(s.Root, "--threads", "2", "--out", outDir.Dir);
        Assert.Equal(0, code);
        foreach (string name in new[] { "darkscheck-report.txt", "darkscheck-darks.csv", "darkscheck-biases.csv", "darkscheck-map-dark.png", "darkscheck-map-bias.png", "darkscheck-map-dark-minus-bias.png" })
        {
            Assert.Contains("-> scris: " + Path.Combine(outDir.Dir, name), stdout, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(outDir.Dir, name)), name);
        }

        string report = File.ReadAllText(Path.Combine(outDir.Dir, "darkscheck-report.txt")).ReplaceLineEndings("\n");
        Assert.Contains("  8. CATE DARK-URI AJUNG\n", report, StringComparison.Ordinal);
        string[] csv = File.ReadAllLines(Path.Combine(outDir.Dir, "darkscheck-darks.csv"));
        Assert.Equal(4, csv.Length);
        Assert.StartsWith("index,file,time,exposure_s,iso,error,skipped,mean_above_black,R_median,", csv[0], StringComparison.Ordinal);
        Assert.EndsWith(",amp_ratio_median,amp_ratio_q25,amp_ratio_q75,edge_left,edge_right,edge_top,edge_bottom,edge_top_left,edge_top_right,edge_bottom_left,edge_bottom_right,excess_light", csv[0], StringComparison.Ordinal);
        Assert.Equal(CsvFields(csv[0]), CsvFields(csv[1]));
        Assert.Equal(PngWriterSignature, File.ReadAllBytes(Path.Combine(outDir.Dir, "darkscheck-map-dark.png")).Take(8));
    }

    private static readonly byte[] PngWriterSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    [Fact]
    public void R026_OutInsideTheSessionIsRefused_Exit1()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(2, i => new SyntheticDark { Seed = 100 + i });
        foreach (string dir in new[] { s.Root, s.DarksDir, Path.Combine(s.Root, "rapoarte") })
        {
            (int code, string stdout, string stderr) = Cli.Run(s.Root, "--threads", "2", "--out", dir);
            Assert.Equal(1, code);
            Assert.Contains("EROARE: --out nu poate fi folderul sesiunii sau unul din el", stderr, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(s.Root, "rapoarte")));
            // refused before the analysis: no report was printed
            Assert.DoesNotContain("=== DARK-URI", stdout, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void R026_TheBiasesCsvHasOneRowPerBiasAndTheRawColumns()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(2, i => new SyntheticDark { Seed = 100 + i }, biases: 3);
        using TempWorkspace outDir = new();
        Cli.Run(s.Root, "--threads", "2", "--out", outDir.Dir);
        string[] csv = File.ReadAllLines(Path.Combine(outDir.Dir, "darkscheck-biases.csv"));
        Assert.Equal(4, csv.Length);
        Assert.StartsWith("index,file,time,exposure_s,iso,error,skipped,R_median,R_sigma,R_mean,R_p999,R_saturated,R_low,R_min,R_max,G1_median", csv[0], StringComparison.Ordinal);
        Assert.All(csv.Skip(1), row => Assert.Equal(CsvFields(csv[0]), CsvFields(row)));
    }

    /// <summary>Fields of a CSV row, a quoted field (an error text with commas) counting as one.</summary>
    private static int CsvFields(string row)
    {
        int n = 1;
        bool quoted = false;
        foreach (char c in row)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                n++;
            }
        }

        return n;
    }

    [Fact]
    public void R026_SkippedAndFailedFramesFillTheirCsvRowsAndTinyFramesGetNoMap()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i }, biases: 2);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0005.fit"), new byte[100]);
        using TempWorkspace outDir = new();
        (int code, _, _) = Cli.Run(s.Root, "--threads", "2", "--max-frames", "2", "--out", outDir.Dir);
        Assert.Equal(1, code);
        string[] csv = File.ReadAllLines(Path.Combine(outDir.Dir, "darkscheck-darks.csv"));
        int columns = CsvFields(csv[0]);
        Assert.All(csv.Skip(1), row => Assert.Equal(columns, CsvFields(row)));
        Assert.Contains(csv.Skip(1), row => row.Contains(",\"\",\"sarit (peste 2 cadre", StringComparison.Ordinal));

        using SessionFixture tiny = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i, Width = 24, Height = 16 });
        using TempWorkspace outTiny = new();
        (int code2, string stdout, _) = Cli.Run(tiny.Root, "--threads", "2", "--out", outTiny.Dir);
        Assert.Equal(0, code2);
        Assert.DoesNotContain("map-dark.png", stdout, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(outTiny.Dir, "*.png"));
    }

    [Fact]
    public void R027_SkippedFramesPrintTheirReasonWithoutErrorAndTheRunExits0()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 100 + i });
        (int code, string stdout, _) = Cli.Run(s.Root, "--threads", "2", "--max-frames", "2");
        Assert.Equal(0, code);
        Assert.Contains("=== DARK-URI: cadre citite 2 din 5", stdout, StringComparison.Ordinal);
        Assert.Contains("  2  DSC_0002.fit          sarit (peste 2 cadre, se decodeaza unul din 3)", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("eroare:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R041_UnusableBiasesPrintTheirAttentionLine_Exit1()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i }, biases: 2, bias: i => new SyntheticDark { Seed = 500 + i, Width = 128, Height = 96 });
        (int code, string stdout, _) = Cli.Run(s.Root, "--threads", "2");
        Assert.Equal(1, code);
        Assert.Contains("ATENTIE            : bias-urile nu s-au putut folosi: bias-urile au 128 x 96 px, dark-urile 256 x 192", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R025_WhenNoDarkDecodesTheErrorNamesTheFiles_Exit1()
    {
        using TempWorkspace ws = new();
        File.WriteAllBytes(ws.PathTo("DSC_0001.fit"), new byte[100]);
        File.WriteAllBytes(ws.PathTo("DSC_0002.fit"), new byte[100]);
        (int code, _, string stderr) = Cli.Run(ws.Dir, "--threads", "2");
        Assert.Equal(1, code);
        Assert.Contains("EROARE: niciun dark nu s-a putut decoda: DSC_0001.fit: FITS", stderr, StringComparison.Ordinal);
        Assert.Contains("DSC_0002.fit: FITS", stderr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--threads", "0")]
    [InlineData("--max-frames", "0")]
    [InlineData("--out", "")]
    public void R004_ABadOptionValueNamesTheOption_Exit1(string option, string value)
    {
        (int code, _, string stderr) = Cli.Run("folder", option, value);
        Assert.Equal(1, code);
        Assert.Contains(option, stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void R005_NumbersUseTheDotWhateverTheCulture()
    {
        using SessionFixture s = new SessionFixture().WithDarks(1, exposure: 0.5);
        (_, string stdout, _) = Cli.Run(s.Root, "--inventory");
        Assert.Contains("0.5 s la ISO 400", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("0,5", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R006_IntervalLineGivesSpanCadenceAndPauses()
    {
        using SessionFixture s = new SessionFixture().WithDarks(3, exposure: 119.2, cadence: 120);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0004.fit"), SessionFixture.Frame(SessionFixture.FirstStart.AddSeconds(600), 119.2, 400));
        (_, string stdout, _) = Cli.Run(s.Root, "--inventory");
        // Starts at 0, 120, 240 and 600 s; the last ends at 719.2 s (11 whole minutes); the pause before frame 4 is 600 - 359.2 = 241 s.
        Assert.Contains("interval darks     : 2026-08-13 00:48:53 -> 2026-08-13 01:00:52 (11 min); 4 cadre, cate unul la 120.0 s; 1 pauza peste 5 s (241 s inainte de cadrul 4)", stdout, StringComparison.Ordinal);
        Assert.Contains("  4  DSC_0004.fit          00:58:53     119.2    400   241 s", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R006_BackToBackDarksHaveNoLongPause()
    {
        using SessionFixture s = new SessionFixture().WithDarks(3, lights: 2, lightsCadence: 150);
        (_, string stdout, _) = Cli.Run(s.Root, "--inventory");
        // Three darks of 119.2 s started 120 s apart span 359.2 s: 5 whole minutes.
        Assert.Contains("interval darks     : 2026-08-13 00:48:53 -> 2026-08-13 00:54:52 (5 min); 3 cadre, cate unul la 120.0 s; fara pauze peste 5 s", stdout, StringComparison.Ordinal);
        Assert.Contains("interval lights    : 2026-08-12 23:43:53 -> 2026-08-12 23:48:22 (4 min); 2 cadre, cate unul la 150.0 s; 1 pauza peste 5 s (31 s inainte de cadrul 2)", stdout, StringComparison.Ordinal);
    }

    /// <summary>EXIF times have a 1 s granularity: a 120.2 s dark started 120 s after the previous one has no pause, not a negative one.</summary>
    [Fact]
    public void R006_APauseBelowZeroFromTheTimeGranularityIsZero()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, exposure: 120.2, cadence: 120);
        (_, string stdout, _) = Cli.Run(s.Root, "--inventory");
        Assert.Contains("  2  DSC_0002.fit          00:50:53     120.2    400     0 s", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("-0 s", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R006_SeveralLongPausesNameTheLongestOne()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, exposure: 119.2, cadence: 120);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0003.fit"), SessionFixture.Frame(SessionFixture.FirstStart.AddSeconds(300), 119.2, 400));
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0004.fit"), SessionFixture.Frame(SessionFixture.FirstStart.AddSeconds(600), 119.2, 400));
        (_, string stdout, _) = Cli.Run(s.Root, "--inventory");
        // Starts at 0, 120, 300 and 600 s (median interval 180 s); pauses 0.8 s, 300 - 239.2 = 61 s, 600 - 419.2 = 181 s.
        Assert.Contains("4 cadre, cate unul la 180.0 s; 2 pauze peste 5 s (cea mai lunga 181 s inainte de cadrul 4)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R006_FramesWithoutATimeOrAnExposureLeaveTheIntervalUnknown()
    {
        using TempWorkspace ws = new();
        File.WriteAllBytes(ws.PathTo("DSC_0001.fit"), SessionFixture.Frame(SessionFixture.FirstStart, null, 400));
        File.WriteAllBytes(ws.PathTo("DSC_0002.fit"), SessionFixture.Frame(null, 119.2, 400));
        (_, string stdout, _) = Cli.Run(ws.Dir, "--inventory");
        Assert.Contains("interval darks     : necunoscut (cadrele nu au ora sau expunerea)", stdout, StringComparison.Ordinal);
        Assert.Contains("  1  DSC_0001.fit          00:48:53         -    400       -", stdout, StringComparison.Ordinal);
        Assert.Contains("  2  DSC_0002.fit          -            119.2    400       -", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_WithoutExposuresOrIsoTheMatchIsUnmeasured()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, lights: 1);
        File.WriteAllBytes(Path.Combine(s.LightsDir, "light_0002.fit"), SessionFixture.Frame(SessionFixture.FirstStart, 119.2, null));
        File.Delete(Path.Combine(s.LightsDir, "light_0001.fit"));
        (_, string stdout, _) = Cli.Run(s.Root, "--inventory");
        Assert.Contains("expunere lights    : 119.2 s (toate cadrele)", stdout, StringComparison.Ordinal);
        Assert.Contains("potrivire          : NEMASURAT: cadrele nu spun expunerea sau ISO-ul", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_AGroupOfUnreadableFramesHasUnknownSettingsAndTheCountIsPlural()
    {
        using SessionFixture s = new SessionFixture().WithDarks(1);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0002.fit"), new byte[100]);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0003.fit"), new byte[100]);
        (int code, string stdout, _) = Cli.Run(s.Root, "--inventory");
        Assert.Equal(1, code);
        Assert.Contains("ATENTIE            : 2 cadre nu s-au putut citi (vezi tabelele)", stdout, StringComparison.Ordinal);
        Directory.CreateDirectory(s.BiasesDir);
        File.WriteAllBytes(Path.Combine(s.BiasesDir, "bias_0001.fit"), new byte[100]);
        (_, stdout, _) = Cli.Run(s.Root, "--inventory");
        Assert.Contains("expunere biases    : necunoscuta", stdout, StringComparison.Ordinal);
        Assert.Contains("bias               : NEMASURAT: cadrele nu spun expunerea", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_DarksThatMatchTheLightsAreOk()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, lights: 2, biases: 1);
        (_, string stdout, _) = Cli.Run(s.Root, "--inventory");
        Assert.Contains("potrivire          : OK: dark-urile au aceeasi expunere (119.2 s) si acelasi ISO (400) ca lights-urile", stdout, StringComparison.Ordinal);
        Assert.Contains("bias               : OK: 0.0002 s, destul de scurt pentru un bias", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_DarksAtAnotherIsoOrExposureAreAWarning()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, exposure: 120.2, iso: 1600, lights: 2, lightsExposure: 119.2, lightsIso: 400);
        (int code, string stdout, _) = Cli.Run(s.Root, "--inventory");
        Assert.Equal(0, code);
        Assert.Contains("potrivire          : ATENTIE: dark-urile (120.2 s, ISO 1600) nu au setarile lights-urilor (119.2 s, ISO 400)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_WithoutLightsTheMatchIsUnmeasuredAndTheBiasAbsent()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2);
        (_, string stdout, _) = Cli.Run(s.Root, "--inventory");
        Assert.Contains("potrivire          : NEMASURAT: nu exista lights", stdout, StringComparison.Ordinal);
        Assert.Contains("bias               : (lipsa)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_ABiasLongerThanASecondIsAWarning()
    {
        using SessionFixture s = new SessionFixture().WithDarks(1, biases: 2, biasExposure: 2);
        (_, string stdout, _) = Cli.Run(s.Root, "--inventory");
        Assert.Contains("bias               : ATENTIE: 2.0 s e prea lung pentru un bias (cel mult 1 s)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_AGroupWithMixedExposuresSaysSo()
    {
        using SessionFixture s = new SessionFixture().WithDarks(3);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0004.fit"), SessionFixture.Frame(SessionFixture.FirstStart.AddSeconds(360), 60, 400));
        (_, string stdout, _) = Cli.Run(s.Root, "--inventory");
        Assert.Contains("expunere darks     : 119.2 s la ISO 400 (3 din 4 cadre; celelalte au alta expunere sau alt ISO)", stdout, StringComparison.Ordinal);
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
