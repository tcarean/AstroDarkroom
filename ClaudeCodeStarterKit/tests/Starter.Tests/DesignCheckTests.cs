using System.Text;
using Xunit;

namespace Starter.Tests;

/// <summary>
/// scripts/check-design.ps1 is the "design notes" step of verify.ps1: every note in docs/design/
/// is named in the index and has a Covers sentence whose paths name files or folders that git
/// has; with -Since it lists the notes that cover a code or test file a branch changed (the
/// kit's ADR 0031). These tests build a throwaway git repository holding one project, Proj, in a
/// subfolder, with the real script and kit-config.ps1, its code under src/ and its tests under
/// tests/ (kit.json), and write the notes each case needs.
/// </summary>
public sealed class DesignCheckTests : IDisposable
{
    /// <summary>What -Since prints for the branch of <see cref="Since_ListsTheNotesThatCoverAChangedFile"/>.</summary>
    private static readonly string[] Touched =
    [
        "touched  docs/design/casenew.md: src/App.Core/case.cs",
        "touched  docs/design/caseold.md: src/App.Core/Case.cs",
        "touched  docs/design/cli.md: src/App.Cli/Program.cs",
        "touched  docs/design/deep.md: src/App.Core/Analysis/Deep/Fitter.cs",
        "touched  docs/design/folder.md: src/App.Core/Analysis/Deep/Fitter.cs, src/App.Core/Analysis/New.cs",
        "touched  docs/design/golden.md: tests/App.Tests/Golden/case_1.txt",
        "touched  docs/design/other.md: src/App.Core/Other.cs",
        "touched  docs/design/props.md: Directory.Build.props",
        "touched  docs/design/stats.md: src/App.Core/Stats.cs",
    ];

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
            "src/App.Core/Artifacts/Report.cs", "src/App.Core/Câmp.cs", "src/App.Cli/Program.cs",
            "src/App.Cli/Helper.cs", "tests/App.Tests/StatsTests.cs", "tests/App.Tests/Golden/case_1.txt",
            "Directory.Build.props", "README.md",
            // Build output is ignored by git, so a name found only there names nothing, and a
            // folder that holds only build output is no folder of the project.
            "src/App.Core/bin/Release/Ghost.cs", "src/App.Old/bin/Old.dll",
        })
        {
            Code(file, "content\n");
        }

        File.WriteAllText(Path.Combine(_repo, ".gitignore"), "bin/\n");
        Git("init", "-q", "-b", "main");
        Git("config", "user.name", "Test");
        Git("config", "user.email", "test@example.com");
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
        // Windows line endings; the marker at the end of a line with the list on the next; the
        // sentence ends at the blank line, so the next paragraph's path is not a Covers path.
        Note("second.md",
            ("# Second\n\nA plain paragraph. **Covers:**\n`App.Core/Other.cs` and `first.md`\n\n" +
             "A later paragraph names `App.Core/Gone.cs`, which is not a Covers path.\n\n## Part\n\nText.\n")
                .ReplaceLineEndings("\r\n"));
        // A byte order mark, sections of level 3 only, a glob, a symbol with a star, flags, a test,
        // and a sentence that ends at the quote block's empty line.
        File.WriteAllText(
            Path.Combine(_project, "docs", "design", "third.md"),
            "# Third\n\n> **Covers:** `App.Tests/Golden/case_*.txt`, `second.md`, `Th.Smooth*`, `--fast`,\n" +
            "> `-o=out/x.txt` and `R001_Median_Works`\n>\n> Later, `App.Core/Gone.cs` was removed.\n\n" +
            "### 1. Part\n\nText.\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        // A name with capitals and an underscore; the marker with the colon outside the bold; a
        // code span that wraps; "e.g." and a line starting with "#" that is no heading, which do
        // not end the sentence; code names whose last part is an extension in another case; a
        // folder named like build output; a name outside ASCII; a bare glob found deeper; and a
        // line of spaces that ends the sentence before the next paragraph's removed path.
        Note("Two_Apps.md",
            "# Two apps\n\n**Covers**: `src/App.Cli/Program.cs`, run as `app --input <file>\n" +
            "--verbose`, e.g. `src/App.Core/Artifacts/Report.cs`, the\n#if DEBUG branch of\n" +
            "`src/App.Core/Câmp.cs`, the goldens `case_*.txt`, and the writers that use\n" +
            "`System.Text.Json` and `RunOutcome.Png`\n   \nLater, `src/App.Core/RunOn.cs` was removed.\n");
        Index(
            "| Two parts | `first.md`, `second.md` | `Stats.cs` |\n| Goldens | `third.md` | |\n" +
            "| Apps | `Two_Apps.md` | `README.md`, the project's own |\n\n" +
            "The table names every note; `requirements.md` is named here in prose only.\n\n" +
            "```text\nAn example that names `ghost.md`, which is not a note.\n```\n\n" +
            "> ~~~\n> | Example | `ghost2.md` | |\n> ~~~\n");

        (int exitCode, string output) = RunCheck();
        Assert.True(exitCode == 0, output);
        Assert.Contains("ok       4 design notes in docs/design/", output);
    }

    [Fact]
    public void EachBrokenRule_IsNamed()
    {
        Note("orphan.md", "# Orphan\n\n**Covers:** `App.Core/Stats.cs`.\n");
        Note("nocover.md", "# No cover\n\nIntro.\n\n## Later\n\n**Covers:** `App.Core/Stats.cs`.\n");
        Note("codeonly.md", "# Code only\n\nThe marker `Covers:` is quoted here, not used.\n\n## Part\n\nText.\n");
        Note("level3.md", "# Level 3\n\n### Part\n\n**Covers:** `App.Core/Stats.cs`.\n");
        Note("comment.md", "# Comment\n\n<!-- **Covers:** `App.Core/Stats.cs`. -->\nText.\n");
        Note("heading.md", "# Heading Covers: `App.Core/Stats.cs`\n\nText.\n");
        Note("fenced.md", "# Fenced\n\n> ```text\n> **Covers:** `App.Core/Stats.cs`.\n> ```\n\n~~~\n**Covers:** `App.Core/Stats.cs`.\n~~~\n");
        File.WriteAllText(
            Path.Combine(_project, "docs", "design", "bom.md"),
            "## Part\n\n**Covers:** `App.Core/Stats.cs`.\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Note("nopath.md", "# No path\n\n**Covers:** the whole analysis, `SmoothAnalyzer` and `Stats.Median`.\n");
        Note("missing.md", "# Missing\n\n**Covers:** `App.Core/Gone.cs`, and again `App.Core/Gone.cs`.\n");
        Note("case.md", "# Case\n\n**Covers:** `app.core/stats.cs` and `App.Tests/Golden/Case_*.txt`.\n");
        Note("ghost.md", "# Ghost\n\n**Covers:** `Ghost.cs` and `src/App.Old/`.\n");
        Note("wrapped.md", "# Wrapped\n\n> **Covers:** `src/App.Core/Stats.cs`, run as `app --input <file>\n> --verbose` and `src/App.Core/WrapMissing.cs`.\n");
        Note("hash.md", "# Hash\n\n**Covers:** `src/App.Core/Stats.cs`, guarded by\n#if DEBUG in `src/App.Core/HashMissing.cs`.\n");
        Note("abbr.md", "# Abbr\n\n**Covers:** `src/App.Core/Stats.cs`, i.e. `src/App.Core/AbbrMissing.cs` and `Gone.props`.\n");
        Note("glob.md", "# Glob\n\n**Covers:** `src/*.cs` and `src/App.Core/Foo[*.cs`.\n");
        Note("deleted.md", "# Deleted\n\n**Covers:** `src/App.Core/Deleted.cs`.\n");
        Code("src/App.Core/Deleted.cs", "content\n");
        Git("add", "Proj/src/App.Core/Deleted.cs");
        File.Delete(Path.Combine(_project, "src", "App.Core", "Deleted.cs"));
        Index("| A | `nocover.md`, `codeonly.md`, `level3.md`, `comment.md`, `heading.md`, `fenced.md`, `bom.md` | |\n" +
              "| B | `nopath.md`, `missing.md`, `case.md`, `ghost.md`, `wrapped.md`, `hash.md`, `abbr.md` | |\n" +
              "| C | `glob.md`, `deleted.md`, `gone.md` | |\n");

        (int exitCode, string output) = RunCheck();
        Assert.Equal(1, exitCode);
        string[] lines = Lines(output);
        Assert.Contains("INDEX      docs/design/orphan.md: not named in docs/design/README.md", lines);
        Assert.Contains("INDEX      docs/design/README.md: names `gone.md`, which is not a note in docs/design/", lines);
        foreach (string note in new[] { "nocover", "codeonly", "level3", "comment", "heading", "fenced", "bom" })
        {
            Assert.Contains($"COVERS     docs/design/{note}.md: no 'Covers:' sentence above its first section", lines);
        }

        Assert.Contains("COVERS     docs/design/nopath.md: a 'Covers:' sentence names no file or folder as a path", lines);
        foreach ((string note, string span) in new[]
        {
            ("missing", "App.Core/Gone.cs"), ("case", "app.core/stats.cs"), ("case", "App.Tests/Golden/Case_*.txt"),
            ("ghost", "Ghost.cs"), ("ghost", "src/App.Old/"), ("wrapped", "src/App.Core/WrapMissing.cs"),
            ("hash", "src/App.Core/HashMissing.cs"), ("abbr", "src/App.Core/AbbrMissing.cs"), ("abbr", "Gone.props"),
            ("glob", "src/*.cs"), ("glob", "src/App.Core/Foo[*.cs"), ("deleted", "src/App.Core/Deleted.cs"),
        })
        {
            Assert.Contains($"MISSING    docs/design/{note}.md: Covers names `{span}`, which is no file or folder of this project", lines);
        }

        Assert.Single(lines, l => l.Contains("`App.Core/Gone.cs`", StringComparison.Ordinal));
        Assert.DoesNotContain("orphan.md: Covers", output);
        Assert.Equal(22, lines.Count(l => l.Length > 0));
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

    /// <summary>The paths are git's, so a project outside a git repository cannot be checked.</summary>
    [Fact]
    public void AProjectOutsideGit_IsAnError()
    {
        Note("first.md", "# First\n\n**Covers:** `App.Core/Stats.cs`.\n");
        Index("| A | `first.md` | |\n");
        ScriptRunner.DeleteTree(Path.Combine(_repo, ".git"));

        (int exitCode, string output) = RunCheck();
        Assert.Equal(1, exitCode);
        Assert.Contains("GIT        cannot list the files of ", output);
    }

    /// <summary>
    /// -Since lists a note once a code or test file its Covers sentence names changed on the
    /// branch, committed or not, deleted, renamed (also by letter case only) or new under a
    /// covered folder, with the letter case and the one-name globs of the check; it ignores files
    /// no note covers, the notes themselves, files outside the code and test folders and files of
    /// kit.json, and the rest of the repository. The script runs from the repository root.
    /// </summary>
    [Fact]
    public void Since_ListsTheNotesThatCoverAChangedFile()
    {
        File.WriteAllText(
            Path.Combine(_project, "kit.json"),
            ScriptRunner.KitJson(null).Replace("\"testFolders\"", "\"codeFiles\": [\"Directory.Build.props\"], \"testFolders\"", StringComparison.Ordinal));
        Code("src/App.Core/Case.cs", "content\n");
        Note("cli.md", "# Cli\n\n**Covers:** `src/App.Cli/Program.cs`.\n");
        Note("deep.md", "# Deep\n\n**Covers:** `Fitter.cs`.\n");
        Note("folder.md", "# Folder\n\n**Covers:** `App.Core/Analysis/`.\n");
        Note("golden.md", "# Golden\n\n**Covers:** `App.Tests/Golden/case_*.txt`.\n");
        Note("other.md", "# Other\n\n**Covers:** `App.Core/Other.cs`.\n");
        Note("props.md", "# Props\n\n**Covers:** `Directory.Build.props`.\n");
        Note("root.md", "# Root\n\n**Covers:** `README.md`.\n");
        Note("stats.md", "# Stats\n\n**Covers:** `App.Core/Stats.cs`.\n");
        Note("lower.md", "# Lower\n\n**Covers:** `App.Core/stats.cs`.\n");
        Note("caseold.md", "# Case old\n\n**Covers:** `App.Core/Case.cs`.\n");
        Note("casenew.md", "# Case new\n\n**Covers:** `App.Core/case.cs`.\n");
        Note("outside.md", "# Outside\n\n**Covers:** `outside.cs`.\n");
        Note("notes.md", "# Notes\n\n**Covers:** `stats.md`.\n");
        File.WriteAllText(Path.Combine(_repo, "outside.cs"), "content\n");
        Commit("Start");

        Git("switch", "-q", "-c", "claude/Task");
        Code("src/App.Core/Stats.cs", "changed\n");
        Code("src/App.Core/Analysis/Deep/Fitter.cs", "changed\n");
        File.Delete(Path.Combine(_project, "tests", "App.Tests", "Golden", "case_1.txt"));
        Code("tests/App.Tests/Golden/sub/case_2.txt", "new\n");
        Git("mv", "Proj/src/App.Core/Other.cs", "Proj/src/App.Core/Renamed.cs");
        Git("mv", "Proj/src/App.Core/Case.cs", "Proj/src/App.Core/case.cs");
        Code("src/App.Cli/Helper.cs", "changed\n");
        Code("Directory.Build.props", "changed\n");
        Code("README.md", "changed\n");
        File.AppendAllText(Path.Combine(_project, "docs", "design", "stats.md"), "More.\n");
        File.WriteAllText(Path.Combine(_repo, "outside.cs"), "changed\n");
        Commit("Change");
        // Not committed: a changed covered file, and a new file under a covered folder.
        Code("src/App.Cli/Program.cs", "changed\n");
        Code("src/App.Core/Analysis/New.cs", "new\n");

        (int exitCode, string output) = RunCheckFrom(_repo, "-Since", "main");
        Assert.True(exitCode == 0, output);
        string[] touched = Lines(output).Where(l => l.StartsWith("touched", StringComparison.Ordinal)).ToArray();
        Assert.Equal(Touched, touched);
    }

    [Fact]
    public void Since_WithAnUnknownRef_IsAnError()
    {
        Index(string.Empty);
        Commit("Start");
        Git("switch", "-q", "-c", "claude/Task");

        (int exitCode, string output) = RunCheck("-Since", "nosuchref");
        Assert.Equal(1, exitCode);
        Assert.Contains("GIT        cannot find where 'nosuchref' and HEAD meet", output);
    }

    /// <summary>
    /// Run where HEAD is on the ref itself (the main checkout, not the task's worktree), -Since
    /// would compare the branch with itself and list nothing; that is an error, not an "ok".
    /// </summary>
    [Fact]
    public void Since_OnTheRefItself_IsAnError()
    {
        Note("stats.md", "# Stats\n\n**Covers:** `App.Core/Stats.cs`.\n");
        Index("| A | `stats.md` | |\n");
        Commit("Start");
        Code("src/App.Core/Stats.cs", "changed\n");

        (int exitCode, string output) = RunCheck("-Since", "main");
        Assert.Equal(1, exitCode);
        Assert.Contains("GIT        HEAD is on 'main' itself", output);
        Assert.DoesNotContain("ok ", output);
    }

    public void Dispose()
    {
        ScriptRunner.DeleteTree(_repo);
    }

    private static string[] Lines(string output)
    {
        return output.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
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
