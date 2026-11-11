# 0020. The front page and the README name no test session and no camera

Date: 2026-11-11
Status: accepted 2026-11-11; the front page's order superseded by 0021
Supersedes: 0008 in part (the order of the front page's sections); 0009 in part (the README's sections How a release is made and Building); 0019 in part (the equipment it names)

## Context

ADR 0019 took "the author" out of the front page the same day and kept the equipment and the
figures it named. The owner then asked for every README to drop what is tied to one test set,
the test nights' names and dates and the real folder paths, and doubted the value of naming the
camera and the kit, since the programs are meant for any camera and any session, wide-field or
deep-sky. A review of the suite's READMEs, place by place, found about 260 such places, 36 of
them in the front page and this project's README, and a few facts gone stale there: FlatCheck's
Smoothing tab, which is gone; DarksCheck's checks in their old order; the files FlatCheck writes
when a folder opens; CopyWcs's mirror check; the consoles said to be in each tool's own release,
which does not exist. The owner approved its eight recommendations, among them that what only a
developer needs moves to the end of each README.

## Decision

The front page and this project's `README.md` name no test session, date of a test, real path,
camera, lens or mount; where the limits come from tests with one camera, the front page says so
without naming it, and the suite is described for wide-field or deep-sky sessions. A timing or a
size stays rounded, without its date or the computer, and the user text carries no ADR or
requirement number. What only a developer needs sits under "For developers" at the end: on the
front page, after the licence, the build of the suite from a checkout and the repository's other
folders; in this README, how a release is made and the build. The front page links the suite's
wiki, where the windows of five tools have a manual, and this README links the wiki's home.

## Alternatives rejected

- **Keeping the equipment (ADR 0019).** A reader with another camera would read the tools as made
  for one kit.
- **Keeping the build second on the front page (ADR 0008).** Most readers come for the download
  and for what the tools do; the build is for the few who work on the source.

## Consequences

- The stale facts the review found were fixed in the same pass.
- The tools' own READMEs and the wiki's manuals took the same rule the same day, each project
  with an ADR of its own.
