# 0058. A `BZERO` or `BSCALE` that is not a number is refused, where cfitsio takes the default

Date: 2026-11-20
Status: accepted 2026-11-20

## Context

`FitsHeader.Number` reads `BZERO` and `BSCALE`. It refused a value that parses to NaN or an
infinity (R-019), but text that does not parse at all was treated as a missing keyword and read
as the default, 0 or 1. The review of 2026-11-13 found the case
(`docs/review/2026-11-13-code-review.md`, CR-009): a 16-bit file with
`BZERO   =             3.2768d4` (a lower-case exponent letter; only a capital `D` was replaced
by `E`) or `32768,0` got a `BZERO` of 0, its samples were read as signed, and the image was
shown 32,768 too low with its dark half black, with no message. `Count`, beside it, already
never reads a value as its default.

Read on 2026-11-20 in the source of cfitsio 4.7.0 (GitHub, `HEASARC/cfitsio`, branch `develop`),
the library Siril reads a FITS through:

- `fitscore.c`, `ffc2dd`: a capital `D` is replaced by `E` before `strtod`; a lower-case `d` and
  a decimal comma stop the conversion, which then returns the error `BAD_C2D`.
- `getkey.c`, `ffgphd`: when `ffc2dd` fails on `BSCALE` or `BZERO`, the error is reset, the
  value is set to 1.0 or 0.0, a line goes to cfitsio's own message stack, and the read goes on.

So Siril opens such a file and shows it with the default, as this program did. Being "as Siril"
here means showing the same wrong image without a word; refusing means the two programs differ
on a file no program writes. The owner chose on 2026-11-20, of the two: refuse the file.

## Decision

The default of `BZERO` or `BSCALE` is taken only when the header has no such keyword. A value
that is there and is not a finite number (text, an empty value, a lower-case `d` as the
exponent letter, a decimal comma, NaN, an infinity) makes the header invalid:
`FITS: the header is not valid.` (R-019), at the probe, before anything is decoded. The Fortran
exponent letter stays the capital `D`, the only one cfitsio replaces; a lower-case `e`
(`3.2768e4`) is a number for cfitsio's `strtod` and for this reader alike, and is read. The
tests are
`FitsHeaderTests.R019_ABZeroOrBScaleThatDoesNotParse_IsAnInvalidHeader`,
`R019_ABZeroOrBScaleThatIsNotFinite_IsAnInvalidHeader` and
`R014_ReadLayout_ReadsALowerCaseE_WhichIsANumber_UnlikeALowerCaseD`.

## Alternatives rejected

- **The default, as cfitsio and so Siril.** The image on screen would match Siril's, but it is
  not the image the file stores, and nothing says so. This program exists to show a difference
  of one level; a silent offset of 32,768 is the opposite. cfitsio itself notes the error on its
  message stack; a display has nowhere to put such a note but a message.
- **Accepting a lower-case `d` or a decimal comma.** It would read what was meant in those two
  cases, and then show another image than Siril does for the same file, again in silence. A
  refusal says what is wrong and leaves the file to be fixed.

## Consequences

- This is the one place the reader is stricter than Siril on a file Siril opens: such a file
  cannot be compared here until its card is corrected. No file the owner has is written so.
- A quoted number (`'32768'`) is still read, since `Value` takes the quotes off before the
  number is parsed; nothing changed there.
- R-019 lists the case; `docs/design/imaging.md` says where the code refuses it.

Source: docs/review/2026-11-13-code-review.md (CR-009); cfitsio 4.7.0 `fitscore.c` (`ffc2dd`)
and `getkey.c` (`ffgphd`); src/ImageVisualCompare.Core/FitsHeader.cs;
tests/ImageVisualCompare.Tests/FitsHeaderTests.cs.
