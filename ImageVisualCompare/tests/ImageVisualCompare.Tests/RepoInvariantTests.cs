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
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ImageVisualCompare.Tests;

/// <summary>
/// Tests about the repository itself: things a rule asks for and a test can enforce instead
/// (ADR 0001). Cheap, and never forgotten in a long session. The requirement-to-test link is read
/// from the test sources, by the method names a reader sees, as in the sibling projects. Version
/// and copyright plumbing is in <see cref="VersionTests"/>, the licence notice in
/// <see cref="LicenseHeaderTests"/>.
/// </summary>
public class RepoInvariantTests
{
    private static readonly string Root = TestData.RepoDir();

    private static readonly string[] TestProjects = [Path.Combine("tests", "ImageVisualCompare.Tests")];

    private static readonly Regex RequirementRow =
        new(@"^\|\s*R-(\d{3})\s*\|\s*(draft|accepted|implemented|dropped)\s*\|", RegexOptions.Compiled);

    private static readonly Regex TestMethod =
        new(@"public\s+(?:async\s+)?(?:void|Task)\s+R(\d{3})_\w+\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// The projects target .NET 8, so the Astro Darkroom suite carries one runtime (ADR 0037):
    /// the core, the application and the tests, each in its own project file.
    /// </summary>
    [Theory]
    [InlineData("src/ImageVisualCompare.Core/ImageVisualCompare.Core.csproj", "net8.0")]
    [InlineData("src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj", "net8.0-windows")]
    [InlineData("tests/ImageVisualCompare.Tests/ImageVisualCompare.Tests.csproj", "net8.0-windows")]
    public void TargetFramework_IsNet8_InEveryProjectThatShips(string project, string framework)
    {
        string text = File.ReadAllText(Path.Combine(Root, project));
        Assert.Contains($"<TargetFramework>{framework}</TargetFramework>", text, StringComparison.Ordinal);
    }

    /// <summary>The kit's rule "Requirements first" made checkable: an implemented requirement has at least one test named after it.</summary>
    [Fact]
    public void EveryImplementedRequirement_HasATestCarryingItsId()
    {
        Dictionary<string, string> requirements = ReadRequirements();
        HashSet<string> tested = TestIdsInSources();
        string[] missing = requirements
            .Where(r => r.Value == "implemented" && !tested.Contains(r.Key))
            .Select(r => r.Key)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "Implemented requirements without a test named R###_...: " + string.Join(", ", missing));
    }

    /// <summary>The reverse: a test cannot claim a requirement ID that requirements.md does not know.</summary>
    [Fact]
    public void EveryTestId_ExistsInRequirements()
    {
        Dictionary<string, string> requirements = ReadRequirements();
        string[] unknown = TestIdsInSources().Where(id => !requirements.ContainsKey(id)).ToArray();

        Assert.True(
            unknown.Length == 0,
            "Tests named after requirement IDs that are not in docs/requirements.md: " + string.Join(", ", unknown));
    }

    /// <summary>A requirement tested by name is implemented, not merely accepted: the status must say so.</summary>
    [Fact]
    public void EveryTestedRequirement_IsMarkedImplemented()
    {
        Dictionary<string, string> requirements = ReadRequirements();
        string[] stale = TestIdsInSources()
            .Where(id => requirements.TryGetValue(id, out string? status) && status != "implemented")
            .ToArray();

        Assert.True(
            stale.Length == 0,
            "Requirements with an R###_ test but a status other than implemented: " + string.Join(", ", stale));
    }

    [Fact]
    public void RequirementIds_AreUnique()
    {
        var ids = new List<string>();
        foreach (string line in File.ReadLines(Path.Combine(Root, "docs", "requirements.md")))
        {
            Match m = RequirementRow.Match(line);
            if (m.Success)
            {
                ids.Add("R" + m.Groups[1].Value);
            }
        }

        string[] duplicates = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.True(duplicates.Length == 0, "Duplicate requirement IDs: " + string.Join(", ", duplicates));
    }

    /// <summary>
    /// The design notes and their index exist. The kit's check-design.ps1 checks the index and the
    /// Covers paths but passes a project without docs/design/; this one has notes, and CLAUDE.md
    /// sends every session to them.
    /// </summary>
    [Fact]
    public void DesignNotes_HaveTheirIndex()
    {
        string dir = Path.Combine(Root, "docs", "design");

        Assert.True(File.Exists(Path.Combine(dir, "README.md")), "docs/design/README.md is missing");
        Assert.True(Directory.EnumerateFiles(dir, "*.md").Count() > 1, "docs/design/ holds no design note");
    }

    /// <summary>Every validation case names two images the manifest knows, and the case IDs are unique.</summary>
    [Fact]
    public void EveryValidationCase_NamesManifestFiles()
    {
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Root, "tests", "testdata", "manifest.json")));
        HashSet<string> known = manifest.RootElement.GetProperty("files").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        using JsonDocument validation = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Root, "docs", "validation.json")));
        var ids = new List<string>();
        var problems = new List<string>();
        foreach (JsonElement c in validation.RootElement.GetProperty("cases").EnumerateArray())
        {
            string id = c.GetProperty("id").GetString()!;
            ids.Add(id);
            foreach (string key in new[] { "before", "after" })
            {
                string image = c.GetProperty(key).GetString()!;
                if (!known.Contains(image))
                {
                    problems.Add($"{id}: {key} {image} is not in the manifest");
                }
            }
        }

        string[] duplicates = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicates.Length > 0)
        {
            problems.Add("duplicate case IDs: " + string.Join(", ", duplicates));
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>CLAUDE.md holds the rules; the explanations live in docs/design/. Over 200 lines it is growing back.</summary>
    [Fact]
    public void ClaudeMd_StaysShort()
    {
        int lines = File.ReadLines(Path.Combine(Root, "CLAUDE.md")).Count();

        Assert.True(lines <= 200, $"CLAUDE.md has {lines} lines; move explanations to docs/design/ (ADR 0001)");
    }

    /// <summary>Table rows of docs/requirements.md: "| R-001 | implemented | ..." gives R001, implemented.</summary>
    private static Dictionary<string, string> ReadRequirements()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(Path.Combine(Root, "docs", "requirements.md")))
        {
            Match m = RequirementRow.Match(line);
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
        foreach (string project in TestProjects)
        {
            string dir = Path.Combine(Root, project);
            foreach (string file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.TopDirectoryOnly))
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
