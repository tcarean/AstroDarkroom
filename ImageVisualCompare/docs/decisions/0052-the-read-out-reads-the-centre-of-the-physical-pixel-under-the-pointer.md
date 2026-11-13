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
in four at 400 %), and where it falls in the second half it was right.

**A centre exactly on an edge.** Reading the centre and rounding down, the first form of this
fix, names the pixel after an edge that the centre is exactly on. The review of the branch
pointed at the states where that happens, and they are ordinary ones: `Fit`, `100%`, `+` four
times on a surface of odd width leaves the horizontal offset ending in .375, and one screen
column in four has its centre on an edge. (Restated 2026-11-13: since ADR 0053 the image is
drawn on whole physical pixels at a whole scale, so that state no longer puts a centre on an
edge; one can still fall on an edge at a scale that is not whole.) Measured on screen the same day, with offsets that put
the centres exactly on edges (300.375 and 200.625 at 400 %, 300.3125 and 200.4375 at 800 %,
300.21875 and 200.53125 at 1600 %):

| | Screen pixels with the centre on an edge | Painted from the pixel before the edge |
|---|---:|---:|
| 400 %, columns / rows | 160 / 90 | 160 / 87 |
| 800 %, columns / rows | 80 / 45 | 80 / 45 |
| 1600 %, columns / rows | 40 / 23 | 40 / 23 |

and, moving the centre off the edge by a fraction of an image pixel, on columns 300 to 460 of the
1200 pixel wide pair: short of the edge by any amount, always the pixel before; past it by
1/16384, 156 of 160 columns still from the pixel before; by 1/4096, 107; by 1/2048, 26; by
1/1024, none. Near column 3800 of a 4000 pixel wide pair the adapter is less exact: past the
edge by 1/2048, 121 of 160 columns are still painted from the pixel before, by 1/1024, 80, by
1/512, one, by 1/256, none. The same fractions hold at 800 % and 1600 %, so the band is a
fraction of an image pixel, not of a screen pixel.

WPF's software rasteriser, which draws under Remote Desktop and renders a `RenderTargetBitmap`,
does the opposite: with the same offsets it paints every screen pixel whose centre is on an
edge from the pixel after it (0 of 148 columns and 0 of 85 rows from the pixel before, at
400 %; the same at 800 % and 1600 %).

## Decision

- `Viewport.PixelAt` answers "which image pixel is under the pointer". It adds half a physical
  pixel each way to the pointer's position, converts that point with `ScreenToContent`, and
  names the image pixel that holds it; off the image it is null. It replaces
  `Viewport.ContentPointInsideAt`.
- An edge belongs to the pixel before it: a point exactly on the edge between two image pixels,
  or up to `Viewport.EdgeReach`, 1/4096 of an image pixel, past it, is in the pixel before. That
  is what the display adapter paints, and it puts a centre that rounding left an ulp to either
  side of an edge on one side of it.
- The read-out follows the display adapter, not the software rasteriser: the two differ only
  where a centre is exactly on an edge, and the adapter is what draws the window on the
  owner's displays.
- The half pixel is added to the position as the surface receives it, never after rounding
  that position: the surface's own origin can sit on a fraction of a physical pixel, and the
  centre of the pointer's physical pixel is still half a pixel further.
- Core returns the whole pixel, an `ImagePixel` (`int X`, `int Y`). The surface's event,
  `CompareView.CursorPixelChanged`, carries it, and neither the surface nor the main window
  cuts a fraction off any more (the rule "Core has no UI").

## Alternatives rejected

(Restated 2026-11-13: "converts that point with `ScreenToContent`" in the decision is, since
ADR 0053, through the offsets the image is drawn with, which at a whole scale are not the pan's
that `ScreenToContent` uses.)

- **Keep the offsets whole at a whole scale and leave the read-out alone.** That is CR-002's
  direction and settles part (a) only: at 400 % and above a fractional offset is ordinary, after
  any wheel zoom, and part (b) stays.
- **Add a small constant before cutting the fraction off, at the pointer's corner.** Settles
  part (a) only.
- **Read the centre and round down, an edge going to the pixel after it.** The first form of
  this fix. Right everywhere but on an edge, and there wrong on screen: one column in four at
  400 % after `Fit`, `100%`, `+` four times on a surface of odd width, where the read-out before
  this decision was right.
- **A reach that grows with the image coordinate**, to follow the adapter's coarser arithmetic
  far from the image's origin. The band between "always before" and "always after" is not a
  line but a mix, screen pixel by screen pixel; a centre inside it is a rare accident of a wheel
  zoom, and no constant gets it right.
