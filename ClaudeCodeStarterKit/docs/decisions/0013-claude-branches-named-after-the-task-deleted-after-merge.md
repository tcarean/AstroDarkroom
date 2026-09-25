# 0013. Branches are named claude/ plus their task and deleted once merged

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

The kit said "branch per task" but not how a branch is named or what happens to it after the
merge. A session the desktop app starts in a worktree gets a generated branch name, chosen before
the task exists: `claude/` plus two random words and a hex suffix (`claude/nice-elbakyan-ae7d04`).
On 2026-09-25 the `photo` repository held three such branches at once, and none of the names said
which task it held; the same day a merge left one of them in `main`'s history for good, as the
message `Merge branch 'claude/wizardly-fermat-1c8ce0'`. The owner asked, in chat, for branch
names that reflect the task, always in English, and allowed the assistant to delete a branch once
it has been merged into `main`. In a second message the same day, the owner asked that the name
always start with `claude/`.

## Decision

- A branch is named `claude/` followed by its task, in English, like a commit subject: an
  imperative phrase, words joined by hyphens, first letter capital. The owner's example, with the
  prefix: `claude/Document-the-console-extension-contract-and-goldens`.
- A branch that starts with a generated name is renamed with `git branch -m claude/<Task-name>`
  as soon as the task is clear, and at the latest before its first commit; `/wrap-up` checks the
  name before it proposes the commit. A generated name is told apart by its shape, not by its
  prefix: two random words and a hex suffix after `claude/`.
- The merge into `main` is still proposed and asked (rule 13). Once it is done, the assistant
  deletes the branch without asking, with `git branch -d`, which refuses a branch that is not
  fully merged. A branch held by a worktree is released first: `git switch --detach` from the
  session inside that worktree, or `git worktree remove` from outside, which refuses a worktree
  with uncommitted changes.
- Force deletion, `git branch -D`, is in the `permissions.ask` list: the owner decides each time.
- The assistant does not push a task branch (rule 13). If the owner pushed one, deleting it on
  the remote is a push, asked like any push.

## Alternatives rejected

- **Keep the generated names.** They identify a session, not the work; telling three of them
  apart meant opening each one, and a merge writes the name into history.
- **No prefix**, as in the owner's first example. The owner then asked for `claude/`: it shows at
  a glance that the assistant made the branch, keeps those branches together in the sorted list
  and apart from any the owner makes by hand, and matches the prefix the desktop app already uses.
- **A category prefix** (`feature/`, `fix/`). The category is already the CHANGELOG subsection;
  a second prefix is one more choice per branch, for no reader.
- **A short slug** (`claude/branch-names`). Clear the day it is made, cryptic a week later; the
  full phrase reads like the commit it becomes.
- **Keep merged branches.** Their commits are already in `main`; left in the list, they hide the
  branches that are still open.
- **Delete with `-D`.** Would drop unmerged work without a word when a merge failed or never
  happened.
- **A hook that refuses a commit on a generated branch name.** Not now: the name is chosen once,
  at the start of a task, and a missed rename costs a rename, not lost work; `/wrap-up` checks it
  before the commit. If generated names still reach commits, the commit gate gets the check
  (ADR 0005).

## Consequences

`git branch` lists only work in progress, and each line says what it is. CLAUDE.md states the
rule in its Workflow section; `/wrap-up` step 9 renames a generated name before the commit and
deletes the branch after the merge; `permissions.ask` lists `git branch -D`. FlatCheck, which
follows the kit, carries the same Workflow line, wrap-up step and `ask` entry. Nothing deletes
old branches retroactively: a branch another session left is that session's, and its worktree
may still hold uncommitted work. The app may no longer find a renamed branch by its old name when
it cleans up an archived session (not observed yet); the deletion after the merge covers the
usual case.
