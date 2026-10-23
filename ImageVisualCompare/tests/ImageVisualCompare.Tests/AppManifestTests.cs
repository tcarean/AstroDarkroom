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
using System.Text;
using ImageVisualCompare.Wpf;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The manifest the build embeds in the application (R-022, ADR 0034). Windows reads it from the
/// executable when the process starts, so what counts is what the built files carry, read here
/// through <see cref="Win32Resources"/>, as <see cref="AppIconTests"/> reads the icon.
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
            string manifest = Encoding.UTF8.GetString(Win32Resources.ReadSingle(file, Win32Resources.Manifest));

            Assert.Contains("PerMonitorV2", manifest);
            Assert.Contains("true/pm", manifest, StringComparison.OrdinalIgnoreCase);
        }
    }
}
