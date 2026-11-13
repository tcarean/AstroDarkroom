# Review the whole code and list what to fix

## Context

FlatCheck's first code was written by Opus and had almost no code review. The owner wants the
whole project read once, critically, and one documented list of problems at the end, so that
later sessions can fix them one at a time in order of severity. Measured on 2026-11-12: about
28,800 non-blank lines of code (Core 14,414, console 748, window 13,622) and 23,600 of tests.
That does not fit one context read with care, so the code is read by area, each area by a
reviewer with a clean context, and the findings are merged into one list.

This task **changes no code**. It writes one findings document, one ADR, one line in
`CLAUDE.md` and this plan.

## Scope

In: `FlatCheck.Core/`, `FlatCheck/`, `FlatCheck.Wpf/` (the folders `kit.json` lists as code).

Out:
- `scripts/`, `.claude/` - the kit's; a problem there is fixed in the kit.
- `flatcheck.py`, `crosscheck.py` - frozen (ADR 0047). `lensfun/` - data.
- The tests, line by line. A reviewer opens the tests of its area only to see whether a
  suspected bug is already covered. Test strength is `/mutate`'s job.

## The ten areas

Eight were announced; with the file list in hand the Core root splits better by subject and the
view-models are too many for one reader, so there are ten. Each reviewer reads every file of its
area whole, plus the design notes named, plus `docs/design/known-issues.md` (70 lines).

