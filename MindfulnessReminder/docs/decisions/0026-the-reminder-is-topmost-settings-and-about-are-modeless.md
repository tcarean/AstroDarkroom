# 0026. The reminder is topmost, shown after 600 ms on the primary monitor and deaf to Enter; Settings and About are modeless and single-instance

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Windows the app shows"; "Structure", the
`App.xaml.cs` line), `README.md` ("Behaviour", the reminder bullet; "Periodic reminders", the
fourth bullet; "Design decisions", row "Settings window") and the comments of
`BreakWindow.xaml(.cs)`, `WpfReminderPresenter.cs`, `WindowPlacement.cs`, `App.xaml.cs`,
`SettingsWindow.xaml` and `AboutWindow.xaml`; the decision itself dates from 61a0121 (the base
version, 2026-08-28: a topmost reminder that takes the focus, shown after a delay), 51cb690 (the
reminder moved to the primary screen, 2026-09-08), b6554d5 (the periodic reminder, 1.1.0,
2026-09-08: Enter, and the modeless Settings window) and 432edfd (the dark theme, 1.3.0,
2026-09-08: About as a modeless window). The base version showed the reminder on unlock only, on
the monitor holding the mouse pointer, with a Close button that was `IsDefault` and `IsCancel`
(its README: "Esc and Enter also close it"). Two things taught the rest. The pointer "sits
wherever it was left before the lock, or wherever the logon screen parked it, so the reminder
surfaced on a different monitor each time" (51cb690). And once the reminder became periodic it
arrived mid-work and took the focus, so "the next Enter already on its way would both dismiss an
unread reminder and be lost to whatever was being typed" (`CLAUDE.md`). The same commit brought
the Settings window, and a modal `ShowDialog` "disables every other window in the process, so a
reminder falling due while it sat open would appear with a dead Close button".

## Decision

- **The reminder, `BreakWindow`**: modeless, `Topmost`, and it takes the focus.
  `OnSourceInitialized` places it, re-asserts `Topmost`, activates it and focuses its Close
  button, because "Windows only grants foreground activation to a process with recent input, and
  a freshly unlocked desktop has none". `WpfReminderPresenter` creates it `DefaultShowDelay`
  (600 ms) after it is asked for, because "a window created into that moment often ends up behind
  everything else"; a queued show counts as showing. It is centred by `WindowPlacement.CenterIn`
  on the work area of the primary monitor, "always the same one", and kept to the area's top-left
  edge when it is wider or taller than the area; the pointer's monitor is used only when there is
  no primary screen.
- **Its keys.** The Close button is not `IsDefault`, and `OnPreviewKeyDown` marks Enter handled:
  "It must be *Preview*: plain `KeyDown` bubbles up from the focused button, which would already
  have handled the key." Esc closes it (the button is `IsCancel`, and `OnKeyDown` closes on
  Escape), and so does Space, to which the focused button responds.
- **`SettingsWindow` and `AboutWindow`**: modeless (`Show`, never `ShowDialog`), not topmost,
  ordinary focus, and single-instance: `App` keeps the one open copy, and reopening restores it
  if minimised and activates it. Their lifetimes are `App`'s (`ShowSettings`, `ShowAbout`);
  `TrayIconHost` only raises the request. Settings hands its result back through its `Accepted`
  event, since there is no `ShowDialog` to return to. About's Close button *is* `IsDefault`:
  "this window was opened on purpose, so Enter closing it is what the user means."

## Alternatives rejected

- **The monitor holding the pointer, as in 61a0121.** Following it "sounds friendlier but is not
  reproducible": the reminder surfaced on a different monitor each time
  (`BreakWindow.MoveToPrimaryScreen`).
- **WPF's `CenterScreen` for the reminder.** It "picks whichever monitor Windows happened to place
  the new handle on, which on a multi-monitor desk is effectively random" (`WindowPlacement`).
  Settings and About, which the user opens, do use it.
- **Showing the window the instant it is asked for.** "the shell is still restoring the desktop
  then" (`WpfReminderPresenter`), and the window lands behind everything.
- **Enter closing the reminder, as until b6554d5.** It suited a window that "only appeared on a
  freshly unlocked desktop where nobody is typing" (README); mid-work, a keystroke meant for
  something else would dismiss a reminder nobody read.
- **Swallowing Enter in `KeyDown`.** Too late: the focused button has handled it by then.
- **`ShowDialog` for Settings or About.** It disables every other window in the process, the
  reminder's Close button included.
- **A new Settings window on every click.** "two copies of the window could be saved in either
  order, and the loser would silently win" (`App.ShowSettings`).

## Consequences

- Nothing but the reminder is topmost or takes the focus on a schedule. A new window the user
  opens on purpose is modeless and single-instance, with its lifetime in `App`; ADR 0032 records
  how the opening of About moved there from `TrayIconHost`.
- If another key ever turns out to be likely mid-sentence, "the same handler is the place"
  (`CLAUDE.md`).
- The reminder has a fixed size (ADR 0024), which is what lets it be placed in
  `OnSourceInitialized`. A reminder already open, or still in its 600 ms queue, is raised rather
  than doubled (ADR 0015) and keeps the text it opened with (ADR 0022).
- `--preview` shows the window directly, without the presenter and its delay.
- Guards: `WindowPlacementTests` pins the centring (a 440 x 260 window in a 1920 x 1080 work area
  lands at 740, 410; a secondary monitor's offset is kept; an area smaller than the window keeps
  it at the area's corner). The rest: none by test; launch the application with `--preview` (the
  place, Enter, Esc, Space), or from the tray (the delay after an unlock, Settings, About).

Source: CLAUDE.md ("Windows the app shows"; "Structure"); README.md ("Behaviour"; "Periodic
reminders"; "Design decisions", row "Settings window");
src/MindfulnessReminder.App/BreakWindow.xaml; src/MindfulnessReminder.App/BreakWindow.xaml.cs;
src/MindfulnessReminder.App/WpfReminderPresenter.cs;
src/MindfulnessReminder.Core/WindowPlacement.cs; src/MindfulnessReminder.App/App.xaml.cs;
src/MindfulnessReminder.App/SettingsWindow.xaml;
src/MindfulnessReminder.App/SettingsWindow.xaml.cs; src/MindfulnessReminder.App/AboutWindow.xaml;
tests/MindfulnessReminder.Tests/WindowPlacementTests.cs.
