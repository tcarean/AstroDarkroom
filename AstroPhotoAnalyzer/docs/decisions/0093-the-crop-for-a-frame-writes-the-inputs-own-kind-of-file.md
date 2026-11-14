# 0093. The crop for a frame writes the input's own kind of file, a FITS as its own bytes

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

The owner has a frame of a given size (70 x 54.62 cm) and wants the image cut to exactly its
proportions, choosing by hand which part is left out, and saved as a new file of the same kind
and depth as the input, with no loss: a 32-bit FITS gives a 32-bit FITS, a 16-bit TIFF a 16-bit
TIFF. Until now the tool wrote two kinds of image, both after a change of the pixels (ADR 0032,
ADR 0083), both from the raster in memory, and said that every other export is made from Siril.
The raster cannot carry every input without loss: it is 32-bit float clipped to 0..1 (R-013), so
a 32-bit integer or 64-bit float FITS and any value outside 0..1 do not survive it.

Measured on 2026-11-14 on `de_printat.png` (16-bit RGB, 5938 x 3888, an `iCCP` of 9552 bytes),
its 4983 x 3888 crop written several ways (a scratchpad probe on ImageSharp 3.1.11):

| writer | size | time | samples equal | profile |
| --- | --- | --- | --- | --- |
| ImageSharp's PNG encoder, default | 85.1 MiB | 16.8 s | yes | written, the same bytes |
| the same, compression level 1 | 86.0 MiB | 11.4 s | yes | written |
| by hand, filter None, zlib Optimal | 99.3 MiB | 12.8 s | yes | - |
| by hand, filter Up, zlib Optimal | 85.8 MiB | 13.9 s | yes | - |
| by hand, filter Paeth, zlib Optimal | 85.6 MiB | 20.4 s | yes | - |

The encoder wrote the `iCCP` chunk (and a `pHYs` the source did not have), from a loaded image
and from a fresh one given a profile: `dependencies.md` and `input.md` say it does not, which
this measurement contradicts. The plate-solved reference FITS keeps its solution in `CRPIX1`,
`CRPIX2`, `CRVAL`, `CDELT`, `PC` and SIP cards; only `CRPIX` depends on where the image starts
(the SIP polynomials are in pixels from `CRPIX`).

## Decision

The crop is the tool's third export and the only one that changes no sample. A FITS is cropped
on its bytes: the rectangle's bytes are copied from the input file, in the file's own row order,
under the input's header with `NAXIS1`, `NAXIS2` set to the new size and `CRPIX1`, `CRPIX2`
moved by what was cut before them. A TIFF of 8 or 16 bits is written by `TiffWriter` at the
input's depth, a PNG by ImageSharp's encoder at 16 bits or 8, both from the analysed raster with
the input's ICC profile; a JPEG becomes an 8-bit TIFF. The owner's answers of 2026-11-14: the
JPEG as an 8-bit TIFF; a tab named `Crop with aspect ratio`, the image filling it, without
zoom; the two sizes read as typed, width then height.

## Alternatives rejected

- **A FITS crop through the raster and `FitsWriter`, as the two other exports.** It rounds a
  32-bit integer to 24 bits and loses every value the reader clipped; the owner asked for no
  loss, and the byte copy is also the shorter code.
- **`CRPIX` left as it is.** The cropped file would name the wrong sky for every pixel, and
  the tool's own dust catalogue (ADR 0025) would read it.
- **A PNG writer by hand, as the TIFF's.** The same size and time as the encoder (the table),
  and a hundred lines to maintain; the TIFF writer exists only because the encoder cannot
  write 16 bits (ADR 0083).
- **A JPEG saved again as a JPEG.** A second compression changes the pixels; the owner chose
  the 8-bit TIFF.
- **A JPEG refused.** It leaves the owner of a JPEG with no crop at all.
- **A TIFF of 32 bits cropped through ImageSharp's conversion.** The reader already converts
  it to 16 bits (R-012), so the crop would not be the input's samples; it is refused with a
  sentence.
- **Zoom and pan on the crop's image.** The owner asked for the image on the whole area
  without zoom; the rectangle is placed to the pixel of the reduced copy, a few image pixels.
- **The frame's long side laid along the image's long side, with a rotate button.** The owner
  chose the sizes as typed.

## Consequences

`FitsWriter` gains a second way of writing, on bytes, and `TiffWriter` an 8-bit depth; a new
`PngWriter` is the second place the core touches ImageSharp's encoders. The crop of a TIFF or
PNG carries the pixels and the profile only: an alpha channel, a second page and other tags are
not written, and a compressed TIFF comes out uncompressed. `CLAUDE.md`, the README and
`extending.md` name three exports. The sentence about the PNG encoder and `iCCP` in
`dependencies.md` and `input.md` is corrected to what was measured; the hand-built PNG fixture
stays. Guarded by the `R180_` to `R183_` tests: the FITS crop's data equal the input's bytes for
every BITPIX, `CRPIX` moves by the cut, each kind reads back to the rectangle of the analysed
raster.
