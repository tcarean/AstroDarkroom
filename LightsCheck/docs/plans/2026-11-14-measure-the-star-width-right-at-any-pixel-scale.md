# Measure the stars' width right at any focal length and pixel scale

## Context

LightsCheck takes a star's width (FWHM), elongation and angle from second moments inside a
fixed circle of 6 px, on values cut at zero (`StarShapes.Measure` in
`src/LightsCheck.Core/Analysis/StarShape.cs`, `Th.MomentRadius`, R-014). The 6 is the default
`rad=6` of `prototype/analyze.py`, written for stars of about 3 px at 135 mm and never compared
with anything else. It is right up to about 4 px and too narrow above (5 px reads 4.82, 7 reads
5.89, 10 reads 6.52), so on a session of wide stars the frames read closer to one another than
they are and a frame out of focus passes the 1.3 x test. ADR 0052 (2026-11-14) kept the window,
because with today's moments a wider or a star-following circle reads the sky's noise and the
neighbours, and added the `wide stars` row (R-088, `Th.WideStarFwhm` 4.5 px). The owner now
wants the width measured right at any scale: another estimator, chosen after a measurement.

The task has two stages with the owner's decision between them. **Stage 1 changes nothing in
the repository's code**; stage 2 starts only after the owner has chosen.

Branch `claude/Measure-the-star-width-right-at-any-pixel-scale`, worktree
`LightsCheck/.claude/worktrees/Measure-the-star-width-right-at-any-pixel-scale`, made from
`main` in `E:\Photografie\Code`. This plan is saved there as
`docs/plans/2026-11-14-measure-the-star-width-right-at-any-pixel-scale.md` and committed first.

## Stage 1: measure the candidates (code outside the repository)

### The harness

A C# console project in the session's scratchpad (`fwhm-bench/`), never copied into the
repository. It compiles the worktree's `src/LightsCheck.Core/**/*.cs` by link, so it runs the
tool's own pipeline (decode, green, background, detection, the choice of the 400 stars) and
every candidate gets **the same stars on the same residual raster**. `Analysis/StarShape.cs` is
left out of the link and replaced by a scratch copy with a switch for the estimator, so a whole
`SeriesRun` can also run with a candidate. Its packages are those of `Directory.Packages.props`,
same versions; `bin/` and `obj/` stay in the scratchpad. Release build, timing in-process
(E: is a hard disk; decode time is kept out of the figures). The 2D fit is hand-written
(Levenberg-Marquardt); no new package. `prototype/` is not touched.

### The candidates

| | Estimator | Variants measured |
| --- | --- | --- |
| O | Today's: moments cut at zero, circle of 6 px | the baseline |
| A | Moments **not** cut at zero, local background from a ring around the star taken out | circle of 6 px; a circle that follows the star (1.5 x and 2 x the width read) |
| B | Adaptive moments with a Gaussian weight that follows the star (size, elongation and angle), iterated until it settles | start from the component's moments; with and without the ring background |
| C | Width at half height from the radial profile around the centre | elongation and angle from the half-height outline, and from the profile along the two axes of B |
| D | Fit of a 2D elliptical Gaussian with its own background | Gaussian; a Moffat fit as a second variant, since real stars have wings |

Width, elongation and angle come from the same measurement in each.

### Synthetic stars (the true width is known)

- Profiles: Gaussian, Moffat with beta 2.5 and 4. Widths 2, 3, 4, 5, 7, 10, 15 px. Drawn by
  integrating over each pixel, centres at random fractions of a pixel.
- Noise: none; then the sky noise of a Veil frame (sigma 14 ADU) with star peaks at 20, 50 and
  200 sigma (20 is the faintest star the tool measures). 200 stars per cell.
- Elongated: ratios 1.3, 1.6 and 2.0 at 0, 30 and 80 degrees, minor width 3, 5 and 10 px; and a
  doubled star (two cores a few px apart), as frames 17 and 23 of the Veil session are.
