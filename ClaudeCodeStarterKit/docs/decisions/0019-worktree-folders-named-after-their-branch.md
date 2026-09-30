# 0019. A worktree folder the assistant makes is named after its branch

Date: 2026-09-27
Status: accepted 2026-09-27; the desktop app's worktree superseded by 0027

## Context

A task branch lives in its own worktree under the project's `.claude/worktrees/` (ADR 0013), but
nothing said what the folder is called. On 2026-09-27 the `photo` repository held four worktrees
whose folders did not say which task they held: three the desktop app made
(`dreamy-hermann-dd71bc`, `salut-worktree-5c4f5e`, `wizardly-fermat-1c8ce0`) and one a session made
by hand (`share-skills`, for `claude/Share-the-skills-and-the-agent-through-sync`). The owner
reviews a task's diff in its worktree before the commit, and asked, in chat, that a worktree
folder be named after its branch, without the `claude/` prefix, so it can be found.

## Decision

A worktree the assistant makes is created in one command, with the branch:
`git worktree add .claude/worktrees/<Task-name> -b claude/<Task-name> main`. The folder is the
branch name without `claude/`, which is already a valid folder name (letters and hyphens), so
branch and folder read the same in `git worktree list` and in the file explorer.

A worktree the desktop app opened keeps its generated folder: the session runs inside it and the
app tracks it by path. Its branch is still renamed (ADR 0013), so `git worktree list` pairs the
generated folder with the task's name.

## Alternatives rejected

- **The full branch name as a path** (`.claude/worktrees/claude/<Task-name>`). A folder per
  prefix with one level more, for no reader; every worktree the assistant makes starts with the
  same prefix.
- **A short slug** (`share-skills`). What the rule replaces: clear the day it is made, and it
  has to be matched to its branch by hand.
- **Move the app's worktree after the rename** (`git worktree move`). The session's working
  directory would vanish under it, and the app would lose the worktree it cleans up when the
  session is archived.

## Consequences

CLAUDE.md states the command in its Workflow section, and the guide says why. FlatCheck, which
follows the kit, carries the same line and records the rule in its ADR 0050. A rename of the
branch after the worktree exists (the task turned out different) moves the folder too, with
`git worktree move` run from outside it. Existing worktrees are not renamed:
each belongs to the session that made it.
