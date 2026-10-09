# 0021. Two reminders, two switches, two messages, independent of each other and of the pauses; the unlock gate sits after the schedule restart and before everything else

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Two reminders, two switches, two
messages", its first three paragraphs; "Structure", the line on `ReminderSettings.cs`; "Pausing
for what the user is doing", its first paragraph), `README.md` ("Two reminders, two messages",
its first two paragraphs), the summaries of `ReminderSettings`, `ReminderCoordinator` and
`SuppressionPolicy`, the comment at the gate in `HandleUnlock`, the comment on the pauses in
`SettingsWindow.xaml` and the message of 2be5361; the decision itself dates from 2be5361 (a
switch and a message for each reminder, 1.4.0, 2026-09-14). Its message gives the situation:
"The unlock reminder had no switch of its own and both reminders said the same hardcoded text."
The periodic reminder had had its switch, `PeriodicEnabled`, since 1.1.0 (b6554d5). An unlock
does two jobs: it restarts the periodic countdown for a whole interval (ADR 0014), and it shows
the unlock reminder, after the already-showing check and the debounce (ADR 0015) and unless a
pause holds it back (ADR 0018).

## Decision

- `ReminderSettings` carries `UnlockEnabled` (default true), `UnlockMessage` and `PeriodicMessage`
  (both `DefaultMessage`, "Ground yourself"): init-only properties with the old behaviour as
  their default rather than more positional parameters, so that `new ReminderSettings(true, 30)`
  "goes on meaning exactly what it meant before they existed" (the `ReminderSettings` summary).
- The two switches are independent: "Nothing about one switch is allowed to read the other"
  (`CLAUDE.md`). Turning the unlock reminder off does not stop the countdown restarting on
  unlock; turning the periodic one off does not stop the unlock reminder.
- In `HandleUnlock` the unlock gate, `if (!Settings.UnlockEnabled) return;`, sits after the
  schedule restart (with the reset of `Suppression`, `SyncTimer` and the `CountdownChanged`
  event that follow it) and before everything else. The restart belongs to the periodic
  reminder; everything below the gate is the unlock reminder acting: "the already-showing raise,
  the debounce, the activity read and the window" (`CLAUDE.md`).
- The two pauses are independent of each other, each switch turning off only its own check, and
  belong to neither reminder: they hold back whichever is about to appear. `SuppressionPolicy`
  reads neither reminder's switch, and the Settings window puts the pauses at the top level,
  "under both reminders and inside neither panel, because they belong to neither" (the
  `SettingsWindow.xaml` comment).

## Alternatives rejected

- **More positional parameters on the record.** Every one of the new values has a default "the
  same on a fresh machine and in a hand-written test, so writing it out at every call site would
  be repetition with nothing to say" (the `ReminderSettings` summary).
- **The gate first in `HandleUnlock`.** With the unlock reminder off, "a lock of three hours"
  would then be followed by "a deadline set before the lock" instead of "a whole interval"
  (`CLAUDE.md`).
- **The gate further down, after the already-showing check or the activity read.** A window
  left open from before the lock would be raised by a reminder that is switched off, and the
  registry would be walked "to decide whether a reminder that is not going to appear would have
  been held back" (`CLAUDE.md`).
- **The pauses inside the periodic panel.** The unlock reminder "obeys them too, which is why
  they sit at the top level of the Settings window rather than inside the periodic panel"
  (`CLAUDE.md`, "Pausing for what the user is doing").

## Consequences

- With the unlock reminder off, a lock of any length is still followed by a whole interval of
  the periodic one: "the periodic reminder still comes a whole interval after you come back"
  (README). With both reminders off, nothing ever appears and no timer runs.
- With the unlock reminder off, an unlock neither reads the activity monitor nor raises a window
  left open from before the lock.
- The new values are stored as `UnlockReminderEnabled`, `UnlockMessage` and `PeriodicMessage`
  (ADR 0027); a registry an older build wrote has none of them and reads as the defaults, the
  old behaviour. Who picks a message is ADR 0022, how it is normalized ADR 0023.
- Guards: `ReminderCoordinatorTests`, from both sides as `CLAUDE.md` says:
  `R027_Switching_the_periodic_reminder_off_leaves_the_unlock_reminder_alone` and
  `R013_Switching_the_unlock_reminder_off_shows_nothing_on_unlock`, with the countdown still
  restarted on unlock, no activity read and an open window left where it is while the unlock
  reminder is off, and nothing at all with both off; `ReminderSettingsStoreTests` (the defaults,
  the round trip, a registry without the new values); `SuppressionPolicyTests` (each pause
  switch turns off only its own check; the pauses apply with the periodic reminder off).

Source: CLAUDE.md ("Two reminders, two switches, two messages", "Structure", "Pausing for what
the user is doing"); README.md ("Two reminders, two messages");
src/MindfulnessReminder.Core/ReminderSettings.cs;
src/MindfulnessReminder.Core/ReminderCoordinator.cs (the summary, `HandleUnlock`);
src/MindfulnessReminder.Core/SuppressionPolicy.cs;
src/MindfulnessReminder.App/SettingsWindow.xaml; the message of 2be5361.
