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

using DarksCheck.Core;

namespace DarksCheck.Tests;

/// <summary>
/// The <c>--lang</c> option (ADR 0021): its forms, its default and its wrong values. This assembly
/// runs in Romanian (<see cref="RomanianByDefault"/>), so a test of English names it.
/// </summary>
public class LanguageTests
{
    [Theory]
    [InlineData(new[] { "--lang", "en" }, true)]
    [InlineData(new[] { "--lang=ro" }, false)]
    [InlineData(new[] { "--LANG=RO" }, false)]
    [InlineData(new[] { "--lang", "Ro" }, false)]
    [InlineData(new[] { "--lang=ro", "--lang", "en" }, true)]
    [InlineData(new[] { "--lang", "en", "--lang=ro" }, false)]
    public void R090_LangIsReadInEitherFormAndTheLastCounts(string[] args, bool english)
    {
        LanguageArgument lang = LanguageArgument.Extract(args);

        Assert.Equal(english ? UiLanguage.English : UiLanguage.Romanian, lang.Language);
        Assert.True(lang.Given);
        Assert.Null(lang.BadValue);
        Assert.Empty(lang.Rest);
    }

    [Fact]
    public void R090_LangAndItsValueAreTakenOutWhereverTheyStand()
    {
        LanguageArgument lang = LanguageArgument.Extract(["sess", "--lang", "ro", "--out", "outdir", "--lang=en", "--threads", "2"]);

        Assert.Equal(UiLanguage.English, lang.Language);
        Assert.Equal(["sess", "--out", "outdir", "--threads", "2"], lang.Rest);
    }

    [Fact]
    public void R090_TheSpacedFormTakesTheNextArgumentEvenWhenItLooksLikeAFolder()
    {
        LanguageArgument lang = LanguageArgument.Extract(["--lang", "sess"]);

        Assert.Null(lang.Language);
        Assert.Equal("sess", lang.BadValue);
        Assert.Empty(lang.Rest);
    }

    [Fact]
    public void R090_TheConsoleSpeaksTheLanguageItIsGiven()
    {
        (int en, string enOut, _) = Cli.Run("--help", "--lang", "en");
        (int ro, string roOut, _) = Cli.Run("--lang=RO", "--help");

        Assert.Equal(0, en);
        Assert.Equal(0, ro);
        Assert.Contains("Usage: darkscheck", enOut, StringComparison.Ordinal);
        Assert.Contains("Folosire: darkscheck", roOut, StringComparison.Ordinal);
    }

    [Fact]
    public void R090_AnOptionErrorIsWordedInTheLanguageGiven()
    {
        (int code, _, string stderr) = Cli.Run("sess", "--threads", "0", "--lang", "en");

        Assert.Equal(1, code);
        Assert.Equal("ERROR: --threads expects a number of threads, at least 1\n", stderr);
    }

    [Fact]
    public void R091_WithoutLangTheConsoleIsEnglish()
    {
        LanguageArgument lang = LanguageArgument.Extract(["sess"]);
        Assert.Null(lang.Language);
        Assert.False(lang.Given);
        Assert.Null(lang.BadValue);

        // This assembly set Romanian as its default, so the console's own default is seen only in a
        // process of its own: darkscheck.dll, built beside the tests, run without --lang.
        string dll = Path.Combine(AppContext.BaseDirectory, "darkscheck.dll");
        System.Diagnostics.ProcessStartInfo start = new("dotnet") { RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add(dll);
        start.ArgumentList.Add("--help");
        using System.Diagnostics.Process p = System.Diagnostics.Process.Start(start)!;
        string stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit();

        Assert.Equal(0, p.ExitCode);
        Assert.Contains("Usage: darkscheck", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Folosire", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R091_WithoutLangTheConsoleKeepsTheLanguageItRunsIn()
    {
        // Without --lang the console sets no language of its own: here the assembly's Romanian.
        (int code, string stdout, _) = Cli.Run("--help");

        Assert.Equal(0, code);
        Assert.Contains("Folosire: darkscheck", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R091_AScopeIsInheritedByTheTasksItStartsAndRestoredOnDispose()
    {
        UiLanguage before = Ui.Current;
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("en", await Task.Run(() => Ui.T("en", "ro")));
            using (Ui.Use(UiLanguage.Romanian))
            {
                Assert.Equal("ro", Ui.T("en", "ro"));
            }

            Assert.Equal("en", Ui.T("en", "ro"));
        }

        Assert.Equal(before, Ui.Current);
    }

    [Theory]
    [InlineData(new[] { "sess", "--lang", "fr" }, "ERROR: --lang expects en or ro, got 'fr'\n")]
    [InlineData(new[] { "--lang=", "--help" }, "ERROR: --lang expects a language, en or ro, and got nothing\n")]
    [InlineData(new[] { "sess", "--lang" }, "ERROR: --lang expects a language, en or ro, and got nothing\n")]
    public void R092_AWrongLanguageStopsTheConsoleInEnglish(string[] args, string expected)
    {
        (int code, string stdout, string stderr) = Cli.Run(args);

        Assert.Equal(1, code);
        Assert.Equal(expected, stderr);
        Assert.Equal(2, stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.StartsWith("darkscheck ", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void R095_ARomanianCountOfTwentyAndUpTakesDe()
    {
        // the rule's edges: from 20 up, unless the last two digits are 01 to 19; a figure with decimals takes none
        Assert.Equal("0 ", Ui.RoCount(0));
        Assert.Equal("1 ", Ui.RoCount(1));
        Assert.Equal("19 ", Ui.RoCount(19));
        Assert.Equal("20 de ", Ui.RoCount(20));
        Assert.Equal("99 de ", Ui.RoCount(99));
        Assert.Equal("100 de ", Ui.RoCount(100));
        Assert.Equal("101 ", Ui.RoCount(101));
        Assert.Equal("119 ", Ui.RoCount(119));
        Assert.Equal("120 de ", Ui.RoCount(120));
        Assert.Equal("1019 ", Ui.RoCount(1019));
        Assert.Equal("3933 de ", Ui.RoCount(3933));
        Assert.Equal("6 ", Ui.RoCount("6"));
        Assert.Equal("-20 de ", Ui.RoCount("-20"));
        Assert.Equal("20.5 ", Ui.RoCount("20.5"));
        Assert.Equal("2.0 ", Ui.RoCount("2.0"));

        // a session of 20 darks: the inventory and the report count them with "de"; the English has no such word
        using SessionFixture s = new SessionFixture().WithDarkSeries(20, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticDark.OnePerLevel });
        (_, string ro, _) = Cli.Run(s.Root, "--threads", "2", "--lang", "ro");
        (_, string en, _) = Cli.Run(s.Root, "--threads", "2", "--lang", "en");

        Assert.Contains("darks\\ (20 de cadre .fit)", ro, StringComparison.Ordinal);
        Assert.Contains(", media a 20 de cadre, pe patrate de ", ro, StringComparison.Ordinal);
        Assert.Contains("masterul dark, facut din 20 de cadre, mai are ", ro, StringComparison.Ordinal);
        Assert.Contains("darks\\ (20 frames .fit)", en, StringComparison.Ordinal);
        Assert.Contains(", mean of 20 frames, in squares of ", en, StringComparison.Ordinal);
        Assert.DoesNotContain(" de ", en, StringComparison.Ordinal);
    }
}
