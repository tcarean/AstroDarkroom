# Fix CR-001: the read-out names the pixel that is drawn under the pointer

## Context

`docs/review/2026-11-13-code-review.md`, entry CR-001 (severity 1, common): the status bar's
read-out can name the neighbouring pixel.

- **(a), confirmed.** `Viewport.ZoomTo` leaves an offset an ulp under a whole number, and the
  read-out truncates `screen / scale + offset` with `(int)` (`MainWindow.UpdatePixelReadout`,
  `CompareView.SetCursorContentPoint.Same`). Recomputed again for this plan, in IEEE doubles: a
  5938 x 3888 pair on a 1920 x 976 surface, `Fit` then `100%`, gives
  `OffsetY = 1455.9999999999998`; 593 of the 976 screen rows read one row up; 15 of the 201
  heights from 900 to 1100 do this. Reading at the centre of the physical pixel gives 0 wrong
  rows in the same case.
- **(b), to measure.** The pointer's position is the top-left corner of its physical pixel. From
  400 % up the images are drawn nearest-neighbour; if WPF paints each screen pixel with the image
  pixel at its centre, the first screen row or column of a square reads the pixel before it.

Outcome wanted: the read-out gives the pixel that is drawn under the pointer, in both parts, with
the truncation tested in Core (today it sits untested in the WPF layer).

## The design (one choice for the owner, recommendation first)

**Recommended: Core answers with the whole pixel.** `Viewport.PixelAt(screenX, screenY)` replaces
`Viewport.ContentPointInsideAt`. It takes the pointer's position (the top-left corner of the
physical pixel it is on), adds half a physical pixel each way, converts with `ScreenToContent`,
and returns `ImagePixel?` (a new Core record struct, `int X`, `int Y`): the floor of that point,
or null off the image. `CompareView` and `MainWindow` then hold no arithmetic: the two `(int)`
casts and `Same` go away (record equality), and the event `CursorContentPointChanged` carries
`ImagePixel?`. This follows the rule "Core has no UI ... the controls do no arithmetic".

The half pixel is added to the pointer's position relative to the surface, never after flooring
that position: the surface's origin can sit on a fraction of a physical pixel (CR-002), and the
pointer's own pixel centre is still `position + 0.5`.

Alternative, not recommended: keep `ContentPointInsideAt` returning a `PointD`, add the half pixel
inside it, and leave the casts in the WPF layer. Smaller diff, but the truncation stays untested
and in two places.

Not in this task: whole offsets at a whole scale (CR-002), refreshing the read-out under a resting
pointer (CR-006).

## Steps

1. **Worktree.** `git worktree add .claude/worktrees/Fix-CR-001-read-out-pixel -b
   claude/Fix-CR-001-read-out-pixel main`, from `E:\Photografie\Code`; all builds there.
