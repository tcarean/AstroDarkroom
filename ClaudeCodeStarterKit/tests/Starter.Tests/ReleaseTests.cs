using Xunit;

namespace Starter.Tests;

/// <summary>
/// scripts/release.ps1 refuses, before it changes anything, a release off main and a version
/// the owner did not leave room for (the kit's ADR 0029): the floor is the newest released
/// CHANGELOG heading, and a version the owner already set in &lt;Version&gt; is released as it
/// is. These tests build a throwaway repository holding one project, Proj, with the real
/// release.ps1 and kit-config.ps1 and a stub verify.ps1 that records that it ran; the owner has
/// bumped &lt;Version&gt; to 2.3.0 and the newest release is 2.2.0, as in FlatCheck on 2026-10-01.
/// </summary>
public sealed class ReleaseTests : IDisposable
{
    private const string Changelog =
        "# Changelog\n\n## [Unreleased]\n\n### Added\n\n- Something new.\n\n## [2.2.0] - 2026-09-23\n\n- Older.\n";

    private readonly string _repo = Path.Combine(Path.GetTempPath(), "starter-release", Guid.NewGuid().ToString("N"));
    private readonly string _project;
    private readonly string _worktree;

    public ReleaseTests()
    {
        _project = Path.Combine(_repo, "Proj");
        _worktree = _repo + "-wt";
        Directory.CreateDirectory(Path.Combine(_project, "scripts"));
        Git("init", "-q", "-b", "main");
        Git("config", "user.name", "Test");
        Git("config", "user.email", "test@example.com");
        string scripts = Path.Combine(TestData.FindRepoRoot(), "scripts");
        foreach (string script in new[] { "release.ps1", "kit-config.ps1" })
        {
            File.Copy(Path.Combine(scripts, script), Path.Combine(_project, "scripts", script));
        }

        File.WriteAllText(Path.Combine(_project, "scripts", "verify.ps1"),
            "Set-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'ran.txt') -Value 'verify'\nexit 0\n");
        File.WriteAllText(Path.Combine(_project, "kit.json"), ScriptRunner.KitJson(null));
        WriteProps("2.3.0");
        File.WriteAllText(Path.Combine(_project, "CHANGELOG.md"), Changelog);
        File.WriteAllText(Path.Combine(_repo, ".gitignore"), "ran.txt\n");
        Commit("Start");
    }

    [Fact]
    public void OffMain_IsRefused_BeforeAnythingRuns()
    {
        Git("switch", "-q", "-c", "claude/Task");

        (int exitCode, string output) = Release("2.3.0", "-NoPublish");
        Assert.Equal(1, exitCode);
        Assert.Contains("A release is cut on main", output);
        Assert.Contains("branch 'claude/Task'", output);
        AssertNothingChanged();
    }

    [Fact]
    public void ADetachedHead_IsRefused()
    {
        Git("switch", "-q", "--detach");

        (int exitCode, string output) = Release("2.3.0", "-NoPublish");
        Assert.Equal(1, exitCode);
        Assert.Contains("a detached HEAD", output);
        Assert.False(File.Exists(Path.Combine(_project, "ran.txt")), "verify ran");
    }

    /// <summary>A linked worktree may hold main when the main checkout was switched away; it is still not the main checkout.</summary>
    [Fact]
    public void ALinkedWorktreeOnMain_IsRefused()
    {
        Git("switch", "-q", "-c", "parked");
        Git("worktree", "add", "-q", _worktree, "main");

        (int exitCode, string output) = ScriptRunner.RunScript(
            Path.Combine(_worktree, "Proj"), Path.Combine(_worktree, "Proj", "scripts", "release.ps1"), "-Version", "2.3.0", "-NoPublish");
        Assert.Equal(1, exitCode);
        Assert.Contains("this is a linked worktree", output);
        Assert.False(File.Exists(Path.Combine(_worktree, "Proj", "ran.txt")), "verify ran");
    }

    /// <summary>A standalone repository whose main branch has another name says so in kit.json.</summary>
    [Fact]
    public void TheReleaseBranch_ComesFromKitJson()
    {
        Git("branch", "-m", "master");
        (int exitCode, string output) = Release("2.3.0", "-WhatIf");
        Assert.Equal(1, exitCode);
        Assert.Contains("A release is cut on main", output);

        File.WriteAllText(Path.Combine(_project, "kit.json"),
            ScriptRunner.KitJson(null).Replace("\"publish\": []", "\"publish\": [], \"branch\": \"master\"", StringComparison.Ordinal));
        Commit("Release from master");
        (exitCode, output) = Release("2.3.0", "-WhatIf");
        Assert.True(exitCode == 0, output);
    }

    [Theory]
    [InlineData("2.03.0")]
    [InlineData("02.3.0")]
    [InlineData("2.3.00")]
    public void AVersionWithALeadingZero_IsRefused(string version)
    {
        (int exitCode, string output) = Release(version, "-NoPublish");
        Assert.Equal(1, exitCode);
        Assert.Contains("without leading zeros", output);
        AssertNothingChanged();
    }

