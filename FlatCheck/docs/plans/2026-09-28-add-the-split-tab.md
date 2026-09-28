# 2026-09-28 Add the Split tab

## Context

The owner shoots flats in rotated groups (15 + 15 at 180°, or 4 x 10 at 90°) to cancel
gradients, and wants to see what each group's own master looks like beside the whole session's
master: the five deviation tests, the vignetting depth against the lens profile, and the
residual maps side by side. Today that means building the sub-sessions by hand and running the
Siril script and the console on each.

Measured before the design (2026-09-28, `rotiri-perfecte`, 40 flats, Siril 1.4.4):

| fact | value |
| --- | --- |
| real sessions | 14 with `flats/`; 30 (180°) or 40 (90°) frames, plus a `readme.txt` inside `flats/` in the 40-frame ones |
| input sizes | flats 530-700 MB, biases 460-770 MB, lights 2 frames, ~40 MB |
| one 10-flat part, copied and run with `OSC_Preprocessing_WithoutDark.ssf` | copy 5 s, 673 MB; Siril 38 s, exit 0; `process/` 4.1 GB |
| four parts | ~2.7 GB copied, ~16.5 GB of `process/`, ~3 min of Siril; C: had 187 GB free |
| part 1 vs whole, five tests (%) | 7.58 / 6.95 / 7.79 / 5.48 / 7.16 (BUN) vs 1.18 / 2.03 / 2.86 / 0.58 / 2.65 (BUN) |
| depth vs nearest profile | part 1: 0.73 stops, Adobe +0.07; whole: 0.76 stops, lensfun 7D2 -0.08 |

The last line drove one design point: "nearest profile" is chosen per master, so a table with a
different reference per column would compare nothing. The reference is the whole session's
nearest profile, the same for every column.

## Decisions taken by the owner (2026-09-28)

Recorded in ADR 0054. In short: at most 2 files of `lights/` (the first by name); labels in the
mosaic through a built-in bitmap font, no package; the temp folder is kept only after a failure;
the part masters are not kept; `Rezultat split.txt` and `flatcheck-split.png` in the session
root, overwritten without asking; every value in the table with its verdict; a profile column;
the report and the PNG carry the date and the version; reuse the Siril run and the structure
analysis instead of writing them again; the version goes to 2.3.0 (owner's call, rule 17).

## Steps

1. Requirements R-091..R-093 (`draft`), then the tests that fail: `FlatCheck.Tests/SplitRunTests.cs`,
   `BitmapFontTests.cs`, `FlatCheck.Wpf.Tests/SplitTabTests.cs` (a fake `SirilRunner` writes a
   generated master into `process/` of the folder it is given; the temp root is redirected into
   the test workspace; one test snapshots the original session before and after).
2. Reuse refactors, behaviour unchanged: `StructureRun.Execute` = `Analyze` + report + map, so
   `SplitRun` runs `Analyze` per master; the script choice (list, default, blocker, GUI warning)
   moves from `PreprocessViewModel` into `ScriptChoiceViewModel`, which `PreprocessViewModel`
   extends; `SirilScriptRun` and `SirilRunViewModel` are used as they are, the latter shown
   inline on the tab.
3. Core: `Runs/SplitRun.cs` (`SplitResult`, `SplitColumn`; `Execute(whole, parts, query, when,
   framesPerPart, report)`), `Report/SplitReport.cs` (`Headers`, `Rows` of `SplitCell`, the
   report text, the map order line, the meaning, the PNG title and labels), `BitmapFont.cs`
   (5x7 glyphs: digits, A-Z, `. - / : % ( ) + ,`), `PngWriter.EncodeMosaic` (title band, label
   band, tiles in one row, one shared automatic range).
4. Window: `Session/SplitPlan.cs` (frames, division, copy list, temp root, delete),
   `ViewModels/SplitTabViewModel.cs`, `Views/SplitView.xaml`, the tab in `MainWindow.xaml`, the
   seams through `MainViewModel` (`runScript`, `clock`, `tempRootFor`), `SessionMessages` texts,
   `SirilSession.SplitResultTextPath` / `SplitPngPath`; the smoke test renders the tab.
5. The real run: `rotiri-perfecte` in 4 and a 180° session in 2, the tables pasted into
   `docs/validation.md` as dated measurements (no `validation.json` case: the part masters exist
   only during the run, so nothing can re-measure them without Siril).
6. Record: README (the tab, what it copies, the measured cost, the files written), CHANGELOG,
   `two-applications.md`, `code-map.md`, ADR 0054, R-091..R-093 `implemented`,
   `Directory.Build.props` 2.3.0, `/wrap-up`, the merge proposal.

Out of scope: a console mode (the core takes file paths, so `--split` stays cheap), keeping the
part masters, parallel runs, a trimmed script.

## Branch

Worktree `FlatCheck/.claude/worktrees/Add-the-Split-tab`, branch `claude/Add-the-Split-tab`
from `main` (ADR 0050).

## Verification

Verify green; the R-091..R-093 tests green; the golden files untouched; the tab rendered by
`WindowSmokeTests`; the two real runs pasted in the session and in `validation.md`.

## What deviated, and why (2026-09-28, at wrap-up)

- Two subagent code reviews ran on the branch, at the owner's request, and their findings were
  fixed in three follow-up commits: the copy list follows the script's `cd` folders instead
  of a fixed `biases/lights/flats` (the stock `OSC_Preprocessing.ssf` enters `darks/`),
  `process/` and `masters/` are never copied, the column verdict is against the reference
  profile (one profile for the whole table), the temp folder survives neither a read-only raw
  nor the window closing, and the frame count is re-read on the button.
- The owner asked for four more things after seeing the tab: the verdict chip beside the
  value, one row per lens profile (as on the Structure tab) instead of one reference row, the
  mouse wheel over the table, and a disk estimate (`SplitCost`, no time) in place of the
  measured-cost sentence. ADR 0054 records them.
- Step 5 ran headless through `MainViewModel` from a scratch console, not from the window,
  so the reports carry the 2.2.0 banner (the bump came after); the figures are in
  `validation.md`.
- Not done, on purpose: a cap on `darks/` like the one on `lights/` (a reviewer's note; the
  script needs them and the estimate makes the cost visible), a console `--split`.
