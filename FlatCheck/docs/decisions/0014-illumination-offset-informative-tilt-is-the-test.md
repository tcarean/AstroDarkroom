# 0014. The illumination-centre offset is informative; the tilt is the test

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/thresholds.md ("The residual scale") and
docs/design/pipeline.md (`FitCenter`); the decision itself dates from version 1.2.0. Until then
the offset of the paraboloid's vertex from the sensor centre, as a percentage of the
half-diagonal, was one of the structure tests. The vertex is `-b/(2d)`, gradient over curvature:
not a fraction and not scale-invariant. Measured with the same 11 % gradient held fixed, it read
2.38 % at 2 stops of vignetting, 9.49 % at 0.5, 31.16 % at 0.15, 57.40 % at 0.08 - and then
0.00 % at 0.05, because the vertex leaves the frame and the out-of-frame guard returns the
sensor centre. Past that point the worse the defect, the cleaner the report: a silent false
negative on exactly the flattest lenses. The fixtures never caught it because
`FitsTestData.Radial` defaults to `k = 0.45` (0.86 stops), 5.7x deeper than a real Samyang 135
at f/2.8. On the real Samyang master that prompted the change (0.15 stops, an 11 % twilight-sky
gradient) the offset said 42.36 % PROBLEMA and the mode said REFA.

## Decision

The offset prints under `INFORMATIV (nu sunt teste)` with no verdict. The test is the tilt,
`tiltPct = 2·sinh(|b| + |c|)·100`: the peak-to-peak amplitude of the fitted linear term over the
frame, a fraction on the shared residual scale. `FitCenter` returns
`(cx, cy, tiltPct, vertexValid)`; when the vertex system is singular or the vertex lands outside
the frame it falls back to the geometric centre with `vertexValid = false`, so the report says
`nedeterminat` instead of a fake `0.00`.

## Alternatives rejected

- **Keeping the offset as a test with a recalibrated threshold.** It was not miscalibrated but
  non-monotonic; no threshold fixes a metric that returns 0.00 % on the worst case.
- **The linear approximation `2(|b| + |c|)` for the tilt.** `sinh` converts exactly from log
  space to intensity; the tilt recovers an injected gradient to within ~1 %.
- **Including the cross term `f` in the tilt.** A saddle is not a tilt, and the residual test
  catches it anyway.
- **Printing `0.00` when the vertex cannot be determined.** The Python prototype does exactly
  that on a constant frame (garbage vertex at (136, 136), a 6.35 % offset); the C# guard on
  `|det| < 1e-15` is the correct behaviour and is not to be "fixed" to match.

## Consequences

- The tilt still catches genuine decentring: a radial field centred off the sensor produces a
  linear term of about `offset x curvature`, so the offset is weighted by what it costs
  photometrically. On the Samyang master the tilt reads 12.36 % ACCEPTABIL, in line with the
  other tests (12.62 / 7.05 / 6.05 at the time).
- If the normal equations are singular the tilt is `NaN`, which degrades to REFA like every
  other NaN.
- Since 1.2.0 the constant-frame divergence from `flatcheck.py` is confined to the informative
  line; both implementations reach the same verdict.
- Pinned by `FitCenter_TiltIsInvariantToTheVignettingDepth`,
  `FitCenter_WhenAFlatFieldMakesTheVertexEscape_TheTiltStillReadsTheGradient`,
  `OnAFlatLensWithAGradient_TheTiltCarriesTheVerdict` and
  `FitCenter_OnAConstantImage_FallsBackToTheGeometricCentre`.

Source notes: docs/design/thresholds.md ("The residual scale"), docs/design/pipeline.md
(`FitCenter`), docs/design/python-parity.md (the constant-frame divergence).
