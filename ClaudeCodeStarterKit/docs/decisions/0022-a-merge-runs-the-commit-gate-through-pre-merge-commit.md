# 0022. A merge git records by itself runs the commit gate, through pre-merge-commit

Date: 2026-09-27
Status: accepted 2026-09-27

## Context

The gate ran at every commit (`pre-commit`, and the Claude commit hook for the model's), and the
kit said a commit is the one moment the full gate is mandatory. A merge is not a `git commit`:
when it completes without conflicts, git calls neither `pre-commit` nor the Claude hook, only
`pre-merge-commit`, which the kit did not have. On 2026-09-27 the merge of
`claude/Say-how-to-conclude-a-conflicted-merge` into `main` was recorded with no gate; verify
was run by hand afterwards. The same day, a conflicted merge had shown what a merged result can
break on its own: two branches each added a FlatCheck ADR 0051, and `RepoInvariantTests` failed
only on the combination. `GitHookTests` reproduced it: a clean merge with a red `verify.ps1`
was recorded. The owner asked, in chat, for the hook.

## Decision

The kit gains `scripts/hooks/pre-merge-commit`, which runs the `pre-commit` beside it
(`exec "$(dirname "$0")/pre-commit"`, as git's own sample does). When git calls it, the index
holds the merged result and `git diff --cached` lists what the merge brings in over `HEAD`, so
`pre-commit` gates exactly the projects the merge touches, on the merged tree. It is a git hook
like the other two: it stays in the kit, `install-hooks.ps1` copies it to `.githooks/` in a
monorepo, and `sync.ps1` leaves it out of the projects.

## Alternatives rejected

- **Run verify by hand after each merge.** A rule the model or the owner must remember; the
  first clean merge after this was found went in unchecked.
- **A copy of pre-commit's logic in the new hook.** Two implementations of one gate would
  drift; delegating keeps one.
- **Gate in the Claude hook for `git merge`.** Covers only the model's merges, not the owner's,
  and would run before git has produced the merged result it needs to check.

## Consequences

A merge that brings code into a project now waits for that project's verify (about 1.5 min for
the kit, 2 for FlatCheck); a red result is not recorded, git keeps the merged files and
`MERGE_HEAD`, and `git merge --abort` goes back. A conflicted merge is unchanged: it ends with
`git merge --continue` or `git commit`, which run `pre-commit`. The hook's messages say
"pre-commit", since it is that script that runs. `git merge --no-verify` skips it, as
`--no-verify` skips pre-commit; rule 4 forbids both. A hook change takes effect in every
worktree only after it is merged into the main checkout's `.githooks/` (ADR 0016). The merge
that adds this hook already runs it: git writes the merged tree, `.githooks/pre-merge-commit`
included, before it calls the hook (seen on 2026-09-27, merge `425a4ee`, gated in both
projects). `GitHookTests` guards it: a clean merge runs A's check-staged and verify, and a red
verify leaves `HEAD` where it was.
