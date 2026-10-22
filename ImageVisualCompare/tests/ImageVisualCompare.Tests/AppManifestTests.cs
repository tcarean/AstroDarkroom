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
using System.Text;
using ImageVisualCompare.Wpf;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The manifest the build embeds in the application (R-022, ADR 0034). Windows reads it from the
/// executable when the process starts, so what counts is what the built files carry, read here
/// as <see cref="AppIconTests"/> reads the icon.
/// </summary>
public class AppManifestTests
{
    [Fact]
    public void R022_TheApplicationDeclaresPerMonitorDpiAwareness()
    {
        string assembly = typeof(VersionInfo).Assembly.Location;
        string executable = Path.ChangeExtension(assembly, ".exe");

        foreach (string file in new[] { assembly, executable })
        {
            string manifest = Manifest(file);

            Assert.Contains("PerMonitorV2", manifest);
            Assert.Contains("true/pm", manifest, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The text of the one manifest a built file carries.</summary>
    private static string Manifest(string file)
    {
        IntPtr module = LoadLibraryEx(file, IntPtr.Zero, LoadAsDatafile | LoadAsImageResource);
        Assert.NotEqual(IntPtr.Zero, module);
        try
        {
            var names = new List<IntPtr>();
            EnumResourceNames(module, ManifestType, (_, _, name, _) => { names.Add(name); return true; }, IntPtr.Zero);
            Assert.True(names.Count == 1, $"{names.Count} manifests in {file}");

            IntPtr found = FindResource(module, names[0], ManifestType);
            int length = (int)SizeofResource(module, found);
            byte[] bytes = new byte[length];
            Marshal.Copy(LockResource(LoadResource(module, found)), bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            FreeLibrary(module);
        }
    }

    private const uint LoadAsDatafile = 0x2;
    private const uint LoadAsImageResource = 0x20;
    private static readonly IntPtr ManifestType = 24;

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
