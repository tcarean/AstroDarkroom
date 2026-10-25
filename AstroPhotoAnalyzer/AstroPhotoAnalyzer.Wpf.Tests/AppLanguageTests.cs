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

using AstroPhotoAnalyzer.Tests;

namespace AstroPhotoAnalyzer.Wpf.Tests;

/// <summary>
/// The window's command line (ADR 0048): the language it starts in, the message for a wrong
/// --lang, and the file argument, which never takes --lang or its value.
/// </summary>
public class AppLanguageTests
{
    [Fact]
    public void R151_WithoutLangTheWindowStartsInEnglish()
    {
        Assert.Equal(UiLanguage.English, App.LanguageFromArgs(new[] { "poza.tif" }, out string error));
        Assert.Null(error);
        Assert.Equal(UiLanguage.English, App.LanguageFromArgs(null, out _));
    }

    [Fact]
    public void R150_TheWindowTakesBothFormsOfLang()
    {
        Assert.Equal(UiLanguage.Romanian, App.LanguageFromArgs(new[] { "--lang", "ro", "poza.tif" }, out string error));
        Assert.Null(error);
        Assert.Equal(UiLanguage.Romanian, App.LanguageFromArgs(new[] { "poza.tif", "--lang=RO" }, out _));
        Assert.Equal(UiLanguage.English, App.LanguageFromArgs(new[] { "--lang=ro", "--lang", "en" }, out _));
    }

    [Fact]
    public void R152_AWrongLangIsSaidAndTheWindowStaysEnglish()
    {
        Assert.Equal(UiLanguage.English, App.LanguageFromArgs(new[] { "--lang", "de" }, out string error));
        Assert.Equal("--lang expects en or ro, got 'de'. The interface is in English.", error);
    }

    [Fact]
    public void R150_TheFileArgumentIsNeverTheLanguageOrItsValue()
    {
        using var ws = new TempWorkspace();
        string ro = ws.WriteFile("ro", new byte[] { 1 });
        string image = ws.WriteFile("poza.tif", new byte[] { 1 });

        // A file called like the value is the value after --lang, not the image.
        Assert.Equal(image, App.FileFromArgs(new[] { "--lang", ro, image }));
        Assert.Equal(image, App.FileFromArgs(new[] { "--lang=ro", image }));
    }
}
