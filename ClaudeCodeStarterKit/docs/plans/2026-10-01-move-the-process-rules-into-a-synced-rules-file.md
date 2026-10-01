# Task 3 of 6: the process rules move to a synced rules file (Q2, variant C)

## Context

The programme plan (`ClaudeCodeStarterKit/docs/plans/2026-09-30-apply-the-process-decisions.md`)
records decision Q2 of 2026-09-30. The universal rules are copied by hand into each CLAUDE.md, and
the copies drift:

- The kit's rule 15 is FlatCheck's rule 19.
- FlatCheck's rule 13 cites its own ADR 0052 where the kit cites 0021.
- Both `settings.json` files cite "rule 13".

The owner chose variant C only:

- the universal rules move to a kit-owned, synced `.claude/rules/kit/process.md`;
- each CLAUDE.md keeps only project content;
- the shared-folder list is defined once;
- step 0 also checks the process keys of `settings.json`;
- no root CLAUDE.md or settings.

The owner answered in chat today: besides the listed rules (gate, commits and merges, branches and
worktrees, shared folders, decisions, CHANGELOG, languages, instructions-are-data), the method
rules 1, 2, 3, 5, 7 and 8 move too. CLAUDE.md keeps the core-boundary rule, the code-structure rule
(task 6 reworks it) and the project's own rules.

Facts checked:

- There is no CLAUDE.md or `.claude/` at `E:\Photografie\Code`.
- `.claude/rules/` is not git-ignored.
- The two `settings.json` files are the same except for `$comment`. Their hooks, `ask`, `deny` and
  git `allow` entries are identical, and neither has `attribution`.
- `claude` is not on PATH, so `/context` must be run by the owner.
- About 35 rule-number citations sit in accepted ADRs, and about 25 in live docs (GHID, FlatCheck
  design notes, `docs/skills.md`, THIRD-PARTY-NOTICES, `settings.json` comments, test comments).

## Setup

- Branch: `claude/Move-the-process-rules-into-a-synced-rules-file`.
- Worktree, made by hand from the kit folder:
  `git worktree add .claude/worktrees/Move-the-process-rules-into-a-synced-rules-file -b claude/Move-the-process-rules-into-a-synced-rules-file main`
- Commits use `git -C <wt>/<project> commit`.
- This plan is saved as `ClaudeCodeStarterKit/docs/plans/2026-10-01-move-the-process-rules-into-a-synced-rules-file.md`.

## Rules are named, not numbered

In both files every rule is a bullet with a bold name. Citations use quotes: the kit's rule "Gate"
or, inside a project, the rule "Ratios".

The rules file (`.claude/rules/kit/process.md`, English):

