# 0003. The gate enforces whitespace and warnings as errors; style analyzers and Nullable stay off

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

The kit's `Directory.Build.props` turns on `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`,
`AnalysisLevel` latest-recommended and `Nullable`, and its `.editorconfig` demands a code style.
AstroPhotoAnalyzer on 2026-10-02, measured: a Release build with 0 warnings; `Nullable` disabled
in the core, the console and the window and enabled in the two test projects; no
`.editorconfig`. A build at `AnalysisLevel` latest-recommended raised 129 analyzer warnings
(CA1861, CA2263, CA1859 the most frequent), and `Nullable` enabled in the three application
projects raised 139 more (85 in the core, 5 in the console, 49 in the window), on code with 270
green tests and seven golden files.

## Decision

`scripts/verify.ps1` builds with every warning as an error, on the command line, so a build in
the IDE stays lenient. `dotnet format --verify-no-changes` runs against an `.editorconfig` that
holds whitespace rules only (LF, four spaces, a final newline, no trailing space) plus `IDE0005`
(unused usings). Code style analyzers, `AnalysisLevel`, `EnforceCodeStyleInBuild` and `Nullable`
in the application projects are not enabled; each is a task of its own, with its own CHANGELOG
line. Reproducibility comes from the kit in full: `global.json` pins the SDK 9.0.318 (the one
that built the project before, and which builds `net8.0`), `Directory.Packages.props` holds every
package version once, every project has a `packages.lock.json`, builds are `Deterministic`.

The one `dotnet format` pass of 2026-10-02 changed 10 files: object initialisers wrapped one
member per line, the two `case` blocks of `IccProfileInfo` re-indented, four unused usings
removed. No behaviour changed: the 270 tests and the golden files are unmoved.

## Alternatives rejected

- **The kit's strictness as it is.** 268 findings on the first build; the migration would have
  become a style rewrite with no test behind it, which the kit's guide warns against.
- **Start the gate without warnings as errors.** The guide offers it for a project with warnings;
  this one has none.
- **CRLF.** Every source file is LF in the repository and on disk (the kit's ADR 0014, the root
  `.gitattributes`); the project's `.gitattributes` and `.editorconfig` say so again.

## Consequences

- A warning anywhere in the five projects fails the gate.
- `dotnet format` reflows object initialisers: new code with several members on one line is
  reformatted at the next pass, so run it before `scripts/verify.ps1`.
- Enabling `Nullable` in the core is the most valuable of the deferred tasks: the readers and
  `IccProfileInfo` return null on purpose in many places.
