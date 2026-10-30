# 0023. The version has one source and no literal in code; it is always on screen, so there is no --version flag

Date: 2026-10-02
Status: accepted 2026-10-02; the banner heading About in its title and on its first line superseded by 0045

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Versioning"), the header and `Read`
comments of `src/ImageVisualCompare.Wpf/Version.cs`, the `VersionTests` summary and
`src/ImageVisualCompare.Wpf/MainWindow.xaml.cs`; the decision itself dates from c6b676d (added
license and versioning, 2026-09-08), which set the version to 1.0.0. The application is a
window, with no console to print a version to; the version went to 1.1.0 and 1.1.1 on
2026-09-09 (ADR 0007). From c6b676d `<Version>` lived in
`src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj`; since today it lives in
`Directory.Build.props` (ADR 0004), where the kit's `scripts/release.ps1` reads and writes it.

## Decision

- One source: `<Version>` in `Directory.Build.props`. MSBuild carries it into
  `AssemblyInformationalVersion`, and `VersionInfo.Value` reads it from there, cuts any
  `+sourcerevision` suffix at the `+`, and falls back to the assembly version, then to "0.0.0".
  No version is written as a literal in code.
- It is on screen for the whole run. `VersionInfo.Banner`, "Image Visual Compare" and the
  version, is the window title, alone before a pair is loaded and after the two file names once
  one is; it opens the status bar's legal text, docked right, before the copyright; and it heads
  the About dialog, in its title and on its first line.
- There is no `--version` flag.
- The owner bumps the version by hand, per feature or fix, and chooses the increment (the kit's
  rule "Version and year are the owner's"; the kit's ADR 0029 for how a release uses it).

## Alternatives rejected

- **A `--version` flag.** Not needed: "a GUI that already displays it permanently does not need
  one" (`CLAUDE.md`, "Versioning").
- **A literal in `Version.cs`.** A second place to change, where the header comment wants "a
  single place to change"; `CLAUDE.md` says "Don't reintroduce a literal there."
- **`AssemblyVersion` as the value shown.** It reads "1.0.0.0", "which is not what should be
  shown" (the `Read` comment); the informational version keeps the three-part form.

## Consequences

- A bump is one edit in `Directory.Build.props` and breaks no test: `VersionTests` checks the
  form and the plumbing, never a number. Removing `<Version>`, or breaking the way `VersionInfo`
  reads it back, fails it.
- `scripts/release.ps1` reads and sets the same element, and floors a release at the newest
  released heading of `CHANGELOG.md`, 1.1.1 today (the kit's ADR 0029; ADR 0007).
- Guards: `VersionTests` (a three-part number, equal to `<Version>` in `Directory.Build.props`,
  with no `+` suffix and no fourth part; the banner is the name and the version). Where the
  banner is shown is wiring in `MainWindow`, with no test: the application has to be launched
  and looked at (ADR 0026).

Source: CLAUDE.md ("Versioning"); src/ImageVisualCompare.Wpf/Version.cs;
tests/ImageVisualCompare.Tests/VersionTests.cs; src/ImageVisualCompare.Wpf/MainWindow.xaml.cs;
Directory.Build.props; the diffs of c6b676d, 861f5c1 and f4b91ee.
