# 0026. The main checkout refuses a Claude session's commit on a claude/ branch

Date: 2026-09-30
Status: accepted 2026-09-30

## Context

ADR 0013 keeps the main checkout on `main`: the owner merges there, parallel sessions share it,
and a task branch lives in its own worktree. That rule is prose. The main checkout's reflog shows
sessions switching it to task branches four times: 2026-09-25 19:17 and 21:00, 2026-09-26 17:35
and 17:42. The last three were `claude/` branches, and each was followed by a commit there. The
09-26 switches came from a session started in a folder with no `.claude/settings.json`, so no
Claude Code hook ran; only a git hook reaches every session in the repository. On 2026-09-30 the
owner decided, in chat, that the guard binds the assistant and never him.

## Decision

The kit's `scripts/hooks/pre-commit`, installed for the whole repository in `.githooks/`, refuses a
commit, before it runs any gate, when all three hold:

- `CLAUDECODE` is set, as Claude Code sets it in the shells of its tools;
- the current branch starts with `claude/`;
- the repository is the main worktree: `git rev-parse --absolute-git-dir` equals
  `git rev-parse --path-format=absolute --git-common-dir` (a linked worktree has a git folder of
  its own).

The refusal says how to move the work to the branch's own worktree. `pre-merge-commit` runs
`pre-commit`, so a session's merge onto a `claude/` branch left in the main checkout is refused
the same way. The owner's commits and merges do not meet the first condition, and a session's
merge into `main` does not meet the second.

## Alternatives rejected

- **Bind the owner too.** He asked not to be bound; a merge of his onto a `claude/` branch left
  in the main checkout stays his choice.
- **A check in `claude-commit-gate.ps1`, or a parser of `git switch` and `git checkout`.** It
  reaches only sessions whose project settings load it, which the 09-26 session's did not, and a
  command parser misses `git branch -m`, `git switch -`, `git -C` and the PowerShell forms. The
  commit gate's tests also run about thirty cases on a `claude/` branch in a temporary main
  worktree, all of which would need rebuilding; `/wrap-up` checks the worktree before its commit
  instead, which saves the gate run a refusal would waste.
- **A `post-checkout` hook.** It cannot block the switch, and a fourth git hook adds to what
  `install-hooks.ps1` and `sync.ps1` must list. It stays the escalation if a switch without a
  commit ever does harm.
- **Refuse any branch but `main` in the main checkout.** It would bind the owner's own branches.

## Consequences

`GitHookTests` covers five cases: a session's commit on `claude/` in the main checkout is refused
before the gate; the owner's runs the gate; a session's commit in a linked worktree runs it; a
session's merge onto `claude/` in the main checkout is refused, with the merge left to abort; a
session's merge into `main` runs the gate. `ScriptRunner` removes `CLAUDECODE` from what it
starts, so the suite gives the same results under Claude Code and by hand. The limits: the check
trusts `CLAUDECODE`, so a session that clears it passes; it stops the commit, not the switch; a
session's commit on `main` in a folder where no Claude hook runs is outside it; it needs git 2.31
or later. Whether the desktop app's own terminal panel sets `CLAUDECODE` is not measured; if it
does, the owner is refused there only on a `claude/` branch in the main checkout. In `photo` the
check takes effect when the refreshed `.githooks/` is merged into `main`.
