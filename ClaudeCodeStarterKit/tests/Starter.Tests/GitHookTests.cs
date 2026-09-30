using Xunit;

namespace Starter.Tests;

/// <summary>
/// The git hooks gate every way a commit enters the history, merges included (ADR 0022). A merge
/// that completes without conflicts runs neither pre-commit nor the Claude commit hook, only
/// pre-merge-commit; before that hook existed, a clean merge into main was recorded with no gate
/// at all. These tests build a throwaway repository whose core.hooksPath is a copy of the kit's
/// scripts/hooks/, holding one project, A, with a stub verify.ps1 that records that it ran and
/// exits with a chosen code. Windows only: the hooks call powershell.exe. pre-commit also
/// refuses a Claude Code session's commit on a claude/ branch in the main checkout (the kit's
/// ADR 0026); a test plays that session by passing CLAUDECODE, which ScriptRunner removes
/// otherwise.
/// </summary>
public sealed class GitHookTests : IDisposable
{
    private static readonly Dictionary<string, string> ClaudeSession = new() { ["CLAUDECODE"] = "1" };

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
        File.WriteAllText(Path.Combine(_repo, ".gitignore"), "hooks/\nran.txt\nwt/\n");
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
        // Absolute, as in a monorepo (the kit's ADR 0016): a relative path would resolve against a
        // linked worktree's own root, where hooks/ is ignored, and no hook would run there.
        Git("config", "core.hooksPath", Path.Combine(_repo, "hooks").Replace('\\', '/'));
    }

    [WindowsFact]
    public void CleanMerge_RunsTheGateOfTheProjectsItBrings()
    {
        (int exitCode, string output) = Merge();
        Assert.True(exitCode == 0, output);
        Assert.Equal(["verify.ps1"], Ran(_repo));
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

    [WindowsFact]
    public void SessionCommit_OnAClaudeBranchInTheMainCheckout_IsRefusedBeforeTheGate()
    {
        Git("switch", "-q", "claude/Task");
        Change(_repo);
        string before = GitOutput("rev-parse", "HEAD");
        (int exitCode, string output) = ScriptRunner.RunWith(_repo, ClaudeSession, "git", "commit", "-q", "-m", "Work");
        Assert.NotEqual(0, exitCode);
        Assert.Contains("the kit's ADR 0026", output);
        Assert.Empty(Ran(_repo));
        Assert.Equal(before, GitOutput("rev-parse", "HEAD"));
    }

    [WindowsFact]
    public void OwnerCommit_OnAClaudeBranchInTheMainCheckout_RunsTheGate()
    {
        Git("switch", "-q", "claude/Task");
        Change(_repo);
        (int exitCode, string output) = ScriptRunner.Run(_repo, gitDir: null, "git", "commit", "-q", "-m", "Work");
        Assert.True(exitCode == 0, output);
        Assert.Equal(["verify.ps1"], Ran(_repo));
    }

    [WindowsFact]
    public void SessionCommit_InALinkedWorktree_RunsTheGate()
    {
        Git("worktree", "add", "-q", "wt", "claude/Task");
        string wt = Path.Combine(_repo, "wt");
        Change(wt);
        (int exitCode, string output) = ScriptRunner.RunWith(wt, ClaudeSession, "git", "commit", "-q", "-m", "Work");
        Assert.True(exitCode == 0, output);
        Assert.Equal(["verify.ps1"], Ran(wt));
    }

    [WindowsFact]
    public void SessionMerge_OntoAClaudeBranchInTheMainCheckout_IsRefused()
    {
        Git("switch", "-q", "claude/Task");
        (int exitCode, string output) = ScriptRunner.RunWith(_repo, ClaudeSession, "git", "merge", "--no-ff", "main", "-m", "Merge main");
        Assert.NotEqual(0, exitCode);
        Assert.Contains("the kit's ADR 0026", output);
        Assert.Empty(Ran(_repo));
        Assert.True(File.Exists(Path.Combine(_repo, ".git", "MERGE_HEAD")), "git merge --abort needs the merge still in progress");
    }

    [WindowsFact]
    public void SessionMerge_IntoMain_RunsTheGate()
    {
        (int exitCode, string output) = ScriptRunner.RunWith(_repo, ClaudeSession, "git", "merge", "--no-ff", "claude/Task", "-m", "Merge claude/Task");
        Assert.True(exitCode == 0, output);
        Assert.Equal(["verify.ps1"], Ran(_repo));
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

    /// <summary>What the stub verify.ps1 of A recorded in the checkout at <paramref name="root"/>.</summary>
    private static string[] Ran(string root)
    {
        string path = Path.Combine(root, "A", "ran.txt");
        return File.Exists(path) ? File.ReadAllLines(path).Where(l => l.Length > 0).ToArray() : [];
    }

    /// <summary>A staged change to A in the checkout at <paramref name="root"/>.</summary>
    private void Change(string root)
    {
        File.AppendAllText(Path.Combine(root, "A", "src", "X.cs"), "// work\n");
        Git("-C", root, "add", ".");
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
