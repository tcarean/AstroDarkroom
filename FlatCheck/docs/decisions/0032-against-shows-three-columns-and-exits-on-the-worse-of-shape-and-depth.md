# 0032. --against shows three columns (A, B, A/B) and exits on the worse of shape and depth

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-synthetic.md ("--against") and
docs/design/lens-profiles.md (section 7); the decision itself dates from 2026-09-17/18. The
first version of `--against` showed the structure battery on the ratio A / B alone, and
"vignetare -0.42 stopuri" could not be read without knowing that the master has 0.27 stops and
the synthetic 0.69; the user asked for the three values side by side. The verdict was the shape
alone - the five tests on the ratio - and the sky master, 0.42 stops shallower than the model
("clearly enormous" to the user, 34 % in the corner), came out ACCEPTABIL.

## Decision

`AgainstAnalyzer` prepares both frames, normalises each to median 1, and runs
`StructureAnalyzer.AnalyzePrepared` on all three: A, B and A / B. `AgainstReport.Rows` prints
eight rows in three columns - vignetting in stops, illumination offset, local spot, then the five
tests - with a note per row written about the difference. The verdict of the difference has two
parts and the exit code follows the worse: the shape (the five tests on the ratio) and the depth,
`2^|D| - 1` through the unchanged residual scale (`AgainstResult.DepthErrorOf`; PERFECT under
0.03 stops, BUN 0.14, ACCEPTABIL 0.26, else REFA, derived from `Th.Resid*` by `StopsFor`). The
depth row is the only one with a verdict in the difference column (the owner's decision). The
window shows the rows as `CompareRow`: label and note on the left in the one elastic column, the
three values in shared-size `Auto` columns packed on the right (the owner's decision).

## Alternatives rejected

- **The battery on the ratio alone.** Unreadable without the two absolute depths.
- **Shape verdict only.** The sky master read ACCEPTABIL at −0.42 stops.
- **A verdict chip on every row of the difference column.** "PERFECT next to 0.11 % on a ratio"
  said nothing the shape verdict at the top does not.
- **Values given the width, note running under them (the first window cut).** Replaced by the
  packed-right layout.
- **The structure tab's row notes reused.** They said "a property of the lens, not an error"
  under a row that here *is* the error; `AgainstReport.TestNote` is written about the difference.

## Consequences

- Acceptance figures on the Tokina masters against the Adobe synthetic (2026-09-18, camera crop
  1.523, corner): monitor close +0.19 stops (13.8 %, ACCEPTABIL), sky −0.39 (31.5 %, REFA),
  on-monitor +0.12 (8.3 %, BUN); Adobe vs lensfun 7D2 entry −0.18 with a PERFECT ratio, vs the
  D7100 entry −0.25 (both ACCEPTABIL). Expected within ±0.02 stops when re-run.
- The row's note states that a BUN / ACCEPTABIL depth is within what the two databases disagree
  by (~0.2 stops).
- The golden `against_radial` pair is 0.30 stops apart, hence REFA and exit 3 there.
- The four depth rows of the written flat have their own `DepthRowTemplate` in
  `SyntheticView.xaml`, because the shared `MetricRowTemplate` runs the note under the value.
- Not `--ratio`, which separates camera-fixed from world-fixed by rotation; the report says so.
- Guards: `SyntheticFlatTests` (two correct flats give PERFECT and 0.00 stops; a flat against
  `flat²` is PERFECT on shape, REFA on depth at 0.7 stops), `SyntheticRunTests`, the golden
  `against_radial`, `SyntheticTabTests` and two smoke renders.
- Source notes: docs/design/modes-synthetic.md, docs/design/lens-profiles.md; memory note
  flatcheck-synthetic-flat.
