using System.Runtime.CompilerServices;
using System.Text;

namespace FlatCheck.Tests;

/// <summary>
/// Phase 0 safety net: whole-CLI characterization tests. These pin the exact console
/// output of every mode so that the refactoring phases cannot silently change a number.
///
/// They are deliberately dumb - they assert nothing about *why* a number is right, only
/// that it has not moved. Real assertions on the numbers arrive with the analyzer split
/// in Phase 6. Until then, this is the thing standing between a refactor and a
/// regression.
///
/// To re-approve after an intentional change:  FLATCHECK_UPDATE_GOLDEN=1 dotnet test
/// Do that only when you meant to change the output, and read the resulting diff.
///
/// Exit codes (phase 8): 0 OK, 1 error, 2 SUSPECT, 3 PROBLEMA.
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
    /// Runs the CLI in-process. Absolute paths are scrubbed, because the input path is
    /// echoed into almost every header line and would otherwise make the golden files
    /// machine-specific.
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
        return (text, exit);
    }

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
        Assert.Contains("VERDICT: OK", output);
        AssertGolden("structure_clean", output);
    }

    [Fact]
    public void Structure_OnLinearGradient_ReportsProblem()
    {
        using var ws = new TempWorkspace();
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.12);
        string f = Flat(ws, "grad.fit", scene);

        var (output, exit) = RunCli(ws, f, "-o", ws.PathTo("out.png"));

        Assert.Equal(3, exit);
        Assert.Contains("VERDICT: PROBLEMA", output);
        AssertGolden("structure_gradient", output);
    }

    // ---------------------------------------------------------------- ratio

    [Fact]
    public void Ratio_OfAFrameAgainstItself_IsClean()
    {
        using var ws = new TempWorkspace();
        string a = Flat(ws, "a.fit", FitsTestData.Radial(Size, Size));
        string b = Flat(ws, "b.fit", FitsTestData.Radial(Size, Size));

        var (output, exit) = RunCli(ws, a, "--ratio", b, "-o", ws.PathTo("out.png"));

        Assert.Equal(0, exit);
        Assert.Contains("VERDICT: OK", output);
        AssertGolden("ratio_identical", output);
    }

    [Fact]
    public void Ratio_CleanAgainstGradient_ReportsProblem()
    {
        using var ws = new TempWorkspace();
        string a = Flat(ws, "a.fit", FitsTestData.Radial(Size, Size));
        var grad = FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.12);
        string b = Flat(ws, "b.fit", grad);

        var (output, exit) = RunCli(ws, a, "--ratio", b, "-o", ws.PathTo("out.png"));

        Assert.Equal(3, exit);
        Assert.Contains("VERDICT: PROBLEMA", output);
        AssertGolden("ratio_gradient", output);
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
