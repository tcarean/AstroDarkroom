# Commit lines up to 100 characters; the CHANGELOG line by judgment, not by a gate

## Context

On 2026-09-28 the owner set two rules for the whole `photo` repository: a commit message may be
up to 100 characters (was 72), and the CHANGELOG is no longer checked at commit, because a
commit such as a code-review follow-up rightly has no line; the assistant adds one only where it
makes sense. The owner also asked for the Stop hook's CHANGELOG check to go. ADR 0023.

## Decisions (approved by the owner in chat on 2026-09-28)

1. The `commit-msg` hook accepts one line of at most 100 characters.
2. `scripts/check-staged.ps1` is deleted (the CHANGELOG was its only check); `pre-commit`,
   `pre-merge-commit` and the Claude commit gate run `verify.ps1` alone.
3. The Stop hook drops its CHANGELOG check; the green-verify check stays.
4. Rule 9 becomes a judgment: a line for what a reader of the version history cares about, none
   for a review follow-up, a test-only change, a comment or doc fix.

## Steps

1. Kit: the three git hooks, the two Claude hooks, `kit-config.ps1` and `sync.ps1` comments;
   delete `check-staged.ps1` and `CheckStagedTests`; `HookTests` and `GitHookTests` expect
   `verify.ps1` alone, the Stop hook passes with a clean CHANGELOG, and `commit-msg` takes 100
   characters and refuses 101.
2. Kit docs: CLAUDE.md rules 9 and 13, the wrap-up and mutate skills, `.claude/settings.json`,
   README, GHID, the AI prompt, ADR 0023 with status notes on 0005 and 0021, CHANGELOG.
3. `install-hooks.ps1`, so `.githooks/` at the repository root carries the new hooks.
4. FlatCheck: `scripts/sync.ps1`, delete `CheckStagedTests`, CLAUDE.md rules 9 and 13,
   `.claude/settings.json`, an ADR pointing at the kit's 0023, CHANGELOG.
5. Both gates green; one commit for the kit, one for FlatCheck; memory updated.
