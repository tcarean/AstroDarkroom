# 0017. Full screen is SHQueryUserNotificationState, mapped in Core; QUNS_NOT_PRESENT, quiet time and an unknown state hold nothing back

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Pausing for what the user is doing", the
paragraph "Full screen is `SHQueryUserNotificationState`" and the rule "`QUNS_NOT_PRESENT` must
not suppress"), `README.md` ("How it detects them", the paragraphs on full screen and on
`QUNS_NOT_PRESENT`; the "Design decisions" row "Full screen") and the comments of
`ForegroundState.cs`, `SuppressionPolicy.Occupies` and `WindowsActivityMonitor.ShellState`; the
decision itself dates from 89de6c8 (pausing for a call or full screen, 1.2.0, 2026-09-08). The
second switch of 1.2.0, "Pause during full screen and presentations", on by default, is for "a
game, a film, a shared screen, or Windows presentation mode" (README). Windows asks itself the
same question before it shows a notification: `SHQueryUserNotificationState` answers with one
of the seven QUERY_USER_NOTIFICATION_STATE values of ShellAPI.h. One of them,
`QUNS_NOT_PRESENT`, "is what Windows reports behind a lock screen and can still be the answer
for a moment after an unlock -- long enough to swallow the unlock reminder" (`CLAUDE.md`),
"which is the oldest thing this app does" (README).

## Decision

- The reading is one P/Invoke, `SHQueryUserNotificationState` in `shell32.dll`, made by
  `WindowsActivityMonitor`, which passes the raw integer on. A failed call (a non-zero HRESULT,
  a missing DLL or entry point) gives 0, which is not a documented state.
- The table is in Core, `UserNotificationState.FromShellState`, over the integer, with the
  header's values as constants: `AcceptsNotifications` (5) is `Normal`; `Busy` (2),
  `RunningDirect3DFullScreen` (3) and `RunningWindowsStoreApp` (`QUNS_APP`, 7) are
  `FullScreen`; `PresentationMode` (4) is `Presenting`; `QuietTime` (6) is `QuietHours`;
  `NotPresent` (1) is `UserAway`; anything else, 0 included, is `Unknown`. `QUNS_BUSY` also
  covers presentation settings and cannot be told apart from them; both suppress, so it is named
  after "the commoner cause".
- With `PauseWhileFullScreen` on, `SuppressionPolicy` holds a reminder back for `FullScreen`
  and `Presenting` only. `UserAway` must not suppress; `QuietHours` (Focus Assist) is mapped and
  unused; `Unknown` holds nothing back, so neither a state a future Windows adds nor a shell that
  will not answer can switch the reminder off.
- If the reading ever proves lossy on borderless-fullscreen windows, the fallback is
  `GetForegroundWindow` plus `GetWindowRect` against the monitor bounds, and `--detect` is how
  to find out (ADR 0020).

## Alternatives rejected

- **The table in the adapter, beside the P/Invoke.** It would be "a thing to be checked by hand
  on a real machine"; in Core "the mapping IS the rule", the table is a unit test, and the
  adapter is "one call with nothing left to get wrong" (the `UserNotificationState` summary).
- **`QUNS_NOT_PRESENT` holding a reminder back.** It could swallow the unlock reminder in the
  moment after an unlock, and it buys nothing: "the schedule is suspended across a lock anyway"
  (`CLAUDE.md`; ADR 0014).
- **Quiet time or an unknown state holding a reminder back.** Both are left out because
  "neither was asked for, and a state Windows adds in a future build must not be able to switch
  the reminder off by accident" (the `Occupies` comment).
- **The foreground window's rectangle against the monitor bounds** (`GetForegroundWindow` plus
  `GetWindowRect`) as the first reading. It is kept as the fallback; the shell's answer is "One
  P/Invoke, no dependency, and it is the question Windows asks itself before it shows a
  notification" (README, "Design decisions").

## Consequences

- The unlock reminder appears even while the shell still answers `QUNS_NOT_PRESENT` just after
  an unlock.
- Focus Assist does not hold a reminder back: `QuietHours` is "Read and mapped, but nothing acts
  on it yet" (the enum's comment).
- Guards: `UserNotificationStateTests` (each documented state's mapping, anything outside the
  documented range read as `Unknown`, the constants equal to the Windows header's),
  `SuppressionPolicyTests` (`FullScreen` and `Presenting` hold a reminder back; `UserAway`,
  `QuietHours`, `Unknown` and `Normal` do not) and `ReminderCoordinatorTests` (an unlock still
  shows its reminder when Windows says the user is away). The P/Invoke and its failure handling
  are in `WindowsActivityMonitor`, with no test: `--detect` shows the raw number and what it was
  read as (ADR 0020).

Source: CLAUDE.md ("Pausing for what the user is doing"); README.md ("How it detects them",
"Design decisions" row "Full screen"); src/MindfulnessReminder.Core/ForegroundState.cs;
src/MindfulnessReminder.Core/SuppressionPolicy.cs;
src/MindfulnessReminder.App/WindowsActivityMonitor.cs (`ShellState`).
