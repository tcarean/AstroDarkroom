# 2026-09-27 Run the Claude Code hooks in the worktree of the commit, not the app's folder

## Context

Both Claude Code hooks of the kit, `scripts/hooks/claude-commit-gate.ps1` and
`scripts/hooks/claude-stop-gate.ps1`, take the project folder from `CLAUDE_PROJECT_DIR`. The
Claude Code documentation (hooks reference, worktrees page) says that variable "stays put: it
still points at the project root where the session started", also inside a worktree, while the
JSON every hook receives on stdin carries `cwd`, "the current working directory when the hook is
invoked", which follows `cd` and worktrees. The kit's workflow (ADR 0013, ADR 0019) puts every
task in a worktree the assistant makes by hand from a session the app opened on the project
folder, so in such a session the hooks look at the wrong folder.

Measured on 2026-09-27, in the session that did ADR 0017 and ADR 0018:

- the session's edit list (`%TEMP%\claude-edits-<session>.txt`) had 76 entries, 75 under
  hand-made worktrees and 0 under the shared checkout; the Stop hook compared them with the
  shared checkout and judged nothing in three tasks;
- a `git commit` from a green FlatCheck worktree was blocked by the commit gate, which ran
  `verify.ps1` in the shared checkout, red at step 0 (the kit had just been merged and `main`'s
  FlatCheck was not synced yet).

A worktree the app itself opens has no such problem: there the variable is the worktree.

## Decisions taken by the owner (2026-09-27, "merg pe recomandarile tale")

1. The commit gate resolves the commit's folder in this order: `git -C <path>` in the command,
   a `cd <path>` earlier in the same command, the payload's `cwd`, `CLAUDE_PROJECT_DIR`.
2. A commit outside any folder with a `kit.json` above it is let through; the git `pre-commit`
   hook remains the net, with its own rule on the staged files.
3. The Stop hook judges each edited file by its own project (the nearest ancestor with
   `kit.json`), not by one folder for the whole turn.
4. The hooks get tests in the kit, with stub scripts in a throwaway repository.

## Design

- `scripts/kit-config.ps1`: `Find-KitProject([string]$Path)` walks up from a file or folder to
  the nearest folder holding `kit.json`; `$null` when there is none. The same rule as
  `pre-commit`, which walks up to the nearest `scripts/verify.ps1`.
- `claude-commit-gate.ps1`: the folder of the commit (decision 1), then `Find-KitProject`; runs
  `scripts/check-staged.ps1` and `scripts/verify.ps1` of that project; none found: exit 0
  (decision 2). The `--no-verify` block stays first, before any resolution.
- `claude-stop-gate.ps1`: groups the edited paths by `Find-KitProject`; for each project, in
  turn: check A (a shared folder edited, `isKit` from that project's `kit.json`), check B (its
  `.verify-stamp` newer than the newest edit; its `CHANGELOG.md` dirty while a code edit is
  dirty). Paths outside any project are ignored, as today. The message names the project when
  more than one is involved.
- `claude-track-edits.ps1`: unchanged (absolute paths already).
- `HookTests` (kit, `tests/Starter.Tests/`): a throwaway repository with two projects A and B,
  each with `kit.json` and stub `scripts/check-staged.ps1` and `scripts/verify.ps1` that write
  a marker file and exit with a chosen code; the hooks are the real ones, copied. Cases:
  commit gate with `cwd` in A runs A's scripts and returns their verdict; `git -C B commit` runs
  B's; `cd B && git commit` runs B's; `--no-verify` is blocked before anything runs; a `cwd`
  outside both passes without running a stub. Stop gate: an edit under A's `src/` with no stamp
  in A exits 2 naming A, even with `cwd` in B; with a fresh stamp and a dirty CHANGELOG exits 0;
  an edit under A's `scripts/` exits 2 when A's `kit` is `../Kit` and 0 when it is `.`; edits
  in A and B are judged separately.
- Docs: ADR 0020 (complements 0016 and 0019: the hooks follow `cwd`, the variable stays at the
  app's folder), GHID (the worktree paragraph: what the hooks look at), README "Ce e impus
  mecanic" (the Stop and commit hooks judge the project of the edited file or of the commit),
  CHANGELOG (Fixed).

## Branch

Worktree `ClaudeCodeStarterKit/.claude/worktrees/Run-the-Claude-hooks-in-the-commits-worktree`,
branch `claude/Run-the-Claude-hooks-in-the-commits-worktree`, made from the shared checkout
(ADR 0016, ADR 0019). Implemented in a session started in that worktree's kit folder, so the
kit's rules and hooks apply.

## Steps

### Session 1, in the kit

1. `Find-KitProject` in `kit-config.ps1`.
2. `claude-commit-gate.ps1`: folder resolution and the project's scripts.
3. `claude-stop-gate.ps1`: per-project grouping.
4. `HookTests`, with `ScriptRunner` piping the JSON payload on stdin (add a stdin parameter).
5. ADR 0020, GHID, README, CHANGELOG; `scripts/verify.ps1` green; `sync.ps1 -Check` from the
   kit lists FlatCheck's two hooks and `kit-config.ps1` as drifted.

### Session 2, in FlatCheck (`claude/Sync-the-hooks-from-the-kit`)

`scripts/sync.ps1`, verify green, one commit; no ADR, this is the routine sync commit ADR 0048
foresees. Then, from a FlatCheck session with a hand-made worktree: an edit of a test file and
a turn ended without verify must be refused by the Stop hook; a commit from the worktree must
run the worktree's gate.

## Verification

Kit: verify green with `HookTests`. FlatCheck, after session 2: the two live checks above,
pasted in the session-2 plan.

## What deviated

Session 1 (kit), 2026-09-27:

- `Find-KitProject` also stops at the first folder holding `.git`. Without it, a file at the
  root of a worktree made under a project's `.claude/worktrees/` (the monorepo's own files)
  walked up into the enclosing project; the `pre-commit` walk stops at the repository root too.
  In ADR 0020's rejected alternatives.
- The Stop hook names the project (its full path) in every refusal, not only when more than one
  is involved: with worktrees, "which copy" is the question even for one project, and the plan's
  own test ("exits 2 naming A") asked for it.
- The commit gate recognises git options between `git` and `commit` (`-C <path>`, `-c k=v`);
  the old pattern did not see `git -C <path> commit` as a commit at all, so it passed ungated.
  A `cd` is read in its Bash and PowerShell forms (`cd`, `pushd`, `Set-Location [-Path]`,
  `Push-Location`, `sl`), quoted or not, relative, or as a Git Bash path (`/e/...`); `--no-verify`
  is checked in every commit of the command; the patterns match at the start of any line.
- More cases than planned in `HookTests` (18): every `cd`/`-C` form above, `-n` after `git -C`,
  a command that only mentions `git commit`, and a committed, clean CHANGELOG in the edited
  file's project (proves `git status` runs there). Run against the old hooks, 10 of the 18 fail.
- Verify green, 48 tests. `sync.ps1 -Check` from the kit: FlatCheck differs in exactly
  `scripts/hooks/claude-commit-gate.ps1`, `scripts/hooks/claude-stop-gate.ps1` and
  `scripts/kit-config.ps1`, as planned.
