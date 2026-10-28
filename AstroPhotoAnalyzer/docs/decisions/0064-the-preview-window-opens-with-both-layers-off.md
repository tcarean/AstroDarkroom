# 0064. The preview window opens with both layers off

Date: 2026-10-28
Status: accepted 2026-10-28

## Context

Every row of the Tonuri tab's background simulation, the as-is row included, has a `Preview`
button that opens a maximised window with the image lifted to the row's target and two layers
painted over it: red where a pixel is under paper black, amber where it is under L\*
`LowSeparationLstar` (15) but over black, each behind a check box (R-131, ADR 0025). Since the
window was added both boxes were ticked when it opened (R-131 said "on by default"), so the first
look went to the two classes painted over the image (red at alpha 140 of 255, amber at 100), not
to the image the row's target gives. Each box's label is its legend, with the share of the
pixels in that class, and it is shown whether the box is ticked or not. On 2026-10-28 the owner
asked for both boxes to be unchecked when the window opens ("As vrea cand se deschide acea
fereastra, cele 2 checkboxuri de Under paper black si under L*15 sa fie debifate by default").

## Decision

Both layers start off: `PreviewWindowViewModel` is built with `ShowBlack` and `ShowLow` false, so
the window opens on the lifted image alone, and a ticked box paints its own layer. Every preview
window starts so; a new one does not take the boxes of the one before. The map window (R-130) is
not changed: its layers and its clouds stay on when it opens.

## Alternatives rejected

- **Keeping both on.** The window then opens on the classes, which the legends already count,
  instead of on the image at the row's target.
- **Remembering the boxes, between windows or in `settings.json` (ADR 0053).** Not asked for, and
  a preview would then open in a state the reader may not remember choosing.
- **The map window's layers off as well.** Not asked for; the map exists to show its classes,
  so that the owner can judge them by eye (ADR 0026).

## Consequences

- R-131 says the layers are off by default. `R131_ThePreviewLiftsToTheRowAndPaintsTheTwoClasses`
  asserts the view-model starts with both off; `R131_ThePreviewWindowOpensWithBothLayersOff`
  builds the window and asserts both boxes unchecked, both layers collapsed, and a ticked box
  showing its own layer alone.
- The README's paragraph on **Preview** says the boxes start unchecked.
- The `preview-rand` screen that `APA_DUMP_SCREENS` writes shows the image without the layers.
