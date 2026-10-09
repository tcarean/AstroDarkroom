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
| `inventory` | `InventoryRun` on the session | the counts of lights and biases, the camera, the exposure and ISO of the first frame, the start of the first frame, the span, the duty | 0.01 s exposure, 1 s span, 0.05 points duty |
| `frame` | `FrameRun` on one light | detected and bright counts, median and p90 FWHM, median elongation, the elongated fraction, the angle and its coherence, the halo, the background and noise, the four channel medians, the centre and edge FWHM, the trail count | counts 1 %, FWHM 0.05 px, elongation 0.05, fraction 0.03, angle 3 or 8 degrees (8 where the coherence is under 0.5 and the angle means little), coherence 0.03 / 0.05, halo 0.01, ADU 1.0, sigma 0.2 |
| `series` | `SeriesRun` on the whole session (minutes) | the class counts and the list of bad frames, the registered frames, the moves, pauses and pauses without a move, the distinct spots at 5 and 10 px, the isolated frames, the longest run at 10 px, the median move, the rotation, the drift along Y, the session FWHM, the read noise of G1, the swamping ratio of R on the first frame, the RA axis and the four verdicts | counts and lists exact; px 0.1, arcmin 0.3, FWHM 0.05, ADU 0.1, ratio 0.3 |
| `words` | `SeriesRun` on the whole session (minutes) | the trail axis word of every frame with a trailing flag, one expected word for the bad and one for the marginal frames, and the `acelasi loc` cell of the frames listed | exact |

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
| V-008 | same, the whole session | series | 48 good / 17 marginal / 22 bad, the bad frames 1, 13, 15, 17, 18, 20, 22, 23, 25, 27, 38, 43, 45, 50, 52, 54, 56, 60, 63, 68, 70, 76; 87 registered; 41 moves (>= 5 px), 47 pauses, 9 pauses without a move; 38 distinct spots at 5 px, 25 at 10 px, 1 isolated, longest run at 10 px 10 frames (61-70); median move 13.29 px; rotation 8.5 arcmin; drift -0.48 px/min along Y; session FWHM 3.12 px; read noise G1 2.9 ADU; swamping of R on frame 1 9.4; RA along Y (WCS); focus held, no dew, hot pixels fine, walking noise insufficient | 2026-10-09 |
| V-009 | same, the whole session | words | the trail axis word `RA` on all 22 bad and all 17 marginal frames (angles 77 to 89 degrees in absolute value, coherence 0.94 and 0.86 or more); the `acelasi loc` cells `1/4 de la 57` to `4/4 de la 57` on 57 to 60, `1/10 de la 61` to `10/10 de la 61` on 61 to 70, `-` on 71 and 72 | 2026-10-09 |

The series figures are the console's run of 2026-10-09 (88 s on 4 threads) and equal the hand
report's: the same 22 bad and 17 marginal frames, 47 pauses and 9 presses without effect, the
same groups of frames in 5 and 10 px (`raport.md`, section 6), the rotation of 8.5 arcmin, the
read noise and the swamping of red. The drift along Y is -0.48 px/min over 45 non-move pairs
where the hand report quoted -0.8 px/min over the 40 back-to-back pairs only: the tool's pairs
also include the small-step pairs after a pause, which the 5 px threshold leaves out of the
moves; both are the same slow drift.

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

## How to re-measure

```bash
LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro dotnet test LightsCheck.sln -c Release --filter "Category=Validation"
```

Or `/validate` in Claude Code, which prints expected versus measured for every case and stops on
drift instead of editing the figures.

## History

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
