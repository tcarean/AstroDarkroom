# Review the whole code and list what to fix

## Context

LightsCheck's first code was written by Opus and had almost no code review. The owner wants the
whole project read once, critically, and one documented list of problems at the end, so that
later sessions can fix them one at a time in order of severity. Measured on 2026-11-13, blank
lines included: 16,507 lines of code (`src/LightsCheck.Core/` 11,492, `src/LightsCheck.Cli/`
567, `src/LightsCheck.Wpf/` 4,448) and 10,912 of tests. That does not fit one context read with
care, so the code is read by area, each area by a reviewer with a clean context, and the
findings are merged into one list.

FlatCheck and ImageVisualCompare were reviewed the same way on the same day (FlatCheck's ADR
0100); this plan follows theirs so the three lists read alike.

This task **changes no code**. It writes one findings document, one ADR, one line in
`CLAUDE.md` and this plan.

## Scope

In: `src/` (the folder `kit.json` lists as code), and the tests of each area, read for one
question: does a test check what its name says, or can it not fail.

Out:
- `scripts/`, `.claude/` - the kit's; a problem there is fixed in the kit.
- `prototype/` - frozen (ADR 0003).
- How strong the tests are line by line: `/mutate` measures that.

The files of `src/LightsCheck.Core/Imaging/` marked "copied from FlatCheck unchanged" are read,
because LightsCheck runs them, but a defect there is fixed in FlatCheck first (the rule "Copied
files stay copies"). Such a finding goes in a section of its own, with the entry of FlatCheck's
list that already names it, when one does.

## The seven areas

Six were announced in chat; with the file list in hand the window splits better in two (what
decides, and what draws), so there are seven. Each reviewer reads every file of its area whole,
plus the design notes named, plus the area's tests.

| # | Area | Files | Lines | Design notes |
|---|------|-------|------:|--------------|
| 1 | One frame | `Analysis/FrameAnalyzer`, `FrameMeasurement`, `Raster`, `Components`, `StarShape`, `StarCrops`, `PixelPitch`, `Thresholds`, `Runs/FrameRun`, `Report/FrameReport` | 2,079 | `frame-analysis` |
| 2 | The series | `Analysis/Registration`, `Dithering`, `SeriesAnalyzer`, `PatternAnalyzer`, `NoiseAnalyzer`, `Wcs`, `Runs/SeriesRun` | 2,801 | `series-analysis` |
| 3 | The series report and the language | `Report/SeriesReport`, `SeriesOutputs`, `Language/` | 2,132 | `series-analysis`, `language` |
| 4 | Session, console, log | `Session/`, `Analysis/CadenceAnalyzer`, `Runs/InventoryRun`, `Report/InventoryReport`, `RunReport`, `src/LightsCheck.Cli/`, `Logging/`, `Version`, `ThirdParty` | 2,621 | `session-and-console`, `logging` |
| 5 | Imaging | `Imaging/` (ten files; `RawReader` and `ImageLoader` are LightsCheck's) | 2,426 | `imaging` |
| 6 | Window: view-models and shell | `ViewModels/`, `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `Views/AboutWindow`, `Loggers`, `ScreenArea`, `UserSettings` | 2,123 | `window`, `language`, `logging` |
| 7 | Window: views, presentation, theme | the five tab views of `Views/`, `Presentation/`, `Theme/Dark.xaml` | 2,325 | `window`, `language` |

## Steps

1. **Worktree and branch**, from the LightsCheck folder of the main checkout. The name
   `claude/Review-the-whole-code` is taken by another project's open task, so:
   `git worktree add .claude/worktrees/Review-the-whole-LightsCheck-code -b claude/Review-the-whole-LightsCheck-code main`.
   Everything below happens in that worktree. This plan is saved there.

2. **Seven reviewers in parallel** (general-purpose subagents, this session's model, read-only:
   no edit, no build, no test run). Each brief gives the area's files, its notes, its tests, and
   these instructions:
   - Read every file of the area whole. Read `CLAUDE.md`, the notes and the area's requirements
     first; something a note, an ADR or a requirement says is deliberate is not a finding (name
     it only if the code does not do what the note says).
   - Look for: wrong results (formula, index, off-by-one, unit, sign, NaN, division by zero,
     empty input, integer overflow), wrong verdict or wrong threshold use, crashes and unhandled
     errors, anything written into the session's `lights/` or `biases/`, files overwritten or
     left half-written, resource leaks, races and thread affinity (window), stale state after a
     session change or a cancelled run, culture-dependent parsing or formatting, a text that
     says something the number does not, the two languages out of step, dead or duplicated
     code, code that contradicts its own comment or its design note, a test that cannot fail or
     does not check what its name says.
   - A finding needs a concrete case: which input or sequence of actions, what comes out, what
     should. No case, no finding.
   - Report each as: title; severity 1-4; `file:line`; what the code does; the case; whether an
     existing test covers the spot; confidence (read and certain / plausible, needs a run).
     Cleanup items in a separate short list.

3. **I verify every finding in the code** before it enters the list: open the cited lines, follow
   the callers, check the note and the requirement, and for a window finding check it can be
   reached from the screen. Outcome per finding: confirmed, rejected (with the reason, kept out
   of the list), or "to measure" (a formula or a timing claim that only a run on the sessions
   or a test can settle; the fix session starts by measuring). Duplicates across areas are
   merged.

4. **The seams, read by me**: what `Runs/` returns against how the console and `MainViewModel`
   use it (null results, error outcomes, cancellation, paths, the language argument), and the
   thresholds against the texts that quote them.

5. **Write `docs/review/2026-11-13-code-review.md`**, in the form of FlatCheck's list:
   - How the review was done, what was in and out of scope, what the states of evidence mean.
   - A summary table: ID, severity, reach, evidence, one-line title, status (`open`).
   - One section per finding, ordered by severity then reach: ID `CR-001`..., `file:line`, what
     happens, the case, what should happen, the direction of a fix (not the fix), the test that
     would reproduce it, state of the evidence.
   - A section for the copied files of `Imaging/`, to be fixed in FlatCheck first.
   - A last section, "Cleanup", short entries.
   - How the list is used: one finding (or one tight group) per task; the task follows the rules
     "Requirements first", "Test before fix" and "Measured on the sessions"; it sets the entry's
     status to `fixed` with the commit, or `rejected` with the reason.

   Severity, the scale of FlatCheck's list (the fifth level named in chat, cleanliness, is the
   cleanup section):
   1. A wrong figure or a wrong verdict: a frame left out or kept wrongly.
   2. A crash, a hang, or a file overwritten or lost.
   3. A wrong text or a wrong display.
   4. Robustness and cleanliness (duplicated or dead code, a fragile construct).

6. **ADR 0046** (number checked again at commit time): the whole-code review of 2026-11-13, its
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

Seven subagents, each reading 2,000 to 2,800 lines of code plus notes and tests, then my
verification pass: a large run. It stays in one session so the list is one.

## What deviated

- The seams (step 4) were read while the reviewers worked, not after their reports, so the
  wait was used; the three seam findings were each reported by a reviewer too.
- The check of step 3 opened the lines of every entry. The test notes grouped in CR-058 to
  CR-060 and the cleanup list were spot-checked, not opened one by one; the list says so.
- No finding was rejected; one went to the cleanup list. Nothing was run, so "to measure"
  covers every entry that rests on a screenshot, a run or the reviewers' Python replicas.
- The area 5 reviewer read LibRaw's public sources on the web; that was not in the brief and
  is said in the list.
