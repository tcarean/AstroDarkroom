using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Starter.Tests;

/// <summary>
/// Tests about the repository itself: things a rule in CLAUDE.md asks for and a test can
/// enforce instead. Cheap to write, and they never get "forgotten" in a long session.
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

    [Fact]
    public void Manifest_ListsEveryFixtureInRepo()
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "testdata", "manifest.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        HashSet<string> listed = doc.RootElement.GetProperty("files").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        string[] onDisk = Directory.GetFiles(Path.Combine(TestData.Root, "fixtures"))
            .Select(Path.GetFileName)
            .ToArray()!;

        foreach (string file in onDisk)
        {
            Assert.Contains(file, listed);
        }
    }

    /// <summary>Rule 1 made checkable: an implemented requirement has at least one test named after it.</summary>
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
