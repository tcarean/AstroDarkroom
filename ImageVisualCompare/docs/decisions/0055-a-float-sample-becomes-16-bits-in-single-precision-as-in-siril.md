# 0055. A float sample becomes 16 bits in single precision, as in Siril

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

A FITS value is shown at the level its own 16-bit export shows (ADR 0029, R-015): the value is
brought to 16 bits and the high byte kept. The review of 2026-11-13 found that the 16-bit step
was computed in double precision, under a comment that said Siril quantises a float the same
way (`docs/review/2026-11-13-code-review.md`, CR-007). It does not.

Read in Siril's source on 2026-11-14 (GitLab, branch `1.4`, the version the owner's files are
written by):

- `core/proto.h`: `float_to_ushort_range(float f)` is `roundf_to_WORD(f * USHRT_MAX_SINGLE)`,
  and `roundf_to_WORD(float f)` adds `0.5f`, clamps to 0 and 65535 and casts to `WORD`. Every
  step is in single precision.
- `io/image_formats_libraries.c`: the 16-bit PNG (`convert_data`) and the 16-bit TIFF (`savetif`)
  write `float_to_ushort_range` of each float. The display's "Histogram" mode uses the same
  expression (quoted in FlatCheck's `docs/design/siril-histeq-source.md`).
- `io/image_format_fits.c`: a float or a double FITS is read with `fits_read_img(TFLOAT)`, so
  cfitsio hands Siril floats whatever the file holds; when `data_max` is above 10 the floats are
  multiplied by `INV_USHRT_MAX_SINGLE`, which `core/siril.h` defines as `.000015259022f`.

From 0.5 to 1 a `float` product `f * 65535f` is rounded to the nearest 1/256, so a product just
under a half becomes the half, and the half goes up; in double precision it stays under and
goes down. Measured on 2026-11-14 with a throw-away test, over every float from 2^-17 to 1:

- the 16-bit value differs for 32,768 floats, Siril's one higher each time: 16,384 of the
  8,388,608 floats in [0.5, 1), then 8,192, 4,096, 2,048, 1,024, 512, 256 and 128 in the seven
  binades below it, none in [2^-9, 2^-8), and 128 in all in the eight binades below that;
- the level differs for 127 floats, all in [0.5, 1); for instance `0x3F560056`
  (0.8359426259994507) is 54783 and level 213 in double precision, 54784 and level 214 in
  Siril;
- on the five float FITS of the owner's folder `Processed_WithFlats_RBF_light` (5938 × 3888,
  three planes, 69,260,832 samples each, two of them the pair of `docs/validation.md`): 18 to
  2,564 samples differ in their 16-bit value, none in its level. Four of the five are linear
  stacks with about a thousand samples from 0.5 up; a stretched image has millions there.

So against Siril's own 16-bit export of a stretched float FITS, this program showed some pixels
of the FITS one level lower than the same pixels of the PNG: a difference that is not in the
data, in a tool built to show a difference of one level. R-015 said `round(value × 65535)` and
not in which precision, so the entry was the owner's call. The owner chose on 2026-11-14, of
two options (as Siril; keep double precision): as Siril.

## Decision

A float sample is brought to 16 bits as Siril brings it, in `FitsScale.SixteenBitOfFloat`: the
sample as a `float`, times its factor to 0 to 1, times 65535, plus 0.5, each step rounded to a
`float`, then clamped and the fraction dropped. The factor (`FitsScale.ToUnitSingle`) is 1 for a
file in 0 to 1 and Siril's `.000015259022f` for a file read as 0 to 65535 (R-016).

- `FitsRowDecoder` uses it for `BITPIX` -32 and -64, after BZERO and BSCALE, which are applied
  in double precision and the result narrowed to a `float`, as cfitsio does for Siril.
- `FloatSamples.ToSixteenBit` uses it for a float TIFF, which is shown as a float FITS of the
  same samples is (R-091).
- Integer samples keep `FitsScale.SixteenBit` in double precision, which is exact for them.

NaN stays black (R-016); Siril's cast of a NaN is not defined by C.

## Alternatives rejected

- **Double precision, with the comment corrected.** The nearest 16-bit value to the stored
  float, mathematically; but the other half of the pair is written by Siril, and a FITS beside
  its own export would go on showing differences neither file holds.
- **Single precision for the product, double for the half.** Neither Siril's result nor the
  exact one.

## Consequences

- A float FITS, or a float TIFF, and the 16-bit PNG or TIFF Siril exports of it load to the same
  bytes; `FitsImageLoaderTests.R015_AFloatFits_ShowsTheLevelsOfSirilsSixteenBitExportOfIt`.
- The 16-bit value the `Histogram` mode counts on (R-111) is now the one Siril's own histogram
  display counts on.
- When this program reads a float file as 0 to 65535 is still its own rule, more than one
  sample in a hundred above 2.0 (R-016), not Siril's `data_max` above 10; only the arithmetic of
  such a file follows Siril.
- An export of the same floats by another program may round in double precision, and then
  differs from the FITS by the same 127 values the other way. Siril is the tool the files of
  this project come from.
- Not measured: a real 16-bit PNG saved by Siril from a stretched float FITS, loaded as a pair.
  The rule rests on Siril's source and on the arithmetic, not on such a file.
- Guards: `FitsScaleTests.R015_AFloatSample_IsBroughtToSixteenBitsInSinglePrecision_AsSirilDoes`,
  `R015_SinglePrecision_DiffersFromDouble_OnlyByOneUp_AndByALevelFor127Floats` (the counts
  above, over [0.5, 1)), `R015_AFloatSample_KeepsEverySixteenBitValue_OnBothScales`, and the
  `R015_` and `R091_` cases of `FitsRowDecoderTests` and `FloatSamplesTests`.

Source: the owner's answer in chat on 2026-11-14; `docs/review/2026-11-13-code-review.md`,
CR-007; `docs/plans/2026-11-14-fix-cr-003-to-cr-007.md`; Siril's source,
`https://gitlab.com/free-astro/siril`, branch `1.4`.
