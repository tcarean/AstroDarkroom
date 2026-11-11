# 0050. The README names no test file and links the wiki manual

Date: 2026-11-11
Status: accepted 2026-11-11
Supersedes: 0025 in part (how to use the tool written in README.md and nowhere else); 0049 in part (the files it names)

## Context

ADR 0049 took "the author" out of the README the same day and kept the test files and the
figures it named. The owner then asked for every README to drop what is tied to one test set:
named test files, dated measurements, real paths. Those files are not in the repository, so
their names tell a reader nothing. A review of the suite's READMEs, place by place, found about
260 such places and a few facts gone stale; the owner approved its eight recommendations. One
of them: the suite's wiki has held a manual of the window since 2026-11-06, while the README
said that how to use the tool was written there and nowhere else (ADR 0025).

## Decision

The README names no test file, test image or date of a measurement; an example uses generic
names (`before.png`, `after.tif`), and a figure that tells a user what to expect stays, rounded,
without its date or the computer. What a maintainer needs (building, the tests, where the
requirements, decisions and design notes are) sits in one section at the end, and the user text
carries no ADR or requirement number. The README links the window's manual in the suite's wiki;
the two are the user's documentation, the README for the program as a whole, the manual for the
window.

## Alternatives rejected

- **Keeping the test files' names (ADR 0049).** A reader cannot open those files, so their names
  help no one.
- **The README alone, as ADR 0025 had it.** The manual exists and is where a user of the window
  looks; a README that ignored it would send readers past it.

## Consequences

- `CLAUDE.md`'s rule on the user documentation names the wiki manual beside the README.
- A change to the window updates the wiki manual too.
- The other READMEs of the suite, its front page and the wiki's manuals took the same rule the
  same day, each project with an ADR of its own.
