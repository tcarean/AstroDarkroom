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

namespace CopyWcs.Tests;

/// <summary>
/// GPLv3 asks that every copy carry the notice, so every source file begins with it:
/// the SPDX line (the form licence scanners read), the copyright, and the three
/// standard paragraphs. This test is the only thing that keeps the convention alive when
/// a file is added; otherwise the header is silently lost in the first file written in
/// a hurry, and a licence audit is where you find out.
///
/// LICENSE is checked too: that it exists, and that it is the GPL v3 text itself, not a paraphrase.
/// </summary>
public class LicenseHeaderTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    /// <summary>The notice without the comment prefix, which differs between C# and Python.</summary>
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

    /// <summary>
    /// XAML is source like C#: hand-written, it describes the interface, so it carries the notice
    /// too, inside one comment; the Python prototype and the icon script carry it with the hash.
    /// </summary>
    private static readonly (string Ext, string Open, string? Close)[] Kinds =
    [
        (".cs", "//", null),
        (".xaml", "<!--", "-->"),
        (".py", "#", null),
    ];

    private static IEnumerable<string> SourceFiles()
    {
        var root = RepoDir();

        foreach (var project in new[] { "src", "tests", "prototype", "Icon" })
        {
            foreach (var f in Directory.EnumerateFiles(Path.Combine(root, project), "*", SearchOption.AllDirectories))
            {
                if (!Kinds.Any(k => k.Ext == Path.GetExtension(f)))
                {
                    continue;
                }

                // bin/ and obj/ hold files MSBuild generates (AssemblyInfo and such):
                // not our source, and no reason to carry the notice.
                var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                if (rel.Contains("/bin/") || rel.Contains("/obj/"))
                {
                    continue;
                }

                yield return f;
            }
        }
    }

    /// <summary>Renders the notice with the comment marks of the language.</summary>
    private static string Expected((string Ext, string Open, string? Close) kind)
        => kind.Close == null
            ? string.Join("\n", Notice.Select(l => (kind.Open + " " + l).TrimEnd()))
            : kind.Open + "\n" + string.Join("\n", Notice) + "\n" + kind.Close;

    private static (string Ext, string Open, string? Close) KindOf(string file)
        => Kinds.First(k => k.Ext == Path.GetExtension(file));

    private static string HeaderOf(string file)
    {
        var kind = KindOf(file);
        var text = File.ReadAllText(file).Replace("\r\n", "\n");
        var lines = text.Split('\n').ToList();

        // The shebang of a .py must stay on the first line, so the notice comes under it; the
        // XML declaration of a .xaml the same.
        if (lines.Count > 0 && (lines[0].StartsWith("#!") || lines[0].StartsWith("<?xml")))
        {
            lines.RemoveAt(0);
        }

        return string.Join("\n", lines.Take(Notice.Length + (kind.Close == null ? 0 : 2)));
    }

    [Fact]
    public void EverySourceFileStartsWithTheLicenseNotice()
    {
        var offenders = SourceFiles()
            .Where(f => HeaderOf(f) != Expected(KindOf(f)))
            .Select(f => Path.GetRelativePath(RepoDir(), f))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Files without the licence notice at the top:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>If the enumeration went wrong, the test above would pass on zero files.</summary>
    [Fact]
    public void TheScanSeesEveryProjectThePrototypeAndTheIconScript()
    {
        var names = SourceFiles().Select(Path.GetFileName).ToList();

        Assert.Contains("CopyWcsCli.cs", names);
        Assert.Contains("LicenseHeaderTests.cs", names);
        Assert.Contains("MainWindow.xaml", names);
        Assert.Contains("WindowSmokeTests.cs", names);
        Assert.Contains("copy_wcs.py", names);
        Assert.Contains("make_icon.py", names);
        Assert.True(names.Count > 20, "too few files scanned: " + names.Count);
    }

    [Fact]
    public void TheRepoShipsTheFullGplText()
    {
        var license = File.ReadAllText(Path.Combine(RepoDir(), "LICENSE"));

        Assert.Contains("GNU GENERAL PUBLIC LICENSE", license);
        Assert.Contains("Version 3, 29 June 2007", license);
        Assert.Contains("TERMS AND CONDITIONS", license);
        Assert.Contains("How to Apply These Terms to Your New Programs", license);
    }
}
