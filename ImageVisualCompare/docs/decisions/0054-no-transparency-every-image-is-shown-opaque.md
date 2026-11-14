# 0054. No transparency: every image is shown opaque

Date: 2026-11-14
Status: accepted 2026-11-14
Supersedes: 0013 in part (that a file's transparency is shown over the backdrop)

## Context

The review of 2026-11-13 found that a pixel with transparency is drawn as one value and read as
another (`docs/review/2026-11-13-code-review.md`, CR-005). `WicImageLoader.Load` converted a
frame to Bgra32 and kept the file's alpha; the surface drew the image blended over the black
backdrop (ADR 0013); and the read-out printed the red, green and blue of the bitmap, which are
the unblended ones, and never looked at the alpha. A float TIFF of the same picture went the
other way: `FloatSamples` does not read the fourth sample, and the pixel is opaque (R-091).
Neither R-051 nor ADR 0013 said what the read-out shows on a transparent pixel, nor that the two
paths may differ, so the entry was the owner's call.

Measured on 2026-11-14 on generated files, with the loader as it was:

- An 8-bit RGBA PNG pixel holding 200, 200, 200 with alpha 128 loaded as those four bytes, was
  drawn at 100, 100, 100 over the backdrop (the software rasteriser), and was read as
  `200 200 200`. A pixel holding 50, 100, 150 with alpha 64 was drawn at 13, 25, 38. The same
  with a 16-bit RGBA PNG and with an indexed PNG whose palette entries carry an alpha.
- Converted to `Bgr32` and from there to `Bgra32`, the same frames give the same colour bytes
  with alpha 255.
- On the three real files of `docs/validation.md`, which have no alpha, that double conversion
  gave rows byte for byte equal to the direct one, in the same time and memory within the noise
  of two runs each: `de_printat.png` 5.6 and 6.3 s against 6.9 and 5.6 s, +88 and +89 MiB
  against +87 and +88; the 16-bit TIFF 3.7 and 5.5 s against 3.9 and 5.0 s; the 8-bit TIFF 1.1 s
  against 1.0 s.

The owner chose on 2026-11-14, of three options (no transparency; the alpha added to the
read-out, with the float path then blended too; leave it): no transparency.

## Decision

Every image is opaque once loaded. `WicImageLoader.Load` converts the frame through `Bgr32`,
which has no alpha, and on to `Bgra32`, whose alpha is then 255 (`Opaque`); both conversions are
lazy and are realised once, as the single one was. A pixel is drawn, and read, with the colours
WIC decodes for it, whatever its alpha: what is on screen is what the status bar says. This is
the rule a FITS and a float TIFF already follow (R-015, R-091), so there is one rule for every
file. There is no branch on the format: a file without alpha goes through the same two
conversions and comes out as before.

The format said in the status bar is still the file's own, `8-bit RGBA` for instance (R-011,
ADR 0022).

## Alternatives rejected

- **The alpha in the read-out** (`A 200 200 200 α128`), the drawing left blended over black. The
  screen would still show 100 where the file holds 200, in a tool built to show the stored
  levels, and for one rule the float path would have to keep its fourth sample and be blended
  too, against R-091.
- **The value over black in the read-out.** The number would match the screen and no longer the
  file; two exports of one picture, one with a soft-edged alpha, would read differently where
  their stored pixels are equal.
- **Leave it.** The two halves of a pair can then look different while the two read-outs are
  equal, which is the lie the entry names.
- **Alpha forced to 255 in a pass over the decoded pixels.** A second copy of about 88 MiB for a
  23-megapixel image while it is made, where the conversion costs nothing.
- **The double conversion only for a format that has alpha.** A branch on the format (against
  "Formats are reported, not branched on"), to save a cost that was measured as none.

## Consequences

- The colours left under a fully transparent pixel become visible; in an export of a finished
  astro frame there are none, and where there are, they are what the file holds.
- What "the colours WIC decodes" are is WIC's: for a TIFF written with associated alpha at 8 or
  16 bits its decoder hands over straight colours it has already divided by alpha (measured: a
  pixel written as 200 at alpha 128 through `Pbgra32` comes back 199, and a fully transparent
  one comes back 0). Only the float path is read as stored there (R-091).
- The shared backdrop of ADR 0013 stays: it is what shows where an image of a padded pair does
  not reach (ADR 0014), the same on both sides of the line. What ADR 0013 says of transparency,
  that it is shown over black on both sides, no longer happens, since no image has any.
- Guards: `WicImageLoaderTests.R010_ATransparentPixel_IsLoadedOpaque_WithItsStoredColours` and
  `R010_AFileWithoutAlpha_LoadsToTheBytesWicConvertsItTo`.

Source: the owner's answer in chat on 2026-11-14; `docs/review/2026-11-13-code-review.md`,
CR-005; `docs/plans/2026-11-14-fix-cr-003-to-cr-007.md`.
