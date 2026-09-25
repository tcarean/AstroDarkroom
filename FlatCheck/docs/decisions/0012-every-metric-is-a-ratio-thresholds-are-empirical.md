# 0012. Every metric is a ratio; thresholds are empirical rules of thumb kept in Th

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/pipeline.md and docs/design/thresholds.md
(the rule itself is stated in docs/design/process.md); the decision dates from the original
design, with the "everything in `Th`" half completed in phase 8. A flat is shot at whatever ISO
and gain the session used, and a master is a float normalised by Siril or an ADU frame; a figure
that depends on the absolute level would read differently on the same defect. Thresholds were
partly inline: `--light` had 2.0/5.0 in code, `--expo` kept its bands as locals, and
`Th.ExpoLo`/`Th.ExpoHi` were dead and wrong (50/60 while the code used 50/65).

## Decision

Every metric is a fractional amplitude - a ratio - so ISO and gain cancel: corner spread,
azimuthal asymmetry, residual, worst tile, the tilt as `2·sinh(|b|+|c|)·100` (a fraction of the
level), the ring-fit `q` as relative amplitudes per `r²`, the profile depth as `2^|D| - 1`. A
metric that is not scale-invariant is a design smell. Every threshold lives in `Th`, is an
empirical rule of thumb to be calibrated against a known-good setup, and is read from `Th`
wherever it is printed rather than written out.

## Alternatives rejected

- **Gain-dependent corrections.** Forbidden by the rule; a metric needing one is the wrong
  metric.
- **The illumination-centre offset as a test.** `-b/(2d)` is gradient over curvature, not a
  fraction and not scale-invariant; it was non-monotonic and became informative in 1.2.0
  (ADR 0014).
- **Thresholds as literals in code or text.** `Th.ExpoLo`/`Th.ExpoHi` drifted to dead-and-wrong
  that way; `StructureReport.LocalSpotNote` reads `Th.LocalSpotWarn` so a recalibration cannot
  leave the text quoting the old value.
- **A new threshold for the profile-depth verdict.** The difference in stops goes through
  `Stats.DepthErrorPct` onto the existing residual scale (0.03 / 0.14 / 0.26 stops from
  `Th.Resid*`); the map's fixed caps are likewise `{ Th.ResidPerfect, Th.ResidUsable,
  Th.ResidCheck }`, not new numbers.
- **Aligning the depth bands to `--light`'s 2 / 5 %** (option B, measured 2026-09-23). It put
  most of the library - paper masters at 6-8 %, Nikkor sky masters at 7-8 % - on REFA below the
  reference's own resolution (`Th.ProfileDatabaseSpreadStops` = 0.2 stops = 15 %); the note
  prints both readings instead.

## Consequences

- A new figure must be a ratio; a new bound goes into `Th` and is quoted from there.
- Thresholds are starting points, not physical constants: recalibrating one is a legitimate
  change and touches one file, and the golden files show what moved.
- `Th.ExpoTarget` (57) and `Th.ExpoSkyTarget` (30) are the midpoints the suggested-stops
  arithmetic uses; the exposure bands are also exposed as a value (`ExposureBands`) so the
  window's chart cannot define the verdict a second time.
- Pinned by `FitCenter_TiltIsInvariantToTheVignettingDepth`,
  `TheFixedCapsAreTheResidualScaleBoundaries` and
  `TheDepthNoteSaysWhatTheLightScaleWouldReadAndWhyItDoesNotDecide`.

Source notes: docs/design/pipeline.md (`FitCenter`, `RadialModel`), docs/design/thresholds.md
(the table, "The residual scale"), docs/design/process.md (the two convention lines).
