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

    /// <summary>
    /// The guide and the README name scripts, folders and files; a rename leaves them pointing at
    /// nothing, and no other check notices (ADR 0011). Kit only: a project made from the kit
    /// deletes GHID.md, and its README still points at the kit's guide.
    /// </summary>
    [Fact]
    public void KitGuideAndReadme_NameOnlyPathsThatExist()
    {
        string root = TestData.FindRepoRoot();
        if (!File.Exists(Path.Combine(root, "GHID.md")))
        {
            return;
        }

        HashSet<string> fileNames = FileNamesOutsideBuildOutput(root);
        List<string> missing = [];
        foreach (string doc in KitDocs)
        {
            foreach (string path in PathReferences(File.ReadAllText(Path.Combine(root, doc))))
            {
                bool exists = IsUnderTopLevelFolder(path)
                    ? File.Exists(Path.Combine(root, path)) || Directory.Exists(Path.Combine(root, path))
                    : fileNames.Contains(path);
                if (!exists && !AbsentOnPurpose.Contains(path))
                {
                    missing.Add($"{doc}: {path}");
                }
            }
        }

        Assert.True(missing.Count == 0,
            $"Paths named in the kit's docs that do not exist in the kit: {string.Join(", ", missing)}");
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

    private static readonly string[] KitDocs = ["GHID.md", "README.md"];

    /// <summary>First segments that make a code span a path relative to the kit root.</summary>
    private static readonly string[] TopLevelFolders =
        ["scripts/", "docs/", "tests/", "src/", ".claude/", ".github/", ".config/", "monorepo/"];

    /// <summary>Folders holding build output or local copies: bin/ has copies of docs that would hide a rename.</summary>
    private static readonly HashSet<string> NotKitFiles = new(StringComparer.Ordinal)
    {
        "bin", "obj", "StrykerOutput", "artifacts", "TestResults", ".vs", ".git", "worktrees",
    };

    /// <summary>Named in the docs on purpose although absent. A new entry is the owner's decision (ADR 0011).</summary>
    private static readonly HashSet<string> AbsentOnPurpose = new(StringComparer.Ordinal)
    {
        "docs/ai-workflow.md",   // deleted on 2026-09-25 (ADR 0006); the guide tells why
        "docs/design/x.md",      // an example name
        "settings.local.json",   // machine-local, git-ignored
        "tests/testdata/large/", // git-ignored; exists only where the large data is
    };

    /// <summary>
    /// Inline code spans without spaces or placeholders, plus the script after each -File, kept
    /// when they look like a path: under a top-level folder of the kit, or a bare file name.
    /// </summary>
    private static IEnumerable<string> PathReferences(string markdown)
    {
        string text = markdown.ReplaceLineEndings("\n");
        string prose = FencedBlock().Replace(text, string.Empty);
        IEnumerable<string> spans = InlineCode().Matches(prose).Select(m => m.Groups[1].Value);
        IEnumerable<string> scripts = FileArgument().Matches(text).Select(m => m.Groups[1].Value);
        return spans.Concat(scripts)
            .Where(s => !NotAPath().IsMatch(s))
            .Where(s => IsUnderTopLevelFolder(s) || BareFileName().IsMatch(s))
            .Distinct(StringComparer.Ordinal);
    }

    private static bool IsUnderTopLevelFolder(string path) =>
        TopLevelFolders.Any(folder => path.StartsWith(folder, StringComparison.Ordinal));

    private static HashSet<string> FileNamesOutsideBuildOutput(string root)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        Stack<string> folders = new();
        folders.Push(root);
        while (folders.Count > 0)
        {
            string folder = folders.Pop();
            foreach (string file in Directory.EnumerateFiles(folder))
            {
                names.Add(Path.GetFileName(file));
            }

            foreach (string sub in Directory.EnumerateDirectories(folder))
            {
                if (!NotKitFiles.Contains(Path.GetFileName(sub)))
                {
                    folders.Push(sub);
                }
            }
        }

        return names;
    }

    [GeneratedRegex(@"<Version>([^<]+)</Version>")]
    private static partial Regex VersionElement();

    [GeneratedRegex(@"^\|\s*R-(\d{3})\s*\|\s*(draft|accepted|implemented|dropped)\s*\|")]
    private static partial Regex RequirementRow();

    [GeneratedRegex(@"^R(\d{3})_")]
    private static partial Regex TestName();

    [GeneratedRegex(@"^[ \t]*```.*?^[ \t]*```[ \t]*$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FencedBlock();

    [GeneratedRegex(@"(?<!`)`([^`\n]+)`(?!`)")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"-File\s+([^\s`]+)")]
    private static partial Regex FileArgument();

    [GeneratedRegex(@"[\s<>*\[\]|]")]
    private static partial Regex NotAPath();

    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_.-]*\.(md|ps1|json|props|yml|sln|csproj|cs)$")]
    private static partial Regex BareFileName();
}
