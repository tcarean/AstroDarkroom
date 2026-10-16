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
| `darks` | `DarkSeriesRun` on the session (seconds to a minute) | the frames used, the quantisation step, the normalisation, the master's counts at the five hot levels, the hot pixels always present and flickering, their median amplitude, the hot and quiet temporal sigma, the RTS candidates, the dead and saturated, the per-frame sigma, the first frame's residual sigma, the master's sigma, the row banding ratio, the noise curve, the master's predicted noise, the median-of-half figure, the dark map's centre and bottom, the warming ratios and degrees, the structured banding peak to peak, the leak offset and region, the read noise per plane, the dark current's median, centre and top-left, the bias clipping share, the six verdicts | ADU 0.02, ratios 0.01, counts 2, degrees 0.1, banding ratio 0.3, clipping share 0.01; words exact |

## Cases

| ID | Session | Kind | Figures | Measured on |
| --- | --- | --- | --- | --- |
| V-001 | `Milkyway-Core/2026-08-12-Smida` (Nikon D3300, Tokina 11-20 at 20 mm, SA GTi) | inventory | 20 darks, 30 biases, 32 lights, `Nikon D3300`, darks 119.2 s at ISO 400, first dark 2026-08-13 00:48:53, span 2399.2 s, interval 120.0 s, no pause over 5 s, darks vs lights `Ok`, bias `Ok` (0.05 s) | 2026-10-11 |
| V-002 | `Flats and Darks/Darks/2026-10-10-ISO1600-120s-15.8celsius/darks` (the library, Nikon D3300, 15.2-16.5 °C room) | inventory | 30 darks, no biases, no lights, 120.2 s at ISO 1600, first dark 2026-10-10 23:03:24, span 3628.2 s, interval 121.0 s, no pause over 5 s, both verdicts `Unmeasured` | 2026-10-11 |
| V-003 | the Smida session | darks | 20 frames, step 2, norm 0.993; 2283 / 443 / 111 / 24 / 0 pixels above 10 / 20 / 50 / 100 / 500 ADU-eq (the prototype's 2086 / 433 / 107 / 24 / 0 above the raw levels: ten pixels sit at exactly 20.0 ADU, see ADR 0007), 412 always, 19 flicker, median 33, temporal sigma 5.58 hot and 1.45 quiet, 92 RTS; sigma 1.514 per frame, residual 1.474, master 0.628, rows 4.1 x white noise; the mean of 1 / 2 / 4 / 5 / 10 frames at 1.426 / 1.016 / 0.725 / 0.652 / 0.467 ADU, the median master 0.400 (1.25 x 1.426 / sqrt(20)), median-of-10 0.786; dark map centre -0.551, bottom -0.174; warming 0.826 → 1.068, +2.2 °C; banding 0.387 rows, 0.169 columns; leak +0.09 top-left on dark minus bias; read noise 1.563 / 1.257 / 1.257 / 1.447; dark current median 0.140, centre 0.126, top-left 0.219; 57.8 % of bias pixels below black; verdicts OK / ATENTIE (drift) / OK / OK / OK / OK | 2026-10-11 |
| V-004 | the library | darks | 30 frames, step 2, norm 4.007; 5608 / 3940 / 528 / 176 / 13 above the levels, 3759 always, 71 flicker, median 33 ADU-eq, temporal sigma 23.64 and 5.23, 291 RTS; one saturated pixel; sigma 5.70, residual 4.94, master 2.08, rows 1.8 x; curve 4.83 / 3.46 / 2.51 / 2.27 / 1.67, the median master 1.105, median-of-15 2.46; warming 0.735 → 1.086 (the console's, at the 80.1 ADU threshold; the prototype's 100 ADU set gave 0.736 → 1.085), +3.4 °C; leak +0.22 top-left on the dark master (no biases); verdicts OK / ATENTIE (drift) / OK / OK / ATENTIE (one saturated) / OK | 2026-10-11 |

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
- 2026-10-16: the master noise of V-003 and V-004 re-recorded after the step's review changed
  the estimator from the spatial sigma over sqrt(N) (a mean master) to 1.25 x the temporal
  sigma over sqrt(N) (the median master actually built): 0.339 → 0.400 on Smida, 1.04 → 1.105 on
  the library; the library's warming figures re-recorded as the console measures them.
