# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json` (read by `ValidationTests`, trait `Category=Validation`, not in the gate,
ADR 0006) and is explained here. A number changes only after a re-measurement, with a dated
line under History saying old value, new value and why. The sessions are whole folders of
camera raws under `E:\Photografie\Astro`, the folder `DARKSCHECK_TESTDATA_LARGE` names; a
case names its session relative to it. A case whose session is absent returns early with a
message.

The hand analysis these cases reproduce is `docs/validation/2026-08-12-smida/report.md` (the
report delivered on 2026-10-11), with its figures in `results.json`,
`results_supplementary.json` and the CSV files beside it, from the Python scripts in
`prototype/`, which read the same libraw through rawpy.

## Kinds of case

| Kind | What runs | What is recorded | Tolerance |
| --- | --- | --- | --- |
| `inventory` | `InventoryRun` on the session | the counts of darks, biases and lights, the camera, the dominant exposure and ISO of the darks, the start of the first dark, the span and the median interval of the darks, their long pauses, the two verdicts of check 1 | 0.01 s exposure, 1 s span, 0.1 s interval; counts and words exact |
| `match` | `DarkSeriesRun` on the session with `--against` the folder the case's `against` names, when it has one (a minute to two: the lights are decoded too) | the index with its ranks and the label; with lights: the lights used, the persistence threshold, the persistent pixels, the overlap with the master at 10 / 20 / 50 / 100 ADU-eq both ways, the missed pixels and their master median, the amplitude pixels, the night's ratio, the first and last light's, the verdict; with a partner: its frames, normalisation, index and label, the darks-over-partner median and quartiles, pixel count, degrees and verdict, the two overlap counts at 50 ADU-eq | index 0.2 ADU-eq, ratios 0.01, counts 2, degrees 0.1, the missed median 1 ADU-eq; words exact |
| `darks` | `DarkSeriesRun` on the session (seconds to a minute) | the frames used, the quantisation step, the normalisation, the master's counts at the five hot levels, the hot pixels always present and flickering, their median amplitude, the hot and quiet temporal sigma, the RTS candidates, the dead and saturated, the per-frame sigma, the first frame's residual sigma, the master's sigma, the row banding ratio, the noise curve, the master's predicted noise, the median-of-half figure, the dark map's centre and bottom, the warming ratios and degrees, the structured banding peak to peak, the leak offset and region, the read noise per plane, the dark current's median, centre and top-left, the bias clipping share, the six verdicts, the lit frames and the scale of the warming fit (R-028), the glow part, the constant part and the cause of the verdict region, the side with the most constant light and its light after the bias (R-029, ADR 0014) | ADU 0.02, ratios 0.01, counts 2, degrees 0.1, banding ratio 0.3, clipping share 0.01; words exact |

## Cases

