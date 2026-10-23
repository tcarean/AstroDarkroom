# 0032. Suggested curves are contrast curves anchored on the classes, and the window saves a FITS

Date: 2026-10-05
Status: accepted 2026-10-05; the histogram anchors of a frame without a WCS superseded by 0033; the uncapped Maxima factor of decision 4 and the judgement of a curve on its clamped table superseded by 0039

## Context

The owner asked for a tab that suggests S-curves for a finished image, for print or for screen,
shows a preview of each, shows the curve as a table and a chart so it can be redone by hand in
Siril's Curves Transformation tool, and saves a new FITS with the curve applied, the input never
touched. Until now the tool exported nothing (`CLAUDE.md`, first paragraph; ADR 0008).

Measured on 2026-10-05 on `de_printat.png`, `starless_rbf0.5_stretch0.1.fit` and
`starless_rbf0.5_stretch0.17.fit`, with a Python port of `DustAnalyzer` that reproduces the
console's class shares and dust and sky L\* on both FITS files:

- **A curve does what a re-stretch does.** The two FITS files are the same linear data stretched
  to 0.10 and to 0.17, pixel aligned. The transfer function between them is one curve, the same
  on R, G and B: residual 0.0007 RMS on values, 0.03 L\* RMS on the 32 px patches (max 0.45).
- **Three ways to build a curve.** Anchors on the classes (one control point per class): hits its
  targets, monotone everywhere, slope under 1.9. A sigmoid around a pivot: darkens the sky and
  the dust (on the 0.10 FITS, 7 to 19 % of the patches under L\* 3.6, against 2.3 % as is). A
  blend with the histogram's cumulative distribution: slope 1.9 to 3.5 at the sky, which is the
  noise gain, and one variant not monotone.
- **The owner's earlier curve for the 0.17 FITS** (0.08 > 0.08, 0.17 > 0.165, 0.32 > 0.42,
  0.55 > 0.76, 0.72 > 0.92) sends the peaks into the empty range above them: patches' L\* dust
  7.7, sky 17.2, band 27.3, bright clouds 64.2, peaks 90.9, noise gain 1.21, against bright
  clouds 57.0, peaks 75.7 and a gain of 1.40 for the strongest curve of a rule that held the
  peaks in place. Its segment slopes are 1.70 (sky to band), 1.48 (band to clouds), 0.94 (clouds
  to peaks).
- **Where the anchors come from.** Its inputs are the upper part of each class, not the median:
  the band's 80 % percentile is 0.306, the bright clouds' 75 % percentile 0.543, the peaks'
  median 0.720. With the class medians (band 0.225) the same slopes give a noise gain of 1.40
  and peaks at L\* 85.1; with the upper parts 1.26 to 1.28 and L\* 90.5 to 90.8. Without a WCS
  the frame's histogram stands in: the 94.2, 99.1 and 99.9 % percentiles of the pixel grey give
  0.317, 0.538 and 0.701 on the 0.17 FITS.
- **Lifting the sky in the curve.** A print curve that takes the sky of the 0.10 FITS to L\* 15
  has a noise gain of 1.45 to 1.66, of which the lift alone is about 1.4; the same curve without
  the lift, 1.13 to 1.39. By the first measurement the lift is the same operation as the
  re-stretch the Tonuri tab already recommends.
- **Per channel or on the luminance.** Per channel the curve's shoulder lowers the chroma of the
  highlights (bright clouds' median C\* 24 to 18 on the PNG at the strongest curve); on the
  luminance alone the chroma is kept and more pixels leave the gamut (bright clouds 0.1 % to
  0.5 % on the 0.10 FITS, baryta). Siril's tool applies the curve per channel.
- **Gamut**, through the lab's two profiles (relative colorimetric, black point compensation,
  Delta E over 3): on baryta 3 to 4 % of the pixels, nearly all under L\* 10 where the chroma
  lost is 1 to 1.5 C\*; the band and the bright clouds 0.1 to 4 % with under 1 C\* lost; only the
  peaks (0.1 % of the frame) lose 13 to 19 C\* at their 99 % percentile. No per-channel curve
  raised the share.
- **Siril's spline.** `curve_transform.c` on Siril's master branch solves a natural cubic spline
  (second derivative zero at both ends), clamps outside the first and last point and clamps the
  result to 0..1, per channel. Whether 1.4.4 lets the points be typed was not verified.

## Decision

