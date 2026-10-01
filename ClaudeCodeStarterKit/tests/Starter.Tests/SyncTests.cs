using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Starter.Tests;

/// <summary>
/// scripts/sync.ps1 keeps a project's shared folders (scripts/, .claude/skills/,
/// .claude/agents/, .claude/rules/kit/) byte-identical with the kit's, except the kit-only files
/// under scripts/, and the process keys of its .claude/settings.json equal to the kit's; with
/// -Check it is step 0 of verify.ps1. These tests build a throwaway "repository" (a folder with
/// .git) holding a kit (the real shared folders and settings) and a project whose kit.json points
/// at it, then break the project's copy in the possible ways.
/// </summary>
public sealed class SyncTests : IDisposable
{
    /// <summary>The shared folders, as kit-config.ps1 lists them for sync.ps1 and the Stop hook (checked below); the citation test reads them too.</summary>
    internal static readonly string[] Shared =
        ["scripts", Path.Combine(".claude", "skills"), Path.Combine(".claude", "agents"), Path.Combine(".claude", "rules", "kit")];

    private static readonly string SettingsPath = Path.Combine(".claude", "settings.json");
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private static readonly string[] KitOnly =
    [
        Path.Combine("scripts", "rename.ps1"),
        Path.Combine("scripts", "install-hooks.ps1"),
        Path.Combine("scripts", "hooks", "pre-commit"),
        Path.Combine("scripts", "hooks", "pre-merge-commit"),
        Path.Combine("scripts", "hooks", "commit-msg"),
    ];

    private readonly string _repo = Path.Combine(Path.GetTempPath(), "starter-sync", Guid.NewGuid().ToString("N"));
    private readonly string _kit;
    private readonly string _project;

    public SyncTests()
    {
        _kit = Path.Combine(_repo, "Kit");
        _project = Path.Combine(_repo, "Proj");
        Directory.CreateDirectory(Path.Combine(_repo, ".git"));
        string real = TestData.FindRepoRoot();
        foreach (string folder in Shared)
        {
            ScriptRunner.CopyTree(Path.Combine(real, folder), Path.Combine(_kit, folder));
            ScriptRunner.CopyTree(Path.Combine(real, folder), Path.Combine(_project, folder));
        }

        foreach (string file in KitOnly)
        {
            File.Delete(Path.Combine(_project, file));
        }

        File.Copy(Path.Combine(real, SettingsPath), Path.Combine(_kit, SettingsPath));
        File.Copy(Path.Combine(real, SettingsPath), Path.Combine(_project, SettingsPath));

        File.WriteAllText(Path.Combine(_kit, "kit.json"), ScriptRunner.KitJson("."));
        File.WriteAllText(Path.Combine(_project, "kit.json"), ScriptRunner.KitJson("../Kit"));
    }

    [Fact]
    public void IdenticalCopy_WithoutTheKitOnlyFiles_Passes()
    {
        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(0, exitCode);
        Assert.Contains("identical", output);
    }

    [Fact]
    public void ChangedFile_MissingFile_AndExtraFile_AreEachNamed()
    {
        File.AppendAllText(Path.Combine(_project, "scripts", "verify.ps1"), "# an isolated edit\n");
        File.Delete(Path.Combine(_project, "scripts", "hooks", "claude-track-edits.ps1"));
        File.WriteAllText(Path.Combine(_project, "scripts", "stray.ps1"), "# not in the kit\n");

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("differs  scripts/verify.ps1", output);
        Assert.Contains("missing  scripts/hooks/claude-track-edits.ps1", output);
        Assert.Contains("extra    scripts/stray.ps1", output);
        Assert.Contains("3 difference(s)", output);
    }

