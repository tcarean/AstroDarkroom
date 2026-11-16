# 0103. A FITS header number that cannot be read is not its default

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

`FitsReader.HDbl` and `HInt` parsed a card's value with `NumberStyles.Any` and gave the caller's
default when the parse failed. The code review of 2026-11-13 (CR-007) followed three cases.
.NET does not read the `D` exponent the FITS standard allows, so `BZERO = 3.2768D+04` on a 16-bit
file read as 0: the samples were taken as signed, everything from mid-scale up was clipped, and
the report described another image under an Ok-level note. `NumberStyles.Any` accepts forms FITS
does not have: `0,5` read as 5, `1,024` as 1024, `(5)` as -5. And `Wcs.TryFrom` tested its
keywords for presence only and read them with a default of 0: a `CDELT1` with a `D` exponent made
a complete plate solution "no WCS", while a `CRVAL1` written `18:06:22` gave a WCS at RA 0 and a
report that says "no galactic band in the frame". cfitsio, so Siril, writes `E`; that a given
writer emits `D` was not checked on a file, the standard allows it.

## Decision

One parser reads a header number, `FitsReader.TryNumber`: a sign, digits with an optional point,
an optional exponent after `E` or `D`, nothing else, and finite. What a caller does with a
keyword that is present and is not such a number depends on what the number is for:

- a number the image cannot be read without (`BITPIX`, `NAXIS`, `NAXISn`, `BZERO`, `BSCALE`)
  refuses the file, with the keyword and the value as the file holds it: `FITS: BZERO din header
  nu se poate citi ca numar: '3.2768X+04'.` (`HDbl`, `HInt`; a whole number written as a real,
  `16.0`, is still one);
- a WCS number means the header holds no WCS (R-018: "any other header means no WCS, never an
  error"), through `TryHDbl`.

An absent keyword keeps its default, as before.

## Alternatives rejected

- **Keep the default and add a note to the report.** A BZERO read as 0 moves every sample; the
  figures under the note would still be another image's.
- **Refuse the file for an unreadable WCS number too.** The WCS is optional for the analysis,
  and R-018 already makes every unusable header "no WCS"; `--wcs` reports a file without one.
- **Keep `NumberStyles.Any` and add only the `D` exponent.** `0,5` would still read as 5.

## Consequences

- `FitsWriter` reads the same members, so a curve or a crop saved from a FITS reads `BZERO` and
  `BSCALE` the same way; the reader has refused an unreadable one before any save.
- A header whose sizes parse but are impossible (a negative axis, a product past the file) is
  CR-012, its own entry, still open.
- Guards: `R011_ARealWithADExponentIsReadAsFitsWritesIt`,
  `R011_AHeaderNumberThatCannotBeReadRefusesTheFile`,
  `R018_ADExponentIsReadAndAnUnreadableNumberMeansNoWcs`.
