# 0016. In a monorepo, core.hooksPath is the absolute path of the main checkout's .githooks

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

`install-hooks.ps1` set `core.hooksPath = .githooks` in a monorepo (ADR 0004). git resolves a
relative value against the root of the working tree that runs the hook, so it is right in the
shared checkout and in any worktree. But the Claude desktop app, when it creates a worktree for
a session, reads `git config --type=path --get core.hooksPath` in the folder the app was opened
on, which in a monorepo is the project folder, resolves the relative answer against that folder
instead of the git root, and writes the result into the worktree's `config.worktree`, which
wins over `.git/config`. Its log says so: "Configured worktree hooks path to
E:\Photografie\Code\FlatCheck\.githooks (from base repo config)". That folder does not exist,
so in all five app worktrees of `photo` on 2026-09-25 no hook ran: `git hook run commit-msg`
answered "cannot find a hook named commit-msg", and commit c19735c went in without check-staged,
verify or the one-line check. git 2.55 then copies the current worktree's `config.worktree` into
every worktree added from it, so a worktree made by hand from inside an app session (ADR 0013)
inherited the same wrong path. A worktree added from the shared checkout had no override and ran
the hooks.

## Decision

In a monorepo, `install-hooks.ps1` sets `core.hooksPath` to the absolute path of the main
checkout's `.githooks/` (the first entry of `git worktree list`), for `photo`
`E:/Photografie/Code/.githooks`. An absolute value comes back unchanged from `--type=path`, so
the app copies a right path into each new worktree, and git's copy passes it on. The script
warns when a `config.worktree` hides the value it wrote. Standalone repositories keep the
relative `scripts/hooks`: there the app's folder is the git root, so its resolution is right.

## Alternatives rejected

(Restated 2026-09-30: the desktop app's worktrees, the reason given above, are no longer used
in `photo` (ADR 0027). The path stays absolute because every worktree, a hand-made one
included, then runs the main checkout's `.githooks/`, which only a merge into `main` changes;
a relative `.githooks` would make each worktree run its own branch's copy, so a branch could
change the gate its own commits pass. A standalone repository keeps the relative
`scripts/hooks`, whose worktrees do run their branch's copy.)

- **A SessionStart hook in `.claude/settings.json` that removes the override.** The app writes
  `config.worktree` in the same second the session starts, so the hook races it; and only the
  projects that carry the hook would be fixed.
- **Open the monorepo root in the app.** The relative path would resolve right, but worktrees
  would move to `<root>/.claude/worktrees/`, and Claude Code would load no project's CLAUDE.md
  (ADR 0004; `monorepo/README.md`: never in the git root).
- **`install-hooks.ps1` repairs every `config.worktree` it finds.** Fixing the source is enough;
  the worktrees that existed before the fix were a one-off repair by hand.
- **Keep the relative value and wait for the app to be fixed.** Until then no hook runs in any
  app worktree, and nothing says so.

## Consequences

Every worktree runs the main checkout's copy of the hooks, not its own branch's: a hook change
takes effect after the merge, which the app's override already forced. The setting names a
drive and folder, so a clone elsewhere runs `install-hooks.ps1` once (from the kit) instead of
`git config core.hooksPath .githooks`; moving the clone means running it again. Worktrees made
before the fix keep their wrong `config.worktree` until repaired with
`git config --worktree core.hooksPath <root>/.githooks`, which the script's warning prints. On
2026-09-25 the five worktrees of `photo` were repaired and `git hook run commit-msg` rejected a
two-line message in each, in the shared checkout and in a worktree added from inside one. Still
to confirm on the next app session: its `config.worktree` carries the absolute path. In `photo`
the decision is FlatCheck ADR 0046.
