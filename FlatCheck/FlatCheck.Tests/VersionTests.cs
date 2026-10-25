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

namespace FlatCheck.Tests;

/// <summary>
/// The version is incremented by hand. The source of truth is <c>&lt;Version&gt;</c> in
/// Directory.Build.props - <see cref="VersionInfo"/> reads it from the assembly, it does
/// not hard-code it. These tests check the plumbing, and that the banner really appears on
/// the first line of every run. (The prototype flatcheck.py is frozen at 2.2.0 and is no
/// longer kept in step; ADR 0047.)
/// </summary>
public class VersionTests
{
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    static string BuildProps()
        => File.ReadAllText(Path.Combine(RepoDir(), "Directory.Build.props"));

    [Fact]
    public void VersionIsAThreePartNumber()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", VersionInfo.Value);
    }

    /// <summary>
    /// The source of truth, since the solution has three assemblies, is
    /// Directory.Build.props - not the csproj of any one project. Checks the plumbing
    /// props -> assembly -> VersionInfo: if someone removes &lt;Version&gt; from there, or
    /// moves it back into a csproj (where it would hold for one project only), it shows
    /// here.
    /// </summary>
    [Fact]
    public void R097_TheVersionComesFromDirectoryBuildProps()
    {
        var m = Regex.Match(BuildProps(), @"<Version>([^<]+)</Version>");

        Assert.True(m.Success, "Directory.Build.props nu are <Version>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Value);
    }

    /// <summary>
    /// All three assemblies carry the same version. VersionInfo reads it from Core, so a
    /// &lt;Version&gt; left in a csproj would pass unnoticed by the test above and would give
    /// an exe with another version in the file's properties than the one it prints itself.
    /// </summary>
    [Theory]
    [InlineData("FlatCheck")]
    [InlineData("FlatCheck.Core")]
    [InlineData("FlatCheck.Wpf")]
    public void NoProjectOverridesTheSharedVersionOrCopyright(string project)
    {
        string path = Path.Combine(RepoDir(), project, project + ".csproj");
        if (!File.Exists(path)) return;   // proiectul WPF apare abia in faza 2

        string csproj = File.ReadAllText(path);

        Assert.DoesNotContain("<Version>", csproj);
        Assert.DoesNotContain("<Copyright>", csproj);
    }

    /// <summary>"1.0.1.0" or "1.0.1+abc123" from the assembly must not remain.</summary>
    [Fact]
    public void TheVersionIsNotTheFourPartOrSourceTaggedForm()
    {
        Assert.DoesNotContain("+", VersionInfo.Value);
        Assert.Equal(3, VersionInfo.Value.Split('.').Length);
    }

    /// <summary>
    /// The copyright comes from &lt;Copyright&gt; in the csproj, just as the version comes
    /// from &lt;Version&gt;. If someone removes it, the fallback in the code remains - and it
    /// shows here.
    /// </summary>
    [Fact]
    public void TheCopyrightComesFromDirectoryBuildProps()
    {
        var m = Regex.Match(BuildProps(), @"<Copyright>([^<]+)</Copyright>");

        Assert.True(m.Success, "Directory.Build.props nu are <Copyright>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Copyright);
    }

    // ------------------------------------------------------------ the banner

    [Fact]
    public void TheBannerIsTheNameAndTheVersion()
    {
        Assert.Equal($"flatcheck {VersionInfo.Value}", VersionInfo.Banner);
    }

    static string[] Lines(params string[] args)
    {
        var stdout = new StringWriter();
        FlatCheckCli.Run(args, stdout, new StringWriter());
        return stdout.ToString().Replace("\r\n", "\n").Split('\n');
    }

    static string FirstLine(params string[] args) => Lines(args)[0];

    static string SecondLine(params string[] args) => Lines(args)[1];

    [Fact]
    public void R001_EveryModePrintsTheBannerOnTheFirstLine()
    {
        using var ws = new TempWorkspace();
        string flat = ws.WriteFile("f.fit",
            FitsTestData.BuildFits(128, 128, FitsTestData.Radial(128, 128)));
        string png = ws.PathTo("out.png");

        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "-o", png));
        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "--ratio", flat, "-o", png));
        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "--light", "-o", png));
        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "--expo"));
        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "--histeq", "-o", png));
        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "--smooth", ws.PathTo("s.fit"), "--scale", "50", "-o", png));
        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "--smooth", ws.PathTo("s.fit"), "--scale", "auto", "-o", png));
        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "--synthetic", ws.PathTo("syn.fit"), "--lens", "Tokina AT-X 11-20mm F2.8 PRO DX", "--profiles", ws.Dir));
        Assert.Equal(VersionInfo.Banner, FirstLine("--lenses", "--profiles", ws.Dir));
        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "--against", flat, "-o", png));
    }

    /// <summary>
    /// GPLv3 wants the copyright notice visible at every run, so it sits on the second line
    /// everywhere the banner sits on the first - including at --help and on errors.
    /// </summary>
    [Fact]
    public void R001_EveryModePrintsTheCopyrightOnTheSecondLine()
    {
        using var ws = new TempWorkspace();
        string flat = ws.WriteFile("f.fit",
            FitsTestData.BuildFits(128, 128, FitsTestData.Radial(128, 128)));
        string png = ws.PathTo("out.png");

        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "-o", png));
        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "--ratio", flat, "-o", png));
        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "--light", "-o", png));
        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "--expo"));
        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "--histeq", "-o", png));
        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "--smooth", ws.PathTo("s.fit"), "--scale", "50", "-o", png));
        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "--synthetic", ws.PathTo("syn.fit"), "--lens", "Tokina AT-X 11-20mm F2.8 PRO DX", "--profiles", ws.Dir));
        Assert.Equal(VersionInfo.Copyright, SecondLine("--lenses", "--profiles", ws.Dir));
        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "--against", flat, "-o", png));
        // and on a refusal, which prints no report at all
        string cube = ws.WriteFile("cube.fit", FitsTestData.BuildFitsCube(16, 16, 3, (x, y, p) => 100));
        Assert.Equal(VersionInfo.Copyright, SecondLine(cube, "--smooth", ws.PathTo("s2.fit")));
        Assert.Equal(VersionInfo.Copyright, SecondLine("--help"));
        Assert.Equal(VersionInfo.Copyright, SecondLine(ws.PathTo("nu-exista.fit")));
    }

    [Fact]
    public void TheCopyrightNamesTheAuthorAndTheYear()
    {
        Assert.Equal("Copyright (C) 2026 Tudor Carean", VersionInfo.Copyright);
    }

    /// <summary>
    /// GPLv3 section 5(d): a program that shows legal notices must also show the absence
    /// of warranty. It cannot go under the banner - it would thicken every report - so it
    /// sits in --help, and from there it points to the full text.
    /// </summary>
    [Fact]
    public void R005_HelpCarriesTheWarrantyDisclaimerAndPointsAtTheLicense()
    {
        string help = string.Join("\n", Lines("--help"));

        Assert.Contains("FARA NICIO GARANTIE", help);
        Assert.Contains("GPLv3", help);
        Assert.Contains("LICENSE", help);
        Assert.Contains("gnu.org/licenses/gpl-3.0.html", help);
    }

    [Fact]
    public void HelpAlsoStartsWithTheBanner()
    {
        Assert.Equal(VersionInfo.Banner, FirstLine("--help"));
        Assert.Equal(VersionInfo.Banner, FirstLine());
    }

    /// <summary>A run that fails also says which version failed.</summary>
    [Fact]
    public void AFailedRunStillPrintsTheBanner()
    {
        using var ws = new TempWorkspace();

        Assert.Equal(VersionInfo.Banner, FirstLine(ws.PathTo("nu-exista.fit")));
        Assert.Equal(VersionInfo.Banner, FirstLine("--optiune-inventata"));
    }

    /// <summary>The banner is not doubled in --help, where WriteUsage printed it
    /// too.</summary>
    [Fact]
    public void TheBannerIsPrintedExactlyOnce()
    {
        var stdout = new StringWriter();
        FlatCheckCli.Run(new[] { "--help" }, stdout, new StringWriter());

        int count = Regex.Matches(stdout.ToString(), Regex.Escape(VersionInfo.Banner)).Count;
        Assert.Equal(1, count);
    }

    /// <summary>--version is gone: it is now just an unknown argument.</summary>
    [Fact]
    public void TheVersionFlagIsGone()
    {
        Assert.Contains("optiune necunoscuta", Options.Parse(new[] { "f.fit", "--version" }).Error);
    }
}
