# Starter

One paragraph, written by the owner: what the tool does, who uses it, what it is not.
(Kit placeholder: a CLI that prints count, median and robust sigma of a file of numbers.
The sample code exists to show how a requirement, a test and a validation figure connect.)

## Commands

- Gate (what pre-commit, pre-merge-commit and CI run): `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build Starter.sln -c Release --nologo`
- Test: `dotnet test Starter.sln -c Release --nologo`
- One test: `dotnet test Starter.sln --filter "FullyQualifiedName~R001"`
- Format: `dotnet format Starter.sln` (check only: `--verify-no-changes`)
- Run: `dotnet run --project src/Starter.Cli -- <numbers-file>`
- Test data hashes after an intentional fixture change: `scripts/check-testdata.ps1 -Update`
- Design notes a branch touched (the index and Covers paths are a gate step):
  `scripts/check-design.ps1 -Since main`
- Mutation score of Core (minutes, not in the gate): `scripts/mutate.ps1 [-Mutate "**/File.cs"] [-BreakAt 80]`
- Release (verify, version, CHANGELOG, publish; no commit, no tag): `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`
- Take the kit's shared folders and settings keys (or, in the kit, push them to every project):
  `scripts/sync.ps1 [-Check]`

## Layout

- `src/Starter.Core` - all logic. No Console, no UI, no entry point. This is a compile boundary, keep it one.
- `src/Starter.Cli` - thin entry point. `Program.Run(args, stdout, stderr)` is what tests drive.
- `tests/Starter.Tests` - unit (`StatsTests`), black-box acceptance (`AcceptanceTests`),
  property-based (`PropertyTests`), figures (`ValidationTests`), repo rules (`RepoInvariantTests`).
- `tests/testdata/` - fixtures in repo, `manifest.json` with hashes; large files outside, see its README.
- `docs/requirements.md` - the behaviour, with IDs. Source of truth. Code follows it, not the reverse.
- `docs/decisions/` - ADRs. Why things are the way they are, and what was rejected.
- `docs/validation.md` + `validation.json` - acceptance figures; the JSON is read by tests.
- `docs/plans/` - approved plans, one per task. `docs/design/` - per-area notes, read on demand.
- `kit.json` - what the shared scripts, skills and agent need to know about this project
  (solution, code and test folders, test data, what a release publishes, what the auditor may
  also read, where the kit is). `docs/skills.md` - this project's additions to the kit's skills,
  one section per skill. `scripts/`, `.claude/skills/`, `.claude/agents/`,
  `.claude/rules/kit/` and the process keys of `.claude/settings.json` are the kit's, the same
  here.
- `.claude/rules/kit/process.md` holds the process rules; this file holds the project's own. The
  explanations behind them (why, alternatives, how to adopt) are in `GHID.md`, kept once, in the
  kit folder `ClaudeCodeStarterKit` beside this project in the `photo` repository. For humans;
  not needed in a session. A rule added that needs a "why" gets it there, in the same task.

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are this project's own:

- **Core has no UI.** `Starter.Core` never references Console, WPF, file dialogs or anything with
  a UI.

## Read before touching

- `docs/design/README.md` lists the areas that have a design note. Read the note before editing that area.

## Style

- `.editorconfig` and the analyzers enforce style at build time; do not restate style here.
- Nullable enabled, warnings are errors, `AnalysisLevel` latest-recommended. Fix, do not suppress,
  unless the suppression carries a comment with the reason.
