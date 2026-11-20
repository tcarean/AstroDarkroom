# 0113. An image a JPEG cannot hold is analysed without the JPEG rows

Date: 2026-11-20
Status: accepted 2026-11-20

## Context

The JPEG stage encodes the whole image at each quality (R-083). A JPEG stores a side in 16
bits, so it holds at most 65 535 px on a side, and the stage did not ask the size first. The
code review of 2026-11-13 (CR-011) expected the encoder to throw on a wider image, as the last
stage but one, with the tones, the dust, the stars and the sizes measured and lost. Measured on
2026-11-20 with ImageSharp 3.1.11, flat rasters through `JpegAnalyzer.Analyze`:

```
65535 x 8, 3 plane(s): no exception; rows 1, q100 bytes 14947, max error 0.50, tiff bytes 6356
65536 x 8, 3 plane(s): SixLabors.ImageSharp.ImageFormatException: Image is too large to encode at 65536x8 for JPEG format.
65536 x 8, 1 plane(s): SixLabors.ImageSharp.ImageFormatException: Image is too large to encode at 65536x8 for JPEG format.
8 x 65536, 3 plane(s): SixLabors.ImageSharp.ImageFormatException: Image is too large to encode at 8x65536 for JPEG format.
8 x 65536, 1 plane(s): SixLabors.ImageSharp.ImageFormatException: Image is too large to encode at 8x65536 for JPEG format.
```

A whole run on a 65 536 x 128 px mono raster reached the JPEG stage and ended there with that
exception; every stage before it had passed.

## Decision

The owner's choice on 2026-11-20, of two put to them: such an image is analysed, without the
JPEG rows. `JpegAnalyzer.Analyze` asks the size first: with a side over
`Thresholds.JpegMaxSidePx` (65 535) nothing is encoded as JPEG, the result has no rows and
`TooLarge`, and the 8-bit TIFF size is measured as before. Section 1 then has, in place of the
"if the lab takes only JPEG" line, an Ok line, `A JPEG cannot hold this image: send the TIFF.`,
whose evidence is the image's size and the format's limit; Appendix C and the Export tab have
one line in place of the table, `JPEG: not measured (...)`, written once in the core
(`ReportWriter.JpegNotMeasured`), and the tab leaves out the guide to a table that is not there.

## Alternatives rejected

- **Refuse the image at load.** Simpler, but a large panorama would get no measurement at all,
  for the sake of a format the report does not recommend.
- **Catch the encoder's exception.** It would hide any other failure of the encoder behind the
  same note, and encode nothing all the same.
- **An Attention line.** That no JPEG can be made says nothing against the image: the TIFF of
  the first line is what the lab gets either way.

## Consequences

- `JpegMaxSidePx` is the format's limit, not a rule of thumb, and is in `Thresholds` so that the
  texts interpolate it.
- A caller that reads `JpegResult.Rows` meets an empty list for such an image; the three that
  exist (`Recommendations.Format`, Appendix C, the Export tab) ask `TooLarge`.
- Not looked at: what the window's own bitmaps (the galleries, the previews, the map) do with an
  image that wide; it takes a real file of that size on screen.
- Guards: `R083_AnImageWithASideAJpegCannotHoldHasNoJpegRowsAndKeepsItsTiffSize`,
  `R091_AnImageAJpegCannotHoldIsAnalysedAndToldToSendTheTiff`,
  `R114_AnImageAJpegCannotHoldShowsTheCoresNoteInPlaceOfTheTable`.
