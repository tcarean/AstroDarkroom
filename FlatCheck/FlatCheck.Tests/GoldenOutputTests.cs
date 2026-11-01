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

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace FlatCheck.Tests;

/// <summary>
/// Whole-CLI characterization tests: they pin the exact console text and exit code of
/// every mode against a snapshot in Golden/.
///
/// These started life as the phase 0 safety net for the refactoring, but they are not
/// scaffolding and should not be deleted now that it is over. Their job today is twofold,
/// and neither half is covered by the unit tests:
///
///   - They pin the *output contract*. For this tool the console text isn't a side effect
///     of the product, it IS the product - there is no API, no GUI, no result file, only
///     30 lines of Romanian and an exit code. Wording, line order, column formatting and
///     what goes to stderr are all user-visible behaviour, and nothing else checks them.
///
///   - They are the only end-to-end test of the numbers. The unit tests check Percentile,
///     FitCenter and LargeScalePtp in isolation; only these check that the whole chain
///     Load -> Prepare -> RadialModel -> residual -> verdict still produces 0.30 % on a
///     clean radial field.
///
/// They remain deliberately dumb - they assert nothing about *why* a number is right,
/// only that it has not moved. That is the point: the analyzer tests say what is correct,
/// these say what changed.
///
/// To re-approve after an intentional change:  FLATCHECK_UPDATE_GOLDEN=1 dotnet test
/// Do that only when you meant to change the output, and then read the git diff on
/// Golden/*.txt. Re-approving without reading it is the one way to make these worthless -
/// all of their value is in that diff, which is where you find the numbers you did *not*
/// mean to change.
///
/// Keep the set small enough to stay readable: add a golden file only for a genuinely
/// distinct behaviour, and use Assert.Contains for "this one line must appear".
///
/// Exit codes: 0 OK, 1 error, 2 SUSPECT, 3 PROBLEMA.
/// </summary>
public class GoldenOutputTests
{
    internal const int Size = 512;

    static string GoldenDir([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "Golden");

    internal static void AssertGolden(string name, string actual)
    {
        string path = Path.Combine(GoldenDir(), name + ".txt");
        actual = actual.Replace("\r\n", "\n");

        if (Environment.GetEnvironmentVariable("FLATCHECK_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(GoldenDir());
            File.WriteAllText(path, actual, new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(path),
            "Golden file missing: " + path + "\nRe-run with FLATCHECK_UPDATE_GOLDEN=1 to create it.");
        string expected = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Runs the CLI in-process.
    ///
    /// Two things are scrubbed before comparing, for the same reason: they change without
    /// the *behaviour* changing, and would otherwise fail every golden file for nothing.
    ///
    ///   - absolute paths, echoed into almost every header line, which differ per machine
    ///   - the version banner, which changes on every release
    ///
    /// Scrubbing the banner loses no coverage: VersionTests asserts its exact content and
    /// its position on line 1, for every mode. And if it vanished entirely these tests
    /// would still fail, because the golden files carry a placeholder line that the output
    /// would no longer produce.
    /// </summary>
    internal static (string Output, int Exit) RunCli(TempWorkspace ws, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        int exit = FlatCheckCli.Run(args, stdout, stderr);

        string text = stdout.ToString();
        string err = stderr.ToString();
        if (err.Length > 0) text += "[stderr]\n" + err;

        text = text.Replace(ws.Dir + Path.DirectorySeparatorChar, "<TMP>/")
                   .Replace(ws.Dir, "<TMP>");
        text = ScrubVersion(text);
        return (text, exit);
    }

    internal const string VersionPlaceholder = "flatcheck <VERSIUNE>";

    // \r? because StringWriter writes CRLF on Windows, and '$' in .NET's Multiline mode
    // matches only before '\n' - it does not skip '\r'. Without it, the regex catches
    // nothing and the scrub silently becomes a no-op.
    static readonly Regex VersionBanner =
        new(@"^flatcheck \d+\.\d+\.\d+\r?$", RegexOptions.Multiline | RegexOptions.Compiled);

    static string ScrubVersion(string text) => VersionBanner.Replace(text, VersionPlaceholder);

    internal static string Flat(TempWorkspace ws, string name, Func<int, int, double> scene, int size = Size) =>
        ws.WriteFile(name, FitsTestData.BuildFits(size, size, scene));

    // ---------------------------------------------------------------- structure

    [Fact]
    public void R011_Structure_OnPerfectRadialFlat_IsClean()
    {
        using var ws = new TempWorkspace();
        string f = Flat(ws, "clean.fit", FitsTestData.Radial(Size, Size));

        var (output, exit) = RunCli(ws, f, "-o", ws.PathTo("out.png"));

        Assert.Equal(0, exit);
        Assert.Contains("VERDICT: PERFECT", output);
        AssertGolden("structure_clean", output);
    }

    /// <summary>
    /// A 12% corner spread lands in the 10-20% band: usable, but confirm with --light
    /// before a hard stretch. Under the old three-level ladder this was PROBLEMA.
    /// </summary>
    [Fact]
    public void Structure_OnLinearGradient_IsUsableButNeedsConfirming()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.12);
        string f = Flat(ws, "grad.fit", scene);

        var (output, exit) = RunCli(ws, f, "-o", ws.PathTo("out.png"));

        Assert.Equal(2, exit);
        Assert.Contains("VERDICT: ACCEPTABIL", output);
        Assert.Contains("confirma cu testul End-to-end (--light)", output);
        AssertGolden("structure_gradient", output);
    }

    /// <summary>A frame far outside the scale must land in the reshoot band.</summary>
    [Fact]
    public void Structure_OnAViolentGradient_SaysReshoot()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.60);
        string f = Flat(ws, "violent.fit", scene);

        var (output, exit) = RunCli(ws, f, "-o", ws.PathTo("out.png"));

        Assert.Equal(3, exit);
        Assert.Contains("VERDICT: REFA", output);
        AssertGolden("structure_reshoot", output);
    }

