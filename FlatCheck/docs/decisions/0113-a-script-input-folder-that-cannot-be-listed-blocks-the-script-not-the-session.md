# 0113. A script input folder that cannot be listed blocks the script, not the session

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

The window checks, before a Siril script can be started, that every folder the script enters
(`biases/`, `flats/`, `lights/` for WithoutDark) exists and holds a file. The review of
2026-11-13 (CR-016) read that the check listed each folder without catching anything, so a
folder that is there but cannot be listed (a deny rule, a broken junction) threw out of it.
The failing tests of 2026-11-14 showed three outcomes, by where the check was called from:

- a folder without a master: the check runs for the problem card, outside the `try` of the
  opening, in a task nobody awaits. The card showed the script list, a grey `Run in Siril`
  button, no reason, and nothing in the log;
- a session with a master: the check runs for the Split tab, inside that `try`, so the whole
  folder was refused with the card "The folder could not be read", as R-080 said for any
  folder that cannot be listed, though the Structure, Exposure, Histogram and Synthetic tabs
  never read `lights/` or `biases/`;
- a folder that turned unlistable after the opening: the press on `Start Split` threw before
  its own `try`, into a task nobody reads, and the button seemed to do nothing.

The first and the third are defects under any reading. The second was a choice: R-080 gave the
card on purpose for a folder the window itself must list (`flats/` for the Exposure tab's
list), and the script's inputs fell under it only because the same exception passed through.

## Decision

The owner's choice of 2026-11-14, between opening the session and keeping the card:

1. A folder the chosen script enters that cannot be listed is a blocker of the script choice,
   beside "missing or empty" and before it, with a text of its own that names the script, the
   folder and the system's message, and says to check the permissions and open the session
   folder again. It is never called missing or empty: nobody saw inside.
2. The blocker is shown where the script is run: on the problem card of a folder without a
   master, and on the Split tab. A session with a master opens with every tab.
3. The exception is one Warn line of the log, as for a script that cannot be read.
4. The card "The folder could not be read" stays for what the window itself must list to open
   the session (`flats/` with a master present).
5. A task the window starts without awaiting it (the opening from the start, from the folder
   dialog, the reopening after a Siril run) is followed to its end, and a fault is written to
   the log at Error. Nothing on screen: what such a fault would be is not known in advance.

## Alternatives rejected

- **Keep the card for a session with a master.** One rule for every folder that cannot be
  listed, but a session is refused for a folder four of its five tabs never read.
- **Call the folder missing or empty.** No new text, and false: the advice "put the camera's
  frames there" sends the user to a folder that may be full.
- **Catch the exception around the whole check and name no folder.** The system's message
  holds the path, but the text would not say which of the script's folders it is in the
  script's own words (`flats/`).
- **Show a card for any fault of an unawaited opening.** A text for a failure nobody has seen;
  the Error line is there for the report of a bug.

## Consequences

- R-080 holds the behaviour; `R080_AnUnlistableInputFolderBlocksTheCardsButtonAndIsNamed` in
  `MainViewModelTests.cs` and two `R080_...` tests in `SplitTabTests.cs` deny a folder to the
  user and read the blocker and the log;
  `R110_ATaskTheWindowDoesNotAwaitLeavesAnErrorLineWhenItFaults` holds the last point for the
  helper, `Detached.Watch`; that `MainWindow` passes its three tasks to it is read in the code,
  not pinned by a test.
- The text says "open the session folder again" because nothing re-checks a blocked choice by
  itself: the button is disabled while the blocker stands. The older "missing or empty" text
  still says "press again"; the review's CR-037 lists such texts.
- The Warn line is written at every check, so a folder that stays unlistable leaves one line
  per attach, change of script and press.
