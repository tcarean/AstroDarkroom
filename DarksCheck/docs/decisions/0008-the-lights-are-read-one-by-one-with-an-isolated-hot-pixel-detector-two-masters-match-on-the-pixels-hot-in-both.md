# 0008. The lights are read one by one with an isolated hot-pixel detector, and two masters match on the pixels hot in both

Date: 2026-10-16
Status: accepted 2026-10-16

## Context

Check 9 asks whether the darks fit the lights of the night: do the lights show the dark's hot
pixels, at what amplitude, and did the sensor sit at the same temperature. A light is sky and
stars over the same dark current, so a hot pixel has to be told from a star. The prototype
(`prototype/analyze_darks.py`, the lights loop) did it with a local detector: a pixel more than
6 sigma above the median of its eight same-colour neighbours, under saturation, none of whose
eight immediate neighbours is above 3 sigma (a star lights its neighbours too), and kept the
pixels flagged in at least 80 % of the lights. On the Smida lights it found 86 such pixels, 85
of them above 20 ADU in the dark master, and the lights' amplitude at the master's hot pixels at
0.72 of the master in the first light (the camera still cold), 1.0 ± 0.05 from the fifteenth
minute, 0.94 at the last.

ADR 0004 set the matching of two sets of darks as the median amplitude ratio over the pixels
"the partner master has above 20 ADU equivalent". Measured on 2026-10-16 with the console's
masters (Smida 2026-08-13 at ISO 400 against the library of 2026-10-10 at ISO 1600, both
normalised to ISO 400 and 120 s), the ratio depends on which pixels are compared:

| Pixels compared | n | Smida / library | library / Smida | degrees |
| --- | --- | --- | --- | --- |
| library above 20 ADU-eq (ADR 0004's wording) | 3933 | 0.323 (q25 0.293, q75 0.363) | 3.09 | -9.8 |
| library above 50 | 521 | 0.403 | 2.48 | -7.9 |
| Smida above 20 | 436 | 0.422 (0.381 .. 0.443) | 2.37 | -7.5 |
| Smida above 50 (the prototype's set) | 104 | 0.448 | 2.23 | -7.0 |
| both above 20 | 434 | 0.422 (0.381 .. 0.443) | 2.37 | -7.5 |
| both above 50 | 104 | 0.448 | 2.23 | -7.0 |
| Smida 10 .. 20 | 1840 | 0.336 | 2.98 | -9.4 |
| library 20 .. 40 | 2991 | 0.318 | 3.14 | -9.9 |
| library 80 .. 160 | 177 | 0.428 | 2.34 | -7.3 |

The weak pixels of the warm master (20 to 40 ADU-eq in the library, which is 7 to 13 in Smida)
give a ratio of 3.1; the pixels hot in both masters give 2.4, the same as the prototype's 2.2 on
the strongest ones. Two readings: the weak hot pixels respond more to temperature than the
strong ones, or at 7 ADU-eq in Smida the master's own noise (0.63 ADU) and quantisation (2 ADU)
pull the denominator. Either way a pixel hot in one master only puts an amplitude over a
near-noise value.

## Decision

- **The lights are decoded one at a time and never kept**: each is measured and dropped
  (`LightsAnalyzer`), since the lights are the largest group of a session and nothing needs
  two of them at once. The frame cap still applies to them, not for memory but for time: a
  light costs about a second to decode and measure, and above the cap every k-th light is
  enough for a persistence count and a night's ratio. The detector is the prototype's, with
  its constants in `Thresholds.cs`
  (`LightHotSigmas` 6, `LightIsolationSigmas` 3, `LightSaturationFraction` 0.977,
  `PersistentFraction` 0.8); the sigma of a plane's local excess is the clipped sigma of every
  fourth row and column. A pixel flagged in 80 % of the lights is persistent; the report gives
  how many persistent pixels the master has at each of its hot levels and how many of the
  master's hot pixels the lights confirm, both ways, and the persistent pixels under the hot
  threshold in the master (stars that stayed put, or hot pixels the dark missed).
- **The lights' amplitude is read at the dark master's hot pixels** (above `Th.HotAdu`, 20
  ADU-eq, under 0.73 of white), as the median over those pixels of the light's local excess
  over the master's excess, each normalised by its own ISO and exposure; the night's ratio is
  the median over the lights, with the first and the last light named, since the first lights
  of a night run cold.
- **Two masters match on the pixels both have above 20 ADU-eq**, not on the partner's alone:
  this changes ADR 0004's wording, which gets a dated note. The ratio on those pixels (0.42 for
  Smida over the library, 434 pixels) reproduces the prototype's 2.2 on the strongest pixels
  within 7 % and does not depend on which master is the reference. The report also gives the
  overlap of the two hot-pixel sets: how many of the session's pixels above 50 ADU-eq the
  partner has above 50, and how many of the partner's above 50 the session has above 20.
- **The lights against a partner** are measured through the session's darks: the lights-over-
  dark ratio times the darks-over-partner ratio, and the verdict is read on that product; a
  session without lights gets the darks' verdict against the partner. The equivalent index of
  the lights is the darks' index times the lights-over-dark ratio.
- **The label takes the parent's name** when the darks sit in a `darks` subfolder (a session,
  or a library kept as `<name>\darks\`), the folder's own name otherwise, plus `-idx<NN>` with
  the index rounded half up (66.5 reads `idx67`).

## Alternatives rejected

- **Matching on the partner's pixels above 20 ADU-eq** (ADR 0004's wording): on a warmer
  partner it compares the session's near-noise values with the partner's weak hot pixels and
  reads 3.1 instead of 2.4 on the measured pair; on a colder partner it compares the partner's
  weak pixels with strong ones of the session. Asymmetric and biased by the choice of reference.
- **Matching on the session's pixels above 50** (the prototype): 104 pixels on Smida, the
  same ratio as "both above 20" within 6 %, a quarter of the pixels.
- **Keeping the lights in memory** like the darks: 32 lights are 1.5 GB as 16-bit, and no
  figure of check 9 needs two lights at once.
- **A detector on the full-resolution neighbours**: in a Bayer mosaic the eight immediate
  neighbours are other colours, so a pixel is compared with the median of its same-colour
  neighbours two pixels away, and the isolation test reads the immediate neighbours' excess
  over their own colour's local median, as the prototype did.

## Consequences

Check 9 and the matching reproduce the prototype's figures on Smida (V-005: 86 persistent, 85
in the master above 20, 1.00 over the night, 0.72 at the first light; index 25; the library
idx67). One in a hundred hot pixels per light is lost to the isolation test when one of its
eight neighbours crosses 3 sigma by noise, which the persistence threshold absorbs. The
amplitude dependence of the ratio is a finding for the owner: a library much warmer than a
session over-corrects its weak hot pixels more than its strong ones, which `-opt` cannot fix
with one factor.
