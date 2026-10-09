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
/// GPLv3 asks that every copy carry the notice, so every source file starts with it: the
/// SPDX line (the machine-readable half, for licence scanners), the copyright, and the
/// three standard paragraphs. This test is the only thing keeping that convention alive as
/// files get added -- otherwise the header quietly goes missing in the first file written
/// in a hurry, and you find out at a licence audit.
///
/// The rest of the file pins the other half of the arrangement: the LICENSE text itself,
/// the single source of truth for the copyright string, and the fact that the notice is
/// actually reachable from the running app.
/// </summary>
public class LicenseHeaderTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    /// <summary>The notice without a comment prefix -- that differs between C# and XAML.</summary>
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

    private static readonly string[] Projects =
    {
        Path.Combine("src", "MindfulnessReminder.Core"),
        Path.Combine("src", "MindfulnessReminder.App"),
        Path.Combine("tests", "MindfulnessReminder.Tests"),
    };

    private static IEnumerable<string> SourceFiles()
    {
        var root = RepoDir();

        foreach (var project in Projects)
        {
            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(root, project), "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file);
                if (extension != ".cs" && extension != ".xaml")
                {
                    continue;
                }

                // bin/ and obj/ hold what MSBuild generated (AssemblyInfo, the XAML
                // code-behind, ...). None of it is our source, so none of it carries the
                // notice -- and obj/ keeps a copy of every .xaml, which would double the
                // scan and then fail on files nobody wrote.
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/bin/") || relative.Contains("/obj/"))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    /// <summary>Renders the notice the way the language in question comments it out.</summary>
    private static string Expected(string extension)
        => extension == ".xaml"
            ? "<!--\n" + string.Join("\n", Notice.Select(l => ("    " + l).TrimEnd())) + "\n-->"
            : string.Join("\n", Notice.Select(l => ("// " + l).TrimEnd()));

    private static string HeaderOf(string file)
    {
        var lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n');

        // XAML wraps the notice in one block comment, so its header is two lines longer.
        var length = Path.GetExtension(file) == ".xaml" ? Notice.Length + 2 : Notice.Length;

        return string.Join("\n", lines.Take(length));
    }

    [Fact]
    public void R092_Every_source_file_starts_with_the_license_notice()
    {
        var offenders = SourceFiles()
            .Where(f => HeaderOf(f) != Expected(Path.GetExtension(f)))
            .Select(f => Path.GetRelativePath(RepoDir(), f))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Files that do not start with the license notice:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>Without this, a broken scan would let the test above pass on zero files.</summary>
    [Fact]
    public void R092_The_scan_sees_all_three_projects_and_the_xaml()
    {
        var names = SourceFiles().Select(Path.GetFileName).ToList();

        Assert.Contains("LicenseInfo.cs", names);
        Assert.Contains("TrayIconHost.cs", names);
        Assert.Contains("LicenseHeaderTests.cs", names);
        Assert.Contains("App.xaml", names);
        Assert.Contains("BreakWindow.xaml", names);
        Assert.True(names.Count > 20, "too few files scanned: " + names.Count);
    }

    [Fact]
    public void R092_The_repo_ships_the_full_gpl_text()
    {
        var license = File.ReadAllText(Path.Combine(RepoDir(), "LICENSE"));

        Assert.Contains("GNU GENERAL PUBLIC LICENSE", license);
        Assert.Contains("Version 3, 29 June 2007", license);
        Assert.Contains("TERMS AND CONDITIONS", license);
        Assert.Contains("How to Apply These Terms to Your New Programs", license);
    }

    /// <summary>
    /// The copyright comes from Directory.Build.props, through AssemblyCopyrightAttribute.
    /// If someone drops the property, the fallback literal in LicenseInfo takes over -- and
    /// this is where that shows up.
    /// </summary>
    [Fact]
    public void R091_The_copyright_comes_from_the_build_props()
    {
        var props = File.ReadAllText(Path.Combine(RepoDir(), "Directory.Build.props"));

        var match = Regex.Match(props, "<Copyright>([^<]+)</Copyright>");

        Assert.True(match.Success, "Directory.Build.props has no <Copyright>");
        Assert.Equal(match.Groups[1].Value.Trim(), LicenseInfo.Copyright);
    }

    [Fact]
    public void R091_The_copyright_names_the_author_and_the_year()
    {
        Assert.Equal("Copyright (C) 2026 Tudor Carean", LicenseInfo.Copyright);
    }

    /// <summary>
    /// GPLv3 section 5(d): a program that displays legal notices has to display the warranty
    /// disclaimer too. There is no console here, and the reminder window is the wrong place
    /// for it, so About carries it -- and points at the full text from there.
    /// </summary>
    [Fact]
    public void R091_About_carries_the_warranty_disclaimer_and_points_at_the_license()
    {
        var about = LicenseInfo.AboutText;

        Assert.Contains(LicenseInfo.Copyright, about);
        Assert.Contains("ABSOLUTELY NO WARRANTY", about);
        Assert.Contains("GNU General Public License v3", about);
        Assert.Contains("LICENSE", about);
        Assert.Contains("https://www.gnu.org/licenses/gpl-3.0.html", about);
    }

    /// <summary>About says the licence sits next to the executable; the build has to make that true.</summary>
    [Fact]
    public void R093_The_app_copies_the_license_next_to_the_executable()
    {
        var csproj = File.ReadAllText(Path.Combine(
            RepoDir(), "src", "MindfulnessReminder.App", "MindfulnessReminder.App.csproj"));

        var match = Regex.Match(csproj, "<None Include=\"[^\"]*LICENSE\"[^>]*>");

        Assert.True(match.Success, "the app project does not include LICENSE");
        Assert.Contains("CopyToOutputDirectory", match.Value);
    }
}
