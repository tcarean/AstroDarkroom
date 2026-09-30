# 0003. One verify script is the gate, run from pre-commit, the Claude Code hook and CI

Date: 2026-09-24
Status: accepted 2026-09-24; the list of steps superseded by 0024

## Context

Rules in CLAUDE.md are followed most of the time. A gate that is skipped one time in twenty still
lets a red build into `main`. Three separately written gates (a git hook, a CI YAML, a rule for
the assistant) drift apart within months.

## Decision

`scripts/verify.ps1` is the only definition of "green": test data manifest, restore, format
check, build with warnings as errors, tests. The git pre-commit hook, the Claude Code
`PreToolUse` hook on `git commit`, and both CI files call that script and nothing else.

## Alternatives rejected

- **Rely on CLAUDE.md rule 4 alone.** Probabilistic.
- **CI only.** Catches the problem after the commit exists; the model then makes "fix build"
  commits that bury the history.
- **A PostToolUse hook building after every edit.** Good when the build is under ten seconds;
  left out of the kit as a default because it slows large solutions. Add it per project.

## Consequences

Committing costs one full verify run. `-SkipTests` exists for the edit loop, never for the
commit. The script is Windows PowerShell 5.1 compatible on purpose, because `pwsh` is not
installed on every machine.
