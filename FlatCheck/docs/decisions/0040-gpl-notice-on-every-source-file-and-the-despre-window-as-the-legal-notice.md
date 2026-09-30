# 0040. Every source file starts with the GPL notice, and the "Despre" window is the program's legal notice

Date: 2026-09-25
Status: accepted 2026-09-25; the copyright in flatcheck.py superseded by 0047

## Context

Retroactive: recorded on 2026-09-25 from docs/design/process.md ("Licensing") and
docs/design/code-map.md (`ThirdParty.cs`, `Views/AboutWindow.xaml`); the decision itself dates
from the original design, extended when the window and the third-party packages arrived. The
tool is GPLv3-or-later, with the verbatim FSF text in `LICENSE`. GPLv3 §5(d) wants a program that
displays legal notices to display the warranty disclaimer too. LibRaw is taken under LGPL-2.1
(its CDDL option is not GPL-compatible), and LGPL-2.1 §6 says that a program which displays
copyright notices at runtime - this one does, on line 2 of every report and in the window's top
bar - must include the library's notice among them, with a reference to the LGPL. The single-file
exe carries no notices file beside it.

## Decision

Five things, all pinned by tests: every source file starts with the notice (SPDX line,
copyright, the three GNU paragraphs; `//` for C#, `#` for Python below the shebang, one
`<!-- ... -->` block for XAML, which counts as source); every run prints the copyright on line 2;
`--help` carries the warranty disclaimer (`RunReport.WarrantyLines`) and points at `LICENSE`; the
window's "Despre" (button in the top bar, F1 through `ApplicationCommands.Help`) is its §5(d)
notice and shows the third-party list from `ThirdParty.Components` with each component's
copyright line - LibRaw's from LibRaw 0.21.1's `COPYRIGHT` file; one source of truth per language
for the copyright string, `<Copyright>` in `Directory.Build.props` read back through
`AssemblyCopyrightAttribute` and `__copyright__` in `flatcheck.py`. The year and the author are
the owner's call, like the version: never rolled forward on an assistant's initiative.

## Alternatives rejected

- **The warranty text under the banner on every run.** Five lines on top of every report would
  drown the numbers; `--help` is where it lives.
- **Shipping the notices file only.** The single-file exe has nothing beside it, and LGPL-2.1 §6
  asks for the notice where the program displays its own; hence the list is displayed.
- **Discovering the projects `LicenseHeaderTests` walks.** The `Projects` list is explicit, so
  adding a project is a decision.
- **Scrubbing the copyright line like the banner.** It is constant, so the golden files carry it
  verbatim and `crosscheck.py` compares it as text; if it ever stops being printed, the goldens fail.

## Consequences

- A new file gets the notice first, not as an afterthought; `LicenseHeaderTests` walks all four
  projects plus the two `.py` files (and `Icon/*.py`, `lensfun/update-lensfun.py`) and fails on
  any file missing it.
- A new package needs an entry in `THIRD-PARTY-NOTICES.md` (the why of the compatibility) and in
  `ThirdParty.cs` (what "Despre" displays); `ThirdPartyTests` binds the two to the Core csproj
  in both directions.
- The two warranty notices (C# `WriteUsage`, Python argparse `epilog`) are worded the same by
  choice, not by test: `--help` is not one of the 43 cross-check scenarios.
- `AboutViewModel` formulates nothing: constant properties from `VersionInfo`,
  `RunReport.WarrantyLines`, `ThirdParty.Components` and `SessionMessages`.
- Guards: `LicenseHeaderTests`, `Golden/help.txt`, `AboutTests`,
  `LibRawIsTakenUnderTheLgplAndCarriesItsCopyright`, `VersionTests` (binds the three copyright
  sources), `ThirdPartyTests`, `WindowSmokeTests` on the About content.
- Source notes: docs/design/process.md, docs/design/code-map.md.
