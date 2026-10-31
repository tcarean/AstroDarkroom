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

/// <summary>
/// The Visual C++ runtime LibRaw's DLLs need (R-018, ADR 0016): which of its four DLLs a loader
/// cannot load, the main window's warning in both languages and the log's line. The window's
/// real loader is not run here (the window has no tests, ADR 0001): the core takes it as a
/// function, so a fake one answers.
/// </summary>
public class VcRuntimeTests
{
    [Fact]
    public void R018_Missing_NamesTheFilesTheLoaderCannotLoad_AskingEachOnceInOrder()
    {
        string[] files = ["vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "vcomp140.dll"];
        Assert.Equal(files, VcRuntime.Files);

        List<string> asked = [];
        Assert.Empty(VcRuntime.Missing(file => { asked.Add(file); return true; }));
        Assert.Equal(files, asked);

        string[] openMpOnly = ["vcomp140.dll"];
        Assert.Equal(openMpOnly, VcRuntime.Missing(file => file != "vcomp140.dll"));
        Assert.Equal(files, VcRuntime.Missing(_ => false));
    }

    [Fact]
    public void R018_TheWarning_NamesTheRawToolsBeforeMicrosoftsLink_InBothLanguages()
    {
        Assert.Equal("https://aka.ms/vc14/vc_redist.x64.exe", VcRuntime.DownloadUrl);
        string romanian = VcRuntime.Warning;   // the tests run in Romanian
        using IDisposable scope = Ui.Use(UiLanguage.English);
        Assert.Equal(
            "Microsoft's Visual C++ runtime is missing or too old, so DarksCheck, FlatCheck and LightsCheck cannot read camera raw files until its latest version is installed: ",
            VcRuntime.Warning);
        Assert.Equal(
            "Runtime-ul Microsoft Visual C++ lipseste sau este prea vechi, deci DarksCheck, FlatCheck si LightsCheck nu pot citi fisiere raw de aparat pana nu instalezi ultima lui versiune: ",
            romanian);
    }

    [Fact]
    public void R018_TheLogLine_SaysTheFourLoadOrWhichAreMissing_InEnglish()
    {
        // In Romanian, as all the tests run: the log's words stay English (R-012).
        Assert.Equal("Visual C++ runtime: vcruntime140.dll, vcruntime140_1.dll, msvcp140.dll, vcomp140.dll load",
            LogText.VcRuntime([]));
        Assert.Equal("Visual C++ runtime: missing vcomp140.dll; DarksCheck, FlatCheck and LightsCheck cannot read camera raw files",
            LogText.VcRuntime(["vcomp140.dll"]));
        Assert.Equal("Visual C++ runtime: missing vcruntime140.dll, msvcp140.dll; DarksCheck, FlatCheck and LightsCheck cannot read camera raw files",
            LogText.VcRuntime(["vcruntime140.dll", "msvcp140.dll"]));
    }
}
