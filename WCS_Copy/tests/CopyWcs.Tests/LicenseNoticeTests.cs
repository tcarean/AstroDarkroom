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
/// GPLv3 wants the copyright notice shown on every run, and a program that displays
/// legal notices to display the lack of warranty too (section 5(d)). These tests keep
/// the two promises: the second line of any run is the copyright, and --help carries
/// the disclaimer and points at the full text. They also bind the source of truth: the
/// string comes from &lt;Copyright&gt; in Directory.Build.props, not from a literal left in the code.
/// </summary>
public class LicenseNoticeTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    private static string[] Lines(params string[] args)
    {
        var stdout = new StringWriter();
        CopyWcsCli.Run(args, stdout, new StringWriter());
        return stdout.ToString().Replace("\r\n", "\n").Split('\n');
    }

    private static string SecondLine(params string[] args) => Lines(args)[1];

    /// <summary>
    /// Checks the plumbing Directory.Build.props -> assembly -> LicenseInfo. If someone removes
    /// &lt;Copyright&gt;, the fallback in the code is what remains, and it shows here.
    /// </summary>
    [Fact]
    public void TheCopyrightComesFromTheBuildProps()
    {
        var props = File.ReadAllText(Path.Combine(RepoDir(), "Directory.Build.props"));

        var m = Regex.Match(props, @"<Copyright>([^<]+)</Copyright>");

        Assert.True(m.Success, "Directory.Build.props has no <Copyright>");
        Assert.Equal(m.Groups[1].Value.Trim(), LicenseInfo.Copyright);
    }

    [Fact]
    public void TheCopyrightNamesTheAuthorAndTheYear()
    {
        Assert.Equal("Copyright (C) 2026 Tudor Carean", LicenseInfo.Copyright);
    }

    [Fact]
    public void R001_EveryRunPrintsTheCopyrightOnTheSecondLine()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(400);
        var source = ws.WriteFile("source.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var dest = ws.WriteFile("dest.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));

        Assert.Equal(LicenseInfo.Copyright, SecondLine(source, dest, "--dry-run"));
        Assert.Equal(LicenseInfo.Copyright, SecondLine(source, dest, "-o", ws.FilePath("out.fit")));
        Assert.Equal(LicenseInfo.Copyright, SecondLine("--help"));
    }

    /// <summary>A failed run too says whose tool failed.</summary>
    [Fact]
    public void AFailedRunStillPrintsTheCopyright()
    {
        using var ws = new TempWorkspace();

        Assert.Equal(LicenseInfo.Copyright,
            SecondLine(ws.FilePath("missing.fit"), ws.FilePath("also-missing.fit")));
        Assert.Equal(LicenseInfo.Copyright, SecondLine("--optiune-inventata"));
        Assert.Equal(LicenseInfo.Copyright, SecondLine("un-singur-argument"));
    }

    [Fact]
    public void TheCopyrightIsPrintedExactlyOnce()
    {
        var stdout = new StringWriter();
        CopyWcsCli.Run(["--help"], stdout, new StringWriter());

        var count = Regex.Matches(stdout.ToString(), Regex.Escape(LicenseInfo.Copyright)).Count;
        Assert.Equal(1, count);
    }

    /// <summary>
    /// GPLv3 section 5(d): a program that displays legal notices must display the lack of
    /// warranty too. It cannot go under the banner, it would bloat every run, so it sits in
    /// --help, and points from there at the full text.
    /// </summary>
    [Fact]
    public void R003_HelpCarriesTheWarrantyDisclaimerAndPointsAtTheLicense()
    {
        var help = string.Join("\n", Lines("--help"));

        Assert.Contains("FARA NICIO GARANTIE", help);
        Assert.Contains("GPLv3", help);
        Assert.Contains("LICENSE", help);
        Assert.Contains("gnu.org/licenses/gpl-3.0.html", help);
    }

    /// <summary>The disclaimer appears too when the usage is printed because of an error.</summary>
    [Fact]
    public void UsagePrintedAfterABadArgumentAlsoCarriesTheDisclaimer()
    {
        var help = string.Join("\n", Lines("un-singur-argument"));

        Assert.Contains("FARA NICIO GARANTIE", help);
    }
}
