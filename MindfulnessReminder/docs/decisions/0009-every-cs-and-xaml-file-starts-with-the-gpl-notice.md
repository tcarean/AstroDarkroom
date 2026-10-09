# 0009. Every .cs and .xaml file starts with the GPL notice, kept by LicenseHeaderTests; a XAML comment rewords the double-hyphen dash

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Conventions", "Every source file starts
with the GPL notice"; "Licensing", the first bullet), `README.md` ("Licence", the first of its
four points) and the `LicenseHeaderTests` summary; the decision itself dates from d3ccd6c
(licensing and versioning, 1.0.0, 2026-09-01), which put the notice at the top of every `.cs` and
`.xaml` file of the three projects and added `LicenseHeaderTests`, and the note on `--` in XAML
from b6554d5 (the periodic reminder, 1.1.0, 2026-09-08), which wrote the convention into the new
`CLAUDE.md`. GPLv3 "asks that every copy carry the notice, so every source file starts with it"
(the `LicenseHeaderTests` summary), and the licence's own instructions for applying it advise
putting the notices at the start of each source file (`LICENSE`, "How to Apply These Terms to Your
New Programs"). Kept by habit, a header "quietly goes missing in the first file written in a
hurry, and you find out at a licence audit" (the `LicenseHeaderTests` summary). The C# comments
of this project use `--` as a dash, and XML comments "cannot contain `--`" (`CLAUDE.md`,
"Conventions").

## Decision

- Every `.cs` and `.xaml` file of `src/MindfulnessReminder.Core/`,
  `src/MindfulnessReminder.App/` and `tests/MindfulnessReminder.Tests/` starts with the notice,
  new files included: the line `SPDX-License-Identifier: GPL-3.0-or-later`, the copyright line,
  and the three paragraphs of the standard GNU notice (the permission, the absence of warranty,
  where to get a copy of the licence). In C# each of its sixteen lines starts with `//`; in XAML
  they sit in one `<!-- -->` block, indented four spaces, with `<!--` and `-->` on lines of their
  own.
- `LicenseHeaderTests` keeps the convention alive. It walks an explicit list of the three project
  folders, skips `bin/` and `obj/`, and compares the first lines of every `.cs` and `.xaml` file
  with the notice as that language comments it. A second test fails when the scan misses one of
  five known files (`LicenseInfo.cs`, `TrayIconHost.cs`, `LicenseHeaderTests.cs`, `App.xaml`,
  `BreakWindow.xaml`) or sees 20 files or fewer, so the first cannot pass on none.
- A comment in XAML rewords the `--` dash of the C# comments.

## Alternatives rejected

- **The header kept by habit, with no test.** The test "is the only thing keeping that convention
  alive as files get added" (the `LicenseHeaderTests` summary).
- **The notice on `.cs` files only.** The convention is "`.cs` and `.xaml` alike" (`CLAUDE.md`,
  "Conventions"): a XAML file is a source file of the same projects, and the notice belongs at
  the start of each source file.
- **The C# comment's `--` carried into XAML as written.** XML comments "cannot contain `--`"
  (`CLAUDE.md`, "Conventions"): the file would not be well-formed XML.

## Consequences

- A new `.cs` or `.xaml` file starts with the notice before anything else, "the first thing to
  write, not an afterthought" (README, "Licence"). A new project folder is not scanned until it
  is added to the list in `LicenseHeaderTests`.
- The check compares text: a reworded, reflowed or re-indented notice fails it as a missing one
  does.
- The notice carries the copyright line, so rolling the year or changing the author, which only
  the owner does (the kit's rule "Version and year are the owner's"), touches every header and
  the `Notice` array of `LicenseHeaderTests`, with the other places ADR 0010 lists.
- Guards: `LicenseHeaderTests` (the notice at the top of every `.cs` and `.xaml` file of the
  three projects; the scan sees all three projects and the XAML). The `--` rule: none by test.

Source: CLAUDE.md ("Conventions"; "Licensing"); README.md ("Licence");
tests/MindfulnessReminder.Tests/LicenseHeaderTests.cs; LICENSE ("How to Apply These Terms to
Your New Programs"); the diff of d3ccd6c.