| ID | Session | Kind | Figures | Measured on |
| --- | --- | --- | --- | --- |
| V-001 | `Milkyway-Core/2026-08-12-Smida` (Nikon D3300, Tokina 11-20 at 20 mm, SA GTi) | inventory | 20 darks, 30 biases, 32 lights, `Nikon D3300`, darks 119.2 s at ISO 400, first dark 2026-08-13 00:48:53, span 2399.2 s, interval 120.0 s, no pause over 5 s, darks vs lights `Ok`, bias `Ok` (0.05 s) | 2026-10-11 |
| V-002 | `Flats and Darks/Darks/2026-10-10-ISO1600-120s-15.8celsius-idx67/darks` (the library, Nikon D3300, 15.2-16.5 °C room; the folder carried no `-idx67` until the owner renamed it on the label, see History 2026-10-20) | inventory | 30 darks, no biases, no lights, 120.2 s at ISO 1600, first dark 2026-10-10 23:03:24, span 3628.2 s, interval 121.0 s, no pause over 5 s, both verdicts `Unmeasured` | 2026-10-11 |
| V-003 | the Smida session | darks | 20 frames, step 2, norm 0.993; 2283 / 443 / 111 / 24 / 0 pixels above 10 / 20 / 50 / 100 / 500 ADU-eq (the prototype's 2086 / 433 / 107 / 24 / 0 above the raw levels: ten pixels sit at exactly 20.0 ADU, see ADR 0007), 412 always, 19 flicker, median 33, temporal sigma 5.58 hot and 1.45 quiet, 92 RTS; sigma 1.514 per frame, residual 1.474, master 0.628, rows 4.1 x white noise; the mean of 1 / 2 / 4 / 5 / 10 frames at 1.426 / 1.016 / 0.725 / 0.652 / 0.467 ADU, the median master 0.400 (1.25 x 1.426 / sqrt(20)), median-of-10 0.786; dark map centre -0.551, bottom -0.174; warming 0.826 → 1.068, +2.2 °C, plateau 1.10 of the master (time constant 15 min, 0.2 °C left at the last frame), 2 frames (1, 2) more than 2 °C under it, the master 0.7 °C under the plateau without them at 5 % more noise (2026-10-21, ADR 0017); banding 0.387 rows, 0.169 columns; leak +0.09 top-left on dark minus bias; read noise 1.563 / 1.257 / 1.257 / 1.447; dark current median 0.140, centre 0.126, top-left 0.219; 57.8 % of bias pixels below black; verdicts OK / OK (ATENTIE on the drift until 2026-10-21) / OK / OK / OK / OK; no lit frame, scale of the warming fit 0.10 ADU-eq, top-left glow 0.05 and constant 0.05 (0.02 in the bias, 0.04 remains), `Glow`; sides after the bias: top 0.04, bottom -0.01, left -0.03, right 0.08 ADU-eq (±0.02 .. 0.05), none a leak (2026-10-20) | 2026-10-11 |
| V-005 | the Smida session against the library | match | index 25.2 (`idx25`, ranks 121 .. 483 of 24.2 Mpx), label `2026-08-12-Smida-idx25`; 32 lights, 26 needed, 86 persistent, 85 / 85 / 70 / 16 of them above 10 / 20 / 50 / 100 ADU-eq in the master (of its 2283 / 443 / 111 / 24), 1 missed at 3 ADU-eq; the lights at 1.00 of the master over the night on 443 pixels, 0.72 at the first light, 0.94 at the last, `Direct`; the library: 30 frames, norm 4.007, index 67.4, label `2026-10-10-ISO1600-120s-15.8celsius-idx67`; Smida over the library 0.422 (0.381 .. 0.443) on 434 pixels hot in both, -7.5 degrees, `CosmeticOnly`; 111 of 111 Smida pixels above 50 are above 50 in the library, 425 of the library's 528 above 50 are above 20 in Smida | 2026-10-16 |
| V-006 | the library alone | match | index 67.4, `idx67`, label `2026-10-10-ISO1600-120s-15.8celsius-idx67`; no lights, no partner | 2026-10-16 |
| V-007 | `Veil/2026-10-06-3h` (87 lights of 120.2 s at ISO 1600, 30 biases of 0.2 s, no darks; Nikon D3300, Samyang 135) against the library, whose darks stand in for the session's (ADR 0011) | match | the library's index 67.4 and label; 44 lights measured (the cap of 60 on 87 takes one in two), 36 needed, 236 persistent, 225 / 224 / 214 / 117 of them above 10 / 20 / 50 / 100 ADU-eq in the library's master (of its 5608 / 3940 / 528 / 176), 12 missed at 3 ADU-eq; the lights at 0.43 of the master over the night on 3933 pixels, 0.41 at the first light, 0.37 at the last, `CosmeticOnly` (the library 7.2 degrees warmer than the night, which the owner's notes put at 8-10 degrees by the camera against the library's 15.8 degree room); no partner rows | 2026-10-16 |
| V-004 | the library | darks | 30 frames, step 2, norm 4.007; 5608 / 3940 / 528 / 176 / 13 above the levels, 3759 always, 71 flicker, median 33 ADU-eq, temporal sigma 23.64 and 5.23, 291 RTS; one saturated pixel; sigma 5.70, residual 4.94, master 2.08, rows 1.8 x; curve 4.83 / 3.46 / 2.51 / 2.27 / 1.67, the median master 1.105, median-of-15 2.46; warming 0.735 → 1.086 (the console's, at the 80.1 ADU threshold; the prototype's 100 ADU set gave 0.736 → 1.085), +3.4 °C, plateau 1.10 of the master (time constant 18 min, 0.1 °C left at the last frame), 5 frames (1 .. 5) more than 2 °C under it, the master 0.4 °C under the plateau without them at 10 % more noise (2026-10-21, ADR 0017); leak +0.22 top-left on the dark master (no biases); verdicts OK / OK (the drift verdict was ATENTIE until 2026-10-21) / OK / OK / ATENTIE (one saturated) / OK; no lit frame, scale 0.02 ADU-eq, top-left glow 0.22 and constant 0.01, `Glow` (2026-10-20) | 2026-10-11 |
| V-008 | `Flats and Darks/Darks/Lightleaks` (the owner's deliberate leak set of 2026-10-20: 30 darks of 120.2 s at ISO 1600 at room temperature, the eyepiece uncovered and a lamp aimed at the camera from three positions in turn, the first ten frames of the hour discarded, `conditii.txt` beside the frames; the 30 Veil biases of 2026-10-06 copied by the owner into `biases\` the same day, the same ISO; no lights) | darks | 30 frames, step 2, norm 4.007; 7759 / 5443 / 3135 / 400 / 31 above the levels, 5246 always, 65 flicker, median 53 ADU-eq, temporal sigma 34.63 and 6.90, 439 RTS; ten saturated pixels; sigma 7.34, residual 6.51, master 2.999, rows 1.6 x; curve 6.26 / 4.48 / 3.24 / 2.92 / 2.12, the median master 1.43, median-of-15 3.17; dark map centre 2.08, bottom 2.16; warming 0.74 → 1.13, +3.6 °C, plateau 1.17 (28 min, 0.4 °C left), 8 frames (1 .. 8) off it, the master 0.9 °C under the plateau without them at 17 % more noise; banding 0.76 rows, 1.00 columns; leak +0.49 top-left on dark minus bias, under the 1 ADU-eq threshold; read noise 3.38 / 2.96 / 2.95 / 3.38; dark current median 2.16 raw ADU (0.54 ADU-eq), centre 2.00, top-left 3.97; 53.6 % of bias pixels below black; lit frames 26 to 30 at +0.17 / +0.46 / +0.45 / +0.47 / +0.44 ADU-eq over the warming fit (scale 0.02; the lamp's third position, moved during frame 26), top-left glow 0.32 and constant 0.17 of which -0.01 in the bias and 0.18 remains, `Glow`; sides after the bias: top +0.20 (±0.05), bottom -0.05, left +0.03, right +0.10 ADU-eq, the top side the eyepiece band of frames 1 to 25, constant light over 0.15 and 3 x its error; verdicts ATENTIE (lit frames and constant light) / OK (ATENTIE on the drift until 2026-10-21) / OK / OK / ATENTIE (saturated) / OK | 2026-10-20 |
| V-009 | `Flats and Darks/Darks/2026-10-11-ISO1600-120s-13.3celsius-idx47/darks` (the second library, 30 darks of 120.2 s at ISO 1600 shot 2026-10-11 23:19 to 00:20 at 13.3 °C by the owner's note in the name; no biases) | darks | 30 frames, step 2, norm 4.007; 4531 / 2538 / 286 / 87 / 4 above the levels, 2362 always, 42 flicker, median 25 ADU-eq, temporal sigma 19.21 and 4.55, 228 RTS; no dead or saturated pixel; sigma 4.89, residual 4.50, master 1.782, rows 2.1 x; curve 4.42 / 3.13 / 2.22 / 2.00 / 1.46, the median master 1.01, median-of-15 2.23; dark map centre 0.80, bottom 1.20; warming 0.79 → 1.10, +2.9 °C, plateau 1.11 (24 min, 0.2 °C left), 4 frames (1 .. 4) off it, the master 0.6 °C under the plateau without them at 7 % more noise, drift verdict OK; banding 0.60 rows, 0.56 columns; leak +0.19 bottom-left on the dark master; no lit frame, scale 0.01, bottom-left glow 0.05 and constant 0.15, `Constant` (a readout offset by the bias of V-008, undecidable here without biases); index 47.2, `idx47` | 2026-10-20 |
| V-010 | `Flats and Darks/Darks/2026-10-11-ISO1600-120s-9.5celsius-idx33/darks` (the third library, 30 darks of 120.2 s at ISO 1600 shot 2026-10-15 22:58 to 23:59, the folder's date notwithstanding, at 9.5 °C by the owner's note in the name; no biases) | darks | 30 frames, step 2, norm 4.007; 3757 / 738 / 174 / 45 / 0 above the levels, 663 always, 43 flicker, median 30 ADU-eq, temporal sigma 22.26 and 4.08, 233 RTS; no dead or saturated pixel; sigma 4.33, residual 4.08, master 1.580, rows 2.5 x; curve 3.99 / 2.84 / 2.01 / 1.81 / 1.30, the median master 0.91, median-of-15 2.04; dark map centre 0.64, bottom 1.08; warming 0.78 → 1.08, +2.8 °C, plateau 1.10 (21 min, 0.2 °C left), 3 frames (1 .. 3) off it, the master 0.6 °C under the plateau without them at 5 % more noise, drift verdict OK; banding 0.60 rows, 0.52 columns; leak +0.19 bottom-left on the dark master; no lit frame, scale 0.02, bottom-left glow 0.02 and constant 0.17, `Constant`; index 32.9, `idx33` | 2026-10-20 |

## History

- 2026-10-11: V-001 and V-002 recorded from the EXIF read of the Python prototype
  (`prototype/nefexif.py`, `docs/validation/2026-08-12-smida/*_per_frame.csv`) and confirmed
  by the console on the same folders.
- 2026-10-11: V-003 and V-004 recorded from the console's first runs of plan step 2 and checked
  line by line against `results.json` and `results_supplementary.json`: every figure the two
  define the same way agrees within the tolerances; the counts at the hot levels differ by the
  normalisation (ADU-eq against raw ADU), the library's counts by the master (median here, mean
  in the prototype's streamed pass), and the column banding by the clamp of the hot pixels. The
  prototype's 0.628 master sigma, 0.826 / 1.068 warming, 0.126 / 0.219 dark current and 1.257
  read noise are reproduced. For the library the prototype streamed the frames and left only
  the master's counts, its regions and the warming: V-004's stability, RTS, sigmas, noise curve,
  banding and master noise are the console's own first run, pinned against drift, not
  prototype figures.
- 2026-10-16: V-005 and V-006 recorded from the console's first run of plan step 3 and checked
  against the prototype's section 9 (`results.json`, `light_hot_pixels.csv`): the 86 persistent
  pixels, 85 of them in the master above 20, the 1.00 over the night with 0.72 at the first
  light and 0.94 at the last, and the index 25 are the prototype's; 70 of 111 above 50 against
  the prototype's 68 of 107 by the ADU-eq levels (ADR 0007); the library's 67.4 against the
  prototype's 66.5 by the median master (the prototype streamed a mean). The darks-over-library
  ratio is measured on the pixels hot in both masters (ADR 0008): 0.422, i.e. the library at
  2.37 x Smida after the ISO factor, -7.5 degrees, against the prototype's 2.2 on the Smida
  pixels above 50 (its 8.9 raw); on the library's pixels above 20 alone it would be 3.1.
- 2026-10-16: the master noise of V-003 and V-004 re-recorded after the step's review changed
  the estimator from the spatial sigma over sqrt(N) (a mean master) to 1.25 x the temporal
  sigma over sqrt(N) (the median master actually built): 0.339 → 0.400 on Smida, 1.04 → 1.105 on
  the library; the library's warming figures re-recorded as the console measures them.
- 2026-10-16: at plan step 5, V-001 to V-006 re-run on the two sessions after the window step
  (the report as rows, the `Normalisation` record): every case passes within its tolerance and
  the console's figures are the recorded ones to the printed decimals (Smida: 2283 / 443 / 111 /
  24 / 0, warming 0.83 → 1.07, read noise 1.56 / 1.26 / 1.26 / 1.45, master noise 0.40, 86
  persistent, 1.00 / 0.72 / 0.94, idx25, 0.42 on 434 pixels, -7.5 degrees; the library:
  5608 / 3940 / 528 / 176 / 13, 0.73 → 1.09, one saturated, 1.11, idx67); no figure moved.
- 2026-10-16: V-007 recorded from the console's first run of the lights-only session (task 6,
  ADR 0011): the Veil lights against the 2026-10-10 library, 96 s at 4 threads. The run reads
  `lights / dark 0.43 pe noapte (mediana pe 44 cadre; primul 0.41, ultimul 0.37; 3933 pixeli)`
  and `echivalent idx29 (dark x 0.43, adica -7.2 grade fata de dark)`; no prototype figure
  exists for this session, so the case pins the console's own first run against drift, as
  V-004 does. The 7.2 degrees agree with the owner's estimate of the night (8-10 degrees by the
  camera) against the library's room (15.8 degrees). The run's lines the case pins:

  ```
  === SESIUNE: E:\Photografie\Astro\Veil\2026-10-06-3h ===
      darks              : (lipsa; se folosesc dark-urile partenerului E:\Photografie\Astro\Flats and Darks\Darks\2026-10-10-ISO1600-120s-15.8celsius\darks)
      biases             : biases\ (30 cadre .NEF)
      lights             : lights\ (87 cadre .NEF)
      aparat             : Nikon D3300
      expunere partener  : 120.2 s la ISO 1600 (toate cadrele)
      expunere biases    : 0.2 s la ISO 1600 (toate cadrele)
      expunere lights    : 120.2 s la ISO 1600 (86 din 87 cadre; expuneri sau ISO diferite)
      interval lights    : 2026-10-06 23:35:03 -> 2026-10-07 02:57:49 (3 h 22 min); 87 cadre la 134.5 s; 47 pauze peste 5 s (cea mai lunga 141 s inainte de cadrul 76)
      potrivire          : OK: dark-urile partenerului au expunerea (120.2 s) si ISO-ul (1600) lights-urilor
      bias               : OK: 0.2 s
  === DARK-URI (ale partenerului ...\2026-10-10-ISO1600-120s-15.8celsius\darks): 30 cadre decodate din 30, 6016 x 4016 px, scara 150 .. 4095 ADU (libraw (exact)), CFA RGGB ===
    9. LIGHTS FATA DE DARK
      pixeli fierbinti   : 236 pixeli persistenti in lights (izolati, peste 6 sigma locale, in cel putin 36 din 44 cadre)
      in masterul dark   : peste 10: 225 din 236 (95 %); peste 20: 224 din 236 (95 %); peste 50: 214 din 236 (91 %); peste 100: 117 din 236 (50 %)  [ADU-eq]
      master in lights   : peste 10: 225 din 5608 (4 %); peste 20: 224 din 3940 (6 %); peste 50: 214 din 528 (41 %); peste 100: 117 din 176 (66 %)
      nepotriviti        : 12 pixeli persistenti in lights sub 20 ADU-eq in master (mediana lor in master 3 ADU-eq)
      amplitudine        : lights / dark 0.43 pe noapte (mediana pe 44 cadre; primul 0.41, ultimul 0.37; 3933 pixeli)
      verdict            : ATENTIE: doar corectie cosmetica (-cc=dark): raport 0.43, dark-ul e mai cald cu 7.2 grade si vede toti pixelii fierbinti, dar scaderea l-ar supracorecta
    INDEX
      dark-uri (partener): idx67 (67.4 ADU-eq: mediana pixelilor de pe rangurile 5-20 pe Mpx, al 122-lea .. al 483-lea din 24.2 Mpx, normalizat la ISO 400 si 120 s)
      eticheta           : 2026-10-10-ISO1600-120s-15.8celsius-idx67
      lights             : echivalent idx29 (dark x 0.43, adica -7.2 grade fata de dark)
  ```

  The library's own checks 2 to 8 in that run are V-004's figures (the same 30 darks), with
  the Veil biases giving the read noise (2.96 ADU on G1 at ISO 1600) and the dark current.
