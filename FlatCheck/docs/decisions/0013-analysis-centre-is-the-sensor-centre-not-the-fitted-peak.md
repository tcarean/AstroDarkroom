# 0013. The analysis centre is the sensor centre, not the fitted illumination peak

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/pipeline.md ("The core idea") and
docs/design/known-issues.md (item 5); the decision itself dates from the original design. The
whole method rests on one fact: for a lens with no optical train, a correct flat is a purely
radial function about the optical axis, so any departure from radial symmetry is error. The
radial model and the azimuthal asymmetry need a centre to be radial about. A linear gradient laid
over quasi-quadratic vignetting merely shifts the paraboloid's vertex, so a centre taken from the
fit would move with exactly the defect being measured.

## Decision

`RadialModel` and `AzimuthalAsymmetry` are computed around the sensor centre, `(w/2, h/2)`,
never around the fitted illumination peak. `FitCenter` never supplies the centre the other
metrics use; it supplies the tilt (a test) and the vertex offset (informative since 1.2.0,
ADR 0014).

## Alternatives rejected

- **Centring on the fitted peak.** The fit would absorb the linear gradient the test exists to
  detect, and the test would silently cancel itself out.
- **Moving the centre to `(w-1)/2, (h-1)/2`, the true middle of pixel indices `0..w-1`.** It
  would remove the half-pixel floor below, but changing the centre convention breaks numeric
  parity with `flatcheck.py`, which is tested across 43 `crosscheck.py` scenarios. (Restated
  2026-09-28: ADR 0047 retired the cross-check, so this reason no longer holds as written. What
  holds: moving the centre is a formula change under rule 16, judged only by `/validate` and by
  the 43 parity goldens, whose figures an independent implementation confirmed once and cannot
  confirm again; and it would buy about 0.2 % at analysis size, below every band. Whether it
  would move the parity goldens was not measured.)

## Consequences

- The corner-spread metric has a systematic non-zero floor: the half-pixel offset puts the
  top-left corner fractionally farther from the centre than the bottom-right, so a perfectly
  radial field reports a spread. It scales as ~1/n: 2.88 % at 100 px, 1.44 % at 200, 0.56 % at
  512, 0.28 % at 1024 (cross-checked against a numpy reimplementation), ~0.2 % at the
  ~1500x1000 analysis raster - far below the 2 % PERFECT band, but it would swamp the threshold
  on a small enough frame. Not a bug to fix.
- The same cause costs `Img.Symmetrise` a half-pixel: `Rotate180` mirrors about
  `((H-1)/2, (W-1)/2)` while rings are measured about `(H/2, W/2)`, perturbing a radial field by
  ~0.3 % on a 512² raster and ~2.8 % on 64², fading as ~1/n.
- The sixth structure test measures the lens profile's depth with the master's own operator -
  the model built as a raster at analysis size and `CornerStats` run on it - so the artefact is
  the same on both sides and a master that is the model reads 0.00 stops.
- Small test fixtures sit above the floor by discretisation alone; a threshold test on a
  fixture must allow for it.
- Pinned by `CornerStats_HasAHalfPixelCentringArtifact_ThatHalvesAsTheFrameDoubles`,
  `Symmetrise_OnTheFixtureFieldCostsOnlyTheHalfPixelArtefact` and
  `AMasterThatIsTheModelReadsZeroStopsAndPerfect`.

Source notes: docs/design/pipeline.md ("The core idea", `RadialModel`, `Symmetrise`),
docs/design/known-issues.md (item 5), docs/design/modes-structure.md (the sixth test's
"measured with the master's own operator").
