using Xunit;

namespace Starter.Tests;

/// <summary>
/// scripts/sync.ps1 keeps a project's shared folders (scripts/, .claude/skills/,
/// .claude/agents/) byte-identical with the kit's, except the kit-only files under scripts/,
/// and with -Check it is step 0 of verify.ps1. These tests build a throwaway "repository" (a
/// folder with .git) holding a kit (the real shared folders) and a project whose kit.json points
/// at it, then break the project's copy in the three possible ways.
/// </summary>
public sealed class SyncTests : IDisposable
{
    /// <summary>The shared folders, as sync.ps1 and the Stop hook list them (checked below); the citation test reads them too.</summary>
    internal static readonly string[] Shared = ["scripts", Path.Combine(".claude", "skills"), Path.Combine(".claude", "agents")];
    private static readonly string[] KitOnly =
    [
        Path.Combine("scripts", "rename.ps1"),
        Path.Combine("scripts", "install-hooks.ps1"),
        Path.Combine("scripts", "hooks", "pre-commit"),
        Path.Combine("scripts", "hooks", "pre-merge-commit"),
        Path.Combine("scripts", "hooks", "commit-msg"),
    ];

    private readonly string _repo = Path.Combine(Path.GetTempPath(), "starter-sync", Guid.NewGuid().ToString("N"));
    private readonly string _kit;
    private readonly string _project;

    public SyncTests()
    {
        _kit = Path.Combine(_repo, "Kit");
        _project = Path.Combine(_repo, "Proj");
        Directory.CreateDirectory(Path.Combine(_repo, ".git"));
        string real = TestData.FindRepoRoot();
        foreach (string folder in Shared)
        {
            ScriptRunner.CopyTree(Path.Combine(real, folder), Path.Combine(_kit, folder));
            ScriptRunner.CopyTree(Path.Combine(real, folder), Path.Combine(_project, folder));
        }

        foreach (string file in KitOnly)
        {
            File.Delete(Path.Combine(_project, file));
        }

        File.WriteAllText(Path.Combine(_kit, "kit.json"), ScriptRunner.KitJson("."));
        File.WriteAllText(Path.Combine(_project, "kit.json"), ScriptRunner.KitJson("../Kit"));
    }

    [Fact]
    public void IdenticalCopy_WithoutTheKitOnlyFiles_Passes()
    {
        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(0, exitCode);
        Assert.Contains("identical", output);
    }

    [Fact]
    public void ChangedFile_MissingFile_AndExtraFile_AreEachNamed()
    {
        File.AppendAllText(Path.Combine(_project, "scripts", "verify.ps1"), "# an isolated edit\n");
        File.Delete(Path.Combine(_project, "scripts", "hooks", "claude-track-edits.ps1"));
        File.WriteAllText(Path.Combine(_project, "scripts", "stray.ps1"), "# not in the kit\n");

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("differs  scripts/verify.ps1", output);
        Assert.Contains("missing  scripts/hooks/claude-track-edits.ps1", output);
        Assert.Contains("extra    scripts/stray.ps1", output);
        Assert.Contains("3 difference(s)", output);
    }

    /// <summary>A skill edited in a project, and an agent the project added, are drift like a script.</summary>
    [Fact]
    public void SkillsAndAgents_AreMirroredToo()
    {
        File.AppendAllText(Path.Combine(_project, ".claude", "skills", "wrap-up", "SKILL.md"), "\n12. A project-only step.\n");
        File.WriteAllText(Path.Combine(_project, ".claude", "agents", "extra.md"), "---\nname: extra\n---\n");

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("differs  .claude/skills/wrap-up/SKILL.md", output);
        Assert.Contains("extra    .claude/agents/extra.md", output);

        Assert.Equal(0, RunSync(_project).ExitCode);
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(_kit, ".claude", "skills", "wrap-up", "SKILL.md")),
            File.ReadAllBytes(Path.Combine(_project, ".claude", "skills", "wrap-up", "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(_project, ".claude", "agents", "extra.md")));
    }

    [Fact]
    public void Sync_TakesTheKitCopy_DeletesExtras_AndLeavesKitOnlyFilesOut()
    {
        File.AppendAllText(Path.Combine(_project, "scripts", "verify.ps1"), "# an isolated edit\n");
        File.WriteAllText(Path.Combine(_project, "scripts", "stray.ps1"), "# not in the kit\n");

        Assert.Equal(0, RunSync(_project).ExitCode);

        Assert.Equal(File.ReadAllBytes(Path.Combine(_kit, "scripts", "verify.ps1")), File.ReadAllBytes(Path.Combine(_project, "scripts", "verify.ps1")));
        Assert.False(File.Exists(Path.Combine(_project, "scripts", "stray.ps1")));
        foreach (string file in KitOnly)
        {
            Assert.False(File.Exists(Path.Combine(_project, file)), file);
        }

        Assert.Equal(0, RunSync(_project, "-Check").ExitCode);
    }

    [Fact]
    public void FromTheKit_CheckFindsEveryProjectPointingAtIt()
    {
        File.AppendAllText(Path.Combine(_project, "scripts", "verify.ps1"), "# an isolated edit\n");
        // A folder with a kit.json that points elsewhere is not this kit's project.
        Directory.CreateDirectory(Path.Combine(_repo, "Other"));
        File.WriteAllText(Path.Combine(_repo, "Other", "kit.json"), ScriptRunner.KitJson("../Elsewhere"));

        (int exitCode, string output) = RunSync(_kit, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("check Proj <-", output);
        Assert.DoesNotContain("Other", output);
        Assert.Contains("differs  scripts/verify.ps1", output);
    }

    [Fact]
    public void KitFolderThatDoesNotExist_IsAnError()
    {
        File.WriteAllText(Path.Combine(_project, "kit.json"), ScriptRunner.KitJson("../Nowhere"));
        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("kit not found", output);
    }

    public void Dispose()
    {
        ScriptRunner.DeleteTree(_repo);
    }

    private static (int ExitCode, string Output) RunSync(string projectDir, params string[] args)
    {
        return ScriptRunner.RunScript(projectDir, Path.Combine(projectDir, "scripts", "sync.ps1"), args);
    }

    /// <summary>The two scripts that repeat the list of shared folders name the same ones as <see cref="Shared"/>.</summary>
    [Fact]
    public void TheSharedFolderList_IsTheSameInSyncAndInTheStopHook()
    {
        string scripts = Path.Combine(TestData.FindRepoRoot(), "scripts");
        string[] expected = Shared.Select(f => f.Replace('\\', '/')).ToArray();
        Assert.Equal(expected, QuotedFolders(File.ReadAllText(Path.Combine(scripts, "sync.ps1")), "$Shared = @("));
        Assert.Equal(expected, QuotedFolders(File.ReadAllText(Path.Combine(scripts, "hooks", "claude-stop-gate.ps1")), "$sharedDirs = @("));
    }

    /// <summary>The quoted folders of the PowerShell array that starts at <paramref name="marker"/>, with forward slashes and no trailing one.</summary>
    private static string[] QuotedFolders(string script, string marker)
    {
        int start = script.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, marker + " not found");
        int end = script.IndexOf(')', start);
        return script[(start + marker.Length)..end]
            .Split(',')
            .Select(s => s.Trim().Trim('\'').Replace('\\', '/').TrimEnd('/'))
            .ToArray();
    }
}
