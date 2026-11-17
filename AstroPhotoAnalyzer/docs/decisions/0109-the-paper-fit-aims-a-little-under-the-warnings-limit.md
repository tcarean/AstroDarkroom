# 0109. The paper fit aims at a whole difference of 3.5, a reserve under the warning's 4

Date: 2026-11-17
Status: accepted 2026-11-17
Supersedes: 0083 in part (decision 1, the fit moving only the pixels the warning marks and landing them on the warning's own edge); 0093 in part (the Soft proof judge's word deciding which pixels are moved)

## Context

The owner fitted the Smida Milky Way TIFF of 2026-11-16
(`denoised_syqon_studio_in_max_0.75_stretch0.17-log4_Gentle_lowdust_sat0.25_signed.tif`, sRGB,
5837 x 3867) to the WhiteWall Photo Rag Baryta profile with the `Lina` variant and opened the
saved file in Lightroom Classic (soft proofing, relative, simulate paper and ink): the tool's
Soft proof tab showed 0.0 % outside, Lightroom's destination gamut warning still marked the rims
of the darkest dust. Measured that day and the next with scratchpad probes over a copy of the
core (a pixel in four along each axis; the limit made settable in the copy) and the owner's
Lightroom captures registered on the image (1118 pixels wide):

- Under Lightroom's red on the fitted file the tool's whole difference was 3.86 at the median
  (3.34 to 4.05 between the 10 % and 90 % marks), L\* 5.6, chroma 7.0: the pixels the fit had
  landed just under the limit of 4. Three quarters of them had been moved by the fit; in the
  original the same pixels read 4.42. Of the 3.86, 3.74 is lightness and 0.71 colour.
- In Lightroom a mask on the dark tones with the saturation at -100 cleared the red (0.30 % of
  the image to 0.006 %), and so did lifting those tones with the saturation back: the pixels
  hold more colour than the paper makes at that darkness, which a lower chroma cures.
- Trial files fitted with the limit lowered, the `Lina` knee, judged by the owner's captures:

| profile | not fitted for it | fit to 4 | fit to 3.5 | fit to 3 |
| --- | --- | --- | --- | --- |
| WhiteWall Photo Rag Baryta | no capture | 0.31 % marked | at most 0.005 % | at most 0.001 % |
| Hahnemuhle P900 Photo Rag Baryta (`HFA_EpsSC-P900_PK_PhotoRagBaryta.icc`) | 1.31 % (the file fitted for WhiteWall) | 0.04 % | none in the capture (a few points by eye) | none (by eye) |
| the lab's baryta (`Hahnemuhle PRBaryta.icm`) | none (by eye) | none | none | none |

- What the lower limit costs, on the tonal subsample of that TIFF (pixels changed; chroma given
  up at the median): WhiteWall 4.33 % and 1.19 at 4, 5.52 % and 1.43 at 3.5, 6.91 % and 1.72 at
  3; P900 6.79 % and 1.68, 7.85 % and 1.97, 8.98 % and 2.33; the lab's baryta 1.49 % and 0.16,
  2.70 % and 0.29, 4.09 % and 0.53. L\* moved by at most 0.001 in every trial file.
- The limit cannot be read from the profile. The round trip's own error on colours the paper
  certainly makes (35 937 printer values, the tool's rule on what each prints as): 99 % under
  2.0 on all three, at most 3.6 on WhiteWall (3.2 among the colours sRGB holds), 3.2 (2.5) on
  the lab's baryta, 5.2 (4.5) on the P900. The guess that Lightroom's limit follows that error
  failed: on the lab's baryta, with the smallest error, Lightroom marks nothing, not even on
  the original, where the tool finds 0.40 % past 4. On the P900 file not fitted for it
  Lightroom's 1.31 % sit inside the tool's 3.7 % and match a limit of 5 to 6 best (overlap
  0.50, against 0.22 at 4), yet after the fit it still marks pixels at 3.95. Five rules were
  scored on the eight captures (the whole difference with and without the black point
  compensation, its colour part with and without, its lightness part without): none explains
  the three papers; the best on the P900 (no compensation, over 11) marks nothing on WhiteWall.
  Lightroom's rule stays unpublished (ADR 0081).

The owner asked for the recommended change on 2026-11-17, after the measurements.

## Decision

1. The fit aims at `GamutFitTargetDeltaE` (3.5): it moves every pixel whose whole colour
   difference after the compensated round trip (the rule of ADR 0081) is over 3.5 and lands it
   on the largest chroma that keeps the difference within 3.5. The table over L\* and hue is
   built at 3.5, and the judge of ADR 0093 is asked at 3.5 (`GamutTable.WithLimit`: the same
   grid and exact trip, a limit and a cache of its own). The knees and the recipe follow that
   edge.
2. The warning stays at `CurveGamutDeltaE` (4) everywhere it is shown: the Soft proof tab, the
   Editare Curbe figure and, in the Culori pentru hartie tab, the shares outside before and
   after, the two red layers, the dark share and the tone out of reach
   (`GamutFitTable.Warning`, `WarningOfPixel`). The share before is still the Soft proof tab's
   own; the share of pixels changed can exceed it, and the evidence says why.
3. A pixel over 3.5 at a tone whose grey is itself over 3.5 cannot reach the target. When the
   warning marks it, it is landed on the warning's own edge, as before this decision, where a
   chroma can do that, and left and counted as a tone out of reach only where none can; when
   the warning does not mark it, it is left and counted nowhere.

## Alternatives rejected

- **Leave the fit at 4 and document the difference.** The owner checks the file in Lightroom
  before ordering a print, and a file the tool calls clean was not clean there on two papers of
  three.
- **Lower the limit to 3.5 everywhere.** The warning would then mark more than Lightroom on an
  unfitted image (at 3.5 it marked 2.04 % against Lightroom's 0.93 % on the image of ADR 0081,
  overlap 0.353 against 0.417 at 4), and every gamut figure of V-008 and V-015 would move for
  no gain.
- **A reserve the user sets.** No number would be claimed, but every paper would need the trip
  through Lightroom to find it, and 3.5 was right at once on the two papers where Lightroom
  marks anything. Kept as the next step if a paper needs more.
- **A limit derived from each profile.** Measured: the profile's own round-trip error does not
  predict where Lightroom stops (above).
- **3 instead of 3.5.** It clears the last points, and costs a quarter more pixels changed
  again (6.91 % against 5.52 % on WhiteWall) for at most 0.005 % of the image.

## Consequences

- R-187 states the behaviour; R-175 and R-176 are amended where they said the fit moves only
  what the warning marks (R-178 said nothing of it). `R187_...` in `GamutFitTests` guard the
  target, the warning's shares, the recipe's aim, the tone the target cannot reach with and
  without the facts, the table's own limit and cache, the run's wiring and the evidence's
  sentence, which is shown only when more pixels changed than the warning marks.
- A tone the target cannot reach is rare and dark: none of the pixels on the three baryta
  profiles measured, 0.05 % on the canvas profile (`paper-fit.md`).
- V-016's figures move with the target (pixels touched, chroma given up, the recipe); its
  share outside before stays V-008's. Every other validation figure stays.
- On a paper where Lightroom marks nothing (the lab's baryta) the fit gives up some colour for
  no visible gain: 2.70 % of the pixels changed instead of 1.49 % on the Smida TIFF, 0.29
  instead of 0.16 at the median.
- 3.5 was found on one image against two profiles and one reference program; a few points can
  remain in Lightroom after the fit. `docs/design/known-limitations.md` says so.
- The gamut table of a run now serves two limits; each has its own cache of exact trips.
