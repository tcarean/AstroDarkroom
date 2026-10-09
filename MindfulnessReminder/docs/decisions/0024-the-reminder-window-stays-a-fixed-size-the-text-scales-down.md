# 0024. The reminder window stays a fixed size, 440 x 260, and a message that would not fit is scaled down to fit

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Two reminders, two switches, two
messages", the paragraph "The window stays a fixed size, and the text scales down to fit"),
`README.md` ("Two reminders, two messages", its third paragraph; the "Design decisions" row
"Reminder text"), the comment on the `Viewbox` in `BreakWindow.xaml` and the diff of 2be5361; the
decision itself dates from 2be5361 (a switch and a message for each reminder, 1.4.0,
2026-09-14). The window has been 440 by 260 since the base version (61a0121), placed by hand in
`OnSourceInitialized`, centred in a monitor's work area, the primary one since 51cb690 (ADR
0026). Until 1.4.0 it showed one fixed text at 30 px ("Ground yourself" since f3c8cba, the
changed message, 2026-09-08). From 1.4.0 a message may run to 50 characters (ADR 0023), and "50
characters can wrap to three lines at 30 px, one more than fits above the Close button"
(`CLAUDE.md`). The comment on `ReminderSettings.MaxMessageLength`, written in the same commit,
says otherwise: fifty characters are "two lines at the reminder window's 30 px type", and "a
longer message would run into the button rather than grow the window". The code does what
`CLAUDE.md` and the `BreakWindow.xaml` comment say: a message that needs a third line is scaled
down. The same diff also moved the text. Before 1.4.0 it sat 22 px under the dot, in a row
sized to it, with the Close button at the bottom of the rest; now the `Viewbox` is centred in
the row between the dot and the button. The sources' "drawn exactly as before" therefore holds
for a shorter message's size, which the `Viewbox` leaves alone; its place follows the centring.

## Decision

- `BreakWindow` keeps `Width="440" Height="260"` and `ResizeMode="NoResize"`, whatever the
  message says.
- The message is a `TextBlock` at `FontSize="30"` with `TextWrapping="Wrap"`, inside a `Viewbox`
  with `Stretch="Uniform"` and `StretchDirection="DownOnly"`, in the row between the dot and the
  Close button. A message that would not fit there is scaled down until it does; a shorter one,
  the default included, stays at 30 px, since `DownOnly` never scales up.
- The `TextBlock` has an explicit `Width="332"`, the width inside the card's 18 px margin and its
  grid's 36 px margins, because "without it a `Viewbox` measures its child with unlimited space
  and nothing wraps" (`CLAUDE.md`).

## Alternatives rejected

- **Growing the window to fit the text.** It "would have meant placing it after layout rather
  than in `OnSourceInitialized`" (`CLAUDE.md`), and the reminder would no longer keep "the same
  size and place on screen whatever it says" (README).
- **A `Viewbox` that also scales up.** Short messages, the default first, would be drawn larger
  than 30 px; one that only scales down "leaves every shorter message, the default included,
  exactly as it was" (README, "Design decisions").
- **Letting a long message run on.** Its third line would run into the Close button; with the
  `Viewbox`, "a long one shrinks to fit instead of running into the Close button" (the
  `BreakWindow.xaml` comment).

## Consequences

- The reminder appears at the same size and in the same place whatever it says; a message that
  needs a third line is drawn smaller than 30 px, and a shorter one at 30 px, unscaled.
- Whether a given message shrinks is seen only on screen: `--preview "<text>"` shows it before
  it is saved (ADR 0023).
- Guards: none by test; launch the application with `--preview`, or from the tray.

Source: CLAUDE.md ("Two reminders, two switches, two messages"); README.md ("Two reminders, two
messages", "Design decisions" row "Reminder text"); src/MindfulnessReminder.App/BreakWindow.xaml;
src/MindfulnessReminder.App/BreakWindow.xaml.cs (`OnSourceInitialized`);
src/MindfulnessReminder.Core/ReminderSettings.cs (`MaxMessageLength`); the diff of 2be5361.
