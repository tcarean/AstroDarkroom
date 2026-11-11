# 0089. The README names no test image and holds the limits a user meets

Date: 2026-11-11
Status: accepted 2026-11-11

## Context

The owner asked for every README of the suite to drop what is tied to one test set: named test
images and sessions, dated measurements, real folder paths, the camera. Those files are not in
the repository, so their names tell a reader nothing, and the programs are meant for any camera
and any image. A review of the suite's READMEs, place by place, found about 260 such places, 25
of them here; the owner approved its eight recommendations. The review also found that the
README sent the user, for the program's other known limits, to a maintainer's note full of test
sessions, decision numbers and Romanian labels.

## Decision

The README names no test image, session or date of a test, and gives no real path; an example
is given by what it shows ("a test image"), and a timing stays rounded, without its date or the
computer. The limits a user meets, from `docs/design/known-limitations.md`, are said in the
README in plain words, and the link to the note goes. What a maintainer needs sits in the "For
developers" section at the end, and the user text carries no ADR or requirement number.

## Alternatives rejected

- **Keeping the link to the note.** The note is written for maintainers; a user would meet test
  names and decision numbers there, not the limits in their own words.
- **Copying the whole note.** Most of it explains what was measured, which a user does not need
  to work around a limit.

## Consequences

- A limit found later that a user can meet is added to the README's list too, in plain words;
  the measurement behind it stays in the note.
- The suite's wiki has no manual of this program yet; when it has one, the README links it.
- The other READMEs of the suite, its front page and the wiki's manuals took the same rule the
  same day, each project with an ADR of its own.
