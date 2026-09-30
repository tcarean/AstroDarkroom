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

using System.Text.Json;
using System.Text.RegularExpressions;

namespace FlatCheck.Tests;

/// <summary>
/// Tests about the repository itself: things a rule in CLAUDE.md asks for and a test can
/// enforce instead (the kit's ADR 0005; here ADR 0001). Cheap, and never forgotten in a long
/// session. The requirement-to-test link is read from the test <em>sources</em> of both test
/// projects rather than by reflection, because FlatCheck.Wpf.Tests is Windows-only and cannot
/// be referenced from this portable project. Version and copyright plumbing is in
/// <see cref="VersionTests"/>, the licence notice in <see cref="LicenseHeaderTests"/>, the
/// third-party list in <see cref="ThirdPartyTests"/>.
/// </summary>
public class RepoInvariantTests
{
    static readonly string Root = TestData.RepoDir();
    static readonly string[] TestProjects = { "FlatCheck.Tests", "FlatCheck.Wpf.Tests" };
    static readonly Regex RequirementRow = new(@"^\|\s*R-(\d{3})\s*\|\s*(draft|accepted|implemented|dropped)\s*\|", RegexOptions.Compiled);
    static readonly Regex TestMethod = new(@"public\s+(?:async\s+)?(?:void|Task)\s+R(\d{3})_\w+\s*\(", RegexOptions.Compiled);
    static readonly string[] SourceProjects = { "FlatCheck", "FlatCheck.Core", "FlatCheck.Tests", "FlatCheck.Wpf", "FlatCheck.Wpf.Tests" };
    // The three forms of a process start: constructing either type by name, declaring a variable
    // of either type with a target-typed construction, and the static Start. Written so the
    // pattern does not match its own text; keep code samples of these forms out of this file.
    static readonly Regex ProcessStart = new(
        @"\bnew\s+(?:global::)?(?:System\.Diagnostics\.)?Process(?:StartInfo)?\s*[({]"
        + @"|\bProcess(?:StartInfo)?\s+\w+\s*=\s*new\s*[({]"
        + @"|\bProcess\.Start\s*\(",
        RegexOptions.Compiled);

    /// <summary>Rule 1 made checkable: an implemented requirement has at least one test named after it.</summary>
    [Fact]
    public void EveryImplementedRequirement_HasATestCarryingItsId()
    {
        Dictionary<string, string> requirements = ReadRequirements();
        HashSet<string> tested = TestIdsInSources();
        string[] missing = requirements
            .Where(r => r.Value == "implemented" && !tested.Contains(r.Key))
            .Select(r => r.Key)
            .ToArray();
        Assert.True(missing.Length == 0,
            "Implemented requirements without a test named R###_...: " + string.Join(", ", missing));
    }

    /// <summary>The reverse: a test cannot claim a requirement ID that requirements.md does not know.</summary>
    [Fact]
    public void EveryTestId_ExistsInRequirements()
    {
        Dictionary<string, string> requirements = ReadRequirements();
        string[] unknown = TestIdsInSources().Where(id => !requirements.ContainsKey(id)).ToArray();
        Assert.True(unknown.Length == 0,
            "Tests named after requirement IDs that are not in docs/requirements.md: " + string.Join(", ", unknown));
    }

    /// <summary>A requirement tested by name is implemented, not merely accepted: the status must say so.</summary>
    [Fact]
    public void EveryTestedRequirement_IsMarkedImplemented()
    {
        Dictionary<string, string> requirements = ReadRequirements();
        string[] stale = TestIdsInSources()
            .Where(id => requirements.TryGetValue(id, out string? s) && s != "implemented")
            .ToArray();
        Assert.True(stale.Length == 0,
            "Requirements with an R###_ test but a status other than implemented: " + string.Join(", ", stale));
    }

    [Fact]
    public void RequirementIds_AreUnique()
    {
        var ids = new List<string>();
        foreach (string line in File.ReadLines(Path.Combine(Root, "docs", "requirements.md")))
        {
            Match m = RequirementRow.Match(line);
            if (m.Success) ids.Add("R" + m.Groups[1].Value);
        }
        string[] dup = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.True(dup.Length == 0, "Duplicate requirement IDs: " + string.Join(", ", dup));
    }

