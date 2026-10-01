# Task 4 of 6: the owner bumps the version, a release runs only from main (Q3 + Q4)

## Context

These decisions come from the programme plan (`ClaudeCodeStarterKit/docs/plans/2026-09-30-apply-the-process-decisions.md`).

**Q3, the version.**
- The owner may bump the version per feature, as FlatCheck already does.
- `release.ps1` takes its floor from the newest released CHANGELOG heading, because no tags exist.
- It refuses a version in four cases:
  - not above that heading;
  - below `<Version>`;
  - already a heading;
  - already tagged.
- `/release` step 2 proposes `<Version>` when the owner has already bumped it.
- A new universal rule: the version and the copyright year change only at the owner's word.

**Q4, where a release runs.**
- A release runs in a session the owner opens in the project folder of the main checkout, on `main`.
- `release.ps1` refuses to run off `main`.
- The skill drops "(or the branch the owner names)".
- `git tag*` goes to `ask`.
- Skill step 6:
  - checks that HEAD did not move;
  - proposes a commit limited to `Directory.Build.props` and `CHANGELOG.md`, and the tag.

Q3 and Q4 share one ADR.

**Facts checked (2026-10-01):**
- `git tag` is empty.
- FlatCheck has `<Version>2.3.0</Version>`, and its newest heading is `## [2.2.0] - 2026-09-23`. Today's `release.ps1` refuses 2.3.0 because the version must be "greater than current".
- The kit has `<Version>0.1.0</Version>` and heading `[0.1.0]`.
- No test covers `release.ps1`.

**Setup:**
- Branch `claude/Let-the-owner-bump-the-version-and-release-only-from-main`.
- A hand-made worktree from the kit folder with the same name, without `claude/`.
- This plan is saved as `ClaudeCodeStarterKit/docs/plans/2026-10-01-let-the-owner-bump-the-version-and-release-only-from-main.md`.

## Changes

**`scripts/release.ps1`** (synced):
- A new first check: `git branch --show-current` must be `main`. Otherwise it refuses and names the branch. A detached HEAD counts as off `main`.
- **Floor:** the highest `X.Y.Z` among the `## [X.Y.Z] - date` headings of `CHANGELOG.md`. With no released heading, there is no floor.
- **Refusals, each with its own message:**
  - `-Version` equals a released heading ("already released");
  - `-Version` is not above the floor;
  - `-Version` is below `<Version>` ("the owner already set <Version> to Y");
  - `[Unreleased]` is empty;
  - the tag exists.
- `-Version` may equal `<Version>`: the owner already bumped it. Step 3 then leaves `Directory.Build.props` unchanged and says so.
- The header documents all of this.
- The final "Propose" line names the commit limited to the two files.

**`.claude/skills/release/SKILL.md`** (synced):
- **Step 1:**
  - The release runs on `main` in the main checkout, with a clean tree, in a session the owner opened in the project folder.
  - The "(or the branch the owner names)" clause is dropped.
  - It records `git rev-parse HEAD`.
- **Step 2:** if `<Version>` is above the newest released heading, the owner has bumped it, so propose that number. Otherwise propose by SemVer. The owner confirms either way, and the kit's rule "Version and year are the owner's" applies.
- **Step 6:**
  - Check that HEAD equals the hash from step 1, and that `git status --porcelain -- .` shows only `CHANGELOG.md` and `Directory.Build.props`. If not, stop and report.
  - Propose `git commit -m "Release X.Y.Z" -- Directory.Build.props CHANGELOG.md` and `git tag -a <tag> -m "Release X.Y.Z"`.
  - Wait. Never push.

**`.claude/settings.json`** (process keys, synced): `ask` gains `Bash(git tag*)` and `PowerShell(git tag*)`.

**`.claude/rules/kit/process.md`** (synced): a new rule after "Measured numbers": **Version and year are the owner's.** "Never bump the version and never roll the copyright year on your own; both change only at the owner's word. Say when a change seems to warrant a bump."

**Kit ADR 0029**, "The owner bumps the version; release.ps1 floors at the newest released heading and runs only on main":
- `Supersedes: 0010 in part (the version precondition and where a release runs)`.
- ADR 0010's Status gains the matching clause.
- It names no adopter ADR numbers.
- Alternatives rejected:
  - floor at the tag, because no tags exist;
  - floor at `<Version>`, because it blocks an owner who already bumped;
  - release from any branch;
  - letting the script commit.

