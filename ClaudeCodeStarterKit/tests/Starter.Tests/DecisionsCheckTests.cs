using Xunit;

namespace Starter.Tests;

/// <summary>
/// scripts/check-decisions.ps1 is the "decisions" step of verify.ps1: it checks the header lines
/// of every ADR and that both sides of a supersession agree (the kit's ADR 0024). These tests
/// build a throwaway "repository" holding a kit and a project whose kit.json points at it, each
/// with the real script, and write the ADRs each case needs.
/// </summary>
public sealed class DecisionsCheckTests : IDisposable
{
    private readonly string _repo = Path.Combine(Path.GetTempPath(), "starter-decisions", Guid.NewGuid().ToString("N"));
    private readonly string _kit;
    private readonly string _project;

    public DecisionsCheckTests()
    {
        _kit = Path.Combine(_repo, "Kit");
        _project = Path.Combine(_repo, "Proj");
        Directory.CreateDirectory(Path.Combine(_repo, ".git"));
        string real = TestData.FindRepoRoot();
        foreach (string dir in new[] { _kit, _project })
        {
            Directory.CreateDirectory(Path.Combine(dir, "scripts"));
            Directory.CreateDirectory(Path.Combine(dir, "docs", "decisions"));
            foreach (string script in new[] { "check-decisions.ps1", "kit-config.ps1" })
            {
                File.Copy(Path.Combine(real, "scripts", script), Path.Combine(dir, "scripts", script));
            }

            File.Copy(
                Path.Combine(real, "docs", "decisions", "0000-template.md"),
                Path.Combine(dir, "docs", "decisions", "0000-template.md"));
        }

        File.WriteAllText(Path.Combine(_kit, "kit.json"), KitJson("."));
        File.WriteAllText(Path.Combine(_project, "kit.json"), KitJson("../Kit"));
        Write(_kit, "0007-a-kit-decision.md", Adr(7, "Status: accepted 2026-09-01"));
    }

    /// <summary>Every form the grammar allows, including a wrapped note outside the decision and a non-ASCII title.</summary>
    [Fact]
    public void WellFormedHeaders_AndSupersessionsRecordedOnBothSides_Pass()
    {
        Write(_project, "0001-first.md", Adr(1, "Status: superseded by 0003"));
        Write(_project, "0002-radial-fit.md", Adr(
            2,
            "Status: accepted 2026-09-01; the list of steps superseded by 0003 (four, not five); the name superseded by 0005",
            title: "Radial fit on 2θ and 4θ",
            consequences: "A reason that no longer holds. (Restated\n  2026-09-20: it moved.)"));
        Write(_project, "0003-third.md", Adr(3, "Status: accepted 2026-09-02", "Supersedes: 0001, 0002 in part (the list of steps)"));
        Write(_project, "0004-fourth.md", Adr(4, "Status: accepted 2026-09-03; the order superseded by 0005; the naming superseded by the kit's ADR 0007"));
        Write(_project, "0005-fifth.md", Adr(5, "Status: accepted 2026-09-04", "Supersedes: 0002 in part (the name); 0004 in part (the order)"));
        Write(_project, "0006-sixth.md", Adr(6, "Status: proposed"));

        (int exitCode, string output) = RunCheck(_project);
        Assert.True(exitCode == 0, output);
        Assert.Contains("ok       6 decisions", output);
    }

