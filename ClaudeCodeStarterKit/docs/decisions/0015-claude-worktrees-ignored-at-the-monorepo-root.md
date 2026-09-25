# 0015. In a monorepo, Claude Code worktrees are ignored at the root; the kit keeps its own line

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

The Claude desktop app and Claude sessions create git worktrees under `.claude/worktrees/` in the
folder they are started in, which in a monorepo is the project folder (ADR 0004). The kit's
`.gitignore` ignores `.claude/worktrees/`, but a project that adopted the kit without copying that
line, FlatCheck in `photo`, made `git status` in the shared checkout list
`?? FlatCheck/.claude/worktrees/` permanently, and every other project with a `.claude/` folder
would do the same. A pattern with a slash in the middle is anchored to the folder of its
`.gitignore`, so the kit's line copied unchanged to the root would match only
`<root>/.claude/worktrees/`.

## Decision

In a monorepo, the root `.gitignore` carries `**/.claude/worktrees/`, which covers every project
folder, adopted or not, and the root. The kit's own `.gitignore` keeps `.claude/worktrees/`,
because a project made from the kit may be a standalone repository with no root above it.

## Alternatives rejected

- **One line per project `.gitignore`.** Every project has to remember it; a project that has not
  adopted the kit never gets it.
- **Remove the line from the kit.** A standalone project made from the kit would show its
  worktrees.
- **The global gitignore (`core.excludesFile`).** Not versioned: a fresh clone or another machine
  shows the folder again.

## Consequences

`monorepo/README.md` and the monorepo table of `GHID.md` list the root line beside the hooks and
CI. The pattern is redundant with the kit's line inside the kit folder, which is harmless. In
`photo` the decision is FlatCheck ADR 0043.
