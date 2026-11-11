# 0045. The README names no test session and no camera

Date: 2026-11-11
Status: accepted 2026-11-11
Supersedes: 0044 in part (the nights and the equipment it names)

## Context

ADR 0044 took "the author" out of the README the same day and kept the nights, the equipment and
the figures it named. The owner then asked for every README to drop what is tied to one test
set: the nights named Veil and Smida, their dates, the real folder paths. Those frames are not in
the repository, so their names tell a reader nothing. The owner also doubted the names of the
camera, the lens and the mount, since the programs are meant for any camera and any session,
wide-field or deep-sky. A review of the suite's READMEs, place by place, found about 260 such
places and a few facts gone stale, here the order in which the pixel size is chosen; the owner
approved its eight recommendations.

## Decision

The README names no test night or date of a test, and no camera, lens or mount but where the
program depends on it: the default EAST and NORTH arrows are those of a Star Adventurer GTi with
the SynScan Pro app, and the last fallback pixel size, 3.92 um, is the Nikon D3300's
specification. A figure that tells a user what to expect stays, given by what was measured ("a
night of 87 frames at 135 mm"), not by the night's name; a timing stays rounded, without its date
or the computer; an example path is generic (`D:\Astro\MySessionFolder`). The tests, the gate
and the pointers to the requirements, the decisions and the design notes sit in one section at
the end, while building keeps a section of its own, since the console exists only once it is
built; the user text carries no ADR or requirement number. The README links the window's manual
in the suite's wiki, and gives the order in which the pixel size is chosen as the program
follows it: a FITS frame's `XPIXSZ`, then the size typed, then the size worked out from the raw.

## Alternatives rejected

- **Keeping the nights and the equipment (ADR 0044).** A reader cannot open those frames, so
  their names and dates help no one.
- **Naming the tested kit once.** The owner wants the programs read as working with any camera
  and mount; the two places where they matter to the program name them.
- **Only adding the wiki link.** The owner chose the whole pass.

## Consequences

- An example added later is given by what it shows, never by the night's name or date; the
  nights the figures were measured on stay named only in `docs/validation.md` and the other
  maintainer documents.
- The facts the review found stale were fixed in the same pass.
- The other READMEs of the suite, its front page and the wiki's manuals took the same rule the
  same day, each project with an ADR of its own.
