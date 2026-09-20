# Acceptance: `de_printat.png`

The tool was built to reproduce, and then automate, the measurements made by hand (Python
/ NumPy / ImageMagick) on one image in the conversation that started it:
`de_printat.png`, a Milky Way core, 5938 x 3888, 16-bit RGB PNG, sRGB (`sRGB-elle-V2`),
98.6 MB. The image is not in the repo. This file records the run of the finished tool on
it, next to the hand-made numbers.

Run: `AstroPhotoAnalyzer.exe de_printat.png --aspect 50:35 --distance 1.5` (the hallway
scenario from the conversation), version 1.0.0, 11 seconds on the whole 23 MP.

## Numbers

| measurement | by hand (conversation) | tool | note |
|---|---|---|---|
| means per channel (0..255) | 32.2 / 29.6 / 27.9 | 32.2 / 29.6 / 27.9 | identical |
| % below level 32 / 64 | 73.8 / 96.3 | 74.1 / 96.3 | `<` on float vs `<` on the high byte |
| reference percentiles p0.1 / p1 / p50 / p90 / p99 / p99.9 | 3.53 / 12.93 / 26.15 / 43.87 / 80.51 / 115.1 | 3.5 / 13.0 / 26.2 / 44.0 / 80.7 / 115.5 | stride 2 vs full |
| L\* median; % < L\*9; % < L\*15; % > L\*50 | 9.4; 44.4; 81.6; 0.4 | 9.3; 45.6; 82.8; 0.3 | hand: on the 50x35 crop; tool: full frame |
| lift to background 0.19: L\*; % < 9; % < 15 | 20.4; 0.5; 6.7 (gamma 0.721 from an assumed 0.10) | 19.9; 0.5; 8.0 (gamma 0.730 from the measured 0.103) | the tool measures the background instead of assuming it |
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

## What the tool said that the conversation also said

Verdict PROBLEMA. Send TIFF 8-bit LZW; if only JPEG, q100 and never below (q95 max
error 27). No visible banding on 8 bits (plateau 0.34 mm at 202 dpi). Background too low
for baryta - 45.6 % under paper black - re-stretch from linear to 0.19. Green cast of
+1.6 on the background. Warm bottom-right corner (R +9.4, B -9.9) and warm bottom edge:
horizon glow, fix on linear or keep as composition. Recommended 70 x 49 cm at 1.5 m; the
50:35 target cuts 192 px per side. Denoise pressed hard for print: sigma 0.45, target
0.8-1.5, and after the re-stretch it becomes ~0.61. Keep the 16-bit master: 46 levels
missing from 8 bits after a shadow lift, none from 16.

## What the real image taught the tool

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
