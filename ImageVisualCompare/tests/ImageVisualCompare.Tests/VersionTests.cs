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
using System.Text.RegularExpressions;
using ImageVisualCompare.Wpf;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The version is bumped by hand. The source of truth is <c>&lt;Version&gt;</c> in
/// Directory.Build.props (ADR 0004) - <see cref="VersionInfo"/> reads it from the assembly and
/// does not hardcode it. These tests check the plumbing; that the notices GPLv3 asks for really
/// are in the About window is <c>AboutWindowTests</c>'s.
/// </summary>
public class VersionTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "") =>
        Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    private static string BuildProps() => File.ReadAllText(Path.Combine(RepoDir(), "Directory.Build.props"));

    [Fact]
    public void R070_VersionIsAThreePartNumber()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", VersionInfo.Value);
    }

    /// <summary>
    /// Checks the plumbing Directory.Build.props -> assembly -> VersionInfo. If someone removes
    /// &lt;Version&gt; or MSBuild stops propagating InformationalVersion, this is where it shows.
    /// </summary>
    [Fact]
    public void R070_TheVersionComesFromTheBuildProps()
    {
        Match m = Regex.Match(BuildProps(), @"<Version>([^<]+)</Version>");

        Assert.True(m.Success, "Directory.Build.props has no <Version>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Value);
    }

    /// <summary>Neither "1.0.0.0" nor "1.0.0+abc123" from the assembly may remain.</summary>
    [Fact]
    public void R070_TheVersionIsNotTheFourPartOrSourceTaggedForm()
    {
        Assert.DoesNotContain("+", VersionInfo.Value);
        Assert.Equal(3, VersionInfo.Value.Split('.').Length);
    }

    /// <summary>
    /// The copyright comes from &lt;Copyright&gt;, just as the version comes from &lt;Version&gt;.
    /// If someone removes it, the fallback in the code is what remains - and this is where it
    /// shows.
    /// </summary>
    [Fact]
    public void R071_TheCopyrightComesFromTheBuildProps()
    {
        Match m = Regex.Match(BuildProps(), @"<Copyright>([^<]+)</Copyright>");

        Assert.True(m.Success, "Directory.Build.props has no <Copyright>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Copyright);
    }

    [Fact]
    public void R071_TheCopyrightNamesTheAuthorAndTheYear()
    {
        Assert.Equal("Copyright (C) 2026 Tudor Carean", VersionInfo.Copyright);
    }

    [Fact]
    public void R073_TheBuildPropsDeclareTheLicence()
    {
        Assert.Contains("<PackageLicenseExpression>GPL-3.0-or-later</PackageLicenseExpression>", BuildProps());
    }

    [Fact]
    public void R070_TheBannerIsTheNameAndTheVersion()
    {
        Assert.Equal($"Image Visual Compare {VersionInfo.Value}", VersionInfo.Banner);
    }
}
