# 2026-09-30 Run siril-cli on a private copy of its settings

## Context

On 2026-09-26 the owner's Siril settings were reset to Siril's defaults. The file is
`%LOCALAPPDATA%\siril\config.1.4.ini`, and the reset lost the selected scripts, the StarNet and
GraXpert paths, the focal length and pixel size, and the catalogues.
- Every leftover `config.1.4.ini.XXXXXX` carries `wd=...\Temp\flatcheck-tests\<guid>`, so
  FlatCheck's `SirilCliIntegrationTests` started those runs.
- The window starts siril-cli the same way, without `-i`: `SirilScriptRun.For` is called from
  `MainWindow.xaml.cs:110` and `SplitTabViewModel.cs:159`. Every window run therefore reads and
  rewrites the owner's file too.
- A draft, uncommitted in the shared checkout on `main`, added an optional `-i` for the tests only.

Measured on 2026-09-30 with siril-cli 1.4.4, always with `-i` on copies in a scratch folder. After
every batch:
- the owner's file had the same SHA-256 (`0AA504B7…F327`);
- `%LOCALAPPDATA%\siril-scripts` and the listing of `%LOCALAPPDATA%\siril` were unchanged.

| Measurement | Result |
| --- | --- |
| `-i` on a missing file | Exit 1, "Could not load or create settings file, exiting." on stderr; the file is not created. |
| `-i` on an empty file | Exit 0, "Warning: nothing could be read from the settings file"; Siril writes 4260 B into it. |
| `-i` on a copy of the owner's file, probe script | Exit 0; the only key Siril changes is `core/wd`, which it sets to the `-d` folder. |
| 200 sequential runs, one reused copy | 9 saves failed (`log: Could not save the settings … g_rename() failed: Permission denied`). After each of the 9 the file was **gone**, with a `.XXXXXX` leftover beside it holding the full settings. |
| 100 runs, a fresh private folder each, deleted right after the exit | 10 saves failed, exit still 0. Every run left a `siril.ini.XXXXXX`. The folder was deleted on the first try in 100 of 100 runs, in about 1 ms. |
| 50 rounds of 3 concurrent runs on one copy | 4 rounds ended with the file gone; 6 runs could not load it (Permission denied or No such file, exit 1). |

**The reset needs no concurrency.**
- A siril-cli save fails on this machine in roughly 5-10 % of runs.
- A failed save deletes the file it was saving: GLib's Windows fallback deletes the target before
  the second rename.
- The next run without `-i` finds no file, loads the defaults and saves them.
- The gate runs the two integration tests back to back, and the commit hooks run the gate twice
  per commit.
- What holds the file is not identified. Windows lists three third-party antivirus products as
  active.

**Siril's source agrees.**
- `initfile.c:205-207`: with `-i`, reads and writes go only to that file, with no migration.
- `command_line_processor.c:404`: a write after every script.
- `main-cli.c:190-201`: the `-d` folder is persisted as `wd`.
- GLib `gfileutils.c:1421-1456`: the Windows fallback that deletes the target.
- siril-cli does no update check and no scripts or SPCC repository sync (GUI only), so `-i` loses
  nothing there.

## Decisions taken by the owner (2026-09-30)

Approved with this plan and recorded in ADR 0055.
1. **Production too.** Every window run gets a private copy with `-i`: the problem card and each
   Split part.
2. **Where the copy comes from.** The copy's source is `SirilInstall.FindConfig()`, the file the
   script list is already read from. When there is none, the copy is an empty file, which means
   Siril's defaults; Siril would use them itself. There is no warning. A version-matched resolver
   is left out: on every realistic layout it picks the same file.
3. **Nothing flows back.** The only key a probe run changes is `wd`. Two visible effects:
   - Siril's GUI no longer opens in the last folder FlatCheck ran on.
   - In 5-10 % of runs the log shows "Could not save the settings in …\siril.ini". It concerns the
     private copy and is harmless; the README says so.
4. **A source that exists but cannot be read stops the run** before siril-cli starts, and the
   message names the file. There is no retry and no silent fallback to defaults. A Split then
   keeps its temp folder, as it does for any failed start.
5. **Structural protection:**
   - `SirilScriptRun` has no path without `-i`, and creates and deletes the private folder itself.
   - A `RepoInvariantTests` scan allows a process start only in `SirilScriptRun.cs` and in the
     About window's link.
   - The red-first test goes through the production entry point, with a fake `siril-cli.cmd`.

## Steps

1. Create the worktree. The draft is not carried over; it is rewritten here. Until the isolation is
   in, only the filtered R094 tests run in the worktree.
2. Save this plan and add R-094 (`draft`).
3. Write the red test `R094_EveryRunGetsAPrivateCopyOfSirilsSettings_PassedWithI_AndGoneAfterTheRun`
   in a new `SirilScriptRunTests` class, with a fake `siril-cli.cmd` called through the production
   `For(cli, folder, script)`. Run it only with the R094 filter.
