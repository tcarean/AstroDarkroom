# 0057. A FITS card's value is read from right after `=`, as cfitsio reads it

Date: 2026-11-20
Status: accepted 2026-11-20

## Context

The FITS standard writes a value card as the keyword in columns 1 to 8, `= ` in columns 9 and
10, and the value from column 11. `FitsHeader.ReadCards` took a card as a value card when its
ninth character was `=` and read the value from the eleventh on; the tenth was skipped without
being looked at. The review of 2026-11-13 found the case
(`docs/review/2026-11-13-code-review.md`, CR-008): a card written with no space after the `=`
lost the first character of its value. `NAXIS1  =5938` gave a width of 938 and `BITPIX  =-32`
gave 32, so floats were decoded as integers, and the file was shown as garbage with no message.
No file the owner has is written so; a crafted or a broken one is.

The entry left two ways open: refuse the file (`FITS: the header is not valid.`, R-019), or read
the value from the tenth character.

Read on 2026-11-20 in the source of cfitsio 4.7.0 (GitHub, `HEASARC/cfitsio`, branch `develop`,
`fitscore.c`), the library Siril reads a FITS through: `ffpsvc` takes the value from column 11
when columns 9 and 10 are `= `, and otherwise looks for the first `=` in the card and takes the
value from right after it; only a card with no `=` at all has no value. So Siril reads
`NAXIS1  =5938` as 5938 and opens the file.

The owner chose on 2026-11-20, of the two: as Siril.

## Decision

A card has a value when its ninth character is `=`, and the value is everything after that
character, trimmed: the space of column 10 goes with the trim when the file has it and nothing
is lost when it does not. `ReadCards` hands `Value` the card from its tenth character on. The
tests are `FitsHeaderTests.R014_ReadLayout_ReadsAValueWrittenRightAfterTheEqualsSign` and
`R014_ReadLayout_ReadsAStringWrittenRightAfterTheEqualsSign`.

## Alternatives rejected

- **Refusing the file.** Stricter than Siril, the display this program is compared against
  (ADR 0029), which opens it. It would also need a rule for which cards count: a commentary card
  such as `COMMENT =====` has `=` in column 9 too and real files hold such lines, so the refusal
  could only be for the keywords the reader uses.
- **Taking such a card as one with no value, as the standard reads it.** The keyword would fall
  back to its default: no image found for a missing `NAXIS1`, and for a `BZERO` an image 32,768
  too low, shown in silence.
- **Looking for the `=` anywhere in the card, as cfitsio also does.** A keyword shorter than
  eight characters followed at once by `=` (`NAXIS1=5938`) is read by cfitsio too. The review
  found no such case, and a commentary line with a `=` somewhere in its text would become a
  value card; left as it was.

## Consequences

- A file written without the space is shown as Siril shows it.
- A commentary card whose text starts with `=` in column 9 is still stored under its keyword,
  with one more character than before; no keyword the reader uses is a commentary one.
- R-014 says where a value starts; `docs/design/imaging.md` says where the code does it.

Source: docs/review/2026-11-13-code-review.md (CR-008); cfitsio 4.7.0 `fitscore.c` (`ffpsvc`);
src/ImageVisualCompare.Core/FitsHeader.cs; tests/ImageVisualCompare.Tests/FitsHeaderTests.cs.
