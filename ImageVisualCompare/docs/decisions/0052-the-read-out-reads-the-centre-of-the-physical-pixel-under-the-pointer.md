# 0052. The read-out reads the centre of the physical pixel under the pointer

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

The status bar names the image pixel under the pointer and reads its value from both images
(R-028, R-051). The review of 2026-11-13 found that it could name the neighbouring pixel
(`docs/review/2026-11-13-code-review.md`, CR-001), in two ways:

- **(a)** `Viewport.ZoomTo` can leave an offset an ulp under a whole number, and the read-out
  cut the fraction off `screen / scale + offset`. A 5938 × 3888 pair on a 1920 × 976 surface,
  `Fit` then `100%`, has the vertical offset 1455.9999999999998, and 593 of the 976 screen rows
  read the row above; 15 of the 101 even heights from 900 to 1100 do this.
- **(b)** the pointer's position is the top-left corner of the physical pixel it is on, and the
  review could not say from the code which image pixel WPF paints on a screen pixel that an
  edge between two image pixels crosses.

Part (b) was measured on 2026-11-13, before any change, on the real `MainWindow` shown on screen
(render tier 2, display scale 1, a surface of 764 × 450.37 physical pixels), with a pair whose
every pixel carries its coordinates in its colour: a capture of the screen says which image
pixel is drawn on a screen pixel, and the surface was asked for the read-out at the same screen
pixel. Along one row of 639 screen pixels and one column of 360:

| State | Read-out names another pixel than the one drawn |
|-------|--------------------------------------------------|
| 1854 × 800 pair, `Fit` then `100%`, horizontal offset 544.9999999999999 | 470 of 639 columns |
| 1200 × 800 pair, 400 %, offsets 300.55, 200.45 | 0 of 639 columns, 90 of 360 rows |
| the same pair, `Fit`, `100%`, `+` four times (400 %, offsets 504.5, 343.70375) | 0 columns, 90 rows |
| the same pair, `Fit`, `100%`, ten wheel notches (404.56 %) | 77 columns, 44 rows |
| the same pair, 400 %, offsets 300.3, 200.6 | none |
| the same pair, 1600 %, offsets 300.21, 200.77 | none |

So WPF paints a screen pixel with the image pixel at that screen pixel's centre: where an edge
falls in the first half of a screen pixel the read-out named the pixel before it (one screen row
in four at 400 %), and where it falls in the second half it was right. In every wrong case the
read-out was one pixel before the one drawn.

## Decision

- `Viewport.PixelAt` answers "which image pixel is under the pointer". It adds half a physical
  pixel each way to the pointer's position, converts that point with `ScreenToContent`, and
  takes it down to whole coordinates; off the image it is null. It replaces
  `Viewport.ContentPointInsideAt`.
- The half pixel is added to the position as the surface receives it, never after rounding
  that position: the surface's own origin can sit on a fraction of a physical pixel, and the
  centre of the pointer's physical pixel is still half a pixel further.
- Core returns the whole pixel, an `ImagePixel` (`int X`, `int Y`). The surface's event,
  `CompareView.CursorPixelChanged`, carries it, and neither the surface nor the main window
  cuts a fraction off any more (the rule "Core has no UI").

## Alternatives rejected

- **Keep the offsets whole at a whole scale and leave the read-out alone.** That is CR-002's
  direction and settles part (a) only: at 400 % and above a fractional offset is ordinary, after
  any wheel zoom, and part (b) stays.
- **Add a small constant before cutting the fraction off.** Settles part (a) only, with a
  number nothing justifies.
- **Round the pointer's position to a whole physical pixel first.** Wrong wherever the surface
  does not start on a whole physical pixel.
- **Add the half pixel in Core but return the point, and keep the two `(int)` casts in the WPF
  layer.** A smaller change; the step from a point to a pixel would stay in two untested places.

## Consequences

- R-028 says it. `ViewportTests` has the review's case, the even heights from 900 to 1100, an
  offset set an ulp under, and the 400 % case; `CompareViewTests` renders the surface and
  compares, screen pixel by screen pixel, the image pixel drawn with the one reported. Nine of
  these failed on the arithmetic before.
- Measured again on screen after the change, the same states and both pairs: of 11 988 screen
  pixels compared, the read-out names another pixel than the one drawn on one, whose centre is
  0.0011 of a physical pixel past an edge (the 1854 × 800 pair after ten wheel notches).
- That one is the limit: within about a hundredth of a physical pixel of an edge the
  rasteriser's arithmetic decides which pixel is painted, and the read-out's cannot follow it.
  WPF's software rasteriser, which renders the test's bitmap, painted the next pixel on four
  screen pixels of a 764 pixel row whose centres were 0.0011 to 0.0079 of a physical pixel short
  of an edge; the test leaves out a screen pixel whose centre is within 1/32 of a physical pixel
  of one.
- The edges of the image move by half a physical pixel: a physical pixel whose centre is past
  the image's far edge reads nothing (before, one whose corner was still inside read the last
  image pixel), and one whose centre is just inside the near edge reads pixel 0 (before, it
  read nothing).
- Where a screen pixel is a blend of image pixels, below 400 %, the read-out names the image
  pixel that holds the screen pixel's centre, which is the one that weighs most. At 100 % with
  the image half-way between screen pixels (CR-002, still open) the read-out before named the
  lighter of the two: on the surface above, the vertical offset was 174.815 and every row read
  was the one above the row drawn at 81.5 %.
- CR-006, a read-out not refreshed when the view changes under a resting pointer, is untouched.
