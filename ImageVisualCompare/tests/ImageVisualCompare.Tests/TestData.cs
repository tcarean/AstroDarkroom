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

namespace ImageVisualCompare.Tests;

/// <summary>
/// Where the test data outside git is (ADR 0005). Every image the unit tests decode is written at
/// run time by <see cref="TempImages"/>; the only stored data is the real pair, three exports of
/// one frame copied by hand into tests/testdata/large/, which git ignores, and listed with their
/// hashes in tests/testdata/manifest.json. IVC_TESTDATA_LARGE may name another folder, as
/// scripts/check-testdata.ps1 allows; nobody sets it. A validation case whose file is absent
/// returns early with a message instead of failing: xUnit 2 has no dynamic skip, and the copies
/// exist on one machine.
/// </summary>
internal static class TestData
{
    /// <summary>The project folder (the one holding ImageVisualCompare.sln), from this file's own path.</summary>
    internal static string RepoDir([CallerFilePath] string thisFile = "") =>
        Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    /// <summary>The folder the large files live in: IVC_TESTDATA_LARGE, or tests/testdata/large/.</summary>
    internal static string LargeDir()
    {
        string? env = Environment.GetEnvironmentVariable("IVC_TESTDATA_LARGE");
        return string.IsNullOrEmpty(env)
            ? Path.Combine(RepoDir(), "tests", "testdata", "large")
            : env;
    }

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