1. A suggestion is a **contrast curve through five anchors**, the dust, the sky, the band, the
   bright clouds and the peaks, plus the two ends, interpolated as Siril's `cubic spline` mode
   does and applied to every channel alike. Nothing else is applied: what the table shows is
   the whole operation.
2. The dust and the sky come from R-044. With the classes, the band, the bright clouds and the
   peaks are the upper part of their classes (80 %, 75 %, median); without them, the frame's
   percentiles (94.2, 99.1, 99.9).
3. The curve **never moves the dust or the sky**, for print and for screen alike. A sky too dark
   for paper is lifted at the stretch, which the Tonuri tab already recommends; the print
   purpose adds the paper checks to a suggestion's evidence and changes no curve.
4. Above the sky the segments keep the earlier curve's slopes, scaled down together when the
   peaks would pass 0.92. `Blanda`, `Medie` and `Puternica` are 50, 75 and 100 % of it;
   `Maxima` scales the slopes up until the peaks reach 0.92, and exists only when `Puternica`
   leaves them under it.
5. The paper's gamut is **a figure in the evidence**, per class, when a profile is loaded. It
   feeds no curve; saturation is corrected at soft-proofing, locally.
6. **The window saves a FITS**, and this is the tool's one export: always FITS whatever the
   input, a new file, every header card of a FITS input kept byte for byte, `HISTORY` cards
   naming the suggestion and its points. The console gets nothing; the report does not change.
7. The suggestions are computed on demand (`Calculeaza`), by a second run in `Runs/` beside
   `AnalysisRun`, from the result of the analysis already made.

## Alternatives rejected

- **A sigmoid around a pivot; a blend with the histogram's distribution.** Measured above: the
  first buries the dust, the second multiplies the sky's noise by 2 to 3.5.
- **Anchors at the class medians.** The band's median sits beside the sky, so the steep segment
  starts at the sky: gain 1.40 against 1.26.
- **Anchors from the frame's histogram always.** They give the same curve on the FITS files, but
  the owner asked for the classes of the Tonuri tab where a WCS exists, and they are the same
  five names the map shows.
- **A print curve that lifts the sky to L\* 15.** The same operation as the re-stretch, decided
  in a second place; the owner lifts at the stretch.
- **The curve on the luminance only.** Keeps the colour of the highlights, but cannot be redone
  in Siril from the table, and pushes more pixels out of gamut.
- **Desaturating out-of-gamut pixels in the tool; lowering the whole image's saturation.** A
  second operation the table would not show; the loss is confined to 0.1 % of the frame.
- **Saving in the input's format.** Three writers and their metadata rules instead of one; the
  owner chose FITS for every input.
- **A console flag for the curves.** Not asked for; the report stays a diagnosis.

## Consequences

- R-140 to R-147. `CLAUDE.md`'s first paragraph and the rule "The image is display-referred and
  final" say what the tool now writes: one FITS, on request, from the window.
- New thresholds, each with its source in `Thresholds.cs`: the class and frame percentiles of the
  anchors, the three slopes, the peak target, the strengths, the clip level, `Maxima`'s minimum
  gain, the shadow chart's factor.
- The FITS reader gains a writer beside it, hand-written as the reader is (ADR 0009).
- The gamut figure needs no new package: measured on 2026-10-05, Wacton.Unicolour evaluates both
  directions of the lab's RGB printer profiles (in-gamut colours come back within Delta E 0.5),
  at about 28 microseconds a colour, so the figure is built on a round-trip grid. It counts the
  chroma only (a\*, b\* moved by more than 3), without black point compensation, which is not
  the study's Delta E with compensation: 1.0 % of the plate-solved FITS on baryta against the
  study's 4.2 %, the difference being the darks.
- The evidence gives each class's level with the statistic of its anchor, so the band and the
  bright clouds read higher than the class medians quoted in the Context (33.2 and 57.7 as is
  on the 0.17 FITS, against medians of 24.2 and 47.4).
- A review on 2026-10-05, before the merge, overwrote the input through other spellings of its
  path and found a FITS saved without its header after a change of working directory. The input
  is therefore known by its full path, held open against writing for the length of a save, and
  required to exist; an integer encoding that cannot hold the curved values refuses the save.
- Known limits: the anchors of a frame without the classes are percentiles of the histogram, not
  objects; the slopes come from one curve on one image; `Maxima` clips more pixels (0.16 to
  0.37 % against 0.02 to 0.10 % on the three images).
