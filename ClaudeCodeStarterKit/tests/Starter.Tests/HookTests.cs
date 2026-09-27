using System.Text.Json.Nodes;
using Xunit;

namespace Starter.Tests;

/// <summary>
/// The Claude Code hooks judge the project the work is in, not the folder the session started in
/// (ADR 0020). CLAUDE_PROJECT_DIR stays at that folder when the session works in a worktree; the
/// commit gate follows the commit's own folder, the Stop hook the folder of each edited file.
/// These tests build a throwaway repository holding two projects, A and B, each with a kit.json
/// and stub check-staged.ps1 and verify.ps1 that record that they ran and exit with a chosen
/// code. The hooks are the real ones, copied into B, the project the session "started" in.
/// Windows only: the hooks are Windows PowerShell scripts that call cmd, and the settings that
/// run them name powershell; on the Linux CI these tests are skipped.
/// </summary>
public sealed class HookTests : IDisposable
{
    private readonly string _repo = Path.Combine(Path.GetTempPath(), "starter-hooks", Guid.NewGuid().ToString("N"));
    private readonly string _a;
    private readonly string _b;
    private readonly string _temp;
    private readonly string _session = Guid.NewGuid().ToString("N");

    public HookTests()
    {
        _a = Path.Combine(_repo, "A");
        _b = Path.Combine(_repo, "B");
        _temp = Path.Combine(_repo, "tmp");
        Directory.CreateDirectory(_temp);
        Git("init", "-q");
        MakeProject(_a);
        MakeProject(_b);

        string scripts = Path.Combine(TestData.FindRepoRoot(), "scripts");
        File.Copy(Path.Combine(scripts, "kit-config.ps1"), Path.Combine(_b, "scripts", "kit-config.ps1"));
        foreach (string hook in new[] { "claude-commit-gate.ps1", "claude-stop-gate.ps1" })
        {
            File.Copy(Path.Combine(scripts, "hooks", hook), Path.Combine(_b, "scripts", "hooks", hook));
        }
    }

    [WindowsFact]
    public void CommitGate_RunsTheScriptsOfTheProjectInCwd_NotOfClaudeProjectDir()
    {
        (int exitCode, string output) = RunCommitGate(cwd: _a, "git commit -m \"Fix a thing\"");
        Assert.True(exitCode == 0, output);
        Assert.Equal(["check-staged.ps1", "verify.ps1"], Ran(_a));
        Assert.Empty(Ran(_b));
    }

    [WindowsFact]
    public void CommitGate_ReturnsTheVerdictOfThatProject()
    {
        WriteStub(_a, "verify.ps1", 1);
        (int exitCode, string output) = RunCommitGate(cwd: _a, "git commit -m \"Fix a thing\"");
        Assert.Equal(2, exitCode);
        Assert.Contains("verify.ps1 failed in " + _a, output);
        Assert.Empty(Ran(_b));
    }

    /// <summary>The folder named in the command wins over cwd: git -C, or a cd before the commit.</summary>
    [WindowsTheory]
    [InlineData("git -C \"{B}\" commit -m \"Fix a thing\"")]
    [InlineData("git -C ../B commit -m \"Fix a thing\"")]
    [InlineData("cd \"{B}\" && git commit -m \"Fix a thing\"")]
    [InlineData("cd {bashB} && git status && git commit -m \"Fix a thing\"")]
    [InlineData("cd ../B; git commit -m \"Fix a thing\"")]
    [InlineData("Set-Location -Path '{B}'; git commit -m \"Fix a thing\"")]
    [InlineData("Push-Location ../B; Push-Location ../A; Pop-Location; git commit -m \"Fix a thing\"")]
    public void CommitGate_FollowsTheFolderTheCommandMovesTo(string command)
    {
        string bashB = "/" + char.ToLowerInvariant(_b[0]) + _b[2..].Replace('\\', '/');
        command = command.Replace("{B}", _b).Replace("{bashB}", bashB);

        (int exitCode, string output) = RunCommitGate(cwd: _a, command);
        Assert.True(exitCode == 0, output);
        Assert.Equal(["check-staged.ps1", "verify.ps1"], Ran(_b));
        Assert.Empty(Ran(_a));
    }

