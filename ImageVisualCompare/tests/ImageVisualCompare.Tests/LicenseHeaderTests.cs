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

namespace ImageVisualCompare.Tests;

/// <summary>
/// GPLv3 wants every copy to carry the notice, so every source file starts with it: the SPDX
/// line (the form licence scanners can read), the copyright, and the three standard
/// paragraphs. This test is the only thing that keeps the convention alive when a file is
/// added - otherwise the header is lost silently in the first file written in a hurry, and
/// only a licence audit finds out.
///
/// LICENSE is checked too: that it exists, and that it really is the GPL v3 text, not a
/// paraphrase.
/// </summary>
public class LicenseHeaderTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "") =>
        Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    /// <summary>The notice, without the comment prefix.</summary>
    private static readonly string[] Notice =
    [
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
    ];

    private static readonly string[] Projects =
    [
        Path.Combine("src", "ImageVisualCompare.Core"),
        Path.Combine("src", "ImageVisualCompare.Wpf"),
        Path.Combine("tests", "ImageVisualCompare.Tests"),
    ];

    private static IEnumerable<string> SourceFiles()
    {
        string root = RepoDir();

        foreach (string project in Projects)
        {
            foreach (string file in Directory.EnumerateFiles(
                         Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                // bin/ and obj/ hold files MSBuild generates (AssemblyInfo, .g.cs from XAML)
                // - they are not our source and have no reason to carry the notice.
                string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/bin/") || relative.Contains("/obj/"))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static string Expected() =>
        string.Join("\n", Notice.Select(line => ("// " + line).TrimEnd()));

    private static string HeaderOf(string file)
    {
        string text = File.ReadAllText(file).Replace("\r\n", "\n");
        return string.Join("\n", text.Split('\n').Take(Notice.Length));
    }

    [Fact]
    public void R073_EverySourceFileStartsWithTheLicenseNotice()
    {
        List<string> offenders = SourceFiles()
            .Where(f => HeaderOf(f) != Expected())
            .Select(f => Path.GetRelativePath(RepoDir(), f))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Files whose first lines are not the licence notice:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>If the enumeration went wrong, the test above would pass on zero files.</summary>
    [Fact]
    public void R073_TheScanSeesAllThreeProjects()
    {
        List<string> names = SourceFiles().Select(Path.GetFileName).ToList()!;

        Assert.Contains("Viewport.cs", names);
        Assert.Contains("Version.cs", names);
        Assert.Contains("CompareView.xaml.cs", names);
        Assert.Contains("LicenseHeaderTests.cs", names);
        Assert.True(names.Count > 25, "too few files scanned: " + names.Count);
    }

    [Fact]
    public void R073_TheRepoShipsTheFullGplText()
    {
        string license = File.ReadAllText(Path.Combine(RepoDir(), "LICENSE"));

        Assert.Contains("GNU GENERAL PUBLIC LICENSE", license);
        Assert.Contains("Version 3, 29 June 2007", license);
        Assert.Contains("TERMS AND CONDITIONS", license);
        Assert.Contains("How to Apply These Terms to Your New Programs", license);
    }
}
