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

using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

public class StartupArgumentsTests
{
    [Fact]
    public void R100_WithoutLang_TheLanguageIsEnglish_AndEveryArgumentIsAPath()
    {
        StartupArguments parsed = StartupArguments.Parse(["A.png", "B.png"]);

        Assert.Equal(UiLanguage.English, parsed.Language);
        Assert.Null(parsed.UnknownLanguage);
        Assert.Equal(new[] { "A.png", "B.png" }, parsed.Paths);
    }

    [Fact]
    public void R100_NoArgumentsAtAll_IsEnglishWithNoPath()
    {
        StartupArguments parsed = StartupArguments.Parse([]);

        Assert.Equal(UiLanguage.English, parsed.Language);
        Assert.Null(parsed.UnknownLanguage);
        Assert.Empty(parsed.Paths);
    }

    [Theory]
    [InlineData("--lang=ro", "A.png", "B.png")]
    [InlineData("A.png", "--lang=ro", "B.png")]
    [InlineData("A.png", "B.png", "--lang=ro")]
    public void R100_LangEquals_ChoosesTheLanguage_AnywhereAmongThePaths(string first, string second, string third)
    {
        StartupArguments parsed = StartupArguments.Parse([first, second, third]);

        Assert.Equal(UiLanguage.Romanian, parsed.Language);
        Assert.Null(parsed.UnknownLanguage);
        Assert.Equal(new[] { "A.png", "B.png" }, parsed.Paths);
    }

    [Fact]
    public void R100_LangSpace_TakesTheNextArgumentAsItsValue_NeverAsAPath()
    {
        StartupArguments parsed = StartupArguments.Parse(["A.png", "--lang", "ro", "B.png"]);

        Assert.Equal(UiLanguage.Romanian, parsed.Language);
        Assert.Equal(new[] { "A.png", "B.png" }, parsed.Paths);
    }

    [Theory]
    [InlineData("--LANG=RO")]
    [InlineData("--Lang=Ro")]
    public void R100_TheNameAndTheValue_AreCaseInsensitive(string option)
    {
        Assert.Equal(UiLanguage.Romanian, StartupArguments.Parse([option]).Language);
        Assert.Equal(UiLanguage.Romanian, StartupArguments.Parse(["--LANG", "RO"]).Language);
    }

    [Fact]
    public void R100_EnIsEnglish_AndTheLastLangCounts()
    {
        Assert.Equal(UiLanguage.English, StartupArguments.Parse(["--lang=en"]).Language);
        Assert.Equal(UiLanguage.English, StartupArguments.Parse(["--lang=ro", "--lang=en"]).Language);
        Assert.Equal(UiLanguage.Romanian, StartupArguments.Parse(["--lang=en", "--lang", "ro"]).Language);
        Assert.Null(StartupArguments.Parse(["--lang=de", "--lang=ro"]).UnknownLanguage);
    }

    [Fact]
    public void R102_AnUnknownValue_IsEnglish_AndIsKeptToBeReported()
    {
        StartupArguments parsed = StartupArguments.Parse(["--lang=de", "A.png", "B.png"]);

        Assert.Equal(UiLanguage.English, parsed.Language);
        Assert.Equal("de", parsed.UnknownLanguage);
        Assert.Equal(new[] { "A.png", "B.png" }, parsed.Paths);
    }

    [Theory]
    [InlineData("--lang=", "A.png", "B.png")]
    [InlineData("A.png", "B.png", "--lang")]
    public void R102_ALangWithNoValue_IsEnglish_AndIsReportedAsEmpty(string first, string second, string third)
    {
        StartupArguments parsed = StartupArguments.Parse([first, second, third]);

        Assert.Equal(UiLanguage.English, parsed.Language);
        Assert.Equal(string.Empty, parsed.UnknownLanguage);
        Assert.Equal(new[] { "A.png", "B.png" }, parsed.Paths);
    }

    [Fact]
    public void R102_TheMessage_NamesTheValue_AndTheTwoLanguages()
    {
        Assert.Equal("Unknown language", UiText.UnknownLanguageTitle);
        Assert.Equal(
            "The language \"de\" is not one this program has, so the interface is in English." +
            Environment.NewLine + Environment.NewLine +
            "Use --lang=en for English or --lang=ro for Romanian.",
            UiText.UnknownLanguage("de"));
        Assert.Equal(
            "--lang was given without a language, so the interface is in English." +
            Environment.NewLine + Environment.NewLine +
            "Use --lang=en for English or --lang=ro for Romanian.",
            UiText.UnknownLanguage(string.Empty));
    }
}
