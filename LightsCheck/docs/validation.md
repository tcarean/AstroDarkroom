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
