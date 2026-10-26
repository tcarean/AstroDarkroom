# Flip the WCS against the lights only when the lights are raws

Branch `claude/Flip-the-WCS-only-for-raw-lights`, worktree
`.claude/worktrees/Flip-the-WCS-only-for-raw-lights`.

## Context

ADR 0013 maps a dither's star shift to the sky through the solved FITS's WCS applied to
`(-dx, +dy)`, the y reversed because camera raws are read top-down while Siril stores its FITS
bottom-up; measured on raws. `SessionFolder.FramesIn` also accepts a folder of FITS lights when
it holds no raws, and those are read in the file's order, the same as the solve's, so the
reversal was wrong for them: the sense the y axis feeds (RA on the owner's frames, where RA runs
along Y) came out the other way round, and `Wcs.Handedness` with it. Found on 2026-10-26 while
surveying the repo's FITS readers after the `ROWORDER` findings (ImageVisualCompare ADR 0039,
AstroPhotoAnalyzer ADR 0049); the owner asked for the fix in this session.

## Steps

1. R-038 reworded; the session test `R038_TheFrameTableAndTheCsvCarryTheMoveOnTheSky`, whose
   fixture lights are FITS, given the solve a FITS session would carry (the y column mirrored),
   so it fails on the old rule; the same solve in `R050_TheRowsAndTheSummaryWord...` and in
   `LanguageTests`; a unit test for `Wcs.FramesFlipped = false` and the handedness.
2. `Wcs.FramesFlipped` (default true, the raws' measured case) and `Wcs.PointingOffsetDeg`,
   used by the moves, the drift and `Handedness`; `SessionFolder.LightsAreRaws`; `SeriesRun`
   sets the flag.
3. The measurement: the Veil session run from its raws and from a folder of hard links to its
   `process/pp_light_*.fit` with the solved stack at the root, before and after the fix; the
   `move_ra_px` and `move_dec_px` columns compared.
4. ADR 0023 (supersedes ADR 0013 in part), the design note, the README, the CHANGELOG; the gate;
   `/validate` on the sessions (the rule "Measured on the sessions").

## Outcome

Three commits on the branch: the flip (cfc49a0), the CSV quoting (95d7ed8), the review's six
findings (fe937e8). Deviations from the steps above:

- Two more fixtures carried the Veil solve than step 1 listed, R-040's console test and R-041's
  window test; found by the gate, mirrored the same way.
- The measurement of step 3 turned up a second defect, the CSV rows of a FITS session four
  cells longer than the header (the `CFA[row,col]` channel names, bare), which the owner asked
  to fix in the same task: its own commit, R-028, no ADR.
- The validation run (13 of 13, no figure moved) is recorded as a dated line in
  `docs/validation.md`; the FITS copy of the Veil session was a temporary folder of hard links,
  deleted after the measurement, not a case.
- An independent review found no defect; its six remarks (an ADR line that contradicted the
  second commit, the validation line above, the session note, two formattings, `Cell` on the
  class and trail cells too) are the third commit.
