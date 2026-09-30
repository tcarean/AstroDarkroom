# Task 2 of 6: the main-checkout guard (Q8) and no app worktrees (Q9)

## Context

The programme plan (`ClaudeCodeStarterKit/docs/plans/2026-09-30-apply-the-process-decisions.md`)
records the owner's decisions of 2026-09-30. Task 2 applies two of them:

- **Q8.** The main checkout (`E:\Photografie\Code`) must stay on `main` (kit ADR 0013), but
  sessions switched it to task branches four times (reflog 09-25 19:17 and 21:00, 09-26 17:35
  and 17:42). The 09-26 ones came from a CodeMetrics session, where no Claude hook exists, so
  only a git hook can catch that. The owner decided the guard binds the model only
  (`$CLAUDECODE`), never him.
- **Q9.** The desktop app's worktree option starts a session at the root of a whole `photo`
  checkout. The transcripts of all seven such sessions (09-16 to 09-25) show that no project
  CLAUDE.md, settings or Claude hooks loaded: only memory at start, and Stop entries with the
  two built-in callbacks instead of three hooks. Commits went through in 3-10 s with no Claude
  gate (c19735c, 3684018, e10216d). Kit ADR 0020 says those sessions "were not affected",
  which is wrong. The owner decided the option is not used in `photo`.

Facts checked: `CLAUDECODE=1` in this session's Bash and PowerShell shells; git 2.55 supports
`git rev-parse --absolute-git-dir --path-format=absolute --git-common-dir`, which gives the same
path twice only in the main worktree (plain `--git-dir`/`--git-common-dir` differ in form in a
subfolder, so they are not used). `CLAUDECODE` is inherited by `dotnet test` when Claude Code
runs verify, so the tests must control it.

Correction to the programme plan's premise: only three of the four switches went to `claude/`
branches (09-25 21:00, 09-26 17:35, 17:42); each was followed by a commit, which the new guard
would have refused.

