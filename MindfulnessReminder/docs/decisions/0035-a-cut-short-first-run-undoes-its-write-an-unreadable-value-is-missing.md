# 0035. A first run cut short by a refusal deletes the Run value it wrote; an auto-start value the account may not read counts as missing

Date: 2026-10-22
Status: accepted 2026-10-22

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
2026-10-22, the reading of an unreadable veto included (the plan
`docs/plans/2026-10-22-handle-refused-auto-start-access.md`).

## Decision

- **A first run undoes only what it wrote.** Before registering, `EnsureRegisteredOnFirstRun`
  reads the status. When `Enable` or the marker write is then refused and the status was
  `Disabled`, it calls `Disable`, and a refusal of that is let go. A Run value that was already
  there stays: while the marker's key refuses writes every start is a first run, and an entry
  the user ticked on in the tray must survive it. Either way no marker is written, the call
  returns `false`, and the next start tries again (R-003).
- **A value the account may not read counts as missing.** `GetStatus` reads the Run value and
  the approval value through the three refusals R-051 already treats as one for the settings,
  `SecurityException`, `UnauthorizedAccessException` and `IOException`. An unreadable Run value
  is `Disabled`; an unreadable approval value is no veto, so a readable Run value beside it is
  `Enabled` (R-004). Only `GetStatus` changes: `Enable`, `Disable` and `RepairPathIfNeeded`
  still throw, and their callers catch a refusal as before.

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

## Consequences

- The tray menu no longer ends the application over a key it may not read, and a refused first
  run leaves the registry as it found it.
- With an approval value it may not read, the item is ticked for an entry Task Manager may have
  switched off; unticking it still deletes the Run value.
- `FakeRegistryStore.RefusedKeyPath` narrows `ReadRefusal` and `WriteRefusal` to one key, so a
  test can let the first write of a run through and refuse the second; its `DeleteValue` now
  throws `WriteRefusal` too.
- Guards: `StartupRegistrarTests` (a refusal after the Run value is written, from the marker's
  key and from the Task Manager key, leaves nothing registered; a Run value found before the
  first run stays) and `RegistryAutoStartManagerTests` (an unreadable Run value is `Disabled`,
  an unreadable approval value is no veto, each for the three refusals). The tray with a
  refused key: none by test; on an account denied Read on `StartupApproved\Run`, open the menu.
