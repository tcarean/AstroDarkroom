# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in `Directory.Build.props`.

## [Unreleased]

### Added

- Kit: solution with Core, Cli and Tests; verify gate; requirements, decisions, validation, plans
  and design scaffolding; Claude Code settings, hooks and skills; CI for GitHub and Bitbucket.
- Deterministic backstops for three prose rules (ADR 0005): `check-staged.ps1` refuses a commit
  touching `src/` without a CHANGELOG line; `RepoInvariantTests` ties implemented requirements to
  `R###_` test names both ways; a Stop hook refuses to end a turn that edited code until verify
  ran green and CHANGELOG changed.
- `GHID.md`: the reference guide (Romanian). Started as an export of the claude.ai document; since
  2026-09-25 it is the master copy, edited directly. Added: gate versus hook, when each trigger
  runs, what `/wrap-up` does and when to use it, migrating an existing project in three layers,
  what an ADR is, when it counts as accepted, and how to supersede one. ADR template gains a
  `Supersedes:` line and a dated `accepted`; `/new-feature` waits for the owner's yes before
  planning, `/wrap-up` flags an ADR still `proposed` under code about to be committed.
- Mutation testing (ADR 0008): Stryker.NET 4.16.0 as a versioned local tool, `stryker-config.json`
  targeting Core, `scripts/mutate.ps1` printing score and survivors, `/mutate` skill. Not in the
  gate. Baseline on the sample code: 26 mutants tested, score 88.5 %, 3 min 42 s.
- Rule 13 (ADR 0007): one-line English commit messages without trailers; the assistant proposes
  commits and merges and asks each time who runs them. Enforced by a `commit-msg` git hook
  (installed beside pre-commit) and by `permissions.ask` for commit, merge, push and rebase.
- Monorepo support: the pre-commit hook verifies only the project folders touched by the staged
  files; `install-hooks.ps1` detects the layout; root CI templates under `monorepo/`.
- `/docs-health` now launches the `docs-auditor` subagent (`.claude/agents/`, ADR 0009): clean
  context, never opens `src/`, fixed report format ending with the files it opened. First run on
  the kit: 17 findings, none from `src/`.

- `/release` skill over `scripts/release.ps1` (ADR 0010): from `main` with a clean tree, SemVer
  proposed from the `[Unreleased]` sections, verify, version bump, dated CHANGELOG section,
  publish to `artifacts/<version>/`; commit and tag proposed, never run by the script. Tag is
  `vX.Y.Z` standalone, `<folder>-vX.Y.Z` in a monorepo.

### Changed

- ADRs 0001 to 0005 carry the date of acceptance in their status, as the template requires.
- CLAUDE.md rule 13 states the 72-character limit; the mutate command lists `-BreakAt`.
- Guide brought in step with the kit on 2026-09-25: structure table (check-staged, hooks,
  install/rename, monorepo/, GHID.md, `ask` list), all 13 rules, trigger table with `ask` and
  commit-msg, wrap-up step 8, CHANGELOG rule as enforced, `/mutate` and `/release` in the
  start-up steps, agents in the monorepo note, layer 1 of migration.
- Guide: `/code-review` runs in the same session (it is already a background subagent with its
  own context), not in a second one; branch form `main...task/x`; `/wrap-up` suggests it before
  proposing a merge. Findings by category: only correctness findings need a failing test first.
  `/simplify` documented: per-session diff, report first then `--fix`, whole project only per
  area as separate tasks.

### Removed

- `docs/ai-workflow.md` (ADR 0006): it duplicated CLAUDE.md and `GHID.md` and nothing read it in
  the working loop. CLAUDE.md holds the rules, `GHID.md` the explanations, kept once in the kit;
  each project points to it instead of carrying a copy.

## [0.1.0] - 2026-09-24

Placeholder first version. Delete this section when the real project starts.
