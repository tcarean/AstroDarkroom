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

using System.Text;
using AstroPhotoAnalyzer.Tests;

namespace AstroPhotoAnalyzer.Wpf.Tests;

/// <summary>
/// The profile window's view-model and what "Foloseste" does to the parameter card (R-132), on
/// hand-built printer profiles in a folder of the test's own: L* 20 is Dmax 1.52, L* 30 is 1.21.
/// </summary>
public class IccPickerTests
{
    static TempWorkspace FolderWithProfiles()
    {
        var ws = new TempWorkspace();
        ws.WriteFile("baryta.icc", IccPrinterTestData.V2Lut16("Zeta baryta", 20));
        ws.WriteFile("canvas.icm", IccPrinterTestData.V4LutAToB("Alfa canvas", 30));
        ws.WriteFile("tipar.icc", IccPrinterTestData.V2Lut16("Tipar", 8, space: "CMYK"));
        ws.WriteFile("monitor.icc", IccPrinterTestData.V2Lut16("Monitor", 20, deviceClass: "mntr"));
        return ws;
    }

    [Fact]
    public void R132_TheListShowsThePrinterProfilesWithTheCoresSentence()
    {
        using TempWorkspace ws = FolderWithProfiles();
        var vm = new IccPickerViewModel(ws.Dir);

        Assert.Equal(new[] { "Alfa canvas", "Tipar", "Zeta baryta" }, vm.Profiles.Select(p => p.Name));
        Assert.False(vm.IsEmpty);
        Assert.Equal(ws.Dir, vm.Folder);
        IccProfileRow canvas = vm.Profiles[0];
        Assert.Equal("Dmax 1.21 (negrul la L* 30.0 fata de albul hartiei)", canvas.Detail);
        Assert.Equal("1.21", canvas.DmaxText);
        Assert.Null(canvas.Note);
        Assert.Contains("profil CMYK", vm.Profiles[1].Note);
        Assert.Same(canvas, vm.Selected);   // the first readable one
        Assert.True(vm.CanUse);
        Assert.Contains("tabela colorimetrica", vm.Explanation);
    }

    [Fact]
    public void R132_AFileFromElsewhereJoinsTheListAndAnUnreadableOneCannotBeUsed()
    {
        using var ws = new TempWorkspace();
        var vm = new IccPickerViewModel(ws.PathTo("gol"));
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.Selected);
        Assert.False(vm.CanUse);

        string notIcc = ws.WriteFile("note.icc", Encoding.ASCII.GetBytes(new string('x', 400)));
        vm.AddFile(notIcc);
        Assert.False(vm.IsEmpty);
        Assert.Equal("nu e un profil ICC", vm.Selected!.Detail);
        Assert.False(vm.CanUse);

        vm.AddFile(ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20)));
        Assert.Equal("Hartie de test", vm.Selected!.Name);
        Assert.True(vm.CanUse);
        Assert.Equal(2, vm.Profiles.Count);

        // The same file again: selected, not listed twice.
        vm.AddFile(notIcc);
        Assert.Equal(2, vm.Profiles.Count);
        Assert.Equal("nu e un profil ICC", vm.Selected!.Detail);
    }

    /// <summary>
    /// "Foloseste" writes the profile's Dmax into the field; the run names the profile while the
    /// field still shows that Dmax, and forgets it when the user types another or picks a medium.
    /// </summary>
    [Fact]
    public void R132_UsingAProfileFillsTheDmaxFieldAndTheReportNamesItWhileUntouched()
    {
        using TempWorkspace ws = FolderWithProfiles();
        var vm = new IccPickerViewModel(ws.Dir);
        var f = new ParameterFields();

        f.UseProfile(vm.Profiles.Single(p => p.Name == "Zeta baryta"));
        Assert.Equal("1.52", f.Dmax);
        Parameters? p = f.TryBuild(out string? error);
        Assert.Null(error);
        Assert.Equal(1.52, p!.Dmax);
        Assert.Equal(" (din profilul ICC \"Zeta baryta\")", ReportWriter.DmaxOrigin(p));

        f.Dmax = "1.6";
        p = f.TryBuild(out _);
        Assert.Null(p!.DmaxProfile);
        Assert.Equal(" (explicit)", ReportWriter.DmaxOrigin(p));

        f.UseProfile(vm.Profiles.Single(x => x.Name == "Zeta baryta"));
        f.Medium = "canvas";
        Assert.Equal("1.5", f.Dmax);
        p = f.TryBuild(out _);
        Assert.Null(p!.DmaxProfile);
        Assert.Equal("", ReportWriter.DmaxOrigin(p));

        // The CMYK reserve goes with the profile into the report, and leaves with it.
        f.UseProfile(vm.Profiles.Single(x => x.Name == "Tipar"));
        Assert.Contains("profil CMYK", f.TryBuild(out _)!.DmaxProfileNote);
        f.Medium = "baryta";
        Assert.Null(f.TryBuild(out _)!.DmaxProfileNote);
        f.Medium = "canvas";

        // An unreadable profile changes nothing.
        vm.AddFile(ws.WriteFile("note.icc", Encoding.ASCII.GetBytes(new string('x', 400))));
        f.UseProfile(vm.Selected);
        Assert.Equal("1.5", f.Dmax);
    }
}
