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

namespace AstroDarkroom.Tests;

/// <summary>Where the test data is at run time, and where the project and its siblings are on disk.</summary>
internal static class TestData
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "testdata");

    public static string Fixture(string name) => Path.Combine(Root, "fixtures", name);

    /// <summary>
    /// A large file that is not in the repo. Null when absent, so a test can return early with a
    /// clear message instead of failing on a machine without the data.
    /// </summary>
    public static string? Large(string name)
    {
        string dir = Environment.GetEnvironmentVariable("ASTRODARKROOM_TESTDATA_LARGE")
                     ?? Path.Combine(FindRepoRoot(), "tests", "testdata", "large");
        string path = Path.Combine(dir, name);
        return File.Exists(path) ? path : null;
    }

    /// <summary>The folder holding AstroDarkroom.sln; the build output folder when there is none.</summary>
    public static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AstroDarkroom.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
