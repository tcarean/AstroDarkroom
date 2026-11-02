# Merge the Structure tab's two scales into one

## Context

Approved in chat on 2026-11-02. The Structure tab shows two scales with the same four bands:
`SCARA CELOR CINCI TESTE` (2 / 10 / 20 %, the five tests' verdict marked) and, inside the
vignetting card, `SCARA ADANCIMII (in stopuri)` (0.03 / 0.14 / 0.26 stops, the depth's verdict
marked). The stops are the same thresholds translated (ADR 0031: not a new threshold), yet the tab
marks two rows in two tables, often in different bands (the test render: PERFECT below, REFA
above). The owner asked for one table; the proposal (facts, mockup, window only) got "Da, fă-l".
Approving this plan also accepts the requirement text below.

## Requirement R-126 (new, `accepted`, `implemented` at the end)

The Structure tab shows one verdict scale, `THE VERDICT SCALE` (`SCARA VERDICTELOR`), below the
vignetting card: the four bands (`PERFECT`, `BUN`, `ACCEPTABIL`, `REFA`), each with its interval
for the five tests in percent (`Th.ResidPerfect` / `ResidUsable` / `ResidCheck`), its interval
for the depth against the lens profile in stops (the same thresholds through
`Stats.StopsForError`) and its meaning as the console prints it. The stops column, its heading
and, under the table, the sentence that it is the five tests' scale translated into stops and not
a new threshold are on screen only when the vignetting was compared; otherwise the table has the
percent column alone. The cell of the band each verdict fell in is marked, not the row: the
percent cell of the five tests' verdict, the stops cell of the depth's. The vignetting card has no
scale of its own; it keeps what the End to end test would read. The console's report and the
saved `Test result.txt` do not change. Example: the five tests `PERFECT` and the depth `REFA`
(-0.49 stops against the nearest profile) mark `sub 2 %` on the `PERFECT` row and
`peste 0.26 stopuri` on the `REFA` row of the one table; with the box `I didn't use any filters on
my lens` unticked the table has no stops column.

## Decision: ADR 0088, accepted 2026-11-02

One table in place of the five tests' scale; the stops column only when compared; each verdict's
cell marked; the meanings are the five tests' texts, unchanged (BUN's "background extraction
removes what is left" holds for the depth too: the "How to read" guide already says it for any
shape); the depth's own four meanings go (the corner error is the percent column; the ACCEPTABIL
clause on the profile's uncertainty is said by `DepthAdvice` and `DepthLightScaleNote`); window
only, the console keeps its one-line depth scale. Rejected: two tables with a sentence saying
they are one; a second meaning column; marked rows; the stops column always on; the console too.

## Changes, in order

1. **Docs first:** R-126 in `docs/requirements.md`; ADR 0088; this plan saved as
   `docs/plans/2026-11-02-merge-the-structure-tab-scales.md`.
2. **Tests first (they fail to compile until step 4; the output is pasted):**
   - `FlatCheck.Wpf.Tests/StructureTabTests.cs`, new `R126_` tests, built through `Populate`
     on `StructureRun.Compare` with `LensProfileTestData` (as `WindowSmokeTests` does) and through
     a real run for the box: with a comparison, four rows whose stops are
     `StructureReport.DepthScaleRows()`, one percent cell marked on the five tests' verdict and one
     stops cell on the nearest profile's verdict (different rows on this fixture), the depth note
     under the table and not in the card; without a comparison, no stops anywhere and no note;
     the box unticked removes the column, ticked brings it back; `Reset` clears it.
   - Existing tests that describe the old layout change with it, reported at wrap-up:
     the four `ProfileScale` / `ProfileScaleNote` lines of
     `WithARawBesideTheMasterTheVignettingIsComparedWithTheLensProfiles` become the same checks on
     the merged `Scale`; `TheDepthScaleRowsAreTheResidualScaleInStops` drops its `Meaning` line;
     `R122_TheReportsExplanationsAvoidTheMethodsVocabulary` counts 28 texts instead of 32 (the four
     depth meanings are gone); a comment in `TheScaleHasFourBandsAndMarksTheCurrentOne`.
3. **Core:** `StructureReport.DepthScaleRows()` returns `(Band, Range)` only; doc comments of it
   and of `DepthLightScaleNote`. No figure moves, so no `/validate`.
4. **Window:**
   - `ViewModels/Rows.cs` `ScaleRow`: `DepthRange`, `IsDepthCurrent`, `HasDepth`.
   - `ViewModels/StructureTabViewModel.cs`: one helper fills `Scale` from `StructureReport.Scale()`
     and, when compared, `DepthScaleRows()`; `ScaleHasDepth`, `ScaleDepthNote`
     (`DepthScaleNote`), `ProfileLightNote` (`DepthLightScaleNote`) replace `ProfileScale` and
     `ProfileScaleNote`; `Reset` clears them.
   - `Theme/Dark.xaml` `ScaleRowTemplate` (only this tab uses it): the pill, then the percent and
     stops cells in `SharedSizeGroup` columns, the stops cell collapsed without a depth, a marked
     cell drawn with today's row background and bright semibold text, then the meaning.
   - `Views/StructureView.xaml`: the card loses its scale; the merged card has the heading, a
     header row (`cele cinci teste`, `adancimea fata de profil`, `ce inseamna`, the middle one
     only with a depth) in the same size scope, the rows, the depth note, the residual paragraph.
5. **Docs:** `README.md` (Structure tab), `docs/design/two-applications.md`, `lens-profiles.md`
   section 11, `modes-structure.md`, `code-map.md`; CHANGELOG under Changed.

## Verification

- The `R126_` tests and the changed tests green; `WindowSmokeTests` (binding errors asserted)
  green; the tab rendered with `FLATCHECK_DUMP_SCREENS` (compared, without raw, filter, the two
  lens-picker states) and sent to the owner.
- No golden file changes: the golden tests pass untouched.
- A `--no-incremental -warnaserror` build, then `scripts/verify.ps1` green before the commit.
- A subagent code review of `git diff main...HEAD` offered before the merge proposal.

## What deviated

Noted at the wrap-up of 2026-11-02, before the commit.

- The requirement and this plan were approved together, in plan mode, instead of the requirement
  first: the owner had already approved the design and its mockup in chat. The tests were written
  after that approval and before the code, and failed to compile until step 4.
- One more test than listed: `WindowSmokeTests.R126_TheOneScaleDrawsTheStopsBesideThePercentOnlyWhenCompared`
  checks the rendered table itself (each interval in stops drawn once, under the card, on its
  percent's line and under its heading; the marked cells; the stops heading collapsed without a
  comparison).
- `/validate` was run after all: the wrap-up counts any edit in `Report/` as numeric code. The 36
  cases came out equal to the recorded figures (`docs/validation.md`, History, 2026-11-02).
- The marked cell is drawn with the background the marked row had, in bright semibold text, not
  with the blue tint of the chat mockup: the scale keeps the mark it always had, now on a cell.
- After the commit, a subagent's code review found no code bug and five smaller points. Four were
  fixed in a7a6fe14: ADR 0088's line on `/validate`, R-126's example named as the test fixture,
  `language.md`'s plural, and stronger render checks (the card's real last line, the stops
  sentence's place, the zero-width stops cells, the no-raw case with a catalog). On the fifth the
  owner chose to move the paragraph on why the residual matters to the foot of the tests card:
  under the one scale its "the most important one" read as outranking the depth. R-126 and ADR
  0088 say so, and the render test checks its place.
