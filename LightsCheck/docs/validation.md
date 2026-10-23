# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json` (read by `ValidationTests`, trait `Category=Validation`, not in the gate,
ADR 0004) and is explained here. A number changes only after a re-measurement, with a dated
line under History saying old value, new value and why. The sessions are whole folders of
camera raws under `E:\Photografie\Astro`, the folder `LIGHTSCHECK_TESTDATA_LARGE` names; a case
names its session relative to it. A case whose session is absent returns early with a message.

The hand analysis these cases reproduce is `docs/validation/2026-10-06-veil/raport.md` (the
report delivered on 2026-10-09) and `frames.csv` (its per-frame figures, from the Python
scripts in `prototype/`, which read the same libraw through rawpy).

## Kinds of case

| Kind | What runs | What is recorded | Tolerance |
| --- | --- | --- | --- |
| `inventory` | `InventoryRun` on the session | the counts of lights, biases and darks, the camera, the exposure and ISO of the first frame, the start of the first frame, the span, the duty | 0.01 s exposure, 1 s span, 0.05 points duty |
| `frame` | `FrameRun` on one light | detected and bright counts, median and p90 FWHM, median elongation, the elongated fraction, the angle and its coherence, the halo, the background and noise, the four channel medians, the centre and edge FWHM, the trail count | counts 1 %, FWHM 0.05 px, elongation 0.05, fraction 0.03, angle 3 or 8 degrees (8 where the coherence is under 0.5 and the angle means little), coherence 0.03 / 0.05, halo 0.01, ADU 1.0, sigma 0.2 |
| `series` | `SeriesRun` on the whole session (minutes) | the class counts and the list of bad frames, the registered frames, the moves, pauses and pauses without a move, the distinct spots at 5 and 10 px, the isolated frames, the longest run at 10 px, the median move, the rotation, the drift along Y, the session FWHM, the read noise of G1, the swamping ratio of R on the first frame, the RA axis and the four verdicts; since 2026-10-10 also the drift per exposure, the walking-noise dash and word (R-043), the darks count (R-042), the pattern's status, its sigma per CFA position in ADU and its hot pixels per megapixel (R-046), the pattern surviving in the stack in percent (R-048) | counts, lists and words exact; px 0.1, arcmin 0.3, FWHM 0.05, ADU 0.1, ratio 0.3, per Mpx 8 (two pixels of the 262 k read), pct 0.2 |
| `words` | `SeriesRun` on the whole session (minutes) | the trail axis word of every frame with a trailing flag, one expected word for the bad and one for the marginal frames, and the `acelasi loc` cell of the frames listed | exact |
| `tables` | `SeriesRun` on the whole session (minutes) | the rows of every dithering table named, cell by cell; with `expected` null the test prints the tables it measured instead | exact |
| `moves` | `SeriesRun` on the whole session (minutes) | the `dithering` cell of the frames listed and the count of move components to each side (south, north, east, west); with `expected` null the test prints every move it measured | exact |

## Cases

