# 0055. Every siril-cli run gets a private copy of Siril's settings, passed with -i, and the user's file is only read

Date: 2026-09-30
Status: accepted 2026-09-30
Supersedes: 0035 in part (the siril-cli command line)

## Context

On 2026-09-26 the owner's `%LOCALAPPDATA%\siril\config.1.4.ini` was reset to Siril's defaults.
The reset lost the selected scripts, the StarNet and GraXpert paths, `focal_length` 19.24 and
`pixel_size` 3.91, and the catalogue choices. With `script_path` and `selected_scripts` empty,
FlatCheck's script list silently fell back to every `.ssf` in the install's `scripts/` and in
`%LOCALAPPDATA%\siril-scripts` (`SirilInstall.Locate`), no longer the owner's selection.

Five interrupted saves were left beside the file, all with `wd=...\Temp\flatcheck-tests\<guid>`:

| Leftover | Size | Time | Content |
| --- | --- | --- | --- |
| `.MRBDW3` | 5175 B | 09-26 17:46 | the owner's settings |
| `.S7YBW3` | 4208 B | 09-26 18:11 | defaults |
| `.BMJEW3` | 4208 B | 09-27 17:47 | defaults |
| `.DQQJW3` | 4208 B | 09-27 20:12 | defaults |
| `.LROEW3` | 4208 B | 09-27 21:06 | defaults |

The runs that wrote them were `SirilCliIntegrationTests`. The window launched siril-cli the same
way, `siril-cli -d <folder> -s <script>` (ADR 0035), from the problem card and from each Split
part.

Siril 1.4.4's source:
- siril-cli rewrites the whole settings file at the end of every script, whether it succeeded or
  failed (`command_line_processor.c:404`), and persists the `-d` folder as `wd`
  (`main-cli.c:190-201`).
- With `-i <file>`, reading and writing go only to that file, with no migration
  (`initfile.c:205-207`).
- Without any file, siril-cli loads the defaults and creates `config.1.4.ini` at the first save
  (`initfile.c:216-222`).
- The save is GLib's `g_file_set_contents`: a `<file>.XXXXXX` and a rename. When that rename
  fails, the Windows fallback deletes the target and retries (`gfileutils.c:1421-1456`).
- siril-cli does no update check and no scripts or SPCC repository sync; those are GUI only
  (`main.c:422-451`, `callbacks.c:1698,1748`).

