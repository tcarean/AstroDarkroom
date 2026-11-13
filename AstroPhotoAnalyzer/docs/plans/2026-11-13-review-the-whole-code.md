# Review the whole code and list what to fix

## Context

AstroPhotoAnalyzer's first code was written by Opus and had almost no code review; the reviews
since look only at one task's change. The owner wants the whole project read once, critically,
and one documented list of problems at the end, so that later sessions fix them one at a time in
order of severity. Measured on 2026-11-13: about 23,500 lines of code in 115 files (Core 14,200,
console 570, window 8,800) and 15,000 of tests. That does not fit one context read with care, so
the code is read by area, each area by a reviewer with a clean context, and the findings are
merged into one list.

FlatCheck had the same review on the same day (its ADR 0100, its list
`FlatCheck/docs/review/2026-11-13-code-review.md`). This task uses the same method, the same
severity scale and the same entry format, so the two lists read alike.

This task **changes no code**. It writes one findings document, one ADR, one line in
`CLAUDE.md` and this plan.

## Scope

In: `AstroPhotoAnalyzer.Core/`, `AstroPhotoAnalyzer/`, `AstroPhotoAnalyzer.Wpf/` (the folders
`kit.json` lists as code).

Out:
- `scripts/`, `.claude/` - the kit's; a problem there is fixed in the kit.
- `Icon/make_icon.py` - a generator run by hand.
- The tests, line by line. A reviewer opens the tests of its area only to see whether a
  suspected bug is already covered. Test strength is `/mutate`'s job.

## The nine areas

Six plus the seams were announced in chat; with the file list in hand the analyzers split better
by subject and the window is too much for two readers, so there are nine, of 1,900 to 3,800
lines. Each reviewer reads every file of its area whole, plus `CLAUDE.md`, the design notes
named, the ADRs those notes cite for the area, and `docs/design/known-limitations.md` (167
lines).

