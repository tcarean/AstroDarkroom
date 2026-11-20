# 0059. A key hides the minimap and the zoom badge

Date: 2026-11-20
Status: accepted 2026-11-20

## Context

The minimap floats over the bottom-left corner of the comparison surface and the zoom badge
over its top-right corner (`CompareView.xaml`). The pan stops at the image's edges
(`Viewport.ClampOffsets`). So whenever the image overflows the surface on both axes, which is
every pair from 100 % up, the bottom-left corner of the image can only be brought to the
bottom-left corner of the surface, where the minimap lies over it, and no key, button or drag
brought it out; the top-right corner had the same, on a smaller scale, under the badge. The
review of 2026-11-13 listed it as the owner's call (`docs/review/2026-11-13-code-review.md`,
CR-012): ADR 0019 accepts the 9-DIP strips of the scrollbars and says nothing of the minimap.

The sizes, from the code: the minimap of a 5938 × 3888 canvas is 190 × 124.4 DIPs (R-040), 12
DIPs from the left edge and 16 from the bottom; at 100 % on a display scaled 100 % that is
190 × 124 image pixels of the corner, and at 1600 % about 12 × 8. A star in that corner could
not be compared.

The owner chose on 2026-11-20, of three options (a key that hides the two; a pan that may
overshoot the edge by their size; leaving it): the key `M`.

## Decision

The key `M`, without a modifier, wherever the focus is in the main window, hides the minimap
and the zoom badge, and pressed again shows them (R-067). A held key acts once. The choice holds
through a mode switch, a changed image and a new pair, and is not kept between runs: every
start shows both. The scrollbars, the divider and the toolbar's zoom are not touched. The
status bar's hint names the key, `M minimap` (`M: minimap` in Romanian), since nothing else on
screen says it is there.

The window reads the key as it reads `H` and `F` (`MainWindow.OnPreviewKeyDown`,
`OnOverlaysKey`); the surface holds the state and applies it (`CompareView.OverlaysHidden`,
`ToggleOverlays`).

## Alternatives rejected

- **A pan that may overshoot the image's edge by the size of the overlays.** The corner would
  come out from under the minimap by a drag, with nothing to learn. But the limits of
  `Viewport`, the scrollbars' ranges, the minimap's rectangle and the whole-pixel drawing of
  ADR 0052 and ADR 0053 all rest on the image stopping at the edge, and an empty margin would
  show wherever the view overshot.
- **Leaving it, as the scrollbars' strips are left.** A strip of 9 DIPs along an edge is not a
  corner of 190 × 124 DIPs; the tool exists to compare any part of the image.
- **A toolbar button.** One more button on a toolbar whose width already decides the window's
  smallest size, for something used now and then; the hint names the key instead.
- **Hiding the scrollbars with them.** They show where the view is when the minimap is gone,
  and ADR 0019 already weighed their strips.
- **Keeping the choice between runs.** The program keeps no settings file; a minimap missing at
  the next start, with nothing on screen but the hint to say why, would read as a defect.

## Consequences

- With the minimap hidden there is no thumbnail to jump with; the scrollbars and the drag pan.
- The Help manual in the wiki gains the key (ADR 0050); `README.md` has it in "Controls".
- `MinimapView.Hide`, which also drops the thumbnail, is still called by nothing; it stays on
  the review's cleanup list.
- Guards: `MainWindowTests.R067_...` press the key on a window never shown and read the two
  visibilities; where the two are drawn is looked at by running the application (ADR 0026).

Source: docs/review/2026-11-13-code-review.md (CR-012);
src/ImageVisualCompare.Wpf/Controls/CompareView.xaml and CompareView.xaml.cs;
src/ImageVisualCompare.Wpf/MainWindow.xaml.cs; src/ImageVisualCompare.Core/UiText.cs.
