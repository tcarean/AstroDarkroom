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

using System.Text.RegularExpressions;
using AstroDarkroom.Core;

namespace AstroDarkroom.Tests;

/// <summary>
/// The version is bumped by the owner in Directory.Build.props (the kit's ADR 0029) and is the
/// suite's version (ADR 0002); VersionInfo reads it from the assembly. These tests check the
/// plumbing and the banner the "Despre" window shows.
/// </summary>
public partial class VersionTests
{
    private static string BuildProps() => File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "Directory.Build.props"));

    [Fact]
    public void TheVersionComesFromDirectoryBuildProps()
    {
        Match m = VersionElement().Match(BuildProps());
        Assert.True(m.Success, "Directory.Build.props has no <Version>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Value);
        Assert.Matches(@"^\d+\.\d+\.\d+$", VersionInfo.Value);
    }

    [Fact]
    public void TheCopyrightComesFromDirectoryBuildProps()
    {
        Match m = CopyrightElement().Match(BuildProps());
        Assert.True(m.Success, "Directory.Build.props has no <Copyright>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Copyright);
        Assert.Equal("Copyright (C) 2026 Tudor Carean", VersionInfo.Copyright);
    }

    [Theory]
    [InlineData("src/AstroDarkroom.Core/AstroDarkroom.Core.csproj")]
    [InlineData("src/AstroDarkroom.Wpf/AstroDarkroom.Wpf.csproj")]
    public void NoProjectOverridesTheSharedVersionOrCopyright(string project)
    {
        string path = Path.Combine(TestData.FindRepoRoot(), project);
        if (!File.Exists(path))
        {
            return;   // the window arrives in a later step
        }

        string csproj = File.ReadAllText(path);
        Assert.DoesNotContain("<Version>", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("<Copyright>", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBannerIsTheNameAndTheVersion()
    {
        Assert.Equal($"Astro Darkroom {VersionInfo.Value}", VersionInfo.Banner);
    }

    [GeneratedRegex(@"<Version>([^<]+)</Version>")]
    private static partial Regex VersionElement();

    [GeneratedRegex(@"<Copyright>([^<]+)</Copyright>")]
    private static partial Regex CopyrightElement();
}
