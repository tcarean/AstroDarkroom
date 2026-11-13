# Fix CR-002: at 100 % the image sits on whole screen pixels

## Context

`docs/review/2026-11-13-code-review.md`, entry CR-002 (severity 1, common, to measure): at a
scale of exactly 1 the offsets are whatever the centring or the anchor left, and below 400 % the
bitmaps are drawn with `HighQuality`, so a fractional offset at `100%` makes WPF resample. A
single bright pixel is spread over two rows, and a difference of one level becomes a fraction of
a level, in the zoom the README calls the honest one (R-022).

What this session already measured on screen for CR-001 (the real window, render tier 2, display
scale 1):

- The surface is 764 x 450.37 physical pixels in a 780 x 560 window: its height is fractional,
  because the status bar's is. Its origin on screen is whole (48, 114).
- `Fit` then `100%` there leaves the vertical offset 174.815: every screen row is a blend of two
  image rows (81.5 % and 18.5 %). On a surface of odd width the horizontal offset is 217.5: an
  even blend of two columns.
- On a display scaled 150 % the toolbar's 43 DIPs are 64.5 physical pixels, so the surface
  would start half a pixel off (the review's figure; no scaled display is at hand here).

Outcome wanted: at `100%` each physical pixel shows exactly one stored pixel, whatever the
window's size and however the view got there.

## The design (one choice for the owner, recommendation first)

**Recommended: the pan stays exact, what is drawn is snapped.** `Viewport` keeps `OffsetX` and
`OffsetY` as they are today, the pan the user set. It gains `DrawnOffsetX` and `DrawnOffsetY`:
equal to the offsets, except at a whole scale (1, 2, 3, 4, 6, 8, 12, 16), where they are the
nearest offset that puts the image on whole physical pixels (`Offset x Scale` rounded, half up,
and never past the image's end). `RenderPlanner.For` translates by the drawn offsets and
`Viewport.PixelAt` reads through them, so the read-out still names the pixel drawn (ADR 0052).
The anchors, the centring, the scrollbars and the minimap go on using the exact offsets, so
every existing requirement about them holds as written.

Alternative, not recommended: round the offsets themselves after every movement. Smaller, but
the half pixel is lost each time, and it adds up: growing the window one pixel at a time at
`100%` would hold the image's left edge still instead of its centre (R-027), and a drag in
steps smaller than a pixel would never move it.

Two more parts:

- **A scale that should be whole is whole.** `ZoomTo` takes a scale within 1e-9 of a whole
  number as that number, so one wheel notch in and one out, which gives 100 % but for the last
  bit, is 100 %.
- **The surface starts and ends on whole physical pixels.** `UseLayoutRounding="True"` on the
  two bars of `MainWindow.xaml` (the toolbar's and the status bar's `Border`), and `False` on
  the panel inside each, so only the bars' heights are rounded and nothing inside them moves.
  The surface between them then has a whole origin and a whole size on any display scale.
  `CompareView.xaml` already switches rounding off for itself and its images, and stays so.

Not in this task: 200 % and 300 % stay smooth (R-025), so a pixel there is still blended with
its neighbours at the edges of its square; CR-006; CR-011.

## Steps

1. **Worktree.** `git worktree add .claude/worktrees/Fix-CR-002-whole-pixels-at-100-percent -b
   claude/Fix-CR-002-whole-pixels-at-100-percent main`, in `E:\Photografie\Code\ImageVisualCompare`.
2. **Measure on the unfixed build**, with the harness of CR-001 (still in the scratchpad; the
   real `MainWindow` on screen, small, at a corner, never activated, a few seconds, no input;
   skipped if a full-screen application holds the foreground). A generated checkerboard pair,
   one image pixel per square, 0 and 255: on screen every pixel of the image must be 0 or 255,
   and the harness counts the greys in an area clear of the minimap, the badge, the bars and the
   line. States, all at `100%`: after `Fit`; on a window one pixel wider and one taller; after a
   scrollbar set to a fractional offset; after the minimap's centring; after `+` then `-`; after
   one wheel notch in and one out. Also the surface's origin and size, and the bars' heights.
3. **Requirement.** `docs/requirements.md`: R-022 gains the sentence: at a whole scale the image
   is drawn on whole physical pixels (`Viewport.DrawnOffsetX`, `DrawnOffsetY`: the offset times
   the scale rounded to the nearest whole number, half up, not past the image's end), so at
   `100%` each physical pixel shows one stored pixel; the pan itself keeps its fraction, and the
   surface's edges are on whole physical pixels. Example: the 5938 x 3888 pair on a 1920 x 947
   surface, `Fit` then `100%`: offset 1470.5, drawn at 1471. R-028 reads through the drawn
   offsets (one clause).
4. **Failing tests first.**
   - `ViewportTests`: `R022_` tests for the drawn offsets: the example above; a fractional
     height (946.37); every ladder stop from 100 % up after a fractional `SetOffset`; a scale
     that is not whole leaves the offset as it is; a centred image smaller than the view; the
     end of the range (never past `MaxOffset`); the offsets themselves unchanged (resize one
     pixel at a time still holds the centre); `ZoomTo` with 1.15 then 1 / 1.15 gives exactly 1.
   - `RenderPlanTests`: `R022_` the translation is the negated drawn offset.
   - `CompareViewTests`: `R022_AtOneHundredPercent_EveryScreenPixelShowsOneStoredPixel`, the
     checkerboard rendered with `RenderTargetBitmap` on a surface of odd width and height after
     `Fit` then `100%`: no grey.
   - `MainWindowTests`: `R022_TheSurfaceStartsAndEndsOnWholePixels`, the window's content laid
     out, never shown: the surface's origin and size are whole.
   - **Tests of CR-001 that this changes, for the owner to see.** Three `R028_` tests of
     `ViewportTests` set offsets at 400 % that cannot be drawn any more (300.375, 504.375: the
     image would now be drawn a quarter pixel away), to put a screen pixel's centre exactly on
     an edge. They move to a scale that is not whole (450 %), where such a centre still occurs;
     the rule they pin, an edge belongs to the pixel before it, is unchanged. The odd-heights
     test keeps its figures and loses the comment about a half-pixel image. Any other existing
     test that fails is reported, not edited.
5. **Fix.**
   - `src/ImageVisualCompare.Core/Viewport.cs`: `DrawnOffsetX`, `DrawnOffsetY`; `PixelAt` through
     them; `ZoomTo` takes a nearly whole scale as whole.
   - `src/ImageVisualCompare.Core/RenderPlan.cs`: the translation from the drawn offsets.
   - `src/ImageVisualCompare.Wpf/MainWindow.xaml`: the rounding on the two bars.
   - XML documentation on everything added or edited.
6. **Measure again** with the harness: no grey in any state. The origin on a scaled display
   cannot be measured here; the harness checks the mechanism instead (a bar given a height of
   half a pixel more must still leave the surface on a whole pixel) and that is said as such.
7. **Documents.** ADR 0053 (drawn offsets at a whole scale; the bars rounded); a dated
   `Restated` note in ADR 0052 where it calls "`Fit`, `100%`, `+` four times on an odd width" a
   state with centres on edges, which no longer occurs; `docs/design/rendering.md`,
   `docs/design/testing.md`, `CHANGELOG.md` under `Fixed`, this plan saved as
   `docs/plans/2026-11-13-fix-cr-002-whole-pixels-at-100-percent.md`; `README.md` read (its
   "honest" 100 % becomes true; no change expected).
8. **Wrap-up** (`/wrap-up`): the gate, `/validate` (Core is numeric code), the diff read back,
   commit on the task branch, CR-002 closed in the review list with its commit, a review of the
   branch by a subagent and one of its fixes, then the merge proposed. No merge or push without
   the owner's word.

## Not done on my own

- The version stays; no live input; the launch on the real pair with its log read waits for the
  owner to be away, as for CR-001, and is reported as not done if so.
- A display scaled 125 % or 150 % is not at hand: that part rests on WPF's layout rounding and
  on the mechanism check of step 6, and the owner may want to look once on such a display.

## Verification

- `dotnet test ImageVisualCompare.sln -c Release --filter "FullyQualifiedName~R022"`: red before
  step 5, green after; the `R028_` tests green after.
- The harness's counts of grey pixels per state, before and after, pasted.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1` green after
  `dotnet build --no-incremental -warnaserror`; `scripts/check-design.ps1 -Since main`.

## As done

- The offsets the tests of CR-001 set at 400 % could not stay: five tests changed, not three.
  Four moved to 450 % (a centre on an edge, just past one, an edge's reach, the corner against
  the centre); the odd-width state keeps its pan, 504.375, and now pins the drawn offset 504.5
  and the columns read through it. The odd-heights test reads its first row from the drawn
  offset, so its figures changed too, by one row. Two cases of the surface test moved to 450 %
  and 1550 % for the same reason.
- A half is taken up also when the offset is within 1e-9 under it: the zoom leaves the pan an
  ulp under its half at 14 of the 100 odd heights from 901 to 1099, and without this the image
  would be drawn a pixel away on those alone.
- The scale is settled wherever it is set, in `ZoomToFit` and after a resize too, not only in
  `ZoomTo`.
- The harness's own "origin on screen" came from `PointToScreen`, which rounds to whole pixels;
  the origin is now read inside the client area, with its fraction. The figures of ADR 0052 do
  not rest on it.
- One wheel notch in and one out, which the plan names, gives exactly 1 in doubles; four in
  and four out give 1.0000000000000002, and that is the case tested and measured against.
- The review of the branch added: a fit a last bit under 100 % stays the fit (settled, it showed
  a scrollbar at `Fit`); the two bars are measured again when the display's scale changes; the
  window's test no longer assumes a system scale of 100 %.
- The review of those fixes added: a fit kept a last bit under 100 % is still drawn on whole
  pixels; ADR 0053 supersedes ADR 0052 in part, the read-out converting through the drawn
  offsets. The harness ran on screen again after each of the two commits of fixes, with no grey
  pixel in any state.
- The application was not launched on the real pair with its log read (`docs/skills.md`,
  `/wrap-up` step 9): the owner was at the PC. The real window ran on screen in the harness.
