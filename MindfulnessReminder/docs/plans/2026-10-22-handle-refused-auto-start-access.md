# Handle refused auto-start access: undo a cut-short first run, read an unreadable value as missing

## Context

The code review of the documentation backfill (2026-10-21, recorded in
`docs/plans/2026-10-21-document-every-type-and-method.md`, "Code review") found two code gaps
around a registry that refuses access. The owner chose the two fixes I proposed, in chat on
2026-10-22: "poti sa te apuci de cele 2 fixuri care le-ai propus aici". In English: "you can
start on the 2 fixes you proposed here". The two fixes I had proposed:

1. **First run:** undo the Run value when a later write is refused, so that R-003 holds.
2. **Tray refresh:** make `GetStatus` in Core treat an entry it cannot read as not enabled, so
   that a refused read no longer crashes the app.

Today's behaviour, measured by reading the code on `main` (3326fe4):

- **Gap 1.** `StartupRegistrar.EnsureRegisteredOnFirstRun` calls `_autoStart.Enable()` and then
  writes the marker, under one catch that returns `false`. `RegistryAutoStartManager.Enable`
  writes the Run value with `SetString`, then calls `DeleteValue` on `StartupApproved\Run`.
  Suppose the Run key accepts the write, but the approval key or `HKCU\Software\MindfulnessReminder`
  refuses its own. Then the Run value stays, no marker is written, and every later start
  registers again. That undoes an untick and contradicts R-003 and ADR 0028 ("nothing is
  registered").
- **Gap 2.** `GetStatus` lets a refused read through. `TrayIconHost.RefreshAutoStartItem` calls
  it unguarded from the constructor, on every menu opening and after each click, so the app
  ends in its crash dialog. R-005 covers only a refused *change*.

## Facts checked (2026-10-22)

- **`main` is at 3326fe4,** and no worktree holds a branch for this task.
- **`FakeRegistryStore`** (tests) has `ReadRefusal` and `WriteRefusal`, and both apply to every
  key. Its `DeleteValue` ignores `WriteRefusal`, although the property's own summary says "every
  write throws it". As a result no Core test can reach gap 1.
- **What counts as a refusal.** R-051's settings reads treat `SecurityException`,
  `UnauthorizedAccessException` and `IOException` as one (`RegistrySettingsStore.Load`; the
  theory `R051_A_registry_that_refuses_to_be_read_gives_the_defaults`). R-003 and R-005 count
  `UnauthorizedAccessException` and `SecurityException` as a refused write.
- **Existing tests:** `R003_A_refused_registration_does_not_stop_the_app` refuses every write,
  and `R006_A_refused_repair_does_not_stop_the_app` refuses the repair. Both must stay green.

## Decisions (the kit's rule "Two readings": decided, stated)

1. **Gap 1, undo only what this run wrote.**
   - Before the first-run registration, read the status. If `Enable()` or the marker write is
     then refused, and there was no entry before (`Disabled`), call `Disable()`. A refusal of
     that `Disable()` is swallowed.
   - An entry that was already there (a copy registered before the marker existed, say) is left
     in place: this run did not write it.
   - In every case no marker is written, the method returns `false`, and the next start tries
     again, as R-003 says.
2. **Gap 2, a value the account may not read counts as missing.** `RegistryAutoStartManager`
   reads the Run value and the approval value through the same refusal set as R-051:
   - an unreadable Run value is `Disabled`;
   - an unreadable approval value counts as no veto, so a readable Run value is `Enabled`.

   This is the settings store's forgiving read applied to auto-start. It goes beyond my earlier
   wording ("an entry it cannot read counts as not enabled") in one case: when the Run value is
   readable, it treats an unreadable veto as no veto, rather than reporting `Disabled` for an
   entry it can see. That keeps a click useful. A ticked item can still be unticked, which
   deletes the Run value. **Say no at approval and an unreadable approval value reads as
   `Disabled` too.**
3. **Only `GetStatus` changes.** `RepairPathIfNeeded` keeps letting a refused read through.
   `StartupRegistrar` already catches it, and nothing else calls it.
4. **One project ADR, 0035,** records both choices, which were settled in chat. ADR 0028's
   Decision already promises "nothing is registered" and stays as it is. ADR 0035 adds a case
   that 0028 never covered, so it supersedes nothing.

## Setup

- From `E:\Photografie\Code\MindfulnessReminder`:
  `git worktree add .claude/worktrees/Handle-refused-auto-start-access -b claude/Handle-refused-auto-start-access main`
- Work in `<wt>` = `...\.claude\worktrees\Handle-refused-auto-start-access\MindfulnessReminder`.
  Commits are `git -C <path written out> commit`, and each must show the hook's `== OK`.
- This plan is saved as `<wt>/docs/plans/2026-10-22-handle-refused-auto-start-access.md`.

## Changes, in the kit's order: requirement, failing test, code

### Fix 1: the first run (commit 2)

- **`docs/requirements.md`, R-003.** Its refusal sentence gains: the refusal may come after the
  Run value was written; the run then deletes that value again, unless it was there before the
  first run.
- **`FakeRegistryStore`:**
  - a `RefusedKeyPath` property: when it is set, `ReadRefusal` and `WriteRefusal` apply to that
    key path only (case-insensitive); when it is null, they apply to every key, as today;
  - `DeleteValue` honours `WriteRefusal`, as its summary already says.
- **New tests in `StartupRegistrarTests`,** red first, with the failing output pasted:
  - `R003_A_refusal_after_the_run_value_is_written_leaves_nothing_registered`, a theory over
    the refused key (`StartupRegistrar.SettingsKeyPath`,
    `RegistryAutoStartManager.StartupApprovedKeyPath`): returns `false`, no Run value, no marker;
  - `R003_A_refused_first_run_leaves_an_entry_it_found_in_place`: a Run value present before
    the first run and the marker key refused; the Run value is still there. This test is green
    before the fix too; it guards decision 1.
- **`StartupRegistrar.EnsureRegisteredOnFirstRun`** gets the status read, the guarded undo and
  a private `TryDisable`. Its summary and catch comment go back to R-003's promise.

### Fix 2: the unreadable value (commit 3)

- **`docs/requirements.md`, R-004,** gains a sentence: a value the account may not read
  (`SecurityException`, `UnauthorizedAccessException`, `IOException`) counts as missing. An
  unreadable Run value is `Disabled`; an unreadable approval value is no veto.
- **New tests in `RegistryAutoStartManagerTests`,** red first:
  - `R004_GetStatus_reads_a_run_value_it_may_not_read_as_missing`, a theory over the three
    types: `Disabled`, no throw;
  - `R004_GetStatus_reads_an_approval_value_it_may_not_read_as_no_veto`, a theory over the
    three types: the Run value is set, the approval key is refused, and the result is `Enabled`.
- **`RegistryAutoStartManager`** gets two private readers, for the Run value and the approval
  value, each catching the three types and returning `null`. `GetStatus` and
  `IsApprovalRevoked` use them.
- **Comments:**
  - `IAutoStartManager`'s summary and `GetStatus`: it never throws for a refused read;
  - `TrayIconHost.OnAutoStartClick`: drop "a refused read in the refresh that follows is not
    caught".
- **`docs/decisions/0035-...md`,** with both decisions:
  - Context: the review, gaps 1 and 2.
  - Decision: decisions 1 and 2 above.
  - Alternatives rejected:
    - amending R-003 to accept the partial registration;
    - a catch in `TrayIconHost` (App code, untested by design, ADR 0008);
    - an unreadable veto as `BlockedByWindows` or `Disabled`;
    - an undo that also deletes an entry found in place.
  - Consequences: the new tests, the fake's per-key refusal, and the tray no longer crashing.

### Wrap-up (commit 4)

- **`CHANGELOG.md`:** two `Fixed` lines, citing R-003 and R-004.
- **`/wrap-up`:**
  - `check-design.ps1 -Since main`, reading each note it lists (code-map, settings-storage,
    tray-tooltip, dark-theme);
  - the README check, where step 9 may add one sentence to "Behaviour";
  - the plan's "What deviated".
- **No version bump.** I'll say that two fixes seem to warrant 1.4.1; that is the owner's call.

## Verification

- **Each new test fails before its fix.** I paste the `dotnet test --filter` output, then the
  same tests passing after the fix.
- **The gate is green at each commit,** including the existing R003, R005 and R006 tests.
- **Only Core and tests change in code.** `TrayIconHost.cs` changes in a comment only, so the
  App needs no `--preview` or `--detect` run. A full tray start with a refused key is listed for
  the owner's own copy: on a test account, deny Read on `StartupApproved\Run`, then open the
  menu. The item shows, and no crash dialog appears.
- **`/code-review` is suggested before the merge.**

## What deviated

- **Fix 2 went first.** The first run now reads the status before it registers, and until fix 2
  that read would itself throw a refused read. So fix 2 is ff4a33b and fix 1 is 29a50aa, and
  ADR 0035 went into fix 1's commit, not fix 2's.
- **The red runs, then the green ones:**
  - fix 2: the six cases of the two R004 theories failed, each on the refusal exception thrown
    out of `GetStatus`; they passed after the fix, 234 tests in the gate;
  - fix 1: `Failed: 2, Passed: 7, Total: 9` for `StartupRegistrarTests`, both cases of the
    theory on the `Assert.False` for the Run value, while the guard test passed before the fix,
    as planned; after it the 32 tests of the two classes passed, 237 in the gate.
- **ADR 0035 names another found entry.** The plan's example, "a copy registered before the
  marker existed", does not happen: the marker is as old as the base version (ADR 0028). The ADR
  names the case that does: while the marker's key refuses writes every start is a first run,
  and an entry the user ticked on in the tray must survive it.
- **`EnsureRegisteredOnFirstRun`'s summary says "read or write".** Its first catch also takes a
  refused read of the marker. The catch comment that said the tray "reports the same refusal"
  now names the keys a click touches, the Run key and the Task Manager key: never the marker's.
- **One case stays as it was.** While the marker's key refuses writes, every start is a first
  run, and `Enable` rewrites a found entry and lifts its Task Manager veto each time. It did so
  before this task too; changing what a first run does with a found entry is outside this plan.
