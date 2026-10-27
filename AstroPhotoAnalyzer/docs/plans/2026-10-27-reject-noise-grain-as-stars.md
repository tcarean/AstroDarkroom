# Reject noise grain as stars: a star is at least 12 local sigmas over its base

Branch `claude/Reject-noise-grain-as-stars`, worktree `.claude/worktrees/Reject-noise-grain-as-stars`
(made from the monorepo root `E:\Photografie\Code`, base `main`). The approved plan is saved to
`docs/plans/2026-10-27-reject-noise-grain-as-stars.md`.

## Context

`StarAnalyzer` keeps any window maximum whose amplitude over the border median is at least
3000/65535 and whose half-widths fall in 0.8..15 px. On the starless reference FITS
(`starless_rbf0.5_stretch0.17.fit`) that keeps 6297 "stars" at FWHM 2.38 px, recorded in V-004 on
2026-10-02 without anyone noticing the file's name; the Veil starless frame gives 22 485, the
Rosetta starless 2858. A Python replica reproduced the 6297 exactly and showed what they are:
the grain of the stretched noise (median amplitude 0.058, 3.2 local sigmas; the 3000/65535 floor
sits on the expected maximum of 625 noise samples, 0.044) and knots of nebulosity. The README's
promise (a starless image is judged by the dpi rule, R-074) is therefore never kept on a noisy
image, and the Rezolutie tab and the size table report the grain's size as the real resolution.

Measured on 2026-10-27 on the aligned Rosetta pair (shift 0,0; a detection on the with-stars
frame is a star when the difference image at its peak explains at least half its amplitude):
1218 stars and 2181 grain among 3399 detections; stars have a median SNR (amplitude / 1.4826 x
MAD of the 96 border pixels about their median) of 12.5, grain a median of 3.2 and a p90 of 4.3.
Roundness and a 5 x 5 flux SNR do not separate; the global (whole-image) sigma separates far
worse than the local one. Survivors on the starless frames at k = 8 / 10 / 12 / 15: the 0.17
FITS 42 / 9 / 2 / 0, the 0.1 FITS 52 / 11 / 5 / 0, Rosetta 2 / 1 / 1 / 0, Veil 2 / 0 / 0 / 0,
Orion 0, Andromeda 2 / 0 / 0 / 0. On the with-stars frames at k = 12: Rosetta keeps 621 stars and
1 grain (FWHM median 2.14 against 2.37 for all labelled stars), `de_printat.png` about 65 % of
today's count with the median FWHM within 0.03 px, Andromeda 3037 of 3526, Orion 1372 of 3901
(median 4.12 against 3.51 today, which the grain pulled down).

The owner chose on 2026-10-27: **the threshold is 12 local sigmas**, kept beside the absolute
floor (a denoised image has a sigma near 0; the floor still refuses its tiny bumps), and the
with-stars files are **added to `testdata/manifest.json`** with their hashes.

## Steps

1. **Worktree.** `git worktree add .claude/worktrees/Reject-noise-grain-as-stars -b claude/Reject-noise-grain-as-stars main`
   from `E:\Photografie\Code`; copy the whole `AstroPhotoAnalyzer.Tests/testdata/large/` folder
   into the worktree (the images are git-ignored, ADR 0004); save this plan under `docs/plans/`.
2. **Requirement.** R-070 gains the clause: the amplitude is also at least `StarMinSnr` (12)
   times the border's robust sigma, 1.4826 x the median absolute deviation of the 96 border
   pixels about their median (a sigma of 0, a quantised flat border, leaves the floor alone);
   its examples: the reference image's new count and FWHM, and the starless FITS from 6297 to
   the measured few. R-074's example names the starless FITS as the case that now takes the dpi
   rule. Both stay `implemented` / `accepted` as they are (R-074's tests are not renamed here).
3. **Failing tests first.** In `AstroPhotoAnalyzer.Tests/StarAnalyzerTests.cs`:
   `R070_NoiseGrainIsNotAStar` (a flat sky of 0.2 plus Gaussian noise of sigma 0.02 on
   600 x 600, `SyntheticRasters.AddNoise`: the old rule keeps well over 20 bumps, the new one
   under `StarMinCount`, `Insufficient` true) and `R070_FaintStarsOverGrainAreKept` (the same
   sky with `Scenes.WithStars`, amplitude 0.3, FWHM 3: 55..70 stars, FWHM 2.95..3.2, as the
   existing recovery test). A new `StarPairTests.cs`, trait `Category=Validation`, each test
   returning early with a message when its file is absent (the `TestData.Large` pattern of
   `ValidationTests`): `R070_StarlessFramesHaveTooFewStars` over
   `starless_rbf0.5_stretch0.17.fit`, `starless_rbf0.5_stretch0.1.fit`,
   `Rosetta_starless_no_WCS.fit`, `Veil_Starless.fit`, `Andromeda_starless_WithWCS.fit`,
   `Orion_with_WCS.fit` (each `Insufficient`); `R070_FramesWithStarsKeepTheirStars` over
   `Rosetta_withStars_no_WCS.fit` (>= 500), `Andromeda_WithStars_andWCS.fit` (>= 2500),
   `Orion_WithStars_and_WCS.fit` (>= 1000), `de_printat.png` (>= 4000), none `Insufficient`;
   `R070_OnTheRosettaPairEveryKeptStarIsInTheDifference`: at least 99 % of the stars kept on
   the with-stars frame have, at their peak, a with-minus-without difference of at least half
   their amplitude. The readers are the core's (`ImageReader`/`FitsReader`, as `AnalysisRun`
   uses them; confirm the entry point when writing the test).