    /// <summary>The scale itself is printed, so the numbers can be read without the docs.</summary>
    [Fact]
    public void R012_Structure_PrintsTheScaleAndNamesTheMostImportantMetric()
    {
        using var ws = new TempWorkspace();
        string f = Flat(ws, "clean.fit", FitsTestData.Radial(Size, Size));

        var (output, _) = RunCli(ws, f, "-o", ws.PathTo("out.png"));

        Assert.Contains("SCARA celor cinci teste de abatere de mai sus", output);
        Assert.Contains("sub 2 %", output);
        Assert.Contains("2 - 10 %", output);
        Assert.Contains("10 - 20 %", output);
        Assert.Contains("peste 20 %", output);
        Assert.Contains("reziduu dupa model rad. :  0.29 %   [PERFECT]  <- CEL MAI IMPORTANT", output);
        Assert.Contains("pata maxima (o dala)    :", output);
        Assert.Contains("Cel mai important e \"reziduu dupa model rad.\"", output);
    }

    /// <summary>
    /// The illumination-centre offset is reported but carries no verdict, and sits under
    /// the informative heading rather than under TESTE. It stopped being a test in 1.2.0:
    /// it is a gradient-over-curvature ratio, so it is not scale-invariant, and past the
    /// point where the vertex leaves the frame it is not even monotonic. What measures
    /// the same defect as an amplitude is "inclinare", which is a test.
    /// </summary>
    [Fact]
    public void R013_Structure_ReportsTheCentroidOffsetAsInformativeOnly()
    {
        using var ws = new TempWorkspace();
        string f = Flat(ws, "clean.fit", FitsTestData.Radial(Size, Size));

        var (output, _) = RunCli(ws, f, "-o", ws.PathTo("out.png"));

        Assert.Contains("INFORMATIV (nu sunt teste)", output);
        Assert.Contains("offset centru iluminare :  0.19 %", output);
        Assert.DoesNotContain("offset centru iluminare :  0.19 %   [", output);
        Assert.Contains("inclinare (gradient)    :", output);
    }

