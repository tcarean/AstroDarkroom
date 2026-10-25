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

namespace AstroDarkroom.Tests;

/// <summary>
/// Tests about the repository itself: things a rule asks for and a test can enforce instead
/// (the kit's ADR 0005). The kit's own checks of its scripts, hooks and guide are not here:
/// those files are the kit's, synced byte for byte, and tested there (ADR 0001). The version
/// and copyright plumbing is in <see cref="VersionTests"/>, the licence notice in
/// <see cref="LicenseHeaderTests"/>, the suite's consistency in SuiteTests.
/// </summary>
public partial class RepoInvariantTests
{
    [Fact]
    public void Version_IsDeclaredOnceInDirectoryBuildProps()
    {
        string props = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Directory.Build.props"));
        MatchCollection versions = VersionElement().Matches(props);
        Assert.Single(versions);
        Assert.Matches(@"^\d+\.\d+\.\d+$", versions[0].Groups[1].Value);
    }

    /// <summary>
    /// The launcher targets .NET 8, like every component, so the suite folder carries one
    /// runtime (ADR 0002): the shared framework in Directory.Build.props and the window's own.
    /// </summary>
    [Fact]
    public void TargetFramework_IsNet8_InEveryProjectThatShips()
    {
        string root = TestData.FindRepoRoot();
        Assert.Contains("<TargetFramework>net8.0</TargetFramework>",
            File.ReadAllText(Path.Combine(root, "Directory.Build.props")), StringComparison.Ordinal);
        string window = Path.Combine(root, "src", "AstroDarkroom.Wpf", "AstroDarkroom.Wpf.csproj");
        Assert.True(File.Exists(window), "the window project is not where CLAUDE.md says: " + window);
        Assert.Contains("<TargetFramework>net8.0-windows</TargetFramework>", File.ReadAllText(window), StringComparison.Ordinal);
    }

    /// <summary>Every fixture on disk is in the manifest; today there is none, the tests build their folders in temp.</summary>
    [Fact]
    public void Manifest_ListsEveryFixtureInRepo()
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "testdata", "manifest.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        HashSet<string> listed = doc.RootElement.GetProperty("files").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        string fixtures = Path.Combine(TestData.Root, "fixtures");
        string[] onDisk = Directory.Exists(fixtures)
            ? Directory.GetFiles(fixtures).Select(f => Path.GetFileName(f)).ToArray()
            : [];

        foreach (string file in onDisk)
        {
            Assert.Contains(file, listed);
        }
    }

    /// <summary>The kit's rule "Requirements first" made checkable: an implemented requirement has at least one test named after it.</summary>
    [Fact]
    public void EveryImplementedRequirement_HasATestCarryingItsId()
    {
        Dictionary<string, string> requirements = ReadRequirements();
        HashSet<string> testedIds = TestMethodIds();

        string[] missing = requirements
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
        Dictionary<string, string> requirements = ReadRequirements();
        string[] unknown = TestMethodIds().Where(id => !requirements.ContainsKey(id)).ToArray();
        Assert.True(unknown.Length == 0,
            $"Tests named after requirement IDs that are not in docs/requirements.md: {string.Join(", ", unknown)}");
    }

    /// <summary>Table rows of docs/requirements.md: "| R-001 | implemented | ..." => R001 => implemented.</summary>
    private static Dictionary<string, string> ReadRequirements()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "docs", "requirements.md");
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(path))
        {
            Match m = RequirementRow().Match(line);
            if (m.Success)
            {
                result["R" + m.Groups[1].Value] = m.Groups[2].Value;
            }
        }

        Assert.NotEmpty(result);
        return result;
    }

    /// <summary>Every test method in this assembly whose name starts with R### followed by an underscore.</summary>
    private static HashSet<string> TestMethodIds()
    {
        return typeof(RepoInvariantTests).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(m => TestName().Match(m.Name))
            .Where(m => m.Success)
            .Select(m => "R" + m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    [GeneratedRegex(@"<Version>([^<]+)</Version>")]
    private static partial Regex VersionElement();

    [GeneratedRegex(@"^\|\s*R-(\d{3})\s*\|\s*(draft|accepted|implemented|dropped)\s*\|")]
    private static partial Regex RequirementRow();

    [GeneratedRegex(@"^R(\d{3})_")]
    private static partial Regex TestName();
}
