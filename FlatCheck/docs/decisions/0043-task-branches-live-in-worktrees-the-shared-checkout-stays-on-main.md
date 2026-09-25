# 0043. Task branches live in worktrees; the shared checkout stays on main

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

FlatCheck is a folder of the `photo` monorepo, whose main checkout `E:\Photografie\Code` is where
the owner merges into `main`, and where several Claude sessions run at once, each on its own task.
On 2026-09-25 a FlatCheck session created its task branch with `git switch -c` in that checkout;
any merge run there meanwhile would have landed on the task branch instead of `main`. The kit
names branches `claude/<Task>` and deletes them once merged (the kit's ADR 0013); the owner asked
for the worktree rule to be recorded in FlatCheck as well as in the kit.

## Decision

A FlatCheck task branch lives in its own worktree: the one the desktop app opened for the
session, or one made with `git worktree add` under `FlatCheck/.claude/worktrees/` (or the kit's,
for a change that spans both). The shared checkout stays on `main`, for the owner's merges. The
naming, the deletion after the merge and the rest are the kit's ADR 0013, not repeated here.

## Alternatives rejected

- **`git switch -c` in the shared checkout.** One folder has one checked-out branch; the owner's
  merge, editors and builds would follow whatever branch a session left there.
- **Only the kit's ADR.** The owner asked for the rule to be found from FlatCheck's own
  decisions too; this record points to the kit's reasoning instead of copying it.

## Consequences

CLAUDE.md states the rule under Workflow. A commit from a worktree runs the git hooks with
`GIT_DIR` set; `scripts/check-staged.ps1` finds the repository root by walking up to `.git`
since the same day, and `CheckStagedTests` covers it. A worktree takes a full build on its first
verify (about two minutes here) because `bin/` and `obj/` are per folder.
