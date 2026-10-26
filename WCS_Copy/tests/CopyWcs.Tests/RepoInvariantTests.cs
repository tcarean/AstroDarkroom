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

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CopyWcs.Tests;

/// <summary>
/// Tests about the repository itself: things a rule asks for and a test can enforce instead
/// (the kit's ADR 0005). The kit's own checks of its scripts and hooks are not here: those
/// files are the kit's, synced byte for byte, and tested there (ADR 0001). The version and
/// copyright plumbing is in <see cref="VersionTests"/> and <see cref="LicenseNoticeTests"/>,
/// the licence notice in <see cref="LicenseHeaderTests"/>.
/// </summary>
public partial class RepoInvariantTests
{
    [Fact]
    public void Version_IsDeclaredOnceInDirectoryBuildProps()
    {
        var props = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Directory.Build.props"));
        var versions = VersionElement().Matches(props);
        Assert.Single(versions);
        Assert.Matches(@"^\d+\.\d+\.\d+$", versions[0].Groups[1].Value);
    }

    /// <summary>The projects that ship target .NET 8, like every tool of the Astro Darkroom suite (ADR 0001).</summary>
    [Fact]
    public void TargetFramework_IsNet8_InTheSharedProps()
    {
        var root = TestData.FindRepoRoot();
        Assert.Contains("<TargetFramework>net8.0</TargetFramework>",
            File.ReadAllText(Path.Combine(root, "Directory.Build.props")), StringComparison.Ordinal);
    }

    /// <summary>Every fixture on disk is in the manifest, so its hash is checked by the gate.</summary>
    [Fact]
    public void Manifest_ListsEveryFixtureInRepo()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "testdata", "manifest.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var listed = doc.RootElement.GetProperty("files").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var fixtures = Path.Combine(TestData.Root, "fixtures");
        var onDisk = Directory.Exists(fixtures)
            ? Directory.GetFiles(fixtures).Select(f => Path.GetFileName(f)).ToArray()
            : [];

        foreach (var file in onDisk)
        {
            Assert.Contains(file, listed);
        }
    }

    /// <summary>The kit's rule "Requirements first" made checkable: an implemented requirement has at least one test named after it.</summary>
    [Fact]
    public void EveryImplementedRequirement_HasATestCarryingItsId()
    {
        var requirements = ReadRequirements();
        var testedIds = TestMethodIds();

        var missing = requirements
            .Where(r => r.Value == "implemented" && !testedIds.Contains(r.Key))
            .Select(r => r.Key)
            .ToArray();

        Assert.True(missing.Length == 0,
            $"Implemented requirements without a test named R###_...: {string.Join(", ", missing)}");
    }

    /// <summary>The reverse: a test cannot claim a requirement ID that requirements.md does not know.</summary>
    [Fact]
    public void EveryTestId_ExistsInRequirements()
    {
        var requirements = ReadRequirements();
        var unknown = TestMethodIds().Where(id => !requirements.ContainsKey(id)).ToArray();
        Assert.True(unknown.Length == 0,
            $"Tests named after requirement IDs that are not in docs/requirements.md: {string.Join(", ", unknown)}");
    }

    /// <summary>A rule is cited by its name, never by a number (the kit's ADR 0028).</summary>
    [Fact]
    public void ClaudeMd_CitesNoRuleByNumber()
    {
        var text = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "CLAUDE.md"));
        Assert.DoesNotMatch(@"\b[Rr]ule\s+\d+\b", text);
    }

    /// <summary>Table rows of docs/requirements.md: "| R-001 | implemented | ..." gives R001 and implemented.</summary>
    private static Dictionary<string, string> ReadRequirements()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "requirements.md");
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            var m = RequirementRow().Match(line);
            if (m.Success)
            {
                result["R" + m.Groups[1].Value] = m.Groups[2].Value;
            }
        }

        Assert.NotEmpty(result);
        return result;
    }

    /// <summary>
    /// Every test method whose name starts with R### followed by an underscore: by reflection in
    /// this assembly, and by reading the sources of the other test projects under tests/ (a
    /// window's tests would target net8.0-windows and could not be referenced from here).
    /// </summary>
    private static HashSet<string> TestMethodIds()
    {
        var ids = typeof(RepoInvariantTests).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(m => TestName().Match(m.Name))
            .Where(m => m.Success)
            .Select(m => "R" + m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var tests = Path.Combine(TestData.FindRepoRoot(), "tests");
        foreach (var project in Directory.GetDirectories(tests).Where(d => Path.GetFileName(d) != "CopyWcs.Tests"))
        {
            foreach (var file in Directory.GetFiles(project, "*.cs"))
            {
                foreach (Match m in TestMethodInSource().Matches(File.ReadAllText(file)))
                {
                    ids.Add("R" + m.Groups[1].Value);
                }
            }
        }

        return ids;
    }

    [GeneratedRegex(@"<Version>([^<]+)</Version>")]
    private static partial Regex VersionElement();

    [GeneratedRegex(@"^\|\s*R-(\d{3})\s*\|\s*(draft|accepted|implemented|dropped)\s*\|")]
    private static partial Regex RequirementRow();

    [GeneratedRegex(@"^R(\d{3})_")]
    private static partial Regex TestName();

    [GeneratedRegex(@"\bpublic\s+(?:async\s+)?\w+\s+R(\d{3})_\w+\s*\(")]
    private static partial Regex TestMethodInSource();
}
