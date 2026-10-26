# Settings storage

> Moved unchanged on 2026-10-09 (ADR 0001) from `CLAUDE.md`, "Settings storage". Where a paragraph
> became a decision record, its number is given after it. **Covers:**
> `src/MindfulnessReminder.Core/RegistrySettingsStore.cs`,
> `src/MindfulnessReminder.Core/IRegistryStore.cs`,
> `src/MindfulnessReminder.Core/StartupRegistrar.cs`,
> `src/MindfulnessReminder.Core/ReminderSettings.cs`,
> `src/MindfulnessReminder.App/SettingsWindow.xaml.cs`. A change to the code this describes means
> this note is updated in the same task, or deleted with the decision it explains.

## Settings storage

`HKCU\Software\MindfulnessReminder`, the key `StartupRegistrar` already keeps
`AutoStartInitialized` under. Eight values:

| value | contents |
|---|---|
| `PeriodicRemindersEnabled` | `"1"` / `"0"` |
| `PeriodicIntervalMinutes` | decimal string |
| `PauseWhileMicrophoneInUse` | `"1"` / `"0"` |
| `PauseWhileFullScreen` | `"1"` / `"0"` |
| `PauseRetrySeconds` | decimal string |
| `UnlockReminderEnabled` | `"1"` / `"0"` |
| `UnlockMessage` | the text, as typed after normalization |
| `PeriodicMessage` | the text, as typed after normalization |

All of them **strings, not DWORDs**, so `IRegistryStore` stays the four methods it had and
`FakeRegistryStore` needed no change.

Reading is deliberately forgiving. A missing value is a fresh machine, a corrupt one is a
hand-edited registry, and neither is a reason to fail to start; both land on the defaults
(both reminders **on**, 30 minutes, both pauses **on**, 20 seconds, "Ground yourself" on
both) — and a value missing because it was written by an older build is exactly the first of
those. Out-of-range values are **clamped rather than rejected** — a hand-edited `0` would
otherwise mean a reminder on every tick of the timer. A blank or over-long message is treated
the same way: the default text, or the text cut at the limit.

The dialog is the exception: it refuses to save an out-of-range number and says so, because
there is a user standing there to tell. Clamping silently is for the registry, where there
is not.

See ADR 0027.

Silently towards the user, not towards the log: `Load` writes one line with every setting it
ended up with, the names of the values that were missing or unreadable and fell back, and each
number the clamp changed (`<name> <read> -> <kept>`); a key the account may not read says so in
the same line. `Save` writes the settings it wrote. The three `Read*` helpers add a value's name
to the list of fallbacks as they fall back; the clamps are found by comparing the two numbers
before and after `Normalize`. `StartupRegistrar`, which shares the key, writes a line when it
registers the Run entry, when it repairs its path, and when the registry refuses (ADR 0037).
