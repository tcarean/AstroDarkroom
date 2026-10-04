# Move the golden-diff rule into the kit

## Context

AstroPhotoAnalyzer's ADR 0027 (accepted 2026-10-02, on `main` at 6478b30) made the assistant, not
the owner, read and judge the golden-file diffs. The owner asked for it on 2026-10-02, in
Romanian; translated: "Can't you read the golden files yourself and judge whether the changes
are the expected ones? I would rather you did that from now on."

The kit's shared text still says the opposite, and every project loads it:
- `/new-feature` step 6: "A golden file ... is re-approved only after the owner has read the diff
  and said yes".
- `/wrap-up` step 2: "so is a golden file that changed".
- `GHID.md:221`, in Romanian: the model does not regenerate them without an explicit OK.

On 2026-10-04 the owner asked to move the rule into the kit and align the projects.

The rule: after a regeneration, the assistant reads the whole diff and ties every changed line to
its cause in the change. It reports in the wrap-up what changed and why. A line that moved without
a cause is a finding: stop and ask, as under "No test edited to pass", and do not re-approve it.
A golden is never changed to make a test pass.

The owner's answer in plan mode (2026-10-04) on FlatCheck's 43 `Golden/parity_*.txt`: they stay
figures. Any changed line there is a finding, cause or not, and the owner re-approves it.

## Facts checked (2026-10-04, `main` at 6478b30)

- **One repository.** The `photo` monorepo (git root `E:\Photografie\Code`) holds the kit and
  the three adopters. So there is one task branch, with one commit per project.
- `git worktree list` shows no branch for this task.
- **Read-only checks pass today.** `check-decisions.ps1` reports, per project:

  | Project | ADRs |
  |---|---|
  | kit | 32 |
  | AstroPhotoAnalyzer | 27 |
  | FlatCheck | 55 |
  | ImageVisualCompare | 26 |

  `sync.ps1 -Check` is identical with the kit in all three adopters.
- **ImageVisualCompare.**
  - `claude/Adopt-the-starter-kit-process` is already merged into `main` (1bdc38f), so IVC has
    `kit.json` and the shared folders on `main`. The sync therefore goes on `main` through this
    task's branch.
  - `claude/Copy-the-licence-next-to-the-program-and-fix-stale-texts` (9a8eccd, 780802f) touches
    17 files, none under `scripts/` or `.claude/`. It needs no sync. Merged before or after this
    branch, the result keeps the synced copies, and step 0 stays green.
  - IVC has no golden files.
- **Kit ADRs.** None of them decides the owner's read. 0013 and 0018 name goldens only as
  examples, so ADR 0033 supersedes no kit ADR.
- **FlatCheck ADRs.** None decides it either. ADR 0047 says a parity re-approval is "a C#-only
  decision", and R-098 says the owner reads that diff; both stay as they are.
