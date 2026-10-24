# Draw the L* 15 visibility line on the Tonuri histogram

Approved 2026-10-24. Branch `claude/Draw-the-L15-line-on-the-histogram`.

## Context

The Tonuri tab's L* histogram (`AstroPhotoAnalyzer.Wpf/Views/TonesView.xaml`) marks one tone
only: paper black, a red line. Since ADR 0029 the tab judges a second tone, the visibility
threshold `Thresholds.LowSeparationLstar` (L* 15, "the paper separates, the eye in room light
barely"): the row "Sub L* 15 (separare slaba)", the simulation's `cer sub L* 15` mark, the
preview window's amber overlay. The owner wants that second threshold drawn on the histogram
too, as a line of another colour or dashed, and the texts around the chart rewritten for two
lines.

The two lines can appear in either order: on baryta (Dmax 2.4) paper black is L* 3.6, left of
L* 15; on canvas (Dmax 1.5) it is L* 20.7, right of it. The texts must hold both ways.

No number moves in the core, so no `/validate` and no golden changes: this is a window change
(`Presentation/BarsCanvas.cs`, `ViewModels/ResultTabs.cs`, `Views/TonesView.xaml`) plus its
docs and tests.

## Decisions taken in the plan (recorded as ADR 0046)

- **Colour and style.** The new line is amber (`WarnBr`, #E3B341) and dashed; the paper-black
  line stays red (`BadBr`) and solid. These are the two colours the preview window already uses
  for the same two classes (`PreviewWindowViewModel.BlackColour` red = under paper black,
  `LowColour` amber = under L* 15, above black), so one colour means one thing across the
  window. Dashed, because on a paper whose black is near L* 15 (Dmax about 1.9) the two lines
  nearly coincide and the colours alone would not tell them apart.
- **Texts move to the view-model.** The heading and the note under the chart are XAML literals
  today. The new versions carry the number 15, which the rule "Explanation is not diagnosis"
  wants interpolated from `Thresholds.LowSeparationLstar`, so both become properties of
  `TonesTabViewModel` (`HistogramHeading`, `HistogramNote`) and the XAML binds them, as
  `PreviewTooltip` already does.
- **The marker's position comes from the view-model**, a `LowSeparation` property set to
  `Thresholds.LowSeparationLstar` in `Populate` (and NaN in `Clear`), beside `PaperBlack`:
  `BarsCanvas` stays a generic chart that knows nothing about the core.
- ADR number: **0046**, because the unmerged branch `claude/Close-the-backfill-questions`
  already holds 0045.

## Steps

1. **Worktree and branch** (the kit's rule "Worktrees"):
   `git worktree add AstroPhotoAnalyzer/.claude/worktrees/Draw-the-L15-line-on-the-histogram -b claude/Draw-the-L15-line-on-the-histogram main`
   (short name on purpose: Windows path limit). Save this plan as
   `docs/plans/2026-10-24-draw-the-l15-line-on-the-histogram.md`.

2. **Requirement first.** In `docs/requirements.md`, R-113: "the L\* histogram with paper black
   marked" becomes "the L\* histogram with two lines: paper black (red, solid) and the
   visibility threshold L\* `LowSeparationLstar` (amber, dashed), both read by the heading and
   the note under the chart, whose number comes from `Thresholds`; the texts hold whichever
   line is left of the other (canvas puts paper black at L\* 20.7, right of 15)".

3. **Tests before code** (`AstroPhotoAnalyzer.Wpf.Tests`):
   - `ViewModelTests.TonesExportResolutionAndBackgroundCarryTheirRows`: assert
     `tones.LowSeparation == Thresholds.LowSeparationLstar`, and that `HistogramHeading` and
     `HistogramNote` contain `$"L* {Fmt.F0(Thresholds.LowSeparationLstar)}"` and name both
     lines ("rosie", "galbena"/"punctata").
   - A new `R113_...` test: on a canvas run (`Scenes.DarkSky()` on canvas)
     `PaperBlack > LowSeparation`, both finite, and the two texts are the same strings as on
     baryta (they do not depend on the order). After `Clear()` both markers are NaN.
   - `WindowSmokeTests` already renders `tab-tonuri`; it keeps covering the drawing.

4. **BarsCanvas**: two new dependency properties, `SecondMarker` (double, NaN default,
   `AffectsRender`) and `SecondMarkerBrush` (default `Brushes.Goldenrod`), drawn in `OnRender`
   after the first marker with a `Pen` of 1.5 and `DashStyle = DashStyles.Dash`. Doc comments
   on the new members and the file header's sentence about "the marker" updated to two markers.

5. **TonesTabViewModel**:
   - `LowSeparation` (double, NaN until populated) set in `Populate`, reset in `Clear`.
   - `HistogramHeading` =>
     `HISTOGRAMA L* (0..100); linia rosie = negrul hartiei, linia galbena punctata = pragul de vizibilitate L* {LowSeparationLstar}`.
   - `HistogramNote` =>
     `La stanga liniei rosii totul se printeaza cu acelasi negru. La stanga liniei galbene punctate (sub L* {LowSeparationLstar}) hartia inca separa tonurile, dar ochiul, in lumina de camera, abia. Histograma e pe subesantion, in procente.`
     Two "left of" sentences, so the text is right in either order of the lines.
   - Update the class's `<summary>`.

6. **TonesView.xaml**: heading `Text="{Binding HistogramHeading}"`, note
   `Text="{Binding HistogramNote}"`, `BarsCanvas` gets
   `SecondMarker="{Binding LowSeparation}" SecondMarkerBrush="{StaticResource WarnBr}"`.

7. **Docs, same task.** `docs/design/window.md` (the `TonesTabViewModel` row and the
   `BarsCanvas` line); ADR 0046 (context, decision, alternatives: a second red line; a shaded
   band between the lines, rejected because the band would be empty or inverted on canvas);
   `CHANGELOG.md` under `[Unreleased]`, **Added**; `README.md` if it describes the red line.

8. **Gate and wrap-up**: `scripts/verify.ps1`, read the whole diff back, commit on the branch,
   propose the merge. No golden changes expected (window only); if the gate reports one, stop
   and report it.

## Verification

- `dotnet test AstroPhotoAnalyzer.sln -c Release --nologo --filter "Category!=Validation"`.
- `APA_DUMP_SCREENS=<dir> dotnet test AstroPhotoAnalyzer.Wpf.Tests --filter WindowSmokeTests`
  and look at `tab-tonuri.png`: two lines, red solid and amber dashed, heading and note as above.
- Run the window on the reference image on baryta and on canvas and confirm the amber line
  sits at 15 and the red line crosses to its right on canvas, the note still reading correctly.
- `scripts/verify.ps1` green before the commit.
