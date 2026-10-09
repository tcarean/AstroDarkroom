# 0010. GPLv3-or-later: About shows the copyright and the warranty disclaimer, the copyright string has one source, and LICENSE is copied next to the executable

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Licensing", the three bullets after the
first), `README.md` ("Licence"), the comments of `src/MindfulnessReminder.Core/LicenseInfo.cs`,
the `LICENSE` item and its comment in
`src/MindfulnessReminder.App/MindfulnessReminder.App.csproj`, and `LicenseHeaderTests`; the
decision itself dates from d3ccd6c (licensing and versioning, 1.0.0, 2026-09-01), which added
`LICENSE`, `LicenseInfo`, `Directory.Build.props` with `<Copyright>` and
`<PackageLicenseExpression>`, the copy of `LICENSE` into the build output, and an About item in
the tray menu. GPLv3 §5(d) asks each interactive user interface of the work to display
Appropriate Legal Notices, which its §0 defines as a copyright notice, word that there is no
warranty, that the work may be conveyed under the licence, and how to view a copy of it; a
prominent item in a menu meets that. The application is a tray icon with no console, and at
d3ccd6c its only window was the reminder, where the notice "cannot go ... without defeating the
point of the reminder" (`CLAUDE.md`, "Licensing"). In d3ccd6c About was a `MessageBox` raised by
the tray menu; since 432edfd (the dark theme, 1.3.0, 2026-09-08) it is `AboutWindow`, which shows
the same text (ADR 0032). The comment on the fallback in `LicenseInfo` says "the csproj is the
truth"; the property has been in `Directory.Build.props` since d3ccd6c.

## Decision

- The program is licensed GPLv3-or-later. `LICENSE`, at the project root, is the verbatim GNU
  GPL v3 text, and `<PackageLicenseExpression>` in `Directory.Build.props` is
  `GPL-3.0-or-later`.
- About, in the tray menu, shows `LicenseInfo.AboutText`: the banner (ADR 0011) on the first
  line, the copyright on the second, then `LicenseInfo.Warranty`, which says the program "comes
  with ABSOLUTELY NO WARRANTY", that it may be redistributed and modified under the GNU GPL v3 or
  any later version, and that the full text is "in the LICENSE file next to the executable, and
  at https://www.gnu.org/licenses/gpl-3.0.html". The text is a property in Core; the window
  "composes nothing itself" (`CLAUDE.md`, "Structure").
- `<Copyright>` in `Directory.Build.props` is the one source of the copyright string: MSBuild
  writes it into `AssemblyCopyrightAttribute`, `LicenseInfo.Copyright` reads it from there, and
  Windows shows it under the executable's Properties > Details. The literal in `LicenseInfo` "is
  a fallback, not a second place to edit" (README, "Licence").
- The App project links the root `LICENSE` as a `None` item with
  `CopyToOutputDirectory="PreserveNewest"`, so the build output, and a `dotnet publish` from it,
  hold the file About points at.

## Alternatives rejected

- **The notice on the reminder window.** It "cannot go on the reminder window without defeating
  the point of the reminder" (`CLAUDE.md`, "Licensing").
- **A banner printed at the start of every run, as the command-line tools of this repository
  print theirs.** A tray application has nowhere to print it: "a tray app has no console to print
  them on" (the `LicenseInfo` summary).
- **The copyright written in code.** "Don't hardcode it anywhere: the literal below is a fallback
  for the case where the attribute goes missing, not a second place to edit" (the same summary).
- **`LICENSE` at the project root only.** About's "next to the executable" would be false in the
  build output and in a publish, and "GPLv3 wants a copy of the licence to travel with every copy
  of the program" (the comment on the item in the App csproj).

## Consequences

- Rolling the year or changing the author is the owner's call (the kit's rule
  "Version and year are the owner's"; the kit's ADR 0029). It touches `<Copyright>`, the fallback
  in `LicenseInfo`, every file header and the `Notice` array of `LicenseHeaderTests` (ADR 0009),
  the exact copyright string `LicenseHeaderTests` expects, and the README's "Licence".
- `LICENSE` stays the verbatim text: `LicenseHeaderTests` looks for four markers of the GPLv3
  ("GNU GENERAL PUBLIC LICENSE", "Version 3, 29 June 2007", "TERMS AND CONDITIONS", "How to
  Apply These Terms to Your New Programs"), which catches a file that is not the GPL, not a
  reworded one.
- The copy next to the executable is checked in the project file, not in the output:
  `LicenseHeaderTests` looks for the `None` item for `LICENSE` with `CopyToOutputDirectory` in the
  App csproj.
- Guards: `LicenseHeaderTests` (the GPL text; the copyright read from `Directory.Build.props` and
  its exact value; About's text carries the copyright, the disclaimer, the GPL v3, `LICENSE` and
  the GNU address; the `LICENSE` item of the App) and `VersionTests` (About's text opens with the
  banner, then the copyright). What the About window shows is App: none by test; launch the
  application from the tray and open About.

Source: CLAUDE.md ("Licensing"; "Structure"); README.md ("Licence");
src/MindfulnessReminder.Core/LicenseInfo.cs;
src/MindfulnessReminder.App/MindfulnessReminder.App.csproj;
src/MindfulnessReminder.App/AboutWindow.xaml.cs; src/MindfulnessReminder.App/TrayIconHost.cs;
Directory.Build.props; tests/MindfulnessReminder.Tests/LicenseHeaderTests.cs;
tests/MindfulnessReminder.Tests/VersionTests.cs; LICENSE (sections 0 and 5); the diff of d3ccd6c.
