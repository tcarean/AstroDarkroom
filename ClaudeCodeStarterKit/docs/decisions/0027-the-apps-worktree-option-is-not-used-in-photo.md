# 0027. The desktop app's worktree option is not used in photo; task worktrees are made by hand

Date: 2026-09-30
Status: accepted 2026-09-30
Supersedes: 0013 in part (the rename of generated names, "or the one the app opened for the session"); 0019 in part (the desktop app's worktree)

## Context

With its worktree option on, the Claude desktop app starts a session in a new worktree under the
opened folder's `.claude/worktrees/`, on a branch with a generated name (`claude/` plus two random
words and a hex suffix). In a monorepo that worktree is a checkout of the whole repository, and
the session starts at its root. Claude Code takes the project from the folder a session starts in
(ADR 0004), and the repository root has no `CLAUDE.md` and no `.claude/settings.json`. The
transcripts of all seven such sessions in `photo` (2026-09-16 to 2026-09-25) show the result. At
start only the owner's auto-memory was loaded; a project's `CLAUDE.md` came later, if at all, when
a file under it was read. Every Stop recorded the two built-in callbacks and none of the project's
hooks, where sessions started in a project folder the same day recorded the project's Stop hook.
Commits went in within 3 to 10 seconds with no Claude commit gate (c19735c, 3684018, e10216d). So
the rules, the `ask` and `deny` permissions and the Claude hooks (ADR 0005, 0020, 0021) did not
apply there, and ADR 0016 could only restore the git hooks. On 2026-09-30 the owner decided, in
chat, that the option is not used in `photo`.

## Decision

- A session starts in the project folder of the main checkout, with the app's worktree option
  off. A task branch and its worktree are made by hand in one command (ADR 0019), so the branch
  has its task's name from the start and no generated name is left to rename.
- Retired: the rename of generated names (ADR 0013), and the app's worktree as a place for a task
  branch (ADR 0013, ADR 0019).
- `core.hooksPath` stays the absolute path of the main checkout's `.githooks/` (ADR 0016), for
  the reason restated there.

## Alternatives rejected

- **Keep the option and warn in `CLAUDE.md`.** A session at the repository root loads no
  project's `CLAUDE.md`, so the warning would not reach it.
- **A `CLAUDE.md` and a `.claude/settings.json` at the repository root.** The owner chose a
  synced rules file per project instead (decision Q2 of 2026-09-30), and hooks configured at the
  root would run the projects' scripts from the wrong folder.
- **Open the app on the repository root.** Already rejected by ADR 0016 and
  `monorepo/README.md`: no project would load.

## Consequences

`CLAUDE.md`, `/wrap-up` step 10, the guide and the READMEs lose the rename of generated names.
The three worktrees the app left under `FlatCheck/.claude/worktrees/` are merged and clean;
removing them is the owner's call. A session that still starts at a checkout root loads only the
owner's machine-local memory, so a one-line warning there (stop and say so) is the only text that
reaches it; it holds no decision. ADR 0020's statement that app-worktree sessions were not
affected is corrected in place.
