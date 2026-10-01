# Task 6 of 6: how code works lives in its design note, and a synced check keeps the notes true (Q7)

## Context

This task carries out decision Q7, which the owner settled in chat on 2026-09-30 (`ClaudeCodeStarterKit/docs/plans/2026-09-30-apply-the-process-decisions.md`, lines 48-51 and 68):

- Any file may name code as an address.
- What code does, and how its parts fit, goes only in the design note of its area. That note carries a `Covers:` line and is updated or deleted with the code.
- The kit adopts the first adopter's rule that a note repeats neither CLAUDE.md nor an ADR.
- One whole-project code map is allowed.
- A synced `check-design.ps1` and a `/wrap-up` step support the rule.

The recommendation the owner accepted that day draws the line between naming code and describing it. It also aligns the kit's design README and GHID, which today forbid listing classes.

On 2026-10-01 the owner answered six further questions:

- **Scope:** the project's code as `kit.json` names it, code and tests.
- **Covers form:** FlatCheck's existing form is accepted.
- **Listing:** `check-design.ps1 -Since main` lists the touched notes.
- **FlatCheck's index test:** retired.
- **What a check enforces:** any file may say what a test or a check enforces. This answers a new fact found while planning: the rules file, GHID, the READMEs and CLAUDE.md all say what `RepoInvariantTests` and `ThirdPartyTests` check.
- **Allow entry:** both `settings.json` files get one for the script.

This task runs in the session of task 5, at the owner's request ("Da-i drumul la taskul 6"). The kit's rule "One task per session" would start a new session, so this is recorded as a deviation, as task 4 was.

## Facts measured (2026-10-01, main at d09b1a4)

**Today's rule**

- The kit's CLAUDE.md has "No code structure in docs" (24882be). FlatCheck's has "Code structure in design notes" (7fb607c).
- No ADR Decision in either project states the rule (grep over both `docs/decisions/`).
- Four ADR Consequences mention the old state, which ADR 0024:74-76 treats as history: ADR 0028:79 (the two CLAUDE.md rules), 0024:71 ("seven checks"), and 0026:61 and 0027:45 ("/wrap-up step 10").
- These texts restate the rule: the kit's `docs/design/README.md` ("does not list classes or methods") and GHID :76, :82 and :299.

**FlatCheck's design notes**

- 21 notes and a README. All are in the index, as `` `name.md` `` in the Note column, and `RepoInvariantTests.EveryDesignNote_IsInTheIndex` checks that.
- Nothing checks the Covers lines.
- Seven notes (`modes-*.md`) have no `## `; their first section is `### `.
- Their `README.md` lists the gate's checks (:1456-1458) without `decisions`.

**A prototype parser, run inline read-only on the 21 notes (confirmed again by the plan review)**

- 23 `**Covers:**` passages: 21 in the opening quote block, 2 in plain paragraphs.
- 190 code spans, 111 of them path-like. 109 resolve with their exact letter case.
- The 2 that do not resolve are both the stale `Report/AgainstReport.cs`, in lens-profiles.md and modes-synthetic.md. That class lives in `Runs/AgainstRun.cs`, which both notes already list.
- Bases needed: the root, each code and test folder, `docs/design/`, and file names found anywhere (two notes name files that sit two folders down).
- Globs: `Golden/parity_*.txt` exists only under `FlatCheck.Tests/`, and `Th.SmoothTexture*` is a symbol.

**The kit and the rest**

- The kit's `docs/design/` holds only its README, whose index reads "(none yet)".
- Live `/wrap-up` step numbers: only FlatCheck's `docs/skills.md` ("At step 2"). A new step after step 9 keeps every live reference true.
- Synced scripts must be ASCII with LF line endings, run on PowerShell 5.1, contain no `starter` in any letter case (`rename.ps1`), and contain no bare "ADR NNNN" and nothing the rule-number pattern matches (for example `rule1`).
- The worktree path is about 130 characters, so the ADR file name stays under about 100 to keep the full path under Windows' 259 limit.

## Setup

- Branch `claude/Check-design-notes-and-their-covers`. Its worktree is made by hand from the kit folder: `git worktree add .claude/worktrees/Check-design-notes-and-their-covers -b claude/Check-design-notes-and-their-covers main`.
- This plan is saved as `ClaudeCodeStarterKit/docs/plans/2026-10-01-check-design-notes-and-their-covers.md`.

## Changes

**The rule.** It goes into the synced `.claude/rules/kit/process.md`, after "Decisions":

