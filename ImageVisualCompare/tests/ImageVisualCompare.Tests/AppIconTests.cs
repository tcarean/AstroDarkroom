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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImageVisualCompare.Wpf;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The application's icon (R-064, ADR 0030): the file in the source tree holds every size
/// Windows asks for, and the build embeds it in the application, so a removed
/// <c>ApplicationIcon</c> or a regenerated file with a size missing fails here rather than as a
/// generic icon in the taskbar.
/// </summary>
public class AppIconTests
{
    private static readonly int[] Sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    private static string IconPath([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!,
            "src", "ImageVisualCompare.Wpf", "Assets", "ImageVisualCompare.ico");

    /// <summary>The edge and bit depth of each entry of an icon directory; 0 means 256.</summary>
    private static List<(int Edge, int Bits)> Entries(byte[] directory, int entrySize)
    {
        var result = new List<(int, int)>();
        int count = BitConverter.ToUInt16(directory, 4);
        for (int i = 0; i < count; i++)
        {
            int at = 6 + i * entrySize;
            int edge = directory[at] == 0 ? 256 : directory[at];
            result.Add((edge, BitConverter.ToUInt16(directory, at + 6)));
        }
        return result;
    }

    [Fact]
    public void R064_IconFileHoldsEverySizeAt32Bits()
    {
        byte[] ico = File.ReadAllBytes(IconPath());

        Assert.Equal(0, BitConverter.ToUInt16(ico, 0));
        Assert.Equal(1, BitConverter.ToUInt16(ico, 2));
        var entries = Entries(ico, 16);
        Assert.Equal(Sizes, entries.Select(e => e.Edge));
        Assert.All(entries, e => Assert.Equal(32, e.Bits));
    }

    [Fact]
    public void R064_ApplicationCarriesTheIcon()
    {
        string assembly = typeof(VersionInfo).Assembly.Location;
        IntPtr module = LoadLibraryEx(assembly, IntPtr.Zero, LoadAsDatafile | LoadAsImageResource);
        Assert.NotEqual(IntPtr.Zero, module);
        try
        {
            var names = new List<IntPtr>();
            EnumResourceNames(module, GroupIcon, (_, _, name, _) => { names.Add(name); return true; }, IntPtr.Zero);
            Assert.True(names.Count == 1, $"{names.Count} icon groups in {assembly}");

            IntPtr found = FindResource(module, names[0], GroupIcon);
            int length = (int)SizeofResource(module, found);
            byte[] group = new byte[length];
            Marshal.Copy(LockResource(LoadResource(module, found)), group, 0, length);

            Assert.Equal(Sizes, Entries(group, 14).Select(e => e.Edge));
        }
        finally
        {
            FreeLibrary(module);
        }
    }

    private const uint LoadAsDatafile = 0x2;
    private const uint LoadAsImageResource = 0x20;
    private static readonly IntPtr GroupIcon = 14;

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