    [WindowsTheory]
    [InlineData("git commit --no-verify -m \"Fix a thing\"", "--no-verify")]
    [InlineData("git commit --no-veri -m \"Fix a thing\"", "--no-verify")]
    [InlineData("git -C ../B commit -n -m \"Fix a thing\"", "--no-verify")]
    [InlineData("git commit -anm \"Fix a thing\"", "--no-verify")]
    [InlineData("git add . && git commit -m \"First\" && git commit --amend -n --no-edit", "--no-verify")]
    [InlineData("git -c core.hooksPath=nowhere commit -m \"Fix a thing\"", "core.hooksPath")]
    public void CommitGate_BlocksWhatSkipsTheGitHooks_BeforeAnythingRuns(string command, string reason)
    {
        (int exitCode, string output) = RunCommitGate(cwd: _a, command);
        Assert.Equal(2, exitCode);
        Assert.Contains(reason, output);
        Assert.Empty(Ran(_a));
        Assert.Empty(Ran(_b));
    }

    /// <summary>A commit of the repository's own files, outside every project, has no gate to run.</summary>
    [WindowsFact]
    public void CommitGate_LetsACommitOutsideEveryProjectThrough()
    {
        File.WriteAllText(Path.Combine(_repo, ".gitignore"), "tmp/\n");
        Git("add", ".gitignore");
        (int exitCode, string output) = RunCommitGate(cwd: _repo, "git commit -m \"Fix a thing\"");
        Assert.True(exitCode == 0, output);
        Assert.Empty(Ran(_a));
        Assert.Empty(Ran(_b));
    }

    /// <summary>An -n inside the message, or an -m whose value starts with n, is not --no-verify.</summary>
    [WindowsTheory]
    [InlineData("git commit -m \"Say -n in the message\"")]
    [InlineData("git commit -mnote")]
    public void CommitGate_DoesNotTakeAMessageForAnOption(string command)
    {
        (int exitCode, string output) = RunCommitGate(cwd: _a, command);
        Assert.True(exitCode == 0, output);
        Assert.Equal(["check-staged.ps1", "verify.ps1"], Ran(_a));
    }

    /// <summary>Text in a heredoc is written, not run: its "cd" moves nothing.</summary>
    [WindowsFact]
    public void CommitGate_IgnoresACdInsideAHeredoc()
    {
        (int exitCode, string output) = RunCommitGate(cwd: _a, "cat > note.sh <<'EOF'\ncd ../B\nEOF\ngit commit -m \"Fix a thing\"");
        Assert.True(exitCode == 0, output);
        Assert.Equal(["check-staged.ps1", "verify.ps1"], Ran(_a));
        Assert.Empty(Ran(_b));
    }

    /// <summary>Bash's escaped space is part of the path.</summary>
    [WindowsFact]
    public void CommitGate_ReadsAnEscapedSpaceInAPath()
    {
        string spaced = Path.Combine(_repo, "C D");
        MakeProject(spaced);
        (int exitCode, string output) = RunCommitGate(cwd: _a, "cd ../C\\ D && git commit -m \"Fix a thing\"");
        Assert.True(exitCode == 0, output);
        Assert.Equal(["check-staged.ps1", "verify.ps1"], Ran(spaced));
        Assert.Empty(Ran(_a));
    }

    /// <summary>A folder the hook cannot see, like a shell variable, blocks the commit rather than guessing.</summary>
    [WindowsTheory]
    [InlineData("git -C $wt commit -m \"Fix a thing\"")]
    [InlineData("cd \"$env:WT\"; git commit -m \"Fix a thing\"")]
    public void CommitGate_BlocksAFolderItCannotFind(string command)
    {
        (int exitCode, string output) = RunCommitGate(cwd: _a, command);
        Assert.Equal(2, exitCode);
        Assert.Contains("cannot tell which folder", output);
        Assert.Empty(Ran(_a));
        Assert.Empty(Ran(_b));
    }

    /// <summary>The projects come from the files the commit takes, as in pre-commit, not from the folder alone.</summary>
    [WindowsFact]
    public void CommitGate_RunsEveryProjectTheStagedFilesTouch()
    {
        Edit(_a, "src", "X.cs");
        Edit(_b, "src", "Y.cs");
        Git("add", "A/src/X.cs", "B/src/Y.cs");
        (int exitCode, string output) = RunCommitGate(cwd: _a, "git commit -m \"Fix a thing\"");
        Assert.True(exitCode == 0, output);
        Assert.Equal(["check-staged.ps1", "verify.ps1"], Ran(_a));
        Assert.Equal(["check-staged.ps1", "verify.ps1"], Ran(_b));
    }

