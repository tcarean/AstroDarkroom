# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json`, read by `ValidationTests` (trait `Category=Validation`, not in the gate,
ADR 0004), and is explained here. A number changes only after a re-measurement, with a dated
line under History saying the old value, the new value and why; never by editing the expected
values to match (the kit's rule "No test edited to pass").

The data is two images, copies made by hand in `AstroPhotoAnalyzer.Tests/testdata/large/`, which
git ignores, listed with their SHA-256 in `AstroPhotoAnalyzer.Tests/testdata/manifest.json`:

- `de_printat.png`: the owner's Milky Way core, 5938 x 3888, 16-bit RGB PNG, 103 387 067 bytes,
  whose original is in `E:\Photografie\Claude`. No WCS: the dust and the sky are measured by the
  fallback rule (V-001 to V-003).
- `starless_rbf0.5_stretch0.17.fit`: the same core, plate-solved by Siril 1.4.4 (`RA---TAN-SIP`,
  `CDELT` + `PC`, SIP of order 3), starless, VeraLux-stretched to a background of 0.17,
  5837 x 3867, three float planes, 270 869 760 bytes, from
  `E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida\Processed_With_1mmPTFE_Flats\CropAndPlateSolve\RBF0.5\preview\`.
  The image ADR 0025 was measured on; the dust and the sky come from the WCS and the catalogue
  (V-004).

On a machine or in a worktree without the copies, the cases print `NOT MEASURED` and pass.

## How a case runs

One kind of case, `analysis`: `AnalysisRun.Execute` on the image with the parameters the case
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
| V-001 | the PNG; none: the full frame, baryta, the natural distance | every figure of the reference run on the full frame: tones, L\*, the simulation (the dust by the fallback rule, the recommended row, the exact target, the Dmax figures), noise, plateau, rounding, stress test, JPEG and TIFF, stars, background colour, the bottom-right corner, the size table's reference row, the overall verdict |
| V-002 | the PNG; `aspect` 50:35, `distanceM` 1.5 | the run of the acceptance record, the hallway scenario: the crop, the 70 cm row, the recommended width, the verdict |
| V-003 | the PNG; `dmax` 2.35 | the figure measured on 2026-10-01 for a baryta paper that reaches Dmax 2.35 on a good printer: 1.3 % of pixels below paper black, and, since ADR 0025, the dust (L\* 4.6) above paper black (L\* 4.0), so the as-is row is the recommended one; a guard on the paper-black maths more than on the image |
| V-004 | the plate-solved FITS; none | the WCS and the catalogue: 397 clouds in the frame, 3980 cloud patches of 21 840, 114 cloud rows; the dust at L\* 7.9 and the sky at 18.1; the recommended row 0.19, the exact target 0.184 (0.205 at Dmax 1.9, 0.164 at 2.1), the Dmax from which it passes as is 2.06; the five darkest clouds by name (LDN 42 (B78) at 1.0, LDN 1769 (B275, B278) at 3.2, LDN 1793, LDN 572, LDN 559); and every other figure of the run |

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
stars (7166, median 3.133 px, centre 4.27 px), the background colour and the bottom-right
corner, the crop and the 70 cm row on 50:35, and the 70 cm recommended at 1.5 m.

One figure does not. The natural ratio, the distance from which a star is a point over the
natural viewing distance, is given as 1.018 for this image by the record and by
`design/measurements.md`; the build of 2026-10-02 measures 1.0147 on the full frame and 1.0623 on
the 50:35 crop. With the median FWHM of 3.133 px, the formula in `design/measurements.md` gives
1.0147 on the full frame, so the recorded figures follow the code; how the record's 1.018 was
obtained is not written down. Left open for the owner on 2026-10-02.

V-003 adds a figure the record does not have: at Dmax 2.35, paper black is L\* 4.03 and 1.34 % of
the pixels fall below it; the dust (L\* 4.6 by the fallback rule) is above it, so the as-is row
is the recommended one and the overall verdict is ATENTIE (the size section).

V-004, the plate-solved FITS, measured on 2026-10-02 by the build of ADR 0025: 397 clouds have a
patch in the frame (the hand measurement with astropy, which honoured the SIP terms, found 406;
the nine are edge clouds whose one or two patches move by a few pixels), 3980 of 21 840 patches
are cloud patches, 114 clouds have 9 or more; the dust is at L\* 7.89 (astropy: 7.63, the same
SIP difference at the frame's edge) and the sky at 18.08; the recommended row is 0.19 (dust
9.6, sky 20.2), the exact target 0.184 (0.205 at Dmax 1.9, 0.164 at 2.1), and the image passes
as it is from Dmax 2.06. The darkest clouds: LDN 42 (B78) at 1.0 (11 patches, target 0.39),
LDN 1769 (B275, B278) at 3.2 (94 patches, 0.27), LDN 1793 at 3.7, LDN 572 at 4.6, LDN 559 at 4.7.

## History

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
