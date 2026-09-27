# 2026-09-27 Sync the Claude Code hooks from the kit (kit ADR 0020)

## Context

Session 2 of the kit's plan `docs/plans/2026-09-27-run-the-claude-hooks-in-the-commits-worktree.md`
(kit ADR 0020, merged as 4de9f5c): the commit gate and the Stop hook now judge the project of
the commit and of each edited file, found from the hook's `cwd`, the command's `cd`/`git -C`
and the edited file's nearest `kit.json`, instead of `CLAUDE_PROJECT_DIR`, which stays at the
folder the app opened. This is the routine sync commit ADR 0048 foresees: no FlatCheck
decision, no ADR.

## Steps

1. Worktree `FlatCheck/.claude/worktrees/Sync-the-hooks-from-the-kit`, branch
   `claude/Sync-the-hooks-from-the-kit` from `main` (ADR 0050).
2. `scripts/sync.ps1`: `claude-commit-gate.ps1`, `claude-stop-gate.ps1`, `kit-config.ps1`
   copied; `-Check` exit 0.
3. `scripts/verify.ps1` green.
4. Live checks of the new hooks on this worktree, by piping the JSON Claude Code would send
   (the session's own hooks are still the old ones, loaded from the shared checkout at start):
   the Stop hook with an edit under this worktree's `FlatCheck.Tests/` and no stamp must exit 2
   naming this worktree; the commit gate with `cwd` here and `--no-verify` must block; the
   commit gate with `cwd` here and a staged file must run this worktree's gate.
5. One commit; the owner runs it by hand, because this session's commit hook still runs the
   shared checkout's gate, red at step 0 until this very change is merged (the last time).

## Verification

Step 3 and the three outputs of step 4, pasted below.

## Results, 2026-09-27

Step 3: `== OK in 137s`, 937 core tests and 307 WPF tests green, step 0 identical with the kit.

Step 4, with `CLAUDE_PROJECT_DIR` set to the shared checkout `E:\Photografie\Code\FlatCheck`
on purpose, as in a session the app opened there:

- Stop hook, edit list holding this worktree's `FlatCheck.Tests\CheckStagedTests.cs`, `cwd`
  here, no `.verify-stamp` here yet:

  ```
  Stop gate: before ending this turn:
    E:\Photografie\Code\FlatCheck\.claude\worktrees\Sync-the-hooks-from-the-kit\FlatCheck:
      - scripts/verify.ps1 has not run green with tests in this project (no .verify-stamp; ...)
  exit 2
  ```

  The old hook exited 0 on the same input all day (75 of 76 edits unwatched, kit ADR 0020).
- Commit gate, `git commit --no-verify -m x`: `Blocked: git commit --no-verify (or -n) is
  forbidden`, exit 2, nothing run.
- Commit gate, `git commit -m "Sync the hooks"` with the three scripts staged here: exit 0 after
  127 s, and this worktree's `.verify-stamp` rewritten at 17:43:52, so the gate ran here, not in
  the shared checkout, which was red at step 0 at that moment.

## What deviated

- Nothing in the sync itself: three files, as the kit's plan predicted.
- The commit is run by the owner by hand: this session's own commit hook is still the old one,
  loaded from the shared checkout at session start, and it runs the shared checkout's gate, red
  at step 0 until this change is merged. The last time this happens: from the next session on,
  the hook judges the worktree.
