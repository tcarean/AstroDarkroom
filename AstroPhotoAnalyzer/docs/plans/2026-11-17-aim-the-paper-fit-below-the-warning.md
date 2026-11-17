# The paper fit aims at 3.5, under the warning's 4 (R-187, ADR 0109)

## Context

A file fitted to the paper in the Culori pentru hartie tab shows 0.0 % outside in the tool's
Soft proof tab, yet Lightroom Classic's destination gamut warning still marks pixels in it: the
ones the fit landed just under the tool's limit of 4 (3.86 at the median under Lightroom's red).
Measured on 2026-11-17 with trial files on three papers: a fit to 3.5 leaves Lightroom almost
clean on the two papers where it marks anything (WhiteWall 0.31 % to at most 0.005 %,
Hahnemuhle P900 0.04 % to nothing in the capture), for about a fifth more pixels changed. No
limit could be derived from the ICC profile. The owner chose: the fit aims at 3.5, the warning
stays at 4. The measurements and the rejected alternatives are in ADR 0109 (written, accepted
2026-11-17); the requirement is R-187 (draft, accepted by the owner); five `R187_...` tests are
written in `AstroPhotoAnalyzer.Tests/GamutFitTests.cs` and fail to compile on the missing API.

Branch `claude/Aim-the-paper-fit-below-the-warning`, worktree
`AstroPhotoAnalyzer/.claude/worktrees/Aim-the-paper-fit-below-the-warning`.

## One addition to the accepted draft of R-187

The draft said a pixel over 3.5 that no chroma brings within 3.5 "stays as it is". Reading the
code showed a regression in that: at a tone whose grey sits between 3.5 and 4, a pixel the
warning marks is brought inside 4 today, and would be left outside. So: when the warning marks
such a pixel, it is landed on the warning's own edge, as today, and left and counted as a tone
out of reach only where no chroma does that; when the warning does not mark it, it is left and
counted nowhere. R-187's row and ADR 0109 (decision 3) say this; the test
`R187_AToneTheTargetCannotReachIsStillBroughtInsideTheWarning` guards it.

## The change, in order

1. **`AstroPhotoAnalyzer.Core/Thresholds.cs`**: `GamutFitTargetDeltaE = 3.5`, beside the
   paper-fit thresholds, with a comment holding the measurement (the three papers, the cost).
2. **`AstroPhotoAnalyzer.Core/Analysis/GamutAnalyzer.cs`**: `GamutTable.Limit` (default
   `CurveGamutDeltaE`) and `GamutTable.WithLimit(limit)`: the same grid and exact trip, the
   given limit, a cache of its own (the cache holds a yes or no per 8-bit colour, so it cannot
   be shared between limits). `Outside` and `ExactOutside` read `t.Limit` where they read the
   constant. Nothing else in the file changes; the Soft proof tab and the Editare Curbe figure
   keep the table at 4.