- 2026-10-20: the library folder `2026-10-10-ISO1600-120s-15.8celsius` was renamed by the owner
  to its label, `...-15.8celsius-idx67`; the paths of V-002, V-004, V-005, V-006 and V-007
  follow it so the cases still find the session. Their expected labels were not changed: the
  label is the folder's name plus `-idx67` (R-061), so the console now reads
  `...-15.8celsius-idx67-idx67` and V-005, V-006 and V-007 fail on the label alone, a finding
  for the owner (strip an existing `-idxNN` from the name, or keep the folders unrenamed).
- 2026-10-20: V-003 and V-004 extended with the figures of R-028 and R-029 from the console's
  first run with them (the leak task, ADR 0012); the hand regressions of 2026-10-20 on the
  prototype's per-frame CSV files give the library's top-left slope 0.88 ADU raw on the ratio,
  0.22 ADU-eq, the console's 0.22, and Smida's left-edge slope 0.09 ADU, 0.09 ADU-eq, beside
  the console's top-left 0.05. V-008 recorded from the console's first run on the leak set, the
  figures of the other checks pinned against drift; the lit frames are the five the hand
  regression found at +0.25 .. +1.9 ADU raw over the fit on the first nineteen frames (the
  console, fitting all thirty with three clipping passes, reads +0.17 .. +0.47 ADU-eq, that is
  +0.7 .. +1.9 ADU raw).
