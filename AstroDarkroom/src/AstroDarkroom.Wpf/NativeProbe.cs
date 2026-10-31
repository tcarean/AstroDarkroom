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

using System.IO;
using System.Runtime.InteropServices;
using AstroDarkroom.Core;

namespace AstroDarkroom.Wpf;

/// <summary>
/// Asks Windows whether it loads the Visual C++ runtime's DLLs, given by their file names, from
/// where a tool the launcher starts finds them: the suite folder, then Windows' standard search,
/// with the suite folder as the current folder, as the launcher gives it to every tool. That is
/// where LibRaw's DLLs find the runtime when a tool loads them, so the answer is the tools' (R-018).
/// </summary>
internal static class NativeProbe
{
    /// <summary>
    /// The runtime's DLLs Windows does not load (<see cref="VcRuntime.Missing"/>), asked with the
    /// suite folder as the process's current folder, then the current folder put back. A folder
    /// Windows will not make current leaves the asking where it is: the launcher never stops on it.
    /// </summary>
    /// <returns>The files that do not load, in the order of <see cref="VcRuntime.Files"/>; empty when the runtime is there.</returns>
    internal static IReadOnlyList<string> MissingRuntime()
    {
        string before = Environment.CurrentDirectory;
        bool moved = TryMakeCurrent(AppContext.BaseDirectory);
        try
        {
            return VcRuntime.Missing(CanLoad);
        }
        finally
        {
            if (moved)
            {
                // The launcher reads no relative path, so a folder that cannot be put back costs nothing.
                TryMakeCurrent(before);
            }
        }
    }

    /// <summary>Whether Windows loads the DLL; a DLL that loads is released at once.</summary>
    /// <param name="file">The DLL's file name, such as <c>vcomp140.dll</c>; a name, not a path.</param>
    /// <returns>True when it loads.</returns>
    private static bool CanLoad(string file)
    {
        if (!NativeLibrary.TryLoad(file, out IntPtr handle))
        {
            return false;
        }

        NativeLibrary.Free(handle);
        return true;
    }

    /// <summary>Makes a folder the process's current folder, when Windows allows it.</summary>
    /// <param name="folder">The folder, an absolute path.</param>
    /// <returns>True when it is the current folder now.</returns>
    private static bool TryMakeCurrent(string folder)
    {
        try
        {
            Environment.CurrentDirectory = folder;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
