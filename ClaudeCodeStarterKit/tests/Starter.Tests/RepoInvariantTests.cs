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

    [GeneratedRegex(@"<Version>([^<]+)</Version>")]
    private static partial Regex VersionElement();
}
