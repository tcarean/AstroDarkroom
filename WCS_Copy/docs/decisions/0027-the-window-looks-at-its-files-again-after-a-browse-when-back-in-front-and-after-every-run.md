# 0027. The window looks at its files again after a Browse, when back in front, and after every run

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

A box's file was looked at only when the box's text changed, as `docs/design/window.md` made
deliberate against a check in the getters, which the boxes call at every keystroke. CR-010 of the
review of 2026-11-16 read two cases that follow: the path of a file Siril had not saved yet,
pasted, kept Preview and Copy off after Siril saved it, and picking the same file in Browse
changed nothing, the text being equal; and a source that read `not plate-solved` kept saying so
after it was solved and saved, while Preview already worked on the new header. The entry was the
owner's call; on 2026-11-18 the owner chose to have it fixed, with no new text. Since CR-008's fix
(ADR 0025) a look runs off the window's thread, so looking more often costs the window nothing.

## Decision

The window looks at the files again (R-054): a box's file when a Browse of that box picks a file,
the path the box already holds included; both files when the window comes back to the front, the
way the user comes back from Siril or File Explorer; and both after every Preview and Copy, once
its outcome is shown. A look again keeps the fact line and the commands as they are until it
ends, and empties no card; one asked while a run is in flight, the window back in front during a
copy most often, is left to the run's end, whose own look covers it; and a window that has closed
starts no look, so the run a close was held for (R-050) writes no read line after the window's
last one (the review of the branch on 2026-11-18); a look already in flight then, on a slow disk
or share, cannot be stopped, and may still write its line after it or race the process's exit
line. The entry named "before a run": the run reads both files itself, and a look after it makes
the lines agree with what it found without the window's `read` lines falling between the run's
start and done lines in the log (R-043).

## Alternatives rejected

- **Keep the check once per text, and reject the entry.** The owner chose the fix: the two cases
  are ordinary ones in a night with Siril open beside the window.
- **Watch the two files with a `FileSystemWatcher`.** It needs a watch per folder, misses changes
  on some shares, and fires several times per save; coming back to the window covers the cases.
- **Look before a run instead of after.** The look's `read` line would fall inside the run's
  lines, or the run would wait for it; the run reads the files anyway.

## Consequences

Each return of the window to the front writes the reader's `read` line of each existing file to
the log, and so does the end of every run while the window is open. The window's `Activated`
reaches `LookAgain` through `LookAgainWhenBackInFront`, and the Browse handlers go through
`PickSource` and `PickDest`. A box
whose text changes during a run is still looked at at once, as its facts were read at once
before, so that look's read line can fall among the run's, on another thread. Guarded by
`MainViewModelTests.R054_BrowsingThePathTheBoxHoldsLooksAgain`,
`R054_ALookAgainKeepsTheLineAndTheCommandsUntilItEnds`, `R054_ARunLooksAgainWhenItEnds`,
`R054_ALookAgainAskedDuringARunWaitsForTheRunsEnd`,
`WindowSmokeTests.R054_TheWindowBackInFrontLooksAgain` and
`WindowLoggingTests.R054_AClosedWindowLooksAtNoFile`; the window's `OnClosed` calls the
view-model's `StopLooking`.
