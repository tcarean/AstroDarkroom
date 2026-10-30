# 0024. GPLv3-or-later: the notice on every .cs file, the copyright always on screen, the warranty text in About

Date: 2026-10-02
Status: accepted 2026-10-02; About as `VersionInfo.About`, a text, in `MessageDialog` superseded by 0045

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Licensing"), the `StatusLegal` comment
in `src/ImageVisualCompare.Wpf/MainWindow.xaml`, the comments of `VersionInfo.Copyright` and
`VersionInfo.About` in `src/ImageVisualCompare.Wpf/Version.cs`, the `LicenseHeaderTests` summary
and `README.md` ("Licență"); the decision itself dates from c6b676d (added license and
versioning, 2026-09-08), which added `LICENSE`, the notice in every `.cs` file, `VersionInfo`,
`LicenseHeaderTests`, `VersionTests`, the status bar's legal text and the About button. GPLv3
wants every copy to carry the notice, and its §5(d) wants a program with an interactive
interface to show the absence of warranty, not only the copyright. A header kept by habit alone
"is lost silently in the first file written in a hurry, and only a licence audit finds out"
(the `LicenseHeaderTests` summary).

## Decision

- The tool is GPLv3-or-later. `LICENSE`, at the project root, is the verbatim FSF `gpl-3.0.txt`,
  neither reworded nor reflowed, and `<PackageLicenseExpression>` is `GPL-3.0-or-later`.
- Every `.cs` file of the three projects starts with the notice, above its own header comment:
  the line `SPDX-License-Identifier: GPL-3.0-or-later`, which licence scanners read without
  parsing English, the copyright, and the three paragraphs of the standard GNU notice
  (permission, no warranty, where to get a copy), every line with a `//` prefix. `.xaml` files
  carry none. `LicenseHeaderTests` walks an explicit list of the three projects, skipping `bin/`
  and `obj/`, and compares the first lines of each file with the notice.
- The copyright is on screen for the whole run: `StatusLegal`, docked right in the status bar
  so that it is never what gets clipped when the window narrows, shows the banner and
  `VersionInfo.Copyright`.
- The About button shows `VersionInfo.About` in the application's own `MessageDialog`
  (ADR 0018): the banner, the copyright, "This program comes with ABSOLUTELY NO WARRANTY.", the
  terms (GNU GPL, version 3 or later) and where the full text is (`LICENSE`, and the GNU URL).
  It is a property, not text built in the click handler, so a test reads it without opening a
  window.
- `<Copyright>` in `Directory.Build.props` (the Wpf csproj until ADR 0004) is the one source of
  the copyright string, read back through `AssemblyCopyrightAttribute`; the literal in
  `ReadCopyright` is only a fallback. The year and the author change only at the owner's word
  (the kit's rule "Version and year are the owner's").

## Alternatives rejected

- **The warranty text in the status bar.** Five lines of legal text across the bottom of a
  comparison window "would crowd out the pixel readout" (`CLAUDE.md`, "Licensing").
- **The About text built in the button's handler.** A test would have to open a window to read
  it (the `VersionInfo.About` comment).
- **The notice on `.xaml` files too.** The project followed FlatCheck's `.cs`-only convention
  (`CLAUDE.md`, "Licensing").

## Consequences

- A new `.cs` file gets the notice before anything else. A new project is a decision, because
  `LicenseHeaderTests` lists the projects (ADR 0002); a second check in it fails when the scan
  misses a known file of any of the three or finds 25 files or fewer, so it cannot pass on none.
- Rolling the year or changing the author, the owner's call, touches every `.cs` header, the
  `Notice` array of `LicenseHeaderTests`, `<Copyright>`, the fallback in `ReadCopyright`, the
  exact string `VersionTests` expects and the README's "Licență".
- `LICENSE` is checked for four markers of the GPLv3 text ("GNU GENERAL PUBLIC LICENSE",
  "Version 3, 29 June 2007", "TERMS AND CONDITIONS" and "How to Apply These Terms to Your New
  Programs"): that catches a file that is not the GPL, not a reworded or reflowed one, so "don't
  reword it, and don't reflow it" stays a rule.
- Guards: `LicenseHeaderTests` (the notice on every `.cs` file of the listed projects, `LICENSE`
  present and the GPLv3 text), `VersionTests` (`<Copyright>` and `<PackageLicenseExpression>` in
  `Directory.Build.props`, the copyright read back, About's banner, copyright, disclaimer and
  pointers). Where the copyright and About appear on screen has no test: the application has to
  be launched and looked at (ADR 0026).

Source: CLAUDE.md ("Licensing"); src/ImageVisualCompare.Wpf/MainWindow.xaml (`StatusLegal`);
src/ImageVisualCompare.Wpf/Version.cs; tests/ImageVisualCompare.Tests/LicenseHeaderTests.cs;
tests/ImageVisualCompare.Tests/VersionTests.cs; README.md ("Licență"); LICENSE; the diff of
c6b676d.
