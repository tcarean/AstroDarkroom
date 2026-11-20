# What the -opt investigation of 2026-11-19 and 20 found, beyond ADR 0070

ADR 0070 holds the measurement's two tables and the decision. This file holds the rest of what
was learnt, so that it does not live only in notes outside the repository. Nothing here changes
a figure, a verdict or a text of the program.

How to read the sources. A path in backticks is a file of this folder, copied unchanged from
the measurement's working folder (the README lists them). "From the notes" marks a figure that
no saved file holds: it was measured on 2026-11-19 or 20, written down then, and not measured
again for this record. "Research" is `research-older-vs-newer-excerpt.md`, whose points were
each re-checked by a second reader; this task re-read only the two FAQ pages and the manual page.
The pairs are named as in the files: V1006, V1106 and V1108 are the Veil lights of 2026-10-06,
2026-11-06 and 2026-11-08, D13 .. D67 the library masters idx13 .. idx67, all at ISO 1600 and
120.2 s. D34 is a seventh library master, idx34 (9.2 °C by its folder's name), in which the
measurement picked the pixels it followed.

## 1. What Siril itself advises

- **The FAQ**, entry "My camera is not cooled, what does that change?" (https://siril.org/faq/,
  re-read 2026-11-20; in the site's source since at least 2021-01-03, research). The stock
  scripts take the lights and the darks as made at the same temperature and exposure; "with
  DSLR and other non-cooled astronomy camera, it is not the case, especially for long
  acquisition sessions", and the way to process such frames is dark optimisation, the `-opt`
  option of `calibrate`. The master bias, or a value, comes out of the darks and out of the
  lights. The French page (https://siril.org/fr/faq/, re-read the same day) says "les longues
  poses (> 30sec)" where the English one says long sessions. Neither gives a rule by the
  camera's age, by CCD or CMOS, or by amp glow.
- **The manual**, 1.4.4 (https://siril.readthedocs.io/en/stable/preprocessing/calibration.html,
  read 2026-11-20): dark optimisation is for darks not taken in the best conditions, with a
  master dark the bias was taken out of. Its one worked example is a 600 s master dark on 60 s
  lights, where Siril found 0.110; the camera of that example is a cooled CCD, an FLI ProLine
  4240 (research).
- **The stock scripts never use `-opt`** (Cyril Richard, Siril developer, 2020-04-14,
  https://gitlab.com/free-astro/siril/-/issues/465; research).
- **Vincent Hourdin** (vinvin on pixls.us, Siril co-developer; all four from the research):
  - 2020-07-13: told a Nikon D5000 user whose darks were 3 to 4 K warmer than the lights to use
    it (https://discuss.pixls.us/t/how-far-to-process-an-image-m13/19152/4);
  - 2020-07-23: it is no use when the dark without the bias is almost null
    (https://discuss.pixls.us/t/19270/5);
  - 2024-04-23: it often fails on recent CMOS cameras
    (https://discuss.pixls.us/t/strange-lines-and-circles-in-images/43279/4);
  - 2024-12-02: most modern cameras will not support it
    (https://discuss.pixls.us/t/stacking-error-mentioning-fwhm-when-not-relevant/46849/2).
  The two remarks of 2024 give no reason, and in the second thread he says he does not know
  modern cameras well.
- **Adrian Knagg-Baugh** (Siril developer), 2025-11-26, with a hedge: the automatic estimate is
  meant for uncooled DSLRs whose temperature drifts (https://gitlab.com/free-astro/siril/-/issues/1861;
  research).

So by its type, an uncooled DSLR taking 120 s frames, the D3300 is the camera this advice is
written for. The advice DarksCheck gave until 2026-11-20 was Siril's own.

## 2. Older or newer cameras: neither

By the sources, what decides is what the dark holds, not the camera's year (research).

- Scaling a dark by one factor works on a dark that is mostly heat signal. DeepSkyStacker's
  method was built for uncooled CCD cameras (Goesele, Heidrich and Seidel, 2001,
  https://www.cs.ubc.ca/labs/imager/tr/2001/goesele2001a/goesele.2001a.pdf).
- It fails in three cases:
  - amp glow (ZWO, 2018-04-03, https://www.zwoastro.com/2018/04/03/what-is-amp-glow/);
  - an almost empty dark, as on the newest cooled CMOS cameras;
  - a camera that reworks its dark. Canon does it from about 2008 (Roger Clark,
    https://clarkvision.com/articles/dark-current-suppression-technology/). Nikon filters long
    exposures and cuts values under black: the darks of a D5300 are cut at 588 against a black
    of 600 (Iliah Borg, LibRaw, 2018-04-17, https://libraw.org/node/2362).
- These traits do not go by the camera's year: measured the same way, newer cameras are not
  reliably lower (Mark Shelley, 2018-01-13,
  https://stargazerslounge.com/blogs/entry/2004-d7500-has-high-thermal-pattern-noise/).

## 3. The D3300's dark of 120 s at ISO 1600, as measured

The camera's black level is 150 ADU; "above the level" means above 150.

| What | Figure | Source |
|---|---|---|
| A master's pixels that stand less than 4 ADU above the median of their eight neighbours of the same colour | idx13 99.45 %, idx18 99.39 %, idx33 97.44 %, idx34 (9.2 °C) 97.18 % | `skeptic/results/classes_V1108_D13.json`, `classes_V1006_D18.json`, `classes_V1106_D33.json`; `pairs.json`, `class_pixels_in_frame` (idx34's); `emptiness/measures.json`, `emptiness` |
| The pixels that stand 4 ADU or more above their neighbours | 133,003 (idx13), 147,751 (idx18), 615,811 (idx33), 677,596 (idx34): about five times more from idx13 to idx34 | the same files |
| Noise of one light, for comparison | 22.2 to 25.1 ADU | `pairs.json`, `sigma` |
| Lowest raw value in any frame | 147, three under the level | from the notes |
| A dark frame's central pixels that sit exactly at 147 | 33 % (idx13) to 46 % (idx67) | from the notes |
| The master's central pixels exactly at 147, per colour | 6 to 11 % (idx13), 39 to 47 % (idx67) | `critic/bias_pattern.json`, `box_floor_share_per_site` |
| The master's median, above the level | idx13 +0.49, idx18 +0.54, idx20 +0.53, idx33 +0.21, idx47 -0.07, idx67 -2.65 ADU | `emptiness/measures.json`, `emptiness`, `master_median_ADU` |
| How closely the master follows the bias's pattern (1 = the same) | idx13 0.987, idx18 0.966, idx20 0.941, idx33 0.832, idx47 0.806, idx67 0.647 | `critic/bias_pattern.json`, `slope_on_bias` |
| The master's red pixels above its other colours | idx13 0.58, idx18 0.66, idx20 0.62, idx33 0.45, idx47 0.37, idx67 0.19 ADU | `emptiness/measures.json`, `emptiness`, `red_minus_others_whole` (of `master_site_means_whole` the third is red) |
| The same in the 1/6 s master bias | not there | from the notes |
| The right factor, over the 18 pairs | 0.23 to 2.32 | `pairs.json`, `right_median` |

The first row is not a level above 150: it counts the pixels that do not stand out from
their neighbours, and such a count cannot see a level that is even across the frame. It does
not say "how empty the dark is", and reads 97 to 99.5 % on every master measured. What the
rows do say together: few pixels stand out, and by little against the 22 to 25 ADU of one
light's noise; the camera cuts the frame at 147; and the master's level sinks as the camera
warms, which a heat signal would not do. But the hot pixels do follow the temperature: the
right factor changes four to five times across the library on one night, ten times over the 18
pairs. There is something to scale, and it is small.

## 4. Why Siril chose 0.002

ADR 0070 says the colours of the sky decide Siril's factor. More exactly:

- Siril measures the central 512 x 512 pixels of the raw frame, all colours together, and looks
  for the factor that leaves the smallest spread of light minus factor times dark. The factor
  that does that is the covariance of light and dark divided by the dark's variance.
- That covariance has two parts: how light and dark vary together from pixel to pixel within a
  colour, which is what a dark factor should follow, and how the colours' averages line up. In
  the master the red pixels sit highest (0.70 ADU against 0.04 to 0.08 for the others, idx18).
  Under the L-eNhance filter the red sky is the lowest colour: 188 against 335 to 356 (V1006),
  207 against 410 to 435 (V1106). High in the dark where the light is low: the colours' part is
  negative, and about eight times the size of the other (first light of V1006 with D18: -18.6
  against +2.3; `skeptic/results/b_V1006_D18.json`, `b_V1106_D33.json`).
- So the whole comes out negative, and Siril's search, which looks between 0 and 2, ends at its
  lower end, 0.002. On every light where Siril printed 0.002 the covariance over the variance
  is negative (`skeptic/results/all_pairs_opt_k0.json`, `siril_k0` and `cov_var`).
- Light by light, over the 510 lights of the 18 pairs: Siril's factor was 0.002 on 426, at
  most 0.02 on 444, and under that light's own hot-pixel ratio on all 510
  (`all_pairs_opt_k0.json` against `results/ratios_extract.json`, `ratio`). It moved off 0.002
  only with idx67, the 15.8 °C library, and reached 0.424 at most.
- Within the colours alone, the same first light would give 0.92 (`k_within`; `k_sites_removed`
  in `results/ratios_extract.json`), against a right factor of 1.69.
- The measure was written again apart from Siril (`skeptic/tools/crit.py`, `repro.py`) and run
  on the files Siril read. It gave the factor Siril logged in 390 of 390 comparisons: 261 in
  `skeptic/results/flows_V1006_D18.json` (29 lights, nine ways of running) and 112 in
  `g5_V1106_D33_split.json` (28 lights, four colours), both listing no mismatch, 3 in
  `b_V1106_D33.json` and 14 in `b_V1106_D67_interior.json`. That is 57 lights of two nights
  against three masters, not 390 lights.

So 0.002 does not say that the dark holds nothing to scale. It says that Siril's estimate
cannot be used on colour frames where one colour of the sky is far from the others.

## 5. Other ways of running it

All on V1006 with D18, 29 lights, right factor 1.50 to 2.07 (`skeptic/results/flows_V1006_D18.csv`):

| Flow | Siril's factor |
|---|---|
| The level 150 out of the master dark and off the lights (the measurement's own flow) | 0.002 on every light |
| The master bias out of the master dark | 0.002 on every light |
| Each dark calibrated by the bias, then stacked (the FAQ's recipe) | 0.002 on every light |
| No cosmetic correction, no equalising of the colours | 0.002 on every light |
| The stock script with `-opt` added | 0.002 on every light |
| Each colour of the raw frame calibrated apart | 0.47 to 0.78 of the right factor by colour, 0.64 averaged over the colours |

Run on each colour apart, Siril's factor moves off zero, unevenly between the colours and
between the pairs. Siril itself ran that way on two pairs only: V1006 with D18
(`skeptic/results/flows_V1006_D18.json`, the columns `F5`) and V1106 with D33
(`g5_V1106_D33_split.json`: 0.75 to 1.07 of the right factor by colour, 0.98 averaged), both
with no mismatch against the re-written measure. The other 16 pairs are the re-written
measure's (`predict_split_V1006_summary.json`, `predict_split_V1106_V1108.json`). Over all 18:
0.38 to 1.42 of the right factor by colour, 0.55 to 1.19 averaged over the colours.

The FAQ's step of calibrating each dark before the stack fails in Siril 1.4.4 for another
reason too: its `stack` leaves out every value that is exactly 0 and cuts the result to 0 .. 1.
38 % of the idx13 master made that way came out exactly 0, with a mean of 0.97 ADU instead of
0.35 (from the notes).

## 6. What the two stacks showed, exactly

From `stack-veil-2026-11-06-idx33.json` and `stack-veil-2026-10-06-idx18.json`: 28 lights, one
shared registration, the cosmetic correction on, the dark subtracted as it is, multiplied by
the right factor, or with Siril's 0.002, which leaves it out.

- **Darks warmer than the lights** (V1106 with idx33, right factor 0.534). Leaving the dark
  out gave the same picture as subtracting it as it is: both stayed 0.12 to 0.17 of the noise
  away from the stack with the right factor, and the noise moved by less than 0.3 %. Bright
  dots above 5 times the local noise: 2601 with Siril's 0.002, 2696 as it is, 2633 with the
  right factor.
- **Darks colder than the lights** (V1006 with idx18, right factor 1.826). Leaving the dark
  out gave a worse picture: 0.23 to 0.32 of the noise away, against 0.15 to 0.20 as it is,
  with 7 to 10 % more bright dots in red (1396, against 1302 as it is and 1267 with the right
  factor).
- `-opt` was not worse everywhere: on the warm stack it had a hair less noise and the fewest
  bright dots.
- Limits: the L-eNhance filter only, dithered lights, one target on three nights.

## 7. A lower threshold for the cosmetic correction

`-cc=dark 0 2` makes Siril repair the pixels that stand 2 times the master's spread above its
median, instead of 3. On each light it took out nearly all of the pixels that are bright in the
lights and not in the darks (from the notes). In the stack it changed nothing to see
(`impact/cc2/analysis_D18.json`, `analysis_D33.json`: V1006 with D18 and with D33, the master
that matches it, 28 lights, the dark subtracted as it is):

| | V1006 with D18 | V1006 with D33 |
|---|---|---|
| Pixels repaired in a light, 3 then 2 | 128,853 then 159,430 | 121,026 then 157,872 |
| Noise of the stack's sky | 0.02 to 0.03 % lower | 0.10 to 0.14 % lower |
| Bright dots above 5 times the local noise | 13,598 then 13,585 | 13,588 then 13,531 |
| Dark dots above 5 times the local noise | 322 then 314 | 313 then 294 |
| The two stacks apart, on the sky, as in section 6 | 0.05 to 0.09 of the noise | 0.08 to 0.11 of the noise |
| Stars whose peak fell by more than 1 % | 58 to 79 of 2,748 | 146 to 180 of 2,785 |

Not advised: a quarter to a third more pixels are replaced in every light for a stack that looks the same,
and some stars lose a little of their peak. `impact/summary_impact.json` holds the summary of
the two stacks of ADR 0070, for comparison.