| ID | Session | Kind | Figures | Measured on |
| --- | --- | --- | --- | --- |
| V-001 | `Veil/2026-10-06-3h` (Nikon D3300, Samyang 135 f/2.8, L-eNhance, SA GTi) | inventory | 87 lights, 30 biases, `Nikon D3300`, 120.2 s at ISO 1600, first start 2026-10-06 23:35:03, span 12166.2 s (3 h 22 min), duty 86.06 % (10470.4 s of shutter: 86 x 120.2 + 133.2) | 2026-10-09 |
| V-002 | same, frame 1 (trailed, the first of the night) | frame | 12308 / 1532 stars, FWHM 3.85 (p90 4.17), elongation 1.68, 98 % elongated, angle 83 deg at coherence 0.94, halo 0.035, background 128, sigma 13.9, channels 27 / 126 / 126 / 144 | 2026-10-09 |
| V-003 | same, frame 4 (clean) | frame | 15132 / 2104, FWHM 3.06 (3.64), elongation 1.15, 14 %, angle 86 at 0.36, halo 0.019, background 120, sigma 13.1, channels 25 / 120 / 120 / 138 | 2026-10-09 |
| V-004 | same, frame 17 (the worst: doubled stars) | frame | 9763 / 1176, FWHM 4.92 (5.58), elongation 2.29, 99 %, angle -89 at 0.98, halo 0.387, background 131, sigma 14.3, channels 27 / 129 / 129 / 147 | 2026-10-09 |
| V-005 | same, frame 23 (doubled stars) | frame | 11352 / 1340, FWHM 4.88 (5.24), elongation 2.23, 98 %, angle -87 at 0.98, halo 0.182, background 135, sigma 14.5, channels 27 / 135 / 135 / 153 | 2026-10-09 |
| V-006 | same, frame 72 (clean, late) | frame | 13338 / 1886, FWHM 3.03 (3.64), elongation 1.16, 15 %, angle -76 at 0.46, halo 0.012, background 161, sigma 15.0, channels 33 / 159 / 159 / 183 | 2026-10-09 |
| V-007 | same, frame 83 (clean, last hour) | frame | 12135 / 1662, FWHM 3.06 (3.58), elongation 1.13, 13 %, angle -87 at 0.17, halo -0.006, background 174, sigma 15.8, channels 35 / 174 / 174 / 195 | 2026-10-09 |
| V-008 | same, the whole session | series | 48 good / 17 marginal / 22 bad, the bad frames 1, 13, 15, 17, 18, 20, 22, 23, 25, 27, 38, 43, 45, 50, 52, 54, 56, 60, 63, 68, 70, 76; 87 registered; 41 moves (>= 5 px), 47 pauses, 9 pauses without a move; 38 distinct spots at 5 px, 25 at 10 px, 1 isolated, longest run at 10 px 10 frames (61-70); median move 13.29 px; rotation 8.5 arcmin; drift -0.19 / -0.96 px/min over the 39 pairs without a pause (X / Y); session FWHM 3.12 px; read noise G1 2.9 ADU; swamping of R on frame 1 9.4; RA along Y (WCS); focus held, no dew, hot pixels fine, walking noise insufficient by the dither rule; drift 1.96 px per exposure, a dash of 17.7 px in the run of 10, word `Insufficient`; polar error between 4.2' and 12.0' (the DEC drift of 1.1 "/min; the rotation of 2.5'/h at DEC +31.6), at most 0.64 px of drift per exposure, `Ok`; no darks; pattern measured on 87 crops: sigma 1.4 / 2.1 / 2.1 / 2.3 ADU on R / G1 / G2 / B (12, 10, 10 and 10 % of a frame's noise), 4 hot pixels per megapixel; 1.6 % of one frame's pattern survives in the mean (16 pairs within 1.5 px; ideal 1.1 %) | 2026-10-09, the drift, darks and pattern figures 2026-10-10, the drift and polar figures 2026-10-24 |
| V-009 | same, the whole session | words | the trail axis word `RA` on all 22 bad and all 17 marginal frames (angles 77 to 89 degrees in absolute value, coherence 0.94 and 0.86 or more); the `acelasi loc` cells `1/4 de la 57` to `4/4 de la 57` on 57 to 60, `1/10 de la 61` to `10/10 de la 61` on 61 to 70, `-` on 71 and 72 | 2026-10-09 |
| V-010 | same, the whole session | tables | the five dithering tables of R-036, cell by cell: how often (every 1 frame 12 times, 2 frames 22, 3 once, 4 three times, 5 and 6 once each), how far (median 13.29 px = 79.6", 10 moves of 5-10 px, 23 of 10-20, 8 of 20+), direction (X = DEC: 6 alone, 11 plus, 2 minus; Y = RA: 28 alone, 11 plus, 24 minus; both 7), coverage (70 frames within 3 px of another, 79 within 5 with 38 distinct spots and 8 runs, 86 within 10 with 25 and 11), the 8 runs at 5 px and the 11 at 10 px with their minutes (61-70, 10 frames, 22 min) | 2026-10-09 |
| V-012 | `Milkyway-Core/2026-08-12-Smida` (Nikon D3300, Tokina 11-20 at 20 mm f/2.8, SA GTi, a dark site) | inventory | 32 lights, 30 biases, 20 darks, `Nikon D3300`, 119.2 s at ISO 400, first start 2026-08-12 23:22:25, span 4996 s (1 h 23 min), duty 76.3 % | 2026-10-10 |
| V-013 | same, the whole session | series | 32 good, none marginal or bad; 32 registered; 2 moves, 31 pauses, 29 pauses without a move (the dither of a 135 mm night is under 2 px at 40.4 "/px); 3 distinct spots at 5 and at 10 px, none isolated, longest run at 10 px 12 frames (9-20); median move 15.52 px; rotation -6.3 arcmin; drift -0.07 px/min along Y (-2.8 "/min) over 29 pairs with a pause, approximate, since no two frames were shot back to back; polar alignment not measurable for the same reason; session FWHM 4.17 px; read noise G1 1.3 ADU; swamping of R on frame 1 10.1; RA axis unknown (no solved FITS at the root, no trailed frame); focus held, no dew, hot pixels `ATENTIE` (12 consecutive frames within 3 px), walking noise insufficient by the dither rule but drift 0.17 px per exposure, a dash of 1.9 px in the run of 12, word `Static`; 20 darks; pattern measured on 32 crops: sigma 1.1 / 1.2 / 1.2 / 0.9 ADU on R / G1 / G2 / B (21, 19, 18 and 16 % of a frame's noise), 55 hot pixels per megapixel; 23.4 % of one frame's pattern survives in the mean (104 pairs within 1.5 px; ideal 3.1 %) | 2026-10-10 |
| V-011 | same, the whole session | moves | the `dithering` cells of R-038 on ten frames, in the symbol form of R-040 (3: `DEC 13 px ↓ S`, 17: `DEC 28 px ↓ S`, 61: `RA 17 px ← V`, 6: `RA 12 px → E`, 5 and 19 and 80 with both components, 1 and 62 `-`) and, over the 41 moves, 24 components west, 11 east, 11 south, 2 north; the nine DEC jumps of 12 px or more the hand analysis counted are all south | 2026-10-10 |

The series figures are the console's run of 2026-10-09 (88 s on 4 threads) and equal the hand
report's: the same 22 bad and 17 marginal frames, 47 pauses and 9 presses without effect, the
same groups of frames in 5 and 10 px (`raport.md`, section 6), the rotation of 8.5 arcmin, the
read noise and the swamping of red. The drift along Y was first recorded as -0.48 px/min over 45
non-move pairs, against the hand report's -0.8 px/min over the back-to-back pairs; the 45
included nine post-pause presses of 3 to 5 px, which halved it (ADR 0016). Since 2026-10-24 the
drift is over the 39 pairs without a pause, -0.19 / -0.96 px/min, the hand report's figure
(its "-1.9 px per 141 s" is -1.94 px per pair of 121 s). The polar upper bound reads 12.0' with
the tool's end-to-end rotation of 2.5'/h (the first and the last frame each carry the dithers'
projection of up to 40"), against 10.7' with a linear fit at 2.13'/h in ADR 0016's measurement;
the lower bound of 4.2' is the same in both.

The frame figures are the console's `--frame N` output of 2026-10-09 and equal the prototype's
(`docs/validation/2026-10-06-veil/frames.csv`, columns `n_det`, `n_det_bright`, `fwhm_med`,
`fwhm_p90`, `elong_med`, `frac_elong13`, `pa_mean`, `pa_coherence`, `halo`, `bg_median`,
`sigma_green`, `R_median`, `G1_median`, `G2_median`, `B_median`, `fwhm_center`, `fwhm_edge`):
counts to the unit, ADU to the unit, FWHM to 0.01, elongation to 0.02 (frame 17: 2.29 vs
2.31; the prototype measured 400 stars, the C# 397), as `docs/design/frame-analysis.md` says.

The span and the duty come from the console's run on the session on 2026-10-09:
`2026-10-06 23:35:03 -> 2026-10-07 02:57:49 (3 h 22 min); integrare 2 h 54 min din 87 cadre
(86 % din interval)`; the Python analysis of the same day gave the same interval from the EXIF
times with subseconds (`raport.md`: 23:35:03 - 02:57:49, 86 %). Frame 75 carries a 133.2 s
shutter in its EXIF (libraw and PIL agree, `frames.csv` has it); the console prints
`expuneri diferite` for the session, and the report's "+11 % background on frame 75" is that
longer exposure (133.2 / 120.2 = 1.108), not a passing light, a correction noted in the copy
of the report.

The two walking-noise words are the sessions' stacks as the owner saw them on 2026-10-10 (ADR
0015): worms along RA on Veil, a clean stack on Smida. The dash of 9.9 px on Veil is the
console's drift (1.10 px per 120 s) over the run of frames 61-70; the scratch measurement of the
same day gave about 11 px from a slightly larger drift estimate. Smida's dash of 1.9 px is the
same drift in arcseconds at a seventh of the pixels. The pattern figures are per CFA pixel of
the raw mosaic, in the central 512 x 512 px, as a sigma: the first version of the row counted
the pixels above 3 sigma of a frame's noise and gave 126 per megapixel on Veil, against 575
with a MAD sigma on the same raws and 1162 on the green channel of Siril's debayered,
calibrated frames (`pp_light_*.fit`, where the interpolation spreads a pixel of any colour over
several green ones); the count sat on the pattern's steep tail, so it went (ADR 0015). Smida's
pattern (ISO 400 in August: a lower noise floor and more dark current) was taken out by its
master dark and never moved; its figures may hold some star residue, since three groups of
8 to 12 frames sit within 2 px and a pixel under a star in two of the groups sees it in over
half the frames; the rule of R-046 only bounds the largest group.

## How to re-measure

```bash
LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro dotnet test LightsCheck.sln -c Release --filter "Category=Validation"
```

Or `/validate` in Claude Code, which prints expected versus measured for every case and stops on
drift instead of editing the figures.

## History

- 2026-10-24: V-008 and V-013 gain the drift pairs, whether the drift is approximate, the
  polar error's bounds, the drift it can cause per exposure and the verdict (R-049, R-050,
  ADR 0016), recorded from the console's run of the same day: V-008 39 pairs, exact, 4.2' to
  12.0', 0.64 px, `Ok`; V-013 29 pairs, approximate, `Unknown`, its three numeric keys left
  out since the session cannot give them. V-008's drift moved with the rule: -0.48 to -0.96
  px/min along Y (the 45 pairs held nine post-pause presses of 3 to 5 px), 1.1 to 1.96 px per
  exposure, the dash 9.9 to 17.7 px; the word stays `Insufficient`. V-013's drift did not
  move (the fallback is the old rule). All 13 cases re-measured on the branch the same day:
  `ValidationTests` 13 passed in 7 min 22 s on the Release build.

- 2026-10-10: V-001 and V-008 gain the darks count, the drift per exposure, the dash, the
  walking word, the pattern sigmas, the hot rate and the surviving fraction (R-042, R-043,
  R-046, R-048, ADR 0015); the earlier figures of both are unchanged. V-012 and V-013 recorded
  from the console's run of the same day on the Smida session, the second session of the
  project. The warm-pixel count at 3 sigma (126 on Veil, 744 on Smida) was recorded and
  dropped the same day, before any merge, for the reason in ADR 0015.

- 2026-10-09: V-001 recorded from the console's first run on the Veil session. The camera
  string was first written as `NIKON D3300` from the EXIF `Model`; libraw normalises the make
  and model, and the measured value `Nikon D3300` replaced it before the case was accepted.
  The duty was first written as 85.96 % from 87 x 120.2 s; the measured 86.06 % counts frame
  75's 133.2 s, and replaced it the same way.
- 2026-10-09: before 1.0.0, all eight cases re-measured on the merged code of steps 1 to 4
  (`ValidationTests`, 8 passed, the series case in 2 min 16 s on 4 threads); no figure
  changed.
- 2026-10-09: V-009 recorded from the console's `--out` run on the Veil session (R-033, R-034,
  ADR 0011): the CSV's `trail_on` is `RA` on every bad and marginal frame, and the runs at
  10 px the `spot_run_*` columns give are the eleven `grupuri in 10 px` of V-008's run, the
  same as the hand analysis found. The eight earlier cases were re-measured with it.
- 2026-10-09: V-010 recorded from the `ValidationTests` run with `expected: null`, which
  prints the tables it measured (R-036, ADR 0012): every cell is the figure the DITHERING rows
  of V-008 already print (41 moves = 10 + 23 + 8, the same runs), arranged. The nine earlier
  cases were re-measured with it.
- 2026-10-09: V-011 recorded from the `ValidationTests` run with `expected: null` (R-038, ADR
  0013). The senses rest on the two plate-solved raws of ADR 0013 (frames 1 and 61: the
  centre 0.2431 degrees west and 0.2208 degrees south of each other) and agree with the hand
  analysis: its nine DEC jumps of 12 to 29 px, all `+X`, are the nine `S (jos)` cells of 12 px
  or more; two small DEC components (8 and 6 px) read north, which the hand analysis did not
  count. The ten earlier cases were re-measured with it.
- 2026-10-10: V-011's cells re-worded for R-040 (ADR 0014): `V (dreapta)` became `← V`,
  `S (jos)` `↓ S`, `E (stanga)` `→ E`, `N (sus)` `↑ N`, the default keys being right east and
  up north; the pixels and the four counts did not move. Re-measured on the Veil session with
  the other ten cases.
