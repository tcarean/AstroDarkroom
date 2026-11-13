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

namespace FlatCheck.Tests;

/// <summary>
/// A second name (a hard link) for a file, which is how a test sees whether a write went
/// into the existing file or replaced it (R-138): bytes written into the file show under both
/// names, while a file moved over the first name leaves the second one holding the old bytes.
/// A reader that holds the old file open cannot show it on Windows, where an open file is not
/// replaced by a move at all.
/// </summary>
internal static class SecondName
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateHardLinkW(string newName, string existing, IntPtr security);

    [DllImport("libc", SetLastError = true)]
    static extern int link(string existing, string newName);

    /// <summary>Gives an existing file a second name in the same folder.</summary>
    /// <returns>The second name's full path.</returns>
    internal static string Of(string existing)
    {
        string second = existing + ".second-name";
        bool ok = OperatingSystem.IsWindows() ? CreateHardLinkW(second, existing, IntPtr.Zero) : link(existing, second) == 0;
        if (!ok) throw new IOException("no hard link for " + existing + ", error " + Marshal.GetLastWin32Error());
        return second;
    }
}
