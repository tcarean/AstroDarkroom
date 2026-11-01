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

// VcRuntime.cs - Microsoft's Visual C++ runtime, which LibRaw's DLLs need and nothing ships.
//
// The same four names, link and probe as the Astro Darkroom launcher's VcRuntime and NativeProbe
// (AstroDarkroom ADR 0016), kept here because the tool runs without the launcher too: its own
// release has none. The launcher asks at start; this tool asks only after LibRaw failed to load,
// for its log (R-110, R-124, ADR 0085).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace FlatCheck
{
    /// <summary>
    /// Microsoft's Visual C++ v14 runtime for x64, which the DLLs of <c>Sdcb.LibRaw.runtime.win64</c>
    /// import and neither FlatCheck's own release nor the Astro Darkroom folder ships: its DLLs'
    /// names, Microsoft's link to its latest package, and which of the DLLs Windows loads.
    /// </summary>
    internal static class VcRuntime
    {
        /// <summary>Microsoft's permalink for the latest supported x64 package of the runtime.</summary>
        internal const string DownloadUrl = "https://aka.ms/vc14/vc_redist.x64.exe";

        /// <summary>
        /// The runtime's DLLs, as the import tables of the DLLs of <c>Sdcb.LibRaw.runtime.win64</c>
        /// 0.21.1 name them (read on 2026-11-01, held by <c>LibRawLoadTests</c>): <c>raw_r.dll</c>
        /// imports all four, OpenMP's <c>vcomp140.dll</c> included, and <c>jpeg8.dll</c>,
        /// <c>lcms2.dll</c> and <c>zlib1.dll</c> import <c>vcruntime140.dll</c>. The runtimes of 2015
        /// and 2017 lack <c>vcruntime140_1.dll</c>, which Visual Studio 2019 added.
        /// </summary>
        internal static IReadOnlyList<string> Files { get; } = new[] { "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "vcomp140.dll" };

        /// <summary>The runtime's DLLs that a loader cannot load.</summary>
        /// <param name="canLoad">Whether a DLL given by its file name loads; asked once for each of
        /// <see cref="Files"/>, in their order. Not null.</param>
        /// <returns>The files that do not load, in the order of <see cref="Files"/>; empty when the
        /// runtime is there.</returns>
        internal static IReadOnlyList<string> Missing(Func<string, bool> canLoad)
        {
            ArgumentNullException.ThrowIfNull(canLoad);
            return Files.Where(file => !canLoad(file)).ToList();
        }

        /// <summary>
        /// Whether Windows loads a DLL given by its file name, through its standard search (the
        /// program's folder, the system folders, the current folder, PATH), as LibRaw's DLLs find
        /// their imports. A DLL that loads is released at once. The launcher's
        /// <c>NativeProbe.CanLoad</c>.
        /// </summary>
        /// <param name="file">The DLL's file name, such as <c>vcomp140.dll</c>; a name, not a path.</param>
        /// <returns>True when it loads.</returns>
        internal static bool CanLoad(string file)
        {
            if (!NativeLibrary.TryLoad(file, out IntPtr handle)) return false;
            NativeLibrary.Free(handle);
            return true;
        }
    }
}
