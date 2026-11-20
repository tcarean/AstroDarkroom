# 0114. The sizes in a FITS header are checked before they are used

Date: 2026-11-20
Status: accepted 2026-11-20

## Context

The reader, the `--wcs` header walk and the two walks of the FITS writer (the curve's file, the
crop) each computed an HDU's data size from `BITPIX`, `NAXIS` and `NAXISn` with the same few
lines, and none asked whether the numbers were possible. Since ADR 0104 a number that cannot be
read refuses the file; one that reads and makes no sense was still followed. The code review of
2026-11-13 (CR-012) gave the cases, each from one number: `NAXIS1 = -2` in an HDU that is
skipped gives a data size of -2880 bytes, the skip seeks back to the header just read, and the
walk never ends (the console does not return, the window's run does not end; the crop's walk
loops the same way); `NAXIS = 2000000000` loops two billion times; 50000 x 50000 overflows the
raster's pixel count; 40000 x 40000 x 3 in a 3 KB file takes 19 GB before the file's length is
looked at; `BITPIX = -2147483648` throws the platform's overflow from `Math.Abs`.

## Decision

One member, `FitsReader.Shape`, reads an HDU's sizes and checks them, and the four walks read
them through it: `NAXIS` is between 0 and 999 (the standard's range), no `NAXISn` is negative,
`BITPIX` is one of 8, 16, 32, 64, -32, -64 when the HDU has axes (`NAXIS` over 0; an HDU
without axes has no samples: its `BITPIX` is read, and its value is not checked), and the
data's byte count fits a 64-bit integer. Each failure refuses the file with a sentence that names the keyword and
its value. The reader then checks the image itself before memory is taken for it: its `BITPIX`
is one the reader decodes, its pixels fit one array of the platform (`Array.MaxLength`), and,
when the stream can tell its length, the data are in the file. The writer's walks, which copy a
skipped HDU up to its header's end plus its data's blocks, refuse a negative end, which is what
a sum past the largest offset wraps to (the two reviews of this fix, 2026-11-20: the first
form, an end before its start, also refused a file whose last block is not padded, where the
copy of what follows the image starts past the file's end and rightly copies nothing).

## Alternatives rejected

- **Guard `SkipExact` against a negative count only.** It ends the hang and leaves the other
  four cases, and three more copies of the lines without the guard.
- **A limit on the image's size lower than the platform's.** How much memory an analysis may
  take is another question; the limit here is what cannot be held at all.
- **Check the length of a stream that cannot seek by reading it.** The reader reads files; a
  stream that cannot seek still fails, later, at the first read past its end.

## Consequences

- A FITS that the standard allows and the tool refused before is refused as before (four axes,
  two or four planes, `BITPIX = 64` as an image); only the sentence of `BITPIX = 64` now comes
  before the memory is taken.
- A real image larger than the memory still fails with the platform's out-of-memory message
  when its data are in the file; that is not this decision's case.
- The heap of a table HDU (`PCOUNT`) is still not counted when the HDU is skipped, as before;
  what the reader does with a binary table that has a heap before the image was not looked at
  (CR-081 of the review list).
- Guards: `R011_ANegativeAxisLengthIsRefusedOnEveryWalkOfTheFile`,
  `R011_AnImpossibleHeaderNumberIsRefusedWithAMessage`,
  `R011_AnImageLargerThanTheToolCanHoldIsRefusedByItsSize`,
  `R011_TheDataAreLookedForInTheFileBeforeMemoryIsTakenForThem`,
  `R011_AnEmptyPrimaryWithAnImpossibleBitpixIsReadAndCroppedAlike`,
  `R011_ASkippedHduWhoseEndPassesTheLargestOffsetIsRefusedByTheWriterToo`,
  `R145_ASourceWhoseLastBlockIsNotPaddedIsStillCroppedAndSaved`.