- 2026-10-20 (later): the owner copied the Veil biases into the leak set's `biases\`, so V-008 moved
  to the session root and gained the biases' figures (read noise, dark current, clipping share,
  the bias part of the split: -0.01 in the bias, 0.18 remains); the other figures did not move.
  V-009 and V-010 recorded from the console's first run on the two other libraries the owner
  keeps beside the first (folders renamed on their labels, `idx47` and `idx33`; the `9.5celsius`
  folder's frames are dated 2026-10-15, its name 2026-10-11): their bottom-left constant of
  0.15 .. 0.17 ADU-eq with a glow part near zero is the readout offset V-008's biases show at the
  bottom (0.96 raw ADU bottom-left on the bias master), read as `Constant` without biases. The
  three libraries' indexes, 67 / 47 / 33 for 15.8 / 13.3 / 9.5 °C, follow the doubling rule
  within a degree (67 → 33 is one halving for 6.3 °C). Their labels also double the suffix; the
  cases do not record a label.
- 2026-10-20 (review): the span guard of R-029 (`Th.LeakFitMinSpan` 0.1), the cause read on the
  side of the departure and the raw figures in the row were added after the code review; all
  ten cases re-run, V-001 to V-004 and V-008 to V-010 pass with no figure moved (the measured
  spans are 0.24 to 0.38, all over the guard), V-005 to V-007 still fail on the doubled label.
- 2026-10-20 (label): the owner chose to strip an existing `-idx` and digits from the folder's
  name before the tag is appended (R-061, ADR 0013), so the renamed library reads
  `2026-10-10-ISO1600-120s-15.8celsius-idx67` again. All ten cases re-run with
  `DARKSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: `Passed! - Failed: 0, Passed: 10, Skipped: 0,
  Total: 10, Duration: 6 m 6 s`; V-005, V-006 and V-007 pass on the label with no change to
  `validation.json`, no figure moved. The labels of V-009 and V-010 (not recorded) read
  `...-13.3celsius-idx47` and `...-9.5celsius-idx33` by the same rule. Re-run after the code
  review's fixes (the unmeasured tag, a name that is only a tag, the pattern): `Passed! -
  Failed: 0, Passed: 10, Total: 10, Duration: 6 m 23 s`, the same.
- 2026-10-20 (sides): after the owner's objection that every frame of the leak set leaks, not
  five, check 2 reads the constant light per side after the bias (R-029, ADR 0014): on the
  leak set the top side is +0.20 ± 0.05 ADU-eq (the eyepiece band of frames 1 to 25) and the
  verdict now names it beside the five lit frames; Smida's sides stay under 0.08. V-003 and
  V-008 record the worst side; the other figures did not move. Ten of ten pass.
- 2026-10-21 (plateau): check 3 fits the series to a plateau and judges whether it has settled
  (R-023, ADR 0017), so the drift verdict of V-003, V-004, V-008, V-009 and V-010 changes from
  `Warning` to `Ok`, the owner's intended change, and each darks case records the plateau, the
  time constant, the degrees left at the last frame and the count of frames more than two
  degrees off the plateau (the band was 1 °C in the first run of the day, which named 6 / 11 /
  18 / 12 / 10 frames, half of every library; the owner set it to 2 °C). Measured on the five series: plateau 1.10 / 1.10 / 1.17 / 1.11 / 1.10 of the
  master, time constant 15 / 18 / 28 / 24 / 21 min, 0.2 / 0.1 / 0.4 / 0.2 / 0.2 °C left, 2 / 5 /
  8 / 4 / 3 frames off the plateau (Smida, the library, the leak set, the 13.3 and the 9.5
  libraries). The two libraries with their ten discarded frames put back start at 0.38 and 0.39
  of the master, about 10 °C under the plateau (not a case). All ten cases re-run with
  `DARKSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: `Passed! - Failed: 0, Passed: 10, Skipped: 0,
  Total: 10, Duration: 7 m 2 s`; no other figure moved. Re-run after the band went to 2 °C and
  the counts above were re-recorded: `Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10,
  Duration: 8 m 44 s`.
- 2026-10-25: after the move to .NET 8 (ADR 0018), all ten cases re-run on the net8 build
  with `DARKSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: `Total tests: 10, Passed: 10, Total
  time: 7.0789 Minutes`; no figure moved. The working set ADR 0007 measured, re-measured the
  same night with the console on the same code built for .NET 9 and for .NET 8 (peak working
  set, two runs each): the 30-frame library at 3.13 / 3.16 GB in 26.5 / 26.2 s on .NET 9 and
  3.22 / 3.29 GB in 25.9 / 24.2 s on .NET 8; the Smida session at 5.75 / 6.09 GB in 64.8 / 64.3
  s on .NET 9 and 5.72 / 5.87 GB in 64.7 / 62.9 s on .NET 8. The runtime moves neither. Smida
  is above ADR 0007's 4.5 to 4.7 GB and 35 s on both runtimes alike, so the gap comes from what
  the console does on that folder since 2026-10-16, not from .NET 8; its cause was not looked
  for, and ADR 0007 is left as it is.
