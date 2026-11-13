# Compare the soft proof with another image's

Branch `claude/Compare-two-soft-proofs`, worktree
`AstroPhotoAnalyzer/.claude/worktrees/Compare-two-soft-proofs`. Requirements R-165 and R-166
(draft, accepted by the owner on 2026-11-13), ADR 0091 (accepted the same day).

## Context

The Soft proof tab shows one image through the paper's ICC profile. The owner wants to pick a
second image, have the same proof with the same settings applied to it, and compare the two in
the same tab the way ImageVisualCompare does, then change the second image or stop comparing;
the same in full screen. The window already has ImageVisualCompare's comparison, the dragged
vertical line (`BeforeAfterViewer`, R-179, ADR 0090), and the tab and its full-screen window
already share one view-model and one set of controls, so the feature is mostly wiring: a core
step that proofs a second file from the first image's set, state for a second image in the
tab's view-model, and the existing comparison control in the two views.

Already done on the branch, before this plan: the two requirement rows, ADR 0091, and the
failing tests (`SoftProofCompareTests` in the core's tests, three `R166_` tests in
`SoftProofTabTests`; the build fails on the members that do not exist yet).

## What is built

### 1. Core (`AstroPhotoAnalyzer.Core`)

- `Runs/ProofRun.cs`
  - `ProofSet` gains `Gamut` (the `GamutTable` built in `Prepare`) and `SourceW`, `SourceH`
    (the analysed image's size), so the other image is judged by the same table and its size
    compared without the analysis.
  - The mask loop of `Prepare` moves to a private `Mask(GamutTable, Raster)` used by both.
  - `PrepareOther(ProofSet first, string path)`: `ImageLoader.Load` inside a try; any exception
    becomes the set's `Problem` (its message, as `MainViewModel.Describe` would show it), logged
    at Warn with the exception. Then `PrepareOther(first, raster, path)`.
  - `PrepareOther(ProofSet first, Raster other, string path)`: refuses another size
    (`ProofTexts.SizeMismatch`), a linear image (`RasterOps.LooksLinear`, the sentence of
    R-015); otherwise `RasterOps.Subsample` at `Stride`, `Mask`, and a set that holds
    `first.Profile` itself (one profile object, so `ProofProfile.Table(intent)` is built once
    for both). A `RunClock` named `proof-other` (`load`, `mask`), refusals as its done line.
  - `Render` is unchanged: it already works on any set.
- `Runs/AnalysisRun.cs`: the linear refusal's sentence moves to an internal
  `LinearRefusal(double median)` so both runs say the same words; no behaviour change.
- `Report/ProofTexts.cs`: `SizeMismatch(w, h, otherW, otherH)`,
  `PaperGamutLegend(string name, double pct)` (the name, a colon, the existing legend) and
  `CompareSides(string left, string right)`; all `Tr.Pick` pairs, the wording shown to the
  owner on 2026-11-13.

### 2. View-model (`AstroPhotoAnalyzer.Wpf/ViewModels/SoftProofTab.cs`)

- State for the other image: its path, its set, its proof, its three bitmaps (original, proof,
  red layer), and the path of the analysed image it was chosen for.
- New members the views bind: `CanCompare` (a proof shown, nothing computing), `IsComparing`,
  `OtherImage` (proof or original, as `Image`), `PaperOverlay` / `OtherPaperOverlay` (the red
  layer or null, one property per side, as the paper fit's items do), `ImageName`, `OtherName`,
  `OtherPaperGamutLegend`, `CompareSides`; `PaperGamutLegend` carries the file name while
  comparing. `StopComparing` command.
- `CompareAsync(path)` (pool thread, the `_run` number pattern, "computing" meanwhile, also
  `ProofComputation`) and `Compare(path)` (synchronous, for the tests, as `Compute()`): prepare
  and render the other file with the current intent; a refused file sets `Message` + `IsProblem`
  and leaves what was shown (the single image or the earlier comparison).
- `RenderAsync` (a change of intent) renders both sets; `Compute`/`ComputeCore` proof the other
  file again when the analysed image is the same path as the one it was chosen for, and end the
  comparison with the reason if it is then refused; `Populate` drops the other file when the
  path differs; `Clear` keeps the path (it runs before every analysis) but drops its bitmaps.
- Log lines under `Window.SoftProof`: compare with a file, stop, a refusal, a superseded run.

### 3. Views

- `Views/SoftProofControls.xaml` + its class in `Views/ResultViews.xaml.cs`: three named
  buttons after the margin list: `CompareButton` (`Compara cu alta imagine...`, shown when not
  comparing), `ChangeButton` (`Schimba imaginea...`) and `StopButton` (`Opreste comparatia`),
  shown while comparing; the first two enabled by `CanCompare` and handled by one click
  handler that opens the file dialog over `Window.GetWindow(this)` (the full-screen window when
  pressed there, ADR 0091 decision 6) and calls `CompareAsync`. The dialog's filter moves out
  of `MainWindow.Choose` into a small shared helper so both use the same kinds of file.
- `Views/SoftProofView.xaml` and `Views/SoftProofWindow.xaml`: inside the margin `Border`, the
  existing `ZoomPanViewer` (named `Single`, shown when not comparing) and a
  `BeforeAfterViewer` (named `Comparison`, shown when comparing) bound to `Image` /
  `PaperOverlay` / `OtherImage` / `OtherPaperOverlay` / `Width` / `Height` / `ImageName` /
  `OtherName`. Under the image in the tab: the second legend line and the `CompareSides`
  sentence; in the full-screen panel the sentence joins the caption. The tab's controls get
  the name `Controls`.
- `Presentation/BeforeAfterViewer` is used as it is.

### 4. Tests

The failing tests already written turn green. Added while implementing: the new core
sentences join the list of `R163_TheSoftProofTextsAreInPlainWords`; a log test for the new
lines in `WindowLoggingTests` / `LoggingTests`, following the existing soft proof cases.

### 5. Documents

`docs/design/soft-proof.md` (the run's new step, the tab's second image, the two viewers),
`docs/design/logging.md` (the new lines), `docs/design/window.md` and `testing.md` (one
sentence each), `README.md` ("Soft proofing": the three buttons), `CHANGELOG.md` (Added),
`docs/requirements.md` (R-165, R-166 to `implemented`), this plan saved as
`docs/plans/2026-11-13-compare-the-soft-proof-with-another-image.md`.

## Order and how each step is checked

1. Core (`ProofRun`, `ProofTexts`, `AnalysisRun.LinearRefusal`), then
   `dotnet test --filter "FullyQualifiedName~SoftProofCompareTests|FullyQualifiedName~SoftProofTests|FullyQualifiedName~R015"`.
2. View-model, then the two view-model tests
   (`--filter "FullyQualifiedName~SoftProofTabTests"`).
3. Views and the dialog, then the STA test of the same filter, `SoftProofWindowTests` and
   `WindowSmokeTests`; a render of the tab in compare mode with `APA_DUMP_SCREENS` to look at
   the layout.
4. Documents; `scripts/verify.ps1` with `--no-incremental -warnaserror` first.
5. `/validate` (a file under `Report/` changed): the drift table pasted; no figure should move.
6. Read the whole diff; commit on the branch; a subagent review before the merge is proposed.
7. The owner tries it in the running window (the file dialog and the line under the mouse are
   not driven by tests): compare, change, stop, the same in full screen.

## Not in this task

Images of two sizes; a key for the compare mode in full screen; remembering the other file
between runs; a version bump (the owner's word; this adds a feature, so a minor bump seems due).
