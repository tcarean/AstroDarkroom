# 0001. Adopt the starter kit process for the suite's launcher and release

Date: 2026-10-25
Status: accepted 2026-10-25

## Context

On 2026-10-24 the owner decided to ship the five astrophotography tools of the `photo`
repository as one suite, Astro Darkroom, with a small WPF launcher
(`ClaudeCodeStarterKit/docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`). The release
mechanism, the kit's `release.ps1` with components and a zip, exists since the kit's ADR 0036 and
needs a kit project to run from: a `kit.json` with the publish list, a `<Version>`, a
`CHANGELOG.md`. The other five projects are kit projects already; LightsCheck's first commit
(`e857e2a`) is the model of what a copy of the kit keeps and drops.

## Decision

AstroDarkroom is a kit project, a copy of `ClaudeCodeStarterKit` renamed with `rename.ps1`, in
the kit's layout (`src/`, `tests/`, the kit's strictness: nullable, warnings as errors,
`AnalysisLevel` latest-recommended), with the shared folders synced byte for byte and `kit.json`
pointing at `../ClaudeCodeStarterKit`. Its `<Version>` is the suite's version and its `/release`
is the suite's release (ADR 0002). Dropped from the copy: the sample code (`Stats`, `NumberFile`,
the console project, their tests, fixtures, R-001..R-004 and V-001; the launcher has no measured
figures), the kit's own tests of its scripts, hooks and guide (`GitHookTests`, `HookTests`,
`SyncTests`, `ReleaseTests`, `MergePublishTests`, `DecisionsCheckTests`, `DesignCheckTests`,
`ScriptRunner` and the kit-only checks of `RepoInvariantTests`), the kit's ADRs and plans, the
guide, `monorepo/`, the generator prompt, the CI templates and the kit-only scripts, as the kit's
README lists them. Added, as LightsCheck has them: `LicenseHeaderTests` (R-005, every source file
carries the GPL notice, the kit's test files that came without one included) and `VersionTests`
over a `VersionInfo` in the core. The window is a thin adapter over the core and has no test
project (MindfulnessReminder's ADR 0008 is the precedent): every rule, the catalogue, where an
app is, the plan of a start and the reading of `release.json`, is in `AstroDarkroom.Core` and
tested there; the window binds and calls `Process.Start`.

## Alternatives rejected

- **A launcher inside one of the five projects.** Its version would be that project's, and a
  release of the suite would be a release of that tool; the suite needs a version and a
  CHANGELOG of its own.
- **A script in the kit, no project.** The kit's rule "Shared folders" keeps `scripts/` the
  same everywhere; the publish list is a project value and belongs in a `kit.json`.
- **Keeping the kit's script tests.** They test the kit's files, which are the kit's and tested
  there (the kit's ADR 0025), and take tens of seconds per run.
- **A test project for the window.** The window has one button per row and an About box; what
  it shows comes from the core, where it is tested. A rendered smoke test would cost a
  Windows-only project for no rule of its own.

## Consequences

`scripts/verify.ps1` is the gate; `RepoInvariantTests` ties `docs/requirements.md` to the
`R###_` test names. The kit's own ADRs are cited as "the kit's ADR NNNN" and not repeated
(the kit's ADR 0025). A change under `src/AstroDarkroom.Wpf/` is checked by starting the
launcher from a published suite folder, as the plan's 5.4 does.
