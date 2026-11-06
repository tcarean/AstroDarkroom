# 0033. Without the band, the classes are relative to the modelled sky with thresholds from the image, and one measurement serves both paths

Date: 2026-10-06
Status: accepted 2026-10-06; the values of `SkyModelLowPercentile` and `SkyNoiseK` superseded by 0085
Supersedes: 0025 in part (decision 4, the frame rule); 0026 in part (the fallback to the frame rule and the `NoDust` case); 0032 in part (the histogram anchors without a WCS)

## Context

The five classes of ADR 0026 need a WCS and the galactic band covering 10 to 90 % of the
frame. Every other frame fell back to ADR 0025's frame rule: the dust the darkest 1 % of all
patches, the sky the median of all patches, the map with two layers, and the curves (ADR 0032)
with their three upper anchors at fixed percentiles of the pixel histogram (94.2, 99.1,
99.9 %), calibrated on one image. On `Rosetta_starless_no_WCS.fit` the owner found the map
showing noise flecks as dust and nothing else, and curve points named "banda" and "nori
luminosi" that matched nothing on the map; on `Orion_with_WCS.fit` the curves changed nothing.

Measured on 2026-10-06 with a probe over the core, on six images (Rosetta starless and with
stars, Orion starless and with stars, Andromeda with stars, the 0.17 reference FITS), on the
32 px patches:

- The histogram percentiles reproduce the band classes' anchors on the reference within
  1 to 2 L\* (34.4 / 57.1 / 72.9 against 33.2 / 57.7 / 74.6), which is why the curves work
  there and on Rosetta; but they are area quotas, not structure, and on a deep-sky frame the
  1 % rule's "dust" is noise flecks in the sky corners.
- A classification relative to a sky modelled from all patches (the 25th percentile of each
  cell, so a nebula covering half a cell does not lift it) with fixed thresholds borrowed from
  the band path (5 and 10 L\*) segments Rosetta into sky 84 %, nebula 6 %, bright 10 % and
  Orion into 69 / 9.5 / 21 %, but gives anchors of 26.3 / 43.5 on the reference, 7 and 14 L\*
  under the band path's.
- With the two thresholds taken from the image, the same classification reproduces the band
  path without geometry. Nebula = over the modelled sky by more than `SkyNoiseK` (4) robust
  sigmas (1.4826 x MAD) of the residual patch minus model, after one clipping pass at
  `SkyNoiseClipK` (3) sigmas; bright nebula = over the Otsu split of the residuals above that;
  dust = under the model by the same 4 sigmas. One pass of clipping, not two: on Orion starless
  the raw sigma is 2.95 L\* (the halo inflates the MAD), 1.75 after one pass, 1.46 after two.
  Anchors (band / bright class at the 80 / 75 % percentiles): reference 32.5 / 57.3 against
  the band path's 33.2 / 57.7, with the bright class 3.1 % of the frame against 2.0 %; Rosetta
  30.2 / 46.0 against the histogram's 33.2 / 49.8 (the curves the owner liked); Orion 40.6 /
  76.5; Andromeda 18.8 / 33.3. The nebula threshold is 3.8 L\* on Rosetta, 7.0 on Orion, 4.2 on
  the reference; the Otsu threshold 16.0, 32.9 and 21.0. Between k = 3 and 5 the nebula share
  moves by 2 % (Rosetta 14.1 / 12.3 / 10.8 %) and the anchors by under 1.5 L\*.
- A 3 x 3 median of the patch L\*s before the classification, tried against the star speckles
  of the with-stars frames, lowers sigma (0.95 to 0.73 L\* on Rosetta) and moves the anchors
  by 1.6 to 2.0 L\* on the starless frames, and the speckles stay: not adopted.
