# 2026-09-25 Git hooks in the Claude desktop app's worktrees

## Context

Commit c19735c, made in an app worktree of `photo`, passed without check-staged, verify or
commit-msg. The owner asked to find what writes the per-worktree `core.hooksPath`, measure it on
a fresh worktree and fix it. The measurements and four decisions were presented in chat; the
owner answered "merg pe recomandarile tale". That answer is the approved plan below; ADR 0016
(and FlatCheck ADR 0046) record the decision.

## Measured before changing anything

- Writer: the desktop app. `%LOCALAPPDATA%\Claude\logs\main.log`, at every session start:
  "Configured worktree hooks path to E:\Photografie\Code\FlatCheck\.githooks (from base repo
  config)"; it runs `git config --type=path --get core.hooksPath` with cwd `FlatCheck`. Nothing in
  the kit, FlatCheck or `.claude/settings.json` writes `config.worktree`.
- `git hook run commit-msg` on a two-line message: an app worktree, "cannot find a hook named
  commit-msg"; the same worktree with the relative or the absolute root path, rejected.
- `--type=path` returns an absolute value unchanged.
- git 2.55 `worktree add` copies the current worktree's `config.worktree`: added from an app
  worktree, the new one inherits the wrong path; added from the shared checkout, it has no
  override and runs the hooks. Without the copied `core.longpaths`, the checkout fails on the long
  ADR names.

## Steps

1. `install-hooks.ps1`, monorepo branch: absolute path of the main checkout's `.githooks/`; warn
   when a `config.worktree` overrides it. Standalone unchanged.
2. Run it: `.git/config` of `photo` gets `E:/Photografie/Code/.githooks`; `.githooks/` unchanged.
3. Set the same path in the five existing `config.worktree` files; `git hook run commit-msg`
   rejects the two-line message in each, in the shared checkout and in a worktree added from one.
4. ADR 0016 here, ADR 0046 in FlatCheck; `GHID.md`, `README.md`, `monorepo/README.md`, CHANGELOG.
5. Verify green in the kit and FlatCheck.
6. After the merge, at the next app session: its `config.worktree` shows the absolute path.
