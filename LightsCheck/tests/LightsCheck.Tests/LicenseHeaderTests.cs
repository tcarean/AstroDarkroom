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

namespace LightsCheck.Tests;

/// <summary>
/// GPLv3 asks every copy to carry the notice, so every source file starts with it: the SPDX
/// line, the copyright and the three standard paragraphs (R-009, as FlatCheck's ADR 0040).
/// This test is what keeps the convention alive when a file is added. LICENSE must be the
/// GPL v3 text itself, not a paraphrase.
/// </summary>
public class LicenseHeaderTests
{
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

    /// <summary>Explicit, not discovered from the solution, so adding a project is a decision.</summary>
    private static readonly string[] Projects =
    [
        "src/LightsCheck.Core", "src/LightsCheck.Cli", "src/LightsCheck.Wpf", "tests/LightsCheck.Tests", "tests/LightsCheck.Wpf.Tests", "prototype",
    ];

    /// <summary>XAML is source like C#: hand-written, it describes the interface, so it carries the notice too.</summary>
    private static readonly (string Ext, string Open, string? Close)[] Kinds =
    [
        (".cs", "//", null),
        (".xaml", "<!--", "-->"),
        (".py", "#", null),
    ];

    private static IEnumerable<string> SourceFiles()
    {
        string root = TestData.FindRepoRoot();
        foreach (string project in Projects)
        {
            string dir = Path.Combine(root, project);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                if (!Kinds.Any(k => k.Ext == Path.GetExtension(f)))
                {
                    continue;
                }

                // bin/ and obj/ hold files MSBuild generates, not our source.
                string rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                if (rel.Contains("/bin/", StringComparison.Ordinal) || rel.Contains("/obj/", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return f;
            }
        }
    }

    private static string Expected((string Ext, string Open, string? Close) kind)
        => kind.Close == null
            ? string.Join("\n", Notice.Select(l => (kind.Open + " " + l).TrimEnd()))
            : kind.Open + "\n" + string.Join("\n", Notice) + "\n" + kind.Close;

    private static (string Ext, string Open, string? Close) KindOf(string file)
        => Kinds.First(k => k.Ext == Path.GetExtension(file));

    private static string HeaderOf(string file)
    {
        (string Ext, string Open, string? Close) kind = KindOf(file);
        List<string> lines = File.ReadAllText(file).ReplaceLineEndings("\n").Split('\n').ToList();

        // A shebang stays on the first line of a .py; the XML declaration on the first line of a .xaml.
        if (lines.Count > 0 && (lines[0].StartsWith("#!", StringComparison.Ordinal) || lines[0].StartsWith("<?xml", StringComparison.Ordinal)))
        {
            lines.RemoveAt(0);
        }

        return string.Join("\n", lines.Take(Notice.Length + (kind.Close == null ? 0 : 2)));
    }

    [Fact]
    public void R009_EverySourceFileStartsWithTheLicenseNotice()
    {
        List<string> offenders = SourceFiles()
            .Where(f => HeaderOf(f) != Expected(KindOf(f)))
            .Select(f => Path.GetRelativePath(TestData.FindRepoRoot(), f))
            .ToList();

        Assert.True(offenders.Count == 0, "Source files without the licence notice:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>If the enumeration broke, the test above would pass on zero files.</summary>
    [Fact]
    public void TheScanSeesEveryProjectAndThePrototype()
    {
        List<string?> names = SourceFiles().Select(Path.GetFileName).ToList();
        Assert.Contains("Version.cs", names);             // src/LightsCheck.Core
        Assert.Contains("Program.cs", names);             // src/LightsCheck.Cli
        Assert.Contains("LicenseHeaderTests.cs", names);  // tests/LightsCheck.Tests
        Assert.Contains("analyze.py", names);             // prototype
        Assert.True(names.Count > 20, "too few files scanned: " + names.Count);
    }

    [Fact]
    public void R009_TheRepoShipsTheFullGplText()
    {
        string license = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "LICENSE"));
        Assert.Contains("GNU GENERAL PUBLIC LICENSE", license, StringComparison.Ordinal);
        Assert.Contains("Version 3, 29 June 2007", license, StringComparison.Ordinal);
        Assert.Contains("TERMS AND CONDITIONS", license, StringComparison.Ordinal);
    }
}