    /// <summary>From the repository root, where no project is, the staged files still name theirs.</summary>
    [WindowsFact]
    public void CommitGate_FromTheRepositoryRoot_RunsTheProjectOfTheStagedFile()
    {
        Edit(_b, "src", "Y.cs");
        Git("add", "B/src/Y.cs");
        WriteStub(_b, "verify.ps1", 1);
        (int exitCode, string output) = RunCommitGate(cwd: _repo, "git commit -m \"Fix a thing\"");
        Assert.Equal(2, exitCode);
        Assert.Contains("verify.ps1 failed in " + _b, output);
        Assert.Empty(Ran(_a));
    }

    /// <summary>git commit -a takes the changed tracked files too, which are not staged yet.</summary>
    [WindowsFact]
    public void CommitGate_WithAll_CountsTheChangedTrackedFiles()
    {
        Edit(_b, "src", "Y.cs");
        Git("add", "B/src/Y.cs");
        GitCommit("Add Y");
        File.AppendAllText(Path.Combine(_b, "src", "Y.cs"), "// changed\n");
        (int exitCode, string output) = RunCommitGate(cwd: _repo, "git commit -am \"Fix a thing\"");
        Assert.True(exitCode == 0, output);
        Assert.Equal(["check-staged.ps1", "verify.ps1"], Ran(_b));
    }

    [WindowsFact]
    public void CommitGate_IgnoresWhatIsNotACommit()
    {
        (int exitCode, _) = RunCommitGate(cwd: _a, "echo \"git commit\"");
        Assert.Equal(0, exitCode);
        Assert.Empty(Ran(_a));
    }

    [WindowsFact]
    public void StopGate_JudgesAnEditByItsOwnProject_WhateverTheSessionFolder()
    {
        Edit(_a, "src", "X.cs");
        (int exitCode, string output) = RunStopGate(sessionDir: _b);
        Assert.Equal(2, exitCode);
        Assert.Contains(_a + ":", output);
        Assert.Contains("no .verify-stamp", output);
        Assert.DoesNotContain(_b + ":", output);
    }

    [WindowsFact]
    public void StopGate_FreshStampAndDirtyChangelogInThatProject_Pass()
    {
        Edit(_a, "src", "X.cs");
        Green(_a);
        (int exitCode, string output) = RunStopGate(sessionDir: _b);
        Assert.True(exitCode == 0, output);
    }

    /// <summary>git status runs in the edited file's project: a committed, clean CHANGELOG there is caught.</summary>
    [WindowsFact]
    public void StopGate_CleanChangelogInThatProject_IsRefused()
    {
        Edit(_a, "src", "X.cs");
        Green(_a);
        Git("add", "A/CHANGELOG.md");
        GitCommit("Changelog");
        (int exitCode, string output) = RunStopGate(sessionDir: _b);
        Assert.Equal(2, exitCode);
        Assert.Contains("CHANGELOG.md is unchanged", output);
    }

    /// <summary>A shared folder may be edited only in the kit: that project's kit.json decides.</summary>
    [WindowsTheory]
    [InlineData("../Kit", 2)]
    [InlineData(".", 0)]
    public void StopGate_SharedFolderEdit_DependsOnThatProjectsKitJson(string kit, int expected)
    {
        WriteKitJson(_a, kit);
        Edit(_a, "scripts", "extra.ps1");
        (int exitCode, string output) = RunStopGate(sessionDir: _b);
        Assert.True(exitCode == expected, output);
    }

    [WindowsFact]
    public void StopGate_JudgesEachProjectSeparately()
    {
        Edit(_a, "src", "X.cs");
        Green(_a);
        Edit(_b, "src", "Y.cs");
        (int exitCode, string output) = RunStopGate(sessionDir: _a);
        Assert.Equal(2, exitCode);
        Assert.Contains(_b + ":", output);
        Assert.DoesNotContain(_a + ":", output);
    }

    public void Dispose()
    {
        ScriptRunner.DeleteTree(_repo);
    }

    /// <summary>A project folder: kit.json and the two stub check scripts, both green.</summary>
    private static void MakeProject(string project)
    {
        Directory.CreateDirectory(Path.Combine(project, "scripts", "hooks"));
        Directory.CreateDirectory(Path.Combine(project, "src"));
        WriteKitJson(project, "../Kit");
        WriteStub(project, "check-staged.ps1", 0);
        WriteStub(project, "verify.ps1", 0);
    }