Measured on 2026-09-30 (siril-cli 1.4.4, the owner's machine), always with `-i` on copies in a
scratch folder. After every batch, the owner's file had the same SHA-256 and
`%LOCALAPPDATA%\siril-scripts` was untouched.
- **Missing file:** exit 1, "Could not load or create settings file, exiting." on stderr, and the
  file is not created.
- **Empty file:** exit 0, a `log:` warning "nothing could be read from the settings file", and
  Siril writes 4260 B of defaults into it.
- **Copy of the owner's file, probe script:** the only key that changes is `core/wd`.
- **200 sequential runs on one reused copy:** 9 saves failed with `log: Could not save the
  settings … g_rename() failed: Permission denied`. After each of those 9 the file was **gone**,
  and a `.XXXXXX` beside it held the full settings. The exit code stayed 0.
- **100 runs, each on a fresh private folder:** 10 saves failed. Every run left a
  `siril.ini.XXXXXX`, and the folder was deleted on the first try right after the exit in 100 of
  100 runs (about 1 ms).
- **50 rounds of 3 concurrent runs on one copy:** 4 rounds ended with the file gone, and 6 runs
  could not load it (exit 1).

So the reset needs no concurrency:
1. A save fails in roughly 5-10 % of runs on this machine and deletes the file it was saving.
2. The next run without `-i` finds no file and saves the defaults.
3. The gate runs the two integration tests back to back, and the commit hooks run the gate twice
   per commit.

What makes the rename fail is not identified; Windows lists three third-party antivirus products
as active.

## Decision

Every siril-cli start goes through `SirilScriptRun`, and each run gets its own folder:
`%TEMP%\FlatCheck\siril-<32 hex>\siril.ini`.
- **What goes into the folder.** A byte copy of the file the script list is read from
  (`SirilInstall.FindConfig()`), or an empty file when there is none. There is no warning: the
  empty file means Siril's defaults, the same ones Siril would use with no config.
- **How it is passed.** Always with `-i`. The config is looked up inside the runner, at run time,
  so any error lands in that run's verdict.
- **How the owner's file is read.** With `File.ReadAllBytes`, which allows other readers but
  never delete sharing. Per GLib's source, a Siril save that lands in that moment then fails with
  the file intact. With delete sharing, Siril could delete the file and the retried rename could
  fail, leaving no file.
- **An unreadable file.** When the owner's file exists but cannot be read, siril-cli is not
  started, and the message (`SessionMessages.SirilSettingsUnreadable`) names the file. There is no
  retry and no fallback to defaults.
- **Cleanup.** The folder, with Siril's `.XXXXXX` leftovers, is deleted in a `finally` after the
  process has exited: after exit 0, after a non-zero exit, and after `Opreste`, which kills the
  process tree.
- **Nothing flows back into the owner's file.** Not the `wd`, not a script's `set*`. FlatCheck
  never writes `%LOCALAPPDATA%\siril`.
- **A second overload for the tests,** `For(cli, folder, script, settings, privateRoot)`, takes
  the file to copy and the folder of the copy from outside. The Siril integration tests use it
  with no file (an empty copy) in their own workspace.

## Alternatives rejected

- **`-i` in the tests only** (the first draft). Every window run would keep a 5-10 % chance of
  deleting the owner's settings, and would keep writing its `wd`.
- **An empty file for the window runs too.** It would lose the owner's extension, bit depth,
  compression, memory and debayer settings as soon as they differ from the defaults. The Split
  compares its parts with a whole master built with the owner's settings.
- **One shared copy for every run, or for a whole Split.** A failed save deletes it (9 of 200).
  A Split's part 2 would start from part 1's end state.
- **Backing up and restoring the owner's file around each run.** FlatCheck would become a second
  writer of that file, would race the Siril GUI, and would skip the restore on a crash or kill.
- **No `-i`, with runs serialized and never killed during a save.** The failures were measured on
  sequential runs. The save is the last step before the exit and cannot be observed from outside,
  so the only way to be sure of not killing during a save is never to kill, and `Opreste` is
  R-092.
- **A settings file matched to the siril-cli version** (`config.<major>.<minor>.ini`, then Siril's
  fallbacks). It picks the same file as `FindConfig` whenever the current version's file exists,
  and it would split the script list's source from the copy's. The two differ only when that
  file is missing and an older version's is left (below).
- **Pointing `XDG_CONFIG_HOME` at a temp folder.** GLib honours it, but it would also move the
  Python venv, the download cache and the StarNet folder.
- **An optional `configPath` parameter.** A call that forgets it would still compile.

## Consequences

- ADR 0035 is amended: the command line is now `siril-cli -d <session> -s <script> -i <private copy>`.
- **What the owner sees:**
  - Siril's GUI no longer opens in the last folder FlatCheck ran on. For the Split that folder was
    a temp folder already deleted.
  - In 5-10 % of runs the log shows "Could not save the settings in …\siril.ini". It concerns the
    private copy and changes nothing; the README says so.
- **The Split and a failed start.** When the settings cannot be read, a Split keeps its temp
  folder and shows its path. The code does this for any part whose run does not succeed
  (`SplitTabViewModel`); R-092 names only the non-zero exit and the missing master.
- **What stays outside FlatCheck's reach:**
  - A missing `config.1.4.ini`, for example after a failed Siril GUI save, runs with no warning.
    When no other `config*.ini` is left it runs on defaults, just as Siril itself would. When an
    older version's file is left (Siril keeps it after a migration), `FindConfig` takes the
    newest one, and siril-cli reads that copy without migrating it. The newest
    `config.1.4.ini.XXXXXX` holds the last good settings.
  - The Siril GUI saves through the same GLib code and is exposed to the same failure.
  - `-i` does not isolate the Python venv check siril-cli runs at every start
    (`%LOCALAPPDATA%\siril\venv`).
  - A `siril-*` folder of a few KB stays in `%TEMP%\FlatCheck` if FlatCheck exits in the middle of
    a run or the delete fails.
- **Guards:**
  - The R094 tests run with a fake `siril-cli.cmd`.
    - The first test goes through the production `For(cli, folder, script)`. It reads the owner's
      config, copies it through `%TEMP%\FlatCheck`, and checks that `-i` names that copy with the
      config's bytes and that the folder is gone afterwards. It was red on the unfixed code.
    - The others use the second overload with a workspace file or none. They cover the empty
      copy, a fake that rewrites its copy and leaves a `.XXXXXX` like Siril (nothing flows back,
      the leftover goes with the folder), a cancel mid-run, and an unreadable file that stops the
      run.
    - Two mutants were killed on 2026-09-30: a copy-back into the source, and a non-recursive
      delete.
  - `SirilCliIntegrationTests` run the real siril-cli, when installed, on an empty private copy.
    They assert Siril's "Reading configuration file" line with the private path, that no folder
    is left, and that the owner's file does not carry the fixture folder as `wd`.
  - `RepoInvariantTests.OnlySirilScriptRunAndTheAboutLink_StartAProcess` fails on a new launch
    site written in any of the three forms its pattern knows: constructing either type by name,
    a typed variable with a target-typed construction, or the static `Start`. It is a text scan.
- **Worktrees and branches made before this change** still carry the old integration tests, and a
  gate there starts siril-cli without `-i`.
- Source note: docs/plans/2026-09-30-run-siril-cli-on-a-private-copy-of-its-settings.md.
