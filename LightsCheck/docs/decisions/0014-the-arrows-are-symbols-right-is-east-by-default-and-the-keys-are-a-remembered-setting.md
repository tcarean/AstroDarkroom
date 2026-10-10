# 0014. The arrows are symbols, right is east by default, and the keys are a setting the window remembers

Date: 2026-10-10
Status: accepted 2026-10-10
Supersedes: 0013 in part (the SynScan default keys and the decision to take no input for them)

## Context

ADR 0013 took the SynScan arrow mapping from a forum post the owner pasted (left east, right
west, up north, down south) and decided against an input for it. Reading the Veil session
with it, the owner found the left/right words reversed for their own app: on their SynScan
the right arrow moves the mount east. The app's direction keys are a setting, so no default
fits every installation. The owner also found the words (`V (dreapta)`) long in a cell that
already carries the axis, the pixels and the sense, and asked for symbols and for the mapping
to be configurable.

## Decision

The arrows are the symbols `←`, `→`, `↑`, `↓`, written after the pixels and before the sense
letter: `RA 15 px ← V, DEC 12 px ↓ S`. Which arrow moves the mount east and which north are
two values of a setting, `ArrowKeys`, the other two following; the default is the owner's app,
`→` east and `↑` north. The console takes them as `--est stanga|dreapta` and `--nord sus|jos`
and prints UTF-8 so the symbols survive the console; the window has two selectors in the top
bar and remembers them in `%LOCALAPPDATA%\LightsCheck\settings.json`, the program's first and
only stored setting, read at start and written on change, defaults when absent or unreadable.
The sense letter stays in the cell because it is measured, while the arrow depends on the
setting.

## Alternatives rejected

- **Four independent fields, one per direction.** Two fields cannot express an impossible
  mapping (east and west on the same arrow); four would need validation for nothing gained.
- **Words instead of symbols** (`stanga`, `dreapta`). Longer, and the owner reads the arrow on
  the hand controller, not a word.
- **ASCII arrows in the console** (`<-`, `->`, `^`, `v`) with symbols in the window. The Raport
  tab would no longer be the console's text byte for byte (R-030).
- **Keeping the setting per session, not remembered.** The owner said once that the keys are
  set once; a selector reset at every start would be typed every time.

## Consequences

The console's `Main` sets the output encoding to UTF-8; the goldens change where a signed
cell appears, and `help.txt` gains two lines. V-011's cells are re-measured in the new form
(the figures do not move). `R040` pins the symbols, the default and the console options,
`R041` the selectors and the settings file through a path the tests redirect.