    private static void WriteKitJson(string project, string kit)
    {
        File.WriteAllText(Path.Combine(project, "kit.json"),
            $$"""
            { "solution": "X.sln", "codeFolders": ["src/"], "testFolders": ["tests/"],
              "testData": { "folder": "tests/testdata/", "largeEnv": "X_TESTDATA_LARGE" },
              "mutate": { "testProject": "tests/X.Tests/" },
              "release": { "publish": [] },
              "kit": "{{kit}}" }
            """);
    }

    /// <summary>A stand-in for one of the project's check scripts: appends its name to ran.txt, then exits.</summary>
    private static void WriteStub(string project, string name, int exitCode)
    {
        File.WriteAllText(Path.Combine(project, "scripts", name),
            $"Add-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'ran.txt') -Value '{name}'\nexit {exitCode}\n");
    }

    /// <summary>The stubs that ran in a project, in order.</summary>
    private static string[] Ran(string project)
    {
        string path = Path.Combine(project, "ran.txt");
        return File.Exists(path) ? File.ReadAllLines(path).Where(l => l.Length > 0).ToArray() : [];
    }

    /// <summary>Writes a file and adds it to the session's edit list, as claude-track-edits.ps1 does.</summary>
    private void Edit(string project, string folder, string name)
    {
        string path = Path.Combine(project, folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "// edited\n");
        File.AppendAllText(Path.Combine(_temp, $"claude-edits-{_session}.txt"), path + "\n");
    }

    /// <summary>A project after a green verify and a CHANGELOG line: a stamp newer than every edit, a changed CHANGELOG.</summary>
    private static void Green(string project)
    {
        File.WriteAllText(Path.Combine(project, "CHANGELOG.md"), "# Changelog\n");
        string stamp = Path.Combine(project, ".verify-stamp");
        File.WriteAllText(stamp, "green\n");
        File.SetLastWriteTimeUtc(stamp, DateTime.UtcNow.AddMinutes(1));
    }

    private void Git(params string[] args)
    {
        Assert.Equal(0, ScriptRunner.Run(_repo, gitDir: null, "git", args).ExitCode);
    }

    /// <summary>A commit in the throwaway repository, with no hooks: it only sets up the history a test needs.</summary>
    private void GitCommit(string message)
    {
        Git("-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "core.hooksPath=no-hooks", "commit", "-q", "-m", message);
    }

    /// <summary>Runs the commit gate from B, the session's folder, with the command run in <paramref name="cwd"/>.</summary>
    private (int ExitCode, string Output) RunCommitGate(string cwd, string command)
    {
        string payload = new JsonObject
        {
            ["session_id"] = _session,
            ["cwd"] = cwd,
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = "Bash",
            ["tool_input"] = new JsonObject { ["command"] = command },
        }.ToJsonString();
        return RunHook("claude-commit-gate.ps1", payload, sessionDir: _b);
    }

    /// <summary>Runs the Stop hook of a session started, and still standing, in <paramref name="sessionDir"/>.</summary>
    private (int ExitCode, string Output) RunStopGate(string sessionDir)
    {
        string payload = new JsonObject
        {
            ["session_id"] = _session,
            ["cwd"] = sessionDir,
            ["hook_event_name"] = "Stop",
            ["stop_hook_active"] = false,
        }.ToJsonString();
        return RunHook("claude-stop-gate.ps1", payload, sessionDir);
    }

    private (int ExitCode, string Output) RunHook(string hook, string payload, string sessionDir)
    {
        Dictionary<string, string> environment = new()
        {
            ["CLAUDE_PROJECT_DIR"] = sessionDir,
            ["TEMP"] = _temp,
        };
        return ScriptRunner.RunHook(sessionDir, Path.Combine(_b, "scripts", "hooks", hook), payload, environment);
    }
}

/// <summary>A fact that runs on Windows only, where the Claude Code hooks run.</summary>
internal sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "The Claude Code hooks run on Windows only.";
        }
    }
}

/// <summary>A theory that runs on Windows only, where the Claude Code hooks run.</summary>
internal sealed class WindowsTheoryAttribute : TheoryAttribute
{
    public WindowsTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "The Claude Code hooks run on Windows only.";
        }
    }
}