    /// <summary>
    /// The note under the offset says what the user sees once the illumination peak leaves the
    /// frame: the row reads nedeterminat / undetermined (ADR 0014). It used to say the figure
    /// falls back toward 0, which is what the offset did before that guard, and never since.
    /// </summary>
    [Fact]
    public void R013_Structure_OffsetNoteSaysAPeakOutsideTheFrameIsUndetermined()
    {
        using var ws = new TempWorkspace();
        string f = Flat(ws, "clean.fit", FitsTestData.Radial(Size, Size));

        var (ro, _) = RunCli(ws, f, "-o", ws.PathTo("ro.png"));
        var (en, _) = RunCli(ws, f, "--lang", "en", "-o", ws.PathTo("en.png"));

        Assert.Contains("iar cand punctul cel mai luminos iese din cadru e", ro);
        Assert.DoesNotContain("spre 0", ro);
        Assert.Contains("and once the brightest point leaves the frame it reads", en);
        Assert.DoesNotContain("toward 0", en);
    }

    // ---------------------------------------------------------------- light

    [Fact]
    public void R030_Light_OnUncorrectedVignetting_ReportsUnderCorrection()
    {
        using var ws = new TempWorkspace();
        string f = Flat(ws, "light.fit", FitsTestData.Radial(Size, Size));

        var (output, exit) = RunCli(ws, f, "--light", "-o", ws.PathTo("out.png"));

        Assert.Equal(3, exit);
        Assert.Contains("flat-ul nu a scos toata vignetarea", output);
        AssertGolden("light_undercorrected", output);
    }

    /// <summary>
    /// The applicability guard: on a field of ~70 degrees the mode refuses to answer, and
    /// exits with 1 - unsuitable input, not a verdict. Exactly the case that motivated it:
    /// 20 mm on APS-C confidently reported "SUSPECT, SUB-corectie" on a flat without a
    /// radial flaw.
    /// </summary>
    [Fact]
    public void R031_Light_OnATooWideField_RefusesInsteadOfAnswering()
    {
        using var ws = new TempWorkspace();
        string f = ws.WriteFile("light.fit", FitsTestData.BuildFits(
            Size, Size, FitsTestData.Radial(Size, Size),
            extraCards: FitsTestData.WcsCards(0.1)));   // 512x512, diag 724 px, 0.1 deg/px -> 72.4 deg

        var (output, exit) = RunCli(ws, f, "--light", "-o", ws.PathTo("out.png"));

        Assert.Equal(1, exit);
        Assert.Contains("TESTUL NU SE APLICA", output);
        Assert.DoesNotContain("componenta radiala", output);
        AssertGolden("light_rejected_wide_field", output);
    }

    // ---------------------------------------------------------------- light on a series

    /// <summary>
    /// Tight series: all frames show the same under-correction, the sky gradient does not
    /// vary, so the mean IS a measurement and gets a true verdict.
    /// </summary>
    [Fact]
    public void R032_LightSeries_OnAStableSeries_GivesARealVerdict()
    {
        using var ws = new TempWorkspace();
        string dir = SeriesDir(ws, "stabil", Enumerable.Range(0, 6)
            .Select(i => (Func<int, int, double>)FitsTestData.Radial(Size, Size, k: 0.44 + 0.002 * i))
            .ToArray());

        var (output, exit) = RunCli(ws, dir, "--light", "--fov", "10", "-o", ws.PathTo("out.png"));

        Assert.Contains("VERDICT:", output);
        Assert.DoesNotContain("NEDETERMINAT", output);
        Assert.Equal(3, exit);                 // large under-correction, but MEASURED
        AssertGolden("light_series_conclusive", output);
    }

    /// <summary>
    /// Sweeping series: the radial component wanders a lot and follows the sky gradient.
    /// The flat is the same in all frames by construction, so what moves is the sky - and
    /// the mode refuses to give a verdict, with exit 2.
    /// </summary>
    [Fact]
    public void R032_LightSeries_OnASweepingSeries_RefusesToConclude()
    {
        using var ws = new TempWorkspace();
        // k rises from under- to over-correction, and the sky plane falls with it: exactly
        // the pattern of the real session, in which the two proved correlated at -0.94
        string dir = SeriesDir(ws, "baleiaj", Enumerable.Range(0, 6)
            .Select(i => (Func<int, int, double>)FitsTestData.WithLinearGradient(
                TestImages.InvertedRadial(Size, Size, k: -0.5 + 0.2 * i), Size, 0.6 - 0.1 * i))
            .ToArray());

        var (output, exit) = RunCli(ws, dir, "--light", "--fov", "10", "-o", ws.PathTo("out.png"));

        Assert.Equal(2, exit);
        Assert.Contains("VERDICT: NEDETERMINAT", output);
        AssertGolden("light_series_undetermined", output);
    }

