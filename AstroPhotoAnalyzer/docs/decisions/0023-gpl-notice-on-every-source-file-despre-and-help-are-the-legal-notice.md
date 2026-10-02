# 0023. The GPL notice is on every source file; "Despre" and --help are the legal notice; a package is listed where it is shown

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from the old `CLAUDE.md` ("Licensing", and "A new package
means three places" in "The dependencies"), `docs/specification.md` §12 and §13, and
`LicenseHeaderTests` and `ThirdPartyTests`; the decision dates from phase 0, 2026-09-20, as in
FlatCheck (its ADR 0040). The tool is GPLv3-or-later, with the verbatim FSF text in `LICENSE`.
GPLv3 §5(d) asks a program that displays legal notices to display the warranty disclaimer too.
The console publishes as one file, with nothing beside it. ImageSharp's licence is conditional,
and its compatibility has to be shown, not assumed (ADR 0009).

## Decision

- Every `.cs`, `.xaml` and `.py` file starts with the notice: the SPDX line, the copyright and the
  three GNU paragraphs; `//` for C#, `#` for Python below a shebang, one `<!-- -->` block for XAML,
  which is source like C#. `LicenseHeaderTests` walks an explicit list of the five projects, plus
  `Icon/*.py`, so adding a project is a decision, and checks that `LICENSE` is the GPLv3 text.
- `--help` ends with the warranty disclaimer (`RunReport.WarrantyLines`) and points at `LICENSE`.
  The window's "Despre" (a button, or F1) shows the version, the copyright, the same disclaimer and
  the third-party list from `ThirdParty.Components`.
- A package means its `PackageReference` (and, since ADR 0003, its version in
  `Directory.Packages.props`), an entry in `THIRD-PARTY-NOTICES.md` with the GPLv3 compatibility
  argued, and an entry in `ThirdParty.cs`, which "Despre" shows. `ThirdPartyTests` binds the core's
  csproj and the list in both directions and each entry to a heading of the notices file. The
  README's table is the fourth copy, kept by hand.
- The year and the author of the copyright are the owner's (the kit's rule "Version and year are
  the owner's").

## Alternatives rejected

- **The disclaimer under the banner on every run.** Five lines over every report would bury the
  numbers; `--help` is where it is looked for.
- **A notices file only.** The single-file console has nothing beside it.
- **Discover the projects from the solution.** A project left out of the solution's view would
  escape the check silently; the explicit list makes the omission visible.

## Consequences

- A new file gets the notice first, not as an afterthought; a new package cannot reach "Despre"
  without its notice, or be dropped from the csproj with its entry left behind.
- Guards: `LicenseHeaderTests`, `ThirdPartyTests`, `Golden/help.txt`, `WindowSmokeTests` (the
  About window), R-004, R-119, R-121 and R-122.

Source: CLAUDE.md (2026-09-21); docs/specification.md §12, §13.
