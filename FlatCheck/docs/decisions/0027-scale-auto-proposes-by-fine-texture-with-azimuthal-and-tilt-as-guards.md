# 0027. --scale auto proposes the smoothing scale from the fine texture, guarded by azimuthal asymmetry and tilt

Date: 2026-09-25
Status: superseded by 0080

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-smooth.md and
docs/design/smooth-validation.md; the decision itself dates from 2026-09-16. `--smooth` has one
knob, the kernel sigma (150 / 300 / 600 sensor px on the window's three chips), and the user
needs a way to pick it. Three scales were run on the five validation masters, measuring per
scale the texture left in the output, the change in each structure test, and the removed
component's large-scale figure. The input's fine texture separates the textured master from
the clean ones by 12-17x in the shipped C# metric (0.69 % against 0.04-0.06 %).

## Decision

`SmoothScaleAdvisor` computes `Metrics.FineTexture` on the prepared input: the structured part
of the component under ~128 sensor px, a double box blur of `W/47`, with pixel noise
subtracted in quadrature. Below `Th.SmoothTextureMin` (0.20 %) the answer is `NIMIC DE
NETEZIT`. Otherwise one smoothing per rung of the three-chip ladder; a rung is acceptable when
the output's texture is under `Th.SmoothTextureGone` (0.05 %) and neither the azimuthal
asymmetry nor the tilt dropped by more than `Th.SmoothScaleLoss` (0.5 points). The smallest
acceptable rung is proposed; none means `NEDETERMINAT`. In both non-proposing cases the
console writes no file and exits 2; the window's button selects the chip and writes nothing.
It proposes, never decides.

## Alternatives rejected

- **A fixed 32 px blur for the texture metric.** It leaked vignetting curvature on a 256-px
  fixture; `W/47` is the same fraction of the frame on a fixture as on the sensor.
- **Corners, residual or worst tile as "too large" guards.** They drop when the texture leaves
  (worst tile -1.47 points at 300 on the textured master), so a drop there is the smoothing
  working, not overreach.
- **The removed component's own large-scale figure as the guard.** It caught the monitor
  blotches on tokina_aproape at 600 (1.71) but missed the Nikkor's ring at 600 (0.24 %) where
  the azimuthal drop was unambiguous (-0.79).

## Consequences

- The three thresholds live in `Th` and the ladder is `SmoothScaleViewModel`'s, half / default
  / double of `SmoothAnalyzer.DefaultScalePx`, so there is no second list of numbers.
- Acceptance on the five masters (2026-09-16), to re-run with `flatcheck <master> --smooth
  out.fit --scale auto` if `SmoothScaleAdvisor`, `FineTexture` or the thresholds change: 300
  proposed on tokina_pemonitor (0.09 % texture left at 150, 0.02 at 300, guards at 0.00 /
  -0.01), "nothing to smooth" on the other four, no undetermined case.
- Known limits the report must keep stating: lens versus source is still invisible (the
  Samyang's LCD hot spot escapes only because there is no texture); a real feature only in the
  corners, outside the inscribed circle and not a plane, escapes both guards; the texture
  threshold rests on five masters and a faint texture of 0.15-0.25 % sits near it.
- On small fixtures a texture coarser than a few percent of the width reads as structure in the
  azimuthal sectors; the golden fixture for `--scale auto` uses a 13-px weave for that reason.
- `Decide` is separately pure over hand-made trials, so the rule can be tested without a
  smoothing run.
- Source notes: docs/design/modes-smooth.md ("The scale can be proposed"),
  docs/design/smooth-validation.md ("Choosing the scale").
