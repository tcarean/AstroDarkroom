# 0043. Claude Code worktrees are ignored from the monorepo root, not from FlatCheck's .gitignore

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

The Claude desktop app and Claude sessions create git worktrees under `.claude/worktrees/` in the
folder they are started in. `git status` in the shared checkout of `photo` always listed
`?? FlatCheck/.claude/worktrees/`: FlatCheck's `.gitignore` had no entry for it, the root one
neither. The kit's `.gitignore` already ignores `.claude/worktrees/`, which is why
`ClaudeCodeStarterKit/.claude/worktrees/` never showed. AstroPhotoAnalyzer and WCS_Copy already
have a `.claude/` folder and would show the same line after their first worktree session. The
header of `FlatCheck/.gitignore` says it holds what comes "in addition to the repository root's
.gitignore", so patterns common to every project belong at the root. A pattern with a slash in
the middle is anchored to the folder of its `.gitignore`, so `.claude/worktrees/` at the root
would match only `<root>/.claude/worktrees/`.

## Decision

The root `.gitignore` carries `**/.claude/worktrees/`, which matches the folder in every project
and at the root itself. `FlatCheck/.gitignore` has no entry for it. The kit keeps its own line
(kit ADR 0015).

## Alternatives rejected

- **An entry in `FlatCheck/.gitignore`.** Fixes FlatCheck only; every other project with a
  `.claude/` folder repeats the problem at its first worktree session.
- **`.claude/worktrees/` at the root.** Anchored to the root, matches none of the project folders.
- **The global gitignore (`core.excludesFile`, where `**/.claude/settings.local.json` already
  is).** Not versioned: a fresh clone or another machine shows the folder again.

## Consequences

Checked with `git check-ignore -v --no-index FlatCheck/.claude/worktrees/x/`, which names the
root `.gitignore`. If FlatCheck ever leaves the monorepo, its `.gitignore` needs the kit's
`.claude/worktrees/` line back.
