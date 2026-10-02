# 0002. The layout stays src/ and tests/; kit.json holds this project's values

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

The kit lays a project out as `src/` and `tests/`, and its shared scripts, skills and agent read
everything that differs per project from `kit.json` (the kit's ADRs 0017 and 0018).
ImageVisualCompare already had that layout: `src/ImageVisualCompare.Core`,
`src/ImageVisualCompare.Wpf` and `tests/ImageVisualCompare.Tests`, under one solution. It
publishes one application, the WPF window, and it has no console.

## Decision

The layout stays. `kit.json` holds, as this project's values: the solution
`ImageVisualCompare.sln`; the code folder `src/` and the test folder `tests/`; the test filter
`Category!=Validation` (ADR 0005); the test data folder `tests/testdata/`, with
`IVC_TESTDATA_LARGE` as the override `kit-config.ps1` requires a name for and nobody sets
(ADR 0005); Stryker run from `tests/ImageVisualCompare.Tests/`, whose `stryker-config.json`
names `ImageVisualCompare.Core.csproj`; one publish target, the application, self-contained
for `win-x64`, into `artifacts/<version>/` itself; and, for the `docs-auditor`, `README.md`,
the user documentation, which states behaviour and is not code. The project's own steps for
the kit's skills are in `docs/skills.md`.

## Alternatives rejected

- **Copies of the scripts edited for the project.** FlatCheck's first adoption did this (its
  ADR 0002) and replaced it within a day (its ADR 0048): two fixes made in its copies never
  reached the kit.
- **A single-file publish.** A WPF application is one file only with
  `IncludeNativeLibrariesForSelfExtract`, which unpacks the native WPF libraries at start;
  nothing here needs one file, and it can be added to `release.publish` when something does.

## Consequences

- A new project under `src/` or `tests/` is covered by `kit.json` without an edit, but
  `LicenseHeaderTests` lists its projects explicitly, so a new project is still a decision
  there (ADR 0024).
- A change in the kit reaches this project through `scripts/sync.ps1`; until then step 0 of
  `scripts/verify.ps1` is red.
- `scripts/release.ps1` names the tag after the folder: `imagevisualcompare-vX.Y.Z`.