| # | Area | Files | Lines | Design notes |
|---|------|-------|------:|--------------|
| 1 | Input: readers, raster, writers | `Input/FitsReader`, `ImageSharpReader`, `ImageLoader`, `IFrameReader`, `Raster`, `RasterOps`, `Stats`, `ColorMath`, `AtomicFile`, `FitsWriter`, `TiffWriter` | 2,200 | `input`, `dependencies`, `paper-fit` (the writers) |
| 2 | Tones, dust, the sky's geometry | `Analysis/TonalAnalyzer`, `DustAnalyzer`, `PrintStretchSimulator`, `PrintPreview`, `Input/Wcs`, `Astrometry`, `DarkNebulaCatalogue`, `LiftTable` | 2,300 | `measurements` (tones, dust), ADR 0025, 0026, 0033, 0035, 0037, 0085, 0086 |
| 3 | Detail, noise, colour of the sky, the file | `Analysis/StarAnalyzer`, `PrintSizeAnalyzer`, `NoiseAnalyzer`, `QuantizationAnalyzer`, `JpegAnalyzer`, `BitDepthAnalyzer`, `CropAnalyzer`, `FileFacts`, `BackgroundAnalyzer` | 2,000 | `measurements`, ADR 0014 to 0019, 0054, 0056, 0060, 0062 |
| 4 | Colour for the paper | `Analysis/ToneCurve`, `CurveAdvisor`, `GamutAnalyzer`, `GamutFit`, `SoftProof`, `Input/IccPaperBlack`, `IccProfileInfo`, `IccProfiles`, `Runs/CurveRun`, `ProofRun`, `GamutFitRun` | 3,100 | `soft-proof`, `paper-fit`, ADR 0032, 0039, 0042, 0066, 0081, 0083 |
| 5 | Report, recommendations, thresholds, language | `Report/`, `Thresholds.cs`, `Parameters.cs`, `Language.cs` | 3,200 | `recommendations-and-report`, `thresholds`, `language`, ADR 0011, 0012, 0043, 0045 |
| 6 | The run, the log, the console | `Runs/AnalysisRun`, `Logging/`, `ThirdParty.cs`, `Version.cs`, `AstroPhotoAnalyzer/` | 1,950 | `pipeline`, `logging`, `console`, ADR 0069 |
| 7 | Window shell and its view-models | `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `Loggers`, `Mvvm/`, `Interop/`, `MainViewModel`, `ParameterFields`, `WindowSettings`, `IccPickerViewModel`, `AboutViewModel`, `MapViewModel`, `PreviewWindowViewModel`, `Rows` | 2,550 | `window`, `console` (the fields), ADR 0013, 0020 |
| 8 | The tabs' view-models | `ResultTabs`, `CurvesTab`, `GamutFitTab`, `SoftProofTab` | 2,450 | `window`, `soft-proof`, `paper-fit`, ADR 0020, 0051, 0084, 0090, 0091 |
| 9 | Views, presentation, theme | `Wpf/Views/`, `Presentation/`, `Theme/Dark.xaml` | 3,850 | `window`, `language`, `soft-proof`, `paper-fit` (the viewers) |

## Steps

1. **Worktree and branch**, from the AstroPhotoAnalyzer folder of the main checkout:
   `git worktree add .claude/worktrees/Review-the-whole-code -b claude/Review-the-whole-code main`
   (the name is free, checked). Everything below happens in that worktree. Save this plan there
   as `docs/plans/2026-11-13-review-the-whole-code.md`. Before the reviewers start, list the
   files of the three code folders against the table: every file in exactly one area.

2. **Nine reviewers in parallel** (general-purpose subagents, this session's model, read-only:
   no edit, no build, no test run; a throw-away calculation in the scratchpad is allowed and
   its figures are quoted as the reviewer's). Each brief gives the area's files, its notes, and
   these instructions:
   - Read every file of the area whole. Read `known-limitations.md`, the notes and the ADRs
     first; something a note, an ADR or a requirement says is deliberate is not a finding (name
     it only if the code does not do what the note says).
   - Look for: wrong results (formula, index, off-by-one, unit, sign, NaN, division by zero,
     empty or tiny input, integer overflow on a large image), a threshold used the wrong way
     round or against the wrong quantity, crashes and unhandled errors, a file overwritten or
     left half-written, the input opened for writing, resource leaks, races and thread affinity
     (window), stale state after a new image, a failed or a superseded run, culture-dependent
     parsing or formatting, a text that says something the number does not, the two languages
     out of step (a `Tr.Pick` or `T` pair that differs in meaning, a diacritic in the
     Romanian), a number typed in an explanatory text instead of interpolated, something
     computed outside the core, a recommendation without its `Because`, dead or duplicated
     code, code that contradicts its own comment or its design note.
   - A finding needs a concrete case: which input or sequence of actions, what comes out, what
     should. No case, no finding. For a window finding, say that the case can be reached from
     the interface (the button is enabled, no overlay covers it).
   - Report each as: title; severity 1-4; `file:line`; what the code does; the case; whether an
     existing test covers the spot; confidence (read and certain / plausible, needs a run).
     Cleanup items in a separate short list.

3. **I verify every finding in the code** before it enters the list: open the cited lines, follow
   the callers, check the note, the ADR and the requirement. Outcome per finding: confirmed,
   rejected (with the reason, kept out of the list, counted), or "to measure" (a formula or a
   timing claim that only a run on the reference image or a test can settle; the fix session
   starts by measuring). Duplicates across areas are merged.

   This replaces the second, refuting agent per finding that I described in chat. FlatCheck's
   review weighed the two the same day and chose the session's own check: 70 findings, none
   rejected, at half the agents. Same choice here; an entry that still turns out wrong is closed
   `rejected` by the task that takes it.

4. **The seams, read by me**: what `AnalysisRun.Execute` returns against how `AnalyzerCli` and
   `MainViewModel` use it (null parts of the result, refusals, exceptions, the language
   argument); `CurveRun`, `ProofRun` and `GamutFitRun` against the three tabs that call them
   (which thread, what is kept after a failure or a newer run); `Parameters` against
   `Options` and `ParameterFields` (the same value by both doors).

5. **Write `docs/review/2026-11-13-code-review.md`**, in the format of FlatCheck's list:
   - How the review was done, what was in and out of scope, the yield per area (reported,
     rejected, the entries it became).
   - How to read an entry: severity, reach (*common*, *some setups*, *slip*, *rare*,
     *crafted*), evidence (*confirmed*, *to measure*, *owner*), status (`open`, then
     `fixed <commit>` or `rejected: <reason>`).
   - A summary table: ID, severity, reach, evidence, one-line title, status. Its order is the
     order of fixing: severity, then reach.
   - One section per finding: ID `CR-001`..., where (`file:line` and the member's name), what
     happens, the case, the effect, what should happen (the direction of a fix, not the fix),
     the test that covers or would reproduce it, evidence, status.
   - A last section, "Cleanup": dead, duplicated, done twice. Not defects, short entries.

   Severity, as agreed in chat:
   1. A wrong figure, a wrong verdict, or a wrongly written output image.
   2. A crash, a hang, or a file overwritten or lost.
   3. A wrong text or a wrong display.
   4. Robustness and cleanliness.

6. **ADR 0092** (number checked again at commit time): the whole-code review of 2026-11-13, its
   list in `docs/review/`, the severity scale, the three states of evidence, one entry per task
   (which follows "Requirements first", "Test before fix" and "Formula changes re-validate"
   and closes its entry in the list), the session's own check instead of a refuting agent. One
   phrase in `CLAUDE.md`'s Layout for `docs/review/`.

7. **Gate, commit, merge proposal**: `scripts/verify.ps1` in the worktree, read the diff, commit
   on `claude/Review-the-whole-code`, propose the merge and ask. No CHANGELOG line (a document,
   no behaviour); each fix adds its own.

## Verification

- Every entry of the list cites a `file:line` that I opened myself; none is copied from a
  reviewer unread.
- Every file under the three code folders belongs to exactly one area (step 1's listing).
- The findings document says how many reports each area returned and how many were rejected, so
  the yield is visible.
- `scripts/verify.ps1` green before the commit; `git diff main --stat` shows documents only.

## Cost, said plainly

Nine subagents, each reading 2,000 to 3,800 lines plus notes, then my verification pass: a
large run. It stays in one session so the list is one.
