# 0015. The README names no test data and links the wiki manual

Date: 2026-11-11
Status: accepted 2026-11-11

## Context

The owner asked for every README of the suite to drop what is tied to one test set: named
sessions and stacks, dated measurements, real folder paths, the camera. Those files are not in
the repository, so their names tell a reader nothing, and the programs are meant for any camera
and any session. A review of the suite's READMEs, place by place, found about 260 such places,
ten of them here, mostly references to maintainer documents and decision numbers in the user's
text; the owner approved its eight recommendations. The suite's wiki has held a manual of the
window since 2026-11-06, and the README did not link it.

## Decision

The README names no test session, stack or date of a test; an example that stays is given by
what it shows. What a maintainer needs (building, the tests, where the requirements, decisions
and design notes are) sits in one section at the end, and the user text carries no ADR or
requirement number. The README links the window's manual in the suite's wiki.

## Alternatives rejected

- **Leaving the README as it was.** Its few test names and decision numbers send a reader to
  documents that are not for them.
- **Only adding the wiki link.** The owner chose the whole pass.

## Consequences

- An example added later is given by what it shows, never by the name of a test stack.
- The other READMEs of the suite, its front page and the wiki's manuals took the same rule the
  same day, each project with an ADR of its own.
