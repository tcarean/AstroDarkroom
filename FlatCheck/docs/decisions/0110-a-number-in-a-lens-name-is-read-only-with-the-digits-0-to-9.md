# 0110. A number in a lens name is read only with the digits 0 to 9

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

`LensSpec.Parse` reads a lens's focal range and maximum aperture out of its name with three
patterns whose `\d` matches, in a .NET regex, every Unicode digit, and then parsed what they
matched with `double.Parse` in the invariant culture, which takes the digits 0 to 9 only. The
review of 2026-11-13 (CR-014) followed it: one profile whose name has full-width digits
(`１３５mm`) throws a `FormatException` out of `Parse`, and the search by range reads every name
of the catalogue (`LensIdentification.CandidatesByRange`), so that one name ends the
identification of every raw that reaches it. No name Adobe or lensfun ships has such digits
(the reviewer checked the 3531 installed Adobe files and the lensfun database); a profile the
user adds can.

## Decision

The owner's choice of 2026-11-13, between "the name does not say" and converting the digits:

1. A number of a name is read only when it is written with the digits 0 to 9. A number with
   any other digit, in whole or in part, is not read: the name gives no focal range, or no
   aperture, and a part a name does not give contradicts nothing (ADR 0030, ADR 0104).
2. The patterns keep `\d`, so that such a number is found whole, and the parse refuses it
   (`double.TryParse`, then a finite check). With `[0-9]` in the patterns, the direction the
   review gave, `f/1.٨` would be read by its ASCII part as f/1 and a `１35mm` as 35 mm: a
   wrong figure where none is meant, which then contradicts the right raw.
3. A focal range is both ends or neither: `18-２００mm` gives no range.
4. Nothing throws out of `Parse`, whatever the name.

## Alternatives rejected

- **Convert the digits** (full-width to 0 to 9, by Unicode's decimal value). It reads `１３５mm`
  as 135 mm, for a case no shipped profile has, and it has to decide which scripts count and
  what a mixed number means. A name that is not read costs only that the lens is kept as a
  candidate where its name might have ruled it out.
- **`[0-9]` in the patterns**, as point 2 says.
- **Catch the exception around the search.** The lens would still be lost, and with it the
  reason: a name that cannot be read is an ordinary case here, not an error.

## Consequences

- A lens with such a name is never a candidate by range alone (that path needs all three
  parts), and is never left out for contradicting a raw.
- `LcpReader` and `LensfunReader` read the profiles' own numbers from XML attributes, not from
  names; they are not touched (their acceptance of NaN and Infinity is in the review's cleanup
  list).
- R-018 holds the behaviour; `R018_ANameIsReadWithTheDigitsZeroToNineOnly` and
  `R018_OneNameWithOtherDigitsDoesNotStopTheIdentification` in `LensProfileTests.cs` pin it.