4. **Code.** `Thresholds.StarMinSnr = 12` with a comment carrying the measured table above
   (the rule "Thresholds are empirical rules of thumb"). `StarAnalyzer.BorderMedian` also
   returns the border's robust sigma (the absolute deviations from the median of the already
   sorted buffer, sorted again, the median x 1.4826); `Analyze` keeps a candidate when
   `amp >= minAmp && amp >= StarMinSnr * sigma`. The file's header comment and the XML docs say
   so; nothing else in the analyzer moves (window, ties, widths, regions unchanged).
5. **Manifest.** The four with-stars and Andromeda files are already in `large/`; run
   `scripts/check-testdata.ps1 -Update` in the worktree, then write each new entry's `source`
   (the owner's copy, the pair of the starless frame already listed, with size and shape) and
   `demonstrates` (`StarPairTests`), in the manifest's style.
6. **Docs.** ADR 0051 "A star is at least 12 local sigmas over its base, not any bump over the
   floor" (context, the measurement, the alternatives: 10 and 15, roundness, flux SNR, global
   sigma; consequences: the star figures of V-001, V-004, V-010, V-013 move, the starless frames
   take R-074). `docs/design/measurements.md` D1: the sigma and the clause; its "Known"
   paragraph says why the floor alone found grain. `docs/design/known-limitations.md`: a line
   that faint stars under 12 sigmas are not counted and the FWHM is the bright stars'.
   `README.md`: the sentence on starless images gains that noise grain is not counted as a
   star. `CHANGELOG.md` Fixed: a starless image no longer reports thousands of stars and a
   false resolution. `docs/validation.md`: the figures moved and why, in the notes.
7. **Validate.** `/validate` in the worktree (the rule "Formula changes re-validate"): the
   drift table for V-001 (`stars.count` 7163 -> ?, `fwhmMedian` 3.133 -> ?, the regions, the
   size rows), V-004 (6297 -> a few, FWHM `n/a`, `size.*` on the dpi rule), V-010 (2858 -> ?),
   V-013 (22 485 -> 0); `validation.json` takes the `MEASURED` lines, every changed figure tied
   to this change and listed at wrap-up (the kit's ADR 0033). The golden console texts should
   not move (the golden scenes' stars are at 0.3 over 0.5/255 noise); any diff is read line by
   line and reported.
8. **Gate and commit.** `scripts/verify.ps1` green (build `--no-incremental -warnaserror` once
   before the final run), small commits on the branch, `/wrap-up`, merge proposed to the owner.

## Verification

- The two synthetic tests fail on `main`'s `StarAnalyzer` and pass after step 4.
- `dotnet test AstroPhotoAnalyzer.sln -c Release --filter "FullyQualifiedName~StarPairTests"` in
  the worktree with the copied images: every starless frame `Insufficient`, every with-stars
  frame not, the Rosetta pair's kept stars in the difference image.
- The console on the starless FITS: `dotnet run --project AstroPhotoAnalyzer -- <0.17 fit>`
  prints "stars: N measured - too few for a FWHM" and the R-074 size recommendation; on
  `de_printat.png` the FWHM median within 0.05 px of 3.13.
- `/validate` drift table pasted at wrap-up; `scripts/verify.ps1` green.

## Drift table (measured 2026-10-27 against `main`, after step 4 with the plane-fitted sigma; every line a star figure or one derived from the FWHM)

| case | figure | before | after |
|---|---|---|---|
| V-001 | `stars.count` | 7163 | 6274 |
| V-001 | `stars.fwhmMedian` / `Q1` / `Q3` | 3.1332 / 2.8685 / 3.4899 | 3.1065 / 2.8512 / 3.4209 |
| V-001 | centre region FWHM | 4.2709 | 4.2387 |
| V-001 | `size.row70..row100.minDistanceM` | 1.2736 .. 1.8195 | 1.2628 .. 1.804 |
| V-001 | `cornerToCenterRatio`, `naturalRatio` (within tolerance) | 0.7744, 1.0148 | 0.7822, 1.0062 |
| V-002 | `stars.fwhmMedian`; `size.row60..row100.minDistanceM` | 3.1332; 1.1672 .. 1.9453 | 3.1065; 1.1572 .. 1.9287 |
| V-004 | `stars.count` | 6297 | 5 |
| V-004 | every FWHM, region, ratio, `starMm`, `minDistanceM` | figures of the grain | `n/a` (R-074) |
| V-004 | `size.referenceWidthCm`; rows 90 and 100 `pointAtViewingDistance` | 100; true | 80 (the dpi rule); false |
| V-010 | `stars.count` | 2858 | 1 |
| V-010 | every FWHM, region, ratio, `starMm`, `minDistanceM` | figures of the grain | `n/a` (R-074) |
| V-010 | row 50 `pointAtViewingDistance` | false | true (the dpi rule) |
| V-013 | `stars.count` | 22 485 | 1 |
| V-013 | every FWHM, region, ratio, `starMm`, `minDistanceM` | figures of the grain | `n/a` (R-074) |
| V-013 | `size.referenceWidthCm`; rows 80 to 100 `pointAtViewingDistance` | 100; true | 70 (the dpi rule); false |

V-003, V-005 to V-009, V-011, V-012 and V-014: no figure moved. The golden console texts did not
move. The first form of the clause, the deviation about the raw border median, had cost the
DarkSky golden scene 14 of its 64 stars (the nebula's slope read as noise) and V-001 1747 stars
instead of 889; the owner chose the plane the same day (ADR 0051).
