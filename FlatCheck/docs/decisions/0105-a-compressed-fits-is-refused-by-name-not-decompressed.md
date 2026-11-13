# 0105. A compressed FITS is refused by name, not decompressed

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

FITS has a convention for storing an image compressed: the pixels are cut into tiles, each tile
is compressed (Rice, gzip, PLIO or HCOMPRESS) and the tiles are the rows of a binary table,
one variable-length column, with `ZIMAGE = T` and the image's own keywords under `Z` names
(`ZBITPIX`, `ZNAXIS1`, ...). `fpack` writes it, and so does Siril when its FITS compression is
on. The primary HDU of such a file is empty and the table is the first extension.

`FitsReader.LoadFull` took the first HDU with two axes and both sides above 0 for the image and
never looked at `XTENSION`. A table has two axes: `NAXIS1` is the row's length in bytes,
`NAXIS2` the number of rows, and its `BITPIX` is 8. The review of 2026-11-13 (CR-008) read that
a compressed image would therefore come out as a picture 8 pixels wide. Measured the same day
on a file built to the convention (an empty primary, then `XTENSION= 'BINTABLE'`, `BITPIX 8`,
`NAXIS1 8`, `NAXIS2 1024`, `PCOUNT 400000`, `ZIMAGE T`, for a 1536 x 1024 frame compressed by
rows): `FitsReader.Load` returned an image 8 x 1024 without an error; the structure mode
printed its whole report and `VERDICT: REFA`, exit 3; `--histeq` wrote a picture, exit 0;
`--expo` refused it as "not a raw flat", exit 1. No real compressed master was at hand; the
reader looks only at the header keywords, which are the convention's.

## Decision

1. An extension is an image only when its `XTENSION` is `IMAGE`. The primary HDU is an image
   when it has one, as before.
2. A table extension whose `ZIMAGE` is `T` stops the read with a sentence that says what the
   file is and what to do: `FITS: the image is stored compressed (tile compression, as Siril's
   FITS compression or fpack write it), which FlatCheck does not read. Save the file without
   compression.` It is an `InvalidDataException`, like the cut file of R-009, so every mode
   exits 1 with it.
3. Any other table is stepped over whole: its rows and its heap, `|BITPIX| / 8 * GCOUNT *
   (PCOUNT + NAXIS1 * NAXIS2)` bytes padded to a block, cut data refused as in R-009. The
   search goes on and ends, without an image, in the existing "no HDU with a 2D image".

## Alternatives rejected

- **Decompressing the tiles.** Rice and gzip tile decoding, with quantised floating-point
  tiles and their dithering, is a reader of its own. The FITS reader stays hand-rolled
  (`CLAUDE.md`, "Package notices"), and a master is saved uncompressed with one switch in the
  program that wrote it.
- **Refusing every table.** A file may carry a table beside its image (a catalogue, a
  processing log); the image after it is still the image.
- **Leaving it to the "data cut short" check.** A table's bytes are all there, so nothing was
  cut; the file was read, and wrongly.

## Consequences

- A user with Siril's FITS compression on gets one sentence and exit 1, where a REDO verdict
  on meaningless bytes was printed.
- The header-only read (`FitsReader.LoadHeader`, the window's prefill) still looks at the
  primary HDU alone and so reads nothing from such a file; the run that follows it loads the
  file whole and shows the refusal. That difference between the two reads is CR-029's.
- `NAXISn` and `BITPIX` are still used before they are checked (CR-013); the table branch adds
  no new use of an unchecked value beyond the product the image branch already makes.
- R-136 holds the behaviour; `R136_...` in `FitsReaderTests` pins the refusal from the reader
  and from four modes of the console, the skipped table and the cut one.
