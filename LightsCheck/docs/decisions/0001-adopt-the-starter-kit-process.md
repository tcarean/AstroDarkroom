# 0001. Adopt the ClaudeCodeStarterKit process; the kit's own tests stay in the kit

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

LightsCheck starts on 2026-10-09 from the analysis of the Veil session of 2026-10-06 (87 lights,
read-only, with Python scripts), after which the owner asked for a C# WPF application that
repeats that analysis on any session, "implementing the kit the way FlatCheck does": the same
licence, versioning, dark theme, "Despre" window and window layout. The kit's README lists the
steps of a new project, and FlatCheck is the worked example of an adoption.

## Decision

LightsCheck is a copy of the kit (`rename.ps1 -Name LightsCheck`), kept in the kit's layout
(`src/`, `tests/`, the kit's `Directory.Build.props` strictness: nullable, warnings as errors,
`AnalysisLevel` latest-recommended, .NET 9), with the kit's scripts, skills, agent, rules file
and settings keys synced byte for byte and `kit.json` pointing at `../ClaudeCodeStarterKit`.
The kit's tests of its own scripts, hooks and guide (`GitHookTests`, `HookTests`, `SyncTests`,
`ReleaseTests`, `DecisionsCheckTests`, `DesignCheckTests`, `ScriptRunner`, and the kit-only
checks of `RepoInvariantTests`) are not carried: those files are the kit's and are tested
there, as FlatCheck does. The sample code (`Stats`, `NumberFile`, their fixtures and V-001) is
replaced by the first real feature in the same task.

## Alternatives rejected

- **FlatCheck's root layout (`FlatCheck.Core/` beside `FlatCheck/`).** FlatCheck kept it because
  it predated the kit (its ADR 0002); a new project has no such history, and the kit's scripts
  assume `src/` and `tests/`.
- **FlatCheck's .NET 8 and relaxed analyzers.** FlatCheck turned them off because 20 000
  existing lines could not be made strict in one task (its ADR 0003); new code can be.
- **Keeping the kit's script tests.** They run the kit's hooks against the git hooks that the
  README says to delete from a project, take tens of seconds per run, and would test a copy.

## Consequences

`scripts/verify.ps1` is the gate; `RepoInvariantTests` ties `docs/requirements.md` to the
`R###_` test names. The kit's own ADRs are cited as "the kit's ADR NNNN" and not repeated
(the kit's ADR 0025). The licence notice is required on every source file as in FlatCheck
(`LicenseHeaderTests`), including the kit's test files that came without one.
