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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// The version is incremented by hand. The source of truth is <c>&lt;Version&gt;</c> in
/// Directory.Build.props - <see cref="VersionInfo"/> reads it from the assembly, it does
/// not hardcode it. These tests check the plumbing and that the banner really appears on
/// the first line of every run.
/// </summary>
public class VersionTests
{
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    static string BuildProps()
        => File.ReadAllText(Path.Combine(RepoDir(), "Directory.Build.props"));

    [Fact]
    public void R120_VersionIsAThreePartNumber()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", VersionInfo.Value);
    }

    [Fact]
    public void R120_TheVersionComesFromDirectoryBuildProps()
    {
        var m = Regex.Match(BuildProps(), @"<Version>([^<]+)</Version>");

        Assert.True(m.Success, "Directory.Build.props has no <Version>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Value);
    }

    /// <summary>
    /// Every assembly carries the same version. VersionInfo reads it from Core, so a
    /// &lt;Version&gt; left in a csproj would pass unnoticed by the test above and would
    /// give an exe with another version in the file's properties than the one printed.
    /// </summary>
    [Theory]
    [InlineData("AstroPhotoAnalyzer")]
    [InlineData("AstroPhotoAnalyzer.Core")]
    [InlineData("AstroPhotoAnalyzer.Wpf")]
    public void R120_NoProjectOverridesTheSharedVersionOrCopyright(string project)
    {
        string path = Path.Combine(RepoDir(), project, project + ".csproj");
        Assert.True(File.Exists(path), $"{path} does not exist; a renamed project must be renamed here too");

        string csproj = File.ReadAllText(path);

        Assert.DoesNotContain("<Version>", csproj);
        Assert.DoesNotContain("<Copyright>", csproj);
    }

    /// <summary>It must not stay "1.0.0.0" or "1.0.0+abc123" from the assembly.</summary>
    [Fact]
    public void R120_TheVersionIsNotTheFourPartOrSourceTaggedForm()
    {
        Assert.DoesNotContain("+", VersionInfo.Value);
        Assert.Equal(3, VersionInfo.Value.Split('.').Length);
    }

    [Fact]
    public void R120_TheCopyrightComesFromDirectoryBuildProps()
    {
        var m = Regex.Match(BuildProps(), @"<Copyright>([^<]+)</Copyright>");

        Assert.True(m.Success, "Directory.Build.props has no <Copyright>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Copyright);
    }

    [Fact]
    public void R120_TheCopyrightNamesTheAuthorAndTheYear()
    {
        Assert.Equal("Copyright (C) 2026 Tudor Carean", VersionInfo.Copyright);
    }

    // ------------------------------------------------------------ the banner

    [Fact]
    public void R001_TheBannerIsTheNameAndTheVersion()
    {
        Assert.Equal($"AstroPhotoAnalyzer {VersionInfo.Value}", VersionInfo.Banner);
    }

    static string[] Lines(params string[] args)
    {
        var stdout = new StringWriter();
        AnalyzerCli.Run(args, stdout, new StringWriter());
        return stdout.ToString().Replace("\r\n", "\n").Split('\n');
    }

    static string FirstLine(params string[] args) => Lines(args)[0];

    static string SecondLine(params string[] args) => Lines(args)[1];

    [Fact]
    public void R001_HelpStartsWithTheBanner()
    {
        Assert.Equal(VersionInfo.Banner, FirstLine("--help"));
        Assert.Equal(VersionInfo.Banner, FirstLine());
    }

    /// <summary>
    /// GPLv3 wants the copyright notice seen on every run, so it sits on the second line
    /// everywhere the banner sits on the first - --help and errors included.
    /// </summary>
    [Fact]
    public void R001_EveryRunPrintsTheCopyrightOnTheSecondLine()
    {
        using var ws = new TempWorkspace();

        Assert.Equal(VersionInfo.Copyright, SecondLine("--help"));
        Assert.Equal(VersionInfo.Copyright, SecondLine(ws.PathTo("nu-exista.tif")));
        Assert.Equal(VersionInfo.Copyright, SecondLine("--optiune-inventata"));
    }

    /// <summary>A run that fails also says which version failed.</summary>
    [Fact]
    public void R001_AFailedRunStillPrintsTheBanner()
    {
        using var ws = new TempWorkspace();

        Assert.Equal(VersionInfo.Banner, FirstLine(ws.PathTo("nu-exista.tif")));
        Assert.Equal(VersionInfo.Banner, FirstLine("--optiune-inventata"));
    }

    /// <summary>
    /// GPLv3 section 5(d): a program that displays legal notices must also display the
    /// absence of a warranty. It cannot go under the banner - it would thicken every
    /// report - so it sits in --help, and from there points to the full text.
    /// </summary>
    [Fact]
    public void R004_HelpCarriesTheWarrantyDisclaimerAndPointsAtTheLicense()
    {
        string help = string.Join("\n", Lines("--help"));

        Assert.Contains("FARA NICIO GARANTIE", help);
        Assert.Contains("GPLv3", help);
        Assert.Contains("LICENSE", help);
        Assert.Contains("gnu.org/licenses/gpl-3.0.html", help);
    }

    [Fact]
    public void R001_TheBannerIsPrintedExactlyOnce()
    {
        var stdout = new StringWriter();
        AnalyzerCli.Run(new[] { "--help" }, stdout, new StringWriter());

        int count = Regex.Matches(stdout.ToString(), Regex.Escape(VersionInfo.Banner)).Count;
        Assert.Equal(1, count);
    }
}
