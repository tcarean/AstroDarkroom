# 0012. A copy is refused when the destination's own plate solve is the mirror image of the source's

Date: 2026-11-06
Status: accepted 2026-11-06

## Context

The review of the branch that brought R-016 (ADR 0011) found, and a read-only measurement
confirmed, that the SyQon CLI's outputs among the owner's files are images flipped top to bottom.
On the Veil pair, rows 200, 1000, 2800 and 3600 of `procesate\bge-cc\result-bge-cc.fit`
correlate 0.98 to 1.00 with the mirrored rows of `denoised_syqon_studio_in.fit` and about 0 with
the same rows. The SyQon CLI writes a complete flipped plate solve for them: a CD matrix whose
determinant has the opposite sign (+2.9641E-6 against the stack's -2.9641E-6), `CRPIX2 = 1846`
(3693 - 1847), and every SIP coefficient the flip changes, negated; only Siril's `PC` and `CDELT`
are left as they were. Siril reads the CD first, so it shows such a file right on its own. A copy
of the stack's solve into it is wrong whatever it does with the old cards, since the pixels are
mirrored: before R-016 the kept CD hid the error (Siril's names nearly right), with R-016 Siril
shows the mirrored copy. Of the owner's 231 plate-solved files, read CD first, 9 have a positive
determinant, all SyQon outputs (the 4 with a CD and 5 that Siril saved again in its own PC
form); the other 222 are negative. The owner chose, on 2026-11-06, to refuse such a copy.

## Decision

When both files have `CTYPE1` naming the same first axis and a linear part (a CD or a CDELT
card), and the determinants of those linear parts, read as Siril reads them (the CD matrix first,
else CDELT times PC), have opposite signs, the run is refused with nothing written, a dry run
too, unless `--force` (in the window, the renamed option `Continue even when the dimensions
differ or the image is mirrored`), which turns the refusal into a warning (R-017). The two
conditions keep out what only looks like a mirror: the same solve written declination first, a
`LINEAR` one, and a header with no scale at all, whose standard defaults give a determinant of
+1.

## Alternatives rejected

- **Docs only.** The 9 files would take a copy without a word, and in place the destination's
  own plate solve, the one that fits its pixels, would be lost.
- **A warning only.** In place, the destination's own plate solve is gone before the user reads
  the warning.
- **An override of its own.** A second check box for a rare case; `--force` already means "I
  know this pair is right although a check says no".
- **Keep the destination's own plate solve when it is mirrored and copy the rest.** A half copy
  the user did not ask for, which leaves the reference pixel and the distortion of one solve
  beside the scale of the other.

## Consequences

- A flip that CopyWcs could not see before is caught when whatever flipped the pixels flipped
  the destination's own plate solve with them, as Siril's transforms and the SyQon CLI do. A
  program that flips the pixels and passes the header through leaves the source's orientation
  in it, and a destination without a plate solve has nothing to compare: neither is caught, and
  the reminder to check the annotation by eye stays.
- A destination whose own plate solve was left mirrored after its pixels were flipped back is
  refused although the copy would be right; the refusal's advice to flip the image back does not
  fit it, and `--force` is the way through. None of the owner's 231 plate-solved files is such a
  case, nor a solve written declination first, nor one without a scale (rescanned 2026-11-06).
- The check reads numbers and the first axis from the header (`FitsHeader.GetDouble`, the
  exponent letter `D` taken as `E`; `FitsHeader.GetString`); a value that is not a number, or a
  determinant of 0, leaves it out, and the log says at Debug which way the two solves faced when
  they matched or could not be compared.
- `--force` now passes two checks, R-005 and R-017; its texts in the window and in the usage say
  so.
- V-002's pair is refused without `--force`; its copy is measured forced.
- Guarded by the `R017_` tests of `FitsHeaderTests`, `WcsMetadataKeysTests`, `WcsCopyTests`,
  `CopyWcsCliTests`, `MainViewModelTests` and `ValidationTests`, the label in `R036_`, the two
  `mirror` scenarios of `LanguageTests`, and `R042_AMirroredDestinationIsLoggedWithBothDeterminants`.
- Measured end to end on 2026-11-06 on the real Veil pair: refused with exit 1, both files
  unchanged by SHA-256; with `--force` and `-o` into a scratch folder, the warning, 4 keys dropped
  and 72 copied; the pair the other way round refused too.
