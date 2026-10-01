using System.Text;
using Xunit;

namespace Starter.Tests;

/// <summary>
/// scripts/check-design.ps1 is the "design notes" step of verify.ps1: every note in docs/design/
/// is named in the index and has a Covers sentence whose paths name files or folders that exist;
/// with -Since it lists the notes that cover a file a branch changed (the kit's ADR 0031). These
/// tests build a throwaway repository holding one project, Proj, in a subfolder, with the real
/// script and kit-config.ps1, its code under src/ and its tests under tests/ (kit.json), and
/// write the notes each case needs.
/// </summary>
public sealed class DesignCheckTests : IDisposable
{
    private readonly string _repo = Path.Combine(Path.GetTempPath(), "starter-design", Guid.NewGuid().ToString("N"));
    private readonly string _project;

    public DesignCheckTests()
    {
        _project = Path.Combine(_repo, "Proj");
        Directory.CreateDirectory(Path.Combine(_project, "scripts"));
        Directory.CreateDirectory(Path.Combine(_project, "docs", "design"));
        string scripts = Path.Combine(TestData.FindRepoRoot(), "scripts");
        foreach (string script in new[] { "check-design.ps1", "kit-config.ps1" })
        {
            File.Copy(Path.Combine(scripts, script), Path.Combine(_project, "scripts", script));
        }

        File.WriteAllText(Path.Combine(_project, "kit.json"), ScriptRunner.KitJson(null));
        foreach (string file in new[]
        {
            "src/App.Core/Stats.cs", "src/App.Core/Other.cs", "src/App.Core/Analysis/Deep/Fitter.cs",
            "src/App.Cli/Program.cs", "src/App.Cli/Helper.cs", "tests/App.Tests/StatsTests.cs",
            "tests/App.Tests/Golden/case_1.txt", "Directory.Build.props",
            // Build output is never searched: a name found only there names nothing.
            "src/App.Core/bin/Release/Ghost.cs",
        })
        {
            Code(file, "content\n");
        }
    }

    /// <summary>Every form of note and of path the check reads, and spans it must leave alone.</summary>
    [Fact]
    public void ANoteSetInTheIndexWithCoversPathsThatExist_Passes()
    {
        Note("first.md",
            "# First\n\n" +
            "> Moved here on 2026-09-01. The word `Covers:` opens the sentence that follows. **Covers:** in\n" +
            "> full, `src/App.Cli/Program.cs`, `App.Core/Stats.cs` (`Stats.Median`), `App.Tests/StatsTests.cs`\n" +
            "> and `Fitter.cs`; by pointer, `App.Core/Analysis/`, `src/App.Cli` and `Directory.Build.props`.\n" +
            "> A change to the code means this note changes too.\n\n" +
            "## Details\n\nText that names `NotAFile.cs` outside any Covers sentence.\n\n" +
            "**Covers:** `App.Core/Stats.cs`, under this section.\n");
        // Windows line endings, and the marker at the end of a line with the list on the next.
        Note("second.md",
            "# Second\n\nA plain paragraph. **Covers:**\n`App.Core/Other.cs` and `first.md`.\n\n## Part\n\nText.\n"
                .ReplaceLineEndings("\r\n"));
        // A byte order mark, sections of level 3 only, a glob, a symbol with a star, a flag, a test.
        File.WriteAllText(
            Path.Combine(_project, "docs", "design", "third.md"),
            "# Third\n\n> **Covers:** `App.Tests/Golden/case_*.txt`, `second.md`, `Th.Smooth*`, `--fast` and\n" +
            "> `R001_Median_Works`.\n\n### 1. Part\n\nText.\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Index(
            "| Two parts | `first.md`, `second.md` | `Stats.cs` |\n| Goldens | `third.md` | |\n\n" +
            "```text\nAn example that names `ghost.md`, which is not a note.\n```\n");

        (int exitCode, string output) = RunCheck();
        Assert.True(exitCode == 0, output);
        Assert.Contains("ok       3 design notes in docs/design/", output);
    }