    /// <summary>A skill edited in a project, and an agent the project added, are drift like a script.</summary>
    [Fact]
    public void SkillsAndAgents_AreMirroredToo()
    {
        File.AppendAllText(Path.Combine(_project, ".claude", "skills", "wrap-up", "SKILL.md"), "\n12. A project-only step.\n");
        File.WriteAllText(Path.Combine(_project, ".claude", "agents", "extra.md"), "---\nname: extra\n---\n");

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("differs  .claude/skills/wrap-up/SKILL.md", output);
        Assert.Contains("extra    .claude/agents/extra.md", output);

        Assert.Equal(0, RunSync(_project).ExitCode);
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(_kit, ".claude", "skills", "wrap-up", "SKILL.md")),
            File.ReadAllBytes(Path.Combine(_project, ".claude", "skills", "wrap-up", "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(_project, ".claude", "agents", "extra.md")));
    }

    [Fact]
    public void Sync_TakesTheKitCopy_DeletesExtras_AndLeavesKitOnlyFilesOut()
    {
        File.AppendAllText(Path.Combine(_project, "scripts", "verify.ps1"), "# an isolated edit\n");
        File.WriteAllText(Path.Combine(_project, "scripts", "stray.ps1"), "# not in the kit\n");

        Assert.Equal(0, RunSync(_project).ExitCode);

        Assert.Equal(File.ReadAllBytes(Path.Combine(_kit, "scripts", "verify.ps1")), File.ReadAllBytes(Path.Combine(_project, "scripts", "verify.ps1")));
        Assert.False(File.Exists(Path.Combine(_project, "scripts", "stray.ps1")));
        foreach (string file in KitOnly)
        {
            Assert.False(File.Exists(Path.Combine(_project, file)), file);
        }

        Assert.Equal(0, RunSync(_project, "-Check").ExitCode);
    }

    [Fact]
    public void FromTheKit_CheckFindsEveryProjectPointingAtIt()
    {
        File.AppendAllText(Path.Combine(_project, "scripts", "verify.ps1"), "# an isolated edit\n");
        // A folder with a kit.json that points elsewhere is not this kit's project.
        Directory.CreateDirectory(Path.Combine(_repo, "Other"));
        File.WriteAllText(Path.Combine(_repo, "Other", "kit.json"), ScriptRunner.KitJson("../Elsewhere"));

        (int exitCode, string output) = RunSync(_kit, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("check Proj <-", output);
        Assert.DoesNotContain("Other", output);
        Assert.Contains("differs  scripts/verify.ps1", output);
    }

    [Fact]
    public void KitFolderThatDoesNotExist_IsAnError()
    {
        File.WriteAllText(Path.Combine(_project, "kit.json"), ScriptRunner.KitJson("../Nowhere"));
        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("kit not found", output);
    }

    /// <summary>The allow entries that are not git commands, and $comment, are the project's own.</summary>
    [Fact]
    public void Settings_OwnAllowEntryAndComment_Pass()
    {
        EditSettings(s =>
        {
            s["$comment"] = "This project's own words.";
            s["permissions"]!["allow"]!.AsArray().Add("Bash(npm test*)");
            s["permissions"]!["allow"]!.AsArray().RemoveAt(0);
        });

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.True(exitCode == 0, output);
        Assert.Contains("identical", output);
    }

    /// <summary>Each process key that differs is named: hooks, ask, deny, the git allow entries, attribution.</summary>
    [Fact]
    public void Settings_EachProcessKeyThatDiffers_IsNamed()
    {
        EditSettings(s =>
        {
            s["hooks"]!["Stop"]![0]!["hooks"]![0]!["timeout"] = 5;
            s["permissions"]!["ask"]!.AsArray().RemoveAt(0);
            s["permissions"]!["deny"]!.AsArray().Add("Bash(rm -rf*)");
            s["permissions"]!["allow"]!.AsArray().Add("Bash(git stash*)");
            s.Remove("attribution");
        });

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(1, exitCode);
        foreach (string key in new[] { "hooks", "permissions.ask", "permissions.deny", "permissions.allow", "attribution" })
        {
            Assert.Contains("differs  .claude/settings.json: " + key, output);
        }

        Assert.Contains("5 difference(s)", output);
    }

    /// <summary>The order of a list does not count; what it holds does.</summary>
    [Fact]
    public void Settings_ListsCompareAsSets()
    {
        EditSettings(s =>
        {
            JsonArray ask = s["permissions"]!["ask"]!.AsArray();
            JsonNode first = ask[0]!.DeepClone();
            ask.RemoveAt(0);
            ask.Add(first);
        });

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.True(exitCode == 0, output);
    }

    /// <summary>
    /// A sync rewrites only the process keys: the project's own allow entries and $comment stay, and a
    /// file that differed only in process keys comes back byte for byte as the kit's.
    /// </summary>
    [Fact]
    public void Sync_RestoresTheProcessKeys_AndKeepsTheProjectsOwn()
    {
        EditSettings(s =>
        {
            s["permissions"]!["ask"]!.AsArray().RemoveAt(0);
            s.Remove("attribution");
        });
        Assert.Equal(0, RunSync(_project).ExitCode);
        Assert.Equal(File.ReadAllBytes(Path.Combine(_kit, SettingsPath)), File.ReadAllBytes(Path.Combine(_project, SettingsPath)));

        EditSettings(s =>
        {
            s["$comment"] = "This project's own words.";
            s["permissions"]!["allow"]!.AsArray().Add("Bash(npm test*)");
            s["permissions"]!["deny"]!.AsArray().RemoveAt(0);
        });
        (int exitCode, string output) = RunSync(_project);
        Assert.True(exitCode == 0, output);
        Assert.Contains("updated  .claude/settings.json: permissions.deny", output);

        JsonObject settings = ReadSettings(_project);
        Assert.Equal("This project's own words.", (string?)settings["$comment"]);
        Assert.Contains("Bash(npm test*)", settings["permissions"]!["allow"]!.AsArray().Select(n => (string?)n));
        Assert.Equal(0, RunSync(_project, "-Check").ExitCode);
    }

    /// <summary>The keys that switch the hooks or the prompts off are the kit's too, and the kit leaves them unset.</summary>
    [Fact]
    public void Settings_KeysThatSwitchTheGateOff_AreNamedAndRemoved()
    {
        EditSettings(s =>
        {
            s["disableAllHooks"] = true;
            s["permissions"]!["defaultMode"] = "bypassPermissions";
        });

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("differs  .claude/settings.json: disableAllHooks", output);
        Assert.Contains("differs  .claude/settings.json: permissions.defaultMode", output);

        Assert.Equal(0, RunSync(_project).ExitCode);
        JsonObject settings = ReadSettings(_project);
        Assert.False(settings.ContainsKey("disableAllHooks"));
        Assert.False(settings["permissions"]!.AsObject().ContainsKey("defaultMode"));
    }

    /// <summary>Claude Code's colon form, "Bash(git:*)", is a git entry like "Bash(git status*)".</summary>
    [Theory]
    [InlineData("Bash(git:*)")]
    [InlineData("PowerShell(git:*)")]
    [InlineData("Bash(git switch:*)")]
    public void Settings_GitAllowEntryInAnyForm_IsTheKits(string entry)
    {
        EditSettings(s => s["permissions"]!["allow"]!.AsArray().Add(entry));

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("differs  .claude/settings.json: permissions.allow", output);
    }

    /// <summary>Objects compare by what they hold: a hook written with its properties in another order is the same hook.</summary>
    [Fact]
    public void Settings_PropertyOrderInsideTheHooks_DoesNotCount()
    {
        EditSettings(s =>
        {
            JsonObject hook = s["hooks"]!["Stop"]![0]!["hooks"]![0]!.AsObject();
            JsonNode command = hook["command"]!.DeepClone();
            hook.Remove("command");
            hook["command"] = command;
        });

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.True(exitCode == 0, output);
    }

    /// <summary>A settings file that is not JSON is named, never overwritten, and fails the sync too.</summary>
    [Fact]
    public void Settings_NotValidJson_IsNamedAndLeftAlone()
    {
        string path = Path.Combine(_project, SettingsPath);
        File.WriteAllText(path, "{ \"permissions\": { \"allow\": [ ] }, }\n");
        byte[] before = File.ReadAllBytes(path);

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("invalid  .claude/settings.json (project's copy is not valid JSON", output);

        (exitCode, output) = RunSync(_project);
        Assert.Equal(1, exitCode);
        Assert.Contains("fix it by hand", output);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    /// <summary>A rewrite keeps the values of the project's own keys: a fraction, a long number, a date-like string.</summary>
    [Fact]
    public void Sync_KeepsTheValuesOfTheProjectsOwnKeys()
    {
        EditSettings(s =>
        {
            s["env"] = new JsonObject { ["SINCE"] = "2026-10-01T06:05:07Z", ["DAY"] = "2026-10-01" };
            s["ratio"] = 1.25;
            s["big"] = 12345678901;
            s["permissions"]!["ask"]!.AsArray().RemoveAt(0);
        });

        (int exitCode, string output) = RunSync(_project);
        Assert.True(exitCode == 0, output);

        JsonObject settings = ReadSettings(_project);
        Assert.Equal("2026-10-01T06:05:07Z", (string?)settings["env"]!["SINCE"]);
        Assert.Equal("2026-10-01", (string?)settings["env"]!["DAY"]);
        Assert.Equal(1.25, (double)settings["ratio"]!);
        Assert.Equal(12345678901, (long)settings["big"]!);
        Assert.Equal(0, RunSync(_project, "-Check").ExitCode);
    }

    /// <summary>Only .claude/rules/kit/ is the kit's: a rules file of the project's own beside it is neither drift nor deleted.</summary>
    [Fact]
    public void OwnRulesFile_BesideTheKitsFolder_IsLeftAlone()
    {
        string own = Path.Combine(_project, ".claude", "rules", "own.md");
        File.WriteAllText(own, "# This project's own rules\n");

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.True(exitCode == 0, output);

        File.AppendAllText(Path.Combine(_project, ".claude", "rules", "kit", "process.md"), "An isolated edit.\n");
        Assert.Equal(0, RunSync(_project).ExitCode);
        Assert.True(File.Exists(own));
        Assert.Equal(0, RunSync(_project, "-Check").ExitCode);
    }

    [Fact]
    public void Settings_MissingInTheProject_IsNamedAndCopied()
    {
        File.Delete(Path.Combine(_project, SettingsPath));

        (int exitCode, string output) = RunSync(_project, "-Check");
        Assert.Equal(1, exitCode);
        Assert.Contains("missing  .claude/settings.json", output);

        Assert.Equal(0, RunSync(_project).ExitCode);
        Assert.Equal(File.ReadAllBytes(Path.Combine(_kit, SettingsPath)), File.ReadAllBytes(Path.Combine(_project, SettingsPath)));
    }

    public void Dispose()
    {
        ScriptRunner.DeleteTree(_repo);
    }

    private static (int ExitCode, string Output) RunSync(string projectDir, params string[] args)
    {
        return ScriptRunner.RunScript(projectDir, Path.Combine(projectDir, "scripts", "sync.ps1"), args);
    }

    private static JsonObject ReadSettings(string projectDir)
    {
        return JsonNode.Parse(File.ReadAllText(Path.Combine(projectDir, SettingsPath)))!.AsObject();
    }

    /// <summary>Changes the project's settings.json; the layout it is written in does not matter to sync.</summary>
    private void EditSettings(Action<JsonObject> change)
    {
        JsonObject settings = ReadSettings(_project);
        change(settings);
        File.WriteAllText(Path.Combine(_project, SettingsPath), settings.ToJsonString(Indented));
    }

    /// <summary>
    /// The list of shared folders is written once, in kit-config.ps1, and names the same folders as
    /// <see cref="Shared"/>; sync.ps1 and the Stop hook ask it rather than keep a list of their own.
    /// </summary>
    [Fact]
    public void TheSharedFolderList_IsDefinedOnceInKitConfig()
    {
        string scripts = Path.Combine(TestData.FindRepoRoot(), "scripts");
        string config = File.ReadAllText(Path.Combine(scripts, "kit-config.ps1"));
        int function = config.IndexOf("function Get-KitSharedFolders", StringComparison.Ordinal);
        Assert.True(function >= 0, "Get-KitSharedFolders not found in kit-config.ps1");
        string[] expected = Shared.Select(f => f.Replace('\\', '/')).ToArray();
        Assert.Equal(expected, QuotedFolders(config[function..], "@("));

        foreach (string script in new[] { "sync.ps1", Path.Combine("hooks", "claude-stop-gate.ps1") })
        {
            string text = File.ReadAllText(Path.Combine(scripts, script));
            Assert.Contains("Get-KitSharedFolders", text);
            Assert.DoesNotContain("'.claude/skills", text);
            Assert.DoesNotContain("'.claude\\skills", text);
        }
    }

    /// <summary>The quoted folders of the PowerShell array that starts at <paramref name="marker"/>, with forward slashes and no trailing one.</summary>
    private static string[] QuotedFolders(string script, string marker)
    {
        int start = script.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, marker + " not found");
        int end = script.IndexOf(')', start);
        return script[(start + marker.Length)..end]
            .Split(',')
            .Select(s => s.Trim().Trim('\'').Replace('\\', '/').TrimEnd('/'))
            .ToArray();
    }
}
