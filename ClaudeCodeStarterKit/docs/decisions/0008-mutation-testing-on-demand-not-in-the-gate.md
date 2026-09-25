# 0008. Mutation testing on Core, on demand, never in the commit gate

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Coverage says a line ran under a test; it does not say a test would notice the line going
wrong. For numeric code a flipped sign or a wrong constant yields a plausible number, which is
exactly what example-based tests miss. The owner asked for mutation testing after reading about
it in the guide. Measured on the kit's sample Core (2 files, 18 tests): 35 mutants generated,
26 tested, 22 killed, 3 survived, 1 timeout, score 88.5 %, 3 min 42 s including the build.

## Decision

Stryker.NET is a versioned local tool (`.config/dotnet-tools.json`, 4.16.0), configured in
`tests/Starter.Tests/stryker-config.json` to mutate only the Core project, and run through
`scripts/mutate.ps1`, which prints the score and every survivor with file, line and mutation.
The `/mutate` skill turns survivors into proposed tests carrying a requirement ID, or declares
them equivalent with a reason. It is never part of `scripts/verify.ps1`.

Stryker 5.0.0 is pinned out: `dotnet tool install` fails on SDK 9.0.318 with a missing
`DotnetToolSettings.xml` in the package. Re-try the upgrade with a newer SDK.

## Alternatives rejected

- **In the commit gate.** One test-suite run per mutant; minutes on the sample, tens of minutes
  on a real project. The gate must stay under twenty seconds or it gets bypassed.
- **Mutate every project.** The CLI and any UI are thin adapters; mutants there measure glue,
  not logic, and slow the run.
- **A coverage threshold instead.** Cheaper, but it is the weaker measure for the code that
  matters here.
- **Global tool install.** Unversioned, differs per machine; the local manifest restores the same
  version everywhere.

## Consequences

The survivors on the sample code are the worked example in the guide: two removed null guards
that no requirement mentions, one exception message that no requirement fixes. A score of
80 to 90 % on Core is the realistic band; 100 % is not a target. A weekly CI job with
`mutate.ps1 -BreakAt 80` is the natural next step when a project grows.
