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

namespace ImageVisualCompare.Tests;

/// <summary>
/// Reads one Win32 resource out of a built file, the way Windows reads the icon and the
/// manifest the build embeds: the file is loaded as data, never run.
/// </summary>
internal static class Win32Resources
{
    /// <summary>The resource type of an icon group (RT_GROUP_ICON).</summary>
    public static readonly IntPtr GroupIcon = 14;

    /// <summary>The resource type of a manifest (RT_MANIFEST).</summary>
    public static readonly IntPtr Manifest = 24;

    /// <summary>The bytes of the one resource of a type a file carries; fails when it carries
    /// none or several.</summary>
    public static byte[] ReadSingle(string file, IntPtr type)
    {
        IntPtr module = LoadLibraryEx(file, IntPtr.Zero, LoadAsDatafile | LoadAsImageResource);
        Assert.NotEqual(IntPtr.Zero, module);
        try
        {
            var names = new List<IntPtr>();
            EnumResourceNames(module, type, (_, _, name, _) => { names.Add(name); return true; }, IntPtr.Zero);
            Assert.True(names.Count == 1, $"{names.Count} resources of type {type} in {file}");

            IntPtr found = FindResource(module, names[0], type);
            int length = (int)SizeofResource(module, found);
            byte[] bytes = new byte[length];
            Marshal.Copy(LockResource(LoadResource(module, found)), bytes, 0, length);
            return bytes;
        }
        finally
        {
            FreeLibrary(module);
        }
    }

    private const uint LoadAsDatafile = 0x2;
    private const uint LoadAsImageResource = 0x20;

    private delegate bool EnumResNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr param);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr module);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumResNameProc callback, IntPtr param);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);

    [DllImport("kernel32.dll")]
    private static extern uint SizeofResource(IntPtr module, IntPtr resource);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LockResource(IntPtr data);
}
