using System.Diagnostics;
using Xunit;

namespace Starter.Tests;

/// <summary>
/// scripts/check-staged.ps1 is the machine behind rule 9. A commit made from a linked worktree
/// runs the git hooks with GIT_DIR set and no GIT_WORK_TREE; git then takes the current folder
/// for the top of the work tree, so a script that asks git for the repository root gets its own
/// project folder instead. These tests build a throwaway repository with the project in a
/// subfolder, as in a monorepo, stage a file and run the script with and without GIT_DIR.
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
        File.Copy(Path.Combine(TestData.FindRepoRoot(), "scripts", "check-staged.ps1"),
            Path.Combine(_project, "scripts", "check-staged.ps1"));
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

    public void Dispose()
    {
        if (!Directory.Exists(_repo))
        {
            return;
        }

        // git writes its objects read-only; Directory.Delete refuses those on Windows.
        foreach (string file in Directory.EnumerateFiles(_repo, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_repo, recursive: true);
    }

    private void Git(params string[] args)
    {
        Assert.Equal(0, Run(_repo, gitDir: null, "git", args));
    }

    /// <summary>Runs the script from the repository root, where git starts a hook.</summary>
    private int RunCheckStaged(string? gitDir)
    {
        string shell = OperatingSystem.IsWindows() ? "powershell" : "pwsh";
        return Run(_repo, gitDir, shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            Path.Combine(_project, "scripts", "check-staged.ps1"));
    }

    /// <summary>
    /// Every GIT_* variable is removed first: these tests also run inside the real pre-commit
    /// hook, and an inherited GIT_DIR would point git at the real repository.
    /// </summary>
    private static int Run(string workDir, string? gitDir, string file, params string[] args)
    {
        ProcessStartInfo start = new(file)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        foreach (string name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            start.Environment.Remove(name);
        }

        if (gitDir is not null)
        {
            start.Environment["GIT_DIR"] = gitDir;
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {file}.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdout, stderr);
        return process.ExitCode;
    }
}
