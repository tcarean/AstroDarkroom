# 0041. The README is in English

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0025 in part (README.md in Romanian); 0006 in part (README.md stays Romanian)

## Context

ADR 0025 made `README.md` the user documentation, in Romanian, and ADR 0006 kept it the
project's one Romanian file: the reader was the owner, and the interface was English only. Since
2026-10-25 the interface is English or Romanian, English unless `--lang ro` is given (ADR 0038),
so the Romanian README described a window that starts in English, its labels quoted in English
with the Romanian ones in parentheses. On 2026-10-26 the repository got an English front page
whose section on this tool sends the reader to its `README.md` as the user guide (AstroDarkroom's
ADR 0008), and the same day the owner asked for this README to be rewritten in English, up to
date and relevant.

Read against `docs/requirements.md` and the code on 2026-10-26 (`main` at 876782d), by this task
and by a second reader, the Romanian text was right on most points. It was wrong on three: its
first line and the licence's spoke of two images "of the same size" (a pair of different sizes is
compared on one canvas since 1.1.0, R-003, R-004), it put nearest-neighbour "above" 400 %, where
it starts at 400 % (R-025), and it gave the plateau of 570 MB reached after several zoom levels
for a pair as soon as it is drawn, which at fit holds 283 MB (R-080). It left out, among others,
what cancelling an open dialog does (R-001), the scrollbars (R-043), the zoom badge and the window
title (R-053, R-008), the divider staying where it is while the images move under it (R-030),
that the keys and buttons zoom about the centre (R-022, R-024), that a file is not locked once
loaded (R-010), and that the readout is the 8-bit level on screen, the high byte of a 16-bit
value (R-015, R-051). Its section for developers retold the adoption of the kit on 2026-10-02,
which ADR 0001 and `CLAUDE.md` hold. Its figures are those of R-080 and `docs/validation.md`;
none was measured again.

## Decision

- `README.md` is in English, like everything else in the project but the Romanian wordings of
  `UiText` (ADR 0038). It quotes the English interface, the default, and gives the Romanian name
  of a button or a readout beside it, in parentheses, for a reader of the Romanian one: a
  quotation, which the rule "Two interface languages" allows.
- The rest of ADR 0025 holds: the README is the user documentation and the one place that says
  how to use the tool; `docs/requirements.md` states the same behaviour with IDs; a user-visible
  change updates both in the same task.
- The README holds what a user of the tool needs. For the process it has a short "For developers"
  section of pointers into `CLAUDE.md` and `docs/`, and does not retell them.

## Alternatives rejected

- **Keeping it in Romanian.** The front page is English and sends readers here, and the window
  starts in English; a reader who does not read Romanian would meet a Romanian page at the first
  link.
- **One README per language.** Two texts of some 300 lines to keep in step at every user-visible
  change, for an interface that already speaks both; the owner asked for one, in English.
- **Keeping the section for developers as it was.** It restated `CLAUDE.md`, the kit's process
  rules and ADR 0001, and two copies drift, the reason ADR 0025 gives against restating the
  README elsewhere.

## Consequences

- `CLAUDE.md` (its layout and the rule "User documentation lives in `README.md`, and only
  there"), `docs/requirements.md`, `docs/design/README.md` and `docs/skills.md` (step 8 of
  `/new-feature`, step 7 of `/release`) say English and name the README's English sections;
  `docs/validation.md` compares the decode time of V-001 with R-080 instead of the README's
  Romanian words. The README gives the decode time of the V-001 pair, about three seconds (2.63
  to 3.64 s in its eight runs), where the Romanian one gave R-080's 2.7 s for another pair.
- The Romanian wordings keep the words the Romanian README used: "linia" for the divider,
  "pânza" for the canvas, "tu" for the user. `docs/design/language.md` and the comments of
  `UiText` say so, since no Romanian document holds those words any more.
- The repository's front page said every tool's README is in Romanian; it now names this one as
  English (AstroDarkroom's ADR 0008: a task that changes a fact the front page restates edits it
  there in the same task).
- Guards: none by test, as for ADR 0025; the kit's `/wrap-up` reads the README against every
  user-visible change, and the docs-auditor reads it (`kit.json`, `audit.alsoRead`).
