# 0023. Commit lines up to 100 characters; a CHANGELOG line by judgment, not by a gate

Date: 2026-09-28
Status: accepted 2026-09-28
Supersedes: 0005 in part (item 1, and the CHANGELOG half of item 3); 0021 in part (the 72-character limit)

## Context

Since ADR 0005, `scripts/check-staged.ps1` refused any commit that touched a code folder without
touching `CHANGELOG.md` (run by the git `pre-commit` hook, so also at a merge through
`pre-merge-commit`, and by the Claude commit gate), and the Stop hook refused to end a turn with
uncommitted code edits and an unchanged CHANGELOG. The effect was a CHANGELOG line for every
commit, including a code-review follow-up, a rename or a comment fix, none of which a reader of
the version history needs; the file filled with `Internal` lines written to satisfy a script.
Separately, the `commit-msg` hook capped the one-line message at 72 characters, which forced
reasons to be cut short. The owner asked, in chat on 2026-09-28, for both rules to change across
the whole `photo` repository: messages up to 100 characters, and a CHANGELOG line only for the
commits where it makes sense, with no check at commit.

## Decision

- The `commit-msg` hook accepts one line of at most 100 characters (was 72). One line, English,
  no body, no trailers, as before.
- `scripts/check-staged.ps1` is deleted: its only check was the CHANGELOG. `pre-commit` (and so
  `pre-merge-commit`) and the Claude commit gate run `verify.ps1` alone. The Stop hook no longer
  looks at `CHANGELOG.md`; it still refuses a turn that edited code without a green verify
  after the last edit.
- A CHANGELOG line is a judgment, made at `/wrap-up`: a line for what a reader of the version
  history cares about (a behaviour added, changed or removed, a fix, a figure that moved, a
  refactor worth knowing about); none for a review follow-up, a test-only change, a comment or
  doc fix, or a small internal tidy-up.

## Alternatives rejected

- **Keep check-staged with an opt-out (a flag, a marker in the message).** ADR 0005 rejected a
  bypass because the model would learn to use it; a check that is bypassed whenever it is wrong
  checks nothing, and the owner wants the judgment, not a ritual.
- **Keep the CHANGELOG check in the Stop hook only.** It would still demand a line for a review
  follow-up before the turn could end, the case the owner named.
- **A soft warning instead of a refusal.** Noise on every commit that rightly has no line.

## Consequences

A code change can now reach `main` with no CHANGELOG line; `/wrap-up` step 4 and the owner's
review of the diff before the merge are what catch a missing one, and `/release` reads
`[Unreleased]` before cutting a version. `CheckStagedTests` is gone; `HookTests` and
`GitHookTests` assert that the hooks run `verify.ps1` alone and that the Stop hook passes with a
clean CHANGELOG. The 100-character limit is guarded by the `commit-msg` hook for every committer.
