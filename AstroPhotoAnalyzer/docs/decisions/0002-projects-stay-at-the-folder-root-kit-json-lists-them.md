# 0002. The projects stay at the folder root; kit.json lists them and holds this project's values

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

The kit lays a project out as `src/` and `tests/`, and its shared scripts, skills and agent read
everything that differs per project from `kit.json` (the kit's ADRs 0017 and 0018).
AstroPhotoAnalyzer has five projects at the folder root (`AstroPhotoAnalyzer.Core`,
`AstroPhotoAnalyzer`, `AstroPhotoAnalyzer.Wpf`, `AstroPhotoAnalyzer.Tests`,
`AstroPhotoAnalyzer.Wpf.Tests`), referenced by path from the solution, from the `<Compile ...
Link>` items with which the window's tests borrow five fixtures from the core's, from the icon
items of two csproj files, from `LicenseHeaderTests.Projects`, and from the tests that build
paths from their own folder (`GoldenOutputTests`, `VersionTests`, `ThirdPartyTests`).

## Decision

The layout stays. `kit.json` lists, as this project's values: the solution; the code folders
(the core, the console, the window) and the two test folders; the test filter
`Category!=Validation` (ADR 0004); the test data folder `AstroPhotoAnalyzer.Tests/testdata/`;
Stryker run from `AstroPhotoAnalyzer.Tests/`; two publish targets, the console as one
self-contained single file for `win-x64` under `console/` and the window as a self-contained
folder under `wpf/`; and, for the `docs-auditor`, `THIRD-PARTY-NOTICES.md` and the golden texts
under `AstroPhotoAnalyzer.Tests/Golden/`, the console's expected output. The project's own steps
for the kit's skills are in `docs/skills.md`.

## Alternatives rejected

- **Move the projects under `src/` and `tests/`.** `git mv` keeps the history, but the move
  touches the solution, five csproj files, the linked compile items, the icon paths, three tests
  and the README, for nothing the scripts need: `kit.json` lists folders.
- **Copies of the scripts edited to name the folders.** FlatCheck's first adoption did this (its
  ADR 0002) and replaced it within a day (its ADR 0048): two fixes made in its copies never
  reached the kit.

## Consequences

- A new project folder must be added to `kit.json` and to `LicenseHeaderTests.Projects`, both
  explicit lists, so adding a project is a decision.
- The console's publish passes no `IncludeNativeLibrariesForSelfExtract`: ImageSharp is pure
  managed, which is what keeps the console one copyable file (ADR 0009).
- A change in the kit reaches this project through `scripts/sync.ps1`; until then step 0 of
  `scripts/verify.ps1` is red.
