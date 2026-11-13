# 0106. An Adobe profile's centre counts in widths and heights of its image

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

An Adobe profile may say that the lens's axis does not fall on the centre of the frame:
`stCamera:ImageXCenter` and `stCamera:ImageYCenter` in its vignette model. `VignettingModel`
read both as fractions of the calibration image's long side (Dmax), which is how Adobe's
document words them ("Adobe Camera Model", the descriptor tables, text extracted again on
2026-11-13): "stCamera:ImageXCenter: u0 / Dmax. If absent, default to 0.5" and
"stCamera:ImageYCenter: v0 / Dmax. If absent, default to 0.5", each followed by "Need to be
multiplied by the Dmax of the target image when applying the model for lens correction."

The review of 2026-11-13 (CR-009) noticed that the files do not look like that, and left the
choice to the owner once Adobe's sentence had been read. Measured that day on the 3531 files
under `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0`, both spellings of a property counted
(attribute and element), one value per file (the median of its entries):

| Where the centre stands | Image shape | Files | Median `ImageYCenter` | 5 % to 95 % | A centred axis as v0 / Dmax |
|---|---|---:|---:|---|---:|
| in the vignette model | 3:2 | 110 | 0.5008 | 0.485 to 0.528 | 0.3333 |
| in the vignette model | 4:3 | 16 | 0.5012 | 0.467 to 0.546 | 0.3750 |
| anywhere in the file | 3:2 | 640 | 0.5004 | 0.490 to 0.515 | 0.3333 |
| anywhere in the file | 4:3 | 840 | 0.4994 | 0.481 to 0.517 | 0.3750 |
| anywhere in the file | 16:9 | 6 | 0.5003 | 0.500 to 0.505 | 0.2813 |

So the value is about one half whatever the shape, which a fraction of the long side cannot
give for a lens that is roughly centred. Two readings fit a half: a fraction of the image's
height, or a fraction of a Dmax-by-Dmax square the image sits centred in. The data cannot tell
them apart (the spread of the two centres across files is too uneven to compare). Another
program can: RawTherapee's `rtengine/lcp.cc` (the `dev` branch, read 2026-11-13) multiplies the
two centres by the image's full width and full height and the two focal lengths by Dmax, under
a comment that the Adobe technical paper has a bug there, the Dmax being handled differently
for the focal length and for the image centre. And the document's own default, 0.5, is the
centre only when the value is a fraction of the height.

`FocalLengthY / FocalLengthX` is exactly 1 in all 697 vignette models that give both, so the
two focal lengths are in one unit, as the document says and the code has them.

Of the 241929 vignette models in those files, 697 carry both centres, in 126 files. The
owner's lenses: the two Tokina 11-20 files and the Samyang and Nikkor 135 files carry none; the
Nikkor 35 f/1.8G DX file (calibrated on a D90, 4310 x 2868) writes one in each of its 21 models,
away from 0.5 in 11 of them, up to 0.5156, but its seven models at the farthest distance, 1.51
m, the ones the nearest rule takes, are at exactly 0.5.

## Decision

The owner's choice of 2026-11-13, between this and leaving the model as the document words it:

1. `ImageYCenter` is a fraction of the calibration image's height and `ImageXCenter` of its
   width. The model keeps each side as a fraction of the long one (`CenterXUnit`,
   `CenterYUnit`: 1 and 2/3 on a landscape 3:2 body) and multiplies the decentring by it.
2. The image's shape is the entry's own `stCamera:ImageWidth` and `ImageLength`. An entry that
   gives none keeps both units at 1, the document's reading and the formula before.
3. The focal lengths stay in Dmax.

## Alternatives rejected

- **Leaving the model as the document says and recording the contradiction.** The shift would
  stay 1.5 times too large on every 3:2 profile that has one, against the data and against the
  one other implementation read.
- **The Dmax square.** It gives the same number as the formula before (a fraction of the long
  side, measured from the square's edge), so it is the reading the files already fit; but
  nothing was found that describes such a square, and RawTherapee's code says height.
- **Assuming 3:2 when the entry gives no size.** A guess; the entry that says nothing keeps
  the formula it had.

## Consequences

- On a 3:2 calibration body the vertical shift is two thirds of what it was: 0.57 mm where it
  was 0.85 for an `ImageYCenter` of 0.536912 at `SensorFormatFactor` 1.565304. It moves the
  top-bottom balance of a synthetic flat (the offset and corner-spread rows of `--against`);
  the four-corner depth, an average, hardly moves.
- A profile calibrated on a portrait image has its horizontal shift scaled instead; none was
  met among the files with a centre.
- The validation figures use centred profiles (above), so none is expected to move; the task
  that made this change ran `/validate` to check it.
- The evidence is two facts and one reading of another program, not a statement by Adobe. If
  Adobe's wording is ever shown to be meant literally, the square reading is the first
  alternative above and the units go back to 1.
- R-072 holds the behaviour; `R072_...` in `LensProfileTests` pins the shift, a centred
  profile, a portrait image and what the reader hands over.
