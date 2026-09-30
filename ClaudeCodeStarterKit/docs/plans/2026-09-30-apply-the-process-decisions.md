# Apply the process decisions of 2026-09-30 - task 1 of 6: ADR format check and citing the kit

## Context

A read-only analysis in this session compared the kit's 23 ADRs with FlatCheck's 55. The shared
process reaches projects well where it is synced (scripts/, skills, agent) and drifts where it is
copied by hand (process rules in each CLAUDE.md, settings.json, mirror ADRs). The worst concrete
defect: synced files cite bare "rule 15", "ADR 0013", "ADR 0021", which in FlatCheck are other
rules and decisions (FlatCheck rule 15 = "Every metric is a ratio"; FlatCheck ADR 0013 = analysis
centre). Nine questions were answered by the owner in chat on 2026-09-30 (translated from
Romanian): "For Q2 do only variant C. For Q8, do not bind me too. For the rest, I go with
your recommendations."

This plan (1) records the nine decisions in the repo so later sessions find them, (2) splits the
work into six tasks, one per session, each writing its own ADR, and (3) details task 1.
Assumption (rule 8): the decisions live in this plan until their task writes the ADR; task 1
writes only the ADRs of Q6 and Q1.

## The nine decisions (owner, in chat, 2026-09-30)

- **Q1 - no mirror ADRs.** Kit process decisions live only in the kit. A project cites them as
  "the kit's ADR NNNN" and writes its own ADR only for a deviation or a project value; a kit
  decision that reaches a project through sync gets no project ADR. New kit ADRs do not name
  adopters' ADR numbers. The docs-auditor also reads the kit's docs/decisions/ (via kit.json
  `kit`). Existing FlatCheck ADRs stay as they are; nothing is backfilled.
- **Q2 - variant C only.** Universal rules move to a kit-owned, synced
  `.claude/rules/kit/process.md` (measured with /context first); each CLAUDE.md keeps project
  content; the shared-folder list is defined once; step 0 also checks the process keys of
  settings.json, which gains `attribution` empty. No root CLAUDE.md in photo.
- **Q3 - version.** The owner may bump per feature (FlatCheck ADR 0039). release.ps1's floor is
  the newest released CHANGELOG heading (no tags exist): it refuses a version not above it,
  below `<Version>`, already a heading or already tagged. /release step 2 proposes `<Version>`
  when the owner already bumped. New universal rule: the version and the copyright year change
  only at the owner's word.
- **Q4 - where a release runs.** A session the owner opens in the project folder of the main
  checkout, on main. release.ps1 refuses off main; the skill drops "(or the branch the owner
  names)"; `git tag*` goes to `ask`; skill step 6 checks HEAD did not move and proposes the
  commit (limited to Directory.Build.props and CHANGELOG.md) and the tag. One ADR with Q3.