    /// <summary>A project that never released has no floor: the owner's version goes out as it is.</summary>
    [Fact]
    public void ACHANGELOGWithNoReleasedHeading_HasNoFloor()
    {
        File.WriteAllText(Path.Combine(_project, "CHANGELOG.md"), "# Changelog\n\n## [Unreleased]\n\n- First.\n");
        Commit("No release yet");

        (int exitCode, string output) = Release("2.3.0", "-WhatIf");
        Assert.True(exitCode == 0, output);
        Assert.Contains("newest released none", output);
    }

    [Fact]
    public void AReleasedVersion_IsRefused()
    {
        WriteProps("2.2.0");
        Commit("Back to 2.2.0");

        (int exitCode, string output) = Release("2.2.0", "-NoPublish");
        Assert.Equal(1, exitCode);
        Assert.Contains("already released", output);
    }

    [Fact]
    public void AVersionNotAboveTheNewestReleasedHeading_IsRefused()
    {
        WriteProps("2.1.0");
        Commit("Back to 2.1.0");

        (int exitCode, string output) = Release("2.1.5", "-NoPublish");
        Assert.Equal(1, exitCode);
        Assert.Contains("not above 2.2.0", output);
    }

    [Fact]
    public void AVersionBelowTheOnesTheOwnerSet_IsRefused()
    {
        WriteProps("2.4.0");
        Commit("Owner bumps to 2.4.0");

        (int exitCode, string output) = Release("2.3.0", "-NoPublish");
        Assert.Equal(1, exitCode);
        Assert.Contains("below <Version> 2.4.0", output);
        AssertNothingChanged();
    }

    [Fact]
    public void AnExistingTag_IsRefused()
    {
        Git("tag", "proj-v2.3.0");

        (int exitCode, string output) = Release("2.3.0", "-NoPublish");
        Assert.Equal(1, exitCode);
        Assert.Contains("Tag proj-v2.3.0 already exists", output);
    }

    [Fact]
    public void WhatIf_ChangesNothing()
    {
        (int exitCode, string output) = Release("2.3.0", "-WhatIf");
        Assert.True(exitCode == 0, output);
        Assert.Contains("newest released 2.2.0", output);
        Assert.Contains("leave <Version> at 2.3.0", output);
        AssertNothingChanged();
    }

    /// <summary>The owner already set 2.3.0: the release uses it, closes [Unreleased] and leaves the props file alone.</summary>
    [Fact]
    public void TheVersionTheOwnerSet_IsReleasedAsItIs()
    {
        byte[] props = File.ReadAllBytes(Path.Combine(_project, "Directory.Build.props"));

        (int exitCode, string output) = Release("2.3.0", "-NoPublish");
        Assert.True(exitCode == 0, output);
        Assert.True(File.Exists(Path.Combine(_project, "ran.txt")), "verify did not run");
        Assert.Equal(props, File.ReadAllBytes(Path.Combine(_project, "Directory.Build.props")));
        Assert.Contains("unchanged, already 2.3.0", output);
        Assert.Contains("-- Directory.Build.props CHANGELOG.md", output);

        string changelog = File.ReadAllText(Path.Combine(_project, "CHANGELOG.md"));
        string today = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains($"## [Unreleased]\n\n## [2.3.0] - {today}\n\n### Added\n\n- Something new.", changelog);
    }

    [Fact]
    public void AVersionAboveTheOnesTheOwnerSet_IsWrittenToTheProps()
    {
        (int exitCode, string output) = Release("2.4.0", "-NoPublish");
        Assert.True(exitCode == 0, output);
        Assert.Contains("<Version>2.4.0</Version>", File.ReadAllText(Path.Combine(_project, "Directory.Build.props")));
        Assert.Contains("2.3.0 -> 2.4.0", output);
    }

    public void Dispose()
    {
        ScriptRunner.DeleteTree(_worktree);
        ScriptRunner.DeleteTree(_repo);
    }

    private (int ExitCode, string Output) Release(string version, params string[] args)
    {
        return ScriptRunner.RunScript(_project, Path.Combine(_project, "scripts", "release.ps1"), ["-Version", version, .. args]);
    }

    private void WriteProps(string version)
    {
        File.WriteAllText(Path.Combine(_project, "Directory.Build.props"),
            $"<Project>\n  <PropertyGroup>\n    <Version>{version}</Version>\n  </PropertyGroup>\n</Project>\n");
    }

    /// <summary>No file changed and verify did not run: the refusal came before step 2.</summary>
    private void AssertNothingChanged()
    {
        (int _, string status) = ScriptRunner.Run(_repo, gitDir: null, "git", "status", "--porcelain");
        Assert.Equal(string.Empty, status.Trim());
        Assert.False(File.Exists(Path.Combine(_project, "ran.txt")), "verify ran");
    }

    private void Commit(string message)
    {
        Git("add", "-A");
        Git("-c", "core.hooksPath=no-hooks", "commit", "-q", "-m", message);
    }

    private void Git(params string[] args)
    {
        (int exitCode, string output) = ScriptRunner.Run(_repo, gitDir: null, "git", args);
        Assert.True(exitCode == 0, output);
    }
}
