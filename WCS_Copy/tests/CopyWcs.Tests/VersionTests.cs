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

namespace CopyWcs.Tests;

/// <summary>
/// The version is bumped by hand, and the source of truth is <c>&lt;Version&gt;</c> in
/// Directory.Build.props; <see cref="VersionInfo"/> reads it from the assembly, never hardcodes it. These
/// tests check that plumbing, and that the banner really appears on the first line of every
/// run: otherwise a pasted output no longer says which version produced it.
/// </summary>
public class VersionTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    private static string[] Lines(params string[] args)
    {
        var stdout = new StringWriter();
        CopyWcsCli.Run(args, stdout, new StringWriter());
        return stdout.ToString().Replace("\r\n", "\n").Split('\n');
    }

    private static string FirstLine(params string[] args) => Lines(args)[0];

    [Fact]
    public void VersionIsAThreePartNumber()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", VersionInfo.Value);
    }

    /// <summary>
    /// Checks the plumbing Directory.Build.props -> assembly -> VersionInfo. If someone removes
    /// &lt;Version&gt; or MSBuild stops propagating InformationalVersion, it shows here.
    /// </summary>
    [Fact]
    public void TheVersionComesFromTheBuildProps()
    {
        var props = File.ReadAllText(Path.Combine(RepoDir(), "Directory.Build.props"));

        var m = Regex.Match(props, @"<Version>([^<]+)</Version>");

        Assert.True(m.Success, "Directory.Build.props has no <Version>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Value);
    }

    /// <summary>Neither "1.0.0.0" nor "1.0.0+abc123" from the assembly may remain.</summary>
    [Fact]
    public void TheVersionIsNotTheFourPartOrSourceTaggedForm()
    {
        Assert.DoesNotContain("+", VersionInfo.Value);
        Assert.Equal(3, VersionInfo.Value.Split('.').Length);
    }

    [Fact]
    public void TheBannerIsTheNameAndTheVersion()
    {
        Assert.Equal($"copywcs {VersionInfo.Value}", VersionInfo.Banner);
    }

    [Fact]
    public void R001_EveryRunPrintsTheBannerOnTheFirstLine()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(400);
        var source = ws.WriteFile("source.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var dest = ws.WriteFile("dest.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));

        Assert.Equal(VersionInfo.Banner, FirstLine(source, dest, "--dry-run"));
        Assert.Equal(VersionInfo.Banner, FirstLine(source, dest, "-o", ws.FilePath("out.fit")));
        Assert.Equal(VersionInfo.Banner, FirstLine("--help"));
    }

    /// <summary>A failed run too says which version failed.</summary>
    [Fact]
    public void AFailedRunStillPrintsTheBanner()
    {
        using var ws = new TempWorkspace();

        Assert.Equal(VersionInfo.Banner,
            FirstLine(ws.FilePath("missing.fit"), ws.FilePath("also-missing.fit")));
        Assert.Equal(VersionInfo.Banner, FirstLine("--optiune-inventata"));
        Assert.Equal(VersionInfo.Banner, FirstLine("un-singur-argument"));
    }

    [Fact]
    public void TheBannerIsPrintedExactlyOnce()
    {
        var stdout = new StringWriter();
        CopyWcsCli.Run(["--help"], stdout, new StringWriter());

        var count = Regex.Count(stdout.ToString(), Regex.Escape(VersionInfo.Banner));
        Assert.Equal(1, count);
    }

    /// <summary>
    /// There is no --version: the banner on every run makes it redundant, and one more flag
    /// would be one more exit path to maintain. It stays an unknown option.
    /// </summary>
    [Fact]
    public void ThereIsNoVersionFlag()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Options.Parse(["a.fit", "b.fit", "--version"]));

        Assert.Contains("Optiune necunoscuta", ex.Message);
    }
}
