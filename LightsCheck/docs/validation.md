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
| V-008 | same, the whole session | series | 48 good / 17 marginal / 22 bad, the bad frames 1, 13, 15, 17, 18, 20, 22, 23, 25, 27, 38, 43, 45, 50, 52, 54, 56, 60, 63, 68, 70, 76; 87 registered; 41 moves (>= 5 px), 47 pauses, 9 pauses without a move; 38 distinct spots at 5 px, 25 at 10 px, 1 isolated, longest run at 10 px 10 frames (61-70); median move 13.29 px; rotation 8.5 arcmin; drift -0.20 / -0.73 px/min over the 36 pairs without a pause or a move (X / Y); session FWHM 3.12 px; read noise G1 2.9 ADU; swamping of R on frame 1 9.4; RA along Y (WCS); focus held, no dew, hot pixels fine, drift 1.52 px per exposure, a dash of 13.7 px in the run of 10, 53 frames in the 11 stretches whose dash reaches 3 px (61 %, R-084), word `Insufficient`; polar error between 4.5' and 6.7' (the DEC drift of 1.2 "/min; the tracking's rotation of 1.1'/h at DEC +31.6, the dithers' slews taken out of the 2.5'/h end to end), at most 0.46 px of drift per exposure, `Ok`; no darks; pattern measured on 87 crops: sigma 1.4 / 2.1 / 2.1 / 2.3 ADU on R / G1 / G2 / B (12, 10, 10 and 10 % of a frame's noise), 4 hot pixels per megapixel; 1.6 % of one frame's pattern survives in the mean (16 pairs within 1.5 px; ideal 1.1 %) | 2026-10-09, the drift, darks and pattern figures 2026-10-10, the drift and polar figures 2026-10-24 |
| V-009 | same, the whole session | words | the trail axis word `RA` on all 22 bad and all 17 marginal frames (angles 77 to 89 degrees in absolute value, coherence 0.94 and 0.86 or more); the `acelasi loc` cells `1/4 de la 57` to `4/4 de la 57` on 57 to 60, `1/10 de la 61` to `10/10 de la 61` on 61 to 70, `-` on 71 and 72 | 2026-10-09 |
| V-010 | same, the whole session | tables | the five dithering tables of R-036, cell by cell: how often (every 1 frame 12 times, 2 frames 22, 3 once, 4 three times, 5 and 6 once each), how far (median 13.29 px = 81.2", 79.6" until 2026-10-27 at 3.92 um, 10 moves of 5-10 px, 23 of 10-20, 8 of 20+), direction (X = DEC: 6 alone, 11 plus, 2 minus; Y = RA: 28 alone, 11 plus, 24 minus; both 7), coverage (70 frames within 3 px of another, 79 within 5 with 38 distinct spots and 8 runs, 86 within 10 with 25 and 11), the 8 runs at 5 px and the 11 at 10 px with their minutes (61-70, 10 frames, 22 min) | 2026-10-09 |
| V-012 | `Milkyway-Core/2026-08-12-Smida` (Nikon D3300, Tokina 11-20 at 20 mm f/2.8, SA GTi, a dark site) | inventory | 32 lights, 30 biases, 20 darks, `Nikon D3300`, 119.2 s at ISO 400, first start 2026-08-12 23:22:25, span 4996 s (1 h 23 min), duty 76.3 % | 2026-10-10 |
| V-013 | same, the whole session | series | 32 good, none marginal or bad; 32 registered; 2 moves, 31 pauses, 29 pauses without a move (the dither of a 135 mm night is under 2 px at 40.4 "/px); 3 distinct spots at 5 and at 10 px, none isolated, longest run at 10 px 12 frames (9-20); median move 15.52 px; rotation -6.3 arcmin; drift -0.07 px/min along Y (-2.8 "/min) over 29 pairs with a pause, approximate, since no two frames were shot back to back; polar alignment not measurable for the same reason; session FWHM 4.17 px; read noise G1 1.3 ADU; swamping of R on frame 1 10.1; RA axis unknown (no solved FITS at the root, no trailed frame); focus held, no dew, hot pixels `ATENTIE` (12 consecutive frames within 3 px), drift 0.17 px per exposure, a dash of 1.9 px in the run of 12, no frame in a stretch whose dash reaches 3 px (R-084), word `Static`; 20 darks; pattern measured on 32 crops: sigma 1.1 / 1.2 / 1.2 / 0.9 ADU on R / G1 / G2 / B (21, 19, 18 and 16 % of a frame's noise), 55 hot pixels per megapixel; 23.4 % of one frame's pattern survives in the mean (104 pairs within 1.5 px; ideal 3.1 %) | 2026-10-10 |
| V-011 | same, the whole session | moves | the `dithering` cells of R-038 on ten frames, in the symbol form of R-040 under its default keys (3: `DEC 13 px ↑ S`, 17: `DEC 28 px ↑ S`, 61: `RA 17 px ← V`, 6: `RA 12 px → E`, 5 and 19 and 80 with both components, 1 and 62 `-`) and, over the 41 moves, 24 components west, 11 east, 11 south, 2 north; the nine DEC jumps of 12 px or more the hand analysis counted are all south | 2026-10-10, the arrows re-worded 2026-11-07 |
| V-014 | `Veil/2026-11-06` (Nikon D3300, Samyang 135 f/2.0, SA GTi; 30 biases, no darks; at the root `result_9908s.fit`, the plate-solved stack of 83 frames) | series | 80 good / 2 marginal / 0 bad (the three short frames of a failing intervalometer, R-085, deleted from `lights\` by the owner on 2026-11-07), the marginal ones 4 and 59 (`DSC_0061`); frame 50 (`DSC_0051`), a 137.5 s shot, good with a note; 82 registered; 75 moves (>= 5 px), 81 pauses, 6 pauses without a move; 52 distinct spots at 5 px, 37 at 10 px, 14 isolated, longest run at 10 px 4 frames (11-14); median move 17.14 px = 104.7"; rotation 5.4 arcmin; drift +0.00 / -0.59 px/min over 6 pairs with a pause, approximate (X / Y); session FWHM 4.70 px; read noise G1 3.0 ADU; swamping of R on frame 1 11.6; RA along Y (from the solved FITS at the root, declination +31.5 degrees); focus held, no dew, hot pixels fine; drift 1.19 px per exposure, a dash of 3.6 px in the run of 4, 4 frames in the one stretch whose dash reaches 3 px (4.9 %, R-084), word `Ok`; polar alignment unknown (under 3 pairs of frames in a row without a pause or a move); no darks; pattern measured on 82 crops: sigma 1.1 / 2.4 / 2.3 / 1.5 ADU on R / G1 / G2 / B (8, 9, 9 and 5 % of a frame's noise), 0 hot pixels per megapixel; 1.5 % of one frame's pattern survives in the mean (8 pairs within 1.5 px; ideal 1.2 %) | 2026-11-07, re-recorded the same evening |

The series figures are the console's run of 2026-10-09 (88 s on 4 threads) and equal the hand
report's: the same 22 bad and 17 marginal frames, 47 pauses and 9 presses without effect, the
same groups of frames in 5 and 10 px (`raport.md`, section 6), the rotation of 8.5 arcmin, the
read noise and the swamping of red. The drift along Y was first recorded as -0.48 px/min over 45
non-move pairs, against the hand report's -0.8 px/min over the back-to-back pairs; the 45
included nine post-pause presses of 3 to 5 px, which halved it (ADR 0016). Since 2026-10-24 the
drift is over the 36 pairs without a pause and without a move, -0.20 / -0.73 px/min: the 39
back-to-back pairs of ADR 0016's measurement held three jumps of 6 to 9 px (frames 16, 22 and
54, the mount's, behind the doubled stars of 17 and 23), which the move rule now leaves out;
the hand report's -0.8 px/min was this figure. The polar upper bound reads 6.7' from the
tracking's rotation of 1.1'/h, the end-to-end 2.5'/h less the hour-circle convergence of the
dithers' net slew (most of what the registration saw, ADR 0016); the lower bound is 4.5'.

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
0015): worms along RA on Veil, a clean stack on Smida. Since 2026-11-07 the word reads the frames
whose streak would show and the pattern that piles (R-084, ADR 0039), and V-014, the Veil session
of 2026-11-06, is the third stack: dithered after 77 of 85 frames, `Ok`, where the rule of ADR
0007 read `Insufficient`. The dash of 9.9 px on Veil is the
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

- 2026-11-08: a median the user reads is said `on average` / `in medie` (ADR 0043); no figure
  of the analysis changed. At the owner's word the same day, before the run, V-010's first
  `cat de mult` cell became `in medie` (was `de obicei`), its `13.29 px = 81.2"` and its
  `measuredOn` kept. All 14 cases re-run with `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`:
  `Failed: 2, Passed: 12, Skipped: 0, Total: 14, Duration: 14 m 35 s`. V-010 passed with its new
  word. V-008 (`polarErrorMax: the frame yielded no figure (null)`) and V-011 (`frame 2: cell
  'RA 5 px', expected 'RA 5 px ← V'`) failed as recorded since 2026-10-27, the Veil session of
  2026-10-06 still holding no solved FITS at its root.
- 2026-11-07, evening: the session lines open with the total exposure to the second (R-086,
  ADR 0042); no figure of the analysis changed. All 14 cases re-run with
  `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: `Failed: 3, Passed: 11, Skipped: 0, Total:
  14, Duration: 16 m 25 s`. V-008 (`polarErrorMax: the frame yielded no figure (null)`) and V-011
  (`frame 2: cell 'RA 5 px', expected 'RA 5 px ← V'`) failed as recorded since 2026-10-27. V-014
  failed on `bad` (`Expected: 3, Actual: 0`): that day the owner had deleted the three short
  frames 16, 58 and 64 from `lights\`; that evening they also plate-solved the stack at the root,
  `result_9908s.fit` (83 frames, 9908.6 s), and took `darks\` out of the folder. At the owner's
  word V-014 was re-recorded from a console run on the folder as it then was: bad 3 -> 0 (the bad
  frames [16, 58, 64] -> none), registered 85 -> 82, moves 77 -> 75, pauses 82 -> 81, pauses
  without a move 5 -> 6, distinct spots 53 -> 52 at 5 px and 38 -> 37 at 10 px, isolated 15 ->
  14, drift Y -0.98 -> -0.59 px/min over 7 -> 6 pairs, RA axis `Unknown` -> `Y` (from the solve),
  drift per exposure 1.97 -> 1.19 px, dash 5.9 -> 3.6 px, frames whose streak would show 7 -> 4,
  darks 40 -> 0, pattern sigma 1.3 / 2.6 / 2.5 / 2.0 -> 1.1 / 2.4 / 2.3 / 1.5 ADU, pattern in
  the stack 1.4 -> 1.5 %. The good and marginal counts, the longest run, the median move, the
  rotation, the FWHM, the read noise, the swamping and the words `Ok` and polar `Unknown` stay.
  Re-run alone: `Passed: 1, Duration: 3 m 7 s`. This is the owner's decision the entry below
  left open; that branch and this one were merged the same evening.
- 2026-11-07, last: the default SynScan arrows are those of a Star Adventurer GTi driven by the
  SynScan Pro app, `↓` north and `↑` south, as the owner tested that pair (R-040, ADR 0041).
  V-011's five cells with a DEC part are re-worded, their pixels and senses and the four counts
  kept: frame 3 `DEC 13 px ↓ S` to `↑ S`, 5 `RA 4 px ← V, DEC 8 px ↑ N` to `↓ N`, 17
  `DEC 28 px ↓ S` to `↑ S`, 19 `RA 10 px ← V, DEC 26 px ↓ S` to `↑ S`, 80
  `RA 4 px → E, DEC 12 px ↓ S` to `↑ S`. The Veil root still holds no solved FITS, so V-011 ran
  on a mirror in a scratch folder: junctions to the session's `lights\` and `biases\` and, at its
  root, a copy of `procesate\bge-cc\result-bge-cc.fit`, the plate-solved stack ADR 0013 read,
  with `LIGHTSCHECK_TESTDATA_LARGE` naming the mirror's parent. `procesate\result.fit`, which the
  lines below name as the solved stack, carries no WCS: a first run with it at the mirror's root
  failed as V-011 has since 2026-10-27, so putting it back at the Veil root would not bring the
  senses back, and `result-bge-cc.fit` would. Before the code change, with the old cells:
  `Total tests: 1, Passed: 1, Total time: 1.5102 Minutes`; after it, with the new ones:
  `Total tests: 1, Passed: 1, Total time: 2.0385 Minutes`. Then all 14 cases ran on the real
  sessions, `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro` (the change touched a comment in
  `Report/`): `Total tests: 14, Passed: 11, Failed: 3, Total time: 15.7186 Minutes`. V-008
  (`polarErrorMax: the frame yielded no figure (null)`) and V-011 (`frame 2: cell 'RA 5 px',
  expected 'RA 5 px ← V'`) failed as recorded since 2026-10-27. V-014 failed on `bad`
  (`Expected: 3, Actual: 0`; its good and marginal counts passed): since it was recorded the
  session itself changed. The three short frames it rejects (`DSC_0016`, `DSC_0058`, `DSC_0064`)
  are no longer in its `lights\` (82 frames), the `darks\` folder is gone (`masters\` holds
  `dark_stacked.fit`), and a plate-solved stack, `result_9908s.fit`, sits at the root, so its RA
  axis, the senses of its moves and its polar figures would now be measured too. The branch
  moves no figure; V-014 stays as recorded until the owner decides.
- 2026-11-07, later: a frame exposed under half the session's usual exposure is bad, another
  exposure is noted (R-085, ADR 0040). V-014's classes move the owner's way, 81 good / 4
  marginal / 0 bad to 80 / 2 / 3, the bad frames 16 (34.9 s), 58 and 64 (0.2 s), which the
  clouds and jump rules had read as marginal and good; every other figure of V-014 stays,
  the short frames' 131 stars now out of the usual count without moving any clouds flag.
  V-008's frame 75 (133.2 s) gains a note and its +11 % jump the exposure's tail, its class
  unchanged. All 14 cases re-run with `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`:
  `Failed: 2, Passed: 12, Skipped: 0, Total: 14, Duration: 11 m 14 s`; V-008 (`polarErrorMax:
  the frame yielded no figure (null)`, after its classes and walking figures passed) and
  V-011 (`frame 2: cell 'RA 5 px', expected 'RA 5 px ← V'`) failed as recorded since
  2026-10-27, the Veil root still without its solved FITS.
- 2026-11-07: the walking-noise word reads the frames whose streak would show and the pattern
  that piles, not the map's spread (R-084, ADR 0039). V-008 and V-013 trade `walkingNoiseOk`
  for `walkingSharedFrames` (53 and 0: the frames inside the stretches of two or more frames in
  a row at one spot whose dash reaches 3 px, 11 such stretches on Veil, none at Smida, read
  first by a prototype over the console's frames CSV, then by the code); their words stay
  `Insufficient` and `Static`. V-014 recorded from the console's runs of the day on the Veil
  session of 2026-11-06, dithered after 77 of its 85 frames, which the old rule called
  `Insufficient` (38 of 85 distinct, 15 alone) and the new one `Ok` (7 frames in 2 stretches,
  8 %; 1.4 % of the pattern against 1.2 % ideal). All 14 cases re-run with
  `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: `Failed: 3, Passed: 11, Skipped: 0, Total:
  14, Duration: 11 m 3 s`. V-008 (`polarErrorMax: the frame yielded no figure (null)`, after
  its walking figures passed) and V-011 (`frame 2: cell 'RA 5 px', expected 'RA 5 px ← V'`)
  failed as recorded since 2026-10-27, the Veil root still without its solved FITS. V-014
  failed on `darks` (`Expected: 0, Actual: 40`): between the morning's run and the validation
  run the owner had added 30 biases and 40 darks to the folder; re-recorded from a second
  console run (read noise G1 3.0 ADU, swamping of R 11.6, 40 darks, every other figure the
  same) and re-run alone: `Passed: 1, Duration: 1 m 26 s`.
- 2026-11-02, fifth run: a Romanian count of 20 and up takes `de` (R-082, ADR 0037). At the owner's
  word the same day ("Da, aplica-le"), V-010's `cat de des` cell `22 ori` became `22 de ori`, its
  figure unchanged. All 13 cases re-run with `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`:
  `Failed: 2, Passed: 11, Skipped: 0, Total: 13, Duration: 9 m 16 s`; V-010 passed with its new
  word, V-008 (`polarErrorMax: the frame yielded no figure (null)`) and V-011 (`frame 2: cell 'RA
  5 px', expected 'RA 5 px ← V'`) failed as in the runs below, the solved `result.fit` still being
  in `procesate\`. No figure moved.
- 2026-11-02, fourth run: the Rejected tab and the console's REJECTED line in plain words (R-081,
  ADR 0036); all 13 cases re-run with `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: `Failed:
  2, Passed: 11, Skipped: 0, Total: 13, Duration: 7 m 31 s`. V-008 (`polarErrorMax: the frame
  yielded no figure (null)`) and V-011 (`frame 2: cell 'RA 5 px', expected 'RA 5 px ← V'`)
  failed as in the runs below, the solved `result.fit` still being in `procesate\`. No figure
  moved.
- 2026-11-02, third run: the Frames tab and the reasons in plain words (R-079, ADR 0034); all 13
  cases re-run with `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: `Failed: 2, Passed: 11,
  Skipped: 0, Total: 13, Duration: 6 m 18 s`. V-008 (`polarErrorMax: the frame yielded no figure
  (null)`) and V-011 (`frame 2: cell 'RA 5 px', expected 'RA 5 px ← V'`) failed as in the two runs
  below, the solved `result.fit` still being in `procesate\`. No figure moved.
- 2026-11-02, later: the Summary tab and its console sections in plain words (R-078, ADR 0033);
  all 13 cases re-run with `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: `Total tests: 13`,
  11 passed, `Total time: 10.7914 Minutes`. V-008 (`polarErrorMax: the frame yielded no figure
  (null)`) and V-011 (`frame 2: cell 'RA 5 px', expected 'RA 5 px ← V'`) failed as in the run
  below, the solved `result.fit` still being in `procesate\`; V-010 passed with its new words.
  No figure moved.
- 2026-11-02: the Dithering texts in plain words (R-077, ADR 0032); all 13 cases re-run with
  `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: `Total tests: 13`, 10 passed, `Total time:
  8.5354 Minutes`. V-010 failed on its words alone: `cat de mult: measured [de obicei | 13.29 px
  = 81.2" || 5-10 px | 10 || 10-20 px | 23 || 20+ px | 8], expected [median | 13.29 px = 81.2" ||
  ...]`; its tables now read `de obicei`, `pe ambele` and the title `apropierea cadrelor` for
  `median`, `ambele` and `acoperire`, and a console run on Veil the same day printed every one of
  its figures unchanged. V-008 (`polarErrorMax: the frame yielded no figure (null)`) and V-011
  (`frame 2: cell 'RA 5 px', expected 'RA 5 px ← V'`) failed because the Veil session's root no
  longer holds the plate-solved `result.fit`, which sits in `procesate\` since 2026-10-27 12:53:
  without a solve at the root the moves have no sense on the sky and the polar alignment no upper
  bound (R-025, R-038, R-050). At the owner's word the same day: V-010's three words became the
  new ones (`de obicei`, `pe ambele`, the title `apropierea cadrelor`), every figure kept, its
  `measuredOn` kept at 2026-10-09; V-008 and V-011 stay as recorded and fail until a solved FITS
  is back at the Veil root, which the owner left as it is.
- 2026-10-27: the pixel size of a raw session comes from the raw's 35 mm-equivalent focal
  length over its mosaic's diagonal (R-066, ADR 0026), 4.00 um on the D3300 at 135 mm instead of
  the 3.92 um constant, so the Veil scale is 6.11 "/px instead of 5.99; all 13 cases re-run on
  the two sessions (11 min 26 s), 12 passed, one moved: V-010's median move cell, `13.29 px =
  79.6"` to `13.29 px = 81.2"` (+2.0 %, the scale alone; the pixels did not move), taken at the
  owner's word. V-008's polar error stayed within its 0.3' tolerance. The other arcsecond
  figures the rows quote (V-008's DEC drift of 1.2 "/min, V-013's 40.4 "/px and -2.8 "/min)
  were written at 3.92 um and are not compared by the tests; at the new scale they would read
  1.2, 41.1 and -2.9.
- 2026-10-26: the solve's flip against the lights became the raws' alone (ADR 0023); all 13
  cases re-run on the two sessions, 13 passed, no figure moved, as expected for sessions of
  raws, whose mapping is unchanged. The FITS copy of the Veil session the ADR measured is a
  temporary folder, not a case.
- 2026-10-25: after the move to .NET 8 (ADR 0017), all 13 cases re-run on the net8 build with
  `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: `Total tests: 13, Passed: 13, Total
  time: 8.8582 Minutes`; no figure moved.

- 2026-10-24: V-008 and V-013 gain the drift pairs, whether the drift is approximate, the
  polar error's bounds, the drift it can cause per exposure and the verdict (R-049, R-050,
  ADR 0016), recorded from the console's run of the same day: V-008 36 pairs, exact, 4.5' to
  6.7', 0.46 px, `Ok`; V-013 29 pairs, approximate, `Unknown`, its three numeric keys left
  out since the session cannot give them. V-008's drift moved with the rule: -0.48 to -0.73
  px/min along Y (the 45 pairs held nine post-pause presses of 3 to 5 px; a first reading of
  the day over 39 back-to-back pairs, -0.96, still held three mount jumps of 6 to 9 px, which
  the review of the same day took out), 1.1 to 1.52 px per exposure, the dash 9.9 to 13.7 px;
  the word stays `Insufficient`. V-013's drift did not move (the fallback is the old rule).
  All 13 cases re-measured on the branch the same day: `ValidationTests` 13 passed in 7 min
  22 s on the Release build before the review's fixes, and 13 passed in 7 min 39 s after them.

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
- 2026-10-12: the senses of V-011 cross-checked on four more plate-solved frames, around the
  run 61-70 (`siril-cli` 1.4.4, `platesolve 314.43,31.61 -focal=130.1 -pixelsize=3.91
  -noflip` on Siril's `process/pp_light_000NN.fit`, online Tycho-2, a private copy of Siril's
  settings; 6.198"/px). The centre moved 111.8" west and 1.7" north from 60 to 61 (V-011's
  move of 17 px west on RA), 23.2" west and 16.2" south from 61 to 64, 21.2" east and 25.9"
  north from 64 to 66, and 2.0" west and 9.8" north from 61 to 66 (10 min 49 s). The WCS rule
  of ADR 0013 on the console's own `dx` and `dy` of the same day (`--out`) gives 107.9" west
  1.5" north, 20.6" west 16.4" south, 19.0" east 25.9" north and 1.6" west 9.5" north: the
  same sense on every component, within 4" on RA and 0.3" on DEC. A cross-check: no case
  re-run, no figure changed; it corrects ADR 0013's reading of the drift as a lag (a dated
  note there).
