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

// ProfileBarTests.cs - the ICC profile in the top bar (R-155): choosing and clearing it re-run
// the analysis by themselves; and the remembered profile (R-156): the settings file written on
// every change and read back at the next start, on paths of the test's own, never the user's.

using AstroPhotoAnalyzer.Tests;
using System.Text;
using System.Text.Json;

namespace AstroPhotoAnalyzer.Wpf.Tests;

public class ProfileBarTests
{
    static string DarkTiff(TempWorkspace ws)
    {
        Raster r = Scenes.DarkSky();
        return ws.WriteFile("cer-intunecat.tif", TiffTestData.Build16Rgb(r.W, r.H,
            (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V2("sRGB test")));
    }

    static IccProfileRow Baryta(TempWorkspace ws)
    {
        string file = ws.WriteFile("baryta.icc", IccPrinterTestData.V2Lut16("Zeta baryta", 20));
        return new IccProfileRow(IccPaperBlack.Read(file));
    }

    /// <summary>The Anexa F line of the report shown, where the Dmax's origin is named.</summary>
    static string DmaxLine(MainViewModel vm) => vm.Report.Text.Split('\n').Single(l => l.TrimStart().StartsWith("mediu ") && l.Contains("Dmax "));

    [Fact]
    public async Task R155_ChoosingAProfileRerunsAndTheReportNamesIt()
    {
        using var ws = new TempWorkspace();
        var vm = new MainViewModel();
        await vm.OpenAsync(DarkTiff(ws));
        Assert.True(vm.HasResult, vm.Error);
        Assert.DoesNotContain("profilul ICC", DmaxLine(vm));

        await vm.ChooseProfile(Baryta(ws));

        Assert.True(vm.HasResult, vm.Error);
        Assert.Equal("1.52", vm.Parameters.Dmax);
        Assert.Contains("din profilul ICC \"Zeta baryta\"", DmaxLine(vm));
    }

    [Fact]
    public async Task R155_ClearingRerunsWithoutTheProfile()
    {
        using var ws = new TempWorkspace();
        var vm = new MainViewModel();
        await vm.OpenAsync(DarkTiff(ws));
        await vm.ChooseProfile(Baryta(ws));
        Assert.Contains("profilul ICC", DmaxLine(vm));

        await vm.ClearProfile();

        Assert.True(vm.HasResult, vm.Error);
        Assert.False(vm.Parameters.HasProfile);
        Assert.Equal("2.4", vm.Parameters.Dmax);
        Assert.DoesNotContain("profilul ICC", DmaxLine(vm));
        Assert.Contains("Dmax 2.4", DmaxLine(vm));
    }

    /// <summary>
    /// The same profile chosen again, its Dmax still in the field, starts no run: the busy flag
    /// never rises. Clearing when nothing is set runs nothing either.
    /// </summary>
    [Fact]
    public async Task R155_ChoosingTheSameProfileAgainRunsNothing()
    {
        using var ws = new TempWorkspace();
        var vm = new MainViewModel();
        await vm.OpenAsync(DarkTiff(ws));
        IccProfileRow baryta = Baryta(ws);
        await vm.ChooseProfile(baryta);
        int busy = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.IsBusy)) busy++; };

        await vm.ChooseProfile(baryta);
        await vm.ChooseProfile(null);
        Assert.Equal(0, busy);

        await vm.ClearProfile();
        Assert.Equal(2, busy);   // one run: busy up, busy down
        await vm.ClearProfile();
        Assert.Equal(2, busy);
    }

    /// <summary>Without an image the choice is kept for the first run and nothing runs now.</summary>
    [Fact]
    public async Task R155_WithoutAnImageTheChoiceWaitsForTheFirstRun()
    {
        using var ws = new TempWorkspace();
        var vm = new MainViewModel();

        await vm.ChooseProfile(Baryta(ws));

        Assert.False(vm.HasResult);
        Assert.False(vm.IsBusy);
        Assert.Equal("Zeta baryta - Dmax 1.52 (negrul la L* 20.0 fata de albul hartiei)", vm.Parameters.ProfileText);
        await vm.OpenAsync(DarkTiff(ws));
        Assert.Contains("din profilul ICC \"Zeta baryta\"", DmaxLine(vm));
    }

    [Fact]
    public async Task R156_TheChosenProfileIsWrittenAndReadBack()
    {
        using var ws = new TempWorkspace();
        string settingsPath = ws.PathTo(Path.Combine("AstroPhotoAnalyzer", "settings.json"));
        IccProfileRow baryta = Baryta(ws);

        var first = new MainViewModel(WindowSettings.Read(settingsPath));
        Assert.False(first.Parameters.HasProfile);
        Assert.False(File.Exists(settingsPath));

        await first.ChooseProfile(baryta);
        Assert.True(File.Exists(settingsPath));
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(settingsPath)))
            Assert.Equal(baryta.File, doc.RootElement.GetProperty("iccProfile").GetString());
        Assert.False(File.Exists(settingsPath + ".tmp"));

        // The next start: the profile is applied as if chosen.
        var second = new MainViewModel(WindowSettings.Read(settingsPath));
        Assert.True(second.Parameters.HasProfile);
        Assert.False(second.Parameters.ProfileIsProblem);
        Assert.Equal("Zeta baryta", second.Parameters.Profile!.Name);
        Assert.Equal("1.52", second.Parameters.Dmax);
        Assert.Equal("Zeta baryta", second.Parameters.TryBuild(out _)!.DmaxProfile);

        // Dropped by a typed Dmax: the key is gone from the file.
        second.Parameters.Dmax = "1.9";
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(settingsPath)))
            Assert.False(doc.RootElement.TryGetProperty("iccProfile", out _));
        Assert.False(new MainViewModel(WindowSettings.Read(settingsPath)).Parameters.HasProfile);

        // Cleared: the same.
        await second.ChooseProfile(baryta);
        Assert.Contains("baryta.icc", File.ReadAllText(settingsPath));
        await second.ClearProfile();
        Assert.False(new MainViewModel(WindowSettings.Read(settingsPath)).Parameters.HasProfile);
    }

    [Fact]
    public async Task R156_AProfileThatCannotBeReadAnyMoreIsShownWithItsReasonAndNotUsed()
    {
        using var ws = new TempWorkspace();
        string settingsPath = ws.PathTo("settings.json");
        IccProfileRow baryta = Baryta(ws);
        await new MainViewModel(WindowSettings.Read(settingsPath)).ChooseProfile(baryta);

        File.Delete(baryta.File);
        var vm = new MainViewModel(WindowSettings.Read(settingsPath));

        Assert.True(vm.Parameters.HasProfile);
        Assert.True(vm.Parameters.ProfileIsProblem);
        Assert.StartsWith("baryta.icc - fisierul nu se poate citi: ", vm.Parameters.ProfileText);
        Assert.Equal("2.4", vm.Parameters.Dmax);
        Parameters? p = vm.Parameters.TryBuild(out string? error);
        Assert.Null(error);
        Assert.Null(p!.DmaxProfile);
        Assert.Equal(2.4, p.Dmax);

        // A broken file, not a missing one: the core's reason.
        File.WriteAllBytes(baryta.File, Encoding.ASCII.GetBytes(new string('x', 400)));
        Assert.Equal("baryta.icc - nu e un profil ICC", new MainViewModel(WindowSettings.Read(settingsPath)).Parameters.ProfileText);

        // "Scoate" removes it from the file too.
        await vm.ClearProfile();
        Assert.False(new MainViewModel(WindowSettings.Read(settingsPath)).Parameters.HasProfile);
    }

    [Fact]
    public async Task R156_AMissingOrBrokenSettingsFileMeansNoProfileAndAFailedWriteThrowsNothing()
    {
        using var ws = new TempWorkspace();
        Assert.Null(WindowSettings.Read(ws.PathTo("absent.json")).IccProfilePath);
        Assert.Null(WindowSettings.Read(ws.WriteFile("garbage.json", Encoding.ASCII.GetBytes("{not json"))).IccProfilePath);
        Assert.Null(WindowSettings.Read(ws.WriteFile("array.json", Encoding.ASCII.GetBytes("[1, 2]"))).IccProfilePath);
        Assert.Null(WindowSettings.Read(ws.WriteFile("other.json", Encoding.ASCII.GetBytes("{\"language\": \"ro\"}"))).IccProfilePath);
        Assert.Null(WindowSettings.Read(ws.WriteFile("number.json", Encoding.ASCII.GetBytes("{\"iccProfile\": 7}"))).IccProfilePath);
        Assert.Null(WindowSettings.Read(null).IccProfilePath);

        // A settings path under a file, where no folder can be made: the choice holds for the
        // run, the write fails quietly.
        string blocker = ws.WriteFile("blocker", Encoding.ASCII.GetBytes("x"));
        var vm = new MainViewModel(WindowSettings.Read(Path.Combine(blocker, "settings.json")));
        await vm.ChooseProfile(Baryta(ws));
        Assert.True(vm.Parameters.HasProfile);
        Assert.Equal("1.52", vm.Parameters.Dmax);

        // No path: nothing is written anywhere (the tests' and the parameterless constructor's case).
        var none = new MainViewModel();
        await none.ChooseProfile(Baryta(ws));
        Assert.True(none.Parameters.HasProfile);
        Assert.Empty(Directory.GetFiles(ws.Dir, "settings*", SearchOption.AllDirectories));
    }
}
