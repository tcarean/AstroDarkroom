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

namespace FocalLenghChecker.Tests;

/// <summary>
/// The version is bumped by hand, and the source of truth is the csproj -
/// <see cref="VersionInfo"/> reads it back out of the assembly rather than hardcoding
/// it. These tests pin that plumbing, and the shape of the banner built from it.
/// </summary>
public class VersionTests
{
    /// <summary>The folder holding the .sln - two levels up from this file.</summary>
    private static string SolutionDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    [Fact]
    public void The_version_is_a_three_part_number()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", VersionInfo.Value);
    }

    /// <summary>
    /// Checks the csproj -> assembly -> VersionInfo plumbing. If someone removes
    /// &lt;Version&gt;, or MSBuild stops propagating InformationalVersion, this is where
    /// it shows rather than in a quietly wrong banner.
    /// </summary>
    [Fact]
    public void The_version_comes_from_the_csproj()
    {
        var csproj = File.ReadAllText(
            Path.Combine(SolutionDir(), "FocalLenghChecker", "FocalLenghChecker.csproj"));

        var match = Regex.Match(csproj, @"<Version>([^<]+)</Version>");

        Assert.True(match.Success, "the csproj has no <Version>");
        Assert.Equal(match.Groups[1].Value.Trim(), VersionInfo.Value);
    }

    /// <summary>Neither "1.0.0.0" nor "1.0.0+abc123" may survive out of the assembly.</summary>
    [Fact]
    public void The_version_is_not_the_four_part_or_source_tagged_form()
    {
        Assert.DoesNotContain("+", VersionInfo.Value);
        Assert.Equal(3, VersionInfo.Value.Split('.').Length);
    }

    /// <summary>
    /// The MetadataExtractor package reference also carries a Version attribute, so a
    /// regex over the csproj could match the wrong one. Pin the shipped value.
    /// </summary>
    [Fact]
    public void The_version_is_not_the_dependency_version()
    {
        Assert.NotEqual("2.9.3", VersionInfo.Value);
    }

    [Fact]
    public void The_banner_is_the_name_and_the_version()
    {
        Assert.Equal($"FocalLenghChecker {VersionInfo.Value}", VersionInfo.Banner);
    }

    /// <summary>The name in the banner is the one the usage text tells people to type.</summary>
    [Fact]
    public void The_banner_uses_the_name_of_the_executable()
    {
        Assert.StartsWith("FocalLenghChecker ", VersionInfo.Banner);
        Assert.Contains("FocalLenghChecker [SourceFolderPath]", CommandLineParser.Usage);
    }

    /// <summary>
    /// There is no --version flag, and there should not be one: the banner is on the
    /// first line of every run already, so a flag would only be a second way to ask.
    /// </summary>
    [Fact]
    public void There_is_no_version_flag()
    {
        var parsed = CommandLineParser.Parse(new[] { "--version" }, @"C:\anywhere");

        Assert.Equal("Unknown option '--version'.", parsed.Error);
    }
}
