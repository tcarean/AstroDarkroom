# 0090. The paper fit is compared with the original across a line, and zoomed like the soft proof

Date: 2026-11-13
Status: accepted 2026-11-13
Supersedes: 0084 in part (decision 3: the original as the gallery's first position with its own figures under it)

## Context

The Culori pentru hartie tab (R-178, ADR 0083) showed one image at a time, the original first
and then the three variants, inside a view that only fitted it: no zoom, and a comparison meant
pressing `Inapoi` and remembering what was there. The fit changes the saturation of a few
percent of the pixels by a few units, mostly in the darkest dust, which is exactly what memory
does not hold across a flip. On 2026-11-12 the owner asked for the zoom and pan the Soft proof
tab has, and for a comparison with the unmodified original "made as in ImageVisualCompare, with
clear Before and After labels: the buttons change only After, Before is always the original".
ImageVisualCompare compares in one way: a vertical line over the view, one image left of it and
the other right, the line dragged with the mouse and staying put while the images zoom and pan
under it (its ADRs 0013 and 0021). Four choices were put to the owner the same day, each with a
recommendation, and answered in chat.

## Decision

1. The tab's image is a comparison across a vertical line (R-179): the original left of it, the
   variant chosen with the buttons right of it, labelled `Before` / `After` (`Inainte` / `Dupa`)
   beside the line. The line is always there; it has no switch and no keys, the Left and Right
   arrows staying the gallery's.
2. The original leaves the gallery: the buttons go through the three variants (`1 / 3`) and the
   tab opens on `Exacta` beside the original. Each variant's evidence already holds the
   original's share (`before: x % of the pixels`), so the original's own rows go.
3. With the box `arata ce nu intra` on, red is painted on both sides, each its own: on the
   original what the paper cannot hold (the warning of R-161), on the variant what is left.
4. In Romanian the gallery buttons become `Precedenta` / `Urmatoarea`, in both gallery tabs, so
   that `Inainte` means one thing on the screen, the label.
5. The zoom and pan are the Soft proof's (R-163), on the reduced copy: the same viewer, the same
   limits, the same gestures.
6. The line's code is ImageVisualCompare's `DividerModel`, copied into the window project as
   `SplitDivider`: the monorepo has no shared library and its tools share code by copying. The
   line, the zoom and the labels are view state; the view-model gains only the original as a
   second image.

## Alternatives rejected

- **A box that flips between the original and the variant, as the Soft proof's `Originalul`.**
  It is the flip the owner wanted to leave: a small change of saturation is seen across an edge,
  not across a blink.
- **Keep the original as the first position.** It would be compared with itself; its figures are
  in every variant's evidence.
- **Red on the variant only.** With the original out of the gallery nothing in the tab would
  show where the paper falls short, the first thing the owner looked for (ADR 0084).
- **Real pixels at high zoom.** The fit over the whole 22.6 MP image takes 11 to 27 s per
  variant and a whole-size gallery held 1.6 GB (ADR 0084's table), so it would need a fit
  computed for the part in view at every move. The change of colour is broad and per pixel, with
  no spatial step, so the reduced copy shows it; a task of its own if the detail is ever wanted.
- **Two viewers kept in step.** One viewer with both images in its child cannot drift; the price
  is a clip expressed in the child's units and set again at every change of the view.
- **A shared library with ImageVisualCompare.** One class of 140 lines does not justify the
  first cross-tool project reference of the suite.

## Consequences

- R-178 and R-146 say so and R-179 is new; `SplitDividerTests`, `BeforeAfterViewerTests`,
  `ZoomPanViewerTests` and `GamutFitTabTests` hold the line's rules, the clip under zoom and pan,
  the two additions of the viewer and the gallery of three. The mouse itself (the press that
  grabs the line, the cursor) is not driven by a test; it is checked in the running window.
- `ZoomPanViewer` serves two tabs: `FitEnlarges` keeps ADR 0084's rule that an image smaller
  than the area is not blown up, and `ViewChanged` lets the comparison follow the view.
- A recompute for a larger area changes the copy's size, so the zoom goes back to the fit, as
  R-163 says of an image of another size; the line stays.
- `GamutFitTexts.OriginalRows` is removed. The Editare Curbe tab keeps its single image; the
  comparison control is reusable there.
