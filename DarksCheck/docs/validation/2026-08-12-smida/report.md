# Darks check, Smida session 2026-08-12 (Nikon D3300)

One-off, read-only check of the session's darks against its lights and biases, plus the
2026-10-10 library darks (ISO 1600). Everything is measured on the raw CFA data of the NEF
files (rawpy, no demosaic, no Siril). Scripts and logs are in `prototype/`, the numbers in
`results.json`, `results_supplementary.json` and the per-frame CSV files.

Session folder: `E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida`.
Library folder: `E:\Photografie\Astro\Flats and Darks\Darks\2026-10-10-ISO1600-120s-15.8celsius\darks`.

## Verdict

- **The 20 Smida darks are good.** Same exposure and ISO as the lights, taken 5 minutes after
  the last light with a perfect 120 s cadence, no light leak, no amp glow, no outlier frame,
  and their hot pixels are within ±5 % (about ±0.4 °C) of the lights' hot pixels for 26 of
  the 32 lights. They cover 85 of the 86 hot pixels that the lights themselves show.
- **The 30 biases are fine as biases** (read noise 1.26 ADU in green). They are 1/20 s, not
  the camera's shortest exposure, and were taken 47 days later; neither matters at this level.
- **The 30 library darks of 2026-10-10 are clean** (no leak, no outliers, perfect cadence) but
  the sensor warmed by about 3.4 °C during the hour, so the first and last frames differ by
  47 % in hot-pixel amplitude. They ran about 7 °C warmer than the Smida darks.

## The camera's raw data

The D3300 writes 12-bit lossy-compressed NEF. Near black the values come in steps of 2 ADU
(only odd values: 147, 149, 151 ...), at the Smida sky level (about 700 ADU) in steps of 3-4,
near 2000 ADU in steps of 7. A dark frame's green plane holds only 44 distinct values. The
black level is 150 and the noise distribution is not clipped (values of 147 exist, 58 % of the
pixels sit below 150). Consequences: a MAD-based noise estimate returns 0 or one step, so all
sigmas here are clipped standard deviations; any dark-current pedestal below 1 ADU is invisible
per frame and only appears in averages.

## The nine checks

### 1. Metadata and cadence

| set | frames | exposure | ISO | first | last | cadence |
|---|---|---|---|---|---|---|
| lights | 32 | 119.2 s (one 118.5 s) | 400 | 2026-08-12 23:22:25 | 00:43:42 | 127-244 s |
| darks | 20 | 119.2 s | 400 | 2026-08-13 00:48:53 | 01:26:53 | 120.0 ± 0.1 s |
| biases | 30 | 1/20 s | 400 | 2026-09-29 18:29:16 | 18:29:45 | bursts of 4 |
| library darks | 30 | 120.2 s | 1600 | 2026-10-10 23:03:24 | 00:01:52 | 121.0 ± 0.1 s |

The darks match the lights in exposure and ISO and follow them directly. No gap in either
dark series, so no Live View or pause (and no cooling) inside the series.

### 2. Light leaks

Dark master (mean of 20) minus black, binned 16×16 per CFA plane, green G1: centre −0.55 ADU,
corners −0.44 to −0.10 ADU, bottom edge −0.17 ADU. The bottom edge is 0.4 ADU above the centre
in all four planes, and the bias master shows exactly the same step (0.4-0.5 ADU), so it is a
readout offset, not light. Dark minus bias (fig2, fig2b) is flat: 0.05 to 0.27 ADU over the whole
frame, median 0.14 ADU, 0.22 ADU in the top-left corner. A leak would be at the ADU level and
would be absent from the bias. **No leak.**

Library darks (ISO 1600): the left and top edges sit 0.5-0.7 ADU above the centre, the right
edge 0.15-0.2, the bottom 0.3 ADU. The left/top excess grows with the warming of the series
(left 0.51 → 0.69 ADU, top 0.48 → 0.73 ADU, fig9) while the right and bottom stay flat: the
growing part scales with the dark current, so it is edge glow of the sensor, not a leak.
**No leak.**

### 3. Thermal drift inside the series

Read from the hot pixels: the median of (frame amplitude / master amplitude) over the 433
pixels above 20 ADU, with the rule "dark current doubles every 6 °C".

- Smida darks: 0.83 at the first frame → 1.08 at the last (fig8), about +2.3 °C in 38 minutes.
  Pixels above 20 ADU in G1: 115 → 179. Per-frame noise 1.46 → 1.55 ADU.
- Library darks: 0.74 → 1.09 (fig9), about +3.4 °C in 58 minutes, most of it in the first
  25 minutes. Pixels above 100 ADU in G1: 521 → 1077. Per-frame noise 5.0 → 5.9 ADU.

The camera heats itself when shooting back to back. A master is the median of the series, so
its amplitudes belong to the middle of the series.

### 4. Noise per frame

Sigma of (frame − master), ADU: green 1.47-1.55, red 1.60-1.88, blue 1.67-1.86 (fig4). No
outlier frame (largest/smallest ratio 1.17 in red). Read noise from bias pairs: green 1.26,
red 1.56, blue 1.45 ADU, so the dark-current shot noise is about 0.8 ADU in green.
Row means of the residual have a standard deviation of 0.11-0.16 ADU per frame against
0.027 ADU expected from white noise: a weak random horizontal banding of 0.1 ADU rms.
Columns show none (0.035 vs 0.033).

