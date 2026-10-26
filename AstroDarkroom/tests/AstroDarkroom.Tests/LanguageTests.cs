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

using AstroDarkroom.Core;

namespace AstroDarkroom.Tests;

/// <summary>R-006 to R-008: the two languages, the remembered setting, the tool's language.</summary>
public class LanguageTests
{
    private static SuiteApp App(string folder) => AppCatalogue.Apps.Single(a => a.Folder == folder);

    [Fact]
    public void R006_TheTestsRunInRomanian_AndAScopePicksEnglish()
    {
        Assert.Equal(UiLanguage.Romanian, Ui.Current);
        Assert.Equal("romana", Ui.T("english", "romana"));
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal(UiLanguage.English, Ui.Current);
            Assert.Equal("english", Ui.T("english", "romana"));
        }

        Assert.Equal(UiLanguage.Romanian, Ui.Current);
    }

    [Fact]
    public void R006_StartupDefault_IsEnglish()
    {
        Assert.Equal(UiLanguage.English, Ui.StartupDefault);
    }

    [Fact]
    public void R006_TheCatalogue_DescribesEveryAppInEnglishToo()
    {
        string[] romanian = AppCatalogue.Apps.Select(a => a.Description).ToArray();
        using IDisposable english = Ui.Use(UiLanguage.English);

        string[] expected =
        [
            "Checks the darks of a session or of a library",
            "Checks the flats of a lens",
            "Checks the lights of a session: focus, trailing, dithering, drift",
            "Compares two versions of the same processing, pixel by pixel",
            "Checks a final image before printing",
        ];
        Assert.Equal(expected, AppCatalogue.Apps.Select(a => a.Description));

        // The rest of the catalogue does not change with the language.
        Assert.Equal(["DarksCheck", "FlatCheck", "LightsCheck", "ImageVisualCompare", "AstroPhotoAnalyzer"], AppCatalogue.Apps.Select(a => a.Folder));
        Assert.Equal(AppCatalogue.Apps.Select(a => a.Folder), AppCatalogue.Apps.Select(a => a.Title));
        Assert.Equal(AppCatalogue.Apps.Select(a => a.Folder + ".Wpf.exe"), AppCatalogue.Apps.Select(a => a.WindowExe));

        // Each English wording is its own text, not the Romanian one left in place.
        Assert.All(expected.Zip(romanian), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void R006_AMissingWindowExe_IsSaidInEnglishToo()
    {
        using TempWorkspace ws = new();
        SuiteFolder folder = new(ws.Root);
        using IDisposable english = Ui.Use(UiLanguage.English);

        Assert.Equal("FlatCheck.Wpf.exe is missing", folder.Locate(App("FlatCheck")).Reason);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Launcher.Plan(App("FlatCheck"), folder));
        Assert.Equal("FlatCheck.Wpf.exe is missing", ex.Message);
    }

    [Fact]
    public void R006_TheWarranty_IsInBothLanguages()
    {
        string romanian = LicenseInfo.Warranty;
        Assert.StartsWith("Acest program vine FARA NICIO GARANTIE", romanian, StringComparison.Ordinal);
        using IDisposable scope = Ui.Use(UiLanguage.English);
        string english = LicenseInfo.Warranty;
        Assert.StartsWith("This program comes with ABSOLUTELY NO WARRANTY", english, StringComparison.Ordinal);
        Assert.Contains(LicenseInfo.LicenseUrl, english, StringComparison.Ordinal);
        Assert.Contains(LicenseInfo.LicenseUrl, romanian, StringComparison.Ordinal);
    }

    [Fact]
    public void R007_TheSetting_RoundTripsThroughItsFile()
    {
        using TempWorkspace ws = new();
        string path = ws.PathTo(Path.Combine("AstroDarkroom", "settings.json"));

        Assert.Null(LanguageSetting.TryRead(path));
        LanguageSetting.Write(path, UiLanguage.Romanian);
        Assert.Equal(UiLanguage.Romanian, LanguageSetting.TryRead(path));
        LanguageSetting.Write(path, UiLanguage.English);
        Assert.Equal(UiLanguage.English, LanguageSetting.TryRead(path));
    }

    [Fact]
    public void R007_TheFile_IsTheCodeUnderTheLanguageKey()
    {
        using TempWorkspace ws = new();
        string path = ws.PathTo("settings.json");
        LanguageSetting.Write(path, UiLanguage.Romanian);
        Assert.Equal("""{"language":"ro"}""", File.ReadAllText(path).Trim());

        Assert.Equal(UiLanguage.English, LanguageSetting.TryRead(ws.Write("en.json", """{ "language": "en" }""")));
        Assert.Equal(UiLanguage.Romanian, LanguageSetting.TryRead(ws.Write("upper.json", """{ "language": "RO" }""")));
    }

    [Fact]
    public void R007_AMissingOrInvalidFile_GivesNoLanguage()
    {
        using TempWorkspace ws = new();
        Assert.Null(LanguageSetting.TryRead(ws.PathTo("missing.json")));
        Assert.Null(LanguageSetting.TryRead(ws.Write("broken.json", "{ not json")));
        Assert.Null(LanguageSetting.TryRead(ws.Write("array.json", "[1]")));
        Assert.Null(LanguageSetting.TryRead(ws.Write("empty.json", "{}")));
        Assert.Null(LanguageSetting.TryRead(ws.Write("number.json", """{ "language": 1 }""")));
        Assert.Null(LanguageSetting.TryRead(ws.Write("unknown.json", """{ "language": "fr" }""")));
    }

    [Fact]
    public void R007_AWriteThatCannotHappen_IsIgnored()
    {
        using TempWorkspace ws = new();
        // A file where the folder should be: the folder cannot be created, the write fails, nothing throws.
        string blocker = ws.Touch("blocker");
        LanguageSetting.Write(Path.Combine(blocker, "settings.json"), UiLanguage.Romanian);
        Assert.True(File.Exists(blocker));
    }

    [Fact]
    public void R008_ThePlan_CarriesTheLanguageOfTheLauncher()
    {
        using TempWorkspace ws = new();
        string exe = ws.Touch("LightsCheck.Wpf.exe");
        SuiteFolder folder = new(ws.Root);

        LaunchPlan romanian = Launcher.Plan(App("LightsCheck"), folder);
        Assert.Equal(exe, romanian.FileName);
        Assert.Equal(["--lang=ro"], romanian.Arguments);

        using IDisposable scope = Ui.Use(UiLanguage.English);
        LaunchPlan english = Launcher.Plan(App("LightsCheck"), folder);
        Assert.Equal(["--lang=en"], english.Arguments);
    }
}
