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

namespace FlatCheck.Tests;

/// <summary>
/// The --lang option and the language scope (R-100, R-102). The assembly runs in Romanian
/// (<see cref="RomanianByDefault"/>); every English case here opens its own scope.
/// </summary>
public class LanguageTests
{
    [Theory]
    [InlineData(new[] { "--lang", "ro", "f.fit" }, "ro")]
    [InlineData(new[] { "f.fit", "--lang=ro" }, "ro")]
    [InlineData(new[] { "--LANG=RO", "f.fit" }, "ro")]
    [InlineData(new[] { "f.fit", "--lang", "EN" }, "en")]
    [InlineData(new[] { "--lang", "ro", "f.fit", "--lang=en" }, "en")]
    public void R100_TheLanguageIsTakenOutWhereverItIsAndTheLastOneCounts(string[] args, string expected)
    {
        var lang = LanguageArgument.Extract(args);

        Assert.Equal(expected == "ro" ? UiLanguage.Romanian : UiLanguage.English, lang.Language);
        Assert.True(lang.Given);
        Assert.Null(lang.BadValue);
        Assert.Equal(new[] { "f.fit" }, lang.Rest);
    }

    [Fact]
    public void R100_TheOtherArgumentsKeepTheirOrder()
    {
        var lang = LanguageArgument.Extract(new[] { "a.fit", "--lang", "ro", "--ratio", "b.fit", "-o", "x.png" });

        Assert.Equal(new[] { "a.fit", "--ratio", "b.fit", "-o", "x.png" }, lang.Rest);
    }

    [Fact]
    public void R100_WithNoLangTheLanguageIsTheStartupOneEnglish()
    {
        var lang = LanguageArgument.Extract(new[] { "f.fit" });

        Assert.False(lang.Given);
        Assert.Null(lang.Language);
        Assert.Null(lang.BadValue);
        Assert.Equal(UiLanguage.English, Ui.StartupDefault);
    }

    [Fact]
    public void R100_AScopePicksItsWordingAndRestoresTheOneBefore()
    {
        Assert.Equal("ro", Ui.T("en", "ro"));
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("en", Ui.T("en", "ro"));
            using (Ui.Use(UiLanguage.Romanian))
                Assert.Equal("ro", Ui.T("en", "ro"));
            Assert.Equal("en", Ui.T("en", "ro"));
        }
        Assert.Equal("ro", Ui.T("en", "ro"));
    }

    [Fact]
    public async Task R100_ATaskStartedInAScopeKeepsItsLanguage()
    {
        using (Ui.Use(UiLanguage.English))
            Assert.Equal("en", await Task.Run(() => Ui.T("en", "ro")));
    }

    [Fact]
    public void R100_TheUsageIsWordedInTheLanguageAsked()
    {
        using var ws = new TempWorkspace();

        var (en, enExit) = GoldenOutputTests.RunCli(ws, "--lang", "en");
        var (ro, roExit) = GoldenOutputTests.RunCli(ws, "--lang=ro", "--help");

        Assert.Equal((0, 0), (enExit, roExit));
        Assert.Contains("Usage:", en);
        Assert.Contains("--lang en|ro", en);
        Assert.Contains("Utilizare:", ro);
        Assert.DoesNotContain("Utilizare:", en);
    }

    [Fact]
    public void R100_AParseErrorIsWordedInTheLanguageAsked()
    {
        using var ws = new TempWorkspace();

        var (en, _) = GoldenOutputTests.RunCli(ws, "--lang", "en", "f.fit", "--nope");
        var (ro, _) = GoldenOutputTests.RunCli(ws, "--lang", "ro", "f.fit", "--nope");

        Assert.Contains("ERROR: unknown option: --nope", en);
        Assert.Contains("EROARE: optiune necunoscuta: --nope", ro);
    }

    /// <summary>A --lang and its value alone leave no input file: the usage, as for an
    /// empty command line.</summary>
    [Fact]
    public void R100_ALanguageAloneIsNotAnInputFile()
    {
        using var ws = new TempWorkspace();

        var (output, exit) = GoldenOutputTests.RunCli(ws, "--lang", "ro");

        Assert.Equal(0, exit);
        Assert.Contains("Utilizare:", output);
        Assert.DoesNotContain("[stderr]", output);
    }

    [Theory]
    [InlineData(new[] { "f.fit", "--lang", "fr" }, "ERROR: --lang expects en or ro, got 'fr'")]
    [InlineData(new[] { "f.fit", "--lang=" }, "ERROR: --lang expects a language, en or ro, and got nothing")]
    [InlineData(new[] { "f.fit", "--lang" }, "ERROR: --lang expects a language, en or ro, and got nothing")]
    public void R102_ALanguageNotUnderstoodStopsTheRunInEnglish(string[] args, string expected)
    {
        using var ws = new TempWorkspace();

        var (output, exit) = GoldenOutputTests.RunCli(ws, args);

        Assert.Equal(1, exit);
        Assert.Contains("[stderr]\n" + expected, output.Replace("\r\n", "\n"));
        Assert.StartsWith(GoldenOutputTests.VersionPlaceholder, output);
    }
}