| # | Area | Files | Lines | Design notes |
|---|------|-------|------:|--------------|
| 1 | Numeric core, structure and light | `Img`, `Stats`, `Metrics`, `Thresholds`, `Analysis/StructureAnalyzer`, `VignettingComparison`, `LightAnalyzer`, `LightSeriesAnalyzer`, `LightSuitability`, `SyntheticFlatBuilder` | 2,452 | `pipeline`, `radial-model-even-term`, `modes-structure`, `modes-light`, `thresholds` |
| 2 | Raw, exposure, histeq | `RawReader`, `PreviewReader`, `PreviewImage`, `IFrameReader`, `VcRuntime`, `CfaPattern`, `Mosaic`, `Analysis/ExposureAnalyzer`, `ExposureBands`, `HistEqAnalyzer` | 2,310 | `modes-expo`, `modes-histeq`, `siril-histeq-source`, `dependencies` |
| 3 | Files and log | `FitsReader`, `FitsWriter`, `ImageLoader`, `PngWriter`, `BitmapFont`, `Version`, `ThirdParty`, `Logging/` | 2,336 | `pipeline` (input contract), `png-output`, `logging` |
| 4 | Lens profiles | `LensProfiles/` | 1,923 | `lens-profiles`, `modes-synthetic` |
| 5 | Report and language | `Report/`, `Language/` | 3,529 | `language`, `thresholds` |
| 6 | Runs and console | `Runs/`, `FlatCheck/` | 2,612 | `two-applications`, `process`, `pipeline` |
| 7 | Window: session, Siril, shell | `Wpf/Session/`, `Interop/`, `Mvvm/`, `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `Loggers` | 3,424 | `two-applications`, ADR 0033-0037, 0055 |
| 8 | View-models A | `MainViewModel`, `TabViewModel`, `HeavyRunGate`, `OpeningStep`, `ExposureTab`, `HistEqTab`, `HistEqView`, `HistogramSource`, `LightTab`, `ImageZoom`, `About`, `Rows` | 2,478 | `two-applications`, ADR 0077, 0087 |
| 9 | View-models B | `StructureTab`, `StructureRows`, `SyntheticTab`, `SplitTab`, `Preprocess`, `SirilRun`, `MapGuide`, `MapScale`, `SensorFormat` | 2,993 | `two-applications`, `png-output`, ADR 0054 |
| 10 | Views, presentation, theme | `Wpf/Views/`, `Presentation/`, `Theme/Dark.xaml` | 4,727 | `two-applications`, `language` |

## Steps

1. **Worktree and branch**, from the FlatCheck folder of the main checkout:
   `git worktree add .claude/worktrees/Review-the-whole-code -b claude/Review-the-whole-code main`.
   Everything below happens in that worktree. Save this plan there as
   `docs/plans/2026-11-13-review-the-whole-code.md`.

2. **Ten reviewers in parallel** (general-purpose subagents, this session's model, read-only:
   no edit, no build, no test run). Each brief gives the area's files, its notes, and these
   instructions:
   - Read every file of the area whole. Read `known-issues.md` and the notes first; something a
     note, an ADR or a requirement says is deliberate is not a finding (name it only if the code
     does not do what the note says).
   - Look for: wrong results (formula, index, off-by-one, unit, sign, NaN, division by zero,
     empty input, integer overflow), wrong verdict or wrong threshold use, crashes and unhandled
     errors, files overwritten or left half-written, resource leaks, races and thread affinity
     (window), stale state after a session change or a cancelled run, culture-dependent parsing
     or formatting, a text that says something the number does not, the two languages out of
     step, dead or duplicated code, code that contradicts its own comment or its design note.
   - A finding needs a concrete case: which input or sequence of actions, what comes out, what
     should. No case, no finding.
   - Report each as: title; severity 1-4; `file:line`; what the code does; the case; whether an
     existing test covers the spot; confidence (read and certain / plausible, needs a run).
     Cleanup items in a separate short list.

3. **I verify every finding in the code** before it enters the list: open the cited lines, follow
   the callers, check the note and the requirement. Outcome per finding: confirmed, rejected
   (with the reason, kept out of the list), or "to measure" (a formula or a timing claim that
   only a run on the masters or a test can settle; the fix session starts by measuring).
   Duplicates across areas are merged.

4. **The seams, read by me**: what `Runs/` returns against how `FlatCheckCli` and the tab
   view-models use it (null results, error outcomes, cancellation, paths, the language
   argument), and `SirilSession` against the runs that take its paths.

5. **Write `docs/review/2026-11-13-code-review.md`**:
   - How the review was done, what was in and out of scope, what "confirmed" and "to measure"
     mean.
   - A summary table: ID, severity, area, one-line title, status (`open`).
   - One section per finding, ordered by severity then area: ID `CR-001`..., `file:line`, what
     happens, the case, what should happen, the direction of a fix (not the fix), the test that
     would reproduce it, state of the evidence.
   - A last section, "Cleanup", severity 4, short entries.
   - How the list is used: one finding (or one tight group) per task; the task follows the rules
     "Requirements first" and "Test before fix"; it sets the entry's status to `fixed` with the
     commit, or `rejected` with the reason.

   Severity, as agreed in chat:
   1. A wrong figure or a wrong verdict.
   2. A crash, a hang, or a file overwritten or lost.
   3. A wrong text or a wrong display.
   4. Robustness and cleanliness (duplicated or dead code, a fragile construct).

6. **ADR 0100** (number checked again at commit time): the whole-code review of 2026-11-13, its
   list in `docs/review/`, the severity scale, one finding per task. One line in `CLAUDE.md`'s
   Layout for `docs/review/`.

7. **Gate, commit, merge proposal**: `scripts/verify.ps1` in the worktree, read the diff, commit
   on `claude/Review-the-whole-code`, propose the merge and ask. No CHANGELOG line (a document,
   no behaviour); each fix adds its own.

## Verification

- Every entry of the list cites a `file:line` that I opened myself; none is copied from a
  reviewer unread.
- Every file under the three code folders belongs to exactly one area (checked by listing the
  files against the table before the reviewers start).
- The findings document says how many reports each area returned and how many were rejected, so
  the yield is visible.
- `scripts/verify.ps1` green before the commit; `git diff main --stat` shows documents only.

## Cost, said plainly

Ten subagents, each reading 2,000 to 5,000 lines plus notes, then my verification pass: a large
run, the largest this project has had in one session. It stays in one session so the list is one.