> **Design notes.** Any file may name the project's code (the folders and files `kit.json` lists as code and tests) and say what a test or a check enforces. How that code works and how its parts fit go only in the design note of its area: a note in `docs/design/` whose `Covers:` sentence, above its first section, names the files it describes, and which is updated or deleted in the same task as that code. A note repeats neither `CLAUDE.md` nor an ADR; one note may map the whole project (the kit's ADR 0031).

The kit's CLAUDE.md drops "No code structure in docs" and keeps "Core has no UI". FlatCheck's CLAUDE.md drops "Code structure in design notes". Both Commands sections gain a line for `scripts/check-design.ps1` (`-Since main` lists the notes a branch touched).

**`scripts/check-design.ps1`** (new, synced). Its header, `Read-KitConfig`, padded red lines and exit codes are modelled on `check-decisions.ps1`, with the SPDX line first and notes read as UTF-8.

- **File index.** One walk of the project builds an exact-case index of relative paths (files, plus folders with a trailing `/`) and a map from file name to paths.
  - The walk skips folders named bin, obj, StrykerOutput, artifacts, TestResults, .vs, .git and worktrees (the list in `RepoInvariantTests`).
  - Comparisons are ordinal, so Windows and Linux CI agree.
- **Notes.** Every `*.md` in `docs/design/` except `README.md`. Without the folder, the script prints a yellow line and exits 0.
- **Index check.**
  - Every note must be named in `README.md` as `` `name.md` ``. This uses FlatCheck's test pattern `` `([a-z0-9-]+\.md)` ``, outside fenced blocks.
  - Every name the pattern finds must exist.
  - A missing `README.md` is an `INDEX` line.
- **Covers check.**
  - Fenced blocks are dropped and the `> ` quote markers are stripped.
  - Each note needs a `Covers:` marker, bold or plain and outside inline code, before its first heading of level 2 or more. A note with no such heading counts as a whole.
  - Each passage runs from the marker to the end of its sentence. The end is the first `.` outside backticks followed by a space or a line end, a blank line, or a heading line. Wrapped lines are joined with spaces.
  - A span counts as path-like when:
    - it contains no whitespace, `<`, `>`, `{`, `}`, `\` or `:`, and does not start with `-`;
    - it has a `/`, or ends in one of these extensions: cs, csproj, props, targets, sln, xaml, json, md, py, ps1, txt, yml, yaml, xml, ico, png.
  - `*` and `?` make a glob only in a span with a `/`; `**` is not supported.
  - A path-like span resolves when:
    - base + span is in the index, as a file or a folder, for a base among the root, each code and test folder, and `docs/design/`;
    - for a glob, some base + glob matches an indexed file with `-clike`;
    - for a span without `/` that no base resolves, the file-name map has it.
  - Every other span is skipped.
- **Output.**
  - Red lines: `INDEX`, `COVERS` and `MISSING`, each with the note and the quoted span. Paths are printed with `/`.
  - Otherwise: `ok       N design notes in docs/design/`.
  - Exit 0 or 1.
- **`-Since <ref>`.** The script lists notes and checks nothing.
  - It runs `Push-Location $root`, then takes `base = git merge-base <ref> HEAD`.
  - The changed files come from two commands:
    - `git -c core.quotepath=off diff --name-only --relative --no-renames <base>`, which covers committed, staged and unstaged changes, including both sides of a rename and deleted files;
    - `git ls-files --others --exclude-standard`, for untracked files.
  - Git runs through the Invoke-Git pattern of `claude-commit-gate.ps1`: `Continue`, `$LASTEXITCODE`, and UTF-8 console output.
  - Only changed files under `kit.json`'s code folders, code files and test folders count.
  - A changed path matches a note's Covers span when it equals base + span, starts with base + span + `/` (a folder span), matches base + glob with `-clike`, or, for a bare span that no base resolves, has that file name. String comparison means a deleted file still matches.
  - Output, grouped by note: `touched  docs/design/<note>: <file>, <file>`, or `ok       no design note covers a changed file`.
  - A bad ref or a git failure prints a red `GIT` line and exits 1.

**`scripts/verify.ps1`.**

- A new step, `design notes`, runs after `decisions`.
- The header says "eight checks", renumbers the list, and changes "step 5 built" to "step 6 built".
- `-SkipTests` runs the new step too. The guide says so.

**`/wrap-up`.** A new step 10 goes after step 9, so steps 10 and 11 become 11 and 12:

> `docs/design/`: run `scripts/check-design.ps1 -Since main`. Read each note it lists against the change and update or delete what no longer holds, in this task. Code whose workings a reader needs and no note covers gets a note with a `Covers:` sentence and an index row. The gate checks the index and the paths, not the text.

**The kit's `docs/design/README.md`** is rewritten, in English. Its examples carry no backticked lowercase `.md` names. It says:

- what a note holds: mechanics and the reasons that have no other home. It may name classes and methods, and it repeats neither CLAUDE.md nor an ADR.
- the Covers sentence and the path forms it accepts.
- that "vs. " and "e.g. " end a Covers sentence early, and that a non-path span with `/` (such as `A/B`) does not belong in it.
- the index format: one row per area, with its notes in the Note column as `` `name.md` ``.
- that one note may map the whole project.
- what the check does, and its `-Since` mode.

**Kit ADR 0031**, `docs/decisions/0031-code-mechanics-live-in-design-notes-checked-by-a-script.md`.

- **Context.**
  - Read literally, the kit's rule "No code structure in docs" was broken from its first day: the ADR template asks for test names, and CLAUDE.md names `Program.Run`.
  - The first adopter's practice: its notes say at the top which code they cover, and repeat neither CLAUDE.md nor an ADR. Nothing checked this.
  - The measurement above, Q7, and the six answers of 2026-10-01.
- **Decision.**
  - The rule and its scope. GHID and the READMEs keep explaining the kit's own scripts (ADR 0011).
  - A test or check may be described by what it enforces.
  - The Covers form and the check, as a gate step.
  - `-Since` and the wrap-up step.
  - One whole-project code map is allowed.
  - A project retires its own index test.
- **Alternatives rejected.** Each with the deciding fact:
  - keep "No code structure in docs";
  - apply the rule to every file, scripts included;
  - a strict Covers form (78 of the first adopter's 111 path names would be rewritten);
  - a test in each project;
  - listing touched notes by hand;
  - checking a note's text.
- **Header.** No Supersedes line. The ADR names no adopter's ADR numbers and cites rules by name.

**Tests.** A new `tests/Starter.Tests/DesignCheckTests.cs`, plain `[Fact]`, on a temp repo as in `DecisionsCheckTests`, run through `ScriptRunner`. The git cases copy the helpers of `ReleaseTests`.

1. **A valid set passes.**
   - Two notes share one index row.
   - The Covers marker appears in three forms: bold in a quote block, plain, and at a line end.
   - The Covers spans include:
     - a root path, a code-folder path and a test-folder path;
     - a bare name two folders down, a folder with and without a trailing `/`, a glob under a test folder, and a sibling note;
     - a symbol, `Th.Smooth*`, a flag and a test name.
   - The set includes a CRLF note, a BOM note, a note with only `###` sections, a second Covers under a section, a `Covers:` inside inline code (ignored), and a fenced block in the README.
