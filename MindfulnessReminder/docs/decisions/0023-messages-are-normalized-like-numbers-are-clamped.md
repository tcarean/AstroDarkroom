# 0023. Messages are normalized like numbers are clamped: silently for the registry, refused by the dialog only for a reminder that is on; --preview takes a text

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Two reminders, two switches, two
messages", the paragraph "Messages are normalized like numbers are clamped" and the `--preview`
paragraph; "Settings storage", its last two paragraphs; "Build & test", the two `--preview`
commands), `README.md` ("Two reminders, two messages"), the comments on
`ReminderSettings.NormalizeMessage` and `IsMessageAcceptable`, on the two message boxes in
`SettingsWindow.xaml`, in `SettingsWindow.OnSaveClick` and `RegistrySettingsStore.Load`, and on
`PreviewSwitch` and `PreviewMessage` in `App.xaml.cs`; the decision itself dates from 2be5361 (a
switch and a message for each reminder, 1.4.0, 2026-09-14). With the messages a setting (ADR
0021), text reaches the reminder window from two places: the registry, which holds whatever a
hand edit or an older build left there, and the Settings dialog, where a user is typing. The
numbers already had a rule for the same two places: clamped silently from the registry, refused
with a reason by the dialog, and refused only for a feature that is switched on (ADR 0027). The
window is a fixed size (ADR 0024).

## Decision

- `ReminderSettings.NormalizeMessage` is the one rule: line breaks and tabs become spaces, the
  text is trimmed, nothing at all (null, empty or blank) reads as `DefaultMessage`, and a text
  longer than `MaxMessageLength` (50) is cut at 50 and trimmed again. It returns the same
  instance when nothing needed doing. `Normalize()` applies it to both messages as it clamps the
  two numbers, and everything that builds settings goes through `Normalize`.
- The registry goes through it silently: `RegistrySettingsStore` falls back only when a value is
  absent, and normalizes what it loads and again what it saves.
- The dialog refuses instead: for a reminder that is switched on, Save requires
  `IsMessageAcceptable` (something other than whitespace, at most 50 characters) and otherwise
  says "Enter what the reminder should say, up to 50 characters." For a reminder that is
  switched off a failed check refuses nothing: "A blank box with its reminder switched off falls
  back to the default text rather than standing in the way of the save" (`OnSaveClick`). It is
  "the same rule the numbers follow, for the same reason" (`CLAUDE.md`).
- Each message box is one line, and its `MaxLength` reads the constant through `x:Static`
  (`{x:Static core:ReminderSettings.MaxMessageLength}`), "so the box and the clamp cannot drift
  apart" (`CLAUDE.md`).
- `--preview "some text"` shows the reminder window with that text after `NormalizeMessage`,
  without saving it and without touching the registry; with no text, or an empty one, it shows
  the default.

## Alternatives rejected

- **Refusing a bad message read from the registry.** Of a stored message, "a missing one is a
  build that never wrote it, an empty one is a wiped value, and both land on the default text"
  (the comment in `RegistrySettingsStore.Load`): neither is a reason to fail to start (ADR
  0027). Whatever was written "is read as generously as possible rather than refused" (the
  `NormalizeMessage` summary).
- **Falling back silently in the dialog too.** "The dialog refuses rather than falling back,
  because there is a user standing there to tell; the fallback in NormalizeMessage is for the
  registry, where there is not" (the `IsMessageAcceptable` summary).
- **Requiring a usable message for a reminder that is off.** Switching a reminder off with its
  box cleared would be refused; for the numbers, "Switching the feature off with rubbish left in
  the box would otherwise be impossible" (`OnSaveClick`).
- **A literal limit in the XAML.** The box and the clamp could drift apart.
- **A multi-line box.** One line of input, because "the window wraps for itself, and a line
  break typed here would only be turned into a space on save" (the comments in
  `SettingsWindow.xaml`).

## Consequences

- No message the application shows is blank or longer than 50 characters, whether it came from
  the registry, the dialog or the command line: a hand-edited registry "cannot push the text
  into the Close button" (the `NormalizeMessage` summary).
- `--preview "text"` is "the way to see what a candidate message looks like at the cap"
  (`CLAUDE.md`), with nothing saved.
- Changing the limit is a change to `MaxMessageLength` alone, which the cut, the dialog's check,
  its refusal text and both boxes read; how a long message fits the window is ADR 0024.
- Guards: `ReminderSettingsStoreTests` (a blank message read from the registry is the default;
  an over-long one is cut at the limit; saving normalizes both messages; what normalization
  keeps; a cut message is trimmed after the cut; `IsMessageAcceptable` refuses blank text and
  matches the length limit). The dialog's refusal, the `x:Static` `MaxLength` and
  `--preview "text"` are in `src/MindfulnessReminder.App/`, with no test: launch the application
  with `--preview`, or from the tray.

Source: CLAUDE.md ("Two reminders, two switches, two messages", "Settings storage", "Build &
test"); README.md ("Two reminders, two messages"); src/MindfulnessReminder.Core/ReminderSettings.cs
(`NormalizeMessage`, `Normalize`, `IsMessageAcceptable`, `MaxMessageLength`);
src/MindfulnessReminder.Core/RegistrySettingsStore.cs;
src/MindfulnessReminder.App/SettingsWindow.xaml;
src/MindfulnessReminder.App/SettingsWindow.xaml.cs (`OnSaveClick`);
src/MindfulnessReminder.App/App.xaml.cs (`PreviewSwitch`, `PreviewMessage`).