- **Q5 - discuss before designing.** One sentence added to rule 8 ("a design choice with more
  than one reasonable answer: measure, present facts and options, wait; once answered, implement
  without asking again"), rule 7 gains "a design is chosen after a measurement on real data",
  /new-feature step 3 gains "then go on without re-asking". Memory keeps a pointer.
- **Q6 - ADR lifecycle.** Until merged into main an ADR is edited freely. After: a typo, an
  unclear phrase or a renamed path is fixed in place; a wrong figure or a retired reason, with
  the decision standing, gets a dated "(Corrected|Restated YYYY-MM-DD: ...)" note outside
  `## Decision`; anything that changes what is done is a superseding ADR, and the old one changes
  only its Status. One Status/Supersedes grammar, checked by a synced script in verify.
- **Q7 - rule 11.** Any file may name code as an address; what code does and how its parts fit
  goes only in the design note of its area (a `Covers:` line, updated or deleted with the code).
  Adopt FlatCheck ADR 0051 in the kit; one whole-project code map allowed; a synced
  check-design.ps1 and a wrap-up step.
- **Q8 - main-checkout guard.** The repo-wide pre-commit refuses a commit on a `claude/` branch
  in the main checkout (git-dir equals git-common-dir) **only when `$CLAUDECODE` is set**: it
  binds the model, not the owner.
- **Q9 - app worktrees.** Not used in photo; task worktrees are made by hand (ADR 0019). An ADR
  supersedes the app-worktree clauses of kit 0013, 0019, 0020. The three app worktrees under
  FlatCheck/.claude/worktrees are merged and clean (checked 2026-09-30); removing them is the
  owner's command.

## The six tasks (one session each, in this order)

1. **Q6 + Q1** (this plan, below): kit ADRs 0024, 0025; no FlatCheck ADR (see the deviations).
2. **Q8 + Q9**: pre-commit guard with GitHookTests, .githooks refresh, worktree wording, one ADR.
3. **Q2**: the synced rules file, shared list defined once, settings.json keys at step 0, one ADR.
4. **Q3 + Q4**: release.ps1 floor and branch check, skill steps, `git tag` ask, one ADR; then
   `/release` for FlatCheck 2.3.0 (the owner commits and tags).
5. **Q5**: rule 7/8 text in the rules file, new-feature step, one ADR, memory pointer.
6. **Q7**: rule 11, check-design.ps1, wrap-up step, design README and GHID, one ADR.

Rule texts from tasks 4-6 go into the synced rules file that task 3 creates.

## Task 1 - Q6 + Q1

Branch `claude/Check-ADR-headers-and-cite-the-kit`, in a hand-made worktree of the kit folder
(FlatCheck ADR 0044: a change spanning kit and project goes there), created from
`E:\Photografie\Code\ClaudeCodeStarterKit`:
`git worktree add .claude/worktrees/Check-ADR-headers-and-cite-the-kit -b claude/Check-ADR-headers-and-cite-the-kit main`.
It is a full photo checkout; the kit and FlatCheck are folders in it. Commits use
`git -C <wt>/<project> commit` (the gates run twice per commit: Claude hook + git pre-commit).
The ADRs are written `accepted 2026-09-30`: the owner said yes to the decisions in chat and
approves this plan; he reads the diff before the merge.

### Deviations from the recommendations as given in chat (please confirm by approving)

- **No FlatCheck ADR 0056.** FlatCheck ADR 0001:20 already says FlatCheck adopts the kit's
  process; a new "kit ADRs apply" ADR would be exactly the mirror Q1 forbids.
- **Rule 3:** FlatCheck's test `Decisions_AreNumberedOnceAndCarryAStatus` is **removed**, not
  trimmed: the synced check covers numbering, title and Status, and its regex rejects the
  partial-supersession form. Approving this plan is the owner's yes for that test change.
- **No "withdrawn" status.** An abandoned decision gets a short superseding ADR (GHID.md:182
  rewritten to say so).

### ADR header grammar (kit ADR 0024)

- Status: `proposed` | `accepted DATE` | `superseded by REF`, the last two optionally followed by
  clauses `; <part> superseded by REF[ (<note>)]`. REF is `NNNN`; in a project also
  `the kit's ADR NNNN` (validated against `<kit>/docs/decisions/` only when step 0 would run).
- Supersedes: items `NNNN` or `NNNN in part (<part>)`, separated by `; ` or `, `; local numbers only.
- Both sides agree: whole `X` in N's Supersedes <-> X's Status head `superseded by N`;
  `X in part` <-> a clause `superseded by N` in X's Status; references exist; X < N.
- Also: file `NNNN-slug.md`, line 1 `# NNNN. ` with the file's number, `Date:` line, unique
  numbers, one `## Decision`, no dated `Corrected|Restated|Amended YYYY-MM-DD` note inside
  `## Decision` (lines joined, notes wrap). Template 0000 exempt. Read as UTF-8.
- Rule written in 0024: a later ADR that replaces, removes or makes false part of an earlier one
  supersedes that part; after the merge only the old Status line and the new Supersedes line
  are edited. Header bookkeeping may be completed later so both sides agree.

### Commits

1. **K1** Save this plan as `ClaudeCodeStarterKit/docs/plans/2026-09-30-apply-the-process-decisions.md`.
2. **K2** Kit ADRs:
   - **0024** (Q6): lifecycle and grammar. It supersedes 0001 in part ("not edited after
     acceptance") and 0003 in part (the list of steps now lives in the verify.ps1 header).
   - **0025** (Q1): cite, don't mirror; sync brings no project ADR; kit ADRs name no adopter
     numbers; the docs-auditor reads the kit's decisions. It supersedes 0009 in part (inputs)
     and says why the kit's ADRs are an auditor input while GHID stays out (ADR 0011:34-36): the
     ADRs decide a project's process, the guide only explains it.
   - Back-pointers:

     | ADR | Line | Change |
     |---|---|---|
     | 0001 | Status | `...; "not edited after acceptance" superseded by 0024` |
     | 0003 | Status | `...; the list of steps superseded by 0024` |
     | 0009 | Status | `...; the list of inputs superseded by 0025` |
     | 0010 | Status | `...; who runs the release commit and tag superseded by 0021` |
     | 0017 | Status | `...; the kit-only file list superseded by 0022; CheckStagedTests superseded by 0023` |
     | 0020 | Status | `...; check-staged and the CHANGELOG check superseded by 0023` |
     | 0021 | Status | `...; the 72-character limit and check-staged superseded by 0023` |
     | 0021 | Supersedes | `0007; 0010 in part (...)` |
     | 0022 | Supersedes (new line) | `0017 in part (...)` |
     | 0023 | Supersedes | adds 0017, 0020 and extends 0021 |
3. **K3** The check:
   - new synced `scripts/check-decisions.ps1`, modelled on `check-testdata.ps1` (header, `Read-KitConfig`, `Get-KitPath`, padded lines, exit 0/1), with no project name in it (`rename.ps1`);
   - `verify.ps1` gets step 'decisions' after 'test data manifest'; its header becomes "seven checks", and `-SkipTests` becomes "every step but the tests";
   - new `tests/Starter.Tests/DecisionsCheckTests.cs`, plain `[Fact]`, temp repo as in `SyncTests`, four runs: a valid set; one broken file per rule, each message named; a kit reference in a project passes or fails on the kit's folder; a kit reference inside the kit fails.
   - No test reads `../FlatCheck`.
4. **K4** Citations. Qualify every bare reference:
   - "(ADR NNNN)" becomes "(the kit's ADR NNNN)" in `verify.ps1:45`, `mutate.ps1:20`, `kit-config.ps1:11`, `claude-stop-gate.ps1:9`, `claude-commit-gate.ps1:10`, `install-hooks.ps1:19`, `pre-merge-commit:8`, and in `wrap-up` SKILL.md :39, :45, :46.
   - Rule numbers become the kit ADR or the rule's words:
     - `wrap-up:39` "(rule 15)" becomes "(the kit's ADRs 0017 and 0018)";
     - `release` SKILL.md:9 becomes "(the kit's ADR 0021)";
     - `release.ps1:21` becomes "...which the owner runs (the kit's ADRs 0010 and 0021)";
     - `check-testdata.ps1:25` becomes the rule's words, and `:20` "as its first step" becomes "before the build";
     - `claude-commit-gate.ps1:189` becomes the double-quoted message "(the kit's ADR 0020)", and `:208` becomes "(the kit's ADR 0021)";
     - `pre-commit:20` and `pre-merge-commit:19` become "the rules forbid that";
     - `commit-msg:7` becomes "(the kit's ADRs 0021 and 0023: ...)".
   - New `RepoInvariantTests.SyncedFiles_CiteTheKitsAdrsAndNoRuleNumbers` over `scripts/**`, the kit-only hooks included, `.claude/skills/**` and `.claude/agents/**`. Lines are joined, and it fails on `(?<!the kit's )\bADRs? \d{4}` and `\b[Rr]ules? \d+\b`.
   - Run the kit's `install-hooks.ps1` in the worktree to refresh `<wt>/.githooks` (the `hooksPath` value is unchanged), and commit it here.
5. **K5** Docs and skills:
   - `docs-auditor.md`: also reads `<kit>/docs/decisions/` when `kit` is not `.` (titles and Status first, bodies on demand). Step 6: a decision the kit made is answered by the kit's ADR.
   - `wrap-up` step 6: the three ways an ADR changes, and citing a kit decision, never mirroring it (the kit's ADRs 0024 and 0025).
   - `wrap-up` step 9: a project ADR superseded by a kit ADR gets its Status updated in the sync commit.
   - Kit `0000-template.md`: shows the grammar.
   - `GHID.md` (Romanian): :49, :91-99 (also the stale sentence that -SkipTests keeps only the first four steps), a gate-table row at :101-115, :165-182 (the boundary becomes "merged into main", the three classes, :182), :292 (auditor inputs), :321, and one paragraph on citing kit decisions near :346.
   - `README.md`: :69, :76, and a bullet under "Ce e impus mecanic".
   - `CHANGELOG.md`: an Added line (the decisions check) and a Changed line (the auditor reads the kit's decisions; synced files cite the kit's ADRs).
   - CLAUDE.md is not touched; task 3 puts the rule text into the synced rules file.
6. **F1** FlatCheck: run the kit's `sync.ps1` from the worktree's FlatCheck and commit. It is green: today's headers pass both the new check and the old test.
7. **F2** FlatCheck ADR bookkeeping:
   - Status lines of 0001 (by 0053), 0007 (by 0046), 0035 (by 0055), 0039 and 0042 (by 0047), and 0048 (by 0053).
   - Supersedes lines of 0046, 0047, 0053 and 0055.
   - Its `0000-template.md`.
   - Remove `Decisions_AreNumberedOnceAndCarryAStatus` (see the rule 3 deviation above).
   - One CHANGELOG Internal line.

Between K3 and F1, the worktree's FlatCheck is red at step 0. No FlatCheck commit happens in that window.

### After the commits

- `/wrap-up`: plan deviations, then suggest `/code-review main...claude/Check-ADR-headers-and-cite-the-kit`, then ask who runs the merge. After the merge: `git branch -d` and `git worktree remove` of this worktree.
- Memory: a pointer note to the programme plan, listing tasks 2-6 as pending.

## Verification

- `scripts/verify.ps1` green in `<wt>/ClaudeCodeStarterKit`. It runs the new decisions step on the kit's ADRs, `DecisionsCheckTests` and the citation test.
- `scripts/verify.ps1` green in `<wt>/FlatCheck`. It runs step 0 (sync -Check), the decisions step on 55 ADRs with the new back-pointers, and the remaining tests.
- `check-decisions.ps1` output pasted for both projects.
- Re-read `git diff main...` in full before each commit.

## Known and left out

- `verify.ps1` calls `powershell` by name, which fails on Linux CI. This is pre-existing, and the new step inherits it. (Fixed in the end, after the code review: see "What deviated".)
- Stale Consequences sentences in accepted ADRs (history) are not touched.
- The three non-adopting projects' commit sentences stay as they are, since Q2 was narrowed to C.

## What deviated (task 1)

- As approved with the plan: no FlatCheck ADR 0056, FlatCheck's `Decisions_AreNumberedOnceAndCarryAStatus` removed, no `withdrawn` status.
- The back-pointer criterion is now explicit in ADR 0024: only what a later ADR says it replaces, or what contradicts an earlier Decision, is recorded; a stale sentence in Context or Consequences is history. By that criterion more pairs were recorded than the plan listed: kit 0017 of 0003, 0018 of 0009, 0025 of 0018, FlatCheck 0047 of 0040. FlatCheck 0048 by 0053 was skipped at first and recorded after the review. Kit 0021's Status clause is split so its "(100 characters)" note stays on its own part.
- The plan's reading of ADR 0011 ("the guide only explains") was wrong: 0011 kept the guide out of the audit because it is not part of a project. ADR 0025 quotes that reason and answers it.
- A read-only review of the branch (six agents) found a real bug in `check-decisions.ps1`: the refs of a Status line were matched again without the end anchor, so a part containing "superseded by NNNN", or two clauses joined by ", ", were read differently from the grammar, passing a one-sided supersession. The refs now come from the match that validated the line, with tests for both cases. Also from the review: the header is case-sensitive, messages quote the line, 0000 is reserved for the template and a kit reference to 0000 fails, every rule in the script's header has a test, the citation test accepts "The kit's", a typographic apostrophe and "kit''s" and flags "ADR-0013" and lowercase forms, a correction note for text inside `## Decision` goes right after the section, and the Status update for a project ADR superseded by a kit ADR is the kit task's, on its branch.
- `pre-commit` and `pre-merge-commit` say "CLAUDE.md forbids that." rather than "the rules forbid that". The stale references to wrap-up "step 8" in `GHID.md` and `README.md` went while those lines were edited. ADR 0024's file name is shorter than first written.
- Left as found (review, low): FlatCheck 0002 and 0038 are marked wholly superseded although their successors keep a part of them; `GHID.md` says `/wrap-up` reviews the guide after a change to `scripts/` or `.claude/`, which wrap-up step 9 does not list (a finding of the analysis, for a later task).
- After `/code-review` (nine findings, all fixed on the branch): ADR 0017 gets a dated note beside "Supersedes nothing in the kit", which its new header contradicted; ADR 0024 no longer claims that no ADR lists the gate's steps; the owner's words in this plan are translated; the check requires a title after "# NNNN. " and keeps no unused regex groups; the rule-number guard catches "RULE 4", "rule-15" and "rule #15" like the ADR guard; the shared-folder list lives in `SyncTests.Shared`, which a test compares with `sync.ps1` and the Stop hook; one `KitJson` helper in `ScriptRunner`; and `verify.ps1` and `release.ps1` start child scripts with `Get-KitShell` (the running PowerShell), which also fixes the pre-existing Linux CI failure listed below.
