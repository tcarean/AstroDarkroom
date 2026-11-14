# 0053. A raw whose colour mosaic is not Bayer is refused

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

`RawReader.Decode` refused only a raw for which libraw gives no 16-bit mosaic (a Foveon sensor).
For a mosaic that is not a 2 x 2 Bayer one, libraw gives the mosaic and `CfaPattern.FromLibRaw`
gives no colour order, which the analysis reads as "a monochrome or debayered frame"
(`Raster.InterpolateGreen`): the stars, their width and the sky's noise were then measured on
pixels that alternate red, green and blue, and the four channels were named `CFA[0,0]` and so
on. `.raf`, Fujifilm's extension, is in the list of raws. The review of 2026-11-13 found it
(CR-008, `confirmed`). Shown on 2026-11-14 on a DNG written with a 6 x 6 colour mosaic, for
which libraw gives `filters` 9, its value for X-Trans: the frame was measured, with no message.
No X-Trans raw from a camera was at hand.

## Decision

A raw that has a colour mosaic (`filters` not 0) for which `CfaPattern.FromLibRaw` gives no
order is refused with `InvalidOperationException`, as a raw without a mosaic is: `<file>: the
sensor's colour mosaic is not a Bayer one (Fujifilm X-Trans); LightsCheck measures only Bayer
and monochrome raws.`, the brackets only for `filters` 9. A raw without a colour mosaic
(`filters` 0, a monochrome sensor) is read as before.

## Alternatives rejected

- **Reading an X-Trans mosaic for what it is.** The green of a 6 x 6 mosaic needs its own
  interpolation, and the levels, the noise and the pattern are all written for four positions;
  a feature of its own, with a camera's raws to measure on.
- **Refusing `filters` 9 alone.** A Leaf back (`filters` 1) and a mosaic of four colours (cyan,
  magenta, yellow, green) reach the analysis by the same door and would be measured the same
  wrong way.
- **Refusing by the extension.** A `.raf` of a Fujifilm camera with a Bayer sensor reads fine,
  and a DNG converted from an X-Trans raw would pass.

## Consequences

A session of X-Trans raws lists every frame as unreadable, with the sentence, and exits 1; the
inventory, which reads the metadata alone, still lists the frames with their times and
exposures. libraw's line for the file is logged before the refusal.
Guarded by `R007_ARawWhoseMosaicIsNotBayerIsRefusedNamingTheSensor`, on a DNG the test writes
(`DngTestData`), and `R007_TheRefusalNamesXTransAndNoOtherSensor`.
