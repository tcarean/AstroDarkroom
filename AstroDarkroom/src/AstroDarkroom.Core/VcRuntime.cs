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

using static AstroDarkroom.Core.Ui;

namespace AstroDarkroom.Core;

/// <summary>
/// Microsoft's Visual C++ v14 runtime for x64, which LibRaw's DLLs in the suite folder need and
/// the suite does not ship (R-018, ADR 0016): without it DarksCheck, FlatCheck and LightsCheck
/// cannot read camera raw files. The core names the runtime's DLLs, finds which ones a loader
/// cannot load and words the warning; the window does the loading.
/// </summary>
public static class VcRuntime
{
    /// <summary>Microsoft's permalink for the latest supported x64 package of the runtime.</summary>
    public const string DownloadUrl = "https://aka.ms/vc14/vc_redist.x64.exe";

    /// <summary>
    /// The runtime's DLLs, as the import tables of the DLLs of <c>Sdcb.LibRaw.runtime.win64</c>
    /// 0.21.1 name them (read on 2026-10-31): <c>raw_r.dll</c> imports all four, OpenMP's
    /// <c>vcomp140.dll</c> included, and <c>jpeg8.dll</c>, <c>lcms2.dll</c> and <c>zlib1.dll</c>
    /// import <c>vcruntime140.dll</c>. A suite test holds the raw tools to that version, so another
    /// one has its DLLs read again before it ships.
    /// </summary>
    public static IReadOnlyList<string> Files { get; } = ["vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "vcomp140.dll"];

    /// <summary>
    /// The main window's warning when one of <see cref="Files"/> does not load, in the current
    /// language: the runtime is missing, or too old to have all four. The text comes before
    /// Microsoft's link (<see cref="DownloadUrl"/>), which installs the latest version.
    /// </summary>
    public static string Warning => T(
        "Microsoft's Visual C++ runtime is missing or too old, so DarksCheck, FlatCheck and LightsCheck cannot read camera raw files until its latest version is installed: ",
        "Runtime-ul Microsoft Visual C++ lipseste sau este prea vechi, deci DarksCheck, FlatCheck si LightsCheck nu pot citi fisiere raw de aparat pana nu instalezi ultima lui versiune: ");

    /// <summary>The runtime's DLLs that a loader cannot load.</summary>
    /// <param name="canLoad">
    /// Whether Windows loads a DLL given by its file name, from where a program in the suite folder
    /// finds it; asked once for each of <see cref="Files"/>, in their order. Not null.
    /// </param>
    /// <returns>The files that do not load, in the order of <see cref="Files"/>; empty when the runtime is there.</returns>
    public static IReadOnlyList<string> Missing(Func<string, bool> canLoad)
    {
        ArgumentNullException.ThrowIfNull(canLoad);
        return Files.Where(file => !canLoad(file)).ToList();
    }
}
