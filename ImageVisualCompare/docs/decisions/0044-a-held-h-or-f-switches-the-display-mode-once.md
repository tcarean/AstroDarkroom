# 0044. A held H or F switches the display mode once

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

The keys `H` and `F` switch the display mode of a pair shown as stored, each switch decoding both
files again (R-113, R-114, ADR 0040). A key held down repeats some thirty times a second. While a
switch runs, its repeats did nothing, but every repeat that came after a switch had ended started
the next one: on a FITS pair a held `H` went from `Histogram` to `Linear` and back for as long as
it was held, a full decode of the pair each time. The second review of the diagnostic log's branch
found it on 2026-10-30, while it checked what a held key writes in the log (ADR 0043); the owner
chose the same day to have it fixed in that branch.

## Decision

- A repeat of a held `H` or `F` does nothing: only the key's first press switches the mode. A
  held key therefore switches once and writes one line in the log, whatever the pair.
- `MainWindow.OnModeKeyAsync` takes a press and whether it is a repeat, and passes only a first
  press on to `ToggleModeAsync`; the key handler calls it with `KeyEventArgs.IsRepeat`. It is
  separate from the handler so a test can give it a repeat, which a `KeyEventArgs` built in a test
  cannot be.

## Alternatives rejected

- **Leave it.** A held key would keep decoding the pair, two files of 264 MiB each on the real
  FITS pair, for nothing anyone asked.
- **Ignore a repeat only while a switch runs.** That is what the code did; the repeats after a
  switch had ended are the ones that switched back.
- **Ignore every repeated key of the window.** `+`, `−` and the arrows are meant to repeat, and
  `Ctrl+O` while a pair opens is already ignored by the open itself (R-006).

## Consequences

- R-114 says it, and `MainWindowTests.R114_AHeldModeKeySwitchesOnce_ItsRepeatsDoNothing` fails
  on the code before (the repeats switched back to `Linear` and on again).
- The README's controls table says that a held `H` or `F` switches once.
