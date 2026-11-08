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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// Where the test data outside git is (ADR 0004, ADR 0088). Every fixture of the unit and golden
/// tests is generated or hand-built in the test; the only stored data is the reference images
/// and the lab's two ICC profiles, kept outside the repository in the owner's folder
/// <see cref="DefaultLargeDir"/> and listed with their hashes in testdata/manifest.json.
/// APA_TESTDATA_LARGE may name another folder, as scripts/check-testdata.ps1 allows. A
/// validation case whose file is absent returns early with a message instead of failing:
/// xUnit 2 has no dynamic skip, and the files exist on one machine.
/// </summary>
internal static class TestData
{
    /// <summary>
    /// The folder the large files live in when APA_TESTDATA_LARGE is unset: the owner's folder
    /// for sets kept for the tests alone, the same one from the main checkout and from every
    /// worktree, so no task copies the files (ADR 0088). Absent on any other machine, where the
    /// cases report themselves not measured.
    /// </summary>
    internal const string DefaultLargeDir = @"E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer";

    /// <summary>The project folder (the one holding AstroPhotoAnalyzer.sln), from this file's own path.</summary>
    internal static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    /// <summary>The folder the large files live in: APA_TESTDATA_LARGE when set, else <see cref="DefaultLargeDir"/>.</summary>
    /// <returns>A folder path; it may not exist on this machine.</returns>
    internal static string LargeDir()
    {
        string? env = Environment.GetEnvironmentVariable("APA_TESTDATA_LARGE");
        return string.IsNullOrEmpty(env) ? DefaultLargeDir : env;
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
