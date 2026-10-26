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

namespace CopyWcs.Tests;

/// <summary>Where the test data and the repository are at run time.</summary>
internal static class TestData
{
    /// <summary>The test data folder copied beside the test binaries (the fixtures and the manifest).</summary>
    public static string Root => Path.Combine(AppContext.BaseDirectory, "testdata");

    /// <summary>The path of a fixture in the repo, by file name.</summary>
    public static string Fixture(string name) => Path.Combine(Root, "fixtures", name);

    /// <summary>The folder holding CopyWcs.sln; the build output folder when there is none above it.</summary>
    public static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CopyWcs.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
