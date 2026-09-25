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

using System.Runtime.CompilerServices;

namespace FlatCheck.Tests;

/// <summary>
/// Where the test data outside git is. FlatCheck generates every fixture in the test
/// (<see cref="FitsTestData"/>, <see cref="TestImages"/>); the only stored data are the real
/// masters and raws under <c>E:\Photografie\Astro\Flats and Darks</c>, listed with their
/// hashes in <c>FlatCheck.Tests/testdata/manifest.json</c> and reached through
/// <see cref="Large"/>. A validation test whose file is absent returns early with a message
/// instead of failing: xUnit 2 has no dynamic skip, and the data is on one machine.
/// </summary>
internal static class TestData
{
    /// <summary>The project folder (the one holding FlatCheck.sln), from this file's own path.</summary>
    internal static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    /// <summary>The folder the large files live in: FLATCHECK_TESTDATA_LARGE, or testdata/large/ under the test project.</summary>
    internal static string LargeDir()
        => Environment.GetEnvironmentVariable("FLATCHECK_TESTDATA_LARGE")
           ?? Path.Combine(RepoDir(), "FlatCheck.Tests", "testdata", "large");

    /// <summary>
    /// A large file by its manifest name (a path relative to the large folder, with forward
    /// slashes). Null when it is absent on this machine.
    /// </summary>
    internal static string? Large(string name)
    {
        string path = Path.Combine(LargeDir(), name.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? path : null;
    }
}
