# 0010. One TransformGroup instance on both images and both backdrops, never two transforms configured alike

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Invariants", "One transform object, not
two"), the constructor comment in `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs`, the
comment at the top of `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml` and the summary of
`src/ImageVisualCompare.Core/Viewport.cs`; the decision itself dates from a6e5f10 (the base
version, 2026-09-08). Image A and image B are shown at the same zoom and pan, and the comparison
depends on that holding after every wheel notch, drag, scrollbar move and minimap click. Each of
the two `Image` elements needs a `RenderTransform`, and so, since they were added, does each
backdrop: the one under A in e0ab0b3 ("bug fix", 2026-09-08), the one under B in 861f5c1 (image
padding, 2026-09-09).

## Decision

`CompareView` holds one `TransformGroup`, `_transform`: a `TranslateTransform` (`_translate`)
applied before a `ScaleTransform` (`_scale`). Its constructor assigns that same instance as the
`RenderTransform` of `BeforeImage`, `AfterImage`, `BeforeBackdrop` and `AfterBackdrop`, and
`Sync` is the one method that writes its values, from `RenderPlanner.For` (ADR 0011). The
synchronisation is structural: there is no second state to keep in step, and no code path can
move one image without the other.

## Alternatives rejected

- **Two transforms configured alike**, one per element. Each change would have to be made to
  every one of them, and a future change could update one and forget the other; with a single
  instance there is nothing to keep in step (the constructor comment).

## Consequences

- A, B and their backdrops cannot disagree about zoom or pan. An element that has to move with
  the images gets the same instance, as each backdrop did when it was added.
- Only the transform is shared. What else has to match between the two layers is set by code,
  once for each: the resampling mode, which `Sync` sets on `BeforeImage` and on `AfterImage` from
  the one `plan.ScalingMode`, and the backdrops' size and visibility, set side by side in
  `SetPair` and `Clear`.
- Guards: none by test; the application has to be launched and looked at (ADR 0026).

Source: CLAUDE.md ("Invariants", "One transform object, not two");
src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs (the constructor, `Sync`, `SetPair`,
`Clear`); src/ImageVisualCompare.Wpf/Controls/CompareView.xaml (the top comment);
src/ImageVisualCompare.Core/Viewport.cs (summary); the diffs of e0ab0b3 and 861f5c1.