    [Fact]
    public void LightSeries_OnAFolderWithNoMatches_IsAnError()
    {
        using var ws = new TempWorkspace();
        string dir = Path.Combine(ws.Dir, "gol");
        Directory.CreateDirectory(dir);

        var (output, exit) = RunCli(ws, dir, "--light");

        Assert.Equal(1, exit);
        Assert.Contains("--pattern", output);
    }

    /// <summary>A folder has no place in the other modes; it is said, not
    /// attempted.</summary>
    [Fact]
    public void ADirectoryWithAnyOtherMode_IsAnError()
    {
        using var ws = new TempWorkspace();

        var (output, exit) = RunCli(ws, ws.Dir, "--expo");

        Assert.Equal(1, exit);
        Assert.Contains("doar cu --light", output);
    }

    /// <summary>Writes a series' frames into a subfolder, with Siril's names.</summary>
    internal static string SeriesDir(TempWorkspace ws, string name, params Func<int, int, double>[] scenes)
    {
        string dir = Path.Combine(ws.Dir, name);
        Directory.CreateDirectory(dir);
        for (int i = 0; i < scenes.Length; i++)
            File.WriteAllBytes(Path.Combine(dir, $"pp_light_{i + 1:00000}.fit"),
                FitsTestData.BuildFits(Size, Size, scenes[i]));
        return dir;
    }

    // ---------------------------------------------------------------- histeq

