# 0038. The top bar's inputs explain themselves

Date: 2026-11-04
Status: accepted 2026-11-04

## Context

The window's top bar has four inputs the files do not carry: the pixel size, the threads, and
the SynScan arrows for east and north (R-029, R-041). Each had a short tooltip on its label only,
not on the field, so a user who pointed at the field saw nothing. The pixel tip gave the order
of the sources wrong: it read as if a typed size came first, while a size written in the files
(`XPIXSZ`) wins over a typed one (R-066). It also named the method (`XPIXSZ`, `focala echivalenta
35 mm`) and said 3.92 for a blank field, though a D3300 raw gives about 4.0. The threads tip said
`half the cores` without the number, and the arrow tips did not say that the choice changes no
figure. The owner asked on 2026-11-04 for tips that say what to type and what the defaults are,
and approved the four proposals in chat the same day.

## Decision

Each input's group, its label and its field, carries the tip, shown also while a run disables
the fields (R-083). The tips say what to type, what it is for, and what is used when the field
stays as it is: for the pixel, the raw's figure, about 4.0 on a D3300, else 3.92, and only when
the files do not carry one; for the threads, this computer's count, half its cores; for the
arrows, the default arrow, that the choice only names the dither moves, and that it is kept.
Their figures come from the core: `SeriesOptions.D3300PixelUm`, the new `D3300RawPixelUm` (a
text's figure only, no run computes with it), `Cores` and `DefaultThreads`, which the run now
reads as well, and `ArrowKeys.Default`. The texts are `WindowTexts`'s, read once when the window
is built.

After seeing the window, the owner asked the same day for the defaults to be in the fields
themselves, and chose, among three ways put to them, the value written in the field: the threads
field starts with `DefaultThreads`, 4 on an 8-core computer, which runs exactly as the blank field
did. The pixel field stays blank: a typed size wins over the raw's (R-066), so writing 3.92 there
would give a D3300 raw session the spec-sheet scale instead of its raw's 4.0, and its tip says
what is used. The arrows' selectors already show their default. The owner also asked for the
Romanian label `FIRE` to read `THREADURI`.

The arrow tips name the Dithering tab beside the Frames tab, which the approved wording left out:
the Dithering tab's bars also show the arrows (R-039).

## Alternatives rejected

- **A grey hint in the empty fields** (the recommendation put to the owner): the default in sight
  without a value typed, for the pixel field as well. The owner chose the written value.
- **3.92 written in the pixel field.** It would win over the raw's figure and move the scale.
- **The tips stay on the labels.** The label is a few letters of faint text above the field;
  the field is where the pointer goes.
- **`half the cores` without the number.** The user does not know what the computer counts as a
  core; the number is the one the run will use.

## Consequences

- `R083_TheTopBarInputsSayWhatToTypeAndTheirDefaults` checks each group's tip, its default, that
  it shows while disabled, and that it is in plain words, in both languages.
- `R083_TheThreadsInputStartsWithThisComputersDefault` checks the threads field's start, and the
  window test that the label reads `THREADURI` in Romanian.
- The window's log line of a run's start names the count, `threads=4`, where a blank field gave
  `threads=default`; `R071_ASessionRunLogsItsStartAndItsOutcome` expects the count, at the
  owner's word the same day.
- The console's `--help` and the README are unchanged.