4. Change the code:
   - `SirilScriptRun`:
     - `For(cli, folder, script)` resolves `FindConfig()` inside the delegate and uses
       `%TEMP%\FlatCheck` as the private root.
     - `For(cli, folder, script, seedConfig, privateRoot)` is for tests.
     - `ExecuteAsync` becomes private. It reads the seed first (`File.ReadAllBytes`, never with
       delete sharing), then creates `siril-<guid>\siril.ini`, then runs `try { start with -i; wait }
       finally { delete the folder }`.
   - Add `SessionMessages.SirilSettingsUnreadable`.
5. Add the remaining tests:
   - R094: cancel, an empty or explicit seed, and a locked seed.
   - `SirilCliIntegrationTests` switch to `FindCli()` and get a private empty file in the workspace.
     They assert the log line naming the private file, that no folder is left, and that the
     owner's file does not carry the fixture folder as `wd`.
   - A process-start scan in `RepoInvariantTests`.
6. Follow the safety protocol before any run that can start the real siril-cli: Siril closed, a
   copy of `config.1.4.ini`, and its SHA-256 compared afterwards.
7. Update the docs:
   - R-094 `implemented`;
   - ADR 0055, plus a dated amend line in ADR 0035;
   - `two-applications.md`, `code-map.md`, `design/README.md`;
   - `README.md` (Romanian);
   - CHANGELOG `### Fixed`;
   - the memory note `siril-install-layout`.
8. `/wrap-up`.

## Branch

Worktree `FlatCheck/.claude/worktrees/Run-siril-cli-on-a-private-copy-of-its-settings`, branch
`claude/Run-siril-cli-on-a-private-copy-of-its-settings`, from `main` (ADR 0050).

## Verification

- The R094 test is red before the code and green after, with the filtered output pasted.
- Run the whole suite (`dotnet test FlatCheck.sln -c Release --nologo --filter "Category!=Validation"`),
  then `scripts/verify.ps1`, with the counts reported and the config hash compared around each.
- The integration tests show that they really ran siril-cli: their log line names the private
  file.
- `/validate` is not needed, because no formula moved.

## Results, 2026-09-30

- **The red.** `R094_EveryRunGetsAPrivateCopyOfSirilsSettings_PassedWithI_AndGoneAfterTheRun` on
  the unfixed `SirilScriptRun`:
  `Assert.Equal() Failure: Strings differ … Expected: "-i" Actual: ""` (Failed 1, Passed 0). The
  fake `.cmd` ran, and its fifth argument was empty.
- **After the code.** The four R094 tests and the scan pass. The two integration tests take
  844 ms and 782 ms, so they really ran siril-cli, and they assert its
  "Reading configuration file <workspace>\siril-…" line.
- **The whole suite and the gate.** `verify.ps1`: build 0 warnings and 0 errors;
  `FlatCheck.Tests` 947 passed, 0 failed, 0 skipped; `FlatCheck.Wpf.Tests` 331 passed, 0 failed,
  0 skipped; `== OK in 132s`. The config's SHA-256 was the same before and after every run
  (`0AA504B7…F327`), and the listing of `%LOCALAPPDATA%\siril` was unchanged.
- **Mutation, by hand, on the copy test.**
  - A copy-back into the source in the `finally` was killed: the source gained `wd=…`.
  - A non-recursive delete was killed: the folder stayed, because of the `.XXXXXX` leftover.
  - Both mutants were reverted.

## What deviated, and why (2026-09-30, at wrap-up)

- **The adversarial review (three lenses and a verifier) confirmed nine findings**, all fixed
  before the commit:
  - The CHANGELOG edit had replaced the `### Removed` heading.
  - The CHANGELOG said the integration tests get a byte copy in `%TEMP%\FlatCheck`; they get an
    empty copy in their workspace.
  - The process-start scan missed a typed variable built with a target-typed `new()`. A third
    alternative was added, and the comment and ADR claim only the three forms.
  - The fake CLI never rewrote its `-i` file, so "nothing flows back" was untested. The copy test
    now uses a fake that appends `wd=` and leaves a `.XXXXXX`.
  - ADR 0055 said which overload the tests use, and said "six stock scripts", both wrongly.
  - ADR 0055 said the missing-config case always gives defaults; it does not when an older
    `config*.ini` is left.
  - ADR 0055 cited R-092 for keeping the Split temp folder on a failed start; R-092 does not say
    so.
  - `two-applications.md` repeated ADR 0055 (ADR 0051), and its Covers line lacked 0055.
- **R-092 still names only the non-zero exit and the missing master** as reasons to keep the Split
  temp folder. A failed start keeps it too, in the code, and is not in a requirement. That is left
  for a later task, not widened here.

## For the owner, outside this branch

- The draft's two modified files in the shared checkout must be discarded before the merge.
- The three detached worktrees (`dreamy-hermann-dd71bc`, `salut-worktree-5c4f5e`,
  `wizardly-fermat-1c8ce0`) hold the old tests. Update or remove them once the fix is on `main`.
- Siril's own GUI saves through the same code. The newest `config.1.4.ini.XXXXXX` holds the last
  good settings if the GUI ever loses the file.
