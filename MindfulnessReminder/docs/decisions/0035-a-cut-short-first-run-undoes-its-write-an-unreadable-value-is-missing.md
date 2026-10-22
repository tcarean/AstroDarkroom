# 0035. Auto-start under a registry that refuses access: an unreadable value counts as missing, Enable lifts only a veto it can read, and a first run takes back what it wrote and only repairs an entry it finds

Date: 2026-10-22
Status: accepted 2026-10-22
Supersedes: 0028 in part (Enable deleting the approval value, and the first run registering over an entry already there)

## Context

The code review of the documentation backfill on 2026-10-21
(`docs/plans/2026-10-21-document-every-type-and-method.md`, "Code review") found two cases of a
registry that refuses access which ADR 0028 did not cover. First, `EnsureRegisteredOnFirstRun`
called `Enable`, which writes the Run value and then deletes the Task Manager veto, and then
wrote the marker, all under one catch. When the Run key took its write and the
`StartupApproved\Run` key or `HKCU\Software\MindfulnessReminder` refused the next one, the Run
value stayed without its marker, and every later start registered again: an untick did not
last, and ADR 0028's "nothing is registered" did not hold. Second, `GetStatus` let a refused
read through, and `TrayIconHost` calls it unguarded at start, each time the menu opens and after
each click, so a Run or approval value the account may not read ended the application in its
crash dialog; R-005 covered only a refused change. The owner chose both fixes in chat on
2026-10-22, the reading of an unreadable veto included.

The code review of those fixes, the same day, found two more. `Enable` deleted the approval value
even when there was no veto to lift, and the real store opens a key it writes with read access
as well: on an account whose existing approval key refuses access, a tray click wrote the Run
value and still reported a refusal, and the first run took its Run value back at every start, so
the application never registered itself. And a first run that found an entry still called
`Enable`, which lifted that entry's Task Manager veto, at every start while the marker's key
refused writes. The owner chose both changes in chat the same day (the plan
`docs/plans/2026-10-22-handle-refused-auto-start-access.md`, "Code review").

## Decision

- **One definition of a refused access.** `RegistryRefusal` in Core names the exceptions: a
  refused read is a `SecurityException`, an `UnauthorizedAccessException` or an `IOException`
  (R-051's three), a refused write an `UnauthorizedAccessException` or a `SecurityException`
  (R-005). The settings store, the auto-start manager and the tray item use it; the
  registration reads and writes in one block, so it takes either (R-003, R-006).
- **A value the account may not read counts as missing.** `GetStatus` reads the Run value and
  the approval value through the refused reads. An unreadable Run value is `Disabled`; an
  unreadable approval value is no veto, so a readable Run value beside it is `Enabled` (R-004).
  `Enable`, `Disable` and `RepairPathIfNeeded` still throw a refusal, and their callers catch
  it.
- **`Enable` lifts only a veto it can read.** It writes the Run value, then deletes the
  approval value only when its first byte is odd. An approval value that is no veto needs
  nothing, and a key the account may not read it may not write either, while `GetStatus`
  already counts its value as no veto (R-005).
- **A first run registers only where there is no entry.** It first reads the Run value through
  `RepairPathIfNeeded`, before anything is written, so an entry it may not read stops it
  rather than reading as no entry and being overwritten. When the status is `Disabled`, it
  calls `Enable` and writes the marker, and when a refusal follows the write of the Run value,
  it calls `Disable`, letting a refusal of that go. When the status is anything else, an entry
  is already there: the first run has repaired its path and keeps its veto, as a later run does
  (R-006), writes the marker, and returns `false`, so no balloon claims a registration (R-003).
  A refused access writes no marker either way, the call returns `false`, and the next start
  tries again.

## Alternatives rejected

- **Amending R-003 to accept the partial registration.** A Run value without its marker is
  registered again at every start, the very thing ADR 0028 registers once to avoid.
- **An undo that also deletes an entry it found.** The run did not write it; deleting it would
  switch off an auto-start the user, or an earlier build, had set up.
- **A catch in `TrayIconHost`.** The App is not unit tested (ADR 0008), and the tray is not the
  only reader any more: `StartupRegistrar` reads the status before it registers.
- **An unreadable veto read as `BlockedByWindows` or as `Disabled`.** Both show an entry the
  application can see as off, so a click calls `Enable` rather than `Disable`: the tray could
  never delete the Run value, and so never switch auto-start off.
- **`Enable` deleting the veto before it writes the Run value.** When the write is then refused
  after the veto went, an entry that was there reads as `Enabled` behind a "refused" balloon.
  Written first, a refused deletion leaves the entry visibly vetoed, which matches the balloon.
- **A first run that keeps calling `Enable` on an entry it finds.** It lifts the entry's Task
  Manager veto, at every start while the marker's key refuses writes, so a switch-off in Task
  Manager never lasts.

## Consequences

- The tray menu no longer ends the application over a key it may not read. A refused first run
  takes back the Run value it wrote. It does not put back a leftover veto that `Enable` lifted
  where there was no Run value, and a first run that finds an entry may have repaired its path.
- With an approval value it may not read, the item is ticked for an entry Task Manager may have
  switched off; unticking it still deletes the Run value. On such an account the first run
  registers, and a click enables without a balloon.
- `FakeRegistryStore.RefusedKeyPath` narrows `ReadRefusal` and `WriteRefusal` to one key, so a
  test can let the first write of a run through and refuse the second. Its `DeleteValue` throws
  `WriteRefusal` too, and a key with a `ReadRefusal` refuses writes as well, as the real store
  does.
- Guards: `StartupRegistrarTests` (a first run reads the entry before it writes anything, and
  lifts a veto left without its entry; a refusal after the Run value is written, from the marker's
  key and from the Task Manager key, leaves nothing registered; a Task Manager key the account
  may not read, or may not write while there is no veto, does not stop the registration; a first
  run that finds an entry repairs it and keeps its veto, refused or not; a marker or Run value
  the account may not read does not stop the application) and `RegistryAutoStartManagerTests`
  (an unreadable Run value is `Disabled` and an unreadable approval value no veto, each for the
  three refused reads; `Enable` leaves an approval value that is no veto, or that it may not
  read, in place). The tray with a refused key: none by test. On a test account where
  `StartupApproved\Run` exists and is denied Read, start the application: the first-run balloon
  shows, and the menu shows the item ticked with no crash dialog. Untick it and start again: it
  stays unticked.