2. **Each broken rule is named, with its exact line.**
   - A note missing from the index.
   - The index naming a note that does not exist.
   - No README.
   - No Covers above the first section.
   - A span that names nothing.
   - A span with the wrong letter case.
3. **No `docs/design/`:** a yellow line and exit 0.
4. **`-Since`**, with the project in a subfolder of a git temp repo and the script run from another folder.
   - Listed:
     - a covered file committed on a branch;
     - a covered file left uncommitted;
     - an untracked file under a covered folder;
     - a deleted covered file;
     - a renamed covered file.
   - Not listed: an uncovered file, a changed note, and a change outside the project.
   - A bad ref gives a red `GIT` line and exit 1.

**Docs (Romanian).**

- GHID:
  - the verify and scripts rows (:51, :54);
  - :76, which drops to one own rule;
  - :82 and :299: naming code and saying what a check enforces are fine, and how code works goes in the area's note;
  - :89, which adds "Design notes" to the rule names;
  - the gate flowchart and table (:95-114), :128 and :129;
  - :234, :251 and the wrap-up list (:238-249), which gains the new step;
  - :297, :330 and :333 (a first note needs a Covers sentence and an index row);
  - :382/:384 (an adopting project with an existing `docs/design/` goes red at the new step until its notes have Covers).
- README:
  - :69;
  - the verify row (:73);
  - a bullet under "Ce e impus mecanic";
  - :130-131.

**Kit CHANGELOG (English).** Added: the rule, the check, `-Since` and the wrap-up step. Changed: CLAUDE.md drops its code-structure rule, and the design README is rewritten.

**`settings.json`** in both projects, as the owner approved: the project's own `allow` list gains `PowerShell(powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-design.ps1*)`.

**FlatCheck**, after `scripts/sync.ps1`:

