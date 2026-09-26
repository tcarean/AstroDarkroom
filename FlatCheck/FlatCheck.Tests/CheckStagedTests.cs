// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Diagnostics;

namespace FlatCheck.Tests;

/// <summary>
/// scripts/check-staged.ps1 is the machine behind rule 9. A commit made from a linked worktree
/// runs the git hooks with GIT_DIR set and no GIT_WORK_TREE; git then takes the current folder
/// for the top of the work tree, so a script that asks git for the repository root gets the
/// FlatCheck folder instead, and every staged FlatCheck file looks like console code. These
/// tests build a throwaway repository with a FlatCheck folder in it, as in the photo monorepo,
/// stage files and run the script with and without GIT_DIR. The script reads the code folders
/// from kit.json (ADR 0048), so the real kit.json of FlatCheck is copied beside it: these tests
/// check FlatCheck's own configuration, not the script, which the kit tests.
/// </summary>
public sealed class CheckStagedTests : IDisposable
{
    readonly TempWorkspace _ws = new();
    readonly string _project;

    public CheckStagedTests()
    {
        _project = Path.Combine(_ws.Dir, "FlatCheck");
        Directory.CreateDirectory(Path.Combine(_project, "scripts"));
        foreach (string script in new[] { "check-staged.ps1", "kit-config.ps1" })
            File.Copy(Path.Combine(TestData.RepoDir(), "scripts", script), Path.Combine(_project, "scripts", script));
        File.Copy(Path.Combine(TestData.RepoDir(), "kit.json"), Path.Combine(_project, "kit.json"));
        Git("init", "-q");
    }

    [Fact]
    public void DocsOnly_Pass_WhenGitDirIsSetAsInAWorktreeHook()
    {
        Stage("FlatCheck/CLAUDE.md");
        Assert.Equal(0, RunCheckStaged(Path.Combine(_ws.Dir, ".git")));
    }

    [Fact]
    public void CoreCodeWithoutChangelog_IsRefused_WhenGitDirIsSetAsInAWorktreeHook()
    {
        Stage("FlatCheck/FlatCheck.Core/A.cs");
        Assert.Equal(1, RunCheckStaged(Path.Combine(_ws.Dir, ".git")));
    }

    [Fact]
    public void CoreCodeWithoutChangelog_IsRefused()
    {
        Stage("FlatCheck/FlatCheck.Core/A.cs");
        Assert.Equal(1, RunCheckStaged(null));
    }

    [Fact]
    public void CoreCodeWithChangelog_Passes_WhenGitDirIsSetAsInAWorktreeHook()
    {
        Stage("FlatCheck/FlatCheck.Core/A.cs");
        Stage("FlatCheck/CHANGELOG.md");
        Assert.Equal(0, RunCheckStaged(Path.Combine(_ws.Dir, ".git")));
    }

    public void Dispose()
    {
        // git writes its objects read-only; Directory.Delete refuses those on Windows.
        foreach (string file in Directory.EnumerateFiles(_ws.Dir, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        _ws.Dispose();
    }

    void Stage(string relativePath)
    {
        string path = Path.Combine(_ws.Dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x\n");
        Git("add", relativePath);
    }

    void Git(params string[] args) => Assert.Equal(0, Run(_ws.Dir, null, "git", args));

    /// <summary>Runs the script from the repository root, where git starts a hook.</summary>
    int RunCheckStaged(string? gitDir)
    {
        string shell = OperatingSystem.IsWindows() ? "powershell" : "pwsh";
        return Run(_ws.Dir, gitDir, shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            Path.Combine(_project, "scripts", "check-staged.ps1"));
    }

    /// <summary>
    /// Every GIT_* variable is removed first: these tests also run inside the real pre-commit
    /// hook, and an inherited GIT_DIR would point git at the real repository.
    /// </summary>
    static int Run(string workDir, string? gitDir, string file, params string[] args)
    {
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        foreach (string name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToList())
            start.Environment.Remove(name);
        if (gitDir != null)
            start.Environment["GIT_DIR"] = gitDir;

        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {file}.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdout, stderr);
        return process.ExitCode;
    }
}
