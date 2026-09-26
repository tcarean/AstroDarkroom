using Xunit;

namespace Starter.Tests;

/// <summary>
/// scripts/check-staged.ps1 is the machine behind rule 9. A commit made from a linked worktree
/// runs the git hooks with GIT_DIR set and no GIT_WORK_TREE; git then takes the current folder
/// for the top of the work tree, so a script that asks git for the repository root gets its own
/// project folder instead. These tests build a throwaway repository with the project in a
/// subfolder, as in a monorepo, stage a file and run the script with and without GIT_DIR.
/// The code folders come from kit.json, so one test uses a folder other than src/.
/// </summary>
public sealed class CheckStagedTests : IDisposable
{
    private readonly string _repo = Path.Combine(Path.GetTempPath(), "starter-check-staged", Guid.NewGuid().ToString("N"));
    private readonly string _project;

    public CheckStagedTests()
    {
        _project = Path.Combine(_repo, "proj");
        Directory.CreateDirectory(Path.Combine(_project, "scripts"));
        Directory.CreateDirectory(Path.Combine(_project, "src"));
        string scripts = Path.Combine(TestData.FindRepoRoot(), "scripts");
        File.Copy(Path.Combine(scripts, "check-staged.ps1"), Path.Combine(_project, "scripts", "check-staged.ps1"));
        File.Copy(Path.Combine(scripts, "kit-config.ps1"), Path.Combine(_project, "scripts", "kit-config.ps1"));
        WriteKitJson("src/");
        Git("init", "-q");
        File.WriteAllText(Path.Combine(_project, "src", "A.cs"), "class A { }\n");
        Git("add", "proj/src/A.cs");
    }

    [Fact]
    public void SrcWithoutChangelog_IsRefused()
    {
        Assert.Equal(1, RunCheckStaged(gitDir: null));
    }

    [Fact]
    public void SrcWithoutChangelog_IsRefused_WhenGitDirIsSetAsInAWorktreeHook()
    {
        Assert.Equal(1, RunCheckStaged(gitDir: Path.Combine(_repo, ".git")));
    }

    [Fact]
    public void SrcWithChangelog_Passes_WhenGitDirIsSetAsInAWorktreeHook()
    {
        File.WriteAllText(Path.Combine(_project, "CHANGELOG.md"), "# Changelog\n");
        Git("add", "proj/CHANGELOG.md");
        Assert.Equal(0, RunCheckStaged(gitDir: Path.Combine(_repo, ".git")));
    }

    /// <summary>A project whose code is not under src/ names its folders in kit.json; src/ is then not code.</summary>
    [Fact]
    public void CodeFoldersComeFromKitJson()
    {
        WriteKitJson("app/");
        Assert.Equal(0, RunCheckStaged(gitDir: null));

        Directory.CreateDirectory(Path.Combine(_project, "app"));
        File.WriteAllText(Path.Combine(_project, "app", "B.cs"), "class B { }\n");
        Git("add", "proj/app/B.cs");
        Assert.Equal(1, RunCheckStaged(gitDir: null));
    }

    [Fact]
    public void MissingKitJson_IsAnError()
    {
        File.Delete(Path.Combine(_project, "kit.json"));
        (int exitCode, string output) = ScriptRunner.RunScript(_repo, Path.Combine(_project, "scripts", "check-staged.ps1"));
        Assert.Equal(1, exitCode);
        Assert.Contains("kit.json not found", output);
    }

    public void Dispose()
    {
        ScriptRunner.DeleteTree(_repo);
    }

    private void WriteKitJson(string codeFolder)
    {
        File.WriteAllText(Path.Combine(_project, "kit.json"),
            $$"""
            { "solution": "Proj.sln", "codeFolders": ["{{codeFolder}}"], "testFolders": ["tests/"],
              "testData": { "folder": "tests/testdata/", "largeEnv": "PROJ_TESTDATA_LARGE" },
              "mutate": { "testProject": "tests/Proj.Tests/" },
              "release": { "publish": [] } }
            """);
    }

    private void Git(params string[] args)
    {
        Assert.Equal(0, ScriptRunner.Run(_repo, gitDir: null, "git", args).ExitCode);
    }

    /// <summary>Runs the script from the repository root, where git starts a hook.</summary>
    private int RunCheckStaged(string? gitDir)
    {
        return ScriptRunner.Run(_repo, gitDir, ScriptRunner.Shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            Path.Combine(_project, "scripts", "check-staged.ps1")).ExitCode;
    }
}