3. **`AstroPhotoAnalyzer.Core/Analysis/GamutFit.cs`**:
   - `GamutFitTable.Limit`; `Build(rule, lNodes, hNodes, limit = GamutFitTargetDeltaE)` uses it
     in the four places that compare with the constant (the default judge, the grey, the
     ceiling, the bisection).
   - `GamutFitTable.Warning` (Lab in) and `WarningOfPixel` (the pixel's samples in), null on a
     table without a separate warning (the existing tests), which then counts by its own judge
     exactly as today. A private `Warns(...)` beside `Rejects(...)`.
   - `Apply`: `Decide` (unchanged) says, at the target, whether a pixel is moved and where it
     lands. With a warning set, the facts are the warning's: `before` is its word on the pixel
     as it is, `after` its word on the fitted colour (or on the pixel when it was not moved), a
     grey pixel is counted by it. Where `Decide` answers "outside, no chroma reaches the
     target" (`cmax < 0`): if the warning marks the pixel, the edge is searched with the
     warning (`ExactEdge(t.Warning, ..., c - GamutFitEdgeMargin)`, the existing search) and the
     knee compresses towards it; if that too finds none, the pixel is copied and counted out of
     reach; if the warning does not mark it, it is copied and counted nowhere. This branch runs
     without facts too, so the saved file is the preview's.
   - `Recipe` is not edited: it already reads `Decide`, so it aims at the target.
4. **`AstroPhotoAnalyzer.Core/Runs/GamutFitRun.cs`**: the run keeps `gamut` (4) for the
   warning and makes `gamut.WithLimit(GamutFitTargetDeltaE)` for the fit; the fit's table is
   built at the target with `Judge`/`JudgeOfPixel` on the target's table and
   `Warning`/`WarningOfPixel` on `gamut`. The done line of the log names the target.
5. **`AstroPhotoAnalyzer.Core/Report/GamutFitTexts.cs`** (the two texts the owner accepted):
   `Help` of the hard fit ("...or holds just barely ... a little inside the paper's limit"),
   and a new evidence row after "pixels changed" ("the fit stops a little inside the paper's
   limit (at 3.5, where the warning starts at 4), so more pixels are changed than the warning
   marks, and the gamut warning of other programs (Lightroom, for one) has almost nothing left
   to mark"), both numbers interpolated from `Thresholds`.
6. **Documentation comments** on every member added or edited (the kit's ADR 0035); the file
   headers of `GamutFit.cs` and `GamutAnalyzer.cs` say the two limits.

## Documents, in the same task

- `docs/requirements.md`: R-187 with the addition above, `implemented` at the end; R-175, R-176
  and R-178 amended where they say the fit moves only what the warning marks; their examples
  re-measured (V-016).
- `docs/decisions/0109-...md` (written): decision 3 as above. The `Status:` lines of ADR 0083
  and ADR 0093 get "; ... superseded by 0109" for the parts named (the kit's ADR 0024).
- `docs/design/paper-fit.md` (what the fit aims at, the two judges, the fallback, the trial's
  table), `docs/design/thresholds.md` (the new row), `docs/design/known-limitations.md` (3.5
  comes from one image and two profiles; a few points can remain in Lightroom; on a paper where
  Lightroom marks nothing some colour is given up for no visible gain).
- `README.md`, "Colours for paper": what Exact changes, and that the fit stops a little inside
  the limit so that other programs' warning stays quiet.
- `CHANGELOG.md`, `[Unreleased]`, Changed: one line.
- `docs/plans/2026-11-17-aim-the-paper-fit-below-the-warning.md`: this plan.

## Tests and figures

- The five `R187_...` tests pass; every existing test passes unchanged in its assertions. If an
  existing test's expectation moves because of the intended change (the window's evidence rows,
  say), it is reported with its cause, not adjusted silently.
- `docs/validation.json` V-016: the figures that follow the target (touched, chroma given up,
  after, the recipe) are measured again with `/validate` on the plate-solved FITS through the
  lab's baryta profile, twice (two of its figures differ between runs, CR-074), the drift table
  pasted, and recorded with the date in `docs/validation.md`. This is an intended change of
  those figures; `before` and `dark` must stay as recorded, and every other case must not move.
  Recording them needs the owner's word, asked with the table (the kit's rule "No test edited
  to pass").
- No golden file should change: the console does not run the fit. A changed golden line is a
  finding.

## Verification

1. `dotnet test ... --filter "FullyQualifiedName~R187"`: the failing output now, green after.
2. `scripts/verify.ps1` green (after a `--no-incremental -warnaserror` build, so no warning
   hides behind an incremental build).
3. `/validate`: the drift table, V-016 alone moving.
4. End to end on the owner's image: with the worktree's build, the fit of
   `..._signed.tif` through the WhiteWall profile, `Lina`, saved as a TIFF from a scratchpad
   harness that calls `GamutFitRun.Execute` and `GamutFitExport.Save` (the window's own path),
   compared sample for sample with `..._signed_Soft_limit3.5.tif`, the trial file the owner saw
   clean in Lightroom: expected identical pixels.
5. The window's render of the tab (`APA_DUMP_SCREENS`) read once, for the new evidence row.
6. Read the whole diff back; then a subagent code review of the branch before the merge is
   proposed, and a second one on the fix commit, as on earlier tasks.

## Not in this task

- No control for the user to set the reserve (ADR 0109 keeps it as the next step if a paper
  needs more).
- The version stays as it is; a visible behaviour change, so worth a bump at the owner's word.
- The nine trial files in the owner's `GamutFix` folder are theirs to delete.
