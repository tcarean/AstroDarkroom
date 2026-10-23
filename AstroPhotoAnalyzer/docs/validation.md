# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json`, read by `ValidationTests` (trait `Category=Validation`, not in the gate,
ADR 0004), and is explained here. A number changes only after a re-measurement, with a dated
line under History saying the old value, the new value and why; never by editing the expected
values to match (the kit's rule "No test edited to pass").

The data is two images, copies made by hand in `AstroPhotoAnalyzer.Tests/testdata/large/`, which
git ignores, listed with their SHA-256 in `AstroPhotoAnalyzer.Tests/testdata/manifest.json`:

- `de_printat.png`: the owner's Milky Way core, 5938 x 3888, 16-bit RGB PNG, 103 387 067 bytes,
  whose original is in `E:\Photografie\Claude`. No WCS: the classes are relative to the modelled
  sky and the dust is the darkest sky (ADR 0033; V-001 to V-003, V-005 and V-009).
- `starless_rbf0.5_stretch0.17.fit`: the same core, plate-solved by Siril 1.4.4 (`RA---TAN-SIP`,
  `CDELT` + `PC`, SIP of order 3), starless, VeraLux-stretched to a background of 0.17,
  5837 x 3867, three float planes, 270 869 760 bytes, from
  `E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida\Processed_With_1mmPTFE_Flats\CropAndPlateSolve\RBF0.5\preview\`.
  The image ADR 0025 was measured on; the dust and the sky come from the WCS and the catalogue
  (V-004).
- `Rosetta_starless_no_WCS.fit`: the owner's starless Rosetta nebula, 3551 x 2422, three float
  planes, 103 210 560 bytes, no WCS: the deep-sky frame the sky-relative classes were designed
  on (ADR 0033; V-010, V-011).
- `Orion_with_WCS.fit`: the owner's Orion nebula with the Running Man, 1564 x 2302, three float
  planes, 43 214 400 bytes, a WCS far from the plane: the frame whose peaks are over the curves'
  target (V-012).
- `Veil_Starless.fit`: the owner's starless Veil nebula, 5601 x 3692, three float planes,
  248 158 080 bytes, a WCS inside the band (b -14.6 to -3.6 over 9.6 x 6.4 degrees): the
  deep-sky frame the band path mistook for a Milky Way landscape (ADR 0037; V-013, V-014).

On a machine or in a worktree without the copies, the cases print `NOT MEASURED` and pass.

## How a case runs

Two kinds of case. `icc`: `IccPaperBlack.Read` on the profile the case names as its image, with the figures `icc.description`, `icc.readable`, `icc.blackLstar` and `icc.dmax`. `analysis`: `AnalysisRun.Execute` on the image with the parameters the case
names (`aspect`, `distanceM`, `dmax`, `medium`, `targetBg`; an absent one is the core's
default), exactly as the console runs it. `ValidationTests` reads from the result a fixed set of
figures, named by the record and field they come from (`tonal.pctBelowPaperBlack`,
`simulation.asIs.lstarDust`, `simulation.cloud1.name`, `stars.fwhmMedian`,
`size.recommendedWidthCm`), and prints them all as one JSON line, `MEASURED <id> {...}`, in the
test output and in the file `APA_VALIDATION_DUMP` names when it is set. A case's `expected`
lists the figures it guards: a number within the case's `tolerance` (absolute, in the figure's
own unit), an integer, a boolean or a string exactly. A case whose `expected` is `null` is
unmeasured: it prints what it found and passes.

```bash
dotnet test AstroPhotoAnalyzer.sln -c Release --filter "Category=Validation"
```

Or `/validate` in Claude Code, which prints the expected and measured figures of every case and
stops on a drift instead of editing them.

## Cases

| ID | Image, parameters | What it guards |
| --- | --- | --- |
| V-001 | the PNG; `dmax` 2.0: the full frame, the baryta of before ADR 0028 (paper black L\* 9.0), the natural distance | every figure of the reference run on the full frame: tones, L\*, the simulation (the dust from the darkest sky, L\* 4.2, the sky's median 8.9, under the paper black of 9.0 since ADR 0033, so the verdict is PROBLEMA; the recommended row 0.16, the exact target, the Dmax figures), noise, plateau, rounding, stress test, JPEG and TIFF, stars, background colour, the bottom-right corner, the size table's reference row, the overall verdict |
| V-002 | the PNG; `aspect` 50:35, `distanceM` 1.5 | the run of the acceptance record, the hallway scenario: the crop, the 70 cm row, the recommended width, the verdict |
| V-003 | the PNG; `dmax` 2.35 | the figure measured on 2026-10-01 for a baryta paper that reaches Dmax 2.35 on a good printer: 1.3 % of pixels below paper black, and, since ADR 0025, the dust (L\* 4.1) above paper black (L\* 4.0), so the image is print-safe as it is; since ADR 0029 the sky is under the visibility threshold; since ADR 0033 the dust is L\* 4.2 and the sky 8.9, and the recommended row is 0.16 (sky L\* 15.9); a guard on the paper-black maths more than on the image |
| V-004 | the plate-solved FITS; `dmax` 2.0 | the WCS, the classes and the catalogue (ADR 0026): 2062 dust patches of 21 840, the dust at L\* 7.9 (the 25 % percentile of the dust class) and the sky at 17.6 (the median of the sky class); 397 clouds in the frame, 114 cloud rows; the recommended row 0.19, the exact target 0.184 (0.205 at Dmax 1.9, 0.163 at 2.1), the Dmax from which it passes as is 2.06; the five darkest clouds by name (LDN 42 (B78) at 1.0, LDN 1769 (B275, B278) at 3.2, LDN 1793, LDN 572, LDN 559); and every other figure of the run |
| V-005 | the PNG; none: the full frame on the default medium, baryta at Dmax 2.4 (ADR 0028) | what the owner's paper is told: paper black L\* 3.60, 0.81 % of pixels under it, the dust (L\* 4.18, the darkest sky; the dust class, the Pipe's cores, is 0.6 % of the frame and does not decide, ADR 0033) above it, so the dust is print-safe as it is; the sky (L\* 8.9, the median of the sky class) is under the visibility threshold L\* 15 (ADR 0029), which it reaches at 0.152, so the recommended row is 0.16 (dust L\* 9.1, sky L\* 15.9, 30.7 % of pixels still under L\* 15); the exact target 0.093 (0.108 at Dmax 2.3, 0.079 at 2.5), the Dmax from which it passes as is 2.33; the overall verdict ATENTIE. Only the tonal and simulation figures and the verdict: the rest are V-001's |
| V-006 | the lab's profile `Hahnemuhle PRBaryta.icm`; kind `icc` | the Dmax read from a real v2 `lut16` printer profile (R-028): black at L\* 3.49 relative to paper white, Dmax 2.41, the figure ADR 0028 took baryta's default from; the name from the `desc` tag |
| V-007 | `HARMAN_Canvas.icc`; kind `icc` | a paper with a weak black: L\* 20.84, Dmax 1.49; through the perceptual table the same profile would answer 2.46 |
| V-008 | the plate-solved FITS; kind `curves`, `icc` `Hahnemuhle PRBaryta.icm` | measured 2026-10-05: the suggested curves with the classes (R-140 to R-145, ADR 0032): anchors 0.091, 0.169, 0.306, 0.543, 0.720; three suggestions, no `Maxima`; `Puternica` with the points 0.401, 0.753, 0.919, the five L\* from 7.9, 17.6, 33.2, 57.7, 74.6 to 7.9, 17.8, 43.4, 77.0, 90.8, a noise gain of 1.26 (2.04 LSB to 2.57) and 0.03 % clipped; 1.02 % of the pixels outside the baryta's gamut as is, 0.82 % after `Puternica`, per class; the saved FITS: every card kept, `BITPIX` -32, the same 270 869 760 bytes, the WCS read back, the input's hash unchanged |
| V-009 | the PNG; kind `curves` | measured 2026-10-05, re-measured 2026-10-06 (ADR 0033): the same without a WCS: the anchors from the sky-relative classes (0.057, 0.099, 0.216, 0.395, 0.593); four suggestions, `Maxima` with the peaks at 0.920, a gain of 1.29 and 0.33 % clipped against 0.10 %; one paper check on each (the sky at L\* 8.9); the saved file a float FITS of 5938 px, no WCS, the PNG's hash unchanged |
| V-010 | the Rosetta FITS; none | measured 2026-10-06 (ADR 0033): the sky-relative classes on a deep-sky frame without a WCS: 8325 patches, no cloud; the dust class 18 patches (0.2 %, under the 5 % floor), so the dust is the darkest sky, L\* 6.1, and the sky 8.1; paper black 3.6 on baryta; the recommended row 0.16 (the sky reaches L\* 15 at 0.154), the exact target 0.067 (0.079 at Dmax 2.3, 0.055 at 2.5), passes as is from Dmax 2.17; the overall verdict ATENTIE; and every other figure of the run |
| V-011 | the Rosetta FITS; kind `curves` | measured 2026-10-06: the curves anchored on the sky-relative classes: anchors 0.077, 0.093, 0.272, 0.419, 0.603; four suggestions, `Puternica` with the points 0.409, 0.628, 0.793 and the five L\* from 6.1, 8.1, 30.2, 46.0, 63.5 to 6.2, 8.3, 42.9, 61.2, 77.6, a gain of 1.06; `Maxima` to 0.920 with 0.72 % clipped against 0.17 %; one paper check on each (the sky at L\* 8.1); the noise figure n/a (too little flat sky) |
| V-012 | the Orion FITS; kind `curves` | measured 2026-10-06: a frame at the ceiling: anchors 0.114, 0.137, 0.365, 0.738, 0.971; the peaks over `CurvePeakTarget`, so no suggestion and the problem sentence naming the anchor, 0.971 and the target 0.92 |
| V-013 | the Veil FITS; none | measured 2026-10-11 (ADR 0037): the sky path on a frame inside the band but narrower than it: method `Sky` for the reason `NarrowField`, the patch centres spanning 10.9 degrees of latitude; sky 96.4 %, nebula 2.9 %, bright nebula 0.5 %, peaks 0.1 %; 20 125 patches, 6 clouds named (LDN 873 first, 68 patches, L\* 7.6), the dust class 1 patch, so the dust is the darkest sky, L\* 6.3, and the sky 9.3; the recommended row 0.16 (the sky reaches L\* 15 at 0.155), the exact target 0.074, passes as is from Dmax 2.16; no smooth tile for the noise; 22 485 stars at FWHM 2.18 px; and every other figure of the run |
| V-014 | the Veil FITS; kind `curves` | measured 2026-10-11: the curves anchored on the nebula's own classes: anchors 0.078, 0.102, 0.166, 0.359, 0.498; four suggestions, `Puternica` taking the five L\* from 6.3, 9.3, 17.2, 38.8, 53.2 to 6.3, 10.0, 23.1, 49.4, 63.6 with a noise gain of 1.20, `Maxima` the peaks to 0.92 with a gain of 1.47; one paper check (the sky under L\* 15); the save keeps all 93 cards and the WCS |

The figures recorded for each case are in `validation.json`; the table below, from the reference
run of 2026-09-21, gives the same measurements made by hand in the originating conversation.

## The reference run

Until 2026-10-02 the rest of this file was `docs/acceptance-de-printat.md`, moved here unchanged
(ADR 0001).

The tool was built to reproduce, and then automate, the measurements made by hand (Python
/ NumPy / ImageMagick) on one image in the conversation that started it:
`de_printat.png`, a Milky Way core, 5938 x 3888, 16-bit RGB PNG, sRGB (`sRGB-elle-V2`),
98.6 MB. The image is not in the repo. This file records the run of the finished tool on
it, next to the hand-made numbers.

Run: `AstroPhotoAnalyzer.exe de_printat.png --aspect 50:35 --distance 1.5` (the hallway
scenario from the conversation), version 1.0.0, 11 seconds on the whole 23 MP.

### Numbers

| measurement | by hand (conversation) | tool | note |
|---|---|---|---|
| means per channel (0..255) | 32.2 / 29.6 / 27.9 | 32.2 / 29.6 / 27.9 | identical |
| % below level 32 / 64 | 73.8 / 96.3 | 74.1 / 96.3 | `<` on float vs `<` on the high byte |
| reference percentiles p0.1 / p1 / p50 / p90 / p99 / p99.9 | 3.53 / 12.93 / 26.15 / 43.87 / 80.51 / 115.1 | 3.5 / 13.0 / 26.2 / 44.0 / 80.7 / 115.5 | stride 2 vs full |
| L\* median; % < L\*9; % < L\*15; % > L\*50 | 9.4; 44.4; 81.6; 0.4 | 9.3; 45.6; 82.8; 0.3 | hand: on the 50x35 crop; tool: full frame |
| lift to background 0.19: L\*; % < 9; % < 15 | 20.4; 0.5; 6.7 (gamma 0.721 from an assumed 0.10) | 19.9; 0.5; 8.0 (gamma 0.730 from the measured 0.103) | the tool measures the background instead of assuming it; since ADR 0025 the table is the grid 0.10..0.20 and 0.19 is a row of it |
| noise sigma: median / 10th pct / min (LSB) | 0.48 / 0.36 / 0.32 | 0.45 / 0.33 / 0.30 | different random tiles (134 vs 128 smooth of 400) |
| % smooth tiles below 0.5 LSB | 61 | 65.6 | |
| plateau on quantised sky | 2.6 px (120 tiles) | 2.7 px (20 tiles) | only 20 tiles met the strict criteria in 5000 attempts; same answer |
| 16 -> 8 rounding: mean / max | 0.250 / 0.6 | 0.249 / 0.498 | hand max was on a differently rounded reference |
| shadow-lift stress: from 16 bits / from 8 bits | 111 used, 0 missing / 60 used, 46 missing | 111 / 0 and 60 / 46 | identical |
| JPEG q100 / q95 / q90 size | 13.4 / 3.2 / 1.7 MB (crop) | 12.5 / 3.2 / 1.9 MB (full) | ImageSharp encoder vs ImageMagick |
| JPEG mean error | 0.634 / 0.727 / 0.818 | 0.604 / 0.710 / 0.782 | |
| JPEG max error | 4.5 / 26.0 / 56.2 | 4.5 / 27.0 / 57.2 | |
| q100 samples with error > 2, mean level | 3.03 %, 31 | 1.2 %, 31 | encoder difference; same location (dark sky) |
| TIFF 8-bit LZW size | 20.1 MB (ImageMagick, crop) | 20.7 MB (ImageSharp, horizontal predictor, full) | without the predictor ImageSharp gave 38.6 MB |
| star FWHM median (Q1, Q3), count | 3.00 (2.69, 3.61), 193 | 3.13 (2.87, 3.49), 7166 | hand: 20 000 random candidates above 3 x mean; tool: every candidate above mean + min amplitude |
| background R / G / B | 19.2 / 20.6 / 18.9 | 19.2 / 20.6 / 18.9 | identical |
| G - (R+B)/2; R - B | +1.58; +0.31 | +1.6; +0.3 | |
| bottom-right corner R / G / B | 28.6 / 22.3 / 9.0 | 28.6 / 22.3 / 8.9 | |
| crop to 50:35 | 5554 x 3888, 192 px per side | 5554 x 3888, 192 px, 6.5 % | identical |
| 70 cm row | 215 dpi, star 0.35 mm, point from 1.2 m (full frame, FWHM 3.0) | 202 dpi, 0.39 mm, 1.36 m (the crop, FWHM 3.13) | consistent |

### What the tool said that the conversation also said

Verdict PROBLEMA (ATENTIE since ADR 0025: the sky at L\* 9.3 is above paper black and only the
dust is under it). Send TIFF 8-bit LZW; if only JPEG, q100 and never below (q95 max
error 27). No visible banding on 8 bits (plateau 0.34 mm at 202 dpi). Background too low
for baryta - 45.6 % under paper black - re-stretch from linear to 0.19 (0.17 since ADR 0025,
the lowest grid row that puts the darkest 1 % of the patches on paper black). Green cast of
+1.6 on the background. Warm bottom-right corner (R +9.4, B -9.9) and warm bottom edge:
horizon glow, fix on linear or keep as composition. Recommended 70 x 49 cm at 1.5 m; the
50:35 target cuts 192 px per side. Denoise pressed hard for print: sigma 0.45, target
0.8-1.5, and after the re-stretch it becomes ~0.61 (0.57 at 0.17). Keep the 16-bit master: 46
levels missing from 8 bits after a shadow lift, none from 16.

### What the real image taught the tool

- **Edge regions that brighten neutrally are the object, not a gradient.** The first run
  flagged all eight edge regions as "residual gradient" because the Milky Way band enters
  the top-left corner (+11 / +10 / +16). The rule now separates coloured departures
  (channels move differently: light pollution, horizon glow - Attention) from neutral
  ones (channels move together: most likely the object - informative). On this image that
  leaves exactly the two warm regions the conversation had found.
- **FWHM in the centre region reads 4.27 px** against 2.8-3.3 in the corners: the core is
  nebulosity, stars there sit on a bright, structured base and the border median
  overestimates it. The corner/centre ratio (0.77) is therefore not a sharpness statement
  on this image; the global median (3.13) is.
- **The plateau tiles are scarce here** (20 of 5000 attempts): a frame this full of
  structure has little sky that is flat to 6 levels. The number still matches the hand
  measurement.
- **ImageSharp's LZW needs the horizontal predictor** to be comparable to ImageMagick's.
- **Run without `--distance`, the first version recommended 50 x 33 cm** where the
  conversation had said "up to 100 cm at the natural distance, 70 for the hallway". At
  the natural distance the point-or-disc test is size-independent (the ratio is 1.018 for
  this image, a constant), so every row failed by 2 % and the rule fell back to the
  smallest. The rule now carries a 15 % tolerance there, recommends the largest
  non-oversize size when the image passes, and says in the report that the size is the
  wall's choice and that a given distance is what separates the sizes. With `--distance
  1.5` (the hallway) the recommendation is 70 cm on the 50:35 crop, as in the
  conversation; on the full frame it is 80 (FWHM 3.13 instead of 3.00).
- **The dust is a continuum, not a level, and the fraction is a choice** (ADR 0025, measured
  on the parked branch and on the plate-solved FITS): the cores of the Pipe at L\* 1 to 3, the
  lanes at 4, the Great Rift at 5 to 8, the sky at 9 on the PNG. The catalogue replaces the
  fraction of the frame with the clouds themselves; without a WCS the 1 % rule stays.
- **A real stretch undercuts the gamma curve.** The plate-solved FITS is a VeraLux stretch to
  0.17 and its dust sits at L\* 7.9, under paper black, where the gamma curve from the PNG's
  0.103 predicted the dust above black from 0.14 (the Rift) to 0.17 (the 1 %). The simulation
  is a simulation; its exact target is a lower bound.

## The figures of 2026-10-02

`validation.json` holds every figure the build of 2026-10-02 measured on the images: 177 for
V-001, 36 for V-002, 8 for V-003 and 197 for V-004, in 85 s for the four cases. On the PNG they
reproduce, to the last decimal the record printed, the "tool" column of the table above
wherever ADR 0025 did not change the rule: the means and the shares below 32 and 64, the
percentiles, the L\* figures, the noise (128 smooth tiles, median 0.45 LSB), the plateau
(2.68 px from 20 tiles), the rounding, the stress test, the JPEG and TIFF sizes and errors, the
stars (7163 since ADR 0041; 7166 before it, median 3.133 px, centre 4.27 px), the background colour and the bottom-right
corner, the crop and the 70 cm row on 50:35, and the 70 cm recommended at 1.5 m.

One figure does not. The natural ratio, the distance from which a star is a point over the
natural viewing distance, is given as 1.018 for this image by the record and by
`design/measurements.md`; the build of 2026-10-02 measures 1.0147 on the full frame and 1.0623 on
the 50:35 crop. With the median FWHM of 3.133 px, the formula in `design/measurements.md` gives
1.0147 on the full frame, so the recorded figures follow the code; how the record's 1.018 was
obtained is not written down. Left open for the owner on 2026-10-02.

V-003 adds a figure the record does not have: at Dmax 2.35, paper black is L\* 4.03 and 1.34 % of
the pixels fall below it; the dust (L\* 4.09 by the fallback rule) is above it, so the as-is row
is the recommended one and the overall verdict is ATENTIE (the size section).

V-004, the plate-solved FITS, measured on 2026-10-02 by the build of ADR 0026: 397 clouds have a
patch in the frame (the hand measurement with astropy, which honoured the SIP terms, found 406;
the nine are edge clouds whose one or two patches move by a few pixels), 114 clouds have 9 or
more; the classes put 2062 of 21 840 patches in the dust (the hand measurement with scipy's
filters: 9.3 % against the core's 9.4 %), the dust at L\* 7.90 (hand: 7.5, the two smoothings
differ) and the sky at 17.58 (hand: 17.6); the recommended row is 0.19 (dust 9.6, sky 19.7), the
exact target 0.184 (0.205 at Dmax 1.9, 0.163 at 2.1), and the image passes as it is from Dmax
2.06. Under ADR 0025's cloud-disc dust the same build measured the dust at 7.89 and the sky at
18.08, with the same recommendation. The darkest clouds: LDN 42 (B78) at 1.0 (11 patches, target 0.39),
LDN 1769 (B275, B278) at 3.2 (94 patches, 0.27), LDN 1793 at 3.7, LDN 572 at 4.6, LDN 559 at 4.7.

## History

- 2026-10-23 (ADR 0042, the gamut figure decided by the exact round trip of the 8-bit colour
  near the threshold; `docs/plans/2026-10-23-fix-the-defects-the-analyzer-review-found.md`,
  "Item 9, implemented"), approved by the owner on the comparison with the exact per-pixel round
  trip: V-008's twenty gamut figures, the peaks (0) excepted. As is `curves.before.gamut.all`
  1.0227 -> 1.3563 (exact 1.348), `.dust` 2.6719 -> 4.2609 (4.216), `.sky` 0.4487 -> 0.7187
  (0.713), `.band` 1.7775 -> 1.7945 (1.794), `.clouds` 0.5818 -> 0.5635 (0.571); `Blanda` all
  0.9294 -> 1.2852 (1.276), dust 2.5089 -> 4.1336 (4.086), sky 0.4093 -> 0.6988 (0.690), band
  1.5754 -> 1.6117 (1.616), clouds 0.4684 -> 0.4757 (0.465); `Medie` all 0.8773 -> 1.2432
  (1.230), dust 2.4317 -> 4.0623 (4.006), sky 0.3904 -> 0.6885 (0.676), band 1.4491 -> 1.4982
  (1.499), clouds 0.4172 -> 0.4025 (0.406); `Puternica` all 0.8247 -> 1.1992 (1.188), dust
  2.3521 -> 3.9676 (3.915), sky 0.3678 -> 0.6792 (0.671), band 1.326 -> 1.3781 (1.376), clouds
  0.4208 -> 0.4172 (0.417). The grid alone was 0.3 points low in all and 1.5 to 1.6 on the dust
  class; the new figures are within 0.06 points of exact, the residue being the 8-bit
  quantisation of the colours near the threshold. No other case moved.

- 2026-10-23 (the fixes of the area-2 code review, `docs/plans/2026-10-23-fix-the-defects-the-analyzer-review-found.md`;
  ADRs 0039 to 0044), approved by the owner on the drift table of the plan's record: 146 of
  1696 figures moved, none of the dust, the sky, the exact targets or the verdicts. The renamed
  keys, the same values: `simulation.alreadyPrintSafe` -> `simulation.dustOnPaperBlackAsIs`,
  `simulation.dmaxToPassAsIs` -> `simulation.dmaxDustOnPaperBlackAsIs` (the dust's figures,
  ADR 0043), `crop.cutPerSidePx` -> `crop.cutStartPx` plus the new `crop.cutEndPx` (0 / 0, and
  192 / 192 on V-002). The dark shares count the 8-bit level an export writes (ADR 0043):
  `tonal.pctBelowDarkA` / `B` 74.053 -> 73.0336 and 96.3479 -> 96.252 on the PNG (V-001, V-002,
  V-003, V-005), 17.8327 -> 16.9419 and 88.3661 -> 88.1052 on the FITS (V-004), 72.4919 ->
  71.4654 and 91.728 -> 91.5987 on Rosetta (V-010), 82.906 -> 81.2761 and 99.4746 -> 99.4663 on
  the Veil (V-013). A flat two-pixel star peak counts once (ADR 0041): on the PNG `stars.count`
  7166 -> 7163, `fwhmMedian` 3.133 -> 3.1332, `fwhmQ1` 2.868 -> 2.8685, `fwhmQ3` 3.4901 ->
  3.4899, the centre 4.2711 -> 4.2709 and the top-left corner 2.8198 -> 2.82, and through them
  `size.naturalRatio` 1.0147 -> 1.0148 (1.0623 -> 1.0624 on V-002) and the size rows' `starMm`
  and `minDistanceM` by 0.0001 to 0.0002. Maxima's factor is capped under the spline's overshoot
  (ADR 0039): V-009 `curves.Maxima.point.peaks` 0.92 -> 0.8917 (band 0.3504 -> 0.3417, clouds
  0.684 -> 0.6638; L* after 37.6936 / 68.5218 / 82.9249 -> 36.7604 / 66.6896 / 81.4566, the dust
  and the sky by hundredths; `noiseGain` 1.3096 -> 1.2897, `noiseAfterLsb` 0.5917 -> 0.5827,
  `pctClipped` 0.3227 -> 0.1767); V-011 peaks 0.92 -> 0.8992 (band 0.456 -> 0.4469, clouds
  0.7152 -> 0.6995; L* 47.6964 / 68.4645 / 86.3281 -> 46.7735 / 67.1502 / 84.9267; gain 1.0966
  -> 1.0918; clipped 0.7171 -> 0.3917); V-014 peaks 0.92 -> 0.8277 (band 0.2718 -> 0.2526,
  clouds 0.7155 -> 0.6463; L* 30.961 / 69.3605 / 83.2141 -> 28.5383 / 63.2578 / 77.9226, sky
  11.0812 -> 10.7444; gain 1.4658 -> 1.3815; clipped 0.1235 -> 0.0263). V-004's clouds (397,
  114 rows, every cloud figure), every `keep16BitMaster` and `monotone`, V-002's verdict, V-006
  to V-008 and V-012: no drift. The two speed changes of the same task (the selection in
  `Stats`, the ladder's early stop) were measured before the fixes: all 1689 figures identical.
  Found while rewriting the file: V-003's `expected` listed seven keys twice with the same values
  (`simulation.skyReason`, `latitudeSpanDeg`, the five `pctPatches.*`); the second copies are
  gone, a change the owner did not ask for and is told of here.

- 2026-10-23 (the documentation backfill, area 2, `Core/Analysis/`): comments only, every
  changed C# file's code token for token as on `main`. Re-run on the branch, the seven files of
  the manifest copied into the worktree: the fourteen cases' `MEASURED` lines, 1237 figures, are
  identical to the recorded ones (the largest difference 0). The fourteen cases pass, in 2.2 min.
- 2026-10-22 (the documentation backfill, area 1, `Core/Input/`, with the XML documentation file
  of ADR 0038): comments only, every changed C# file's code token for token as on `main`. Re-run
  on the branch, the seven files of the manifest copied into the worktree: the fourteen cases'
  `MEASURED` lines, 1237 figures, are identical to the recorded ones (the largest difference 0).
  The fourteen cases pass.
- 2026-10-11 (ADR 0037, the band path needs a frame at least as wide as the band): a third
  condition before the band classes, the patch centres spanning at least 20 degrees of
  galactic latitude. `Veil_Starless.fit` added to the manifest, V-013 and V-014 recorded from
  their `MEASURED` lines: on the old code the Veil was on the band path (65 % "banda", dust
  L\* 7.1 from the darkest sky, sky 9.5, the recommended row 0.16); on the new one the sky
  path, dust 6.3, sky 9.3, the same row, and the curves' anchors on the nebula's classes. The
  twelve earlier cases did not move: the reference image spans 68 degrees of latitude, the
  others have no band in the frame. The fourteen cases pass. After the code review the
  analysis cases also record why the path was taken and what the map holds:
  `simulation.skyReason`, `simulation.latitudeSpanDeg` (`n/a` without a WCS) and the five
  `simulation.pctPatches.*` shares, added to V-001 to V-005, V-010 and V-013 from their
  `MEASURED` lines, every other figure unchanged.
- 2026-10-09 (ADR 0035, the lift table): the simulation linearises each lift through one
  16-bit table instead of a lifted raster, and a grid row no longer sorts the L\* plane. No
  figure moved: the twelve cases' `MEASURED` lines, 1365 figures, are identical before and
  after, and the console report on the plate-solved FITS is byte-identical. Time on
  `starless_rbf0.5_stretch0.17.fit` (Release, `--dmax 2.0`, the owner's machine, median of
  three): the console run 27.2 s -> 14.6 s; the simulation alone (`PrintStretchSimulator.Simulate`,
  a `Stopwatch` in a scratch test) 17.1 s -> 2.6 s, and on `de_printat.png` 15.7 s -> 2.75 s;
  the twelve validation cases 3.7 min -> 2.5 min (V-004 26 s -> 13 s, V-001 24 s -> 11 s).
  The scratch probe over every ladder step of both images: no patch moved by more than
  7.63e-6 L\*, one float ulp.
- 2026-10-06, after the code review of ADR 0033's branch: the sky-path thresholds became
  offsets from the model instead of the residual's median, and the Otsu split lost the peak
  patches and gained a floor of 5 % of the tail; the dust's source is decided once, as is. The
  sky-path cases moved by small amounts: V-001, V-003, V-005 `simulation.dustPatchCount` 189 ->
  128, `simulation.asIs.lstarDust` 4.2341 -> 4.1843, `simulation.asIs.lstarSky` 8.9169 ->
  8.8743, the exact targets and `dmaxToPassAsIs` (2.3291 -> 2.3342) with them; V-009 anchors
  0.0579 / 0.0993 / 0.2234 / 0.4084 -> 0.0574 / 0.0989 / 0.216 / 0.3945 and the per-curve
  figures; V-010 `dustPatchCount` 31 -> 18, `lstarSky` 8.1055 -> 8.0849; V-011 anchors 0.2791 /
  0.4271 -> 0.2723 / 0.4194; V-012 anchors 0.3751 / 0.7395 -> 0.3654 / 0.7378. V-002, V-004,
  V-006 to V-008: no drift. The verdicts did not change.
- 2026-10-06 (ADR 0033, the sky-relative classes and the one measurement): on `de_printat.png`
  (no WCS) V-001, V-003 and V-005 moved on the simulation figures: `simulation.method` NoWcs ->
  Sky, `simulation.dustPatchCount` 22385 (all patches) -> 189 (the dust class, the Pipe's
  cores, under the 5 % floor), `simulation.asIs.lstarDust` 4.0925 -> 4.2341 (the darkest 1 %
  of the sky class instead of all patches), `simulation.asIs.lstarSky` 9.3816 -> 8.9169 (the
  median of the sky class, the band's patches no longer in it), `simulation.recommendedTarget`
  0.17 (V-001) and 0.15 (V-003, V-005) -> 0.16, `simulation.exactTarget` by 0.002 to 0.003,
  `simulation.dmaxToPassAsIs` 2.3438 -> 2.3291, `simulation.visibilityTarget` 0.1479 -> 0.1522
  (V-005), the primary row's figures with the row; V-001 `simulation.skyBelowPaperBlack` False
  -> True and `overall` ATENTIE -> PROBLEMA: on the Dmax 2.0 paper the sky's median, 8.92, sits
  a tenth under paper black. V-009 re-measured on the class anchors: `curves.source` ->
  `curves.method` Sky, the anchors 0.0563 / 0.1029 / 0.2138 / 0.3769 / 0.5768 -> 0.0579 /
  0.0993 / 0.2234 / 0.4084 / 0.5932 and every per-curve figure with them (`Maxima`'s gain
  1.3781 -> 1.2882, clipped 0.3679 -> 0.3256). V-004 and V-008 (the band path): no figure
  moved; `simulation.method` Classes -> Band and `curves.source` -> `curves.method` Band are
  the renamed enum and key. V-002: no drift. V-010 to V-012 added on the two new images.
- 2026-10-02: the acceptance record became this file (ADR 0001); V-001 to V-003 recorded from
  the build of the same day, by `ValidationTests`' `MEASURED` lines; the natural ratio differs
  from the record (above).
- 2026-10-02 (ADR 0025, the dust and the sky): the verdict changed rule, so V-001 to V-003 were
  re-measured and their simulation figures re-recorded, by the `MEASURED` lines of the new
  build. V-001: `simulation.recommendedTarget` 0.19 -> 0.17, `simulation.primary.target`
  0.19 -> 0.17, `simulation.primary.gamma` 0.73 -> 0.7788, `simulation.primary.pctBelowLowSeparation`
  8.016 -> 19.3147, `overall` PROBLEMA -> ATENTIE (the sky, L\* 9.25, is above paper black; only
  the dust, L\* 4.1, is under it); the new figures `simulation.asIs.lstarDust` 4.09,
  `simulation.asIs.lstarSky` 9.25, `simulation.exactTarget` 0.1618, `simulation.dmaxToPassAsIs`
  2.34. V-002: `overall` PROBLEMA -> ATENTIE. V-003: `simulation.recommendedTarget` none -> 0.1028
  (the as-is row). The figures `tonal.printSafe`, `tonal.backgroundTooLowForPaper`,
  `simulation.primary.lstarMedian` and `simulation.primary.pctBelowPaperBlack` no longer exist
  (`simulation.primary.lstarSky` and `pctPatchesBelowPaperBlack` replace the last two). V-004
  added, on the plate-solved FITS, 197 figures.
- 2026-10-02 (ADR 0026, the classes): V-004 re-measured; `simulation.method` Catalogue ->
  Classes, `simulation.dustPatchCount` 3980 -> 2062, `simulation.asIs.lstarDust` 7.8911 -> 7.90,
  `simulation.asIs.lstarSky` 18.0835 -> 17.5767, `simulation.primary.lstarSky` 20.2343 ->
  19.7143, the exact targets at Dmax 1.9 / 2.1 by a thousandth. V-001 to V-003 (no WCS) did not
  move. Re-run after the code review's fixes (NaN clipping, the patch grid helper): the four cases
  passed with no drift.
- 2026-10-05 (ADR 0028, baryta's Dmax 2.0 -> 2.4): on the new default V-001 and V-004 drifted on
  every Dmax-dependent figure and on nothing else (V-001: `tonal.paperBlackLstar` 8.9914 -> 3.5961,
  `tonal.pctBelowPaperBlack` 45.6296 -> 0.8104, `simulation.alreadyPrintSafe` False -> True,
  `simulation.recommendedTarget` 0.17 -> 0.1, `simulation.exactTarget` 0.1622 -> 0.0942; V-004:
  `tonal.pctBelowPaperBlack` 4.7305 -> 0.5048, `simulation.recommendedTarget` 0.19 -> 0.12,
  `simulation.exactTarget` 0.1836 -> 0.1102, the five cloud targets 0.3879..0.232 -> 0.2879..0.1502).
  By the owner's decision no recorded figure changed: both cases now name the paper they were
  recorded on (`dmax` 2.0), which keeps the re-stretch path guarded on the real images, and V-005
  was added on the default medium, 43 figures from its `MEASURED` line. V-002 and V-003 did not
  move. The five cases pass.
- 2026-10-05 (ADR 0029, the visibility threshold): the recommended row now also needs the sky at
  L\* 15, so the two cases whose recommended row had the sky under it were re-recorded, by the
  owner's word, from the `MEASURED` lines of the new build. V-003: `simulation.recommendedTarget`
  0.1028 -> 0.15, `simulation.recommendedIsAsIs` True -> False, `simulation.primary.target`
  0.1028 -> 0.15, `.gamma` 1 -> 0.8339, `.lstarDust` 4.0925 -> 7.7976, `.lstarSky` 9.3816 ->
  15.2617, `.pctPatchesBelowPaperBlack` 0.9292 -> 0.1876, `.pctBelowLowSeparation` 82.7636 ->
  48.0471. V-005 (recorded earlier the same day): `simulation.recommendedTarget` 0.1 -> 0.15,
  `simulation.primary.target` 0.1 -> 0.15, `.gamma` 1.0121 -> 0.8339, `.lstarDust` 3.924 ->
  7.7976, `.lstarSky` 9.0246 -> 15.2617, `.pctPatchesBelowPaperBlack` 0.6388 -> 0.143,
  `.pctBelowLowSeparation` 83.8455 -> 48.0471; the new figures `simulation.skyBelowVisibility`
  True and `simulation.visibilityTarget` 0.1479. V-001, V-002 and V-004 did not move: their
  recommended rows already had the sky above L\* 15. The five cases pass.
- 2026-10-05 (ADR 0030, the Dmax from an ICC profile): V-006 and V-007 added, kind `icc`, on the
  two profiles of the lab's page, copied into `testdata/large/` beside the images. Recorded from
  their `MEASURED` lines: `Hahnemuhle PRBaryta` L\* 3.4903, Dmax 2.41; `HARMAN Canvas` L\* 20.8407,
  Dmax 1.49, the figures the hand-written reader of ADR 0028 gave. The seven cases pass.