- **AstroPhotoAnalyzer.** 0027 is the only ADR to supersede, in whole.
- **Places that still say the owner reads the diff:**
  - FlatCheck:
    - `CLAUDE.md:26` (Commands) and `:74-76` (rule "Golden files");
    - `docs/skills.md:24-26` (`/new-feature` step 6);
    - `docs/design/process.md:223` (step 6 of "Adding a golden scenario");
    - `docs/requirements.md` R-098 (kept, per the owner's answer).
  - AstroPhotoAnalyzer: `docs/requirements.md` R-100 ("changes only by re-approving them after
    the owner has read the diff"). It already contradicts ADR 0027.
  - Plans and older ADRs say it too; they are history and are not edited.
- **Tests.** No test reads the text of these rules or steps. The citation test
  (`SyncedFiles_CiteTheKitsAdrsAndNoRuleNumbers`) covers the rules file and the skills.
- **Sizes.**
  - `CLAUDE.md`: 169 lines in APA and 132 in FlatCheck, against a cap of 200.
  - `process.md`: 102 lines.
- **Style of kit ADRs.** Since 0025, kit ADRs name no adopter and no adopter's ADR numbers
  (kit ADR 0025). They also contain no Romanian.

## Setup

- Branch `claude/Move-the-golden-diff-rule-into-the-kit`, worktree made from
  `E:\Photografie\Code\ClaudeCodeStarterKit`:
  `git worktree add .claude/worktrees/Move-the-golden-diff-rule-into-the-kit -b claude/Move-the-golden-diff-rule-into-the-kit main`
- Work and commit with `git -C <wt>/<project>`.
- This plan is saved as `ClaudeCodeStarterKit/docs/plans/2026-10-04-move-the-golden-diff-rule-into-the-kit.md`.
- **Antivirus.** The WPF builds of FlatCheck, APA and IVC run under
  `ClaudeCodeStarterKit\.claude\worktrees\Move-the-golden-diff-rule-into-the-kit\`. If a freshly
  built dll is blocked there, I stop and tell you which folder needs the exception. I do not
  touch security settings.

## Changes

### The kit (`<wt>/ClaudeCodeStarterKit`)

**New ADR** `docs/decisions/0033-the-assistant-reads-and-judges-the-golden-diffs.md`.

Header:
- Title: "The assistant reads and judges the golden diffs; a line without a cause is a finding".
- `Date: 2026-10-04`, `Status: accepted 2026-10-04`, no `Supersedes:`.

Context:
- What a golden file is. The three shared texts that contradicted each other.
- "No test edited to pass", read as written, forbids even an intended regeneration.
- 2026-10-02 in "an adopter": five files and about a hundred lines, regenerated twice; the owner
  saw summaries; the review flagged the rule; the owner's words, translated.
- That adopter's own ADR the same day, while the shared text kept saying the opposite.
- The owner's two instructions of 2026-10-04: move the rule, and keep goldens with confirmed
  numbers as figures.

Decision:
1. A golden changes only with an intended change of its output, never to make a test pass.
2. After a regeneration the assistant reads the whole diff and ties every changed line to its
   cause. The wrap-up reports the lines and causes, or "none". A line with its cause is
   re-approved without waiting for the owner, who reads the report and may read the diff.
3. A line without a cause is a finding: say so and stop, and do not re-approve it.
4. A golden whose numbers were confirmed outside the code it tests (by an independent
   implementation or on real data) is a figure as well. Every changed line there is a finding
   and the owner decides. The project's `CLAUDE.md` names such goldens.
5. Where the rule lives:
   - one sentence of "No test edited to pass" in the rules file, plus `/new-feature` step 6 and
     `/wrap-up` step 2;
   - the project names its golden files and the command that regenerates them in `CLAUDE.md`;
   - no project ADR for the rule (the kit's ADR 0025); a project ADR that recorded it is
     superseded.

Alternatives rejected:
- The owner reads every diff: declined, and in practice already lapsed.
- In each `CLAUDE.md` only: the shared text said the opposite in the same session.
- In the skills only: they load only when invoked, and the rules file forbade the regeneration.
- Confirmed numbers judged like the rest: a cause explains a moved number but does not confirm
  it; the owner chose figures.
- Drop the goldens: they are the only whole-output review.

Consequences:
- Synced to every project.
- The wrap-up report grows one item.
- A project's `CLAUDE.md` keeps where its goldens are, the command, and which carry confirmed
  figures.
- No mechanical check; the owner reads the judgment in the report.

**`.claude/rules/kit/process.md`, rule "No test edited to pass"**

Decision asked of me: yes, the rules file names goldens.
- A golden is regenerated wherever an output changes, in a fix as much as in a feature. The
  skills load only when invoked.
- The rule as written ("never change a fixture to make something pass") forbids the regeneration
  that every intended output change needs. That is the contradiction the task removes.
- The cost is four lines in a file every session loads.

The rule gains, after "the owner decides.":
> A golden file, a whole output a test compares, changes only with an intended change of that
> output: read its whole diff, tie every changed line to its cause in the change, and report
> them at wrap-up, without waiting for the owner. A line without a cause is a wrong test; any
> changed line of a golden whose numbers were confirmed outside the code is a moved figure (the
> kit's ADR 0033).

**`.claude/skills/new-feature/SKILL.md`, step 6.** The golden sentence becomes:
> A golden file, where the project has them, is regenerated only for an intended change of its
> output: read its whole diff, tie every changed line to its cause in the change, and carry the
> lines and causes into the wrap-up report. A line without a cause, or any changed line of a
> golden the project's `CLAUDE.md` names as carrying confirmed figures, is a finding: say so and
> stop (the kit's rule "No test edited to pass", the kit's ADR 0033).

**`.claude/skills/wrap-up/SKILL.md`, step 2.** The `/validate` part stays. "; so is a golden file
that changed, where the project has them" becomes:
> Where the project has golden files, read every one the task changed line by line and report
> each changed line with its cause in the change, or that none changed. A line with its cause is
> reported, not asked about; a line without one is a finding, and so is any changed line of a
> golden the project's `CLAUDE.md` names as carrying confirmed figures, a moved figure (the
> kit's ADR 0033).

**`GHID.md`** (Romanian, no diacritics, as the guide is):
- **Line 221** becomes the rule plus its reasons:
  - the owner's words in the original;
  - why the assistant reads instead (a complete line-by-line read; the owner needs the verdict);
  - why the rules file and not only the skills;
  - the confirmed-figures exception.
- **The `/wrap-up` step list (lines 242-252)** gains the step it lacked, the skill's step 2
  (`/validate`, a moved figure, the goldens read line by line). The rest is renumbered 3-12 to
  match the skill; nothing else in the guide cites those numbers.
- `README.md` says nothing about goldens or this rule; it is unchanged.

**`CHANGELOG.md`**, a line at the top of `### Changed`:
- the new rule (ADR 0033), where it is written, and what it replaces (the owner's OK in the
  guide, the owner's read in `/new-feature`).

### AstroPhotoAnalyzer (`<wt>/AstroPhotoAnalyzer`, after `scripts/sync.ps1`)

- **ADR 0027:** Status becomes `superseded by the kit's ADR 0033`. Only the Status line changes
  (the kit's ADR 0024).
- **`CLAUDE.md`, Commands:**
  `- Golden console texts, after an intended output change (the rule "Golden files"):` then the
  `APA_UPDATE_GOLDEN=1` command.
- **`CLAUDE.md`, rule "Golden files"**, which keeps only the project's part:
  `(extends the kit's rule "No test edited to pass", the kit's ADR 0033). The golden files are
  the console snapshots AstroPhotoAnalyzer.Tests/Golden/*.txt, regenerated with
  APA_UPDATE_GOLDEN=1.`
- **`docs/skills.md`:**
  - `/new-feature` step 6 keeps "The golden files are regenerated with `APA_UPDATE_GOLDEN=1` (the
    rule "Golden files", the kit's ADR 0033)".
  - `/wrap-up` step 2 drops its golden sentence, since the kit's step now says it.
- **`docs/requirements.md` R-100:** "after the owner has read the diff" becomes "as the rule
  "Golden files" says". It is a process clause, not behaviour, and it contradicts ADR 0027 today.
- **`CHANGELOG.md`:** the existing unreleased Internal line on ADR 0027 now cites "the kit's ADR
  0033, which supersedes ADR 0027". There is no new line, since the behaviour is the same.

### FlatCheck (`<wt>/FlatCheck`, after `scripts/sync.ps1`)

- **`CLAUDE.md`, Commands:**
  `- Golden console texts, after an intended output change (the rule "Golden files"):` then the
  `FLATCHECK_UPDATE_GOLDEN=1` command. The parity parenthetical moves into the rule.
- **`CLAUDE.md`, rule "Golden files":**
  `(extends the kit's rule "No test edited to pass", the kit's ADR 0033). The golden files are
  the console snapshots FlatCheck.Tests/Golden/*.txt, regenerated with FLATCHECK_UPDATE_GOLDEN=1.
  The 43 Golden/parity_*.txt carry figures the Python prototype confirmed before it froze, so
  they are figures too: any line that changes there is a finding, and the owner re-approves it, a
  C#-only call (ADR 0047, R-098).`
- **`docs/skills.md`:**
  - `/new-feature` step 6: "The golden files are regenerated with `FLATCHECK_UPDATE_GOLDEN=1`; a
    changed `Golden/parity_*.txt` waits for the owner (the rule "Golden files", R-098)".
  - `/wrap-up` step 2 is unchanged; it already makes a changed parity golden a finding.
- **`docs/design/process.md`, step 6** ("The owner reads the diff and approves it"): "The wrap-up
  report names the new file and every changed line of an existing one with its cause (the rule
  "Golden files")".
- **Unchanged:** R-098 and ADR 0047. No ADR's Status changes.
- **`CHANGELOG.md`**, one Internal line:
  - "The kit's ADR 0033: ..."
  - the parity goldens stay the owner's.

### ImageVisualCompare (`<wt>/ImageVisualCompare`)

- `scripts/sync.ps1` only. It copies `process.md` and the two skills.
- No `CHANGELOG.md` line: IVC has no golden files, so nothing changes for its history.

## Assumptions (the rule "Two readings": decided, stated)

1. **No FlatCheck ADR.** The general rule is the kit's ADR 0033. That the parity goldens carry
   confirmed figures is ADR 0047's, and that the owner rules on them is R-098's.
2. **The kit ADR is anonymous.** It names neither adopter nor their ADR numbers (the kit's ADR
   0025), and it quotes the owner in English (the rule "Languages"). `GHID.md` keeps the
   Romanian original.
3. **Edits beyond your list,** because each still states the old rule:
   - R-100 in APA;
   - in FlatCheck, `docs/skills.md` `/new-feature` step 6 and `docs/design/process.md` step 6;
   - in GHID, the step list.
4. **CHANGELOG decisions:**
   - the kit: a Changed line;
   - FlatCheck: an Internal line;
   - APA: the existing line re-cited;
   - IVC: none.

## Commits on `claude/Move-the-golden-diff-rule-into-the-kit`

1. **K1:** this plan.
2. **K2:** in the kit, ADR 0033, the rules file, the two skills, `GHID.md` and `CHANGELOG.md`.
   From here the worktree's adopters are red at step 0 until their own commit.
3. **A1:** AstroPhotoAnalyzer, the sync plus its edits.
4. **F1:** FlatCheck, the sync plus its edits.
5. **I1:** ImageVisualCompare, the sync.

Each commit message is one line, under 100 characters, and says why. The commit hook runs the
gate of the project each commit touches.

## Then

`/wrap-up` runs its steps once per project folder (kit, APA, FlatCheck, IVC):
- the gate;
- the CHANGELOG decision;
- requirements (R-100's text only, no status moves);
- decisions (what each edit to an ADR on `main` is: APA 0027, Status only);
- `check-design.ps1 -Since main`;
- the README and GHID check;
- "What deviated" at the end of this plan.

Memory, at wrap-up:
- `golden-diffs-read-by-assistant.md` becomes a pointer to the kit's ADR 0033 and the parity
  answer.
- `imagevisualcompare-kit-adoption-plan.md` notes that the adoption is merged (1bdc38f) and the
  fix branch is not.

Then I suggest `/code-review main...claude/Move-the-golden-diff-rule-into-the-kit`. I propose one
merge, `git merge --no-ff claude/Move-the-golden-diff-rule-into-the-kit` in
`E:\Photografie\Code`, and ask who runs it. After the merge: `git worktree remove`, then
`git branch -d`.

## Verification

- `scripts/verify.ps1` is green in each of the four project folders of the worktree. I paste the
  last lines of each.
- `check-decisions.ps1` gives these counts. APA's "superseded by the kit's ADR 0033" resolves
  against the worktree's kit.

  | Project | ADRs |
  |---|---|
  | kit | 33 |
  | APA | 27 |
  | FlatCheck | 55 |
  | IVC | 26 |

- `sync.ps1 -Check` is identical in the three adopters.
- A grep for "owner has read the diff", "owner read the diff" and "re-approved only after" over
  the four folders finds only R-098 and history (plans, older ADRs).
- `git diff main...` is read in full before each commit.

## Known and left out

- **Merged branches of another session.** IVC's adoption branch and its worktree
  (`ImageVisualCompare/.claude/worktrees/Adopt-the-starter-kit-process`) are merged but still
  there. They belong to another session, so I leave them; you can remove them with
  `git worktree remove` and `git branch -d`.
- **The parked APA branch** `claude/Recommend-the-lift-that-puts-the-dust-on-paper-black` touches
  only `AstroPhotoAnalyzer/`. It is not affected beyond what `main` already holds.

## What deviated

- **Measured on 2026-10-04, by the commit hook's gate at each commit:**

  | Project | Gate | Tests | Decisions |
  |---|---|---|---|
  | kit | `== OK in 119s` | 131 | `ok 33` |
  | AstroPhotoAnalyzer | `== OK in 27s` | 290 + 40 | `ok 27` |
  | FlatCheck | `== OK in 116s` | 946 + 331 | `ok 55` |
  | ImageVisualCompare | `== OK in 16s` | 149 | `ok 26` |

  In each adopter, `sync.ps1` copied exactly `.claude/rules/kit/process.md` and the two skills.
  The WPF tests ran in this worktree with no antivirus block. `CLAUDE.md` went from 169 to 168
  lines in APA and from 132 to 133 in FlatCheck.
- **Wording only, no change of substance:**
  - The rules-file sentence says "report the lines and their causes" instead of "report them".
  - The guide's golden line keeps "au o procedura explicita de regenerare" from the old line.
  - FlatCheck's rule dates the confirmation (2026-09-26).
- **Found at wrap-up, not in the plan.** The plan's grep covered `docs/` and `CLAUDE.md` only, so
  it missed two files. `AstroPhotoAnalyzer.Tests/testdata/README.md` and
  `FlatCheck.Tests/testdata/README.md` still said the goldens are re-approved "after the owner
  has read the diff". Both now point at the rule "Golden files", in a wrap-up commit.
- **The first commit was refused** by the Claude commit hook, because the worktree path came
  through a shell variable. It ran again with the path written out.