Branch `claude/Guard-the-main-checkout-and-drop-app-worktrees`, hand-made worktree under
`ClaudeCodeStarterKit/.claude/worktrees/`. This plan is saved as
`ClaudeCodeStarterKit/docs/plans/2026-09-30-guard-the-main-checkout-and-drop-app-worktrees.md`.
Two ADRs instead of the programme plan's "one ADR", one decision each (ADR 0001). Kit ADRs name
no FlatCheck numbers (the kit's ADR 0025).

## Q8: kit ADR 0026, a Claude session never commits on a claude/ branch in the main checkout

- **`scripts/hooks/pre-commit`** (kit-only; `.githooks` copy refreshed with the kit's
  `install-hooks.ps1`):
  - A new check sits right after `cd "$root"`, before any temp file or verify.
  - It fires when `CLAUDECODE` is set, `git branch --show-current` matches `claude/*`, and
    `git rev-parse --absolute-git-dir` equals `--path-format=absolute --git-common-dir`, i.e. the
    main worktree. Absolute forms are used because git may export a relative `GIT_DIR` to hooks.
  - It prints the refusal to stderr, naming the kit's ADR 0026, with the recovery, then exits 1.
    The recovery, from the project folder: `git merge --abort` (only after a refused merge);
    `git stash push -m "<branch>"`; `git switch main`;
    `git worktree add .claude/worktrees/<name> <branch>`; `git stash pop --index` there.
    Untracked files are not stashed, so as not to sweep up the owner's.
  - `pre-merge-commit` runs `pre-commit`, so a merge onto a `claude/` branch left in the main
    checkout by a session is refused too. The owner (no `CLAUDECODE`) is never refused.
  - Detached HEAD passes.
- **`claude-commit-gate.ps1` is unchanged.** Adding the check there would mean restructuring
  about 30 HookTests cases that run on `claude/` in their temp repo's main worktree. Instead,
  wrap-up step 10 checks the worktree before committing, which avoids a wasted verify run.
- **Tests.** These setup and helper edits exist for the new tests (rule 3); no existing assertion
  changes.
  - `ScriptRunner` removes `CLAUDECODE` next to the `GIT_*` scrub, because the suite runs both
    under Claude Code and by hand. It gets `RunWith(workDir, environment, file, args)`.
  - `GitHookTests`:
    - `core.hooksPath` becomes absolute; a relative one resolves against a linked worktree's
      root, where `hooks/` is ignored.
    - `.gitignore` gains `wt/`.
    - `Ran(root)` reads `<root>/A/ran.txt`, and a `Change(root)` helper is added.
  - New `[WindowsFact]`s:
    - a session's commit on `claude/` in the main checkout is refused before the gate;
    - the owner's commit on `claude/` in the main checkout runs the gate;
    - a session's commit in a linked worktree runs the gate;
    - a session's merge onto a `claude/` branch in the main checkout is refused, with MERGE_HEAD
      left behind;
    - a session's merge into `main` runs the gate.
- **ADR 0026 records the limits:**
  - it trusts `CLAUDECODE`;
  - it stops the commit, not the switch;
  - a session's commit on `main` where no Claude hook exists is outside it;
  - it needs git 2.31 or later.
  - Rejected alternatives: binding the owner, a PreToolUse checkout parser, post-checkout, the
    commit-gate duplicate, and "any branch but main".

## Q9: kit ADR 0027, the desktop app's worktree option is not used in photo

- **Context:** the transcript evidence above.
- **Decision:** sessions start in the project folder of the main checkout with the option off,
  and task worktrees are made by hand (the kit's ADR 0019).
- **Headers:**
  - 0027 `Supersedes: 0013 in part (the rename of generated names, "or the one the app opened
    for the session"); 0019 in part (the desktop app's worktree)`.
  - Matching Status clauses go on 0013 and 0019.
- **Notes under the kit's ADR 0024 rules:**
  - **0020:** a `(Corrected 2026-09-30: ...)` note in Context after :18 (and covering :48). No
    header change, because its Decision has no app clause.
  - **0016:** a `(Restated 2026-09-30: ...)` note as the first paragraph of `## Alternatives
    rejected`, not inside Decision.
    - It records that the app reason is gone and that the absolute path stays: each worktree
      runs the main checkout's `.githooks`, which only a merge changes, whereas a relative path
      would run each branch's own copy.
    - Standalone repositories keep the relative `scripts/hooks` and its asymmetry, recorded,
      not hidden.
- **Texts:**
  - Kit `CLAUDE.md` :91-92 drops the generated-name sentence.
  - wrap-up step 10 replaces the rename sentence with "if the commit's folder is the main
    checkout on a `claude/` branch, move the work to a worktree first (the kit's ADR 0026)".
  - `GHID.md` (Romanian): :112-113, :138-139, :242, :251, :340, :351 and :369.
  - `README.md` :39 and the "Ce e impus mecanic" bullet.
  - `monorepo/README.md`: :13 and :18, plus :22's "two git hooks", which should be three.
  - The `install-hooks.ps1` :17-19 comment.
  - CHANGELOG: Added (the guard) and Changed (no app worktrees, no rename rule).

## FlatCheck (after kit ADR 0027 exists, since its links are checked on disk)

- Sync.
- 0044 and 0050 Status: `; the desktop app's worktree superseded by the kit's ADR 0027`.
- 0046: a short Restated note after its Decision, pointing at the kit's ADR 0016 and 0027.
- `CLAUDE.md` :131-132 drops the generated-name sentence.
- One CHANGELOG Internal line.

## Commits

1. K1+K2: the plan, ADRs 0026 and 0027, the headers, and the 0016 and 0020 notes. One commit,
   so that the decisions check stays green.
2. K3: `pre-commit` and the `pre-merge-commit` header, the `install-hooks.ps1` comment,
   `ScriptRunner`, `GitHookTests`, the refreshed `.githooks/`, CHANGELOG Added.
3. K4: `CLAUDE.md`, the wrap-up skill, GHID, README, `monorepo/README.md`, CHANGELOG Changed.
   The worktree's FlatCheck is red at step 0 from here until F1, and no FlatCheck commit is
   made in between.
4. F1: sync, the FlatCheck headers and note, `CLAUDE.md`, CHANGELOG.

Then `/wrap-up`, the plan's "What deviated", `/code-review`, and the merge proposal.

## After the merge (owner's decisions, asked separately)

- Remove the three app worktrees under `FlatCheck/.claude/worktrees/` (clean and merged) with
  `git worktree remove`, or archive their app sessions. This happens only on the owner's yes.
- The owner runs `echo $env:CLAUDECODE` once in the desktop app's own terminal panel. If it is
  set there, the guard also binds him in that shell, but only in the situation the rule forbids
  anyway, which is committing on `claude/` in the main checkout.
- Memory:
  - `git-hooks-in-app-worktrees.md` becomes a one-line warning: a session started at a photo
    checkout root has no project CLAUDE.md, settings or hooks, so say so and ask.
    Memory is the only thing such a session loads.
  - `branch-names-and-cleanup.md` loses the rename guidance and gains the guard.
  - `process-decisions-2026-09-30.md` marks task 2 as merged.

## Verification

- The kit's `verify.ps1` is green, with the new GitHookTests passing under `CLAUDECODE=1` (this
  session) and without it (the scrub).
- A manual check in the task worktree's temp copy: a commit on `claude/` in a scratch repo's main
  worktree with `CLAUDECODE=1` is refused, with the message shown.
- FlatCheck's `verify.ps1` is green, and `check-decisions` passes on both projects.
- `git worktree list` and `sync -Check` are clean before the merge.

## What deviated

- Two ADRs, 0026 and 0027, one decision each, where the programme plan said "one ADR". ADR 0020 got a Corrected note, not a supersession: its Decision has no app clause (the kit's ADR 0024 criterion).
- The refusal's recovery uses `git stash apply --index <hash>` and dropping that entry, not `git stash pop --index`: the stash stack is shared by every worktree and session, so a pop could take another session's entry.
- The worktree bullet of both `CLAUDE.md` files also names the guard, beside dropping the rename sentence.
- The guide's copy of the wrap-up steps keeps its old "9." numbering (a pre-existing drift the analysis listed for a later task).