### 5. Hot pixels

Median master above black: 433 pixels > 20 ADU in 24 MP (R 79, G1 135, G2 140, B 79),
107 > 50, 24 > 100, none > 500, none saturated. Amplitude of the 433: median 33 ADU,
90th percentile 81, 99th 273 ADU. Stability (fig3): 405 of the 433 stay above 10 ADU in all
20 frames, 16 drop below in two or more frames. Their temporal sigma is 5.6 ADU against 1.45
for a quiet pixel, which is just the shot noise of their own current; 92 pixels in G1 have a
temporal sigma above 6× the quiet one (random-telegraph candidates). At ISO 1600 the library
master has 5150 pixels > 50 ADU, which is the same population seen at 4× the gain and a
warmer sensor.

### 6. Fixed pattern

No amp glow and no gradient beyond the 0.1 ADU edge rise of the dark current (fig2). The
master's structured row/column banding is at most 0.04 ADU rms, 0.5 ADU peak to peak.

### 7. Dead and saturated pixels

None in the master (no pixel below 100 ADU, none at 4095).

### 8. How many darks are needed

Noise of the mean of n frames, green, centre crop (fig4): 1.43 (1), 1.02 (2), 0.72 (4),
0.65 (5), 0.47 ADU (10), against sigma/sqrt(n) = 1.51, 1.07, 0.76, 0.68, 0.48. The frames are
independent; the mean of 20 reaches about 0.33 ADU, a median of 20 about 0.42 ADU, both below
the 2 ADU quantisation step. More darks would only refine the hot-pixel amplitudes (a 33 ADU
hot pixel is known to ±1.3 ADU from 20 frames). **20 are enough.**

### 9. Lights against the dark (fig5, fig6, fig8)

Hot pixels in the lights: a pixel more than 6 sigma above the median of its eight same-colour
neighbours, with none of its eight immediate neighbours above 3 sigma (so stars are excluded),
in at least 26 of the 32 lights. That gives 86 persistent hot pixels.

- 85 of the 86 are above 20 ADU in the dark master (the one missing has 3 ADU in the dark and
  is flagged in 30 lights; unexplained, a single pixel).
- Of the dark's 107 pixels above 50 ADU, 68 are persistent in the lights, 14 are flagged in
  17-25 lights, and 25 are never flagged. All 25 have a hot neighbour (clusters such as the
  cross at rows 268-269, columns 148-150), which the isolation test rejects as star-like. They
  are real hot pixels, and a cosmetic correction from the dark will treat them.
- Amplitude: light/dark = 0.72 at the first light, 0.89-0.96 for lights 2-5, 1.00 ± 0.05 from
  minute 15 to 70, 0.92-0.98 for the last five. The camera starts cold and warms up in the first
  15 minutes; the long pauses near the end (up to 244 s between lights) let it cool a little.
  The dark master matches the lights' temperature within ±0.4 °C for 26 lights; the first five
  are 1-3 °C colder, where the dark overestimates their hot pixels by 10-28 % (3-9 ADU on a
  median hot pixel, below the 6 ADU sky noise of a single pixel).

### Library darks against the Smida darks

All 107 Smida pixels above 50 ADU are above 200 ADU in the library master; 81 % of the
library's 512 pixels above 200 ADU are above 20 ADU in Smida. Amplitude ratio library/Smida:
8.9 (quartiles 8.6-10.6) for the strong pixels, 9.7 for the weak ones. The ISO gain accounts
for 4×; the remaining 2.2-2.4× is temperature: **the library darks ran about 7-7.5 °C warmer
than the Smida darks** (sensor, not air; the 6 °C doubling rule).

## Recommendations

1. Use the Smida darks as they are, with `-cc=dark`; plain subtraction is also fine.
2. For a dark library, warm the camera up first: 10-15 minutes of discarded exposures before
   the kept frames, or discard the first 10 frames of a 40-frame run. The 2026-10-10 series
   drifts by 3.4 °C, so its master belongs to a sensor temperature reached about 25 minutes in.
3. Label library darks by the measured hot-pixel amplitude of their master rather than by the
   room temperature; the sensor runs warmer than the room when shooting back to back.
4. To know whether the library darks match the Veil lights of 2026-10-06, run check 9 with the
   Veil lights folder against the library master (same script, `lights` path and `darks` path
   swapped). The ratio should come out near 1.0 for `-opt`-free subtraction; for `-cc=dark`
   anything within the −3/+8 °C margin is fine.

## Files

- `results.json`, `results_supplementary.json`: all numbers.
- `darks_per_frame.csv`, `darks_residual_per_frame.csv`, `darks_warming_per_frame.csv`,
  `biases_per_frame.csv`, `lights_per_frame.csv`, `library_darks_per_frame.csv`,
  `library_warming_per_frame.csv`.
- `fig1` darks frame by frame, `fig2`/`fig2b` maps and profiles, `fig3` hot-pixel stability,
  `fig4` noise, `fig5` light/dark amplitude, `fig6` overlap, `fig7`/`fig7b` library series and
  map, `fig8` warming through the night, `fig9` library warming and edges.
- `prototype/`: `nefexif.py` (EXIF without exiftool), `analyze_darks.py` (checks 1-9),
  `supplementary.py` (mean master, warming curves, library comparison), `plots.py`, logs.
