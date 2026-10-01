# Design notes

One note per area whose code does not explain itself: a numeric model, a file format parser, a
protocol. How that code works and how its parts fit together are written here and nowhere else
(the kit's rule "Design notes", the kit's ADR 0031). Any other file may name the code, as an
address, and say what a test or a check enforces; the mechanics live in the note of their area.
A note may name classes and methods. It holds the mechanics of the code it covers and the reasons
that have no other home; it repeats neither CLAUDE.md nor an ADR, and points at them instead.
One note may map the whole project, file by file.

CLAUDE.md points here under "Read before touching". Nothing in this folder is loaded into a
session unless the task needs it.

## The Covers sentence

Every note says, above its first section, which files it describes, in a sentence that starts
with **Covers:**, in a quote block or not. For example: **Covers:** in full, `Stats.cs` and the
`Median` it exports; by pointer, `src/Starter.Cli/Program.cs`.

- A path in backticks may start at the project root, at a code or test folder of `kit.json`, or
  at this folder (another note); a file name alone is enough when the file is found somewhere in
  the project. `*` and `?` match within one name. Letter case counts.
- A span that is not a path is not checked: a class, a method, a test, a flag. A span with a
  "/" is taken for a path, so a formula such as A/B does not belong in the sentence.
- The sentence ends at the first full stop followed by a space or a line end, so an
  abbreviation such as "e.g." ends it early.
- The note is updated or deleted in the same task as the code it covers. More than one note may
  cover the same file.

## The index

One row per area. The Note column names the area's notes in backticks, by file name; a row may
name several. Every note is in the table, and every name there is a note.

## The check

`scripts/check-design.ps1` is a step of `scripts/verify.ps1`. It fails on a note missing from the
index, a name in the index that is not a note, a note without a Covers sentence above its first
section, and a Covers path that names no file or folder. It never judges the text.
`scripts/check-design.ps1 -Since main` lists the notes whose Covers sentence names a file the
branch changed; `/wrap-up` runs it, and each listed note is read against the change.

| Area | Note | Read before editing |
| --- | --- | --- |
| (none yet) | | |
