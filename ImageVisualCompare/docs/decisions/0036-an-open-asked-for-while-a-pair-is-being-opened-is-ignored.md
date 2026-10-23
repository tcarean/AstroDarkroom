# 0036. An open asked for while a pair is being opened is ignored, neither queued nor cancelling the first

Date: 2026-10-22
Status: accepted 2026-10-22

## Context

R-006 says the two images of a pair are decoded one after the other, never at the same time,
and `ImagePairLoader` keeps to it within a pair. The code review of the documentation backfill
(2026-10-21) found that nothing kept two pairs apart: the loading overlay stops only the mouse,
so `Ctrl+O`, or Space on the focused `Open…` button, started a second `MainWindow.OpenPairAsync`
while the first awaited its decode; the second file dialog opened over the load, the first
load's overlay came down while the second still decoded, and at start-up, cancelling the second
dialog closed the window under the first load. A second request can be ignored, queued until the
first ends, or made to cancel the first and start again. The plan approved on 2026-10-22 named
the first, and the owner did not object.

## Decision

- While a pair is being opened, from its file dialogs to the pair on screen or the message of
  R-005, `OpenPairAsync` returns at once when called again: `Ctrl+O` and `Open…` do nothing
  (R-006, R-063). The field `_opening`, set for the length of a call, decides it.

## Alternatives rejected

- **Queueing the second open until the first ends.** The user pressed the key during a load of
  a few seconds whose end they are watching; a dialog that opens by itself afterwards is a
  surprise, not an answer.
- **Cancelling the first load and starting the second.** `MainWindow` passes no cancellation
  token, and a load stopped halfway would have to give back its memory before the next starts;
  a second `Ctrl+O` a moment later is cheap by comparison.

## Consequences

- A key pressed during a load is lost, as it is during any modal dialog of the application.
- Guards: `MainWindowTests.R006_ASecondOpenWhileAPairLoads_AsksForNothing` (through the button;
  the `Ctrl+O` path reaches the same method and is read, not run).
