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

/// <summary>R-006 to R-009: the two languages, the remembered setting, the tool's language, the prerequisites texts.</summary>
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
        var romanianRest = AppCatalogue.Apps.Select(a => (a.Folder, a.Title, a.WindowExe)).ToArray();
        using IDisposable english = Ui.Use(UiLanguage.English);

        string[] expected =
        [
            "Checks the darks of a session or a library: light leaks, thermal drift, noise, hot and dead pixels, and whether their temperature matches the lights.",
            "Checks the flats of a lens: exposure, symmetry, vignetting against the lens profile, and whether they correct a real light; builds a flat from the profile.",
            "Checks the lights of a night: which frames to leave out of the stack and why, dithering, mount drift, polar alignment, what to change next time.",
            "Compares two versions of one image with a before/after slider (two stretches, with and without stars), pixel by pixel; reveals faint gradients.",
            "Copies the plate solve of a FITS back into its processed version, which lost it on the way, so that annotations and catalogues find the sky again.",
            "Prepares an image for print: simulates the paper, checks the blacks against its limits, helps edit the curves and soft-proofs with the paper's ICC profile.",
        ];
        Assert.Equal(expected, AppCatalogue.Apps.Select(a => a.Description));

        // The rest of the catalogue does not change with the language.
        Assert.Equal(["DarksCheck", "FlatCheck", "LightsCheck", "ImageVisualCompare", "WCS_Copy", "AstroPhotoAnalyzer"], AppCatalogue.Apps.Select(a => a.Folder));
        Assert.Equal(romanianRest, AppCatalogue.Apps.Select(a => (a.Folder, a.Title, a.WindowExe)));

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
        Assert.False(File.Exists(path + ".tmp"));   // the temporary file of the atomic write is gone

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
        Assert.Equal("--lang=ro", romanian.Arguments);
        Assert.Equal(new LaunchPlan(exe, ws.Root, "--lang=ro"), romanian);   // the record keeps its value equality

        using IDisposable scope = Ui.Use(UiLanguage.English);
        LaunchPlan english = Launcher.Plan(App("LightsCheck"), folder);
        Assert.Equal("--lang=en", english.Arguments);
    }

    [Fact]
    public void R009_ThePrerequisites_NameSirilTheVisualCppRuntimeAndTheAdobeProfilesInBothLanguages()
    {
        string romanianSummary = Prerequisites.Summary;
        string[] romanian = Prerequisites.Details.ToArray();
        Assert.StartsWith("Uneltele citesc folderul unei sesiuni Siril", romanianSummary, StringComparison.Ordinal);
        Assert.Equal(4, romanian.Length);
        Assert.StartsWith("Siril 1.4.4 sau mai nou instalat", romanian[1], StringComparison.Ordinal);
        Assert.StartsWith("Runtime-ul Microsoft Visual C++ v14 (x64)", romanian[2], StringComparison.Ordinal);

        using IDisposable scope = Ui.Use(UiLanguage.English);
        string englishSummary = Prerequisites.Summary;
        string[] english = Prerequisites.Details.ToArray();
        Assert.StartsWith("The tools read a Siril session folder", englishSummary, StringComparison.Ordinal);
        Assert.Equal(4, english.Length);
        Assert.StartsWith("Siril 1.4.4 or newer installed", english[1], StringComparison.Ordinal);
        Assert.StartsWith("Microsoft's Visual C++ v14 runtime (x64)", english[2], StringComparison.Ordinal);

        // Each wording is its own text, not the other language's left in place.
        Assert.NotEqual(romanianSummary, englishSummary);
        Assert.All(english.Zip(romanian), pair => Assert.NotEqual(pair.First, pair.Second));

        // The facts every reader must find, whatever the language: the folder structure, the
        // version, the Visual C++ runtime the raw tools need and where it comes from (R-018), the
        // optional profiles and the programs that bring them.
        foreach (string whole in new[] { string.Join('\n', romanian), string.Join('\n', english) })
        {
            foreach (string fact in new[] { "lights/", "darks/", "flats/", "biases/", "process/", "Siril 1.4.4", "siril-cli", "Visual C++", "DarksCheck", "LightsCheck", "https://aka.ms/vc14/vc_redist.x64.exe", "FlatCheck", "Adobe", "Lightroom", "Camera Raw", "DNG Converter", "lensfun" })
            {
                Assert.Contains(fact, whole, StringComparison.Ordinal);
            }
        }

        Assert.Contains("Siril 1.4.4", romanianSummary, StringComparison.Ordinal);
        Assert.Contains("Siril 1.4.4", englishSummary, StringComparison.Ordinal);
    }
}
