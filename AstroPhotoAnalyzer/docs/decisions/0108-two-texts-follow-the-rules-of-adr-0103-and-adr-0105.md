# 0108. Two texts follow the rules of ADR 0103 and ADR 0105

Date: 2026-11-16
Status: accepted 2026-11-16
Supersedes: 0105 in part (the warning's sentence being the same for both clauses)

## Context

Two rules changed on 2026-11-16 and each left a sentence behind, found by the fix itself and by
the reviews of its branch (CR-075 and CR-076 of the code review of 2026-11-13).

- Since ADR 0103 a frame whose stars are too few has no width in any zone. The Rezolutie tab's
  description of the zones still gave the zone's own count as the only reason: "A zone is shown
  only when it has at least 5 stars, otherwise n/a", beside a zone that reads `n/a` with 6
  stars.
- Since ADR 0105 a 16-bit file is an 8-bit upscale also when no plane uses more than 256 levels.
  The warning's sentence, written for the form level x 257, went on: "you lose nothing going to
  8 bits". That is exact for that form alone. A file saved by a shift (level x 256) rounded to 8
  bits gives 127.502 for level 128 and 128.498 for level 129, so the two merge; closely packed
  levels merge further.

A text of the window or of the report changes only on the owner's yes (the rule "Plain words");
both wordings were shown beside the old ones and approved on 2026-11-16.

## Decision

- The zones' description names both reasons: "A zone is shown only when it has at least 5 stars
  and the frame has enough stars to be measured, otherwise n/a." / "O zona se arata doar daca
  are cel putin 5 stele si cadrul are destule stele ca sa fie masurat, altfel n/a." The 5 stays
  interpolated from `Thresholds.RegionMinStars`.
- The warning's sentence follows the clause that held, as its evidence already does. For the
  x 257 form it stays as it was. For a file caught by its levels alone: "The file holds only 8
  bits of information, saved as 16 bits: the 16 bits bring nothing more than 8 would." /
  "Fisierul are doar 8 biti de informatie, salvati ca 16 biti: cei 16 biti nu aduc nimic in plus
  fata de 8."

## Alternatives rejected

- **One sentence for both clauses.** The x 257 form does lose nothing at 8 bits, and that is
  worth saying where it is true.
- **Say how many levels an 8-bit copy would merge.** It depends on where the levels sit and on
  how the copy is made; the tool does not measure it.

## Consequences

- No rule, figure or verdict moves; the two sentences alone. ADR 0105 said the warning's
  sentence is the same for both clauses; that part is superseded here.
- The x 257 sentence is picked as its evidence is, by the share of such samples being over
  half, and level 0 counts as such a sample: a file more than half pure black takes that
  sentence whatever its other levels are (CR-078 of the code review of 2026-11-13, open, older
  than this ADR). (Restated 2026-11-17: CR-078 was closed by ADR 0111; the share leaves out pure
  black and pure white.)
- Guards: `R115_TheZonesDescriptionNamesBothReasonsForNoWidth`,
  `R096_AFileCaughtByItsLevelsAloneIsNotToldItLosesNothingAtEightBits`.
