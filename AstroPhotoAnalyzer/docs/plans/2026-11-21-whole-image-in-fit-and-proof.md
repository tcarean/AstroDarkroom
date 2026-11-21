# Culori pentru hartie and Soft proof on the whole image (R-188, ADR 0118)

## Context

Both tabs show a reduced copy of the image (the fit: a copy made for its area, on Full HD every
fourth pixel of a 23 MP image; the proof: at most 2400 pixels on the long side), and their zoom
enlarges that copy's pixels. The owner asked on 2026-11-21 what the whole image would cost,
saw the measurements, and chose the whole image in both tabs, with one computation of the fit
(about 12 s, no small gallery first) and a cap of 64 MP over which an image is still reduced.
Requirement R-188 (draft) is written and accepted; ADR 0118 is written; the R188 tests are
written and red (the new members do not exist). Worktree
`AstroPhotoAnalyzer/.claude/worktrees/Show-whole-image-in-fit-and-proof`, branch
`claude/Show-whole-image-in-fit-and-proof`. Editare Curbe and Crop are not touched.

## Steps

### 1. Core (`AstroPhotoAnalyzer.Core`)

- `Thresholds.cs`: add `WholeImageMaxPixels = 64_000_000` with its comment (the measurements of
  2026-11-21, a presumption, ADR 0118); remove `ProofMaxSide`. `CurvePreviewMaxPixels` stays
  (curves, crop); its comment loses the paper fit.
- `Input/RasterOps.cs`: add `WholeImageStride(int w, int h)`: the smallest step, 1 or more,
  whose subsample (`ceil(w / s) x ceil(h / s)`, as `Subsample` makes it) has at most
  `WholeImageMaxPixels`.
- `Runs/ProofRun.cs`: `Stride(img)` calls `WholeImageStride`; `ProofSet` gets `Stride`
  (1 = the whole image), set in `Reduce`; comments say "the image, or its reduction".
- `Runs/GamutFitRun.cs`: `Execute(a, progress)` without the area; the preview is
  `RasterOps.Subsample(r, WholeImageStride)`; `GamutFitSet.PreviewCapped` becomes `Stride`.
  The log's start line gives the step instead of the area.
- `Report/GamutFitTexts.cs`: `Note` becomes `Note(bool reduced)`; the sentence "The image here
  is a reduced copy;" only when reduced (the wording the owner approved).

### 2. Window (`AstroPhotoAnalyzer.Wpf`)

- `ViewModels/GamutFitTab.cs`: remove `SetGallerySize`, the area fields and `PreviewCapped`;
  `Compute()` without a size; add `IsReducedCopy`; `Note` and `ViewerHelp` follow it
  (`ViewerHelpFor(bool reduced)`: "pixel of the image" / "pixel of the copy"). The bitmaps and
  red layers are built inside the `Task.Run` of `ComputeCore` (a static step that turns a set
  into the items and the stripped set), so `Show` only assigns them.
- `Views/GamutFitView.xaml` and `Views/ResultViews.xaml.cs`: remove the `SizeChanged` handler
  of the fit's gallery; `FitEnlarges="False"`; the XAML comment says the new rule.
- `ViewModels/SoftProofTab.cs`: add `IsReducedCopy`, `ViewerHelp` and `WindowHelp`
  (`ViewerHelpFor` / `WindowHelpFor(bool reduced)`), the two long sentences moved from the XAML
  with their 8 read from `ZoomPanViewer.MaxScale` instead of typed (the rule "Explanation is
  not diagnosis"); raised when the set changes.
- `Views/SoftProofView.xaml`, `Views/SoftProofWindow.xaml`: bind the two text blocks to them.
- Nothing changes in `ZoomPanViewer`, `BeforeAfterViewer`, `RasterBitmaps`, `CurvesTab`, crop.

### 3. Tests (already written, red by compile error)

- `SoftProofTests`: `R188_TheProofIsMadeOnTheWholeImage` replaces
  `R160_TheProofIsMadeOnAReductionWithinProofMaxSide` (the rule it checked is the one
  replaced); `R188_AnImageOverTheCapIsReducedByTheSmallestWholeStep` (five sizes).
- `GamutFitTests`: `R188_TheFitsImagesAreTheWholeImageAndTheNoteCallsItACopyOnlyWhenReduced`;
  the two calls of `Execute` lose the area. `ValidationTests`: the same call.
- `GamutFitTabTests`: `R188_TheGalleryIsTheWholeImageAndItsSentencesSaySo` replaces
  `R178_ALargerAreaRecomputes...`; the no-profile test loses its larger-area part; the render
  test checks `FitEnlarges` is false and bound to nothing.
- `SoftProofTabTests`: `R188_TheProofShownIsTheWholeImageAndItsSentencesSaySo`.
- Each replaced or cut test is reported at wrap-up with its reason (an intended change of the
  behaviour, not a test edited to pass).

### 4. Documents

- `docs/requirements.md`: R-188 to `implemented` (the older rows already point to it).
- ADR 0084 and ADR 0066: the status line gains "superseded in part by 0118".
- `docs/design/`: `soft-proof.md`, `paper-fit.md`, `window.md`, `pipeline.md`,
  `thresholds.md`, `known-limitations.md` (the memory per megapixel, the cap, the unmeasured
  zoom smoothness), `testing.md` if it names the cut tests.
- `README.md` (the two tabs' sections: the zoom is on the image, no "reduced copy" for these
  two), `CHANGELOG.md` (`Changed`, under `[Unreleased]`).
- The plan saved as `docs/plans/2026-11-21-whole-image-in-fit-and-proof.md`.

## Verification

1. `scripts/verify.ps1` green (build with warnings as errors, format, all tests, design check).
2. `/validate` (Thresholds and `Report/` moved): the drift table pasted; no figure is expected
   to move, V-016 included (its figures are on the tonal subsample).
3. The window from the worktree's build on the reference FITS with the baryta profile: both
   tabs show 5837 x 3867 (the log's `reduced=` and the fit's step), the texts say "pixel al
   imaginii", and the log gives the real times beside the 12 s and 3.3 s measured. I can check
   that it opens and renders; how smooth the zoom and the pan feel is the owner's to try before
   the merge, and a sluggish zoom is reported as a finding, not patched in this task.
4. A code review by a subagent before the merge proposal, and one on the fix commit if any.
5. Commit on the task branch, merge proposed and asked.

## Not in this task

- Editare Curbe and Crop keep their reduced copies.
- No version bump (the owner's); the change adds user-visible behaviour, so a minor bump may be
  due at the next release.
