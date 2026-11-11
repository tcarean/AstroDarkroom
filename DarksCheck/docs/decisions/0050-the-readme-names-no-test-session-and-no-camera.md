# 0050. The README names no test session and no camera

Date: 2026-11-11
Status: accepted 2026-11-11
Supersedes: 0049 in part (the sessions and dates it names)

## Context

ADR 0049 took "the author" out of the README the same day and kept the sessions, the dates and
the figures it named. The owner then asked for every README to drop what is tied to one test
set: the nights named Veil and Smida, the dated sets of darks, the libraries named by their
temperature, the real folder paths. Those frames are not in the repository, so their names tell
a reader nothing. The owner also doubted the camera's name, since the programs are meant for any
camera and any session, wide-field or deep-sky. A review of the suite's READMEs, place by place,
found about 260 such places and a few facts gone stale; the owner approved its eight
recommendations.

## Decision

The README names no test session, set of frames or date of a test, and no camera, lens or mount
but where the program depends on it: the electrons of the ADU scale are known for the Nikon
D3300 alone. A figure that tells a user what to expect stays, given by what was measured ("a
session of 20 darks, 30 biases and 32 lights"), not by the night's name; a timing stays rounded,
without its date or the computer; an example path is generic (`D:\Astro\MySessionFolder`). Where
the limits come from tests with one camera, the README says so without naming it. Building, the
tests, the gate and the pointers to the requirements, the decisions and the design notes sit in
one section at the end, and the user text carries no ADR or requirement number. The README links
the window's manual in the suite's wiki.

## Alternatives rejected

- **Keeping the sessions and dates (ADR 0049).** A reader cannot open those frames, so their
  names and dates help no one.
- **Naming the tested camera once.** The owner wants the programs read as working with any
  camera; the one place where the camera matters to the program, the electrons, names it.
- **Only adding the wiki link.** The owner chose the whole pass.

## Consequences

- An example added later is given by what it shows, never by the night's name or date; the
  sessions the figures were measured on stay named only in `docs/validation.md` and the other
  maintainer documents.
- The facts the review found stale were fixed in the same pass.
- The other READMEs of the suite, its front page and the wiki's manuals took the same rule the
  same day, each project with an ADR of its own.
