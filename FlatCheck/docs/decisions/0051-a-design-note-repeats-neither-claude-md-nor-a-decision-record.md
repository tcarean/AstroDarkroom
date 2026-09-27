# 0051. A design note repeats neither CLAUDE.md nor a decision record

Date: 2026-09-27
Status: accepted 2026-09-27

## Context

ADR 0001 moved every section of the old CLAUDE.md unchanged into `docs/design/`, and the
retroactive ADRs 0008 to 0041 were written the same day from those notes. Two days later
`docs/design/process.md` still carried, word for word, what ADR 0039 (the version), ADR 0040
(the GPL notice), ADR 0042 (the banner) and ADR 0043 (the invariant test host) had recorded from
it, plus the build, test and publish commands that CLAUDE.md, `kit.json` and `README.md` already
hold, and the rules 1, 3, 9 and 17 in their own words. Of its 463 lines, about 170 said nothing
a reader could not find in a shorter, dated, guarded form elsewhere; one sentence ("the flag
must come after the file path") contradicted R-003 and CLAUDE.md. On 2026-09-27 the owner asked
for a review of the note and the deletion of everything duplicated in CLAUDE.md or in a decision.

## Decision

A design note keeps only what neither CLAUDE.md nor an ADR says: the mechanics of the code it
covers and the reasons that have no other home. Where an ADR was written from a section of the
note, the section becomes a pointer to that ADR, naming what the ADR holds and keeping the
headings other documents cite. `process.md` is cut to that first; the same day the owner asked for
the other twenty notes, so the sweep is one task, note by note against the ADRs written from it
(the "Source notes" line of each retroactive ADR maps them).

## Alternatives rejected

- **Keep the copies and let `/docs-health` reconcile them.** The 2026-09-27 audit found three
  contradictions between the note and the rest of the docs in two days; two copies of a reason
  drift, and the ADR is the dated, guarded one.
- **Delete the sections outright, headings included.** ADR 0042 and `docs/skills.md` cite
  "Versioning", "Licensing", "Adding a golden scenario", "Adding a parity golden scenario" and
  "The README Opțiuni section" by name; a pointer under the same heading keeps those links
  working.
- **Move the mechanics into the ADRs too.** An ADR records a decision and its guards; how a
  `TempWorkspace` is built or what `RunCli` scrubs is design, and changes without a decision.

## Consequences

- `process.md` is 298 lines: "Conventions" keeps the README rule and its story; "Versioning",
  "Licensing" and "Build, test and publish" are pointers with the three mechanics ADR 0040 does
  not spell out; "Extending the console" is unchanged except where it restated rules 1, 3 and 9,
  ADR 0043, or the deleted "Run" sentence.
- A reason found in a design note and in an ADR is deleted from the note when next seen; a
  reason found only in a note and worth guarding gets an ADR, as ADRs 0039 and 0040 did.
- The sweep of 2026-09-27 took the folder from 4705 lines to 3499. Every note keeps its
  headings (ADRs, sibling notes, tests and two code comments cite them by name or by section
  number) and a "Covers:" line that says which files it now describes only by pointer.
- The rows of `docs/design/README.md` say what is pointer and what is full.
- Guards: none by test; `/docs-health` reads the notes against the code and the ADRs.
