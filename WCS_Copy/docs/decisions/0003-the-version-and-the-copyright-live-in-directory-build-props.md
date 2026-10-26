# 0003. The version, the copyright and the licence expression live in `Directory.Build.props`

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

`<Version>`, `<Copyright>` and `<PackageLicenseExpression>` were in the console's csproj, the
only project, and `VersionTests` and `LicenseNoticeTests` read that file to bind the assembly
attributes to it. The kit's `scripts/release.ps1` reads and writes `<Version>` in
`Directory.Build.props` and nothing else, and a shared script is never edited in a project
(the kit's rule "Shared folders"). With a core, a console and a window, the version must be
the same in every assembly anyway.

## Decision

The three elements move, with their comments translated, into `Directory.Build.props`, which
every project of the solution imports; the csproj files keep only what is theirs.
`VersionInfo` and `LicenseInfo` keep reading the assembly attributes and hardcode nothing; the
tests follow: `TheVersionComesFromTheBuildProps`, `TheCopyrightComesFromTheBuildProps`, the
same assertions on the new file, and `RepoInvariantTests` checks that `<Version>` is declared
there exactly once. The version number itself does not change: 1.0.0 stays until the owner
bumps it (the kit's rule "Version and year are the owner's").

## Alternatives rejected

- **Asking the kit for a configurable version file.** `Directory.Build.props` is where the kit
  and every sibling keep it; a parameter would serve one project's habit.
- **A version per project.** The window and the console are one tool at one version; the
  banner `copywcs 1.0.0` is printed by the core for both.

## Consequences

- A release is `scripts/release.ps1 -Version X.Y.Z`, which edits this file; nothing else
  carries the number. `copy_wcs.py` carries no `__version__`, as before.
- The copyright year rolls only at the owner's word, in this one place.