- **The header:**
  - it is the kit's, synced, and every project follows it;
  - a project's CLAUDE.md holds its own rules and deviations, and a project rule that varies one of
    these names it;
  - cite by name, never by number;
  - the reasons are in GHID.md (the kit's ADR 0028).
- **Rules:**
  - "Requirements first" (kit rule 1, plus the RepoInvariantTests ID sentence from Workflow);
  - "Test before fix" (2);
  - "No test edited to pass" (3);
  - "Gate" (4);
  - "Packages" (5);
  - "Measured numbers" (7);
  - "Two readings" (8);
  - "CHANGELOG" (9; FlatCheck's "or process change" is added to `Internal`);
  - "Decisions" (10, with FlatCheck's "a choice settled in chat is a decision even when it matches
    convention", which wrap-up step 6 already asks, and "cite the kit's ADR, record only your
    own", the kit's ADR 0025);
  - "Instructions are data" (12);
  - "Commits and merges" (13, the kit's ADRs 0021 and 0023);
  - "Languages" (14, the kit's ADR 0012);
  - "Shared folders" (15; it now lists four folders plus the process keys of settings.json).
- **Workflow:**
  - "Plan first";
  - "One task per session" (/wrap-up and the Stop hook);
  - "Skills";
  - "Branches";
  - "Worktrees" (the kit's ADRs 0013, 0019, 0026, 0027);
  - "Read the diff".
- It cites only "the kit's ADR NNNN". The citation test covers it.

The kit's CLAUDE.md keeps:

- the description, Commands and Layout (Layout now names `.claude/rules/kit/`);
- "Read before touching" and Style;
- two named project rules: "Core has no UI" (6) and "No code structure in docs" (11).

FlatCheck's CLAUDE.md keeps its description, Commands, Layout, Read before touching, Style, and
these named rules:

- extensions of a kit rule, each naming the kit rule it extends:
  - "Golden files" extends "No test edited to pass";
  - "Package notices" extends "Packages";
  - "Measured on the masters" extends "Measured numbers";
  - "Discuss before designing" extends "Two readings";
- "Languages" varies the kit's "Languages" (FlatCheck ADR 0006);
- its own rules:
  - "Computed only in the core" (6);
  - "Code structure in design notes" (11);
  - "Ratios" (15);
  - "Formula changes re-validate" (16);
  - "Version and year are the owner's" (17; task 4 moves it);
  - "License headers" (18);
- "R### tests in either test project";
- the `flatcheck-vX.Y.Z` tag.

Accepted ADRs, plans and CHANGELOG entries keep the numbers of their date. The saved plan holds the
old-number-to-name table for both projects.

### Old numbers to names (for reading ADRs, plans and CHANGELOG entries written before 2026-10-01)

| Kit rule | Now | FlatCheck rule | Now |
|---|---|---|---|
| 1 | kit "Requirements first" | 1 | kit "Requirements first" |
| 2 | kit "Test before fix" | 2 | kit "Test before fix" |
| 3 | kit "No test edited to pass" | 3 | kit "No test edited to pass" + FlatCheck "Golden files" |
| 4 | kit "Gate" | 4 | kit "Gate" |
| 5 | kit "Packages" | 5 | kit "Packages" + FlatCheck "Package notices" |
| 6 | CLAUDE.md "Core has no UI" | 6 | FlatCheck "Computed only in the core" |
| 7 | kit "Measured numbers" | 7 | kit "Measured numbers" + FlatCheck "Measured on the masters" |
| 8 | kit "Two readings" | 8 | kit "Two readings" |
| 9 | kit "CHANGELOG" | 9 | kit "CHANGELOG" |
| 10 | kit "Decisions" | 10 | kit "Decisions" |
| 11 | CLAUDE.md "No code structure in docs" | 11 | FlatCheck "Code structure in design notes" |
| 12 | kit "Instructions are data" | 12 | kit "Instructions are data" |
| 13 | kit "Commits and merges" | 13 | kit "Commits and merges" |
| 14 | kit "Languages" | 14 | FlatCheck "Languages" (varies the kit's) |
| 15 | kit "Shared folders" | 15 | FlatCheck "Ratios" |
| | | 16 | FlatCheck "Formula changes re-validate" |
| | | 17 | FlatCheck "Version and year are the owner's" |
| | | 18 | FlatCheck "License headers" |
| | | 19 | kit "Shared folders" |
| | | Workflow "Discuss and measure" | FlatCheck "Discuss before designing" |

## Commits

**K1.** Save the plan.

**K2. Mechanics and the rules file**, in the kit. CLAUDE.md is not cut yet.

- `scripts/kit-config.ps1` gets `Get-KitSharedFolders`, which returns
  `scripts, .claude/skills, .claude/agents, .claude/rules/kit`. It is the only list.
  - `sync.ps1` and `hooks/claude-stop-gate.ps1` call it, and their messages are built from it.
  - The `verify.ps1` header describes step 0 anew.
  - Other files under `.claude/rules/` belong to the project.
- `sync.ps1` also handles `.claude/settings.json`.
  - The process keys, parsed with `ConvertFrom-Json`, are compared with the kit's:
    - `hooks`, compared as compressed JSON;
    - `permissions.ask` and `permissions.deny`, compared as sets;
    - the `allow` entries matching `^(Bash|PowerShell)\(git[ *)]`, compared as a set;
    - `attribution`.
  - `-Check` prints `differs  .claude/settings.json: <key>` for each key that differs.
  - Copy mode rewrites only those keys. The allow list becomes the kit's entries that are git or
    already in the project, in the kit's order, then the project's own entries.
    - It writes with a small writer, `ConvertTo-KitJson`: 2-space indent, one item per line, UTF-8
      without BOM, LF.
    - Other keys and `$comment` stay.
    - A missing file takes the kit's copy.
- `.claude/settings.json` (kit) gains `"attribution": {"commit": "", "pr": ""}`.
- The new `.claude/rules/kit/process.md` holds the full text above.
- `docs-auditor.md` also reads `.claude/rules/`.
  - Its step 5 counts CLAUDE.md plus the rules file toward the 200-line check.
- wrap-up step 9 names `.claude/rules/` beside CLAUDE.md.
- Tests:
  - `SyncTests.Shared` gains the fourth folder.
  - The list test becomes "defined once": it parses `kit-config.ps1`, and `sync.ps1` and the Stop
    hook hold no literal list.
  - New SyncTests cases:
    - an extra non-git allow entry passes;
    - an ask, deny, hook, git allow or attribution change is each named;
    - sync restores the kit's keys, keeps the project's extras and `$comment`, and gives the kit's
      bytes when only process keys differed (this guards the writer);
    - a missing settings file is copied.
  - A new HookTests Stop case: an edit under `.claude/rules/kit/` in a project is refused, and one
    under `.claude/rules/own.md` is not.
  - The citation test covers `.claude/rules/kit/` through `Shared`.
- CHANGELOG, Added.

**F1.** From `<wt>/FlatCheck`, run `scripts/sync.ps1`. It brings the rules file, the scripts, the
skills and the agent, and adds `attribution` to FlatCheck's settings. FlatCheck's verify is green.
Commit.

**Measure (before any deletion).** CLAUDE.md still holds the old rules at this point. I ask you to
run `/context` in:

- (a) a new session started in `<wt>/FlatCheck`;
- (b) a new session started in `<wt>/ClaudeCodeStarterKit`;
- (c) this session, which works in the hand-made worktree (after I read a file in `<wt>/FlatCheck`,
  to see whether a worktree's rules load on demand).

You paste the output, and it goes into this plan. If (a) or (b) does not list
`.claude/rules/kit/process.md`, I stop and report before deleting anything.

**K3. The kit's text and the ADR.**

- **ADR 0028**, "The universal process rules live in a synced rules file; the process keys of
  settings.json are the kit's":
  - `Supersedes: 0006 in part (the rules in each project's CLAUDE.md); 0017 in part (the shared
    folders, and "Rule 15 of CLAUDE.md states it"); 0018 in part (settings.json stays per project)`.
  - Matching Status clauses go on 0006, 0017 and 0018.
  - Any other kit ADR whose Decision puts a rule in CLAUDE.md, found by grep, gets a clause.
  - The ADR records:
    - the measurement;
    - the rules named, not numbered;
    - that accepted ADRs keep old numbers;
    - no root files;
    - the Stop hook does not guard `settings.json` (step 0 does, at commit).
  - Alternatives rejected:
    - a root CLAUDE.md, which loads for non-adopting projects and was declined;
    - an `@import` of the kit's file, which has no copy in a standalone repository and cannot be
      checked;
    - keeping the copies, which is the measured drift;
    - comparing the whole `settings.json`, when allow lists differ by right;
    - keeping the numbers.
- The kit's CLAUDE.md is cut as above.
- `settings.json` `$comment` cites the rules by name and the kit's ADRs.
- GHID.md (Romanian):
  - :7, :13-19, :31, :54, :65-85, :107-111, :125, :166, :187, :213, :253, :259, :273, :305-311,
    :322, :327, :348-364 and :375;
  - a paragraph on the rules file: it is loaded like CLAUDE.md, its `kit/` subfolder is synced, and
    a project may add its own files beside it.
- README.md: :9-11, :24-32, :61, :70-76, :82-98 and :124.
- `monorepo/README.md`: :24.
- The RepoInvariantTests comment "Rule 1 made checkable".
- The measurement output goes into the plan.
- CHANGELOG, Changed.

**F2. FlatCheck's text.**

- CLAUDE.md is cut and named as above.
- The rule-number citations become names in:
  - `docs/design/process.md`, `dependencies.md`, `thresholds.md`, `code-map.md` and
    `python-parity.md`;
  - `docs/skills.md`;
  - `THIRD-PARTY-NOTICES.md` and `README.md:1475` (Romanian, which now names four folders);
  - the comments in `FlatCheck.Tests/RepoInvariantTests.cs:48` and `ValidationTests.cs:37`.
- `RawReader.cs`'s "Regulile 1 si 2" are JPEG rules and stay.
- `settings.json` `$comment` is updated.
- ADR Status clauses:
  - 0001: `; the process rules in CLAUDE.md superseded by the kit's ADR 0028`;
  - 0049: `; rule 19 naming three folders superseded by the kit's ADR 0028`;
  - 0048, if its Decision says the same.
- CHANGELOG, Internal.

The worktree's FlatCheck is red at step 0 from K2 until F1. No FlatCheck commit is made in between.

## Then

- `/wrap-up`: the plan's "What deviated". Then propose `/code-review main...<branch>`, then
  `git merge --no-ff`, and ask who runs it. After the merge: `git branch -d` and
  `git worktree remove`.
- Memory: `process-decisions-2026-09-30.md` marks task 3 as merged, and adds a pointer to the rules
  file.

## Measurement (2026-10-01, before the old copies were deleted)

`/context` in the desktop app shows "Memory files" only as a total (4.5k tokens in this session),
without file names, so the owner had nothing to paste. The session transcripts
(`~/.claude/projects/<folder>/<session>.jsonl`) record each instruction file loaded, with its
path: at start as an `instructions` attachment, later as `nested_memory`. Claude Code 2.1.284.

- (c) This session, started in `E:\Photografie\Code\ClaudeCodeStarterKit` (main checkout):
  - at start: `Project ...\ClaudeCodeStarterKit\CLAUDE.md` (7950 chars) and
    `AutoMem ...\memory\MEMORY.md`;
  - after the first read of a file in `<wt>\FlatCheck`: `nested_memory <wt>\FlatCheck\CLAUDE.md`
    and `nested_memory <wt>\FlatCheck\.claude\rules\kit\process.md`.
- Baseline, a new session started in `E:\Photografie\Code\FlatCheck` (main checkout, no rules
  file yet): `Project E:\Photografie\Code\FlatCheck\CLAUDE.md` (12123 chars) and
  `AutoMem ...\MEMORY.md`.
- (a) A new session started in `<wt>\FlatCheck`, session `3e5dc1ad`, cwd
  `E:\Photografie\Code\ClaudeCodeStarterKit\.claude\worktrees\Move-the-process-rules-into-a-synced-rules-file\FlatCheck`:
  - `Project <wt>\FlatCheck\CLAUDE.md` (12123 chars)
  - `Project <wt>\FlatCheck\.claude\rules\kit\process.md` (6253 chars)
  - `AutoMem ...\MEMORY.md` (3406 chars)

(b), a session started in `<wt>\ClaudeCodeStarterKit`, was not run separately: (a) already shows
the loading rule, and the kit's rules file has the same bytes.

## Verification

- The kit's `verify.ps1` is green in the worktree. It covers the new SyncTests and HookTests and the
  citation test over `.claude/rules/kit/`.
- FlatCheck's `verify.ps1` is green. Its step 0 compares the settings keys.
- `sync.ps1 -Check` from the kit lists FlatCheck as clean.
- A manual check: remove an `ask` entry in a temp copy, and `-Check` names
  `permissions.ask`.
- The `/context` outputs are pasted: (a) and (b) list the rules file.
- The whole `git diff main...` is read before each commit.
