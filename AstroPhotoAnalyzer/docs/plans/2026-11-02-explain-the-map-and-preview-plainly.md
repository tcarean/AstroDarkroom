# Explain the map and preview windows in plain words

Plan for AstroPhotoAnalyzer, written on 2026-11-02. The owner approved in chat a list of 18
texts, original and proposal side by side ("Da, toate"): the two windows the Tonuri tab opens,
after the seven tabs under the rule "Plain words" (ADR 0071).

## Steps

1. Branch `claude/Explain-the-map-and-preview-plainly` in its worktree.
2. `MapViewModel.cs`: the twelve parts of the layers' legends. `MapWindow.xaml`: the clouds' box,
   the list's heading and a new sentence under it on what a cloud's target is.
3. `PreviewWindowViewModel.cs`: the caption and the second box. `PreviewWindow.xaml`: the note.
4. Tests: the three legend assertions of `R130_WithoutAWcsTheMapHasNoCloudsAndTheLabelsAreOff`;
   `R130_TheMapAndPreviewTextsAreInPlainWords` (both languages, both paths, the XAML texts).
5. R-130, R-131; ADR 0078; CHANGELOG. No validation run: nothing in `Input/`, `Analysis/`,
   `Report/`, `Parameters.cs` or `Thresholds.cs` changes.
6. Gate, commit; the merge is the owner's call.
