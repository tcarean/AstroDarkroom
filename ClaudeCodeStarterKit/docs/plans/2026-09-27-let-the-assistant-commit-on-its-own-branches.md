# The assistant commits on its own branches; the owner commits on main; only the merge is asked

## Context

On 2026-09-27 the owner changed the history rule, for the kit and for FlatCheck: the assistant
may commit, without asking, on the branches it created; commits on `main` are the owner's; the
only thing the assistant still asks for is the merge. Today rule 13 (both CLAUDE.md files), kit
ADR 0007, GHID, the wrap-up and release skills, and `permissions.ask` all say "every commit is
proposed and asked". The change is made in the kit first, then synced to FlatCheck (rule 19).

## Decisions (approved by the owner on 2026-09-27, with the plan)

1. **"A branch the assistant created" = a branch named `claude/*`.** The prefix already marks the
   model's branches (kit ADR 0013). A detached HEAD, `main`, or any other name is the owner's.
2. **Enforced, not only written.** `git commit` moves from `ask` to `allow` in both
   `.claude/settings.json`; `scripts/hooks/claude-commit-gate.ps1` gains a step that refuses a
   model commit when the branch of the commit's folder (already found, ADR 0020) is not
   `claude/*` (exit 2, fail closed, message pointing at rule 13). The git `pre-commit` and
   `commit-msg` hooks are unchanged: the owner commits anywhere, one-line messages still apply.
3. **Still `ask`:** `git merge`, `push`, `rebase`, `branch -D`. The assistant does not push;
   rebase rewrites history. "Only the merge" is read as "of the things I used to ask, only
   the merge remains"; push and rebase stay as they are. `--no-verify` stays denied.
4. **When the assistant commits:** at `/wrap-up` step 10, after verify is green and the whole
   diff has been read back, with the one-line message; it reports the message and the hash.
   Small commits during the task are allowed on the same terms (gate green). The owner's review
   moves from "before the commit" to "before the merge" (`git diff main...claude/<Task>` in the
   worktree, `/code-review` suggested as today).
5. **`/release` runs on `main`,** so the commit `Release X.Y.Z` and the tag are proposed and the
   owner runs them; the skill no longer offers "or should I?".
6. **Kit-to-project sync commits** (`scripts/sync.ps1`, one commit per project) are made on the
   task's `claude/` branch like any other commit.
7. ADR: kit **0021** (supersedes the "never commits" part of 0007; 0007's status says so), and
   FlatCheck **0052** adopting it in a few lines, pointing at kit 0021, as 0050 does for 0019.

## Work

Branch `claude/Let-the-assistant-commit-on-its-own-branches`, worktree
`ClaudeCodeStarterKit/.claude/worktrees/Let-the-assistant-commit-on-its-own-branches` (a change
spanning both starts from the kit, FlatCheck ADR 0044/0050).

### Commit 1 - the kit (`ClaudeCodeStarterKit/`)
- `tests/Starter.Tests/HookTests.cs`: first the failing tests - commit on `main` blocked,
  detached HEAD blocked, `claude/X` passes to the gate; the throwaway repo is created on a
  `claude/` branch so the existing gate tests keep judging what they judged.
- `scripts/hooks/claude-commit-gate.ps1`: the branch step (between step 2 and 3), with the
  header comment updated. Reuses `Invoke-Git` and `Get-CommitFolder`.
- `.claude/settings.json`: `git commit` from `ask` to `allow` (Bash and PowerShell), `$comment`.
- `CLAUDE.md`: rule 13, Workflow lines (wrap-up, release, merge, worktree "review before the
  merge", "read the whole diff back before committing").
- `.claude/skills/wrap-up/SKILL.md` step 10 and description; `.claude/skills/release/SKILL.md`
  intro, step 6 and description.
- `docs/decisions/0021-...md` new; `0007` status line.
- `GHID.md`: "Branch si commit", "Mesajul de commit si cine face commit-ul", the gate tables
  (`permissions.ask` rows, PreToolUse row), the workflow diagram/steps, "Codul necitit",
  "Permisiuni"; kit `README.md` if it states the rule.
- `AI Prompt to Generate Starter Project.md`: principle 7, the wrap-up line, the rule text, the
  hand-off (the first commit is on `main`, so the owner's).
- `CHANGELOG.md` `[Unreleased]`; `docs/plans/2026-09-27-let-the-assistant-commit-on-its-own-branches.md`.
- `scripts/verify.ps1` green in the kit.

### Commit 2 - FlatCheck (`FlatCheck/`)
- `scripts/sync.ps1` from the kit: the hook and the two skills arrive byte-identical.
- `.claude/settings.json` as in the kit; `CLAUDE.md` rule 13 and Workflow lines;
  `docs/design/process.md` line on "the commit is the owner's"; `docs/decisions/0052-...md`.
- No FlatCheck CHANGELOG line (nothing under Core/console/Wpf, rule 9).
- `scripts/verify.ps1` green in FlatCheck.

### After
- Memory: update `commit-and-merge-policy.md`.
- These two commits are still asked: this session loaded the old `ask` list and hook from the
  shared checkout. Then I propose the merge into `main` and ask (with `/code-review` first).
- Note: another worktree, `claude/Cut-what-process-md-repeats-from-CLAUDE-md-and-the-ADRs`,
  may touch the same CLAUDE.md lines; a conflict there is resolved at its merge.

## Verification
- `dotnet test` in the kit, filter `HookTests`: the three new tests red before the hook change,
  green after; all existing HookTests green.
- `scripts/verify.ps1` green in the kit and in FlatCheck; `scripts/sync.ps1 -Check` clean.
- By hand, piping a payload into the hook: `git commit` in the shared checkout (on `main`) is
  refused with the rule-13 message; in the task worktree it runs the gate.

## Deviations

- FlatCheck's ADR is 0052, not 0051: the branch `claude/Cut-what-process-md-repeats-from-CLAUDE-md-and-the-ADRs`,
  merged into `main` meanwhile, took 0051, and `RepoInvariantTests` refused the duplicate at the
  merge. Its cut of `process.md` also removed the sentence this task had edited there; the merge
  keeps `main`'s version.
- The hook's message names "the kit's ADR 0021", since a project's ADR 0021 is another decision.