- CLAUDE.md drops "Code structure in design notes" and gains the Commands line.
- `lens-profiles.md` and `modes-synthetic.md` drop `Report/AgainstReport.cs` and write "`Runs/AgainstRun.cs` (with `AgainstReport`)".
- `RepoInvariantTests` loses `EveryDesignNote_IsInTheIndex` (the owner's yes, given in chat).
- `README.md` (Romanian): the gate list at :1456-1458 gains the decisions and design-notes steps, and :1492-1494 mentions the Covers sentence and the check.
- `settings.json` gets the allow entry.
- CHANGELOG gets an Internal line, in English.
- No FlatCheck ADR. The kit adopts FlatCheck's practice; FlatCheck's ADR 0051 stays as it is, and its "Guards: none by test" is history.

## Commits

1. **K1:** the plan.
2. **K2:** the kit:
   - the rule, CLAUDE.md, the script, `verify.ps1` and the skill;
   - the design README and ADR 0031;
   - GHID, the README, the CHANGELOG, `settings.json` and the tests.

   From here until F1, the worktree's FlatCheck is red at step 0, and no FlatCheck commit is made.
3. **F1:** FlatCheck:
   - the sync, CLAUDE.md and the two notes;
   - the test removal, the README, `settings.json` and the CHANGELOG.

## Then

1. `/wrap-up`. Its new step runs on this branch.
2. `/code-review`.
3. Propose the merge, with a recommendation on who runs it.
4. After the merge: `git worktree remove`, then `git branch -d`, then the programme's memory note (all six tasks merged).

## Verification

- **Kit gate green:**
  - the new step prints "ok 0 design notes in docs/design/";
  - `DesignCheckTests` passes;
  - the citation test and `SyncTests` cover the new script.
- **FlatCheck before the note fix**, after the sync: `check-design.ps1` is red and names exactly the two `Report/AgainstReport.cs` spans. The output is pasted.
- **FlatCheck after F1:** the gate is green, with "ok 21 design notes in docs/design/".
- **Real-data listing.** In the worktree's FlatCheck after F1:
  1. Make a scratch shell edit to `FlatCheck.Core/Analysis/SmoothAnalyzer.cs`. It is not made through the Edit tool, so the Stop hook is not woken.
  2. Run `check-design.ps1 -Since HEAD`. Expect modes-smooth.md, smooth-validation.md and code-map.md, and paste the output.
  3. Run `git restore`.
- **The diff:** `git diff main...` is read in full before each commit.

## Known and left out

- **Stale code comments in FlatCheck.** Some comments cite CLAUDE.md sections that moved to `docs/design/` on 2026-09-25, for example `MetricsTests.cs:76` ("Parity with Python") and `ObservableObject.cs:25`. They predate this task; I will offer them as a separate task.
- **FlatCheck's CLAUDE.md Layout keeps each folder's role.** A role is an address with its responsibility (GHID :75), and it states FlatCheck ADR 0011's decision.
- **Two notes may both claim a file "in full",** for example the code map and an area note. The check does not judge that, nor a note's text; `/wrap-up` and `/docs-health` do.
- **The generator prompt's rule 11** is covered by the separate task already offered.

## What deviated

- As planned, the task ran in the session of task 5, at the owner's request; the kit's rule
  "One task per session" would have started a new one.
- Before K2 the new script ran on FlatCheck's real notes from a copy in the worktree's
  FlatCheck; the sync then found that copy identical. Measured on 2026-10-01, on the branch:
  - kit: `ok       0 design notes in docs/design/` (0.8 s);
  - FlatCheck before the fix, red on exactly the two expected spans (1.1 s):
    `MISSING    docs/design/lens-profiles.md: Covers names` `` `Report/AgainstReport.cs` ``
    and the same line for modes-synthetic.md; no commit ever held that file;
  - FlatCheck after the fix: `ok       21 design notes in docs/design/`;
  - `-Since HEAD` with a scratch edit to `FlatCheck.Core/Analysis/SmoothAnalyzer.cs`, then
    restored: code-map.md, modes-smooth.md and smooth-validation.md; `-Since nosuchref`: the
    red `GIT` line, exit 1;
  - gates: kit 129 tests, FlatCheck 945 + 331 (one test fewer: the index test retired);
    `/wrap-up` step 10 on this branch: no note covers a changed file, in either project.
- The tests group the cases a little differently from the list above: the missing README and
  the unknown ref have tests of their own.
- ADR 0031's title reads "How code works lives in the design note of its area; a synced script
  checks the notes". Its Consequences also record the `settings.json` allow entry, a choice
  the owner settled in chat.
- FlatCheck's `README.md` gate list also gained the decisions step, which it had lacked.
- Left as found: the kit CHANGELOG's earlier `[Unreleased]` line that lists the kit's two own
  rules, history now that this task's line says one of them went.
