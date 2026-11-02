# 0088. The Structure tab shows one verdict scale, the depth's stops in a column beside the five tests' percent, each verdict's cell marked

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

The Structure tab showed two scales with the same four bands. `SCARA CELOR CINCI TESTE`, under
the vignetting card, gave 2 / 10 / 20 % and marked the row of the five tests' verdict. Inside
the card, `SCARA ADANCIMII (in stopuri)` gave 0.03 / 0.14 / 0.26 stops and marked the row of the
depth's verdict, with a sentence saying it was the five tests' scale translated, not a new
threshold (ADR 0031, written after the owner asked whether it was a second scale). The two
verdicts often fall in different bands: the test render marks PERFECT in one table and REFA in
the other. On 2026-11-02 the owner asked for the two to be merged into one table; the proposal,
with a mockup and the window alone, got the owner's yes.

## Decision

One table, `THE VERDICT SCALE` (`SCARA VERDICTELOR`), where the five tests' scale stood: per
band the pill, the five tests' interval in percent, the depth's interval in stops, the meaning.
The stops column, its heading and `StructureReport.DepthScaleNote` under the table are there only
when the vignetting was compared. The cell each verdict fell in is marked, not the row. The
meanings are the five tests' texts, unchanged: BUN's "background extraction removes what is
left" holds for the depth too, as the "How to read" guide already says for any shape. The depth
scale's own four meanings go: the corner error they named is the percent column itself, and the
ACCEPTABIL clause on the profile's uncertainty is said by `DepthAdvice` and
`DepthLightScaleNote`, which stays in the card. The console keeps its two scales.

## Alternatives rejected

- **Two tables, each saying it is the same scale.** What the tab did already; the owner asked for
  one.
- **A second meaning column for the depth.** Its texts repeat the percent column ("corner error
  under 2 %").
- **Marked rows.** With two verdicts in one table, two marked rows do not say which is which.
- **The stops column always on screen.** Without a comparison (no raw in `flats/`, a filter on the
  lens) a column of bands for a depth nobody judged reads as a result.
- **The console merged too.** Its depth scale is already one line inside the vignetting block;
  moving it would reorder the report and its golden files for no gain to its reader.

## Consequences

- R-126. `StructureReport.DepthScaleRows` returns the band and its interval in stops only, so
  R-122's list of texts holds 28 instead of 32. `ScaleRow` gains `DepthRange` and
  `IsDepthCurrent`; `StructureTabViewModel.ScaleHasDepth`, `ScaleDepthNote` and
  `ProfileLightNote` replace `ProfileScale` and `ProfileScaleNote`; `ScaleRowTemplate` marks
  cells, its two interval columns lined up with a header row by shared sizes.
- No golden file moves and no figure moves: `/validate` is not re-run.
- Guards: the `R126_` tests in `StructureTabTests`, the scale checks in
  `WithARawBesideTheMasterTheVignettingIsComparedWithTheLensProfiles` and
  `TheDepthScaleRowsAreTheResidualScaleInStops`, the renders of `WindowSmokeTests` with their
  binding-error check.
- Source notes: `docs/design/two-applications.md`, `lens-profiles.md` section 11,
  `modes-structure.md`.
