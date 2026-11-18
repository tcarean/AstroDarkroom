# 0025. The window looks at its files off its own thread, and only the latest look is shown

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

The source and destination boxes are bound at every keystroke, and each change ran
`File.Exists` and, for an existing file, `FitsHeader.ReadPrimary` in the setter, on the window's
thread (CR-008 of the review of 2026-11-16). Measured on 2026-11-18 with `File.Exists` on paths
typed letter by letter: on a computer that does not answer (`\\nosuchhost7\share\stack.fit`), one
letter took 2,274 ms, at the letter that first names a share; on the owner's NAS (`\\anubis`), 56
ms at most and 119 ms for the whole path; on the mapped drives about 1 ms, on the local disks
3.2 ms at most. The hard disks spin down after 20 minutes on mains; none was caught asleep. A
window that does not answer for seconds, at a letter typed, is a frozen window by the list's
scale. The run already goes off the window's thread (R-030).

## Decision

A box's change starts a look at its file in a `Task.Run`: the existence, then the two facts. The
box's fact line empties at once and its existence turns off, so Preview and Copy wait for the
look; the look's result is shown on the window's thread when it ends, unless a newer look of the
same box was started meanwhile, in which case it is dropped (R-052). A Preview or a Copy waits for
each box's latest look before the core runs, so that the reader's `read` lines of the looks of
what the boxes name stay out of the run's start and done lines in the log, which R-043 compares
with the console's; a look overtaken by a newer text is not waited for, its box no longer naming
its file (the owner's answer of 2026-11-18, after the review of the branch). The look goes
through `LookCore`, which a test replaces to hold a look, as `RunCore` holds a run.

## Alternatives rejected

- **Wait a short time after the last keystroke before looking.** It delays every fact line, a
  local file's too, and a share that does not answer still stops the window when the look comes.
- **Look on the window's thread when the look is fast, off it when slow.** A look cannot be timed
  out on the thread it blocks, and a test under load would see either.
- **Keep the old facts on screen until the new look ends.** They would describe the file the box
  named before, under a box that names another.
- **A run that waits for every look in flight, overtaken ones too.** It keeps every look started
  before the run out of the run's lines, but the review of the branch showed a Preview held 3 s
  by the look of a share path the box no longer held, the card saying the run was in progress and
  a close held meanwhile; the owner chose the latest looks only.

## Consequences

The window's tests wait for `FactsRead`, every look in flight, after setting a path before they
read a fact line or the commands' state, and a render test lets the looks reach the STA thread by
awaiting. A look of a file the window cannot read writes its Debug line from a pool thread. A
box's latest look that hangs on a share delays a Preview or a Copy until the system gives up on
it; the run would hang on the same share. An overtaken look still in flight, or the look of a box
changed during a run, may write its read line among the run's, on another thread. Guarded by
`MainViewModelTests.R052_TheWindowStaysLiveWhileAFileIsLookedAt`,
`R052_OnlyTheLatestTextsLookIsShown`, `R052_ARunWaitsForTheLooksInFlight` and
`R052_ARunWaitsOnlyForEachBoxsLatestLook`.
