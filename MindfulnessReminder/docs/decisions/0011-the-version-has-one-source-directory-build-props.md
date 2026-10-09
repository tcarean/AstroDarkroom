# 0011. The version has one source, Directory.Build.props, read back from the assembly: no literal in code, no version in a project file

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Versioning"), `README.md` ("Versioning"),
the header and comments of `src/MindfulnessReminder.Core/Version.cs` and the `VersionTests`
summaries; the decision itself dates from d3ccd6c (licensing and versioning, 1.0.0, 2026-09-01),
which created `Directory.Build.props` with `<Version>1.0.0</Version>` and added `VersionInfo` and
`VersionTests`. The application is a `WinExe` in the tray: "There is no console to print a banner
on, so the app identifies itself in the three places a user can actually see it" (README,
"Versioning"), the tray tooltip, About and the crash dialog, so that "a screenshot of any of the
three says on its own which version produced it" (the `VersionInfo` summary). The solution builds
three assemblies, and "A `<Version>` in any project file would override the shared one for that
assembly, and the three would silently drift" (the summary of
`R090_No_project_overrides_the_shared_version`). The version has gone to 1.1.0, 1.2.0, 1.3.0 and 1.4.0
since, each step an edit of that one element (ADR 0006).

## Decision

- One source: `<Version>` in `Directory.Build.props`, beside `<Copyright>`. MSBuild carries it
  into `AssemblyInformationalVersion`; `VersionInfo.Value` reads it from there, cuts any
  `+sourcerevision` suffix at the `+`, and falls back to the assembly version, then to "0.0.0".
  No version is written as a literal in code, and no project file declares a `<Version>`.
- `VersionInfo.Banner`, "Mindfulness Reminder" and the version, is how the application names
  itself: at the start of the tray tooltip, on the first line of About, after the error in the
  crash dialog, and at the end of the `--detect` report. Windows reads the same property for the
  executable's file version.
- Who changes the version, and when, is the kit's rule "Version and year are the owner's" (the
  kit's ADR 0029), not repeated here.

## Alternatives rejected

- **A literal in `Version.cs`.** A second place to edit, where the header of `Version.cs` wants
  "One place to edit"; the README says "Don't reintroduce a literal".
- **A `<Version>` in a project file.** It would override the shared one for that assembly, and
  "the three would silently drift" (`VersionTests`).
- **`AssemblyVersion` as the value shown.** It "would give "1.0.0.0", which is not what anyone
  wants to read" (the comment in `VersionInfo.Read`).

## Consequences

- A bump is one edit in `Directory.Build.props` and breaks nothing: "there are no golden files
  here, and `VersionTests` asserts the shape of the version and the plumbing behind it, never a
  particular number" (`CLAUDE.md`, "Versioning").
- `scripts/release.ps1` reads and sets the same element and floors a release at the newest
  released heading of `CHANGELOG.md`, 1.4.0 (ADR 0006; the kit's ADR 0029); the comment in
  `Directory.Build.props` says not to move `<Version>` or `<Copyright>` into a csproj.
- The banner is part of the tooltip, which `NotifyIcon` caps at 63 characters, so a longer
  version string leaves less room in it (ADR 0025).
- Guards: `VersionTests` (a three-part number, equal to `<Version>` in `Directory.Build.props`,
  with no `+` suffix and no fourth part; `R090_No_project_overrides_the_shared_version` over every
  `.csproj` under the project folder; the banner is the name and the version; About's text opens
  with the banner). Where the banner reaches the screen (the tooltip, the About window, the crash
  dialog, the `--detect` report) is App wiring: none by test; launch the application from the
  tray.

Source: CLAUDE.md ("Versioning"); README.md ("Versioning"); src/MindfulnessReminder.Core/Version.cs;
src/MindfulnessReminder.Core/LicenseInfo.cs; src/MindfulnessReminder.App/App.xaml.cs;
src/MindfulnessReminder.App/TrayIconHost.cs; tests/MindfulnessReminder.Tests/VersionTests.cs;
Directory.Build.props; the diff of d3ccd6c; the kit's ADR 0029.
