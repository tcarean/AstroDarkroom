# 0084. The gallery tabs fill their area and recompute themselves, without a button

Date: 2026-11-06
Status: accepted 2026-11-06
Supersedes: 0051 in part (the `Calculeaza` button that recomputed the gallery at the area's size, and the rule that the view never enlarges a preview, which now holds except for a preview held under the cap)

## Context

The two gallery tabs, Editare Curbe (R-146, ADR 0051) and Culori pentru hartie (R-178, ADR 0083),
are computed right after the analysis, before the tab was ever laid out, so their previews were
made for a presumed area of `CurvePreviewDefaultWidth` x `CurvePreviewDefaultHeight` (1600 x
900), and a `Calculeaza` button recomputed them at the area's real size. The reduction's stride
was the smallest integer whose subsample fits inside the area, so a preview was between 80 and
100 % of the area, and the view, which only shrinks an image, never filled it. On 2026-11-06,
right after the paper fit's merge, the owner asked why the button exists, why the tab is not
automatic, and why the image shrinks after the press (on their screen the new tab's image
visibly lost size), and chose the fix in both tabs.

## Decision

1. The preview's stride is the largest integer whose subsample still covers the size the view
   shows the image at, its aspect fitted into the area (`CurveRun.PreviewStride`:
   `floor(max(W / maxWidth, H / maxHeight))`, at least 1): the preview is at least the fitted
   size, and the view shrinks it to fit exactly, at high-quality bitmap scaling. On a 23 MP image
   the presumed area gives a stride of 4 (1460 x 967) instead of 5; covering both dimensions
   (`floor(min)`) would cost 1.8 times the pixels for the same image on screen, since the
   dimension that limits the fit is the only one that matters (the review of 2026-11-06). The
   preview never passes `CurvePreviewMaxPixels` (8 MP): the stride stays at the smallest that
   keeps it within, the core says so (`PreviewCapped` on the set), and only then does the view
   enlarge the preview to fill the area, a little soft on a whole 4K area, where a 23 MP image
   would otherwise be shown whole. An image smaller than the area stays at its own size, as
   before: a small export blown up would mislead about its detail.
2. The button goes. The view reports the gallery area's size at every layout pass, as before;
   the view-model recomputes by itself when the stride that size allows is smaller than the one
   the shown gallery (or the computation running) was made with, so the previews never stay
   smaller than the fitted size; a size that allows no finer stride starts nothing. The gallery on
   screen stays until the new one is there, with the "computing" message beside it, and the run
   number drops a superseded result, as before. A result without a gallery (the core's reason,
   shown in its place: no profile, nothing to suggest) is never recomputed for a larger area,
   since its reason does not depend on the size (the review of 2026-11-06).

3. In the Culori pentru hartie tab the box `arata ce nu intra` starts on, and the original
   carries the figures of the image as it is (the share the paper cannot reproduce, its dark
   share, the recipe): on 2026-11-06, testing the branch, the owner looked for both first, and
   a tab that opens on the original with its warning and its figures answers the question the
   tab is for before any variant is chosen.

## Alternatives rejected

- **Compute the first gallery only when the tab is shown.** The owner wants the tab ready when
  it is opened (ADR 0051); the second computation runs in the background anyway.
- **Let the view enlarge a preview.** A 1600 x 900 preview enlarged to a 4K area is blurred; the
  recompute at the real size costs seconds, once.
- **Recompute on every size change.** Each layout pass would start a computation of seconds;
  comparing strides starts one only when the previews would gain pixels.

## Consequences

- R-146 and R-178 say so; `CurvesTabTests` and `GamutFitTabTests` check that a larger area
  recomputes and a smaller one does not, and that the previews are at least the fitted size
  (their areas keep the image's aspect, so that is the area's size there); `CurveTests` the
  stride's two rules on the reference image's size.
- The previews are larger. Measured 2026-11-06 on the plate-solved FITS (5837 x 3867, 22.6 MP)
  through the lab's baryta profile, a scratchpad console over the branch's core (the curves' run
  with three suggestions, the fit's with three variants; the memory is what the set holds after
  the run, before the window makes its bitmaps):

  | area | stride | preview | curves | fit |
  | --- | --- | --- | --- | --- |
  | 1600 x 900 (presumed) | 4 | 1460 x 967, 1.4 MP | 1.6 s, +147 MB | 1.7 s, +159 MB |
  | 3800 x 1900 | 2 | 2919 x 1934, 5.6 MP | 1.3 s, +348 MB | 2.8 s, +526 MB |
  | 3840 x 2160, uncapped | 1 | 5837 x 3867, 22.6 MP | 2.1 s, +889 MB | 8.0 s, +1 626 MB |

  The process peaked at 3.5 GB in the last case, which is why the cap exists; with it the
  last case is the second. Before this change the stride was 5 at the presumed area (0.9 MP).
  The evidence and V-008/V-016 are measured on the tonal subsample and do not move.
- A tab shown on a 4K screen while the first computation (at the presumed area) still runs
  starts the second at once; the first finishes for nothing, its result dropped by the run
  number, and the two run concurrently on the pool for a while. One wasted run, bounded; a
  cancellation of the first would be a larger change, not made.
- `window.md`, `paper-fit.md`, `pipeline.md`, `testing.md` and the README no longer name the
  button.
