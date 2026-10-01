# 0032. A Covers sentence names what its note describes, not the folder around it

Date: 2026-10-01
Status: accepted 2026-10-01

## Context

ADR 0031 has `/wrap-up` list the design notes whose Covers sentences name a code or test file a
branch changed. Every path in a Covers sentence counts the same: the listing cannot tell "in
full" from "by pointer". Measured on 2026-10-01 on the first adopter, replaying its history with
its notes of that day: of 30 branches merged since its notes existed, 12 listed a note, and its
whole-project map was among them every time; in 5 of the 12, all process tasks that touched only
tests, it was the only note listed, and the map says eight lines about the test projects, all
about their project files. Of 58 earlier commits, 51 would have listed it. A note listed for
changes it says nothing about teaches the reader to skim the list.

The map itself is no copy of the area notes: 1% of its five-word sequences appear in another
note, and about half its sentences give a reason (a decision, a measurement, a "so that"). Moving
it into the area notes would be a rewrite of some 420 lines across 15 notes, not a deduplication.
The map describes every file of the core library by name, so its listing for a change there is
right.

## Decision

A Covers sentence names what its note describes, and no more: a folder only when the note
describes its files, a file only when the note says how it works. A file the note only points
at, or a folder it mentions in one line, stays out of the sentence. `docs/design/README.md`
says so to authors; the check is unchanged.

## Alternatives rejected

- **Teach the check "in full" and "by pointer".** A second grammar inside the sentence, and a
  note would still be listed for the files it covers only by pointer.
- **Move the map into the area notes, leaving a map of pointers.** Measured above: no duplicates
  to drop, half of it reasons that must move. Kept for later, if the narrowed map is still
  listed for changes it says nothing about.
- **Leave the sentences as they are.** The measured noise stays.

## Consequences

The first adopter's map names the core library, the console's three files, the five window
folders it describes, the two test project files and `Icon/`, and points at the rest after the
sentence. Replayed on the same history, it is listed in 7 of the 30 branches instead of 12, and
the 5 test-only branches list no note; on the 58 earlier commits, 43 instead of 51.
