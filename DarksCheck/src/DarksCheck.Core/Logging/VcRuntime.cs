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

// VcRuntime.cs - Microsoft's Visual C++ v14 runtime, which LibRaw's DLLs import and which neither
// the single file nor the Astro Darkroom folder ships. When LibRaw cannot be loaded, Windows' error
// names libraw.dll whatever is missing, so the log asks Windows once per process which of the
// runtime's four DLLs it loads, and from where (R-110, ADR 0026). The user's message is RawReader's;
// this file writes the log's line only, and never throws into the seam that calls it.

using System.Diagnostics;
using System.Runtime.InteropServices;
using NLog;

namespace DarksCheck.Core;

/// <summary>One of the Visual C++ runtime's DLLs, as Windows answered for it.</summary>
/// <param name="File">The DLL's file name, such as <c>vcomp140.dll</c>.</param>
/// <param name="Loads">Whether Windows loads it, by that name, from where this process looks.</param>
/// <param name="Path">The full path it was loaded from; null when it does not load, or when the
/// process's module list does not name it.</param>
/// <param name="Version">Its file version, such as <c>14.51.36247.0</c>; null when it does not load
/// or carries none.</param>
internal sealed record RuntimeDll(string File, bool Loads, string? Path, string? Version);

/// <summary>
/// The Visual C++ runtime's four DLLs and the one line per process the log writes about them,
/// at the first time LibRaw cannot be loaded (R-110, ADR 0026).
/// </summary>
internal static class VcRuntime
{
    /// <summary>The diagnostic log: the runtime's line (R-110).</summary>
    private static readonly Logger Log = LogManager.GetLogger(nameof(VcRuntime));

    /// <summary>0 until this process has written its line, 1 after.</summary>
    private static int _logged;

    /// <summary>
    /// The runtime's DLLs, in the order of the launcher's list (AstroDarkroom's
    /// <c>VcRuntime.Files</c>): the import tables of <c>Sdcb.LibRaw.runtime.win64</c> 0.21.1 name
    /// them, read again on 2026-11-01; <c>raw_r.dll</c> imports all four, <c>jpeg8.dll</c>,
    /// <c>lcms2.dll</c> and <c>zlib1.dll</c> import <c>vcruntime140.dll</c>.
    /// </summary>
    internal static IReadOnlyList<string> Files { get; } = ["vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "vcomp140.dll"];

    /// <summary>
    /// Whether an exception is RawReader's error for a LibRaw that cannot be loaded: an
    /// <see cref="InvalidOperationException"/> around one of the four loader exceptions that
    /// RawReader's <c>Load</c> and <c>LoadInfo</c> turn into it. RawReader's other refusals carry
    /// no inner exception.
    /// </summary>
    /// <param name="e">A frame's exception; not null.</param>
    /// <returns>True for LibRaw's load failure.</returns>
    internal static bool IsLoadFailure(Exception e)
        => e is InvalidOperationException { InnerException: DllNotFoundException or BadImageFormatException or TypeInitializationException or FileNotFoundException };

    /// <summary>
    /// Writes the runtime's line, once per process, when a frame's exception is LibRaw's load
    /// failure; any other exception, and every failure after the first, writes nothing. The two
    /// seams that log a frame's failure call it, after their own line.
    /// </summary>
    /// <param name="e">The frame's exception; not null.</param>
    internal static void LogOnce(Exception e) => LogOnce(e, Probe, ref _logged);

    /// <summary>
    /// <see cref="LogOnce(Exception)"/> with its probe and its guard given, so the tests can call
    /// it: at Warn when one of the four does not load, at Info when all four do, the cause then
    /// being elsewhere, and at Warn with the exception when Windows could not be asked. Nothing
    /// thrown reaches the caller but a null argument.
    /// </summary>
    /// <param name="e">The frame's exception; not null.</param>
    /// <param name="probe">Asks Windows for the four DLLs, in the order of <see cref="Files"/>; not null.</param>
    /// <param name="logged">The guard: 0 while nothing was written, set to 1 by the call that writes.</param>
    /// <returns>True when this call wrote the line.</returns>
    internal static bool LogOnce(Exception e, Func<IReadOnlyList<RuntimeDll>> probe, ref int logged)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(probe);
        if (!IsLoadFailure(e) || Interlocked.Exchange(ref logged, 1) != 0)
        {
            return false;
        }

        IReadOnlyList<RuntimeDll> dlls;
        try
        {
            dlls = probe();
        }
        catch (Exception ex)
        {
            // The caller is a frame's failure path, which must go on: the reason goes to the log.
            Log.Warn(ex, "Visual C++ runtime: Windows could not be asked which of its DLLs load");
            return true;
        }

        if (dlls.Any(d => !d.Loads))
        {
            Log.Warn(Line(dlls));
        }
        else
        {
            Log.Info(Line(dlls));
        }

        return true;
    }

    /// <summary>
    /// Asks Windows to load each of <see cref="Files"/> by its name, as LibRaw's DLLs ask for them,
    /// and releases each one that loads at once, after reading where it came from and its file
    /// version.
    /// </summary>
    /// <returns>The four, in the order of <see cref="Files"/>.</returns>
    internal static IReadOnlyList<RuntimeDll> Probe()
    {
        List<RuntimeDll> dlls = new(Files.Count);
        foreach (string file in Files)
        {
            if (!NativeLibrary.TryLoad(file, out IntPtr handle))
            {
                dlls.Add(new RuntimeDll(file, false, null, null));
                continue;
            }

            try
            {
                string? path = LoadedFrom(file);
                dlls.Add(new RuntimeDll(file, true, path, path is null ? null : FileVersionInfo.GetVersionInfo(path).FileVersion));
            }
            finally
            {
                NativeLibrary.Free(handle);
            }
        }

        return dlls;
    }

    /// <summary>
    /// The line, in the log's English: the DLLs that do not load, then each one that loads with
    /// its version and its path, then what follows for LibRaw.
    /// </summary>
    /// <param name="dlls">Windows' answer for the four; not null.</param>
    /// <returns>The line's text, starting <c>Visual C++ runtime: </c>.</returns>
    internal static string Line(IReadOnlyList<RuntimeDll> dlls)
    {
        ArgumentNullException.ThrowIfNull(dlls);
        string[] missing = dlls.Where(d => !d.Loads).Select(d => d.File).ToArray();
        string[] found = dlls.Where(d => d.Loads).Select(d => d.File + (d.Version is null ? "" : " " + d.Version) + (d.Path is null ? "" : " at " + d.Path)).ToArray();
        List<string> parts = [];
        if (missing.Length > 0)
        {
            parts.Add("missing " + string.Join(", ", missing));
        }

        if (found.Length > 0)
        {
            parts.Add(string.Join(", ", found) + " load");
        }

        parts.Add(missing.Length > 0 ? "LibRaw cannot be loaded until the runtime's latest version is installed" : "LibRaw failed to load for another reason");
        return "Visual C++ runtime: " + string.Join("; ", parts);
    }

    /// <summary>Where this process loaded a DLL from, by its file name.</summary>
    /// <param name="file">The DLL's file name.</param>
    /// <returns>Its full path, or null when the process's module list does not name it.</returns>
    private static string? LoadedFrom(string file)
    {
        using Process self = Process.GetCurrentProcess();
        foreach (ProcessModule module in self.Modules)
        {
            using (module)
            {
                if (string.Equals(module.ModuleName, file, StringComparison.OrdinalIgnoreCase))
                {
                    return module.FileName;
                }
            }
        }

        return null;
    }
}
