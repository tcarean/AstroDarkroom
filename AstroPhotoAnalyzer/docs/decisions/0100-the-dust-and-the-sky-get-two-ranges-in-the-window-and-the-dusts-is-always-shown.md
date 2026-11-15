# 0100. The dust and the sky get two ranges, in the window only, and the dust zone's is always shown

Date: 2026-11-15
Status: accepted 2026-11-15

## Context

The Tonuri tab gives the dust and the sky as one L\* each (R-044), and the owner asked how wide
each zone is around its figure: a dropdown above the simulation table, off by default, that
puts a range in brackets beside the two figures on every row. Three things had more than one
reasonable answer: which ranges, where they are shown, and what the dust's bracket holds when
the dust's figure does not come from the dust class.

### Measured on 2026-11-15

A scratchpad probe over the core, after ADR 0099, each image as it is, patch L\*:

| image | dust figure, from | dust class: min, 10 %, 25 %, 75 %, 90 %, max | sky figure | sky set: min, 10 %, 25 %, 75 %, 90 %, max |
| --- | --- | --- | --- | --- |
| `starless_rbf0.5_stretch0.17.fit` | 7.7, the class (8.9 % of the patches) | 0.8, 5.8, 7.7, 12.9, 14.9, 17.6 | 17.6 | 7.4, 14.0, 15.4, 19.0, 20.0, 27.9 |
| `de_printat.png` | 4.4, the darkest sky (class 4.2 %) | 0.4, 3.1, 4.9, 7.6, 8.5, 9.0 | 9.1 | 3.5, 6.2, 7.8, 10.8, 14.5, 30.2 |
| `Rosetta_starless_no_WCS.fit` | 6.1, the darkest sky (class 1.3 %) | 6.1, 6.6, 7.1, 7.8, 8.0, 8.1 | 8.2 | 5.2, 6.8, 7.4, 9.8, 14.3, 31.1 |
| `Veil_Starless.fit` | 6.4, the darkest sky (class 0.8 %) | 4.8, 5.5, 5.8, 7.0, 7.4, 7.9 | 9.3 | 5.4, 7.8, 8.6, 10.1, 10.9, 13.3 |

- The two ends of the whole spread are single patches: the sky's lightest patch is 30 L\* on
  two frames whose 90 % percentile is 14.
- On every frame measured here whose dust figure is the darkest sky, the reason is the class's
  size (under `DustClassMinFraction`), and the figure sits under the class's middle half on two
  of the three (`de_printat.png`, Rosetta) and inside it on the third (Veil).

## Decision

The owner chose, on 2026-11-15: two ranges, the middle half (the 25 % to the 75 % percentile,
`RangeHalfCut`) and the range without 10 % at each end (`RangeWideCut`), on every row of the
simulation table, over the patch sets and the lift of the row's own figures; shown in the window
only, by a dropdown whose first choice, `None`, leaves the table as it was; and the dust class's
range shown whenever the class has a patch, also when the dust's figure is the darkest sky, with
a note under the table that says where the figure comes from and why, in the report's words.

## Alternatives rejected

- **No bracket for the dust when its figure comes from the darkest sky.** The first draft. The
  figure never falls outside its bracket, but on those frames the window would no longer say
  where the dust class sits, and the owner asked what a dust "without a range" meant. Put to
  the owner with the figures above; the bracket always shown was chosen.
- **The whole spread, darkest patch to lightest.** One patch decides each end.
- **A console flag and the ranges in the saved report.** The owner asked for a dropdown in the
  tab; the report's table and the goldens stay as they are, and a flag can be added later
  without changing what the core measures.
- **The ranges on the as-is row only.** The owner asked for the table to follow the choice;
  every row's sets are sorted for its two figures already, so the other rows cost nothing.

## Consequences

- `DustAnalyzer.Measure` returns the four ranges with the two figures, `SimulationRow` carries
  them, `ReportWriter` writes the table's header, rows and note for a chosen range, and with
  none writes the text of before, byte for byte. The ranges decide nothing: no mark, verdict,
  target or recommendation reads them.
- `RangeHalfCut` and `RangeWideCut` are in `Thresholds.cs` although no rule reads them: the
  texts interpolate them (ADR 0012).
- On a frame whose dust figure is the darkest sky the figure can stand outside its bracket
  (`de_printat.png`: `4.4 (4.9 - 7.6)`); the note is what explains it, so the note is part of
  the feature and not decoration.
- On every row the dust's figure equals the low end of its middle half whenever the class
  decides, since both are the class's 25 % percentile.
- A row with a range is 28 characters wider. Computed, not rendered at those widths (7 px a
  character, Consolas 12 px in the window's Display mode; the Preview button, the paddings,
  the scroll bar and the 300 px parameter column): under a window of about 1330 px the end of
  the row, the last column and the mark, is cut at the right, where the plain table fits down
  to about 1135 px. The first figures written here, 1270 and 1080, took the 6.6 px of the
  tests' stand-alone render, which is not in Display mode.
- Guarded by the `R046_...` tests in `AstroPhotoAnalyzer.Tests` and the `R047_...` tests in
  `AstroPhotoAnalyzer.Wpf.Tests`; the as-is ranges are recorded in `docs/validation.json`.
