# 0019. JPEG verdicts use the maximum error and the share over two levels, never the mean error

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `docs/specification.md` §5 (C3, "Measured and relied
upon"), `docs/implementation-plan.md` ("Notes from phase 3", "Notes from phase 4") and
`docs/acceptance-de-printat.md`; the decision dates from phases 3 and 4, 2026-09-20. The
conversation's JPEG table was made with ImageMagick (libjpeg); the tool encodes with ImageSharp
(ADR 0009). Measured while building: this close to lossless the mean error is not monotone in the
quality (integer YCbCr costs 1 to 2 levels at q100), while the maximum error and the count of
samples over 2 levels are. ImageSharp's TIFF LZW gave 38.6 MB on the reference image without the
horizontal predictor, where ImageMagick gives 20.

## Decision

- A JPEG quality is acceptable when its maximum error is at most 10 levels; q100 is an Attention
  when more than 5 % of its samples are over 2 levels; the mean error is reported and votes
  nothing.
- The error is measured against the unrounded float x 255, so it includes the 8-bit rounding the
  owner also pays, as the conversation's table did.
- The percentiles of about 69 million error samples come from a histogram of 0.01-level bins, not
  a sort: exact to the bin, a hundred times cheaper.
- The TIFF size is TIFF LZW with the horizontal predictor.
- The figures are ImageSharp's encoder's, and the report does not pretend otherwise.

## Alternatives rejected

- **A verdict on the mean error.** Not monotone in the quality here: q100 could read worse than
  q95.
- **The error against the rounded 8-bit image.** It would hide the rounding that the 8-bit file
  carries anyway.
- **libjpeg's numbers.** A native encoder would end the console's single managed file (ADR 0009);
  the difference is tenths of a level at q100.

## Consequences

- On the reference image: 12.5 / 3.2 / 1.9 MB, maximum error 4.5 / 27.0 / 57.2 levels; 1.2 % of
  q100's samples over 2 levels, against 3.03 % with ImageMagick; TIFF 8-bit LZW 20.7 MB.
- Another encoder will differ by tenths of a level at q100 (`known-limitations.md`).
- Guards: `JpegAnalyzerTests`, validation case V-001.

Source: docs/specification.md §5; docs/implementation-plan.md; docs/acceptance-de-printat.md.