    [Fact]
    public void EachBrokenRule_IsNamed()
    {
        Note("orphan.md", "# Orphan\n\n**Covers:** `App.Core/Stats.cs`.\n");
        Note("nocover.md", "# No cover\n\nIntro.\n\n## Later\n\n**Covers:** `App.Core/Stats.cs`.\n");
        Note("codeonly.md", "# Code only\n\nThe marker `Covers:` is quoted here, not used.\n\n## Part\n\nText.\n");
        Note("missing.md", "# Missing\n\n**Covers:** `App.Core/Gone.cs`.\n");
        Note("case.md", "# Case\n\n**Covers:** `app.core/stats.cs`.\n");
        Note("ghost.md", "# Ghost\n\n**Covers:** `Ghost.cs`.\n");
        Index("| A | `nocover.md`, `codeonly.md`, `missing.md`, `case.md`, `ghost.md`, `gone.md` | |\n");

        (int exitCode, string output) = RunCheck();
        Assert.Equal(1, exitCode);
        Assert.Contains("INDEX      docs/design/orphan.md: not named in docs/design/README.md", output);
        Assert.Contains("INDEX      docs/design/README.md: names `gone.md`, which is not a note in docs/design/", output);
        Assert.Contains("COVERS     docs/design/codeonly.md: no 'Covers:' sentence above its first section", output);
        Assert.Contains("COVERS     docs/design/nocover.md: no 'Covers:' sentence above its first section", output);
        Assert.Contains("MISSING    docs/design/missing.md: Covers names `App.Core/Gone.cs`, which is no file or folder of this project", output);
        Assert.Contains("MISSING    docs/design/case.md: Covers names `app.core/stats.cs`, which is no file or folder of this project", output);
        Assert.Contains("MISSING    docs/design/ghost.md: Covers names `Ghost.cs`, which is no file or folder of this project", output);
        Assert.DoesNotContain("orphan.md: Covers", output);
    }

    [Fact]
    public void ANoteSetWithoutItsIndex_IsNamed()
    {
        Note("first.md", "# First\n\n**Covers:** `App.Core/Stats.cs`.\n");

        (int exitCode, string output) = RunCheck();
        Assert.Equal(1, exitCode);
        Assert.Contains("INDEX      docs/design/README.md: missing: it is the index of the notes", output);
    }

    [Fact]
    public void AProjectWithoutDesignNotes_IsNotChecked()
    {
        Directory.Delete(Path.Combine(_project, "docs", "design"));

        (int exitCode, string output) = RunCheck();
        Assert.True(exitCode == 0, output);
        Assert.Contains("no docs/design/ folder, nothing to check", output);
    }