**Tests:** a new `tests/Starter.Tests/ReleaseTests.cs`, plain `[Fact]`, following the shape of `SyncTests`/`GitHookTests`.
- **The temporary repository:**
  - git, branch `main`;
  - a project folder `Proj` holding a copy of `scripts/release.ps1` and `kit-config.ps1`, plus a stub `verify.ps1` that records that it ran;
  - `kit.json` from `ScriptRunner.KitJson`;
  - `Directory.Build.props` with `<Version>2.3.0`;
  - `CHANGELOG.md` with `[Unreleased]` (one line) and `## [2.2.0] - 2026-09-23`;
  - everything committed, so the tree is clean.
- **Cases:**
  - off `main` is refused;
  - 2.2.0 is refused as already released;
  - 2.1.0 is refused, not above 2.2.0;
  - with `<Version>2.4.0`, 2.3.0 is refused as below `<Version>`;
  - an existing tag `proj-v2.3.0` is refused;
  - 2.3.0 with `-NoPublish` succeeds:
    - verify ran;
    - `Directory.Build.props` bytes are unchanged;
    - `CHANGELOG.md` has `## [2.3.0] - <today>` under a new empty `[Unreleased]`;
  - 2.4.0 with `-NoPublish` sets `<Version>2.4.0`;
  - `-WhatIf` changes nothing.

**Docs and CHANGELOG:**
- Kit `CHANGELOG.md`: a Changed line for the floor and `main`, and an Added line for the rule.
- `GHID.md` (Romanian): :59, :281 (the release paragraph), :313.
- `README.md`: :79, and a bullet under "Ce e impus mecanic" for release off `main`.

**FlatCheck** (sync, then):
- `CLAUDE.md` drops its rule "Version and year are the owner's", which is now the kit's.
- `docs/design/process.md` and `docs/skills.md` cite the kit's rule. The `/release` step-2 line in `docs/skills.md` repeats the skill, so it is removed.
- One CHANGELOG Internal line.
- No FlatCheck ADR: FlatCheck ADR 0039 already says the owner bumps per feature, which the kit now adopts.

## Commits

1. K1: the plan.
2. K2: `release.ps1`, the skill, settings, the rules file, `ReleaseTests`, ADR 0029 with the 0010 Status clause, GHID, README, CHANGELOG. From here, the worktree's FlatCheck is red at step 0.
3. F1: the FlatCheck sync, `CLAUDE.md`, the citations, CHANGELOG.

Then `/wrap-up` with "What deviated", `/code-review`, and the merge proposal.

## After the merge: the FlatCheck 2.3.0 release

The owner opens a session in `E:\Photografie\Code\FlatCheck`, the main checkout on `main`, and runs `/release`. Step 2 proposes 2.3.0, because `<Version>` is above `[2.2.0]`. The script closes `[Unreleased]` as `[2.3.0]` and publishes. The skill proposes the commit, limited to the two files, and the tag `flatcheck-v2.3.0`, and the owner runs them. This session does not run it: it was started in the kit folder (Q4).

## Verification

- The kit's `verify.ps1` is green with `ReleaseTests`.
- The new tests fail against the current `release.ps1` where behaviour changes: the off-`main` case and the owner-bumped 2.3.0 case.
- FlatCheck's `verify.ps1` is green after the sync, and `sync -Check` is clean.
- A `-WhatIf` in the worktree's FlatCheck folder is refused (the branch is not `main`), which shows the guard on real data.
- `git diff main...` is read in full before each commit.

## What deviated

- Task 4 ran in the same session as task 3, at the owner's request ("apucate de restul pasilor"),
  although the kit's rule "One task per session" would start a new one.
- With the floor taken as the highest released heading, "already a heading" can only coincide
  with "not above the floor"; it is kept as its own check so the message says "already
  released".
- `ReleaseTests` has eight cases; seven fail against the previous `release.ps1` (the old
  messages and the old floor), the `-WhatIf` case passes on both.
- Beyond the plan's list: FlatCheck's `README.md` version paragraph (the owner bumps per feature,
  a release keeps that version and runs on `main`), the `ask` sentence of both `settings.json`
  comments (tag), and the rules file's "Skills" bullet ("from `main` in the main checkout").
- Checked on real data: `release.ps1 -Version 2.3.0 -WhatIf` in the worktree's FlatCheck is
  refused ("this checkout is on branch 'claude/Let-the-owner-bump-...'").
