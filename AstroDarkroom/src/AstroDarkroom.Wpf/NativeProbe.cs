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

using System.Runtime.InteropServices;

namespace AstroDarkroom.Wpf;

/// <summary>
/// Asks Windows whether it loads a DLL given by its file name, from where a program in the suite
/// folder finds it: the launcher's own folder, then Windows' standard search. That is where LibRaw's
/// DLLs find the Visual C++ runtime when a tool loads them, so the answer is the tools' (R-018).
/// </summary>
internal static class NativeProbe
{
    /// <summary>Whether Windows loads the DLL; a DLL that loads is released at once.</summary>
    /// <param name="file">The DLL's file name, such as <c>vcomp140.dll</c>; a name, not a path.</param>
    /// <returns>True when it loads.</returns>
    internal static bool CanLoad(string file)
    {
        if (!NativeLibrary.TryLoad(file, out IntPtr handle))
        {
            return false;
        }

        NativeLibrary.Free(handle);
        return true;
    }
}