    /// <summary>
    /// -Since lists a note once a file its Covers sentence names changed on the branch, committed
    /// or not, deleted, renamed or new under a covered folder; it ignores files no note covers,
    /// the notes themselves, files outside the code and test folders, and the rest of the
    /// repository. The script runs from the repository root, not from the project.
    /// </summary>
    [Fact]
    public void Since_ListsTheNotesThatCoverAChangedFile()
    {
        Note("cli.md", "# Cli\n\n**Covers:** `src/App.Cli/Program.cs`.\n");
        Note("deep.md", "# Deep\n\n**Covers:** `Fitter.cs`.\n");
        Note("folder.md", "# Folder\n\n**Covers:** `App.Core/Analysis/`.\n");
        Note("golden.md", "# Golden\n\n**Covers:** `App.Tests/Golden/case_*.txt`.\n");
        Note("other.md", "# Other\n\n**Covers:** `App.Core/Other.cs`.\n");
        Note("props.md", "# Props\n\n**Covers:** `Directory.Build.props`.\n");
        Note("stats.md", "# Stats\n\n**Covers:** `App.Core/Stats.cs`.\n");
        Index("| All | `cli.md`, `deep.md`, `folder.md`, `golden.md`, `other.md`, `props.md`, `stats.md` | |\n");
        File.WriteAllText(Path.Combine(_repo, "outside.cs"), "content\n");
        File.WriteAllText(Path.Combine(_repo, ".gitignore"), "bin/\n");
        Git("init", "-q", "-b", "main");
        Git("config", "user.name", "Test");
        Git("config", "user.email", "test@example.com");
        Commit("Start");

        Git("switch", "-q", "-c", "claude/Task");
        Code("src/App.Core/Stats.cs", "changed\n");
        Code("src/App.Core/Analysis/Deep/Fitter.cs", "changed\n");
        File.Delete(Path.Combine(_project, "tests", "App.Tests", "Golden", "case_1.txt"));
        Git("mv", "Proj/src/App.Core/Other.cs", "Proj/src/App.Core/Renamed.cs");
        Code("src/App.Cli/Helper.cs", "changed\n");
        Code("Directory.Build.props", "changed\n");
        File.AppendAllText(Path.Combine(_project, "docs", "design", "stats.md"), "More.\n");
        File.WriteAllText(Path.Combine(_repo, "outside.cs"), "changed\n");
        Commit("Change");
        // Not committed: a changed covered file, and a new file under a covered folder.
        Code("src/App.Cli/Program.cs", "changed\n");
        Code("src/App.Core/Analysis/New.cs", "new\n");

        (int exitCode, string output) = RunCheckFrom(_repo, "-Since", "main");
        Assert.True(exitCode == 0, output);
        Assert.Contains("touched  docs/design/cli.md: src/App.Cli/Program.cs", output);
        Assert.Contains("touched  docs/design/deep.md: src/App.Core/Analysis/Deep/Fitter.cs", output);
        Assert.Contains("touched  docs/design/folder.md: src/App.Core/Analysis/Deep/Fitter.cs, src/App.Core/Analysis/New.cs", output);
        Assert.Contains("touched  docs/design/golden.md: tests/App.Tests/Golden/case_1.txt", output);
        Assert.Contains("touched  docs/design/other.md: src/App.Core/Other.cs", output);
        Assert.Contains("touched  docs/design/stats.md: src/App.Core/Stats.cs", output);
        Assert.DoesNotContain("props.md", output);
        Assert.DoesNotContain("Helper.cs", output);
        Assert.DoesNotContain("outside.cs", output);
        Assert.DoesNotContain("stats.md: docs", output);
    }

    [Fact]
    public void Since_WithAnUnknownRef_IsAnError()
    {
        Index(string.Empty);
        Git("init", "-q", "-b", "main");
        Git("config", "user.name", "Test");
        Git("config", "user.email", "test@example.com");
        Commit("Start");

        (int exitCode, string output) = RunCheck("-Since", "nosuchref");
        Assert.Equal(1, exitCode);
        Assert.Contains("GIT        cannot find where 'nosuchref' and HEAD meet", output);
    }

    public void Dispose()
    {
        ScriptRunner.DeleteTree(_repo);
    }

    private (int ExitCode, string Output) RunCheck(params string[] args)
    {
        return RunCheckFrom(_project, args);
    }

    private (int ExitCode, string Output) RunCheckFrom(string workDir, params string[] args)
    {
        return ScriptRunner.RunScript(workDir, Path.Combine(_project, "scripts", "check-design.ps1"), args);
    }

    private void Code(string relative, string text)
    {
        string path = Path.Combine(_project, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private void Note(string name, string text)
    {
        File.WriteAllText(Path.Combine(_project, "docs", "design", name), text);
    }

    private void Index(string rows)
    {
        Note("README.md", "# Design notes\n\n| Area | Note | Read before editing |\n| --- | --- | --- |\n" + rows);
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