    [Fact]
    public void EachBrokenRule_IsNamed()
    {
        Write(_project, "0001-Bad Name.md", Adr(1, "Status: accepted 2026-09-01"));
        Write(_project, "0002-title.md", Adr(3, "Status: accepted 2026-09-01"));
        Write(_project, "0004-first.md", Adr(4, "Status: accepted 2026-09-01"));
        Write(_project, "0004-second.md", Adr(4, "Status: accepted 2026-09-01"));
        Write(_project, "0005-date.md", Adr(5, "Status: accepted 2026-09-01").Replace("Date: 2026-09-01", "Date: 1 September", StringComparison.Ordinal));
        Write(_project, "0006-status.md", Adr(6, "Status: done"));
        Write(_project, "0007-supersedes.md", Adr(7, "Status: accepted 2026-09-01", "Supersedes: 6"));
        Write(_project, "0008-no-decision.md", Adr(8, "Status: accepted 2026-09-01").Replace("## Decision", "## Choice", StringComparison.Ordinal));
        Write(_project, "0009-note.md", Adr(9, "Status: accepted 2026-09-01", decision: "Use A. (Corrected\n2026-09-20: B.)"));

        // One-sided supersessions: whole and in part, from either side.
        Write(_project, "0010-old-whole.md", Adr(10, "Status: accepted 2026-09-01"));
        Write(_project, "0011-old-part.md", Adr(11, "Status: accepted 2026-09-01"));
        Write(_project, "0012-names-both.md", Adr(12, "Status: accepted 2026-09-02", "Supersedes: 0010; 0011 in part (a clause)"));
        Write(_project, "0013-says-whole.md", Adr(13, "Status: superseded by 0015"));
        Write(_project, "0014-says-part.md", Adr(14, "Status: accepted 2026-09-01; a clause superseded by 0015"));
        Write(_project, "0015-names-neither.md", Adr(15, "Status: accepted 2026-09-02"));

        // A number that does not exist, and a supersession that runs backwards.
        Write(_project, "0016-missing.md", Adr(16, "Status: accepted 2026-09-01", "Supersedes: 0099"));
        Write(_project, "0017-backwards.md", Adr(17, "Status: accepted 2026-09-01", "Supersedes: 0018"));
        Write(_project, "0018-newer.md", Adr(18, "Status: superseded by 0017"));

        (int exitCode, string output) = RunCheck(_project);
        Assert.Equal(1, exitCode);
        Assert.Contains("NAME       0001-Bad Name.md", output);
        Assert.Contains("TITLE      0002-title.md: line 1 must start with '# 0002. '", output);
        Assert.Contains("DUPLICATE  0004-second.md: number 0004 is also used by 0004-first.md", output);
        Assert.Contains("DATE       0005-date.md", output);
        Assert.Contains("STATUS     0006-status.md", output);
        Assert.Contains("SUPERSEDES 0007-supersedes.md", output);
        Assert.Contains("DECISION   0008-no-decision.md: has 0 '## Decision' sections", output);
        Assert.Contains("NOTE       0009-note.md: 'Corrected 2026-09-20' inside '## Decision'", output);
        Assert.Contains("LINK       0010-old-whole.md: is superseded by 0012, but its Status is not 'superseded by 0012'", output);
        Assert.Contains("LINK       0011-old-part.md: is superseded in part by 0012, but its Status has no", output);
        Assert.Contains("LINK       0015-names-neither.md: supersedes 0013 according to 0013-says-whole.md, but its Supersedes line does not name '0013'", output);
        Assert.Contains("LINK       0015-names-neither.md: supersedes 0014 according to 0014-says-part.md, but its Supersedes line does not name '0014 in part (...)'", output);
        Assert.Contains("LINK       0016-missing.md: supersedes 0099, which does not exist", output);
        Assert.Contains("LINK       0017-backwards.md: supersedes 0018, which is not older", output);
        Assert.Contains("LINK       0018-newer.md: is superseded by 0017, which is not newer", output);
    }

    [Fact]
    public void KitReference_InAProject_MustNameAnAdrOfTheKit()
    {
        Write(_project, "0001-follows-the-kit.md", Adr(1, "Status: accepted 2026-09-01; the naming superseded by the kit's ADR 0007"));
        Assert.Equal(0, RunCheck(_project).ExitCode);

        Write(_project, "0001-follows-the-kit.md", Adr(1, "Status: accepted 2026-09-01; the naming superseded by the kit's ADR 0008"));
        (int exitCode, string output) = RunCheck(_project);
        Assert.Equal(1, exitCode);
        Assert.Contains("'the kit's ADR 0008' does not exist in the kit's docs/decisions/", output);
    }

    [Fact]
    public void KitReference_InsideTheKit_IsAnError()
    {
        Write(_kit, "0008-cites-itself.md", Adr(8, "Status: accepted 2026-09-01; the naming superseded by the kit's ADR 0007"));
        (int exitCode, string output) = RunCheck(_kit);
        Assert.Equal(1, exitCode);
        Assert.Contains("'the kit's ADR 0007': the kit names its own ADRs by number", output);
    }

    public void Dispose()
    {
        ScriptRunner.DeleteTree(_repo);
    }

    private static (int ExitCode, string Output) RunCheck(string projectDir)
    {
        return ScriptRunner.RunScript(projectDir, Path.Combine(projectDir, "scripts", "check-decisions.ps1"));
    }

    private static void Write(string projectDir, string name, string text)
    {
        File.WriteAllText(Path.Combine(projectDir, "docs", "decisions", name), text);
    }

    /// <summary>An ADR laid out as the template is; the caller gives the whole Status and Supersedes lines.</summary>
    private static string Adr(
        int number,
        string status,
        string? supersedes = null,
        string decision = "The decision.",
        string title = "A decision",
        string consequences = "What follows.")
    {
        string header = supersedes is null ? status + "\n" : status + "\n" + supersedes + "\n";
        return $"# {number:D4}. {title}\n\nDate: 2026-09-01\n{header}\n## Context\n\nWhy.\n\n"
            + $"## Decision\n\n{decision}\n\n## Alternatives rejected\n\n- **Another.** No.\n\n"
            + $"## Consequences\n\n{consequences}\n";
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
