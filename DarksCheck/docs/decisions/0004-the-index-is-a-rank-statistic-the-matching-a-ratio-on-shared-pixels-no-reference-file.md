# 0004. The index is a rank statistic of the master, the matching a ratio on the pixels two sets share; no reference file

Date: 2026-10-11
Status: accepted 2026-10-11

## Context

The owner wants a number on every library of darks and on every session that says how warm
the sensor ran, so a library master can be matched to a session's lights, and a label for
the library folder. The hot pixels carry that number: their amplitude is dark current, which
doubles about every 6 °C, times the exposure, times the ISO gain. A first proposal, a set of
reference pixels saved per camera, was declined by the owner on 2026-10-11: hot pixels change
over a year, and the same sensor a few months apart should compare directly. Measured the
same day on the Smida darks (2026-08-13, ISO 400) and the library (2026-10-10, ISO 1600): all
107 Smida pixels above 50 ADU are above 200 ADU in the library, 81 % of the library's 512
pixels above 200 ADU are above 20 ADU in Smida, and the amplitude ratio on the shared pixels
is 8.9 (quartiles 8.6 to 10.6), of which 4 is the ISO. A rank statistic of each master gives
25 for Smida and 66.5 for the library (266 ADU at ISO 1600), a ratio of 2.66 against 2.2 to
2.4 pairwise: the rank band of a warmer master takes in weaker pixels.

## Decision

No file is saved. The **index** of a master is the median amplitude above black of the pixels
ranked 5 to 20 per megapixel (the 120th to the 483rd strongest of a 24 MP sensor), divided by
the ISO factor (ISO / 400, linear, a setting with 1x per stop) and by the exposure factor
(exposure / 120 s); saturated pixels are excluded. The **matching** of two sets is the median
of the per-pixel amplitude ratio over the pixels the partner master has above 20 ADU
equivalent, both unsaturated, with the quartiles, after the same normalisation; a set of
lights is measured at those positions as the excess over the median of the eight same-colour
neighbours, per light and over the lights. The equivalent temperature offset is
`D * log2(ratio)` with `D = 6 °C`, a setting. The label is `idx<NN>`, the index rounded, and
the report always names which of the two numbers it shows. The verdict for lights against a
master, with `r` the lights-over-master ratio: plain subtraction for 0.9 to 1.1, subtraction
with `-opt` for 0.7 to 1.4, cosmetic correction only for `r` under 1 down to 0.4 (the master
is warmer and sees every hot pixel), unmatched otherwise; the four bounds live in
`Thresholds.cs`.

## Alternatives rejected

- **A reference pixel set saved per camera.** Declined by the owner: it goes stale as the
  sensor ages, and the pairwise comparison on shared pixels needs none.
- **The median amplitude of the master's own pixels above a fixed threshold.** A selection
  effect: a warmer master takes in more weak pixels, and the median falls while the sensor
  warms. A fixed rank has no threshold.
- **Electrons, through a gain table per ISO.** The gain is not known for every camera; the
  normalisation to ISO 400 and 120 s needs no table and is enough to compare two folders.
- **One index for both sorting and matching.** The two differ by about 15 % on the measured
  pair; naming them keeps a reader from comparing a label to a ratio.

## Note of 2026-10-16

ADR 0008 changes the pixels two masters are compared on, from the pixels the partner master
has above 20 ADU equivalent to the pixels both masters have above it: measured on the Smida
darks against the library, the partner's weak pixels alone gave a ratio of 3.1 (-9.8 degrees)
where the pixels hot in both give 2.4 (-7.5 degrees), the prototype's 2.2 on the strongest
ones. The rank index, the normalisation and the verdict bounds stand; the lights are measured
at the session's own master and reach a partner through the darks' ratio (ADR 0008);
the band on the D3300's 24.2 Mpx is the 122nd to the 483rd strongest pixel (the 0-based ranks
121 to 482), and the console reads 25.2 on Smida and 67.4 on the library with the median
master (R-060).

## Consequences

Plan step 3 implements the index, the matching, `--against` and the label (requirement block
R-060); the rank band, the normalisation base, `D` and the verdict bounds are constants of
`Thresholds.cs`; the ISO factor and `D` become settings of the window at step 4. The index of
a library drifts slowly as new hot pixels appear, which is the sensor's ageing, not an error.
A library at another ISO or exposure than the lights still matches through the normalisation,
on the assumption that the gain is linear in ISO; a measurement with two series at the same
temperature and two ISO settings would confirm or correct the factor.
