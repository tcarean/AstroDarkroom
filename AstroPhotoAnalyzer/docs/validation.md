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
  sky, the median of each cell since ADR 0084, and the dust is the darkest sky (ADR 0033;
  V-001 to V-003, V-005 and V-009).
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

The kinds of case, the fourth being `proof` (below). `icc`: `IccPaperBlack.Read` on the profile the case names as its image, with the figures `icc.description`, `icc.readable`, `icc.blackLstar` and `icc.dmax`. `analysis`: `AnalysisRun.Execute` on the image with the parameters the case
names (`aspect`, `distanceM`, `dmax`, `medium`, `targetBg`; an absent one is the core's
default), exactly as the console runs it. `ValidationTests` reads from the result a fixed set of
figures, named by the record and field they come from (`tonal.pctBelowPaperBlack`,
`simulation.asIs.lstarDust`, `simulation.cloud1.name`, `stars.fwhmMedian`,
`size.recommendedWidthCm`), and prints them all as one JSON line, `MEASURED <id> {...}`, in the
test output and in the file `APA_VALIDATION_DUMP` names when it is set. A case's `expected`
lists the figures it guards: a number within the case's `tolerance` (absolute, in the figure's
own unit), an integer, a boolean or a string exactly. A case whose `expected` is `null` is
unmeasured: it prints what it found and passes.

`proof` (R-160 to R-162, ADR 0066): the analysis with the profile the case names under `icc`, then
`ProofRun.Prepare` and a `ProofRun.Render` per intent, with the simulation off and on: the
profile's facts (`proof.paperWhite.L`, `.a`, `.b`, `proof.paperWhite.srgb`, `proof.paperBlackLstar`,
`proof.dmax`), the proof's width, the share outside the paper's gamut and, per intent and
state, the mean L\* of the proof (`proof.Relative.simulated.meanLstar`).

```bash
dotnet test AstroPhotoAnalyzer.sln -c Release --filter "Category=Validation"
```

Or `/validate` in Claude Code, which prints the expected and measured figures of every case and
stops on a drift instead of editing them.

## Cases

