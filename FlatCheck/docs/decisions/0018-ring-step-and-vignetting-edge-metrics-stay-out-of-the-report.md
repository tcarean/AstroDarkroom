# 0018. The ring-step and vignetting-edge metrics stay out of the report

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/pipeline.md and
docs/design/radial-model-even-term.md (§6.1); the decisions themselves date from 2026-09-23 (the
ring step, asked for under the map after the 4θ artefact was found) and 2026-09-17 (the edge
metric, prototyped while the rings on the Nikkor ISO800 map were still read as a hard-edged,
decentred falloff). Two candidate figures existed, both about rings in the residual map. The
ring step: the largest change of the residual's ring mean between two consecutive profile bins
(bins under 5000 px excluded); floor 0.03-0.36 % on the fifteen library masters, 0.5-1.3 % where
a ring was visible, 8.5 % on the single-orientation stack. The edge metric: the maximum deviation
of the ring medians from a Savitzky-Golay smoothing, meant to detect a hard edge in the
vignetting profile; it read 0.03 % on ISO400 and 0.98 % on ISO800, a clean 30x separation.

## Decision

Neither is printed. The ring step lives only in the test suite (`WorstRingStep` in
`StructureAnalyzerTests`) as a guard on the model; the edge metric is not built. Shapes are the
eye's job: the numbers vote, the map shows the shape.

## Alternatives rejected

- **Printing the ring step under the map.** It is a property of the model, not of the flat;
  beside the five tests it reads as a sixth quality figure that sits at the floor on every
  master anyone will run. And it cannot tell a model artefact from real fine structure: the
  on-monitor master's paper texture reads 0.79 % with no ring of the model's in it, so a warning
  threshold at 0.5 % would fire exactly where `--smooth` is the answer.
- **Building the vignetting-edge metric.** Its 30x separation was the ring artefact of ADR 0017,
  not the flat: on a synthetic with no edge at all it read 3.05 %, and from the ring mean it
  read 0.05 % on both masters; with the symmetrised profile both real masters read 0.06 %, so
  the library has no real positive. Its dynamic range is too narrow to threshold (1504x1004
  synthetics: smooth 0.02, a soft 10 % knee 0.24, a 1 % hard step 0.38, a 3 % hard step 1.16),
  and small rasters sit above that by discretisation alone (64x64 fixture 0.80 %, 256x256
  0.15 %).
- **A shape classifier in general.** The tool does not decide what a shape is and must not
  start: a blotch can be a light leak or a cloud, and the pixels do not tell them apart.

## Consequences

- Since 2026-09-27 `WorstRingStep` implements the definition above (80 profile bins out to the
  corner, bins under 5000 px excluded) and reproduces the whole-library table of
  `radial-model-even-term.md` §7.3 on the real masters. Until then it stepped by 2 % of the
  inscribed radius from 0.6 of it with a 2000 px floor: the same on smooth masters (`Tokina
  ISO400 f2.8-sky-no-difuser` 0.364 vs 0.363 %), different on the textured ones (`Tokina ISO400
  f2.8-monitor-difuzor-aproape` 0.317 vs 0.247 %).
- The guard against model-made rings is the suite: `OnAGradient_TheResidualHasNoRingsOutToTheCorner`
  and `OnAFourfoldTerm_TheResidualHasNoRingsAndReadsTheInjectedStructure` compute the ring step
  on the gradient and four-fold scenes with a 0.5 % threshold; a regression fails the build
  before it reaches a user.
- When the ring model is touched, the check is the whole-library loop of
  docs/design/radial-model-even-term.md §6.1 - ring step on every residual, every map looked at
  on a fixed scale; a step above ~0.4 % on a master that is not textured is the model's.
- The map guide's ring row was rewritten after the artefact was understood; it no longer claims
  a ring means a decentred obstruction, pinned by
  `TheRingRowDoesNotClaimARingMeansADecentredObstruction`.
- If a real hard edge ever turns up, the edge metric's prototype figures are the starting point;
  until then it has nothing to measure.

Source notes: docs/design/pipeline.md ("The ring-step metric stays out of the report",
"A 'vignetting edge' metric was prototyped ... and rejected"),
docs/design/radial-model-even-term.md (§6.1), docs/design/two-applications.md (the guide's ring
row).
