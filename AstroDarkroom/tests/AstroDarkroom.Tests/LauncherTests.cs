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

/// <summary>R-001 to R-004: the catalogue, where an app is, the plan of a start, release.json.</summary>
public class LauncherTests
{
    private static SuiteApp App(string folder) => AppCatalogue.Apps.Single(a => a.Folder == folder);

    [Fact]
    public void R001_Catalogue_ListsTheFiveAppsInWorkflowOrderWithTheirTexts()
    {
        SuiteApp[] expected =
        [
            new("DarksCheck", "DarksCheck", "Verifica dark-urile unei sesiuni sau ale unei biblioteci", "DarksCheck.Wpf.exe", "darkscheck.exe"),
            new("FlatCheck", "FlatCheck", "Verifica flat-urile unui obiectiv", "FlatCheck.Wpf.exe", "flatcheck.exe"),
            new("LightsCheck", "LightsCheck", "Verifica light-urile unei sesiuni: focus, trailing, dithering, drift", "LightsCheck.Wpf.exe", "lightscheck.exe"),
            new("ImageVisualCompare", "ImageVisualCompare", "Compara doua variante ale aceleiasi procesari, pixel cu pixel", "ImageVisualCompare.Wpf.exe", null),
            new("AstroPhotoAnalyzer", "AstroPhotoAnalyzer", "Verifica o imagine finala inainte de print", "AstroPhotoAnalyzer.Wpf.exe", "AstroPhotoAnalyzer.exe"),
        ];
        Assert.Equal(expected, AppCatalogue.Apps);
    }

    [Fact]
    public void R001_EveryApp_NamesAWindowExeAndSaysWhetherItHasAConsole()
    {
        foreach (SuiteApp app in AppCatalogue.Apps)
        {
            Assert.EndsWith(".exe", app.WindowExe, StringComparison.Ordinal);
            Assert.Equal(app.ConsoleExe is not null, app.HasConsole);
            if (app.ConsoleExe is not null)
            {
                Assert.EndsWith(".exe", app.ConsoleExe, StringComparison.Ordinal);
            }
        }

        Assert.Equal(4, AppCatalogue.Apps.Count(a => a.HasConsole));
        Assert.False(App("ImageVisualCompare").HasConsole);
    }

    [Fact]
    public void R002_AnApp_IsAvailableWhenItsWindowExeIsInTheSuiteFolder()
    {
        using TempWorkspace ws = new();
        string exe = ws.Touch("DarksCheck.Wpf.exe");
        SuiteFolder folder = new(ws.Root);

        AppLocation found = folder.Locate(App("DarksCheck"));
        Assert.True(found.IsAvailable);
        Assert.Equal(exe, found.Path);
        Assert.Null(found.Reason);
    }

    [Fact]
    public void R002_AMissingWindowExe_MakesTheAppUnavailableAndNamesTheFile()
    {
        using TempWorkspace ws = new();
        ws.Touch("DarksCheck.Wpf.exe");
        ws.Touch("flatcheck.exe");   // the console alone does not make FlatCheck available
        SuiteFolder folder = new(ws.Root);

        AppLocation missing = folder.Locate(App("FlatCheck"));
        Assert.False(missing.IsAvailable);
        Assert.Equal(ws.PathTo("FlatCheck.Wpf.exe"), missing.Path);
        Assert.Equal("Lipseste FlatCheck.Wpf.exe", missing.Reason);
    }

    [Fact]
    public void R002_AFolderThatDoesNotExist_MakesEveryAppUnavailable()
    {
        SuiteFolder folder = new(Path.Combine(Path.GetTempPath(), "AstroDarkroom.Tests", "nu-exista"));
        Assert.All(AppCatalogue.Apps, a => Assert.False(folder.Locate(a).IsAvailable));
        Assert.Equal(Path.Combine(folder.BaseDirectory, "release.json"), folder.ManifestPath);
    }

    [Fact]
    public void R003_ThePlanOfAnAvailableApp_IsItsWindowExeFromTheSuiteFolder()
    {
        using TempWorkspace ws = new();
        string exe = ws.Touch("LightsCheck.Wpf.exe");

        LaunchPlan plan = Launcher.Plan(App("LightsCheck"), new SuiteFolder(ws.Root));
        Assert.Equal(exe, plan.FileName);
        Assert.Equal(ws.Root, plan.WorkingDirectory);
    }

    [Fact]
    public void R003_PlanningAnUnavailableApp_ThrowsWithTheReason()
    {
        using TempWorkspace ws = new();
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Launcher.Plan(App("AstroPhotoAnalyzer"), new SuiteFolder(ws.Root)));
        Assert.Equal("Lipseste AstroPhotoAnalyzer.Wpf.exe", ex.Message);
    }

    [Fact]
    public void R004_ReleaseJson_GivesTheSuiteVersionAndEachComponent()
    {
        using TempWorkspace ws = new();
        string path = ws.Write("release.json", """
            {
              "name": "AstroDarkroom",
              "version": "0.1.0",
              "date": "2026-10-25",
              "commit": "0123456789abcdef0123456789abcdef01234567",
              "components": [
                { "name": "AstroDarkroom", "version": "0.1.0", "commit": "0123456789abcdef0123456789abcdef01234567", "unreleased": true, "clean": true },
                { "name": "FlatCheck", "version": "2.3.0", "commit": "fedcba9876543210fedcba9876543210fedcba98", "unreleased": true, "clean": false }
              ]
            }
            """);

        ReleaseInfo? info = ReleaseManifest.TryRead(path);
        Assert.NotNull(info);
        Assert.Equal("AstroDarkroom", info.Name);
        Assert.Equal("0.1.0", info.Version);
        Assert.Equal("2026-10-25", info.Date);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", info.Commit);
        Assert.Equal(2, info.Components.Count);

        ComponentInfo? flat = info.Find("FlatCheck");
        Assert.NotNull(flat);
        Assert.Equal("2.3.0", flat.Version);
        Assert.Equal("fedcba9876543210fedcba9876543210fedcba98", flat.Commit);
        Assert.True(flat.Unreleased);
        Assert.False(flat.Clean);
        Assert.Null(info.Find("DarksCheck"));
    }

    [Fact]
    public void R004_AMissingOrInvalidReleaseJson_LeavesTheVersionsUnknown()
    {
        using TempWorkspace ws = new();
        Assert.Null(ReleaseManifest.TryRead(ws.PathTo("release.json")));
        Assert.Null(ReleaseManifest.TryRead(ws.Write("broken.json", "{ not json")));
        Assert.Null(ReleaseManifest.TryRead(ws.Write("array.json", "[1, 2]")));
        Assert.Null(ReleaseManifest.TryRead(ws.Write("no-components.json", """{ "version": "0.1.0" }""")));
    }

    [Fact]
    public void R004_AMissingOrMistypedKey_ReadsAsEmptyOrFalse()
    {
        using TempWorkspace ws = new();
        string path = ws.Write("release.json", """
            { "version": 1, "components": [ { "name": "FlatCheck", "unreleased": "yes" }, 7 ] }
            """);

        ReleaseInfo? info = ReleaseManifest.TryRead(path);
        Assert.NotNull(info);
        Assert.Equal("", info.Version);
        Assert.Equal("", info.Date);
        ComponentInfo flat = Assert.Single(info.Components);
        Assert.Equal("FlatCheck", flat.Name);
        Assert.Equal("", flat.Version);
        Assert.Equal("", flat.Commit);
        Assert.False(flat.Unreleased);
        Assert.False(flat.Clean);
    }
}
