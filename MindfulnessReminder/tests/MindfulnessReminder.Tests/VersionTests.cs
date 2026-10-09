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
using System.Text.RegularExpressions;
using MindfulnessReminder.Core;

namespace MindfulnessReminder.Tests;

/// <summary>
/// The version is bumped by hand. The source of truth is <c>&lt;Version&gt;</c> in
/// Directory.Build.props -- <see cref="VersionInfo"/> reads it back out of the assembly
/// rather than hardcoding it -- so what these tests guard is the plumbing between the two,
/// the shape of what comes out, and the fact that the banner actually reaches the user.
/// </summary>
public class VersionTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    [Fact]
    public void R090_The_version_is_a_three_part_number()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", VersionInfo.Value);
    }

    /// <summary>
    /// Checks the build props -> assembly -> VersionInfo chain. If someone drops
    /// &lt;Version&gt;, or MSBuild stops propagating InformationalVersion, it shows up here.
    /// </summary>
    [Fact]
    public void R090_The_version_comes_from_the_build_props()
    {
        var props = File.ReadAllText(Path.Combine(RepoDir(), "Directory.Build.props"));

        var match = Regex.Match(props, "<Version>([^<]+)</Version>");

        Assert.True(match.Success, "Directory.Build.props has no <Version>");
        Assert.Equal(match.Groups[1].Value.Trim(), VersionInfo.Value);
    }

    /// <summary>Neither "1.0.0.0" nor "1.0.0+abc123" is meant to survive out of the assembly.</summary>
    [Fact]
    public void R090_The_version_is_not_the_four_part_or_source_tagged_form()
    {
        Assert.DoesNotContain("+", VersionInfo.Value);
        Assert.Equal(3, VersionInfo.Value.Split('.').Length);
    }

    /// <summary>
    /// One version for the whole solution. A &lt;Version&gt; in any project file would
    /// override the shared one for that assembly, and the three would silently drift.
    /// </summary>
    [Fact]
    public void R090_No_project_overrides_the_shared_version()
    {
        var root = RepoDir();

        var offenders = Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), "<Version>"))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "these projects declare their own <Version>:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void R090_The_banner_is_the_name_and_the_version()
    {
        Assert.Equal("Mindfulness Reminder " + VersionInfo.Value, VersionInfo.Banner);
    }

    /// <summary>
    /// There is no console to print a banner on, so About is where the app identifies itself:
    /// the banner first, the copyright under it, the disclaimer below. Same shape as the two
    /// header lines the command-line tools print on every run.
    /// </summary>
    [Fact]
    public void R091_About_opens_with_the_banner_and_the_copyright()
    {
        var lines = LicenseInfo.AboutText.Replace("\r\n", "\n").Split('\n');

        Assert.Equal(VersionInfo.Banner, lines[0]);
        Assert.Equal(LicenseInfo.Copyright, lines[1]);
    }
}
