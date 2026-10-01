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
with **Covers:** (or **Covers**:), in a quote block or not. For example: **Covers:** in full,
`Stats.cs` and the `Median` it exports; by pointer, `src/Starter.Cli/Program.cs`. A section may
have a Covers sentence of its own; it is checked the same way.

- Every Covers sentence names its files as paths in backticks. A class or a method alone names
  no file: `/wrap-up` finds the notes a change touches through the paths only.
- A path may start at the project root, at a code or test folder of `kit.json`, or at this
  folder (another note); a file name alone is enough when the file is found somewhere in the
  project. `*` and `?` match within one name, and nothing else is a wildcard. Letter case
  counts, and so does git: an ignored file, such as build output, is no file of the project.
- A span that is not a path is not checked: a class, a method, a test, a flag. A span with a
  "/", or ending in a known extension written in lower case, is taken for a path, so a formula
  such as A/B does not belong in the sentence.
- The sentence ends at the first full stop outside code followed by a space or a line end (not
  the one of "e.g.", "i.e.", "vs.", "etc.", "cf." or a list number), at a blank line or at a
  heading. A code span may wrap across lines; fenced blocks and HTML comments are not read.
- The note is updated or deleted in the same task as the code it covers. More than one note may
  cover the same file.

## The index

One row per area, in a table. The Note column names the area's notes in backticks, by file name;
a row may name several. Every note is in the table, and every `name.md` in a row is a note,
unless it names another file of the project.

## The check

`scripts/check-design.ps1` is a step of `scripts/verify.ps1`. It fails on a note missing from the
index, a name in a row of the index that is no note and no file, a note without a Covers
sentence above its first section, a Covers sentence that names no path, and a Covers path that
names no file or folder git has. It never judges the text. In the task's worktree,
`scripts/check-design.ps1 -Since main` lists the notes whose Covers sentences name a code or test
file (as `kit.json` lists them) the branch changed; `/wrap-up` runs it, and each listed note is
read against the change. Run where the branch is `main` itself, it is an error, not an empty list.

| Area | Note | Read before editing |
| --- | --- | --- |
| (none yet) | | |
