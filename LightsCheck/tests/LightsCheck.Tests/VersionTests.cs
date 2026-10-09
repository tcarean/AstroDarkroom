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
using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// The version is bumped by the owner in Directory.Build.props (the kit's ADR 0029);
/// VersionInfo reads it from the assembly. These tests check the plumbing and that the banner
/// and the copyright open every run (R-001, R-011).
/// </summary>
public partial class VersionTests
{
    private static string BuildProps() => File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "Directory.Build.props"));

    [Fact]
    public void R011_TheVersionComesFromDirectoryBuildProps()
    {
        Match m = VersionElement().Match(BuildProps());
        Assert.True(m.Success, "Directory.Build.props has no <Version>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Value);
        Assert.Matches(@"^\d+\.\d+\.\d+$", VersionInfo.Value);
    }

    [Fact]
    public void R011_TheCopyrightComesFromDirectoryBuildProps()
    {
        Match m = CopyrightElement().Match(BuildProps());
        Assert.True(m.Success, "Directory.Build.props has no <Copyright>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Copyright);
        Assert.Equal("Copyright (C) 2026 Tudor Carean", VersionInfo.Copyright);
    }

    [Theory]
    [InlineData("src/LightsCheck.Core/LightsCheck.Core.csproj")]
    [InlineData("src/LightsCheck.Cli/LightsCheck.Cli.csproj")]
    [InlineData("src/LightsCheck.Wpf/LightsCheck.Wpf.csproj")]
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
        Assert.Equal($"lightscheck {VersionInfo.Value}", VersionInfo.Banner);
    }

    [Fact]
    public void R001_EveryRunPrintsTheBannerAndTheCopyrightFirst()
    {
        using SessionFixture s = new SessionFixture().WithLights(1);
        using TempWorkspace ws = new();
        string[][] runs =
        [
            [],
            ["--help"],
            [s.Root],
            [ws.PathTo("nu-exista")],
            ["--optiune-inventata"],
        ];
        foreach (string[] args in runs)
        {
            string[] lines = Cli.Run(args).Stdout.Split('\n');
            Assert.Equal(VersionInfo.Banner, lines[0]);
            Assert.Equal(VersionInfo.Copyright, lines[1]);
        }
    }

    [Fact]
    public void TheBannerIsPrintedExactlyOnce()
    {
        string stdout = Cli.Run("--help").Stdout;
        Assert.Single(Regex.Matches(stdout, Regex.Escape(VersionInfo.Banner)));
    }

    [GeneratedRegex(@"<Version>([^<]+)</Version>")]
    private static partial Regex VersionElement();

    [GeneratedRegex(@"<Copyright>([^<]+)</Copyright>")]
    private static partial Regex CopyrightElement();
}