    /// <summary>ADRs are numbered once, in order, and each says whether it is proposed, accepted or superseded.</summary>
    [Fact]
    public void Decisions_AreNumberedOnceAndCarryAStatus()
    {
        string dir = Path.Combine(Root, "docs", "decisions");
        var numbers = new List<int>();
        var problems = new List<string>();
        foreach (string f in Directory.EnumerateFiles(dir, "*.md").OrderBy(f => f, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(f);
            Match m = Regex.Match(name, @"^(\d{4})-");
            if (!m.Success) { problems.Add(name + ": not NNNN-slug.md"); continue; }
            int n = int.Parse(m.Groups[1].Value);
            if (n == 0) continue; // the template
            numbers.Add(n);
            string text = File.ReadAllText(f);
            if (!Regex.IsMatch(text, @"(?m)^Status: (proposed|accepted \d{4}-\d{2}-\d{2}|superseded by \d{4})\s*$"))
                problems.Add(name + ": no Status line of the template's form");
            if (!text.StartsWith("# " + m.Groups[1].Value + ". ", StringComparison.Ordinal))
                problems.Add(name + ": title does not start with '# " + m.Groups[1].Value + ". '");
        }
        int[] dup = numbers.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (dup.Length > 0) problems.Add("duplicate numbers: " + string.Join(", ", dup));
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Every design note is in the index, and the index names only notes that exist.</summary>
    [Fact]
    public void EveryDesignNote_IsInTheIndex()
    {
        string dir = Path.Combine(Root, "docs", "design");
        string index = File.ReadAllText(Path.Combine(dir, "README.md"));
        string[] notes = Directory.EnumerateFiles(dir, "*.md").Select(Path.GetFileName)
            .Where(n => n != "README.md").ToArray()!;
        string[] unlisted = notes.Where(n => !index.Contains("`" + n + "`", StringComparison.Ordinal)).ToArray();
        Assert.True(unlisted.Length == 0, "Design notes missing from docs/design/README.md: " + string.Join(", ", unlisted));
        string[] listed = Regex.Matches(index, @"`([a-z0-9-]+\.md)`").Select(m => m.Groups[1].Value).Distinct().ToArray();
        string[] dangling = listed.Where(n => n != "README.md" && !File.Exists(Path.Combine(dir, n))).ToArray();
        Assert.True(dangling.Length == 0, "docs/design/README.md names notes that do not exist: " + string.Join(", ", dangling));
    }

    /// <summary>Every validation case names a file the manifest knows, and the IDs are unique.</summary>
    [Fact]
    public void EveryValidationCase_NamesAManifestFile()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "FlatCheck.Tests", "testdata", "manifest.json")));
        HashSet<string> known = manifest.RootElement.GetProperty("files").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString()!).ToHashSet(StringComparer.Ordinal);
        using JsonDocument validation = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "docs", "validation.json")));
        var ids = new List<string>();
        var problems = new List<string>();
        foreach (JsonElement c in validation.RootElement.GetProperty("cases").EnumerateArray())
        {
            string id = c.GetProperty("id").GetString()!;
            ids.Add(id);
            foreach (string key in new[] { "master", "raw" })
                if (c.TryGetProperty(key, out JsonElement v) && !known.Contains(v.GetString()!))
                    problems.Add($"{id}: {key} {v.GetString()} is not in the manifest");
        }
        string[] dup = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (dup.Length > 0) problems.Add("duplicate case IDs: " + string.Join(", ", dup));
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>CLAUDE.md holds the rules; the explanations live in docs/design/. Over 200 lines it is growing back.</summary>
    [Fact]
    public void ClaudeMd_StaysShort()
    {
        int lines = File.ReadLines(Path.Combine(Root, "CLAUDE.md")).Count();
        Assert.True(lines <= 200, $"CLAUDE.md has {lines} lines; move explanations to docs/design/ (ADR 0001)");
    }

    /// <summary>
    /// ADR 0055 made checkable: siril-cli rewrites the settings file it is given, so every launch
    /// goes through SirilScriptRun, which always hands it a private copy with -i. The only other
    /// process the code starts is the About window's link, opened by the shell. A new launch
    /// site in the applications or in a test, written in any of the three forms the pattern
    /// knows (a new Process or ProcessStartInfo, typed or target-typed, or the static Start),
    /// fails here until it is listed on purpose. It is a text scan, so a start reached through
    /// reflection or a helper outside these projects is not seen. The exact set also proves the
    /// scan saw the sources.
    /// </summary>
    [Fact]
    public void OnlySirilScriptRunAndTheAboutLink_StartAProcess()
    {
        string[] allowed = { "FlatCheck.Wpf/Session/SirilScriptRun.cs", "FlatCheck.Wpf/Views/AboutWindow.xaml.cs" };
        string[] found = CsSources()
            .Where(f => ProcessStart.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();
        Assert.True(found.SequenceEqual(allowed),
            "Files that start a process should be exactly " + string.Join(", ", allowed) + "; found: " + string.Join(", ", found));
    }

    /// <summary>Table rows of docs/requirements.md: "| R-001 | implemented | ..." => R001 => implemented.</summary>
    static Dictionary<string, string> ReadRequirements()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(Path.Combine(Root, "docs", "requirements.md")))
        {
            Match m = RequirementRow.Match(line);
            if (m.Success) result["R" + m.Groups[1].Value] = m.Groups[2].Value;
        }
        Assert.NotEmpty(result);
        return result;
    }

    /// <summary>Every test method in either test project whose name starts with R### and an underscore.</summary>
    static HashSet<string> TestIdsInSources()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string project in TestProjects)
        {
            string dir = Path.Combine(Root, project);
            if (!Directory.Exists(dir)) continue;
            foreach (string f in Directory.EnumerateFiles(dir, "*.cs", SearchOption.TopDirectoryOnly))
                foreach (Match m in TestMethod.Matches(File.ReadAllText(f)))
                    ids.Add("R" + m.Groups[1].Value);
        }
        return ids;
    }

    /// <summary>Every C# source of the five projects, without bin/ and obj/ (generated by MSBuild).</summary>
    static IEnumerable<string> CsSources()
    {
        foreach (string project in SourceProjects)
        {
            string dir = Path.Combine(Root, project);
            if (!Directory.Exists(dir)) continue;
            foreach (string f in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(Root, f).Replace('\\', '/');
                if (rel.Contains("/bin/") || rel.Contains("/obj/")) continue;
                yield return f;
            }
        }
    }
}