- **Follow whichever renderer draws the window**, the adapter or the software rasteriser. It
  would need the render tier of the window in Core's answer, for the one case of a centre
  exactly on an edge under Remote Desktop; left for the owner to ask for.
- **Round the pointer's position to a whole physical pixel first.** Wrong wherever the surface
  does not start on a whole physical pixel.
- **Add the half pixel in Core but return the point, and keep the two `(int)` casts in the WPF
  layer.** A smaller change; the step from a point to a pixel would stay in two untested places.

## Consequences

- R-028 says it. `ViewportTests` has the review's case, the even heights from 900 to 1100, the
  odd ones, an offset set an ulp under, the 400 % case (Restated 2026-11-13: at 450 % since ADR 0053,
  with the edge cases after it), a centre exactly on an edge and just
  past one, and the odd-width state above; `CompareViewTests` renders the surface and compares,
  screen pixel by screen pixel, the image pixel drawn with the one reported.
- Measured again on screen after the change: the 1200 × 800 pair on surfaces 764 and 765 wide
  and the 1854 × 800 pair on 764, nine states each drawn nearest-neighbour (the five of the
  first table from 400 % up, the three with centres on edges, and `Fit`, `100%`, `+` to
  1600 %), 26 982 screen pixels compared (a row of 639 and a column of 360 in each state, the
  row 640 on the wider surface). The read-out names another pixel than the one drawn on
  12: nine have
  the centre exactly on an edge at 400 % and were painted from the row after it (3 of the 90
  such rows, in each run), two the same at 1600 %, and one has its centre 0.00028 of an image
  pixel past an edge after ten wheel notches and was painted from the pixel before. At `Fit`
  then `100%` with the horizontal offset 544.9999999999999, or 218, none of the 639 columns
  differs.
- Those are the limit: on an edge, and for about a thousandth of an image pixel past it, the
  adapter's own arithmetic decides, screen pixel by screen pixel, and the read-out cannot
  follow it.
- Under software rendering (Remote Desktop, or a render tier of 0, which the log gives at
  `Debug`) a screen pixel whose centre is exactly on an edge is painted from the pixel after it
  and read as the one before: one column in four at 400 % in the states above. (Restated
  2026-11-13: not at a whole scale any more, ADR 0053; at a scale that is not whole, wherever a
  centre falls on an edge.)
  `CompareViewTests` renders with that rasteriser, so it leaves out a screen pixel whose centre
  is within 1/32 of a physical pixel of an edge; besides the ties, that rasteriser steps along a
  row in fixed point and painted the next pixel on four screen pixels of a 764 pixel row whose
  centres were 0.0011 to 0.0079 of a physical pixel short of an edge. What an edge belongs to is
  pinned in `ViewportTests`, on the figures measured on screen.
- At the image's own edges: a physical pixel whose centre is exactly on the far edge still
  reads the last image pixel, and one whose centre is exactly on the near edge reads nothing.
  A physical pixel whose corner is on the image but whose centre is past the far edge reads
  nothing (before, it read the last image pixel), and one whose centre is just inside the near
  edge reads pixel 0 (before, nothing).
- Where a screen pixel is a blend of image pixels, below 400 %, the read-out names the image
  pixel that holds the screen pixel's centre. From 100 % up that is the one that weighs most in
  the blend, unless the centre is on an edge, where the two weigh the same and the one before
  is named; below 100 % a screen pixel covers several image pixels and the one named is one of
  them. At 100 % with the image a fraction of a pixel off (CR-002, still open; Restated 2026-11-13:
  closed by ADR 0053) the read-out
  before named the lighter of the two: on the surface above, the vertical offset was 174.815
  and every row read was the one above the row drawn at 81.5 %.
- (Restated 2026-11-13: since ADR 0053 the image is not drawn half a pixel off at 100 %; what
  follows held between the two decisions.) At 100 % with the image exactly half a pixel off, on
  a surface of odd width, every screen pixel is on an edge. All of them read the pixel before, also when the zoom left the offset
  an ulp under its half: no row is read twice and none is skipped.
- A canvas drawn thinner than one screen pixel, an extreme shape at `Fit`, may have no screen
  pixel whose centre is on it, and then reads nothing anywhere.
- A threshold has its own edge: an offset built to put a centre one ulp past the reach reads
  an image pixel on two screen pixels in a row at 100 %. No state the zoom steps or `Fit` leave
  was found to do it.
- The wheel's zoom still holds still the image point at the corner of the pointer's physical
  pixel, half a physical pixel from the point the read-out names; it anchors a zoom and names no
  pixel (R-023).
- CR-006, a read-out not refreshed when the view changes under a resting pointer, is untouched.
