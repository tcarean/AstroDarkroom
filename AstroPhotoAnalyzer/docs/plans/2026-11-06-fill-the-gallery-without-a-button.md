# Fill the gallery without a button

Date: 2026-11-06. Branch `claude/Fill-the-gallery-without-a-button`, worktree
`.claude/worktrees/Fill-the-gallery-without-a-button` (from `main` at 28d2c226, the kit's rule
"Worktrees"). The owner's question, right after the paper fit's merge (fa1a77f7): why the
`Calculeaza` button in the new tab, why not automatic, and why the image shrinks after the
press. The owner chose, in chat, the fix in both gallery tabs.

## Context

Both gallery tabs (Editare Curbe, R-146; Culori pentru hartie, R-178) are computed right after
the analysis, before the tab was ever laid out, so the previews are made for a presumed area of
`CurvePreviewDefaultWidth` x `CurvePreviewDefaultHeight` (1600 x 900); `Calculeaza` recomputed
them at the area's real size (ADR 0051's pattern). The reduction's stride is the smallest integer
whose subsample fits inside the area, so a preview is between 80 and 100 % of the area and the
view, which only shrinks, never fills it: the image gets smaller after the press.

## The change

1. `CurveRun.PreviewStride`: the largest integer stride whose subsample still covers the area
   in both dimensions (`floor(min(W / maxWidth, H / maxHeight))`, at least 1), so the preview is
   at least as large as the area and the view shrinks it to fit exactly. The previews grow (the
   default area gives stride 3 on a 23 MP image, 1946 x 1289, instead of 5); the evidence and
   V-008/V-016 are measured on the tonal subsample, so no figure moves.
2. Both view-models: the `Calculate` command and the button go. `SetGallerySize(w, h)`, called
   by the view at every layout pass, starts `ComputeAsync` by itself when a gallery exists (or a
   computation runs) whose previews are smaller than the new area in either dimension; the run
   number drops a superseded result; the gallery on screen stays until the new one is there,
   with the "computing" message beside it. The size the running computation was asked for is
   remembered, so a second layout pass of the same size starts nothing.
3. R-146 and R-178 rewritten (no button; the previews at least the area's size; the automatic
   recompute); ADR 0084 (supersedes ADR 0051 in part: the button); the tests of both tabs
   (`Calculate` gone; a `SetGallerySize` larger than the previews recomputes, a smaller one
   does not); `window.md`, `paper-fit.md`, `pipeline.md`, `testing.md`, README, CHANGELOG.

## Verification

The gate; `APA_DUMP_SCREENS` renders of both tabs; `/validate` on V-008 and V-016 unchanged.

## Record

Three reviews changed the rule as implemented: the stride covers the size the view shows the
image at (`floor(max)`, the limiting dimension), a cap of `CurvePreviewMaxPixels` (8 MP) with
the measured cost at 4K, the view enlarging only a capped preview, and no recompute after a
result without a gallery. ADR 0084 holds the final rule; the plan above is the first one.
