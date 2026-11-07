# Scale the band path's dust depth by the sky's noise

Date: 2026-11-07. Branch `claude/Scale-the-band-dust-depth-by-the-noise`, worktree
`.claude/worktrees/Scale-the-band-dust-depth-by-the-noise` (from `main` at 9872d2fd, the kit's
rule "Worktrees").

## Context

ADR 0085 (2026-11-06) made the sky path's thresholds multiples of the sky's noise and showed
that the band path's fixed 5 L\* depth finds half of the Great Rift on a darker stretch (5.7 %
of the reference frame at a background of 0.10 against 9.4 % at 0.17). The owner asked for the
band path to be fixed the same way, then had a dust map (SFD98) measured as an alternative; the
measurements and the choice are in ADR 0086: 4 sigmas of the sky path's noise for the dust, 8
for the bright clouds, the map not adopted.

## The change

1. `Thresholds.cs`: `DustDepthLstar` (5) and `BrightCloudLstar` (10) become `BandDustK` (4) and
   `BandBrightK` (8), with comments carrying the measurements.
2. `DustAnalyzer.cs`: `SkyNoise` (the median-cell model, the residuals, the clipped robust
   sigma) shared by `Classify` and `ClassifyBySky`; the band path sets `SkySigma`,
   `NoiseThreshold`, `BrightThreshold`.
3. Texts: the dust class's definition in the report (`PrintStretchSimulator`), the map window's
   legend (`MapViewModel`), the Tonuri tab's guide (`ResultTabs`): the depth in L\* with the
   factor, as the sky path's texts already read.
4. `DustAnalyzerTests` R-044: the rule asserted through the map's thresholds, plus the two
   multiples.
5. ADR 0086 (supersedes ADR 0026 in part), R-044, README, `measurements.md`, `thresholds.md`,
   CHANGELOG; ADR 0085's open item restated.
6. `/validate` with the reference images copied into the worktree: V-004 and V-008 re-measured,
   the drift table pasted at wrap-up, `validation.json` and `validation.md` updated.

## Out of scope

- The SFD98 map as a veto on image-only dust, or as the clouds' extent on the map window.
