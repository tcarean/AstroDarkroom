# 0052. The assistant commits on its own claude/ branches; the owner commits on main

Date: 2026-09-27
Status: accepted 2026-09-27

## Context

Until 2026-09-27 rule 13 made every commit a question: the assistant proposed the message and the
owner decided who ran it. FlatCheck task branches are `claude/<Task-name>` branches in their own
worktrees (ADR 0044, ADR 0050), and nothing on them reaches `main` before the merge. On
2026-09-27 the owner decided, in chat, for the kit and for FlatCheck: the assistant may commit on
the branches it created, commits on `main` are the owner's, and only the merge is still asked.

## Decision

The assistant commits without asking on a `claude/` branch, with the gate green and the whole
diff read back; on `main`, any other branch or a detached HEAD it proposes the message and the
owner commits by hand. Merge, push, rebase and `branch -D` stay asked, one approval per command.
`git commit` is in `permissions.allow`, and the kit's `claude-commit-gate.ps1` refuses a model
commit off a `claude/` branch. The reasoning and the rejected alternatives are the kit's ADR 0021,
not repeated here.

## Consequences

Rule 13 and the Workflow section of CLAUDE.md say so; `/wrap-up` commits on the task branch and
asks only for the merge, and `/release` leaves `Release X.Y.Z` and the tag `flatcheck-vX.Y.Z`
to the owner. The owner reviews a task before its merge, in its worktree. A session started
before the change still prompts for commits until it restarts, since it loaded the old settings.
