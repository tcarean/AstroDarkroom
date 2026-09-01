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
/// GPLv3 asks every copy to carry the notice, so every source file starts with it: the
/// SPDX line (the form licence scanners read), the copyright, and the three standard
/// paragraphs. These tests are the only thing keeping that convention alive when a new
/// file is added - otherwise the header goes missing in the first file written in a
/// hurry, and you find out at a licence audit.
///
/// LICENSE itself is checked too: that it exists, and that it is the real GPL v3 text
/// rather than a paraphrase of it.
/// </summary>
public class LicenseTests
{
    /// <summary>The folder holding the .sln - two levels up from this file.</summary>
    private static string SolutionDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    /// <summary>The notice without the comment prefix, which is the only part that varies.</summary>
    private static readonly string[] Notice =
    {
        "SPDX-License-Identifier: GPL-3.0-or-later",
        "",
        "Copyright (C) 2026 Tudor Carean",
        "",
        "This program is free software: you can redistribute it and/or modify",
        "it under the terms of the GNU General Public License as published by",
        "the Free Software Foundation, either version 3 of the License, or",
        "(at your option) any later version.",
        "",
        "This program is distributed in the hope that it will be useful,",
        "but WITHOUT ANY WARRANTY; without even the implied warranty of",
        "MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the",
        "GNU General Public License for more details.",
        "",
        "You should have received a copy of the GNU General Public License",
        "along with this program.  If not, see <https://www.gnu.org/licenses/>.",
    };

    private static IEnumerable<string> SourceFiles()
    {
        var root = SolutionDir();

        foreach (var project in new[] { "FocalLenghChecker", "FocalLenghChecker.Tests" })
            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                // bin/ and obj/ hold files MSBuild generates (AssemblyInfo and friends).
                // They are not our source and have no reason to carry the notice.
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');

                if (relative.Contains("/bin/") || relative.Contains("/obj/"))
                    continue;

                yield return file;
            }
    }

    private static string ExpectedHeader
        => string.Join("\n", Notice.Select(line => ("// " + line).TrimEnd()));

    private static string HeaderOf(string file)
    {
        var lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n');

        return string.Join("\n", lines.Take(Notice.Length));
    }

    [Fact]
    public void Every_source_file_starts_with_the_license_notice()
    {
        var offenders = SourceFiles()
            .Where(file => HeaderOf(file) != ExpectedHeader)
            .Select(file => Path.GetRelativePath(SolutionDir(), file))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Files without the licence notice at the top:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>Were the enumeration to fail, the test above would pass on zero files.</summary>
    [Fact]
    public void The_scan_sees_both_projects()
    {
        var names = SourceFiles().Select(Path.GetFileName).ToList();

        Assert.Contains("Program.cs", names);
        Assert.Contains("LicenseInfo.cs", names);
        Assert.Contains("LicenseTests.cs", names);
        Assert.True(names.Count > 20, "too few files scanned: " + names.Count);
    }

    [Fact]
    public void The_solution_ships_the_full_gpl_text()
    {
        var license = File.ReadAllText(Path.Combine(SolutionDir(), "LICENSE"));

        Assert.Contains("GNU GENERAL PUBLIC LICENSE", license);
        Assert.Contains("Version 3, 29 June 2007", license);
        Assert.Contains("TERMS AND CONDITIONS", license);
        Assert.Contains("How to Apply These Terms to Your New Programs", license);
    }

    // ------------------------------------------------------- the printed notice

    /// <summary>
    /// Checks the csproj -> assembly -> LicenseInfo plumbing. If someone removes
    /// &lt;Copyright&gt;, or MSBuild stops propagating it, the fallback in the code takes
    /// over silently - and this is where that shows.
    /// </summary>
    [Fact]
    public void The_copyright_comes_from_the_csproj()
    {
        var csproj = File.ReadAllText(
            Path.Combine(SolutionDir(), "FocalLenghChecker", "FocalLenghChecker.csproj"));

        var match = Regex.Match(csproj, @"<Copyright>([^<]+)</Copyright>");

        Assert.True(match.Success, "the csproj has no <Copyright>");
        Assert.Equal(match.Groups[1].Value.Trim(), LicenseInfo.Copyright);
    }

    [Fact]
    public void The_copyright_names_the_author_and_the_year()
    {
        Assert.Equal("Copyright (C) 2026 Tudor Carean", LicenseInfo.Copyright);
    }

    /// <summary>
    /// GPLv3 section 5(d): a program that shows legal notices has to show the absence of
    /// a warranty too. It would pad every report if it sat next to the copyright line, so
    /// it lives in the help, and points at the full text from there.
    /// </summary>
    [Fact]
    public void The_help_carries_the_warranty_disclaimer_and_points_at_the_license()
    {
        Assert.Contains("ABSOLUTELY NO WARRANTY", CommandLineParser.Usage);
        Assert.Contains("GPLv3", CommandLineParser.Usage);
        Assert.Contains("LICENSE", CommandLineParser.Usage);
        Assert.Contains("gnu.org/licenses/gpl-3.0.html", CommandLineParser.Usage);
    }

    /// <summary>The usage text people actually came for still comes first.</summary>
    [Fact]
    public void The_help_still_starts_with_the_usage_text()
    {
        Assert.StartsWith("FocalLenghChecker - counts the focal lengths", CommandLineParser.Usage);
    }
}