- Whole synthetic frames through `FrameAnalyzer.Analyze` (mosaic, green interpolation,
  background, detection), one per width: what the frame's figures read, and **how many of the
  drawn stars reach the estimator at all**. The detection has limits in pixels of its own
  (`Th.MaxStarArea` 400 px, `Th.TrailMinArea` 250 px, the halo's rings of 3, 5 and 10 px); a
  bright star of 10 px or more may be dropped before any estimator sees it. Stage 1 measures
  where that starts; whether to move those limits too is put to the owner with the table.

### Real frames (no true width; same stars for every candidate)

- `Veil/2026-10-06-3h`: frames 1, 4, 17, 23, 72, 83, the six frame cases V-002 to V-007.
- `Veil/2026-11-06`: `DSC_0001`, `DSC_0050`. `Milkyway-Core/2026-08-12-Smida`: `DSC_0002`,
  `DSC_0020`. (The frames ADR 0052 read.)
- Stars of known width added into two real frames (one Veil, one Smida), at random places and
  peaks like the measured stars', and read back: the real sky's noise and real neighbours, with
  a known answer. This is the case that defeated the wider circle in ADR 0052.

### What is reported, per candidate

1. The reading against the true width, per profile and width, without and with noise.
2. The spread from star to star (robust), and whether faint and bright stars read the same.
3. Real frames: median and 90th percentile of the width, elongation, share elongated, angle and
   coherence, centre and edge width, stars kept, against today's.
4. Elongated and doubled stars: ratio and angle read against drawn.
5. Time per frame for the 400 stars, against today's and against the about 4.7 s of a frame.
6. How far each figure of `docs/validation.json` moves:
   - V-002 to V-007: `fwhm`, `fwhmP90`, `elongation`, `elongatedFraction`, `angle`,
     `coherence`, `halo`, `fwhmCentre`, `fwhmEdge`, for every candidate.
   - V-008, V-013, V-014 (whole sessions, about 15 minutes for the three): `sessionFwhm`, the
     class counts and the bad frames, and every figure and word that follows from the shapes
     (focus held, dew, the polar verdict, the pattern's three-star-widths rule, the RA axis,
     V-009's words), for the one or two candidates worth recommending.

### The checkpoint

The table, the options and one recommendation, in chat, in plain words. With it: the user texts
that would change, original and proposal side by side (the `wide stars` row and the README's
sentence); what stays unmeasurable; whether the detection's pixel limits should move in this
task; whether the change seems to warrant a version bump (the owner's call). Then the session
**waits for the owner's answer**. No figure of `docs/validation.json` is touched.

## Stage 2: after the owner's choice

1. **Requirement.** R-014 rewritten for the chosen estimator, its example a wide synthetic star
   read within the tolerance the table supports. R-088 removed, or rewritten for what stays
   unmeasurable, as the owner decided.
2. **Failing test first.** A theory in `tests/LightsCheck.Tests/StarShapeTests.cs`: Gaussians of
   5, 7, 10 and 15 px read within tolerance (today 4.82, 5.89, 6.52, 6.75: red), with noise as
   well, and an elongated wide star for ratio and angle. The test
   `R014_TheWidthIsRightUpToFourPixelsAndTooNarrowAbove` pins the limit this task removes; it is
   replaced because the requirement changes, at the owner's approval of this plan. The R-088
   tests in `SeriesTests.cs` follow R-088's fate the same way.
3. **Code**, all in `LightsCheck.Core`: `Analysis/StarShape.cs` (the estimator),
   `Thresholds.cs` (`MomentRadius` and `MomentIterations` give way to the new constants;
   `WideStarFwhm` goes or stays with R-088), the shape loop of `FrameAnalyzer.Analyze`,
   `Report/SeriesReport.cs` (the row), the doc comments of `StarShape` and `FrameMeasurement`.
   XML documentation on everything added or edited.
4. **Golden files** regenerated (`LIGHTSCHECK_UPDATE_GOLDEN=1`, `--filter GoldenOutputTests`);
   every changed line read and tied to the change.
5. **Documents.** `docs/design/frame-analysis.md` (step 7, the constants, what was measured);
   `series-analysis.md` and `window.md` where they quote the row. A new ADR with the tables,
   superseding ADR 0052 in part; dated notes in ADR 0052, in ADR 0006 (the C# no longer equals
   the frozen prototype on the shapes) and in any ADR that states a width that moves (ADR 0050's
   12.5 px).
6. **`/validate`** on the three sessions, the drift table pasted. Figures of
   `docs/validation.json` are re-recorded only at the owner's word, with a History entry in
   `docs/validation.md`.
7. **CHANGELOG** (`Changed`), **README** (the limit's sentence, the sample output if it moves),
   the **wiki** page `LightsCheck.md` in `E:\Photografie\AstroDarkroomWiki` (a separate
   repository, on a branch of its own; the branch pending there from the last task is still
   unmerged, so its base is asked then).
8. **`/wrap-up`**, a subagent's review of the branch and one of the fix commit, then the merge
   is proposed and asked. No merge and no push without the owner's word.

## Verification

- Stage 1: the harness reproduces today's readings first (clean Gaussians 3.00 / 3.98 / 4.82 /
  5.89 / 6.52 / 6.75 and the six frames' recorded widths, 3.85, 3.06, 4.92, 4.88, 3.03, 3.06)
  before any candidate is trusted.
- Stage 2: the new tests red before the code and green after; `scripts/verify.ps1` green
  (`--no-incremental -warnaserror` build before the last run); the golden diff read whole;
  `/validate` with the drift table; a run of the console and of the window on one real session.
