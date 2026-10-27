# Design notes

One note per area whose code does not explain itself: a numeric model, a file format parser, a
protocol. How that code works and how its parts fit together are written here and nowhere else
(the kit's rule "Design notes", the kit's ADR 0031). Any other file may name the code, as an
address, and say what a test or a check enforces; the mechanics live in the note of their area.
A note may name classes and methods. It holds the mechanics of the code it covers and the reasons
that have no other home; it repeats neither CLAUDE.md nor an ADR, and points at them instead.

CLAUDE.md points here under "Read before touching". Nothing in this folder is loaded into a
session unless the task needs it.

## The Covers sentence

Every note says, above its first section, which files it describes, in a sentence that starts
with **Covers:**, naming its files as paths in backticks; the rules of the sentence and of the
index are the kit's (`docs/design/README.md` in `ClaudeCodeStarterKit`), checked by
`scripts/check-design.ps1` as a step of `scripts/verify.ps1`. The note is updated or deleted in
the same task as the code it covers.

## The index

| Area | Note | Read before editing |
| --- | --- | --- |
| The launcher: the catalogue, where an app is, the plan of a start, `release.json`, the two windows and the icons | `launcher.md` | anything in `src/AstroDarkroom.Core/` or `src/AstroDarkroom.Wpf/`, anything in `Icon/`, any text the window shows, in either language |
