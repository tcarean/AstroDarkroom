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

using System.IO;
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
///     Load -> Prepare -> RadialModel -> residual -> verdict still produces 0.29 % on a
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
    const int Size = 512;

    static string GoldenDir([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "Golden");

    static void AssertGolden(string name, string actual)
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
    static (string Output, int Exit) RunCli(TempWorkspace ws, params string[] args)
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

    // \r? pentru ca StringWriter scrie CRLF pe Windows, iar '$' in modul Multiline din
    // .NET se potriveste doar inainte de '\n' - nu sare peste '\r'. Fara el, regexul nu
    // prinde nimic si scrub-ul devine tacut un no-op.
    static readonly Regex VersionBanner =
        new(@"^flatcheck \d+\.\d+\.\d+\r?$", RegexOptions.Multiline | RegexOptions.Compiled);

    static string ScrubVersion(string text) => VersionBanner.Replace(text, VersionPlaceholder);

    static string Flat(TempWorkspace ws, string name, Func<int, int, double> scene, int size = Size) =>
        ws.WriteFile(name, FitsTestData.BuildFits(size, size, scene));

    // ---------------------------------------------------------------- structure

    [Fact]
    public void Structure_OnPerfectRadialFlat_IsClean()
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
        Assert.Contains("confirma cu --light", output);
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
    public void Structure_PrintsTheScaleAndNamesTheMostImportantMetric()
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
    public void Structure_ReportsTheCentroidOffsetAsInformativeOnly()
    {
        using var ws = new TempWorkspace();
        string f = Flat(ws, "clean.fit", FitsTestData.Radial(Size, Size));

        var (output, _) = RunCli(ws, f, "-o", ws.PathTo("out.png"));

        Assert.Contains("INFORMATIV (nu sunt teste)", output);
        Assert.Contains("offset centru iluminare :  0.19 %", output);
        Assert.DoesNotContain("offset centru iluminare :  0.19 %   [", output);
        Assert.Contains("inclinare (gradient)    :", output);
    }

    // ---------------------------------------------------------------- ratio

    /// <summary>
    /// The defining pair: a gradient that INVERTS between the two orientations is fixed
    /// in the world, so it lands in the informative component and the combined master
    /// comes back clean. This is the case the mode exists for.
    /// </summary>
    [Fact]
    public void Ratio_WhenTheGradientInverts_ItIsAttributedToTheWorld()
    {
        using var ws = new TempWorkspace();
        string a = Flat(ws, "a.fit", FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, +0.12));
        string b = Flat(ws, "b.fit", FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, -0.12));

        var (output, exit) = RunCli(ws, a, "--ratio", b, "-o", ws.PathTo("out.png"));

        Assert.Equal(0, exit);
        Assert.Contains("VERDICT: PERFECT", output);
        AssertGolden("ratio_world", output);
    }

    /// <summary>
    /// The mirror, and the case the mode used to be blind to: the same gradient in both
    /// orientations does not invert, so it belongs to the camera. The world component
    /// must read zero and the gradient must survive into the verdict.
    /// </summary>
    [Fact]
    public void Ratio_WhenTheGradientIsInBothOrientations_ItStaysWithTheCamera()
    {
        using var ws = new TempWorkspace();
        var grad = FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.12);
        string a = Flat(ws, "a.fit", grad);
        string b = Flat(ws, "b.fit", grad);

        var (output, exit) = RunCli(ws, a, "--ratio", b, "-o", ws.PathTo("out.png"));

        Assert.Contains("gradient extern / orientare :  0.00 % +/- 0.00 %", output);
        Assert.Contains("inclinare (gradient)    :", output);
        AssertGolden("ratio_camera", output);
    }

    /// <summary>The second map, for the shape of the external component.</summary>
    [Fact]
    public void Ratio_WritesBothMaps()
    {
        using var ws = new TempWorkspace();
        string a = Flat(ws, "a.fit", FitsTestData.Radial(Size, Size));
        string b = Flat(ws, "b.fit", FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.12));

        RunCli(ws, a, "--ratio", b, "-o", ws.PathTo("out.png"));

        Assert.True(File.Exists(ws.PathTo("out.png")));
        Assert.True(File.Exists(ws.PathTo("out-lume.png")));
    }

    [Fact]
    public void Ratio_OnMismatchedSizes_FailsWithExitCode1()
    {
        using var ws = new TempWorkspace();
        string a = Flat(ws, "a.fit", FitsTestData.Radial(Size, Size), Size);
        string b = Flat(ws, "b.fit", FitsTestData.Radial(256, 256), 256);

        var (output, exit) = RunCli(ws, a, "--ratio", b, "-o", ws.PathTo("out.png"));

        Assert.Equal(1, exit);
        Assert.Contains("dimensiuni diferite", output);
    }

    // ---------------------------------------------------------------- light

    [Fact]
    public void Light_OnUncorrectedVignetting_ReportsUnderCorrection()
    {
        using var ws = new TempWorkspace();
        string f = Flat(ws, "light.fit", FitsTestData.Radial(Size, Size));

        var (output, exit) = RunCli(ws, f, "--light", "-o", ws.PathTo("out.png"));

        Assert.Equal(3, exit);
        Assert.Contains("SUB-corectie", output);
        AssertGolden("light_undercorrected", output);
    }

    /// <summary>
    /// Garda de aplicabilitate: pe un camp de ~70 de grade modul refuza sa raspunda, si
    /// iese cu 1 - intrare nepotrivita, nu verdict. Exact cazul care a motivat-o: 20 mm
    /// pe APS-C raporta increzator "SUSPECT, SUB-corectie" pe un flat fara cusur radial.
    /// </summary>
    [Fact]
    public void Light_OnATooWideField_RefusesInsteadOfAnswering()
    {
        using var ws = new TempWorkspace();
        string f = ws.WriteFile("light.fit", FitsTestData.BuildFits(
            Size, Size, FitsTestData.Radial(Size, Size),
            extraCards: FitsTestData.WcsCards(0.1)));   // 512x512, diag 724 px, 0.1 gr/px -> 72.4 gr

        var (output, exit) = RunCli(ws, f, "--light", "-o", ws.PathTo("out.png"));

        Assert.Equal(1, exit);
        Assert.Contains("TESTUL NU SE APLICA", output);
        Assert.DoesNotContain("componenta radiala", output);
        AssertGolden("light_rejected_wide_field", output);
    }

    // ---------------------------------------------------------------- light pe serie

    /// <summary>
    /// Serie stransa: toate cadrele arata aceeasi sub-corectie, gradientul de cer nu
    /// variaza, deci media E o masuratoare si primeste un verdict adevarat.
    /// </summary>
    [Fact]
    public void LightSeries_OnAStableSeries_GivesARealVerdict()
    {
        using var ws = new TempWorkspace();
        string dir = SeriesDir(ws, "stabil", Enumerable.Range(0, 6)
            .Select(i => (Func<int, int, double>)FitsTestData.Radial(Size, Size, k: 0.44 + 0.002 * i))
            .ToArray());

        var (output, exit) = RunCli(ws, dir, "--light", "--fov", "10", "-o", ws.PathTo("out.png"));

        Assert.Contains("VERDICT:", output);
        Assert.DoesNotContain("NEDETERMINAT", output);
        Assert.Equal(3, exit);                 // sub-corectie mare, dar MASURATA
        AssertGolden("light_series_conclusive", output);
    }

    /// <summary>
    /// Serie care baleiaza: componenta radiala se plimba mult si urmareste gradientul de
    /// cer. Flat-ul e acelasi in toate cadrele prin constructie, deci ce se misca e cerul
    /// - iar modul refuza sa dea un verdict, cu exit 2.
    /// </summary>
    [Fact]
    public void LightSeries_OnASweepingSeries_RefusesToConclude()
    {
        using var ws = new TempWorkspace();
        // k urca de la sub- la supra-corectie, iar planul de cer scade odata cu el: exact
        // tiparul sesiunii reale, in care cele doua s-au dovedit corelate la -0.94
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

    /// <summary>Un folder nu are ce cauta la celelalte moduri; se spune, nu se incearca.</summary>
    [Fact]
    public void ADirectoryWithAnyOtherMode_IsAnError()
    {
        using var ws = new TempWorkspace();

        var (output, exit) = RunCli(ws, ws.Dir, "--expo");

        Assert.Equal(1, exit);
        Assert.Contains("doar cu --light", output);
    }

    /// <summary>Scrie cadrele unei serii intr-un subfolder, cu numele Siril.</summary>
    static string SeriesDir(TempWorkspace ws, string name, params Func<int, int, double>[] scenes)
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
    public void Histeq_OnACfaFlat_WritesThePictureAndExitsZero()
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
    public void Histeq_OnAMonoFlat_ReportsOneChannel()
    {
        using var ws = new TempWorkspace();
        string f = Flat(ws, "flat.fit", FitsTestData.Radial(Size, Size));

        var (output, exit) = RunCli(ws, f, "--histeq", "-o", ws.PathTo("out.png"));

        Assert.Equal(0, exit);
        Assert.Contains("mono/procesat", output);
        AssertGolden("histeq_mono", output);
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
    public void Histeq_TakesPrecedenceOverExpo()
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
    public void Expo_OnDebayeredFrame_RefusesToGiveAVerdict()
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
    public void FlagBeforePath_GivesTheSameResultAsFlagAfterPath()
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
    public void ExitCodesFollowVerdictSeverity(string verdict, int expected)
    {
        Assert.Equal(expected, FlatCheckCli.ExitFor(verdict));
    }
}
