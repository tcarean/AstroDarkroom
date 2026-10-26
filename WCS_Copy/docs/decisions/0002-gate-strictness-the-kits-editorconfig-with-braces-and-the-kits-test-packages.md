# 0002. Gate strictness: the kit's `.editorconfig` with the braces rule on, the kit's test packages, coverlet dropped

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

The project had no `Directory.Build.props`, no `.editorconfig`, no analyzers beyond the SDK's
defaults, and four test packages with versions in the test csproj (`coverlet.collector`
6.0.0, `Microsoft.NET.Test.Sdk` 17.8.0, `xunit` 2.5.3, `xunit.runner.visualstudio` 2.5.3), no
lock files. Measured on 2026-10-26 in a scratch copy under DarksCheck's `Directory.Build.props`
(`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel latest-recommended`,
`GenerateDocumentationFile`) and its `.editorconfig`: the app code had 20 findings, all
IDE0011 (braces on one-line `if`s; `FitsHeader` 12, `CopyWcsCli` 3, `Options` 3,
`VersionInfo` 1, `WcsMetadataKeys` 1); the tests 101 (CA1707 62, CA1861 19, IDE0011 10,
CA1305 8, CA1310 2). ImageVisualCompare declined the braces rule at 38 findings because a
style pass over its code base was a task of its own; DarksCheck has it on since it was born
from the kit.

## Decision

- `Directory.Build.props` is DarksCheck's: warnings as errors, style enforced in the build,
  analyzers at `latest-recommended`, deterministic build, central package management with lock
  files, the documentation file generated with CS1591 muted (the kit's ADR 0035),
  `RuntimeIdentifiers win-x64` on the projects that ship (the kit's ADR 0034).
- The kit's full `.editorconfig`, `csharp_prefer_braces` included: the 30 one-line `if`s were
  given braces by `dotnet format` in the first commit; `dotnet format --verify-no-changes` is
  then clean.
- The tests: CA1707 (underscores in test names) is silenced in the test csproj, as in the
  siblings; `InvariantGlobalization` is on in the test project, which also silences CA1305 and
  CA1310 (culture-free formatting is then a given) and matches the console's own switch; the
  19 CA1861 constant arrays became collection expressions.
- `Directory.Packages.props` holds the kit's current test packages (`Microsoft.NET.Test.Sdk`
  18.10.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.5); `coverlet.collector` is dropped:
  nothing read a coverage file and the kit has no coverage step.

## Alternatives rejected

- **The whitespace rules only, as ImageVisualCompare.** Thirty findings over five files is one
  format pass, not a task.
- **Keeping coverlet with a "why".** There is none: no step reads its output.
- **Silencing CA1305 by `NoWarn` in the tests.** `InvariantGlobalization` says the same thing
  truthfully and the console already relies on it.

## Consequences

- Every commit builds with zero warnings; a new finding is fixed, not suppressed, unless the
  suppression carries its reason.
- The test package versions move with the kit's; a bump is a `Directory.Packages.props` edit and
  a restore that rewrites the lock files, committed together.
