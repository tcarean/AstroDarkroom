# 0039. The version lives in Directory.Build.props and flatcheck.py, is bumped only by the owner, and is scrubbed before every whole-output comparison

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/process.md ("Versioning"); the decision
itself dates from 2.0.0, when the version moved out of the csproj. Every run prints
`flatcheck <version>` as its first line - every mode, `--help`, failed runs - so a pasted report
says which version produced it; both applications print it from `RunReport.WriteBanner`. Since
2.0.0 there are three assemblies: `VersionInfo` reads the one it lives in, Core, but the console
exe and the WPF exe carry their own, which is what Windows shows in file properties. Two places
to edit means, sooner or later, two different values.

## Decision

`<Version>` (and `<Copyright>`) live in `Directory.Build.props`, above all projects, and every
project inherits them; `FlatCheck.Core/Version.cs` reads `AssemblyInformationalVersion` and trims
any `+sourcerevision` suffix, with no literal. The Python side carries `__version__` in
`flatcheck.py`. The owner bumps the version by hand, per feature or fix, choosing the increment;
it is never bumped on an assistant's initiative. The banner is scrubbed to `flatcheck <VERSIUNE>`
before any whole-output comparison - in `GoldenOutputTests.RunCli` (as absolute paths already
are) and in `crosscheck.py`'s `norm()` - and version agreement is checked once, up front, in
`check_versions()`.

## Alternatives rejected

- **`<Version>` in a csproj.** Three assemblies would drift; `VersionTests` checks that no project
  overrides `<Version>` or `<Copyright>`.
- **A literal in `Version.cs`.** A second place to edit.
- **A `--version` flag.** Redundant with the banner on line 1 of every run.
- **Golden files carrying the real version.** Every bump would move every golden file and turn
  all 43 cross-check scenarios into diffs - 42 diffs to learn one thing.

## Consequences

- Bumping the version breaks nothing when both places are bumped. When only one is bumped,
  exactly one test fails, `R097_PythonReportsTheSameVersionAsTheCSharp`, and `crosscheck.py` prints
  one `[VERSIUNE]` line and exits 1. Removing `<Version>` from `Directory.Build.props` or
  re-adding it to a csproj fails the plumbing tests in `VersionTests`. If the banner stops being
  printed, the golden files fail, because they carry a placeholder line the output no longer
  produces.
- The scrubbing does not weaken coverage: `VersionTests` asserts the banner's exact content and
  its position on line 1, for every mode, for `--help`, and for failed runs.
- The WPF app's saved `Rezultat testare.txt` opens on the same two lines as a console run, and a
  bump moves both applications at once.
- If a change seems to warrant a bump, say so and let the owner decide.
- Guards: `VersionTests`, `R097_PythonReportsTheSameVersionAsTheCSharp`, `GoldenOutputTests`,
  `crosscheck.py`'s `check_versions()`.
- Source note: docs/design/process.md.
