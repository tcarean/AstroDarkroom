# Task 5 of 6: discuss before designing becomes a kit rule (Q5)

## Context

Decision Q5 of the programme plan (`ClaudeCodeStarterKit/docs/plans/2026-09-30-apply-the-process-decisions.md`), taken by the owner in chat on 2026-09-30.

FlatCheck has carried the practice since 2026-09-25 (commit 7fb607c). It sat in the Workflow section, and since task 3 (4dea9ef) it is two named rules:
- "Discuss before designing";
- "Measured on the masters".

The kit's rules say neither. The memory note `flatcheck-siril-preprocess-button.md` still points at FlatCheck's Workflow section, which the rule has left.

Q5 moves the practice into the kit's synced rules file and the `/new-feature` skill. Each project then keeps only what is its own.

## Facts checked (2026-10-01, main at 0ee4fe7)

- The kit's `.claude/rules/kit/process.md` is 90 lines and 6560 bytes. It is byte-identical in FlatCheck, and so is `new-feature/SKILL.md`.
- FlatCheck's `CLAUDE.md` is 132 lines. `RepoInvariantTests` caps it at 200.
- No test reads the text of the two rules or of step 3. The citation test (`SyncedFiles_CiteTheKitsAdrsAndNoRuleNumbers`) covers the rules file and the skills.
- **Supersessions: none, on either side.**
  - No ADR in the kit or in FlatCheck decides "discuss before designing", "re-asking" or "measure before design". This was checked by grep over both `docs/decisions/`.
  - FlatCheck 0049 and kit 0018 mention "measure before you design" only in their Context, which is history (the kit's ADR 0024).
  - So ADR 0030 has no `Supersedes:` line, and no Status line changes.
- `check-decisions.ps1` passes today: `ok 29 decisions` in the kit, `ok 55 decisions` in FlatCheck.
- The rule name "Discuss before designing" is cited only in FlatCheck's `CLAUDE.md:80` and in the task-3 plan, which is history.
- FlatCheck's `docs/skills.md` step 3 already says "measure on the real masters before you design" and gives the evidence ("wrong three times"). It stays as it is.
- No branch `claude/*` and no worktree besides the main checkout.

## Setup

- Branch: `claude/Make-discuss-before-designing-a-kit-rule`.
- Worktree, made by hand from `E:\Photografie\Code\ClaudeCodeStarterKit`:
  `git worktree add .claude/worktrees/Make-discuss-before-designing-a-kit-rule -b claude/Make-discuss-before-designing-a-kit-rule main`
- Commits use `git -C <wt>/<project> commit`.
- This plan is saved as `ClaudeCodeStarterKit/docs/plans/2026-10-01-make-discuss-before-designing-a-kit-rule.md`.

## Changes (exact texts)

**`.claude/rules/kit/process.md`** (synced). Each rule gains one sentence at its end:

- **Measured numbers.** "... A validation claim comes with the pasted output that shows it. **A design is chosen after a measurement on real data (the kit's ADR 0030).**"
- **Two readings.** "... Otherwise decide, and state the assumption. **A design choice with more than one reasonable answer: measure, present the facts and the options, and wait; once answered, implement without asking again (the kit's ADR 0030).**"

**`.claude/skills/new-feature/SKILL.md`** (synced), step 3. The text changes from "...then, and not before, go on to the plan and the code." to "...then, and not before, go on to the plan and the code **without re-asking**." Nothing else in step 3 changes.

**Kit ADR 0030**, `docs/decisions/0030-design-choices-are-measured-put-to-the-owner-and-not-asked-again.md`:
- Header: `Date: 2026-10-01`, `Status: accepted 2026-10-01`, no Supersedes line.
- **Context:**
  - the first adopter's standing instruction since 2026-09-25;
  - its notes record a design chosen without a measurement that was wrong three times;
  - what the kit's two rules did not cover;
  - the memory note already out of date;
  - the owner's Q5 of 2026-09-30.
- **Decision:**
  - the two sentences;
  - what "without asking again" means: the answered choice is not put again, and no leave to start is asked. The plan approval ("Plan first"), the merge ("Commits and merges") and a new fact that contradicts the answer, put as a new question with that fact, are still asked;
  - a project names its real data in a `CLAUDE.md` rule that extends "Measured numbers" when they are not obvious;
  - the skill phrase;
  - no project ADR (the kit's ADR 0025).
- **Alternatives rejected:**
  - only in the adopter's `CLAUDE.md`, because copies drift (ADR 0028);
  - only in memory, which is per machine (ADR 0001);
  - a separate rule, which would be a third statement of two existing rules;
  - letting the model choose and state the assumption;
  - confirming once more before code.
- **Consequences:**
  - it reaches every project by sync;
  - no mechanism checks it; it is judgment the owner sees in the session.
- It names no adopter's ADR numbers or rule names.

**`GHID.md`** (Romanian), in "Cerinte, decizii si validare":
- line 180: `/new-feature` asks at the decision step, and after the answer it goes on without re-asking;
- a new paragraph after it, "Intai masurat si intrebat, apoi construit" (ADR 0030). It covers:
  - the why: designs that were wrong when chosen without a measurement, and the cost of re-asking;
  - what is still asked;
  - its history in FlatCheck;
  - "real data" is the project's to name.
- The flowchart node `C` becomes "masuratoare pe date reale, apoi ADR daca exista o alegere reala".
- `README.md` says nothing about these rules and is not changed.

**Kit `CHANGELOG.md`**, a Changed line: the two rules and the skill step (ADR 0030).

**FlatCheck** (after `scripts/sync.ps1`):
- `CLAUDE.md`:
  - "Discuss before designing" is deleted, since the kit's "Two readings" now says all of it;
  - "Measured on the masters" (extends the kit's rule "Measured numbers") keeps only "Here the real data are the masters `docs/validation.md` lists."
- `CHANGELOG.md`, one Internal line ("The kit's ADR 0030: ...").
- No FlatCheck ADR, since FlatCheck does not deviate.

**Memory** (outside the repo, at wrap-up):
- `flatcheck-siril-preprocess-button.md` keeps only pointers:
  - the Siril run-button decisions are FlatCheck ADRs 0034 and 0035, and 0055 for its command line;
  - discuss and measure before design is the kit's rules "Two readings" and "Measured numbers" (the kit's ADR 0030).
- Its `MEMORY.md` line is updated to match.
- `process-decisions-2026-09-30.md` marks task 5, merged once it is.

## Assumptions (the rule "Two readings": decided, stated; say if you want otherwise)

1. The rule sentences are your words, made into sentences. The scope of "without asking again" goes in ADR 0030, not in the rule. In the rule it would add a clause to every session's context for a reading that "Plan first" and "Commits and merges" already force.
2. "Measured on the masters" stays as one line. "Which data are real" is the part the kit's rule does not say. `docs/skills.md` says it only inside `/new-feature`, while `CLAUDE.md` is loaded for every design.
3. GHID gets the "why", per the kit `CLAUDE.md` line "a rule added that needs a why gets it there".
4. `/new-feature` step 3 gets only the phrase, with no reordering.

## Commits

1. **K1:** the plan.
2. **K2:** the rules file, the skill, ADR 0030, GHID and the CHANGELOG. From here the worktree's FlatCheck is red at step 0, and no FlatCheck commit is made until F1.
3. **F1:** in `<wt>/FlatCheck`, run `scripts/sync.ps1`, then make the `CLAUDE.md` cut and add the CHANGELOG line.

## Then

- `/wrap-up`:
  - add "What deviated" to the saved plan;
  - update the memory notes;
  - suggest `/code-review main...claude/Make-discuss-before-designing-a-kit-rule`;
  - propose `git merge --no-ff` and ask who runs it.
- After the merge: `git branch -d`, then `git worktree remove`.

## Verification

- The kit's `scripts/verify.ps1` is green in `<wt>/ClaudeCodeStarterKit`. The decisions step shows 30 ADRs, and the citation test runs over the new text.
- FlatCheck's `scripts/verify.ps1` is green in `<wt>/FlatCheck`:
  - step 0 is clean after the sync;
  - the decisions step shows 55 ADRs;
  - the `CLAUDE.md` 200-line test passes.
- The `check-decisions.ps1` output for both projects is pasted.
- `git diff main...` is read in full before each commit.

## Known and left out

- `AI Prompt to Generate Starter Project.md` still lists the rules by number with their old substance. Tasks 3 and 4 left it too: it lacks "Version and year". Bringing it in step is a task of its own, and I will offer it as a separate task at the end.
- In the kit's `CHANGELOG.md`, a `### Fixed` heading inserted on 2026-09-26 (d1b3062) leaves the older Added items of `[Unreleased]` under Fixed. This is pre-existing, and I will offer it as a separate task.
- If FlatCheck 2.3.0 is released before this merge, the FlatCheck CHANGELOG line moves to the new `[Unreleased]` during the merge.

## What deviated

- Before the review, nothing of substance: the texts were the plan's. Measured then, on
  2026-10-01:
  - the kit: `ok 30 decisions in docs/decisions/`; `verify.ps1` `== OK in 140s`, 123 tests;
  - FlatCheck: `sync.ps1 -Check` identical with the kit after the sync, which copied exactly
    `.claude/rules/kit/process.md` and `.claude/skills/new-feature/SKILL.md`;
    `ok 55 decisions in docs/decisions/`; `verify.ps1` `== OK in 132s`, 946 + 331 tests;
    `CLAUDE.md` 129 lines, 132 before.
- After `/code-review`, fifteen findings. The owner chose, in chat, to keep FlatCheck's wider
  rule, took the recommended answer on two more, and added that options always come with a
  recommendation. All fixed on the branch except the last item:
  - FlatCheck keeps "Discuss before designing", cut to what the kit's rule does not say: every
    design, not only a choice with more than one reasonable answer. This plan's "the kit's "Two
    readings" now says all of it" was wrong, since the old rule had no such condition. ADR 0030's
    bullet "keeps only what the kit's rules do not say" covers it, so FlatCheck writes no ADR.
  - A new general kit rule, "Questions to the owner": a new fact that contradicts an answer goes
    back to the owner as a new question, and options always come with a recommendation. The
    exception for a new fact had been left in the ADR by assumption 1, whose reason covered only
    the approval of the plan and the merge. A first fix put both sentences inside "Two
    readings"; the owner then asked for them as a general rule, for every question, not only a
    design choice. Step 3, ADR 0030, GHID (and its list of rule names) and both CHANGELOGs follow.
  - Step 3: a choice answered in an earlier session is accepted by the approval of the plan that
    names its ADR.
  - ADR 0030 no longer says that projects inherited neither sentence: `/new-feature` step 3
    already asked on such a choice and said to measure first. It now records the sharper test
    the 2026-09-30 session rejected as circular ("a choice that would get an ADR"), and that
    memory keeps a pointer applied by hand in the projects without the kit, which the first
    memory rewrite of this task had dropped.
  - The kit's CHANGELOG and GHID say that the measurement holds for every design; GHID's review
    table gains it for a design finding; GHID line 180 is back as on `main`.
  - FlatCheck's real data are the masters and raws listed in `docs/validation.md`, and a
    calibrated light for `--light`.
  - Line 7 of this plan is half right: at 7fb607c only "Discuss and measure" sat in the Workflow
    section; "A design is chosen after a measurement on the real masters" was rule 7, in Rules.
  - Left as found: lines 73 and 78 quote Romanian GHID text, as a merged plan does.
