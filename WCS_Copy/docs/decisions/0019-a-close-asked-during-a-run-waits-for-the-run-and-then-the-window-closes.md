# 0019. A close asked during a run waits for the run, and then the window closes

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

The window runs the core on a thread-pool thread so that it stays live during a copy (R-030),
and nothing held a close meanwhile: `ShutdownMode="OnMainWindowClose"` ended the process, and the
process ended the background thread inside `input.CopyTo(output)`. CR-002 of the review of
2026-11-16 read the outcome: a new file left with its header and part of its data, an existing
file the user had agreed to overwrite included, a partial `.tmp` beside an in-place destination,
and a log whose last lines were `exit 0` without the copy's done line. A copy of the 290 MB Veil
stack took 0.5 to 1.5 s on 2026-10-30 (`docs/design/logging.md`), longer on a cold disk or a
larger stack. On 2026-11-18 the owner chose, of the two ways the entry named, to hold the close
until the run ends and then close the window, with no new text.

## Decision

`MainWindow.OnClosing` cancels a close while the view-model's `IsRunning` is true, remembers it
and logs it once; when `IsRunning` turns false the window logs and closes itself, without showing
the outcome (R-050). A second close meanwhile adds nothing. A close Windows forces at a logoff or
a shutdown is not held: WPF ignores the cancel then.

## Alternatives rejected

- **Refuse the close with a message.** The owner chose the wait: the user would read a message,
  wait, and close again, for a run of a second or so.
- **Hide the window and let the run finish in the background.** The user would see no window
  while a copy still writes, and could start another one on the same files.
- **Make the run a foreground thread.** The process would outlive its window with no sign of it,
  and a run that hangs on a dropped share would keep it alive unseen.

## Consequences

The view-model exposes `IsRunning`, raised as a run starts and after its outcome is shown, and
`RunCore`, the run its commands call, which `WindowCloseTests` replace with one that waits for
the test so that a run can be held in flight. While a close is held nothing on screen changes
(CR-009, the outcome card during a run, is still open). A run that hangs keeps the window open
until the system gives up on the file. Guarded by `WindowCloseTests` (R-050) and
`WindowLoggingTests`' close lines (R-043).
