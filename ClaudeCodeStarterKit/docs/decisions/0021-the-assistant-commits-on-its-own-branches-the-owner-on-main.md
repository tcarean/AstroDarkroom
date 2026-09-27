# 0021. The assistant commits on its own claude/ branches; the owner commits on main and decides every merge

Date: 2026-09-27
Status: accepted 2026-09-27
Supersedes: 0007

## Context

ADR 0007 made every commit a question: the assistant proposed the message and the owner decided,
each time, who ran it. Since then every task has its own `claude/<Task-name>` branch in its own
worktree (ADR 0013, ADR 0019), and the Claude commit hook runs check-staged and verify on the
projects each commit touches, in the commit's own worktree (ADR 0020). A commit on a task
branch changes nothing the owner uses until the merge, so asking before it added a prompt per
commit without protecting `main`. On 2026-09-27 the owner decided, in chat, for the kit and for
FlatCheck: the assistant may commit on the branches it created; commits on `main` are the
owner's; the assistant asks only for the merge.

## Decision

- A commit message is one line, in English, saying why, under 72 characters. No body, no
  trailers of any kind (unchanged from ADR 0007; the `commit-msg` hook enforces it for everyone).
- The assistant commits without asking on a branch named `claude/...`, the prefix that marks the
  branches it creates (ADR 0013), with the gate green. `main`, any other branch and a detached
  HEAD are the owner's: the assistant proposes the message there and the owner commits by hand.
- The assistant never merges, pushes or rebases on its own. It proposes the merge into `main`
  and asks; the owner decides each time whether the assistant runs it or does it by hand. An
  approval covers that one command. A release, cut on `main`, is committed and tagged by the
  owner.
- Enforced by the tools, not by memory: `git commit` moves from `permissions.ask` to `allow`,
  and `claude-commit-gate.ps1` refuses a model commit when the commit's folder is not on a
  `claude/` branch, before check-staged and verify run. `merge`, `push`, `rebase` and
  `branch -D` stay in `ask`.

## Alternatives rejected

- **Keep asking for every commit (ADR 0007).** The review that matters is before the merge,
  where the whole branch is read against `main`; a prompt per commit on a throwaway branch only
  slowed the task down.
- **Rule in CLAUDE.md only, `git commit` left in `ask` or moved to `allow` without a hook.** In
  `ask` the owner is still prompted, which is what the change removes; in `allow` with no check a
  long session could commit on `main` by mistake. Permission patterns cannot see the branch; the
  hook can, in the commit's own worktree.
- **"Created by the assistant" tracked per session** (a list of branches the session made). The
  app's worktrees are made by the app, sessions end, and a later session continues a branch;
  the `claude/` prefix already says whose branch it is and survives all of that.
- **Let push and rebase go too.** Not asked for: the assistant does not push, and a rebase
  rewrites history the owner may already have read.

## Consequences

The owner reviews a task at the merge (`git diff main...claude/<Task-name>` in its worktree,
`/code-review` before it) instead of at each commit. `/wrap-up` commits on the task branch and
asks only for the merge; `/release` proposes the release commit and the tag for the owner to run.
The sync commits of `scripts/sync.ps1`, one per project, are made on the task branch.
`HookTests` guards the hook: a commit on `main`, `master`, `feature/...` or a detached HEAD is
refused, and a worktree on a `claude/` branch commits while the main checkout is on `main`.
A session started before this change keeps the old `ask` list until it restarts. FlatCheck
records the same rule in its ADR 0052.
