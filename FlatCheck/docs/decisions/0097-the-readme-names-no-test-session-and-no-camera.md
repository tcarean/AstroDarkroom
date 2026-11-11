# 0097. The README names no test session and no camera

Date: 2026-11-11
Status: accepted 2026-11-11
Supersedes: 0096 in part (the lenses it names)

## Context

ADR 0096 took "the author" out of the README the same day and kept the lenses, the masters and
the figures it named. The owner then asked for every README to drop what is tied to one test
set: the nights named Veil and Smida, the dated masters, the real folder paths. Those frames are
not in the repository, so their names tell a reader nothing. The owner also doubted the names of
the camera and the lenses, since the programs are meant for any camera and any session,
wide-field or deep-sky. A review of the suite's READMEs, place by place, found about 260 such
places, 82 of them here, and a few facts gone stale; the owner approved its eight
recommendations. The review also found that about 410 lines of the README described the window
tab by tab, as the suite's wiki manual does, without linking it.

## Decision

The README names no test session, master or date of a test, and no camera or lens but where a
command example or a profile's file name needs one. A figure that tells a user what to expect stays, given by
what was measured ("a test master", "one test lens", "in the tests"); the table of the sixth
test keeps its rows with the lenses unnamed; a timing or a memory figure stays rounded, without
its date or the computer; an example path is generic (`D:\Astro\MySessionFolder`). Where the
limits come from tests with one camera, the README says so without naming it, and it no longer
asks a user to recalibrate limits no option sets. The window's part keeps what the wiki manual
does not cover and links the manual. What a maintainer needs sits in one section at the end, and
the user text carries no ADR or requirement number.

## Alternatives rejected

- **Keeping the lenses and sessions (ADR 0096).** A reader cannot open those masters, so their
  names and dates help no one.
- **Keeping the window's part in full.** Two texts of the same window would have to be kept in
  step, and the README did not even link the other.
- **Replacing the sixth test's table with one sentence.** Its rows show which light sources pass
  and which fail, which a reader can act on without knowing the lens.

## Consequences

- An example added later is given by what it shows, never by the night's name, the lens or the
  date; the masters the figures were measured on stay named only in `docs/validation.md` and the
  other maintainer documents.
- A change to the window's tabs updates the wiki manual; the README changes only for what the
  manual does not cover.
- The other READMEs of the suite, its front page and the wiki's manuals took the same rule the
  same day, each project with an ADR of its own.
