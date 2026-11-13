# Review the whole code and list what to fix

## Context

DarksCheck's first code was written by Opus and had almost no code review. The owner wants the
whole project read once, critically, and one documented list of problems at the end, so that
later sessions can fix them one at a time in order of severity. Measured on 2026-11-13, blank
lines included: 19,331 lines of code (`src/DarksCheck.Core/` 13,544, `src/DarksCheck.Cli/` 530,
`src/DarksCheck.Wpf/` 5,257) and 11,698 of tests. That does not fit one context read with
care, so the code is read by area, each area by a reviewer with a clean context, and the
findings are merged into one list.

FlatCheck, AstroPhotoAnalyzer and LightsCheck were reviewed the same way on the same day
(LightsCheck's ADR 0046); this plan follows theirs so the lists read alike. The owner approved
the approach in chat ("Da, porneste"), with the copied files and the tests included.

This task **changes no code**. It writes one findings document, one ADR, one line in
`CLAUDE.md` and this plan.

## Scope

In: `src/` (the folder `kit.json` lists as code), and the tests of each area, read for one
question: does a test check what its name says, or can it not fail.

Out:
- `scripts/`, `.claude/` - the kit's; a problem there is fixed in the kit.
- `prototype/` - frozen (ADR 0003).
- How strong the tests are line by line: `/mutate` measures that.

The files of `src/DarksCheck.Core/Imaging/` are read, because DarksCheck runs them, but a
defect there is fixed in LightsCheck (or FlatCheck through it) first (the rule "Copied files
stay copies"). Such a finding goes in a section of its own, with the entry of LightsCheck's or
FlatCheck's list that already names it, when one does.

## The nine areas

Six were announced in chat, the tests as one of them. With the file list in hand the report
(one file of 2,058 lines) and the window (5,257) each need a reviewer or two of their own, and
the tests are better read by the reviewer who has just read the code they test, as in
LightsCheck's review; so there are nine areas and no separate one for the tests. Each reviewer
reads every file of its area whole, plus the design notes named, plus the area's tests.

| # | Area | Files | Lines | Design notes |
|---|------|-------|------:|--------------|
| 1 | The dark series | `Analysis/Planes`, `FrameStack`, `Normalisation`, `DarkSeriesAnalyzer` | 2,209 | `dark-analysis` |
| 2 | Biases, leaks, verdicts, thresholds | `Analysis/BiasAnalyzer`, `LeakAnalyzer`, `StackNoise`, `CameraFacts`, `DarkVerdicts`, `Thresholds` | 1,724 | `dark-analysis`, `lights-and-index` |
| 3 | The lights, the index, the run | `Analysis/LightsAnalyzer`, `IndexAnalyzer`, `Runs/DarkSeriesRun` | 1,468 | `lights-and-index`, `dark-analysis` |
| 4 | The report of the dark series | `Report/DarkReport`, `ReportRows` | 2,318 | `dark-analysis`, `lights-and-index` |
| 5 | Session, console, language | `Session/`, `Analysis/CadenceAnalyzer`, `MatchAnalyzer`, `Runs/InventoryRun`, `Report/InventoryReport`, `RunReport`, `Language/`, `Version`, `ThirdParty`, `src/DarksCheck.Cli/` | 2,195 | `session-and-console` |
| 6 | Files written, scale, log | `Report/DarkOutputs`, `ScaleReport`, `Logging/`, `src/DarksCheck.Wpf/Loggers` | 1,892 | `logging`, `dark-analysis` |
| 7 | Imaging | `Imaging/` (ten files, copies) | 2,299 | `imaging` |
| 8 | Window: view-models and shell | `ViewModels/`, `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `Views/AboutWindow`, `UserSettings`, `Presentation/ScreenArea`, `Mvvm` | 2,463 | `window`, `logging` |
| 9 | Window: views, presentation, theme | the seven tab views of `Views/`, the rest of `Presentation/`, `Theme/` | 2,763 | `window` |

## Steps

1. **Worktree and branch**:
   `git worktree add .claude/worktrees/Review-the-whole-DarksCheck-code -b claude/Review-the-whole-DarksCheck-code main`.
   Everything below happens in that worktree. This plan is saved there.

2. **Nine reviewers in parallel** (general-purpose subagents, this session's model, read-only:
   no edit, no build, no test run). Each brief gives the area's files, its notes, its tests, and
   these instructions:
   - Read `CLAUDE.md`, the notes and the area's requirements first; something a note, an ADR or
     a requirement says is deliberate is not a finding (name it only if the code does not do
     what the note says). Then read every file of the area whole.
   - Look for: wrong results (formula, index, off-by-one, unit, sign, NaN, division by zero,
     empty input, integer overflow, a normalisation applied twice or not at all), a wrong
     verdict or a threshold used the wrong way round, assumptions that hold only for a Nikon
     D3300, crashes and unhandled errors, anything written into the session or library folder,
     files overwritten or left half-written, resource leaks, races and thread affinity
     (window), stale state after a folder change or a cancelled run, culture-dependent parsing
     or formatting, a text that says something the number does not, the two languages out of
     step, dead or duplicated code, code that contradicts its own comment or its design note, a
     test that cannot fail or does not check what its name says.
   - A finding needs a concrete case: which input or sequence of actions, what comes out, what
     should. No case, no finding.
   - Report each as: title; severity 1-4; reach; `file:line`; what the code does; the case;
     whether a document calls it deliberate; whether an existing test covers the spot;
     confidence (read and certain / plausible, needs a run). Cleanup items in a separate list.
   - Areas 5, 6, 8 and 9 are given the titles of the entries LightsCheck's list has for the
     same kind of code, which DarksCheck's was patterned on, to check whether each holds here.
     Area 7 is given LightsCheck's and FlatCheck's lists, to say which entries hold in
     DarksCheck's copies.

3. **I verify every finding in the code** before it enters the list: open the cited lines, follow
   the callers, check the note and the requirement, and for a window finding check it can be
   reached from the screen. Outcome per finding: confirmed, rejected (with the reason, kept out
   of the list), or "to measure" (a formula or a timing claim that only a run on the sessions
   or a test can settle; the fix session starts by measuring). Duplicates across areas are
   merged.

4. **The seams, read by me**: what `Runs/` returns against how the console and `MainViewModel`
   use it (null results, error outcomes, cancellation, paths, the language argument), where the
   files of `--out` can land, and the thresholds against the texts that quote them.

5. **Write `docs/review/2026-11-13-code-review.md`**, in the form of LightsCheck's list:
   - How the review was done, what was in and out of scope, what the states of evidence mean.
   - A summary table: ID, severity, reach, evidence, one-line title, status (`open`).
   - One section per finding, ordered by severity then reach: ID `CR-001`..., `file:line`, what
     happens, the case, what should happen, the direction of a fix (not the fix), the test that
     would reproduce it, state of the evidence.
   - A section for the copied files of `Imaging/`, to be fixed in their source first.
   - A last section, "Cleanup", short entries.
   - How the list is used: one finding (or one tight group) per task; the task follows the rules
     "Requirements first", "Test before fix" and "Measured on the sessions"; it sets the entry's
     status to `fixed` with the commit, or `rejected` with the reason.

   Severity, the scale of the other three lists:
   1. A wrong figure or a wrong verdict: a dark set kept or redone wrongly, a wrong index.
   2. A crash, a hang, or a file overwritten, lost or written where it must not be.
   3. A wrong text or a wrong display.
   4. Robustness and cleanliness (duplicated or dead code, a fragile construct).

6. **ADR 0052** (number checked again at commit time): the whole-code review of 2026-11-13, its
   list in `docs/review/`, the severity scale, one finding per task. One line in `CLAUDE.md`'s
   Layout for `docs/review/`.

7. **Gate, commit, merge proposal**: `scripts/verify.ps1` in the worktree, read the diff, commit
   on the task's branch, propose the merge and ask. No CHANGELOG line (a document, no
   behaviour); each fix adds its own.

## Verification

- Every entry of the list cites a `file:line` that I opened myself; none is copied from a
  reviewer unread.
- Every file under `src/` belongs to exactly one area (checked by listing the files against the
  table before the reviewers start).
- The findings document says how many reports each area returned and how many were rejected, so
  the yield is visible.
- `scripts/verify.ps1` green before the commit; `git diff main --stat` shows documents only.

## Cost, said plainly

Nine subagents, each reading 1,500 to 2,800 lines of code plus notes and tests, then my
verification pass: a large run. It stays in one session so the list is one.

## What deviated

- The seams (step 4) were read while the reviewers worked, not after their reports, so the
  wait was used; the three seam findings were each reported by a reviewer too.
- The check of step 3 opened the lines of every entry. The lists inside CR-073 to CR-075 (the
  tests and the notes) and the cleanup section were spot-checked, not opened one by one; the
  list says so.
- 110 findings were reported; one was rejected (behaviour ADR 0048 decided). Nothing was run in
  the project, so "to measure" covers every entry that rests on a screenshot, a run, an outside
  source or the reviewers' replicas.
- Six reviewers worked outside the repository, read-only: throw-away scripts in the temp
  folder (areas 1 to 4), and for area 7 LibRaw's public sources on the web and a reflection
  listing of the shipped binding, for area 8 two source files of WPF. The brief allowed it and
  the list says which figures are theirs.
- The copied files got twelve entries of their own (CP-1 to CP-12), since the area 7 reviewer
  was handed LightsCheck's and FlatCheck's lists and said which entries hold here.
- The entries were written per area as each report was checked, then put in order and numbered
  by a throw-away script, which also built the summary table from the entries' own headings.
