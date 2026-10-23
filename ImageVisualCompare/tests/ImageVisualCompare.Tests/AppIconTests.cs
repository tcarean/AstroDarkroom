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
        byte[] group = Win32Resources.ReadSingle(typeof(VersionInfo).Assembly.Location, Win32Resources.GroupIcon);

        Assert.Equal(Sizes, Entries(group, 14).Select(e => e.Edge));
    }
}
