# Add a small decision log

## Context

The 2026-10-26 crash was diagnosed entirely from outside the application (Event Viewer, a WER
mini dump, a reproduction). Since ADR 0036 a crash leaves its text and stack in the Application
log, but what the application decides without failing (a reminder skipped or held back and why,
a lock or unlock, settings that fell back to defaults, an auto-start repair, the runtime a copy
started on, whether the restart registration succeeded) leaves no trace. The owner accepted
R-110 to R-114 (a capped text log, one line per decision) and ADR 0037 (Core decides and
formats, App appends; always on; no tray item) on 2026-10-26.

The failing tests exist and are parked in the scratchpad, `...\scratchpad\parked-tests-log\`:
`DecisionLogTests.cs`, `FakeDecisionLog.cs` (new) and `ReminderCoordinatorTests.cs` (the
committed file plus a `_log` field, a tenth constructor argument and 13 `R112_` tests). Branch
`claude/Add-a-small-decision-log`, worktree `.claude/worktrees/Add-a-small-decision-log`.

## Steps

1. **Restore the tests**: move the two new files back into `tests/MindfulnessReminder.Tests/`
   and copy the parked `ReminderCoordinatorTests.cs` over the committed one. Build fails on
   `IDecisionLog`.

2. **Core seam: `src/MindfulnessReminder.Core/IDecisionLog.cs`** (new). `void Write(string text)`,
   one text per event, no timestamp (the adapter stamps it). Plus `NullDecisionLog` (internal
   sealed, `Instance`, `Write` does nothing), the default like `IdleActivityMonitor.Instance`
   and `SystemClock.Instance` in `ReminderCoordinator`.

3. **Core: `src/MindfulnessReminder.Core/DecisionLog.cs`** (new, static). What the tests call:
   - `MaxBytes = 262_144`; `ShouldRotate(long length) => length > MaxBytes`.
   - `FormatLine(DateTimeOffset localTime, string text)`: `localTime.ToString("yyyy-MM-dd HH:mm:ss",
     InvariantCulture)` + two spaces + the text with `\r\n` then `\n` replaced by ` | `
     (so `"a\n\nb"` gives `a |  | b`, as the tests and the R-114 example say).
   - `Started(banner, runtime, args, hresult)`: `Started <banner> on <runtime>, arguments:
     <string.Join(' ', args) or "(none)">; restart registration: ok` or `refused (0x<hresult:X8>)`.
   - `Summary(ReminderSettings)`: `periodic <on|off> every <N> min, unlock <on|off>, pause for
     mic <on|off>, pause for full screen <on|off>, retry <N> s, messages "<unlock>" / "<periodic>"`.
   - `Reason(ReminderSuppression)`: `microphone or camera in use` / `full screen`.
   - `Crashed(string text) => "Crashed: " + text`.
   - The coordinator's texts as small functions: `ReminderShown(kind, message)`,
     `BroughtToFront(kind)`, `UnlockSkippedOff()`, `UnlockSkippedRecent(TimeSpan since)` (whole
     seconds, truncated), `UnlockSkipped(reason)`, `HeldBack(reason, TimeSpan retry)`,
     `ShownAfterHold(TimeSpan held, message)` (minutes rounded up, at least 1),
     `SettingsApplied(settings)`, `SettingsLoaded(settings, fallbacks, clamps, refused)`,
     `SettingsSaved(settings)`, `AutoStartRegistered(command)`, `AutoStartRepaired(command)`,
     `AutoStartRefused(Exception)`; constants `Locked`, `Unlocked`. A `ReminderKind` enum
     (`Unlock`, `Periodic`) for the two words. All null-checked, all documented.

4. **Core: `ReminderCoordinator.cs`.** Tenth optional parameter `IDecisionLog? log = null`,
   stored as `_log`. Lines, each at the decision: `HandleUnlock` writes `Unlocked` first, then
   one of: off / brought to front / `UnlockSkippedRecent(now - last)` / `UnlockSkipped(reason)`
   / `ReminderShown(Unlock, message)`. `HandleLock` writes `Locked`. `HandleTick` on a due
   reminder: when postponed, `HeldBack(reason, retry)` only when `_heldSince` is null (first
   postponement) or the reason differs from `_heldReason`; set `_heldSince ??= now`,
   `_heldReason = reason`. When shown or brought to front: `BroughtToFront(Periodic)` or, with
   `_heldSince` set, `ShownAfterHold(now - heldSince, message)`, else `ReminderShown(Periodic,
   message)`; clear `_heldSince`/`_heldReason`. A lock, an unlock and `ApplySettings` clear the
   two hold fields too (the countdown restarts; R-035 clears `Suppression` there already).
   `ApplySettings` writes `SettingsApplied`. Nothing in `RefreshSuppression` or `Start`.

5. **Core: `RegistrySettingsStore.cs`.** Second optional parameter `IDecisionLog? log = null`.
   `Read()` collects the names of values that fell back (a `List<string>` filled by `ReadFlag`,
   `ReadText`, `ReadNumber` when they return the fallback because the stored string was null or
   unparseable; the eight names in reading order) and the clamps (compare each parsed number
   with the normalized one: `IntervalMinutes`, `RetrySeconds`). `Load` writes
   `SettingsLoaded(settings, fallbacks, clamps, refused: false)` on success and
   `SettingsLoaded(Default, [], [], refused: true)` in the catch. `Save` writes `SettingsSaved`
   after the writes. Note a quirk the tests pin: a missing text value counts as a fallback
   (`ReadText` returns the fallback for null), a present empty one does not (it is normalized
   to the default by `Normalize`, which R-051 already states).

6. **Core: `IAutoStartManager.cs`** gains `string Command { get; }` (documented; the class has
   it). `StartupRegistrar.cs`: third optional parameter `IDecisionLog? log = null`;
   `AutoStartRepaired(_autoStart.Command)` when `RepairPathIfNeeded()` returns true,
   `AutoStartRegistered(_autoStart.Command)` after a successful `Enable`, `AutoStartRefused(ex)`
   in the catch (after the undo). The test fake `UnreadableEntryAutoStart` in
   `StartupRegistrarTests.cs` gets a `Command` property (additive).
   Verified by: `dotnet test` green except `RepoInvariantTests` (statuses flip in step 9).

7. **App: `src/MindfulnessReminder.App/FileDecisionLog.cs`** (new, `IDecisionLog`). Folder
   `Path.Combine(Environment.GetFolderPath(LocalApplicationData), "MindfulnessReminder")`, files
   `log.txt` / `log.1.txt` (names as constants). `Write(text)`: inside one `try/catch (Exception)`
   that swallows (comment: a log must never take the tray down): create the folder, if
   `DecisionLog.ShouldRotate(new FileInfo(path).Length)` then `File.Move(path, path1,
   overwrite: true)`, then `File.AppendAllText(path, DecisionLog.FormatLine(DateTimeOffset.Now,
   text) + Environment.NewLine)`. A `Path` property for the README's sentence is not needed; the
   README states the location.
   `RestartRegistration.Register()` returns `int` (the HRESULT); its comment changes.

8. **App: `App.xaml.cs`.** One `FileDecisionLog _log` created in `OnStartup` after the mutex
   (preview and detect return before it: R-110). Pass it to `new RegistrySettingsStore(registry,
   _log)`, `new ReminderCoordinator(..., log: _log)`, `new StartupRegistrar(registry, autoStart,
   _log)`. The registration becomes `var hresult = ApplicationRestart.ShouldRegister(e.Args) ?
   RestartRegistration.Register() : 0;` and the start line follows at once:
   `_log.Write(DecisionLog.Started(VersionInfo.Banner, RuntimeInformation.FrameworkDescription,
   e.Args, hresult))` (R-111: after the registration). `ReportCrash`: after `text` is built and
   the coordinator disposed, `_log?.Write(DecisionLog.Crashed(text))` before the dialog (the
   adapter swallows its own failures; R-114).

9. **Record.** `docs/requirements.md`: R-110 to R-114 `implemented`; R-100's "changes nothing"
   clause gains "but the start line (R-111)". `docs/design/code-map.md`: lines for
   `IDecisionLog.cs`, `DecisionLog.cs`, `FileDecisionLog.cs`, and `App.xaml.cs` mentions the log.
   `scripts/check-design.ps1 -Since main` will list `periodic-reminder.md`, `pausing.md`,
   `two-reminders.md` (the coordinator), `settings-storage.md` (the store): each gets one
   sentence saying the decision is also written to the decision log, where the note describes
   that decision. `CHANGELOG.md`: Added. `README.md`: a Behaviour bullet and a short section
   "The decision log" (where the file is, what a line looks like, the cap, that it never fails
   the app), the "Design decisions" row. Save this plan as
   `docs/plans/2026-10-26-add-a-small-decision-log.md`.

10. **Gate and wrap-up.** `dotnet build --no-incremental -warnaserror`, `scripts/verify.ps1`
    green, `/wrap-up`: commit on the task branch, propose the merge.

## Verification

- `scripts/verify.ps1` green: 277 + 31 new tests, format, warnings as errors, the design index,
  `RepoInvariantTests` with the statuses flipped.
- `--preview` from the worktree's Release build still opens the window and writes nothing to
  `%LOCALAPPDATA%\MindfulnessReminder\log.txt` (R-110): check the file is absent or unchanged.
- Not verifiable here (a tray start of a worktree build repairs the Run key, R-006): the file
  itself, its first line and the rotation. Listed for the owner after the merge; the rotation can
  be provoked by copying any 300 KB text file over `log.txt` before a start.

## Deviations (2026-10-26, at wrap-up)

None of substance. `_log` in `App.xaml.cs` is a nullable field (`FileDecisionLog?`), null in
`--preview` and `--detect`, which is why `ReportCrash` writes through `_log?`. The `--preview`
smoke check from the worktree's Release build opened the window, wrote no `log.txt` (the folder
`%LOCALAPPDATA%\MindfulnessReminder` does not exist on this machine) and left the Run entry as it
was. The tray-side behaviour, the file, its first line and the rotation, was not exercised here,
as planned.

## Code review (2026-10-26)

A subagent reviewed 3472ecb read-only against the rules, the rows and ADR 0037 (308 tests
green). Thirteen findings. Applied in the follow-up commit:

1. A clock moved back during a hold restarted the countdown inside `IntervalSchedule` without
   the coordinator noticing, so the next "shown after N min held back" line carried the old
   hold. `IntervalSchedule.JumpRestartedCountdown` now reports it and the coordinator clears
   the hold; a test pins it.
2. The rotation and the append shared one `try`, so a `log.1.txt` held open would have
   silenced the log until the lock cleared. The rotation has its own `try`; the line is
   appended regardless and the file grows past the cap until the next write can rotate.
3. A hold that ended on an open window lost its total: a new line, `Periodic reminder: window
   already open, brought to front after N min held back`; R-112 also says that a lock, an
   unlock, a save or a clock moved back ends a hold without a line of its own.
4. Tests for the hold reset on an unlock and across a lock (two tests).
5. R-114's example had one space between the two separators of the blank line; it is two.
6. An empty `%LOCALAPPDATA%` would have put the log under the working directory; the writer
   now writes nowhere in that case.
8. "nothing changed" on a refusal could be false when a repair had succeeded first: the line
   says "no registration made" (row, code, test).
9. R-113 names the order of the fallback names (the reading order).
10. ADR 0037 and the README now say the log also holds the command line and the crash text.
11. The README example's timestamps sit on the 20-second retry grid.

Left to the owner (see the wrap-up report): 7, moving `FileDecisionLog` to Core with the
folder injected so the rotation and the swallow rules get tests (a disagreement with ADR
0037's placement); 12, a save writes `Settings saved` and `Settings applied` back to back; 13,
a message normalized on reading leaves no trace in the `Settings loaded` line.

## Writer moved to Core (2026-10-26)

On the owner's word, review finding 7 was taken: `FileDecisionLog` moved from App to Core with
the folder injected (`FileDecisionLog(string folder)`, empty meaning nowhere), App names the
folder in `App.LogFolder()`, and `FileDecisionLogTests` (eight `R110_` tests on a temp folder)
pin the stamped line, the UTF-8 file without a byte order mark, the rotation strictly past the
cap, the replaced `log.1.txt`, a rotation that fails with the line still written, a folder that
cannot be created, an empty folder, and the null checks. ADR 0037's Decision, Alternatives and
Consequences were amended before the merge (the ADR was not yet on `main`). Findings 12 and 13
stay as they were.
