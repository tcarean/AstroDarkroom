using Xunit;

namespace Starter.Tests;

/// <summary>
/// scripts/sync.ps1 keeps a project's scripts/ byte-identical with the kit's, except the
/// kit-only files, and with -Check it is step 0 of verify.ps1. These tests build a throwaway
/// "repository" (a folder with .git) holding a kit (the real scripts/) and a project whose
/// kit.json points at it, then break the project's copy in the three possible ways.
/// </summary>
public sealed class SyncTests : IDisposable
{
    private static readonly string[] KitOnly = ["rename.ps1", "install-hooks.ps1", Path.Combine("hooks", "pre-commit"), Path.Combine("hooks", "commit-msg")];

    private readonly string _repo = Path.Combine(Path.GetTempPath(), "starter-sync", Guid.NewGuid().ToString("N"));
    private readonly string _kit;
    private readonly string _project;

    public SyncTests()
    {
        _kit = Path.Combine(_repo, "Kit");
        _project = Path.Combine(_repo, "Proj");
        Directory.CreateDirectory(Path.Combine(_repo, ".git"));
        string real = Path.Combine(TestData.FindRepoRoot(), "scripts");
        ScriptRunner.CopyTree(real, Path.Combine(_kit, "scripts"));
        ScriptRunner.CopyTree(real, Path.Combine(_project, "scripts"));
        foreach (string file in KitOnly)
        {
            File.Delete(Path.Combine(_project, "scripts", file));
        }

        File.WriteAllText(Path.Combine(_kit, "kit.json"), KitJson("."));
        File.WriteAllText(Path.Combine(_project, "kit.json"), KitJson("../Kit"));
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
            Assert.False(File.Exists(Path.Combine(_project, "scripts", file)), file);
        }

        Assert.Equal(0, RunSync(_project, "-Check").ExitCode);
    }

    [Fact]
    public void FromTheKit_CheckFindsEveryProjectPointingAtIt()
    {
        File.AppendAllText(Path.Combine(_project, "scripts", "verify.ps1"), "# an isolated edit\n");
        // A folder with a kit.json that points elsewhere is not this kit's project.
        Directory.CreateDirectory(Path.Combine(_repo, "Other"));
        File.WriteAllText(Path.Combine(_repo, "Other", "kit.json"), KitJson("../Elsewhere"));

        (int exitCode, string output) = RunSync(_kit, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("check Proj <-", output);
        Assert.DoesNotContain("Other", output);
        Assert.Contains("differs  scripts/verify.ps1", output);
    }

    [Fact]
    public void KitFolderThatDoesNotExist_IsAnError()
    {
        File.WriteAllText(Path.Combine(_project, "kit.json"), KitJson("../Nowhere"));
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

    private static string KitJson(string kit)
    {
        return $$"""
            { "solution": "X.sln", "codeFolders": ["src/"], "testFolders": ["tests/"],
              "testData": { "folder": "tests/testdata/", "largeEnv": "X_TESTDATA_LARGE" },
              "mutate": { "testProject": "tests/X.Tests/" },
              "release": { "publish": [] },
              "kit": "{{kit}}" }
            """;
    }
}