2. **Measure part (b) on the unfixed build** (the review: "the task starts by showing it"). A
   throw-away harness in the scratchpad (net8.0-windows, referencing the worktree's built DLLs, a
   plain `Application` with `Theme.xaml`) shows the real `MainWindow` with a generated
   1200 x 800 PNG pair whose every pixel encodes its own coordinate in its colour. For each state
   (`Fit` then `100%` on a 620 x 414 surface, where `OffsetX = 289.9999999999999`; 400 % and
   1600 % at fractional offsets) it captures the surface from the screen (`BitBlt`), decodes the
   image pixel drawn on every screen pixel of one row and one column, asks the control for the
   read-out at the same screen pixel (`PointFromScreen`, then the control's own handler path), and
   counts the disagreements. Output pasted in the wrap-up.
   - **On screen, because only that shows the hardware path.** The window is small, at a corner,
     never activated, no taskbar button, up for a few seconds; no input is sent and the pointer
     is not moved. If a full-screen application or a film holds the foreground, it is not shown
     and the check is reported as not done.
   - If WPF turns out not to paint the pixel at the screen pixel's centre, stop and return to the
     owner with the figures.
3. **Requirement.** `docs/requirements.md`, R-028 reworded: the image pixel under the cursor
   (`Viewport.PixelAt`) is the one drawn at the centre of the physical screen pixel the pointer is
   on, as whole coordinates; null on the margin, past the far edge and with nothing loaded.
   Example: the 5938 x 3888 case above, screen row 100 reads row 1556.
4. **Failing tests first** (`PixelAt` first written with today's behaviour, so they run red):
   - `ViewportTests`: `R028_PixelAt_AfterFitThen100Percent_NamesTheRowDrawnOnEveryScreenRow` (the
     review's case, all 976 rows, and row 100 = 1556); the same over the 201 heights 900..1100;
     `R028_PixelAt_FromFourHundredPercent_NamesThePixelAtThePhysicalPixelsCentre` (fractional
     offset, first screen row of a square).
   - The four existing `R028_ContentPointInsideAt_*` tests move to `PixelAt` with the requirement.
     **One assertion changes its expected result and is the owner's to see:** in the far-edge
     test, `(99.5, 99.5)` on a 100 x 100 image was "inside"; under the new R-028 that physical
     pixel's centre is at 100, past the image, so the test asserts `(99, 99)` inside and
     `(99.5, 99.5)` null. The other three keep their values.
   - `CompareViewTests` (new, on `WpfThread`, never shown): the control arranged at 620 x 414 with
     the coordinate-coded pair, rendered with `RenderTargetBitmap`; on one row and one column of
     screen pixels, the pixel drawn equals the pixel the control reports
     (`R028_TheSurfaceReportsThePixelItDraws`, at 100 % after `Fit`, 400 %, 1600 %). An internal
     `CompareView.ReportPointer(Point)` gives the test the handlers' path, as
     `MinimapView.RequestCentre` does. If the software rasteriser disagrees with the screen
     capture of step 2, this test is dropped and that is reported.
5. **Fix.**
   - `src/ImageVisualCompare.Core/ImagePixel.cs` (new): `readonly record struct ImagePixel(int X, int Y)`.
   - `src/ImageVisualCompare.Core/Viewport.cs`: `PixelAt` replaces `ContentPointInsideAt`.
   - `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs`: `UpdateCursorContentPoint`,
     `SetCursorContentPoint`, the field and the event use `ImagePixel?`; `Same` removed.
   - `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs`: `UpdatePixelReadout(ImagePixel?)`, no casts.
   - XML documentation on everything added or edited.
6. **Measure again** with the harness of step 2 on the fixed build: 0 disagreements expected.
7. **Documents.** ADR 0052 (the read-out reads the centre of the physical pixel under the
   pointer; Core returns the whole pixel); `docs/design/rendering.md` (a paragraph on the
   read-out, `ImagePixel.cs` in its `Covers`), `docs/design/code-map.md`, `CLAUDE.md` layout
   (the new Core file), `CHANGELOG.md` under `Fixed`, this plan saved as
   `docs/plans/2026-11-13-fix-cr-001-read-out-pixel.md`. `README.md` and the wiki manual: read,
   no change expected (the read-out's description stays true).
8. **Wrap-up** (`/wrap-up`): `scripts/verify.ps1` green, the diff read back, commit on the task
   branch; a second commit closes CR-001 in the review list with `fixed <commit>` (summary row
   and entry, as FlatCheck's list does). A review of the branch by a subagent before the merge is
   proposed. No merge, no push without the owner's word.

## Not done on my own

- The version stays as it is; a fix of a wrong read-out seems to warrant a patch bump, at the
  owner's word.
- No live mouse or keyboard input; the application's ordinary launch check of the rule "Run the
  application" is covered by the on-screen harness of steps 2 and 6, which runs the real window.

## Verification

- `dotnet test ImageVisualCompare.sln -c Release --filter "FullyQualifiedName~R028"`: red before
  step 5, green after.
- The harness figures before and after (disagreements per state, of how many screen pixels).
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1` green before the commit,
  after `dotnet build --no-incremental -warnaserror`.
- `scripts/check-design.ps1 -Since main` for the notes' index and `Covers` paths.

## As done

- The window of step 2 could not be made 620 wide with its toolbar, and its surface has a
  fractional height (764 x 450.37 physical pixels), so part (a) was shown with an 1854 x 800
  pair, whose horizontal offset after `Fit` then `100%` is 544.9999999999999 there; the
  1200 x 800 pair showed part (b). The figures are in ADR 0052.
- `CompareViewTests` uses the same 1854 x 800 pair on a 764 x 450 surface, and leaves out a
  screen pixel whose centre is within 1/32 of a physical pixel of an image pixel's edge: the
  software rasteriser painted the next pixel on four such screen pixels (ADR 0052).
- The surface's event is renamed `CursorPixelChanged`, since it no longer carries a point.