    /// <summary>
    /// No verdict, exit 0, a PNG of the display size beside the report. The golden file
    /// pins the table and the caveat; the numbers in it are input ranges, so they move
    /// only if the fixture does.
    /// </summary>
    [Fact]
    public void R050_Histeq_OnACfaFlat_WritesThePictureAndExitsZero()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000));
        string f = ws.WriteFile("raw_bayerpat.fit", FitsTestData.BuildFits(
            Size, Size, scene, extraCards: FitsTestData.BayerCards("RGGB")));
        string png = ws.PathTo("out.png");

        var (output, exit) = RunCli(ws, f, "--histeq", "-o", png);

        Assert.Equal(0, exit);
        Assert.Contains("CFA (ordine RGGB, din BAYERPAT)", output);
        Assert.Contains("nu da verdict", output);
        var d = PngDecoder.Decode(File.ReadAllBytes(png));
        Assert.Equal((Size / 2, Size / 2), (d.Width, d.Height));
        AssertGolden("histeq_cfa", output);
    }

    [Fact]
    public void R050_Histeq_OnAMonoFlat_ReportsOneChannel()
    {
        using var ws = new TempWorkspace();
        string f = Flat(ws, "flat.fit", FitsTestData.Radial(Size, Size));

        var (output, exit) = RunCli(ws, f, "--histeq", "-o", ws.PathTo("out.png"));

        Assert.Equal(0, exit);
        Assert.Contains("mono/procesat", output);
        AssertGolden("histeq_mono", output);
    }

    /// <summary>
    /// --rainbow writes the Mosaic view in Siril's false colours: the file is the channels'
    /// size, every pixel is a level of the table (one channel at 255, one at 0, since
    /// saturation and value are 1), and the report carries the display line and the legend.
    /// </summary>
    [Fact]
    public void R104_Histeq_Rainbow_WritesTheColouredMosaic()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000));
        string f = ws.WriteFile("raw_bayerpat.fit", FitsTestData.BuildFits(
            Size, Size, scene, extraCards: FitsTestData.BayerCards("RGGB")));
        string png = ws.PathTo("out.png");

        var (output, exit) = RunCli(ws, f, "--histeq", "--rainbow", "-o", png);

        Assert.Equal(0, exit);
        Assert.Contains("culori false", output);
        Assert.Contains("magenta", output);
        var d = PngDecoder.Decode(File.ReadAllBytes(png));
        Assert.Equal((Size / 2, Size / 2), (d.Width, d.Height));
        var (r, g, b) = d.At(Size / 4, Size / 4);
        Assert.Equal(255, Math.Max(r, Math.Max(g, b)));
        Assert.Equal(0, Math.Min(r, Math.Min(g, b)));
        AssertGolden("histeq_rainbow", output);
    }

    /// <summary>The folder check runs before dispatch, so it holds for the new flag too.</summary>
    [Fact]
    public void Histeq_OnAFolder_IsRefusedLikeEveryModeButLight()
    {
        using var ws = new TempWorkspace();

        var (output, exit) = RunCli(ws, ws.Dir, "--histeq");

        Assert.Equal(1, exit);
        Assert.Contains("un folder se poate da doar cu --light", output);
    }

    /// <summary>--histeq wins over --expo: dispatch is first-match, and it comes first.</summary>
    [Fact]
    public void R003_Histeq_TakesPrecedenceOverExpo()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000));
        string f = Flat(ws, "raw.fit", scene);

        var (output, exit) = RunCli(ws, f, "--expo", "--histeq", "-o", ws.PathTo("out.png"));

        Assert.Equal(0, exit);
        Assert.Contains("EGALIZARE DE HISTOGRAMA", output);
        Assert.DoesNotContain("VERDICT", output);
    }

    // ---------------------------------------------------------------- expo

    [Fact]
    public void Expo_OnRawCfaFlat_ReportsLevel()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000));
        string f = Flat(ws, "raw.fit", scene);

        var (output, exit) = RunCli(ws, f, "--expo");

        Assert.Equal(0, exit);
        Assert.Contains("cel mai luminos", output);
        AssertGolden("expo_cfa", output);
    }

    /// <summary>
    /// Same frame, plus the card Siril writes when the file came through Conversion. The
    /// only line that moves is the channel listing, which now carries the colours - which
    /// is why no other golden had to be re-approved for this feature.
    /// </summary>
    [Fact]
    public void Expo_WithABayerPattern_NamesTheChannels()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000));
        string f = ws.WriteFile("raw_bayerpat.fit", FitsTestData.BuildFits(
            Size, Size, scene, extraCards: FitsTestData.BayerCards("RGGB")));

        var (output, exit) = RunCli(ws, f, "--expo");

        Assert.Equal(0, exit);
        Assert.Contains("ordinea R/G/B din BAYERPAT", output);
        Assert.Contains("G1 (CFA[0,1]):", output);
        AssertGolden("expo_cfa_bayerpat", output);
    }

    /// <summary>
    /// The same frame with the header Siril really writes - ROWORDER 'BOTTOM-UP' on an
    /// even height - names [1,0] as R and [0,0] as a green. The numbers do not move (the
    /// medians are per position, whatever they are called), so this pins only the two
    /// labels; the golden file above stays the reference for the figures.
    /// </summary>
    [Fact]
    public void R044_Expo_OnABottomUpFrame_NamesTheChannelsByFileRowOrder()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000));
        string f = ws.WriteFile("raw_bayerpat_bottomup.fit", FitsTestData.BuildFits(
            Size, Size, scene, extraCards: FitsTestData.BayerCards("RGGB", rowOrder: "BOTTOM-UP")));

        var (output, exit) = RunCli(ws, f, "--expo");

        Assert.Equal(0, exit);
        Assert.Contains("G1 (CFA[0,0]):", output);
        Assert.Contains("R  (CFA[1,0]):", output);
    }

    [Fact]
    public void Expo_WithSky_UsesSkyThresholds()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000));
        string f = Flat(ws, "raw.fit", scene);

        var (output, exit) = RunCli(ws, f, "--expo", "--sky");

        Assert.Equal(2, exit);   // "cam sus, dar acceptabil"
        Assert.Contains("mod --sky", output);
        AssertGolden("expo_cfa_sky", output);
    }

    [Fact]
    public void R042_Expo_OnDebayeredFrame_RefusesToGiveAVerdict()
    {
        using var ws = new TempWorkspace();
        string f = Flat(ws, "debayered.fit", FitsTestData.Radial(Size, Size));

        var (output, exit) = RunCli(ws, f, "--expo");

        Assert.Equal(1, exit);   // the file is not what the mode needs
        Assert.Contains("NU PARE UN FLAT BRUT", output);
        Assert.DoesNotContain("VERDICT", output);
        AssertGolden("expo_rejects_debayered", output);
    }

    [Fact]
    public void Expo_WithExplicitSatAndBlack_RescalesThePercentages()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 3600));
        string f = Flat(ws, "raw12bit.fit", scene);

        var (output, exit) = RunCli(ws, f, "--expo", "--sat", "4095", "--black", "150");

        Assert.Equal(3, exit);   // PREA SUS
        Assert.Contains("--sat=4095", output);
        AssertGolden("expo_manual_scale", output);
    }

    // ---------------------------------------------------------------- help

    [Fact]
    public void Help_IsPrintedForNoArgs()
    {
        using var ws = new TempWorkspace();
        var (output, exit) = RunCli(ws);

        Assert.Equal(0, exit);
        AssertGolden("help", output);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Help_IsPrintedForHelpFlags(string flag)
    {
        using var ws = new TempWorkspace();
        var (output, exit) = RunCli(ws, flag);

        Assert.Equal(0, exit);
        AssertGolden("help", output);
    }

    // ---------------------------------------------------------------- errors

    [Fact]
    public void MissingFile_ExitsWith1AndWritesToStderr()
    {
        using var ws = new TempWorkspace();
        var (output, exit) = RunCli(ws, ws.PathTo("nope.fit"));

        Assert.Equal(1, exit);
        Assert.Contains("[stderr]", output);
        Assert.Contains("EROARE:", output);
    }

    /// <summary>Phase 8: a flag before the path now behaves exactly like one after it.</summary>
    [Fact]
    public void R003_FlagBeforePath_GivesTheSameResultAsFlagAfterPath()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000));
        string f = Flat(ws, "raw.fit", scene);

        var before = RunCli(ws, "--expo", f);
        var after = RunCli(ws, f, "--expo");

        Assert.Equal(after.Exit, before.Exit);
        Assert.Equal(after.Output, before.Output);
        Assert.Equal(0, before.Exit);
    }

    [Fact]
    public void ABadArgument_ExitsWith1AndExplainsItself()
    {
        using var ws = new TempWorkspace();
        string f = Flat(ws, "raw.fit", FitsTestData.Radial(Size, Size));

        var (output, exit) = RunCli(ws, f, "--nope");

        Assert.Equal(1, exit);
        Assert.Contains("optiune necunoscuta", output);
    }

    /// <summary>
    /// Exit codes exist so the tool can be used from a script. Verdict severity is
    /// monotonic, so "anything non-zero" means "not clean, go look".
    /// </summary>
    [Theory]
    [InlineData("OK", 0)]
    [InlineData("SUSPECT", 2)]
    [InlineData("PROBLEMA", 3)]
    public void R002_ExitCodesFollowVerdictSeverity(string verdict, int expected)
    {
        Assert.Equal(expected, FlatCheckCli.ExitFor(verdict));
    }

    // ---------------------------------------------------------------- synthetic / against

    internal static string Reference(TempWorkspace ws, string name = "ref.fit", double k = 0.45) =>
        ws.WriteFile(name, FitsTestData.BuildFits(240, 160, FitsTestData.Radial(240, 160, k: k),
            extraCards: new[] { FitsTestData.Card("FOCALLEN", "20.0"), FitsTestData.Card("INSTRUME", "'Nikon D3300'"), FitsTestData.Card("BAYERPAT", "'RGGB'") }));

    /// <summary>
    /// Structure with --lens: the five tests as always, then the master's vignetting against
    /// every matching profile on the camera's mount (the Nikon Adobe file and the lensfun
    /// entry - not the Canon file), the verdict of the nearest one, and the combined verdict.
    /// The fixture is made deeper than the model (k = 0.6: 1.12 stops against 0.66-0.85): BUN
    /// on shape (the half-pixel corner floor of a 240x160 raster, known issue 5), REFA on
    /// depth, exit 3 - the tablet case, the one the five tests cannot see, with the advice
    /// reading the plus sign as a source that falls off with angle.
    /// </summary>
    [Fact]
    public void R016_Structure_WithALens_ComparesTheVignettingWithTheProfilesAndTheVerdictFollows()
    {
        using var ws = new TempWorkspace();
        string profiles = LensProfileTestData.ProfilesDir(ws);
        string reference = Reference(ws, k: 0.6);

        var (output, exit) = RunCli(ws, reference, "--lens", "Tokina", "--fstop", "2.8", "--profiles", profiles, "-o", ws.PathTo("out.png"));

        AssertGolden("structure_with_profiles", output);
        Assert.Equal(3, exit);
        Assert.Contains("forma     : BUN", output);
        Assert.Contains("adancimea : REFA", output);
        Assert.Contains("VERDICT: REFA", output);
        Assert.Contains("MAI MULT", output);
    }

    /// <summary>
    /// The same without an f-stop: nothing to choose a profile with, so the block says so and
    /// the verdict and exit code are the five tests' own.
    /// </summary>
    [Fact]
    public void R017_Structure_WithALensButNoFStop_ExplainsAndKeepsTheFiveTestVerdict()
    {
        using var ws = new TempWorkspace();
        string profiles = LensProfileTestData.ProfilesDir(ws);
        string reference = Reference(ws);

        var (output, exit) = RunCli(ws, reference, "--lens", "Tokina", "--profiles", profiles, "-o", ws.PathTo("out.png"));

        AssertGolden("structure_lens_no_fstop", output);
        Assert.Equal(0, exit);
        Assert.Contains("nu se poate compara", output);
        Assert.Contains("VERDICT: PERFECT", output);
    }

    /// <summary>
    /// The whole synthetic report: which profile, requested vs used, the depth table, the
    /// caveat, the saved line. The lens is named exactly, the mount comes from INSTRUME.
    /// </summary>
    [Fact]
    public void R071_Synthetic_WritesTheFlatAndReportsTheProfileItUsed()
    {
        using var ws = new TempWorkspace();
        string profiles = LensProfileTestData.ProfilesDir(ws);
        string reference = Reference(ws);
        string outPath = ws.PathTo("sintetic.fit");

        var (output, exit) = RunCli(ws, reference, "--synthetic", outPath, "--lens", "Tokina AT-X 11-20mm F2.8 PRO DX",
            "--fstop", "2.8", "--profiles", profiles);

        AssertGolden("synthetic_written", output);
        Assert.Equal(0, exit);
        Assert.True(File.Exists(outPath));
        var (img, hdr, cards, _, bitpix) = FitsReader.LoadFull(File.ReadAllBytes(outPath));
        Assert.Equal(-32, bitpix);
        Assert.Equal("RGGB", FitsReader.HStr(hdr, "BAYERPAT", ""));
        Assert.InRange(img.At(80, 120), 0.99, 1.0);
    }

    /// <summary>
    /// lensfun needs the camera's crop; here it comes from the database by INSTRUME, and the corner
    /// is extrapolated.
    /// </summary>
    [Fact]
    public void R072_Synthetic_FromLensfun_TakesTheCropFromTheDatabase()
    {
        using var ws = new TempWorkspace();
        string profiles = LensProfileTestData.ProfilesDir(ws);
        string reference = Reference(ws);

        var (output, exit) = RunCli(ws, reference, "--synthetic", ws.PathTo("s.fit"), "--lens", "Aspherical",
            "--source", "lensfun", "--fstop", "2.8", "--profiles", profiles);

        AssertGolden("synthetic_lensfun", output);
        Assert.Equal(0, exit);
    }

    /// <summary>
    /// A request far from every profile (500 mm f/1.0 on a lens with 18-20 mm, f/2.8-4) is
    /// not refused: the nearest profile is used, the file is written, exit 0 - and an
    /// ATENTIE line under "profil folosit" says the depth is the profile's, not the request's.
    /// </summary>
    [Fact]
    public void R072_Synthetic_ARequestFarFromEveryProfileIsWrittenWithAWarning()
    {
        using var ws = new TempWorkspace();
        string profiles = LensProfileTestData.ProfilesDir(ws);
        string reference = Reference(ws);

        var (output, exit) = RunCli(ws, reference, "--synthetic", ws.PathTo("s.fit"), "--lens", "Tokina AT-X 11-20mm F2.8 PRO DX",
            "--focal", "500", "--fstop", "1.0", "--profiles", profiles);

        AssertGolden("synthetic_far_request", output);
        Assert.Equal(0, exit);
        Assert.True(File.Exists(ws.PathTo("s.fit")));
    }

    /// <summary>
    /// Two lenses match: nothing is written, the candidates are listed, exit 2 - "read this first".
    /// </summary>
    [Fact]
    public void R071_Synthetic_AnAmbiguousLensListsTheCandidatesAndExitsTwo()
    {
        using var ws = new TempWorkspace();
        string profiles = LensProfileTestData.ProfilesDir(ws);
        string reference = Reference(ws);
        string outPath = ws.PathTo("s.fit");

        var (output, exit) = RunCli(ws, reference, "--synthetic", outPath, "--lens", "11-20", "--fstop", "2.8", "--profiles", profiles);

        AssertGolden("synthetic_ambiguous", output);
        Assert.Equal(2, exit);
        Assert.False(File.Exists(outPath));
    }

    /// <summary>
    /// A FITS reference carries no f-number: without --fstop the mode refuses with 1.
    /// </summary>
    [Fact]
    public void R071_Synthetic_WithoutAnFStopRefusesWithOne()
    {
        using var ws = new TempWorkspace();
        string profiles = LensProfileTestData.ProfilesDir(ws);
        string reference = Reference(ws);

        var (output, exit) = RunCli(ws, reference, "--synthetic", ws.PathTo("s.fit"), "--lens", "Tokina AT-X 11-20mm F2.8 PRO DX", "--profiles", profiles);

        Assert.Equal(1, exit);
        Assert.Contains("--fstop", output);
    }

    [Fact]
    public void R070_Lenses_ListsBothSourcesWithTheirFocalsAndApertures()
    {
        using var ws = new TempWorkspace();
        string profiles = LensProfileTestData.ProfilesDir(ws);

        var (output, exit) = RunCli(ws, "--lenses", "--lens", "tokina", "--profiles", profiles);

        AssertGolden("lenses", output);
        Assert.Equal(0, exit);
    }

    /// <summary>
    /// R-115: a lensfun/ folder the user may not list does not fail the console: the list comes
    /// with the Adobe half, exit 0, and the lensfun line says the folder could not be read.
    /// Before, every mode that locates the profiles printed ERROR and exited 1 (2026-10-30).
    /// </summary>
    [Fact]
    public void R115_Lenses_AnUnlistableLensfunFolderIsSaidSo_AndTheListStillComes()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var ws = new TempWorkspace();
        string profiles = LensProfileTestData.ProfilesDir(ws);
        string lensfun = Path.Combine(profiles, "lensfun");
        using var deny = UnlistableFolder.Deny(lensfun);

        var (output, exit) = RunCli(ws, "--lenses", "--lens", "tokina", "--profiles", profiles, "--lang", "en");

        Assert.Equal(0, exit);
        Assert.Contains("Adobe  : 2 lenses from 2 files", output);
        Assert.Contains("lensfun: 0 lenses from <TMP>/profiles" + Path.DirectorySeparatorChar + "lensfun - the folder could not be read", output);   // RunCli scrubs the workspace path
        Assert.DoesNotContain("ERROR", output);
    }

    /// <summary>
    /// A flat against a deeper copy of itself: the ratio is radial (the shape is PERFECT) and the
    /// vignetting line reads the depth difference - 0.30 stops, which is 23 % in the corner of a
    /// calibrated light, REFA on the depth and therefore on the whole, exit 3.
    /// </summary>
    [Fact]
    public void R073_Against_ReadsTheDifferenceBetweenTwoFlats()
    {
        using var ws = new TempWorkspace();
        string a = ws.WriteFile("a.fit", FitsTestData.BuildFits(Size, Size, FitsTestData.Radial(Size, Size, k: 0.45)));
        string b = ws.WriteFile("b.fit", FitsTestData.BuildFits(Size, Size, FitsTestData.Radial(Size, Size, k: 0.30)));

        var (output, exit) = RunCli(ws, a, "--against", b, "-o", ws.PathTo("out.png"));

        AssertGolden("against_radial", output);
        Assert.Equal(3, exit);
        Assert.True(File.Exists(ws.PathTo("out.png")));
    }
}
