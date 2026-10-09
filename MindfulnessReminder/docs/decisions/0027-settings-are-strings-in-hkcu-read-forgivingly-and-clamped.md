# 0027. Settings are eight strings under HKCU\Software\MindfulnessReminder, read forgivingly and clamped; the Settings dialog is the one place that refuses

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Settings storage"; "Structure", the
`ISettingsStore.cs` / `RegistrySettingsStore.cs` line), `README.md` ("Design decisions", row
"Settings storage") and the comments of `RegistrySettingsStore.cs`, `ISettingsStore.cs`,
`ReminderSettings.cs` and `SettingsWindow.xaml.cs`; the decision itself dates from b6554d5 (the
periodic reminder, 1.1.0, 2026-09-08), which stored the first two values, and was extended to
five by 89de6c8 (pausing for a call or full screen, 1.2.0, 2026-09-08) and to eight by 2be5361
(a switch and a message for each reminder, 1.4.0, 2026-09-14). Until b6554d5 the application's
own key held one value, the `AutoStartInitialized` marker of `StartupRegistrar` (ADR 0028),
written through `IRegistryStore`, whose four methods read a string, read a binary value, write a
string and delete a value. From then on every build has had to start on a registry written by an
earlier build, or by hand: "A missing value is a fresh machine, a corrupt one is a hand-edited
registry", and "a value missing because it was written by an older build is exactly the first of
those". The bounds are not decoration: "a hand-edited `0` would otherwise mean a reminder on every
tick of the timer". The class summary of `RegistrySettingsStore` still says "Both values are
written as strings", from b6554d5; all eight are.

## Decision

One key for everything the application remembers: `RegistrySettingsStore.KeyPath` is
`StartupRegistrar.SettingsKeyPath`, `HKCU\Software\MindfulnessReminder`. Eight values, all
strings (`REG_SZ`), "so `IRegistryStore` stays the four methods it had and `FakeRegistryStore`
needed no change":

| value | contents | default |
| --- | --- | --- |
| `PeriodicRemindersEnabled` | `"1"` / `"0"` | on |
| `PeriodicIntervalMinutes` | decimal string | 30 |
| `PauseWhileMicrophoneInUse` | `"1"` / `"0"` | on |
| `PauseWhileFullScreen` | `"1"` / `"0"` | on |
| `PauseRetrySeconds` | decimal string | 20 |
| `UnlockReminderEnabled` | `"1"` / `"0"` | on |
| `UnlockMessage` | the text, after normalization | "Ground yourself" |
| `PeriodicMessage` | the text, after normalization | "Ground yourself" |

- **Reading is forgiving.** `Load` "Never throws and never returns null; anything unreadable
  falls back to the defaults" (`ISettingsStore`): a missing value, a flag other than `"1"` or
  `"0"`, and a number that does not parse each read as the default, and a key the account may
  not read gives all the defaults. The result then goes through
  `ReminderSettings.Normalize`: the interval is clamped into 1 to 480 minutes and the retry gap
  into 5 to 300 seconds, "clamped rather than rejected", and a blank or over-long message becomes
  the default text or the text cut at 50 characters (ADR 0023).
- **Saving clamps too.** `Save` writes `settings.Normalize()`.
- **The Settings dialog is the exception:** "it refuses to save an out-of-range number and says
  so, because there is a user standing there to tell", in the window, with the cursor put in the
  box to fix. It refuses only what something will use: the interval while the periodic reminder
  is on, the retry gap while either pause is on, a blank message while its reminder is on;
  otherwise the default is saved in its place.

## Alternatives rejected

- **DWORDs.** "a DWORD would have meant a wider interface for the sake of one value"
  (`RegistrySettingsStore`, written when the interval was the only number).
- **Failing on a missing or corrupt value.** Neither "is a reason to fail to start", and a
  missing one is what every upgrade from an older build looks like.
- **Taking a stored number literally.** A hand-edited `0` interval would mean a reminder on every
  tick; a `0` retry gap "would otherwise read the registry on every tick of the timer"
  (`ReminderSettingsStoreTests`).
- **Clamping in the dialog as well.** "Saving is refused, with the reason spelled out in the
  window, rather than silently clamping a number the user typed -- clamping happens on the way
  out of the registry, where there is nobody to tell" (`SettingsWindow`).

## Consequences

- A new setting is one more string value under the same key, read with its default as the
  fallback, so a registry written before it existed lands on the default; its bounds sit in
  `ReminderSettings` beside `Normalize`.
- The dialog's checks (`IsIntervalInRange`, `IsRetryInRange`, `IsMessageAcceptable`) live in Core
  beside the clamps, so the two cannot disagree on a bound.
- `App` saves before it applies the new settings: "if the write fails, the app has not started
  behaving in a way the next launch will contradict" (`App.ShowSettings`).
- Guards: `ReminderSettingsStoreTests` (the key shared with the auto-start marker, which a save
  leaves alone; the `"1"` / `"0"` strings; the defaults from an empty registry and from one
  written before the pauses or the unlock values existed; the fallbacks on corrupt values; the
  clamps on reading and on saving; the dialog's interval check against the clamp and its message
  check against the limit). The refusal in `SettingsWindow`: none by test; launch the application
  from the tray and open Settings.

Source: CLAUDE.md ("Settings storage"; "Structure"); README.md ("Design decisions", row "Settings
storage"); src/MindfulnessReminder.Core/RegistrySettingsStore.cs;
src/MindfulnessReminder.Core/ISettingsStore.cs; src/MindfulnessReminder.Core/ReminderSettings.cs;
src/MindfulnessReminder.App/SettingsWindow.xaml.cs; src/MindfulnessReminder.App/App.xaml.cs;
tests/MindfulnessReminder.Tests/ReminderSettingsStoreTests.cs.
