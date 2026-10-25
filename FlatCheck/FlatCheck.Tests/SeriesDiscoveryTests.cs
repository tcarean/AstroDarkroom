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

namespace FlatCheck.Tests;

/// <summary>
/// The choice of files for --light on a folder.
///
/// Two traps are pinned here, and both would pass unnoticed in production.
///
/// The first: <see cref="ThePatternDoesNotLeakIntoLongerExtensions"/>. Directory.GetFiles
/// with a pattern uses Win32 matching, which from the 8.3 legacy also catches longer
/// extensions - "*.fit" would swallow ".fits" too. That is why the enumeration is complete
/// and the matching is done in process.
///
/// The second: <see cref="RegisteredFramesAreLeftOutByTheDefaultPattern"/>. The frames
/// r_pp_light_* are registered, that is geometrically transformed, so the flat's error -
/// fixed on the sensor - is moved relative to the centre around which the radial model is
/// fitted. Including them would spoil the measurement silently.
/// </summary>
public class SeriesDiscoveryTests
{
    // ------------------------------------------------------------ the matching

    [Theory]
    [InlineData("pp_light_00001.fit", "pp_light_*.fit", true)]
    [InlineData("pp_light_00042.fit", "pp_light_*.fit", true)]
    [InlineData("r_pp_light_00001.fit", "pp_light_*.fit", false)]   // the pattern is anchored at the start
    [InlineData("light_00001.fit", "pp_light_*.fit", false)]
    [InlineData("pp_flat_stacked.fit", "pp_light_*.fit", false)]
    [InlineData("pp_light_00001.fits", "pp_light_*.fit", false)]
    [InlineData("PP_LIGHT_00001.FIT", "pp_light_*.fit", true)]      // case-insensitive
    [InlineData("a.fit", "?.fit", true)]
    [InlineData("ab.fit", "?.fit", false)]
    [InlineData("orice", "*", true)]
    [InlineData("", "*", true)]
    [InlineData("", "?", false)]
    public void TheGlobMatcherHandlesStarAndQuestionMark(string name, string pattern, bool expected)
    {
        Assert.Equal(expected, SeriesFinder.GlobMatch(name, pattern));
    }

    /// <summary>
    /// Character classes are not supported - they are treated LITERALLY, in both
    /// implementations. If the C# ignored them and the Python interpreted them (fnmatch
    /// does), the two would pick different files from the same folder, and the crosscheck
    /// would report numeric differences that are in fact input differences.
    /// </summary>
    [Fact]
    public void CharacterClassesAreLiteralNotWildcards()
    {
        Assert.True(SeriesFinder.GlobMatch("[ab].fit", "[ab].fit"));
        Assert.False(SeriesFinder.GlobMatch("a.fit", "[ab].fit"));
    }

    /// <summary>The backtracking: several stars and a tail that must match.</summary>
    [Theory]
    [InlineData("pp_light_00001.fit", "*light*.fit", true)]
    [InlineData("pp_light_00001.fit", "*light*.png", false)]
    [InlineData("aaa.fit", "*a.fit", true)]
    [InlineData("aaab.fit", "*a.fit", false)]
    public void TheMatcherBacktracksCorrectly(string name, string pattern, bool expected)
    {
        Assert.Equal(expected, SeriesFinder.GlobMatch(name, pattern));
    }

    // ------------------------------------------------------------ the enumeration

    [Fact]
    public void ThePatternDoesNotLeakIntoLongerExtensions()
    {
        using var ws = new TempWorkspace();
        ws.WriteFile("pp_light_00001.fit", new byte[] { 1 });
        ws.WriteFile("pp_light_00002.fits", new byte[] { 1 });

        var found = SeriesFinder.Find(ws.Dir, "pp_light_*.fit");

        Assert.Single(found);
        Assert.EndsWith("pp_light_00001.fit", found[0]);
    }

    [Fact]
    public void RegisteredFramesAreLeftOutByTheDefaultPattern()
    {
        using var ws = new TempWorkspace();
        foreach (string n in new[]
                 { "pp_light_00001.fit", "pp_light_00002.fit", "r_pp_light_00001.fit",
                   "light_00001.fit", "bias_00001.fit", "pp_light_.seq" })
            ws.WriteFile(n, new byte[] { 1 });

        var found = SeriesFinder.Find(ws.Dir, "pp_light_*.fit");

        Assert.Equal(2, found.Count);
        Assert.All(found, p => Assert.StartsWith("pp_light_", Path.GetFileName(p)));
    }

    /// <summary>
    /// The order is ordinal on the file name, so that the window (with culture data) and
    /// the console (without) list the series the same way. The names here hold only digits,
    /// so the test pins the order, not the ordinal comparison: under InvariantGlobalization
    /// (ADR 0043) a cultural sort would give the same result.
    /// </summary>
    [Fact]
    public void FilesComeBackSortedOrdinallyByName()
    {
        using var ws = new TempWorkspace();
        foreach (string n in new[] { "pp_light_00010.fit", "pp_light_00002.fit", "pp_light_00001.fit" })
            ws.WriteFile(n, new byte[] { 1 });

        var names = SeriesFinder.Find(ws.Dir, "pp_light_*.fit")
            .Select(Path.GetFileName).ToArray();

        Assert.Equal(new[] { "pp_light_00001.fit", "pp_light_00002.fit", "pp_light_00010.fit" }, names);
    }

    [Fact]
    public void AFolderWithNoMatchesComesBackEmpty()
    {
        using var ws = new TempWorkspace();
        ws.WriteFile("bias_00001.fit", new byte[] { 1 });

        Assert.Empty(SeriesFinder.Find(ws.Dir, "pp_light_*.fit"));
    }

    /// <summary>Subfolders are not descended into: process/cache/ holds no frames to
    /// analyse.</summary>
    [Fact]
    public void SubdirectoriesAreNotDescendedInto()
    {
        using var ws = new TempWorkspace();
        Directory.CreateDirectory(Path.Combine(ws.Dir, "cache"));
        File.WriteAllBytes(Path.Combine(ws.Dir, "cache", "pp_light_00001.fit"), new byte[] { 1 });

        Assert.Empty(SeriesFinder.Find(ws.Dir, "pp_light_*.fit"));
    }
}
