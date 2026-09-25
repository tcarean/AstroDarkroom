# 0003. The gate enforces whitespace and warnings as errors; style analyzers and Nullable stay off

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

The kit's `Directory.Build.props` turns on `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`,
`AnalysisLevel` latest-recommended and `Nullable`, and its `.editorconfig` demands file-scoped
namespaces. FlatCheck on 2026-09-25: 20 000 lines, block namespaces everywhere (0 of 16 core
files file-scoped), `Nullable` disabled in the three application projects and enabled in the two
test projects, 9 warnings (8 nullable in tests, 1 `xUnit2000`), and `dotnet format
--verify-no-changes` reporting whitespace differences in 12 files. Turning on the analyzers would
have produced hundreds of findings on code that has 1188 green tests and a numeric parity check
against `flatcheck.py`.

## Decision

The gate builds with `-warnaserror` on the command line (so an IDE build stays lenient) after the
nine warnings were fixed, and runs `dotnet format --verify-no-changes` against an `.editorconfig`
that holds whitespace rules only (LF, 4 spaces, final newline, trimmed trailing space) plus
`IDE0005` (unused usings). Code style analyzers, `AnalysisLevel`, `EnforceCodeStyleInBuild` and
`Nullable` in the application projects are not enabled; each is a task of its own with its own
CHANGELOG line and, if it changes a public shape, its own ADR. Reproducibility is taken from the
kit in full: `global.json` pins the SDK (9.0.318, which builds net8.0), `Directory.Packages.props`
holds every package version once, `packages.lock.json` per project, `Deterministic` builds.

The one-time `dotnet format` pass changed 33 files: whitespace, unused usings removed, and object
initialisers wrapped one member per line by the formatter's default. No behaviour changed; the
1188 tests and the golden files are unmoved.

## Alternatives rejected

- **The kit's strictness as-is.** Hundreds of analyzer findings on the first build; the
  migration would have become a style rewrite with no test behind it (the guide's warning on
  `/simplify` over a whole project applies).
- **Start verify without `-warnaserror` and enable it later.** The guide offers this; with nine
  warnings, all in tests and each a one-token fix, there was no reason to defer.
- **CRLF as in the kit.** Every source file in FlatCheck is LF; `.gitattributes` now says so
  (`text=auto eol=lf`), so a checkout on another machine matches `.editorconfig`.

## Consequences

`verify.ps1` takes about 3 minutes on this project (build 20 s, tests 2 min 20 s), against 15 s on
the kit; that is the cost of every commit and every Claude commit attempt. A warning anywhere in
the five projects now fails the gate. `dotnet format` reflows object initialisers; new code that
puts several members on one line will be reformatted at the next `dotnet format`, so run it
before verify. Enabling `Nullable` in `FlatCheck.Core` remains the most valuable of the deferred
tasks: the reader and the lens catalog return null on purpose in many places.
