# 0022. --expo decides on the central 20 % window of the brightest channel, on the file's own levels

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-expo.md; the decision itself dates
from the original design. A flat has a wide distribution by definition - that is what
vignetting is - so a global median is dragged down by the corners; following it, one
overexposes until the centre clips, destroying vignetting information exactly where the signal
is highest. The mode also needs the sensor's white and black levels, which a FITS does not
state, and it is routinely handed the wrong kind of file (a NEF opened in Siril and saved,
which debayers and normalises).

## Decision

The frame is split into the four Bayer positions; if the position medians spread by more than
1.15 the file is CFA and the brightest channel is used (it clips first), else the whole frame.
The decision metric is the median of the central 20 % x 20 % window of that channel; the global
median is printed as informative. On a FITS `white` is auto-detected from the frame max
(<= 1.5 -> 1.0; <= 4300 -> 4095; <= 17203 -> 16383; else 65535). On a camera raw nothing is
guessed: white is libraw's `color.maximum`, black the median over the four of
`color.black + color.cblack[i]` (4095 / 150 on a D3300 at 12 bits), reported as
`sursa: libraw (exact)`. `--sat` beats the file and takes over the source line; `--black` is
overridden separately, so a bare `--sat` leaves the file's black. A sanity gate refuses, without
a verdict, a file with `cenMed < 1.0` (already calibrated or normalised) or CFA spread < 1.02
(debayered). Bands on the central window (`Th.Expo*`): clipping > 0.05 % or > 80 % PREA SUS;
< 30 % PREA JOS; < 50 % ACCEPTABIL, dar cam jos; 50-65 % OK; 65-80 % ACCEPTABIL, dar cam sus.
With `--sky`: 15-50 % OK, > 60 % (or clipping) too high, < 15 % too low. The suggested
correction is `log2(57 / cenMed)` stops, 57 being the midpoint of the 50-65 band
(`Th.ExpoTarget`; `Th.ExpoSkyTarget` = 30).

## Alternatives rejected

- **The global median as the decision metric.** It leads to overexposing until the centre clips;
  it is still printed, labelled informative.
- **Reading only one of libraw's two black fields.** The split varies by camera - Nikon NEFs
  put the common part in `black` and leave `cblack` at zero - so one field alone would
  silently give zero on some cameras.
- **The same bands for sky flats.** The blue-hour sky darkens continuously and is full of
  faint stars, so master SNR comes from the number of frames; without `--sky` a sky flat at
  22 % would be wrongly reported as underexposed.
- **Keeping the bands as locals in the analyzer.** `Th.ExpoLo/Hi` used to be dead and wrong
  (50/60 while the code used 50/65); every threshold now lives in `Th`.

## Consequences

- The exact-level precedence is pinned by `AScaleReadFromTheFileBeatsTheGuess` and the two
  override tests beside it.
- The bands exist once as a value, `ExposureBands`, used by `Classify` and by the window's
  histogram, so the shaded target band is the band that voted.
- Parity with `flatcheck.py` holds except for wording: the scale-source line reads
  `libraw (exact)` against `rawpy (exact)`, and `--sat` is echoed as `--sat=4095` against
  `--sat=4095.0`.
- The true white level of a camera is found by shooting one deliberately overexposed frame;
  its max is saturation - the README says so.
- Source note: docs/design/modes-expo.md.
