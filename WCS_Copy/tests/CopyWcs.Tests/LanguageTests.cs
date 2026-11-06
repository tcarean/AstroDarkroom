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

using System.Text.RegularExpressions;

namespace CopyWcs.Tests;

/// <summary>
/// The interface language (R-020 to R-023): how <c>--lang</c> is read, English when it is
/// absent, a value not understood refused, and every scenario of the console in English with
/// the Romanian run's exit code and figures and no Romanian word.
/// </summary>
public class LanguageTests
{
    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CopyWcsCli.Run(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    [Theory]
    [InlineData("--lang", "ro")]
    [InlineData("--lang=ro")]
    [InlineData("--LANG", "RO")]
    [InlineData("--lang=Ro")]
    public void R020_Extract_SpacedAndEqualsForms_InAnyCase_NameRomanian(params string[] form)
    {
        var args = new List<string> { "a.fit" };
        args.AddRange(form);
        args.Add("b.fit");

        var lang = LanguageArgument.Extract(args);

        Assert.Equal(UiLanguage.Romanian, lang.Language);
        Assert.True(lang.Given);
        Assert.Null(lang.BadValue);
        Assert.Equal(["a.fit", "b.fit"], lang.Rest);
    }

    [Fact]
    public void R020_Extract_TheLastLangCounts_AndTheValueIsNeverAPath()
    {
        var lang = LanguageArgument.Extract(["--lang", "ro", "src.fit", "--lang", "en", "dest.fit"]);

        Assert.Equal(UiLanguage.English, lang.Language);
        Assert.Equal(["src.fit", "dest.fit"], lang.Rest);

        // The spaced form takes the next argument as its value, whatever it is.
        var swallowed = LanguageArgument.Extract(["--lang", "src.fit", "dest.fit"]);
        Assert.Null(swallowed.Language);
        Assert.Equal("src.fit", swallowed.BadValue);
        Assert.Equal(["dest.fit"], swallowed.Rest);
    }

    [Fact]
    public void R020_Extract_WithoutLang_GivesNoLanguageAndEveryArgument()
    {
        var lang = LanguageArgument.Extract(["src.fit", "dest.fit", "--dry-run"]);

        Assert.Null(lang.Language);
        Assert.False(lang.Given);
        Assert.Null(lang.BadValue);
        Assert.Equal(["src.fit", "dest.fit", "--dry-run"], lang.Rest);
    }

    [Fact]
    public void R021_WithoutLang_TheTextsAreEnglish()
    {
        // The production default; the test assembly itself runs in Romanian (RomanianByDefault).
        Assert.Equal(UiLanguage.English, Ui.StartupDefault);

        using (Ui.Use(UiLanguage.English))
        {
            var help = Run("--help");

            Assert.Equal(0, help.ExitCode);
            Assert.Contains("copywcs <source.fit> <destination.fit> [options]", help.Stdout, StringComparison.Ordinal);
            Assert.Contains("--lang en|ro", help.Stdout, StringComparison.Ordinal);
            Assert.Contains("ABSOLUTELY NO WARRANTY", help.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("optiuni", help.Stdout, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void R021_LangRo_GivesTheRomanianUsageWithItsLangLine()
    {
        using (Ui.Use(UiLanguage.English))
        {
            var help = Run("--help", "--lang", "ro");

            Assert.Equal(0, help.ExitCode);
            Assert.Contains("copywcs <sursa.fit> <destinatie.fit> [optiuni]", help.Stdout, StringComparison.Ordinal);
            Assert.Contains("--lang en|ro       Limba textelor", help.Stdout, StringComparison.Ordinal);
            Assert.Contains("FARA NICIO GARANTIE", help.Stdout, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("xx", "--lang expects en or ro, got 'xx'")]
    [InlineData("", "--lang expects a language, en or ro, and got nothing")]
    public void R022_BadLangValue_PrintsTheBannerThenAnEnglishErrorAndExitsOne(string value, string message)
    {
        string[] args = value.Length == 0 ? ["a.fit", "b.fit", "--lang"] : ["a.fit", "b.fit", "--lang", value];

        var result = Run(args);

        Assert.Equal(1, result.ExitCode);
        var lines = result.Stdout.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Assert.Equal([VersionInfo.Banner, LicenseInfo.Copyright], lines);
        Assert.Equal("ERROR: " + message, result.Stderr.Trim());
    }

    /// <summary>The Romanian words the English output of any scenario must not contain.</summary>
    private static readonly string[] RomanianWords =
    [
        "ATENTIE", "Nu pot", "Chei care", "Copiate", "Dimensiuni", "Nicio cheie", "Eroare",
        "sursa", "destinatie", "Sunt necesare", "Optiune", "Verifica vizual", "optiuni",
        "Fisier", "trunchiat", "nu pare FITS", "nu incepe", "Chei vechi", "Eliminate", "eliminate",
        "destinatiei",
    ];

    public static IEnumerable<object[]> Scenarios()
    {
        yield return ["help"];
        yield return ["bad-args"];
        yield return ["unknown-option"];
        yield return ["source-missing"];
        yield return ["dest-missing"];
        yield return ["source-not-fits"];
        yield return ["source-truncated"];
        yield return ["mismatch"];
        yield return ["mismatch-forced"];
        yield return ["no-ctype1"];
        yield return ["dry-run"];
        yield return ["no-keys"];
        yield return ["write-error"];
        yield return ["copy"];
        yield return ["drop-dry-run"];
        yield return ["drop"];
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void R023_EveryScenarioInEnglish_HasTheRomanianExitCodeAndFiguresAndNoRomanianWord(string scenario)
    {
        using var ro = new TempWorkspace();
        using var en = new TempWorkspace();
        var inRomanian = RunScenario(scenario, ro, "ro");
        var inEnglish = RunScenario(scenario, en, "en");

        Assert.Equal(inRomanian.ExitCode, inEnglish.ExitCode);
        Assert.Equal(Figures(inRomanian.Stdout + inRomanian.Stderr, ro.Dir), Figures(inEnglish.Stdout + inEnglish.Stderr, en.Dir));
        var english = inEnglish.Stdout + inEnglish.Stderr;
        foreach (var word in RomanianWords)
        {
            Assert.DoesNotContain(word, english, StringComparison.Ordinal);
        }
    }

    /// <summary>Every run of digits in a text, the temp folder's own path left out.</summary>
    private static string[] Figures(string text, string folder)
        => Regex.Matches(text.Replace(folder, "", StringComparison.Ordinal), @"\d+").Select(m => m.Value).ToArray();

    private static (int ExitCode, string Stdout, string Stderr) RunScenario(string scenario, TempWorkspace ws, string lang)
    {
        var data = FitsTestData.DeterministicData(400);
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));
        var wide = ws.WriteFile("wide.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(200, 80), data));
        var unsolved = ws.WriteFile("unsolved.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));
        var noCtype = FitsTestData.PlateSolvedCards();
        noCtype.RemoveAll(c => c.StartsWith("CTYPE1", StringComparison.Ordinal));
        var noCtypePath = ws.WriteFile("noctype.fit", FitsTestData.MinimalFitsFile(noCtype, data));
        // A header whose first card is not SIMPLE (R-015), and a file cut inside its first block.
        var notFits = ws.WriteFile("notfits.fit", FitsTestData.MinimalFitsFile([FitsTestData.Card("BITPIX", "16"), FitsTestData.Card("NAXIS", "0")]));
        var truncated = ws.WriteFile("truncated.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data)[..100]);
        // A destination that still holds an old plate solve in another form (R-016).
        var oldSolve = ws.WriteFile("oldsolve.fit", FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards(), data));

        string[] args = scenario switch
        {
            "help" => ["--help"],
            "bad-args" => ["only-one.fit"],
            "unknown-option" => [source, dest, "--bogus"],
            "source-missing" => [ws.FilePath("missing.fit"), dest],
            "dest-missing" => [source, ws.FilePath("missing.fit")],
            "source-not-fits" => [notFits, dest],
            "source-truncated" => [truncated, dest],
            "mismatch" => [source, wide],
            "mismatch-forced" => [source, wide, "--force", "-o", ws.FilePath("forced.fit")],
            "no-ctype1" => [noCtypePath, dest, "--dry-run"],
            "dry-run" => [source, dest, "--dry-run"],
            "no-keys" => [unsolved, dest, "--no-optical"],
            "write-error" => [source, dest, "-o", Path.Combine(ws.FilePath("no-such-folder"), "out.fit")],
            "copy" => [source, dest, "-o", ws.FilePath("out.fit")],
            "drop-dry-run" => [source, oldSolve, "--dry-run"],
            "drop" => [source, oldSolve, "-o", ws.FilePath("out.fit")],
            _ => throw new ArgumentException(scenario),
        };

        return Run([.. args, "--lang", lang]);
    }
}
