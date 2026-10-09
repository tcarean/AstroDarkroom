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
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MindfulnessReminder.Tests;

/// <summary>
/// Tests about the repository itself: things a rule asks for and a test can enforce instead
/// (ADR 0001). Cheap, and never forgotten in a long session. The requirement-to-test link is
/// read from the test sources, by the method names a reader sees, as in the sibling projects.
/// Version and copyright plumbing is in <see cref="VersionTests"/>, the licence notice in
/// <see cref="LicenseHeaderTests"/>.
/// </summary>
public class RepoInvariantTests
{
    private static readonly string Root = RepoDir();

    private static readonly string[] TestProjects = { Path.Combine("tests", "MindfulnessReminder.Tests") };

    private static readonly Regex RequirementRow =
        new(@"^\|\s*R-(\d{3})\s*\|\s*(draft|accepted|implemented|dropped)\s*\|", RegexOptions.Compiled);

    private static readonly Regex TestMethod =
        new(@"public\s+(?:async\s+)?(?:void|Task)\s+R(\d{3})_\w+\s*\(", RegexOptions.Compiled);

    /// <summary>The project folder (the one holding MindfulnessReminder.sln), from this file's own path.</summary>
    private static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    /// <summary>The kit's rule "Requirements first" made checkable: an implemented requirement has at least one test named after it.</summary>
    [Fact]
    public void Every_implemented_requirement_has_a_test_carrying_its_id()
    {
        var requirements = ReadRequirements();
        var tested = TestIdsInSources();
        var missing = requirements
            .Where(r => r.Value == "implemented" && !tested.Contains(r.Key))
            .Select(r => r.Key)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "Implemented requirements without a test named R###_...: " + string.Join(", ", missing));
    }

    /// <summary>The reverse: a test cannot claim a requirement ID that requirements.md does not know.</summary>
    [Fact]
    public void Every_test_id_exists_in_requirements()
    {
        var requirements = ReadRequirements();
        var unknown = TestIdsInSources().Where(id => !requirements.ContainsKey(id)).ToArray();

        Assert.True(
            unknown.Length == 0,
            "Tests named after requirement IDs that are not in docs/requirements.md: " + string.Join(", ", unknown));
    }

    /// <summary>A requirement tested by name is implemented, not merely accepted: the status must say so.</summary>
    [Fact]
    public void Every_tested_requirement_is_marked_implemented()
    {
        var requirements = ReadRequirements();
        var stale = TestIdsInSources()
            .Where(id => requirements.TryGetValue(id, out var status) && status != "implemented")
            .ToArray();

        Assert.True(
            stale.Length == 0,
            "Requirements with an R###_ test but a status other than implemented: " + string.Join(", ", stale));
    }

    [Fact]
    public void Requirement_ids_are_unique()
    {
        var ids = new List<string>();
        foreach (var line in File.ReadLines(Path.Combine(Root, "docs", "requirements.md")))
        {
            var m = RequirementRow.Match(line);
            if (m.Success)
            {
                ids.Add("R" + m.Groups[1].Value);
            }
        }

        var duplicates = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.True(duplicates.Length == 0, "Duplicate requirement IDs: " + string.Join(", ", duplicates));
    }

    /// <summary>
    /// The design notes and their index exist. The kit's check-design.ps1 checks the index and the
    /// Covers paths but passes a project without docs/design/; this one has notes, and CLAUDE.md
    /// sends every session to them.
    /// </summary>
    [Fact]
    public void The_design_notes_have_their_index()
    {
        var dir = Path.Combine(Root, "docs", "design");

        Assert.True(File.Exists(Path.Combine(dir, "README.md")), "docs/design/README.md is missing");
        Assert.True(Directory.EnumerateFiles(dir, "*.md").Count() > 1, "docs/design/ holds no design note");
    }

    /// <summary>
    /// docs/validation.json lists no case on purpose (ADR 0005), and the kit's skills read it. This
    /// keeps the file well formed, and its case IDs unique, for the day the first case arrives.
    /// </summary>
    [Fact]
    public void The_validation_cases_parse_and_have_unique_ids()
    {
        using var validation = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Root, "docs", "validation.json")));
        var cases = validation.RootElement.GetProperty("cases");

        Assert.Equal(JsonValueKind.Array, cases.ValueKind);
        var ids = cases.EnumerateArray().Select(c => c.GetProperty("id").GetString()!).ToList();
        var duplicates = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.True(duplicates.Length == 0, "Duplicate validation case IDs: " + string.Join(", ", duplicates));
    }

    /// <summary>CLAUDE.md holds the rules; the explanations live in docs/design/. Over 200 lines it is growing back.</summary>
    [Fact]
    public void Claude_md_stays_short()
    {
        var lines = File.ReadLines(Path.Combine(Root, "CLAUDE.md")).Count();

        Assert.True(lines <= 200, $"CLAUDE.md has {lines} lines; move explanations to docs/design/ (ADR 0001)");
    }

    /// <summary>Table rows of docs/requirements.md: "| R-001 | implemented | ..." gives R001, implemented.</summary>
    private static Dictionary<string, string> ReadRequirements()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(Path.Combine(Root, "docs", "requirements.md")))
        {
            var m = RequirementRow.Match(line);
            if (m.Success)
            {
                result["R" + m.Groups[1].Value] = m.Groups[2].Value;
            }
        }

        Assert.NotEmpty(result);
        return result;
    }

    /// <summary>Every test method whose name starts with R, three digits and an underscore.</summary>
    private static HashSet<string> TestIdsInSources()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in TestProjects)
        {
            var dir = Path.Combine(Root, project);
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.TopDirectoryOnly))
            {
                foreach (Match m in TestMethod.Matches(File.ReadAllText(file)))
                {
                    ids.Add("R" + m.Groups[1].Value);
                }
            }
        }

        return ids;
    }
}