- One measurement for both paths: the sky is the median of the sky class; the dust, the darkest
  extended area, is the lower of the `DustClassFraction` (25 %) percentile of the dust class
  and the darkest `SkyDarkestFraction` (1 %) of the sky class. On the reference with the band,
  min(7.9, 10.1) = 7.9, unchanged; on Rosetta the dust class is 31 patches (0.4 %) at the
  nebula's edge, where the model is lifted, at L\* 8.4, and the darkest sky at 6.1 wins (the
  frame rule gave 6.2); Orion 10.8 (11.0); Andromeda 0.0 (0.0). On `de_printat.png`, the Milky
  Way without a WCS, the dust class is the Pipe's cores (181 patches, 0.8 % of the frame, 25 %
  percentile at L\* 2.5), which ADR 0025 rejected as the dust, against a darkest sky of 4.2 and
  the frame rule's 4.09; the band path finds the whole Rift (9.4 %) with the 25 % percentile
  at 7.9. So a dust class decides only from `DustClassMinFraction` (5 %) of the patches: under
  that it is cores or artefacts (0.4 to 2.7 % on the six frames), and the darkest sky stands.
  The owner chose 5 % on 2026-10-06.
- Orion's peaks (the Trapezium, the brightest 0.1 % of patches) sit at 0.971 of the scale (0.962 by the old histogram rule),
  over `CurvePeakTarget` (0.92). The recipe of ADR 0032 never lowers a point and scales the
  slopes down so the peaks stay under the target: with the peaks already over it, every point
  stays on the diagonal, whatever the anchors (0.014 to 0.029 of movement on the band point).
  The image is at the ceiling; the tool said nothing.
- The code review of 2026-10-06 found two more things, fixed before the merge: the Otsu tail
  must leave the peak patches out and keep at least `BrightClassMinTailFraction` (5 %) of the
  tail above the split (a few star patches far above a faint nebula would otherwise be the
  split, and the bright class would be them or nothing, so the Curbe tab would refuse the
  frame; the real classes are 15 to 30 % of their tail); and the thresholds must be offsets from the model, not from the residual's median,
  or the printed "4 sigma" is two different numbers up and down (0.5 and 0.4 L\* on the
  dark-sky golden). The dust's source is also decided once, on the image as it is, so every
  row of the ladder measures the statistic the definition sentence names.

## Decision

1. The band classes of ADR 0026 stay as they are when a WCS exists and the band covers between
   `BandMinFraction` and 1 - `BandMinFraction` of the patches (`DustMethod.Band`). Every other
   frame (no WCS, far from the plane, wholly inside it: `SkyReason`) gets classes **relative to
   the modelled sky** (`DustMethod.Sky`): the sky model from all patches at
   `SkyModelLowPercentile` (25) per cell; dust under the model by `SkyNoiseK` (4) robust sigmas
   of the residual (one clipping pass at `SkyNoiseClipK`, 3, about the median); nebula over
   it by the same; bright nebula over the Otsu threshold of the nebula residuals taken without
   the peak patches and leaving at least `BrightClassMinTailFraction` of them above; peaks the brightest `PeakFraction` as before; sky the rest. The
   thresholds are the image's and the map carries them (`SkySigma`, `NoiseThreshold`,
   `BrightThreshold`) for the legend and the report. The five classes keep one enum; their names follow the path (`praf, cer, banda,
   nori luminosi, varfuri` or `praf, cer, nebuloasa, nebuloasa stralucitoare, varfuri`,
   `DustAnalyzer.ClassNames`).
2. **One measurement** (`DustAnalyzer.Measure`), on either path: the sky is the median of the
   sky class; the dust is the darkest extended area, min(the `DustClassFraction` percentile of
   the dust class when the class holds at least `DustClassMinFraction` of the patches, the
   `SkyDarkestFraction` percentile of the sky class), decided once on the as-is image
   (`DecideDustSource`, `DustMap.DustSource`) and re-measured as that statistic on every lift,
   so the ladder's rows and the definition sentence agree; the report names it, and says when
   the sky class is empty and the sky is the median of all patches. The frame rule, the
   `NoDust` method and `DustAreaFraction` are gone.
3. The curve anchors come from the classes **always** (`CurveAdvisor.ClassLevels`), the point
   labels take the path's class names, and the sentence under the gallery names the path and
   the reason. `CurveAnchorSource` and the three histogram percentiles are gone. An empty upper
   class is a problem naming it.
