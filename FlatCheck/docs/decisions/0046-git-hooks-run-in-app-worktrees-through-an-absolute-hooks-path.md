# 0046. Git hooks run in app worktrees through an absolute core.hooksPath

Date: 2026-09-25
Status: accepted 2026-09-25
Supersedes: 0007 in part (the fresh-clone hooks command)

## Context

FlatCheck is opened in the Claude desktop app, so every session runs in a worktree under
`FlatCheck/.claude/worktrees/` (ADR 0044). The app copies `core.hooksPath` into each worktree's
`config.worktree`, resolving the relative `.githooks` against `FlatCheck/` instead of the git
root: `E:\Photografie\Code\FlatCheck\.githooks`, which does not exist. No hook ran in any of the
five worktrees on 2026-09-25; commit c19735c passed without check-staged, verify or commit-msg,
which were replayed by hand afterwards. The measurements are in the kit's ADR 0016.

## Decision

`.git/config` of `photo` carries `core.hooksPath = E:/Photografie/Code/.githooks`, written by the
kit's `install-hooks.ps1`, so the app copies a right path into every new worktree. The five
existing worktrees had their `config.worktree` set to the same path. This replaces the command
in the last paragraph of ADR 0007: a fresh clone runs the kit's `install-hooks.ps1` once, not
`git config core.hooksPath .githooks`.

## Alternatives rejected

- **A SessionStart hook in FlatCheck's `.claude/settings.json`.** It races the app, which writes
  the override as the session starts, and fixes FlatCheck only.
- **Open the monorepo root in the app.** FlatCheck's CLAUDE.md and settings would not load.

## Consequences

A worktree runs the hooks in the shared checkout, so a change to `.githooks/` takes effect after
its merge into `main`. Check after a new app session:
`git config --show-scope --get-all core.hooksPath` shows the absolute path in both scopes.
