# 0053. Commit lines up to 100 characters; a CHANGELOG line by judgment, not by a gate

Date: 2026-09-28
Status: accepted 2026-09-28
Supersedes: 0001 in part (the CHANGELOG a commit cannot skip); 0048 in part (FlatCheck's CheckStagedTests)

## Context

Rule 9 demanded a CHANGELOG line for every change under the three code folders, and
`scripts/check-staged.ps1` refused a commit without it (in `pre-commit`, so also at a merge, and
in the Claude commit gate); the Stop hook refused to end a turn with uncommitted code and an
unchanged CHANGELOG. Rule 13 and the `commit-msg` hook capped the message at 72 characters. On
2026-09-28 the owner decided, in chat, for the whole `photo` repository: messages up to 100
characters, and a CHANGELOG line only where it makes sense, since a code-review follow-up
rightly has none; nothing checks it at commit or at the end of a turn.

## Decision

FlatCheck takes the kit's ADR 0023 as it is: `commit-msg` accepts one line of at most 100
characters; `check-staged.ps1` is deleted from the kit and so, through `scripts/sync.ps1`, from
here; the Stop hook keeps only the green-verify check. Rule 9 becomes a judgment made at
`/wrap-up`: a line for a behaviour added, changed or removed, a fix, a figure that moved, or a
refactor or process change worth knowing about; none for a review follow-up, a test-only
change, a comment or doc fix. `CheckStagedTests` goes with the script it tested.

## Alternatives rejected

- **Keep the check in FlatCheck only.** The scripts are the kit's, byte for byte (ADR 0048), and
  the owner asked for the rule across the repository.
- The kit's ADR 0023 lists the others (an opt-out marker, a Stop-hook-only check, a warning).

## Consequences

A code change can reach `main` without a CHANGELOG line; `/wrap-up` step 4 decides and says
which way, and the owner reads the diff before the merge. `/release` still reads `[Unreleased]`
before cutting a version, so a release note written from it is only as complete as those
judgments.