4. Peaks at or over `CurvePeakTarget`: no curve is suggested, and the Curbe tab shows the
   core's reason in red (`CurveTexts.PeaksOverTarget`, `CurvesTabViewModel.IsProblem`).

## Alternatives rejected

- **Keep the frame rule and show the histogram percentiles on the map as classes.** Quotas of
  area with the band's names; on Rosetta they are not a band and not clouds.
- **Fixed thresholds of 5 and 10 L\* relative to the modelled sky.** Borrowed from the band
  path with no justification on deep-sky frames, and 7 to 14 L\* off the band path's anchors on
  the reference, where the image-derived thresholds land within 1 L\*.
- **A 3 x 3 median on the patches before the sky-relative classification.** Moves the anchors
  by up to 2 L\* on the starless frames and does not remove the star speckles.
- **Two clipping passes for sigma.** Contracts it (Orion 1.75 to 1.46 L\*); one pass removes
  the object's inflation and stops.
- **The dust class always decides when non-empty, or the minimum without a floor.** On
  `de_printat.png` the Pipe's cores (0.8 % of the frame) would set the dust at L\* 2.5 and the
  verdict's target up, against ADR 0025's choice on that image.
- **No dust class in the verdict without the band.** The same figures on the six frames, but
  two rules in the code instead of one.
- **A shoulder: an S-curve that compresses the highlights to make room when the peaks are at
  the ceiling.** A different kind of curve from ADR 0032's, which never lowers a point; it would
  flatten the Trapezium for separation in the halo. The owner chose the red sentence and no
  curve; the shoulder stays a possible later task, with its own measurement of what is lost up
  top.

## Consequences

- `DustAnalyzer`: `DustMethod { Band, Sky }`, `SkyReason`, `ClassifyBySky`, `SkyModel` with the
  cell percentile as a parameter, `ClassNames`, `DecideDustSource`, `Measure` on one path;
  `Stats.RobustSigma`, `Stats.OtsuThreshold`. Thresholds: `SkyModelLowPercentile`,
  `SkyNoiseClipK`, `SkyNoiseK`, `BrightClassMinTailFraction`, `DustClassMinFraction`, `SkyDarkestFraction` (in place of
  `DustAreaFraction`); the three `Curve*Percentile` removed. `CurveSet.Method` and `Labels`
  in place of `Source`; `GamutAnalyzer.Measure` always per class. `MapViewModel` always five
  layers; the Tonuri guide and the README rewritten for the two paths. R-044, R-092, R-113,
  R-130, R-141, R-142, R-146, R-147 rewritten.
- Figures: V-004 and V-008 (the band path) moved on no figure, only on the method's name.
  V-001, V-003, V-005 and V-009 (`de_printat.png`, no WCS) moved: the dust 4.09 -> 4.18 (the
  darkest 1 % of the sky class instead of all patches), the sky 9.38 -> 8.87 (the median of
  the sky class, the band's patches no longer in it), the recommended row 0.17 / 0.15 -> 0.16;
  on the Dmax 2.0 paper of V-001 the sky now sits under paper black (9.0) by a tenth, and the
  verdict is PROBLEMA instead of ATENTIE. V-010 to V-012 added on Rosetta (the sky classes, the
  dust from the darkest sky, the curves) and Orion (no curve, the peaks sentence).
- The synthetic band scene's dust blob grew from a radius of 60 px to 80 px (about 5 % to 10 %
  of the patches), so its dust class is over the new floor; the LDN 42 scene, whose disc covers
  nine tenths of the frame, now has the cloud as its modelled sky and the plain sky around it as
  bright nebula, and the dust is the darkest sky, the cloud's own level: the same figure as
  before by another route.
- Known limits: stars pepper the map on the sky path (a 32 px patch with a bright star reads
  as nebula; the anchors move by under 1 L\*); an object filling the frame becomes the modelled
  sky, and the "sky" is then its faintest part, as on the LDN 42 scene; the Great Rift is found
  only with the band (without geometry it is the sky's level, and only its darkest knots fall
  under the model); the dark globules inside a nebula are not dust (that would need the band
  path's local level over the whole frame).