| ID | Image, parameters | What it guards |
| --- | --- | --- |
| V-001 | the PNG; `dmax` 2.0: the full frame, the baryta of before ADR 0028 (paper black L\* 9.0), the natural distance | every figure of the reference run on the full frame: tones, L\*, the simulation (the dust from the darkest sky, L\* 4.4 since ADR 0084 (4.2 under ADR 0033; the dust class is 7.8 % of the patches but its 25 % percentile, 6.1, is lighter than the darkest sky), the sky's median 9.0, a few hundredths over the paper black of 9.0, so the verdict is ATENTIE again (PROBLEMA under ADR 0033, when the sky was 8.9); the recommended row 0.16, the exact target, the Dmax figures), noise, plateau, rounding, stress test, JPEG and TIFF, stars (6274 at a median of 3.11 px since ADR 0054; 7163 at 3.13 before, the faint ones under 12 sigmas now left out), background colour, the bottom-right corner, the size table's reference row, the overall verdict |
| V-002 | the PNG; `aspect` 50:35, `distanceM` 1.5 | the run of the acceptance record, the hallway scenario: the crop, the 70 cm row, the recommended width, the verdict |
| V-003 | the PNG; `dmax` 2.35 | the figure measured on 2026-10-01 for a baryta paper that reaches Dmax 2.35 on a good printer: 1.3 % of pixels below paper black, and, since ADR 0025, the dust (L\* 4.1) above paper black (L\* 4.0), so the image is print-safe as it is; since ADR 0029 the sky is under the visibility threshold; since ADR 0033 the dust is L\* 4.2 and the sky 8.9, since ADR 0084 4.4 and 9.0, and the recommended row is 0.16 (sky L\* 16.1); a guard on the paper-black maths more than on the image |
| V-004 | the plate-solved FITS; `dmax` 2.0 | the WCS, the classes and the catalogue (ADR 0026): 2062 dust patches of 21 840, the dust at L\* 7.9 (the 25 % percentile of the dust class) and the sky at 17.6 (the median of the sky class); 397 clouds in the frame, 114 cloud rows; the recommended row 0.19, the exact target 0.184 (0.205 at Dmax 1.9, 0.163 at 2.1), the Dmax from which it passes as is 2.06; the five darkest clouds by name (LDN 42 (B78) at 1.0, LDN 1769 (B275, B278) at 3.2, LDN 1793, LDN 572, LDN 559); since ADR 0054, 5 stars on this starless frame (6297 at 2.38 px before: the grain) and the size table on the dpi rule of R-074, the reference row 80 cm; and every other figure of the run |
| V-005 | the PNG; none: the full frame on the default medium, baryta at Dmax 2.4 (ADR 0028) | what the owner's paper is told: paper black L\* 3.60, 0.81 % of pixels under it, the dust (L\* 4.40 since ADR 0084, 4.18 before: the darkest sky; the dust class is 7.8 % of the frame, the Rift and the Pipe, but its 25 % percentile, 6.1, is lighter than the darkest sky, so it does not decide; under ADR 0033 the class was the Pipe's cores, 0.6 %) above it, so the dust is print-safe as it is; the sky (L\* 9.0, the median of the sky class) is under the visibility threshold L\* 15 (ADR 0029), which it reaches at 0.152, so the recommended row is 0.16 (dust L\* 9.4, sky L\* 16.1, 30.7 % of pixels still under L\* 15); the exact target 0.090 (0.104 at Dmax 2.3, 0.076 at 2.5), the Dmax from which it passes as is 2.31; the overall verdict ATENTIE. Only the tonal and simulation figures and the verdict: the rest are V-001's |
| V-006 | the lab's profile `Hahnemuhle PRBaryta.icm`; kind `icc` | the Dmax read from a real v2 `lut16` printer profile (R-028): black at L\* 3.49 relative to paper white, Dmax 2.41, the figure ADR 0028 took baryta's default from; the name from the `desc` tag |
| V-007 | `HARMAN_Canvas.icc`; kind `icc` | a paper with a weak black: L\* 20.84, Dmax 1.49; through the perceptual table the same profile would answer 2.46 |
| V-008 | the plate-solved FITS; kind `curves`, `icc` `Hahnemuhle PRBaryta.icm` | measured 2026-10-05: the suggested curves with the classes (R-140 to R-145, ADR 0032): anchors 0.091, 0.169, 0.306, 0.543, 0.720; three suggestions, no `Maxima`; `Puternica` with the points 0.401, 0.753, 0.919, the five L\* from 7.9, 17.6, 33.2, 57.7, 74.6 to 7.9, 17.8, 43.4, 77.0, 90.8, a noise gain of 1.26 (2.04 LSB to 2.57) and 0.03 % clipped; 1.94 % of the pixels the baryta cannot hold as is (the whole colour difference after the compensated round trip over 4, ADR 0081, re-measured 2026-11-05; dust 11.1 %, sky 1.2 %, band 0.3 %, bright clouds 0.2 %), 2.00 % after `Puternica`, per class; the saved FITS: every card kept, `BITPIX` -32, the same 270 869 760 bytes, the WCS read back, the input's hash unchanged |
| V-009 | the PNG; kind `curves` | measured 2026-10-05, re-measured 2026-10-06 (ADR 0033): the same without a WCS: the anchors from the sky-relative classes (0.057, 0.099, 0.216, 0.395, 0.593); four suggestions, `Maxima` with the peaks at 0.920, a gain of 1.29 and 0.33 % clipped against 0.10 %; one paper check on each (the sky at L\* 8.9); the saved file a float FITS of 5938 px, no WCS, the PNG's hash unchanged |
| V-010 | the Rosetta FITS; none | measured 2026-10-06 (ADR 0033): the sky-relative classes on a deep-sky frame without a WCS: 8325 patches, no cloud; sky 80.9 %, nebula 8.8 % (over the model by 3.2 L\*, 3 times the sky's noise since ADR 0084), bright nebula 3.6 % (over 13.7); the dust class 554 patches (6.7 % since ADR 0084; 18 patches, 0.2 %, under ADR 0033), whose 25 % percentile is lighter than the darkest sky, so the dust is the darkest sky, L\* 6.1, and the sky 8.1; paper black 3.6 on baryta; the recommended row 0.16 (the sky reaches L\* 15 at 0.154), the exact target 0.067 (0.079 at Dmax 2.3, 0.055 at 2.5), passes as is from Dmax 2.17; the overall verdict ATENTIE; since ADR 0054, 1 star on this starless frame (2858 at 2.62 px before: the grain) and the size table on the dpi rule; and every other figure of the run |
| V-011 | the Rosetta FITS; kind `curves` | measured 2026-10-06: the curves anchored on the sky-relative classes: anchors 0.077, 0.093, 0.321, 0.445, 0.603 since ADR 0084 (0.272 and 0.419 for the nebula and the bright nebula under ADR 0033); four suggestions, `Puternica` with the points 0.480, 0.663, 0.812 and the five L\* from 6.1, 8.1, 34.8, 47.8, 63.5 to 6.2, 8.3, 49.6, 63.7, 78.8, a gain of 1.06; `Maxima` to 0.920 with 0.72 % clipped against 0.17 %; one paper check on each (the sky at L\* 8.1); the noise figure n/a (too little flat sky) |
| V-012 | the Orion FITS; kind `curves` | measured 2026-10-06: a frame at the ceiling: anchors 0.114, 0.137, 0.441, 0.755, 0.971 (0.365 and 0.738 for the nebula and the bright nebula under ADR 0033); the peaks over `CurvePeakTarget`, so no suggestion and the problem sentence naming the peaks' point, 0.971 and the target 0.92 (worded as of 2026-11-02, ADR 0076) |
| V-013 | the Veil FITS; none | measured 2026-10-11 (ADR 0037): the sky path on a frame inside the band but narrower than it: method `Sky` for the reason `NarrowField`, the patch centres spanning 10.9 degrees of latitude; sky 95.8 %, nebula 2.8 % (over the model by 2.5 L\*), bright nebula 0.5 %, peaks 0.1 % (since ADR 0084; 96.4 / 2.9 / 0.5 / 0.1 under ADR 0033); 20 125 patches, 6 clouds named (LDN 873 first, 68 patches, L\* 7.6), the dust class 167 patches (0.8 %, under the 5 % floor; 1 patch under ADR 0033), so the dust is the darkest sky, L\* 6.4 (6.3 before), and the sky 9.3; the recommended row 0.16 (the sky reaches L\* 15 at 0.155), the exact target 0.072, passes as is from Dmax 2.15; no smooth tile for the noise; 1 star since ADR 0054 (22 485 at FWHM 2.18 px before: the grain of a starless frame) and the size table on the dpi rule, the reference row 70 cm; and every other figure of the run |
| V-014 | the Veil FITS; kind `curves` | measured 2026-10-11: the curves anchored on the nebula's own classes: anchors 0.078, 0.102, 0.166, 0.359, 0.498; four suggestions, `Puternica` taking the five L\* from 6.3, 9.3, 17.2, 38.8, 53.2 to 6.3, 10.0, 23.1, 49.4, 63.6 with a noise gain of 1.20, `Maxima` the peaks to 0.92 with a gain of 1.47; one paper check (the sky under L\* 15); the save keeps all 93 cards and the WCS |
| V-015 | the plate-solved FITS; kind `proof`, `icc` `Hahnemuhle PRBaryta.icm` | measured 2026-10-30: the soft proof through the lab's baryta profile (R-160 to R-162, ADR 0066): the paper white of the `wtpt` tag L\* 97.19, a\* -0.01, b\* +1.11, shown as sRGB 248, 247, 245 under the simulation; the paper black L\* 3.49 at Dmax 2.41 (V-006); the proof 1946 px wide (stride 3); 1.90 % of its pixels outside the paper's gamut (ADR 0081, re-measured 2026-11-05; V-008 reads 1.94 % on the tonal subsample); the mean L\* of the proof 17.0 on `Perceptual`, 19.7 on `Relative`, 18.2 on `RelativeNoBpc` (18.0, 20.4 and 19.1 simulated): the three intents differ, and the compensated one keeps the most of the shadows |
| V-016 | the plate-solved FITS; kind `gamutfit`, `icc` `Hahnemuhle PRBaryta.icm` | measured 2026-11-06: the paper fit through the lab's baryta profile (R-175 to R-177, ADR 0083): 1.91 % of the pixels outside as is; `Exacta` touches 1.91 % and leaves 0.001 %, the chroma given up 0.63 at the median and 4.3 at the 99 % mark, 99.5 % of the pixels outside under L\* 15, none out of reach; `Lina` touches 3.81 % (0.41 at the median), `Foarte lina` 6.22 % (0.58); the recipe: a mask under L\* 12 (10.0 % of the pixels) at 70 % saturation leaves 4.9 % of them; the saved FITS 270869760 bytes, the saved TIFF 135437610 bytes, 16-bit, profile `sRGB`, the two within 0.5 16-bit levels of each other |

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
| background R / G / B | 19.2 / 20.6 / 18.9 | 24.0 / 25.9 / 25.9 (19.2 / 20.6 / 18.9 before ADR 0055) | identical on the darkest quarter; since ADR 0055 the tool takes the sky patches, which the conversation did not have |
| G - (R+B)/2; R - B | +1.58; +0.31 | +0.9; -1.9 (+1.6; +0.3 before ADR 0055) | figures only since ADR 0057: the verdict is the chroma's, 1.0 (L* 9.0, a* -0.9, b* -0.3), no cast |
| bottom-right corner R / G / B | 28.6 / 22.3 / 9.0 | 28.1 / 21.9 / 8.8 on its sky pixels, stride 4 (28.6 / 22.3 / 8.9 on every pixel, stride 3, before ADR 0056) | the levels; the deltas are +4.1 / -4.0 / -17.1 against the sky patches (ADR 0055, ADR 0056), +9.4 / +1.7 / -9.9 against the darkest quarter |
| crop to 50:35 | 5554 x 3888, 192 px per side | 5554 x 3888, 192 px, 6.5 % | identical |
| 70 cm row | 215 dpi, star 0.35 mm, point from 1.2 m (full frame, FWHM 3.0) | 202 dpi, 0.39 mm, 1.36 m (the crop, FWHM 3.13) | consistent |

### What the tool said that the conversation also said

Verdict PROBLEMA (ATENTIE since ADR 0025: the sky at L\* 9.3 is above paper black and only the
dust is under it). Send TIFF 8-bit LZW; if only JPEG, q100 and never below (q95 max
error 27; since ADR 0061 a fact of the Export tab, not a line of the report). No visible banding
on 8 bits (plateau 0.34 mm at 202 dpi; since ADR 0060 the verdict is the share of the frame with
a visible step, 0.00 %). Background too low
for baryta - 45.6 % under paper black - re-stretch from linear to 0.19 (0.17 since ADR 0025,
the lowest grid row that puts the darkest 1 % of the patches on paper black). Green cast of
+1.6 on the background (since ADR 0055, against the sky patches: +0.9 of green and -1.9 of
red minus blue, a cool cast under the level rule; since ADR 0057 the chroma, 1.0, says no
cast at all). Warm bottom-right corner (R +9.4, B -9.9; +4.1 / -4.0 / -17.1
against the sky patches, on the corner's own sky pixels) and warm bottom edge: horizon glow,
fix on linear or keep as composition. Recommended 70 x 49 cm at 1.5 m; the
50:35 target cuts 192 px per side. Denoise pressed hard for print: sigma 0.45, target
0.8-1.5, and after the re-stretch it becomes ~0.61 (0.57 at 0.17). Keep the 16-bit master: 46
levels missing from 8 bits after a shadow lift, none from 16 (since ADR 0061 a mark of the Export
tab's stress row, not a warning).

### What the real image taught the tool

- **Edge regions that brighten neutrally are the object, not a gradient.** The first run
  flagged all eight edge regions as "residual gradient" because the Milky Way band enters
  the top-left corner (+11 / +10 / +16). The rule now separates coloured departures
  (channels move differently: light pollution, horizon glow - Attention) from neutral
  ones (channels move together: most likely the object - informative). On this image that
  leaves exactly the two warm regions the conversation had found. (Restated 2026-10-27,
  ADR 0055: those deltas were against the darkest quarter; against the sky patches the two
  warm regions remain, and the top-left corner, +6.5 / +5.0 / +8.8 on every pixel, is coloured
  too, the band being bluer than the sky; on its own sky pixels, ADR 0056, it is +4.2 / +2.9 / +5.6
  and neutral again, as is the top edge, +3.9 / +3.2 / +3.8.)
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
stars (7163 from ADR 0041 to ADR 0054, 7166 before, 6274 since, with the median 3.133 px then 3.107 and the centre 4.27 then 4.24), the background colour and the bottom-right
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

- 2026-11-06 (ADR 0084, the sky model is the median of each cell and the dust three sigmas
  under it, `docs/plans/2026-11-06-find-the-dust-without-a-wcs.md`): `SkyModelLowPercentile`
  25 -> 50 and `SkyNoiseK` 4 -> 3 in `Thresholds.cs`, so that a Milky Way frame without a WCS
  finds the Great Rift. Re-run on the branch, the twelve files of the manifest copied into the
  worktree: of the 1323 figures the sixteen cases record, 213 moved, all on the sky-path cases
  (V-001, V-003, V-005, V-009 on `de_printat.png`; V-010, V-011 on the Rosetta; V-012 on
  Orion; V-013, V-014 on the Veil); the band-path cases V-004 and V-008 and every other case
  are identical. What moved: the dust class (`de_printat.png` 0.6 -> 7.8 % of the patches,
  Rosetta 0.2 -> 6.7 %, the Veil 0.0 -> 0.8 %), which on these three still does not decide
  (lighter than the darkest sky, or under the 5 % floor), so the dust is the darkest 1 % of a
  slightly different sky class (`de_printat.png` 4.18 -> 4.40, Rosetta 6.15 unchanged, the
  Veil 6.26 -> 6.43) and the sky its median (8.87 -> 9.03, 8.08 -> 8.14, 9.31 -> 9.33); the
  nebula and bright-nebula classes (`de_printat.png` 15.4 / 2.5 -> 10.1 / 2.0 %, Rosetta
  12.6 / 5.7 -> 8.8 / 3.6 %) and with them the curve anchors (V-009 band 0.216 -> 0.236,
  V-011 0.272 -> 0.321, V-012 0.365 -> 0.441) and every lifted L\* of the curves; the exact
  targets by up to 0.003. Two verdict-level figures: V-001's sky at Dmax 2.0 is 9.03 against a
  paper black of 9.0, no longer under it, so `overall` is ATENTIE instead of PROBLEMA (as
  before ADR 0033); and the background colour, measured on the sky patches (ADR 0055), moves
  by a few tenths of a level on `de_printat.png` and Rosetta, which flips three edge flags
  (V-001 the top-left corner now coloured and the bottom-left flagged, V-010 the top-right
  corner flagged) and the bottom edge's blue by 5.7 levels on V-001. The figures of 2026-10-02
  below stay as they were measured.
- 2026-11-02 (ADR 0079, the parameter card and Appendix F in plain words,
  `docs/plans/2026-11-02-explain-the-parameter-card-plainly.md`): Appendix F's two lines and the
  given Dmax's mark in `Report/ReportWriter.cs`, two messages of `Parameters.cs` and a comment of
  `Analysis/PrintStretchSimulator.cs` reworded; no formula moved, and no recorded figure is one of
  these texts. Re-run on the branch, the twelve files of the manifest hard-linked into the
  worktree, their hashes checked: of the 1290 figures the fifteen cases record, 1288 are
  identical, and the same two as since 2026-10-23 differ by 0.0001, inside the 0.01 tolerance
  (V-011 `curves.Maxima.lstar.clouds` 67.1502 -> 67.1503, V-014 `curves.Maxima.lstar.peaks`
  77.9226 -> 77.9227). The fifteen cases and the eleven star-pair checks pass, in 3.0 min.

- 2026-11-02 (ADR 0077, the Soft proof tab in plain words,
  `docs/plans/2026-11-02-explain-the-soft-proof-tab-plainly.md`): the texts of
  `Report/ProofTexts.cs` and the reasons `Analysis/SoftProof.cs` and `Analysis/GamutAnalyzer.cs`
  give for a refused profile reworded; no formula moved, and no recorded figure is one of these
  sentences. Re-run on the branch, the twelve files of the manifest hard-linked into the
  worktree, their hashes checked: of the 1290 figures the fifteen cases record, 1288 are
  identical, and the same two as since 2026-10-23 differ by 0.0001, inside the 0.01 tolerance
  (V-011 `curves.Maxima.lstar.clouds` 67.1502 -> 67.1503, V-014 `curves.Maxima.lstar.peaks`
  77.9226 -> 77.9227). The fifteen cases and the eleven star-pair checks pass, in 4.4 min.

- 2026-11-02 (ADR 0076, the Editare Curbe tab in plain words,
  `docs/plans/2026-11-02-explain-the-curves-tab-plainly.md`): the texts of `Report/CurveTexts.cs`
  reworded and a comment of `Analysis/PrintStretchSimulator.cs` corrected; no formula moved.
  V-012's `curves.problem` takes the new sentence, which the owner approved with the texts:
  `Nu se sugereaza nicio curba: ancora "varfuri" e deja la 0.971, la sau peste tinta 0.92 la care
  ajung varfurile ... imaginea e deja la plafon.` -> `Nu se sugereaza nicio curba: punctul "varfuri"
  e deja la 0.971, la sau peste 0.92, unde ajung varfurile ... imaginea e deja cat de luminoasa
  poate fi.`; its figures, 0.971 and 0.92, stay. Re-run on the branch, the twelve files of the
  manifest hard-linked into the worktree, their hashes checked: of the 1290 figures the fifteen
  cases record, 1288 are identical, and the same two as since 2026-10-23 differ by 0.0001,
  inside the 0.01 tolerance (V-011 `curves.Maxima.lstar.clouds` 67.1502 -> 67.1503, V-014
  `curves.Maxima.lstar.peaks` 77.9226 -> 77.9227); V-012 reads the new sentence. The fifteen
  cases and the eleven star-pair checks pass, in 5.8 min. The run also covers the five
  text-only tasks merged since 2026-10-31 without a run of their own, the Rezolutie, Tonuri,
  Raport, Export and Culoare tabs (ADRs 0071 to 0075).

- 2026-10-31 (ADR 0069, the diagnostic log, `docs/plans/2026-10-31-add-a-diagnostic-log-with-nlog.md`):
  log lines in the four readers of `Input/` and in `Runs/`, a `Cause` on `GamutTable` and
  `ProofProfile`, the readers' exceptions passed on as inner ones; no formula moved. Re-run on
  the branch, the twelve files of the manifest copied into the worktree: of the fifteen cases'
  1350 figures 1348 are identical to the recorded ones, and the same two as since 2026-10-23
  differ by 0.0001, inside the 0.01 tolerance (V-011 `curves.Maxima.lstar.clouds` 67.1502 ->
  67.1503, V-014 `curves.Maxima.lstar.peaks` 77.9226 -> 77.9227). The fifteen cases and the
  eleven star-pair checks pass. Re-run the same day after the review's fixes (`Input/` again:
  the readers log a reworded exception where they catch it, the header's values printable):
  the same 1348 of 1350 identical, the same two differences; the 26 tests pass.

- 2026-10-27 (ADR 0060, the banding verdict on the share of the frame with a visible plateau,
  every one of 5000 positions tried; `docs/plans/2026-10-27-plateau-share-of-flat-tiles.md`, its
  table): `quantization.plateauPositions` (5000) and
  `quantization.plateauVisibleFramePctAtReference` (0 on every analysis case: the reference
  image's 20 clean tiles hold no step over 1 mm at its reference dpi, the FITS frames have no
  clean tile) recorded; no existing figure moved, V-001's 20 tiles and 2.6796 px included, since
  5000 positions were already what found them. The golden console texts move on the dark
  scene, whose plateau is now measured on every clean tile among 5000 positions (419, 8.4 % of
  the frame) and whose banding line carries the share (8.38 %) and the threshold (0.10 %), and
  on Appendix C's plateau line.

- 2026-10-27 (R-080, R-091, R-095: where the plateau is measured): `quantization.plateauFramePct`
  (tiles accepted over positions tried, in percent) and `quantization.plateauSigmaLsb` (the median noise on the
  plateau's tiles) recorded for every analysis case; no existing figure moved. On the reference image
  the 20 tiles are 0.4 % of the frame at sigma 0.32 LSB (V-001). The golden console texts move on the
  banding line's "because", on Appendix C's plateau line and on the denoise line's "because", which
  now always carries the share of smooth tiles under 0.5 LSB.

- 2026-10-27 (ADR 0057, a cast is a sky chroma over 3 in L*a*b*;
  `docs/plans/2026-10-27-cast-on-lab-chroma.md`, its table): the owner asked whether the +2.2
  levels of green on his photometrically calibrated core were a cast; in L*a*b* that sky sits
  at chroma 1.8, under what an eye reads on paper. `background.hasCast` V-001 true -> false
  (chroma 1.0), V-004 true -> false (2.0), V-010 true -> false (1.4); V-013 stays true (4.8).
  `background.lab.l`, `.a`, `.b` and `background.chroma` recorded for the four colour cases.
  No other figure moved; the golden console texts move on the dark scene's cast, now "The
  background is neutral" at chroma 1.7, and on the colour's `Because` and Appendix E line,
  which carry the chroma, a* and b*.

- 2026-10-27 (ADR 0056, each edge region on its own sky pixels, a quarter of sky as the floor;
  `docs/plans/2026-10-27-edge-regions-on-sky-patches.md`, its measurement table): the regions
  move to the map's subsample (stride 4) and take their medians on their sky pixels, so every
  `background.region.*.levels` figure of V-001, V-004, V-010 and V-013 moves by tenths of a
  level (V-001's bottom-right corner 28.5992 / 22.2724 / 8.928 -> 28.1051 / 21.8949 / 8.8249),
  and three verdicts change: V-001's top-left corner, where the band enters, `coloured` true ->
  false (+4.2 / +2.9 / +5.6, neutral); V-004's top edge, which the band crosses, `coloured`
  true -> false; V-013's left edge `flagged` true -> false (+2.8 / +1.4 / +1.8). The background
  levels and the cast do not move, nor any other case; the golden console texts move on the
  regions' levels by a tenth and on the dark scene's warm corner, measured on every pixel
  (sky 14 %), whose row and `Because` now say so.

- 2026-10-27 (ADR 0055, the sky's colour from the sky patches, not the darkest quarter;
  `docs/plans/2026-10-27-sky-colour-from-the-sky-patches.md`, its measurement table): on the
  owner's 2026-08-12 core the darkest quarter was the Great Rift's dust and six of the eight
  edge regions read "cool" against it; the reference is now the per-channel median of the sky
  patches above the bottom band. Every moved figure is a `background.*` figure; no region's
  levels moved (they stay on the stride-3 subsample). V-001: levels 19.1868 / 20.6109 / 18.8716
  -> 23.9883 / 25.8599 / 25.8949, `greenExcess` 1.5817 -> 0.9183, `redMinusBlue` 0.3152 ->
  -1.9066 (`hasCast` stays true, now for the cool side); the top-right, bottom-left, left and
  right regions no longer flagged, the top-left now coloured. V-004: 31.2973 / 34.2517 / 24.8858
  -> 40.9349 / 43.4004 / 41.0771, `greenExcess` 6.1601 -> 2.3944, `redMinusBlue` 6.4114 ->
  -0.1422; the top-left, top-right, left and right regions now neutral, the top edge now
  coloured. V-010: 19.8302 / 18.4731 / 18.5251 -> 26.35 / 23.7603 / 23.3184, `greenExcess`
  -0.7046 -> -1.0739, `redMinusBlue` 1.305 -> 3.0316; five regions no longer flagged, the
  bottom-left, top-left (no longer flagged) and left now coloured. V-013: 8.8172 / 21.2517 /
  14.6799 -> 19.018 / 26.9556 / 23.5689, `greenExcess` 9.5031 -> 5.6621, `redMinusBlue`
  -5.8627 -> -4.5509; six regions no longer flagged, the top-right now coloured, the bottom
  and left now neutral. No other case moved. The golden console texts moved on every scene's
  section 3 and Appendix E: the levels (the darkest quarter of a noisy flat sky sat 0.4 to 1
  level under its median), the deltas, and the new "from the sky patches above the bottom
  band" suffix.

- 2026-10-27 (ADR 0054, a star is at least 12 local sigmas over its base;
  `docs/plans/2026-10-27-reject-noise-grain-as-stars.md`, its drift table): the owner found
  6297 "stars" reported on the starless FITS of V-004, the grain of the stretched noise, which
  the absolute amplitude floor let through and which V-004 had recorded on 2026-10-02 without
  comment. Every moved figure is a star figure or one derived from the FWHM. V-001: `stars.count`
  7163 -> 6274, `fwhmMedian` 3.1332 -> 3.1065 (Q1 2.8685 -> 2.8512, Q3 3.4899 -> 3.4209), the
  centre 4.2709 -> 4.2387 and the four corners by under 0.01, `cornerToCenterRatio` 0.7744 ->
  0.7822, `naturalRatio` 1.0148 -> 1.0062, every row's `starMm` (0.3694 -> 0.3662 at 70 cm) and
  `minDistanceM` (1.2736 -> 1.2628 at 70), the figures within the 0.01 tolerance recorded too, so
  that the file holds what the tool measures (the second review of 2026-10-27). V-002: the same
  FWHM, `naturalRatio` 1.0624 -> 1.0533, every row's `starMm` and `minDistanceM` (the 70 cm row's
  point from 1.3617 -> 1.3501 m). V-004: 6297 -> 5, V-010: 2858 -> 1, V-013: 22 485 -> 1, each with every FWHM,
  region, `starMm` and `minDistanceM` figure to `n/a` and the size table on the dpi rule of R-074
  (the reference row 100 -> 80 cm on V-004, 100 -> 70 on V-013; V-010's 50 cm row passes). No
  other case moved; the golden console texts did not move (the raw deviation about the border
  median, the first form of the clause, had cost the DarkSky scene 14 of its 64 stars and V-001
  1747 instead of 889: the plane fitted to the border, the final form, took that back). The
  reference run below keeps its figures of 2026-10-02 as history.
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

- 2026-10-24 (the backfill's four questions, `docs/plans/2026-10-24-close-the-backfill-questions.md`,
  ADR 0045): `Parameters.Validate` gained the NaN and infinity checks, which no case's
  parameters trip; no formula moved. Re-run on the branch after that commit, the seven files of
  the manifest copied into the worktree: of the fourteen cases' 1242 figures 1240 are identical
  to the recorded ones, and the same two as on 2026-10-23 differ by 0.0001, inside the 0.01
  tolerance (V-011 `curves.Maxima.lstar.clouds` 67.1502 -> 67.1503, V-014
  `curves.Maxima.lstar.peaks` 77.9226 -> 77.9227). The fourteen cases pass. Re-run the same day
  after `ReportWriter.SaveFull` (the report file written whole or not at all; no figure
  touched): the same 1240 of 1242 identical, the same two differences. The fourteen cases pass.
  Re-run once more after `AtomicFile` (the shared temporary-file writer of the report and the
  FITS; `Input/` moved, no figure touched): the same 1240 of 1242, the same two differences.
- 2026-10-25 (ADR 0048, the English interface, `docs/plans/2026-10-25-add-the-english-interface.md`):
  every text of `Input/`, `Analysis/`, `Report/`, `Runs/` and `Parameters.cs` became an
  English / Romanian pair; no formula moved. Re-run on the branch, the seven files of the
  manifest copied into the worktree: of the fourteen cases' 1242 figures 1240 are identical to
  the recorded ones, and the same two as since 2026-10-23 differ by 0.0001, inside the 0.01
  tolerance (V-011 `curves.Maxima.lstar.clouds` 67.1502 -> 67.1503, V-014
  `curves.Maxima.lstar.peaks` 77.9226 -> 77.9227). The fourteen cases pass, in 3.2 min. The
  validation runs in Romanian (the test assembly's default), so the text figures
  (`curves.problem`, `curves.names`) read as recorded.
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
- 2026-10-30 (ADR 0066, the soft proof): V-015 added, kind `proof`, on the plate-solved FITS through
  `Hahnemuhle PRBaryta.icm`, recorded from its `MEASURED` line (the paper white L\* 97.19, b\* 1.11,
  sRGB 248, 247, 245; 1.37 % outside the paper gamut; 0.02 % at most outside the screen gamut).
  The other fourteen cases were re-measured in the same run and did not move: `Analysis/` gained
  a file and `GamutAnalyzer` only shares its trilinear read. The fifteen cases pass.
- 2026-10-30 (the review of ADR 0066): V-015 re-measured after the black point compensation took
  the device black as the neutral of its Y (ISO 18619): the Relative mean L\* 19.6841 to 19.6904,
  the Perceptual screen-gamut share 0.0198 to 0.0214 %, every figure within the tolerance; the
  recorded values are the new line. The fifteen cases pass.
- 2026-10-30 (ADR 0066, the screen-gamut warning dropped): V-015 loses its twelve `pctScreenGamut`
  and `pctBoth` figures with the warning (0 % in the shown state on the baryta and on the canvas,
  which is why it went); the other figures re-measured unchanged. The fifteen cases pass.
- 2026-11-05 (ADR 0081, the paper's gamut as the whole colour difference, lightness included,
  after the round trip with black point compensation, over 4;
  `docs/plans/2026-11-05-count-the-tone-loss-in-the-paper-gamut.md`), the figures that move
  with the rule, as the approved plan said: V-008's twenty gamut figures, the peaks (0)
  excepted, and V-015's share outside the paper gamut. As is `curves.before.gamut.all` 1.3563 -> 1.9427, `.dust` 4.2609 -> 11.0959, `.sky`
  0.7187 -> 1.1631, `.band` 1.7945 -> 0.2818, `.clouds` 0.5635 -> 0.2269; the three curves' `all`
  1.9727, 1.9842 and 1.9983 (before: 1.2852, 1.2432, 1.1992), their dust 11.2861, 11.3626 and
  11.4725 (before: 4.1336, 4.0623, 3.9676), the sky, band and clouds in the same proportion; V-015
  `proof.pctPaperGamut` 1.3704 -> 1.8983. The dust class rises most: it is the dark saturated dust
  the paper prints lighter, which the chroma rule could not see; the band and the clouds fall,
  since the compensation keeps their colour. Exact per pixel the same day: all 1.917, dust 10.956
  (the hybrid within 0.14 points on every class, ADR 0081). The other thirteen cases did not
  move. The fifteen cases (and the eleven star-pair checks) pass.

- 2026-11-06 (ADR 0082, the preview window's layers class the 32 px patches, not the pixels;
  `docs/plans/2026-11-06-classify-the-preview-by-patch.md`): `Analysis/PrintPreview.cs` classes the
  simulation's patches at the row's lift instead of the pixels, and `Analysis/DustAnalyzer.cs` makes
  its grid geometry internal; no formula of the report moved, and the preview has no recorded
  figure. Re-run on the merged `main` (1f070095), the twelve files of the manifest hard-linked
  into the worktree, their hashes checked: of the 1290 figures the fifteen cases record, 1288 are
  identical, and the same two as since 2026-10-23 differ by 0.0001, inside the 0.01 tolerance
  (V-011 `curves.Maxima.lstar.clouds` 67.1502 -> 67.1503, V-014 `curves.Maxima.lstar.peaks`
  77.9226 -> 77.9227). The fifteen cases and the eleven star-pair checks pass, in 4.6 min; a run
  on the branch before its first commit, 2.7 min, had passed the same.
- 2026-11-06 (ADR 0083, the paper fit): V-016 added, kind `gamutfit`, on the plate-solved FITS
  through the lab's baryta profile (`docs/plans/2026-11-06-fit-the-colours-to-the-paper.md`):
  1.91 % of the pixels outside as is on the tonal subsample; `Exacta` touches those and leaves
  0.001 %, the chroma given up 0.63 at the median and 4.3 at the 99 % mark, 99.5 % of the pixels
  outside under L\* 15; `Lina` touches 3.81 % (0.41 at the median, 0.02 % left), `Foarte lina`
  6.22 % (0.58); the recipe a mask under L\* 12 at 70 % saturation, leaving 4.9 %; the saved FITS
  270 869 760 bytes (the input's size, its cards kept), the saved TIFF 135 437 610 bytes, 16-bit,
  profile `sRGB`, the two within 0.5 of a 16-bit level. The other fifteen cases did not move:
  `ColorMath` gained the D50 conversions and the display step took its matrix from there,
  without a figure changing (V-015's means to four decimals).
