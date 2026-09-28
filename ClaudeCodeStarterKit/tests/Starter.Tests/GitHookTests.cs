using Xunit;

namespace Starter.Tests;

/// <summary>
/// The git hooks gate every way a commit enters the history, merges included (ADR 0022). A merge
/// that completes without conflicts runs neither pre-commit nor the Claude commit hook, only
/// pre-merge-commit; before that hook existed, a clean merge into main was recorded with no gate
/// at all. These tests build a throwaway repository whose core.hooksPath is a copy of the kit's
/// scripts/hooks/, holding one project, A, with a stub verify.ps1 that records that it ran and
/// exits with a chosen code. Windows only: the hooks call powershell.exe.
/// </summary>
public sealed class GitHookTests : IDisposable
{
    private readonly string _repo = Path.Combine(Path.GetTempPath(), "starter-githooks", Guid.NewGuid().ToString("N"));
    private readonly string _a;

    public GitHookTests()
    {
        _a = Path.Combine(_repo, "A");
        Directory.CreateDirectory(Path.Combine(_a, "scripts"));
        Directory.CreateDirectory(Path.Combine(_a, "src"));
        Git("init", "-q", "-b", "main");
        Git("config", "user.name", "Test");
        Git("config", "user.email", "test@example.com");
        ScriptRunner.CopyTree(Path.Combine(TestData.FindRepoRoot(), "scripts", "hooks"), Path.Combine(_repo, "hooks"));
        File.WriteAllText(Path.Combine(_repo, ".gitignore"), "hooks/\nran.txt\n");
        WriteStub("verify.ps1", 0);
        File.WriteAllText(Path.Combine(_a, "src", "X.cs"), "// first\n");
        Git("add", ".");
        GitWithoutHooks("commit", "-q", "-m", "First");

        // A task branch that changes A, and main moving on elsewhere, so the merge is a real one.
        Git("switch", "-q", "-c", "claude/Task");
        File.AppendAllText(Path.Combine(_a, "src", "X.cs"), "// task\n");
        Git("add", ".");
        GitWithoutHooks("commit", "-q", "-m", "Task");
        Git("switch", "-q", "main");
        File.WriteAllText(Path.Combine(_repo, "notes.txt"), "main\n");
        Git("add", ".");
        GitWithoutHooks("commit", "-q", "-m", "Main");
        File.Delete(Path.Combine(_a, "ran.txt"));
        Git("config", "core.hooksPath", "hooks");
    }

    [WindowsFact]
    public void CleanMerge_RunsTheGateOfTheProjectsItBrings()
    {
        (int exitCode, string output) = Merge();
        Assert.True(exitCode == 0, output);
        Assert.Equal(["verify.ps1"], Ran());
        Assert.Equal("Merge claude/Task", GitOutput("log", "-1", "--format=%s"));
    }

    [WindowsFact]
    public void CleanMerge_WithARedGate_IsNotRecorded()
    {
        string before = GitOutput("rev-parse", "HEAD");
        WriteStub("verify.ps1", 1);
        (int exitCode, string output) = Merge();
        Assert.NotEqual(0, exitCode);
        Assert.Contains("gate failed", output);
        Assert.Equal(before, GitOutput("rev-parse", "HEAD"));
        Assert.True(File.Exists(Path.Combine(_repo, ".git", "MERGE_HEAD")), "git merge --abort needs the merge still in progress");
    }

    /// <summary>commit-msg takes one line of at most 100 characters (the kit's ADR 0023).</summary>
    [WindowsTheory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void CommitMsg_LimitsTheLineTo100Characters(int length, bool accepted)
    {
        File.WriteAllText(Path.Combine(_repo, "notes.txt"), "more\n");
        Git("add", ".");
        (int exitCode, string output) = ScriptRunner.Run(_repo, gitDir: null, "git", "commit", "-q", "-m", new string('x', length));
        Assert.True((exitCode == 0) == accepted, output);
        if (!accepted)
        {
            Assert.Contains("100 characters", output);
        }
    }

    public void Dispose()
    {
        ScriptRunner.DeleteTree(_repo);
    }

    private (int ExitCode, string Output) Merge()
    {
        return ScriptRunner.Run(_repo, gitDir: null, "git", "merge", "--no-ff", "claude/Task", "-m", "Merge claude/Task");
    }

    /// <summary>A stand-in for A's verify.ps1: appends its name to ran.txt, then exits.</summary>
    private void WriteStub(string name, int exitCode)
    {
        File.WriteAllText(Path.Combine(_a, "scripts", name),
            $"Add-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'ran.txt') -Value '{name}'\nexit {exitCode}\n");
    }

    private string[] Ran()
    {
        string path = Path.Combine(_a, "ran.txt");
        return File.Exists(path) ? File.ReadAllLines(path).Where(l => l.Length > 0).ToArray() : [];
    }

    private void Git(params string[] args)
    {
        (int exitCode, string output) = ScriptRunner.Run(_repo, gitDir: null, "git", args);
        Assert.True(exitCode == 0, output);
    }

    /// <summary>A commit that only sets up the history a test needs, with no hooks.</summary>
    private void GitWithoutHooks(params string[] args)
    {
        Git(["-c", "core.hooksPath=no-hooks", .. args]);
    }

    private string GitOutput(params string[] args)
    {
        return ScriptRunner.Run(_repo, gitDir: null, "git", args).Output.Trim();
    }
}
