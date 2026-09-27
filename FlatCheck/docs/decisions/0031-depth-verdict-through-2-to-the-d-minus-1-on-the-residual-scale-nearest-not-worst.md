# 0031. The vignetting-depth verdict goes through 2^|D| - 1 onto the residual scale, against the nearest profile, with the --light reading printed as a note

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-structure.md ("The sixth test") and
docs/design/lens-profiles.md (sections 7, 11) (and the memory note
flatcheck-structure-profile-comparison); the decision itself dates from 2026-09-18, with the
scale note added 2026-09-23. The five structure tests measure departure from radial symmetry and
are blind to a radial excess: the tablet masters of 2026-09-18 (Tokina 20 mm f/2.8, four
orientations at 90°) read PERFECT on all five with 1.26 stops of vignetting against 0.66-0.92
from the two profile databases, and `--ratio` is just as blind. The lens's published profile is
the only external reference, and the two databases differ by 0.16-0.25 stops on the same lens.

## Decision

The profile's depth is measured with the master's own operator: the model is built as a raster
at analysis size and `Metrics.CornerStats` runs on it, so box median and half-pixel artefact are
the same on both sides. No new threshold: the difference in stops goes through
`Stats.DepthErrorPct` (`2^|D| - 1`, the corner error of a light calibrated with the wrong flat)
onto the residual scale - 0.03 / 0.14 / 0.26 stops for PERFECT / BUN / ACCEPTABIL. The verdict is
the nearest profile's, not the worst (the owner's decision); the block prints every profile,
marks the nearest and states the spread. The run's verdict is the worse of shape and depth and
the exit code follows it; when depth carries it, `DepthAdvice` reads the sign (plus = the source
falls off with angle, minus = the source fills the corners). When it cannot compare it says why
(`ComparisonGap`) and votes nothing. On 2026-09-23 the owner chose option A: the bands stay,
and `StructureReport.DepthLightScaleNote` prints the corner error, what `--light`'s 2 / 5 %
would call it (`Stats.DepthOnLightScale`, the same `Verdict` and `Th.LightRad*` as
`LightAnalyzer`), and the reference's spread `Th.ProfileDatabaseSpreadStops` = 0.2 stops = 15 %.

## Alternatives rejected

- **The polynomial evaluated at the corner point.** The box median and the half-pixel artefact
  would differ between master and model; on a master that is the model the raster method reads
  0.00, the residual under 0.001 stops.
- **The worst profile's verdict.** It would have called the good monitor masters ACCEPTABIL and
  BUN instead of PERFECT and BUN (`Tokina ISO400 f2.8-monitor-difuzor-aproape` +0.19 stops off
  Adobe, 13.9 %; `Tokina ISO400 f2.8-monitor-difuzor-pe-monitor` −0.13 off the lensfun AF
  entry, 9.7 %); a master that coincides with one database is not wrong. (Corrected
  2026-09-27: this line said "REFA (0.15 off Adobe, 0.03 off lensfun)", copied from the comment
  of `VignettingComparison.cs`; the console run with `--raw` on both masters, V-036 and V-038
  give the figures above, and 0.15 stops is 11 %, ACCEPTABIL on the residual scale.)
- **Option B, aligning the depth bands to `--light`'s 2 / 5 %.** Measured and turned down: it put
  the paper masters (6-8 %), the Nikkor 35 DX sky masters (7-8 %) and the Samyang f/2 master on
  REFA, all below the reference's own resolution (15 % in the corner).
- **A mid-field column; a lens picker for a folder without a raw.** Deferred and turned down by
  the owner: the raws are always in `flats/`, the console has `--lens`.

## Consequences

- Without `--raw` / `--lens` nothing changes: same text, same goldens, the 15 `crosscheck.py`
  structure scenarios untouched.
- Library figures (2026-09-18) to re-run when the method changes: tablet 7 / 15 sheets
  +0.34 / +0.33 vs lensfun D7100 → REFA exit 3; sky −0.39 vs Adobe → REFA; monitor close 0.00 vs
  lensfun 7D2 → PERFECT; on-monitor −0.07 → BUN; Nikkor 35 DX +0.11 / +0.10 → BUN; Samyang f/2
  +0.11 / +0.10 → BUN, f/2.8 +0.01 → PERFECT.
- The scale is printed as the residual scale translated into stops, never a new threshold, with
  the sentence saying so (the user asked whether it was a second scale).
- Guards: `AMasterThatIsTheModelReadsZeroStopsAndPerfect`, `TheProfilesDepthIsMeasuredWithTheMastersOwnOperator`,
  `TheDepthNoteSaysWhatTheLightScaleWouldReadAndWhyItDoesNotDecide`, `VignettingComparisonTests`,
  the profile half of `StructureReportTests`, goldens `structure_with_profiles` (BUN on shape,
  REFA on depth, exit 3) and `structure_lens_no_fstop`, `StructureTabTests`.
- Source notes: docs/design/modes-structure.md, lens-profiles.md; memory note flatcheck-structure-profile-comparison.
