# 0053. At a whole scale the image is drawn on whole physical pixels

Date: 2026-11-13
Status: accepted 2026-11-13
Supersedes: 0052 in part (which offsets the read-out converts through)

## Context

`100%` is one image pixel per physical screen pixel (R-022), and the README calls it the zoom to
judge single pixels at. The review of 2026-11-13 found that the image could sit between screen
pixels there (`docs/review/2026-11-13-code-review.md`, CR-002): the offsets are whatever the
centring or the anchor left, below 400 % the bitmaps are drawn with `HighQuality` (R-025), and a
fractional offset at a scale of exactly 1 makes WPF resample. A bright pixel is spread over two
rows, and a difference of one level becomes a fraction of one.

Measured on 2026-11-13, before any change, on the real `MainWindow` shown on screen (render
tier 2, display scale 1, a window of 780 × 560), with a checkerboard pair of one image pixel a
square, 0 and 255, and a capture of the screen:

- The toolbar is 43 physical pixels high and the status bar 27.63, so the surface between them
  is 764 × 450.37: it starts on a whole pixel and ends on a fraction of one. The fraction goes
  into every offset the zoom computes: `Fit` then `100%` leaves 218, 174.815.
- In an area of 154 440 screen pixels of the surface, at `100%`, not one was 0 or 255, in each
  of seven states: after `Fit`; after a drag of 37, 21 pixels; with a scrollbar at offsets
  100.3, 80.7; after the minimap's centring; after `+` then `−`; after a wheel notch in and one
  out; and on a window one pixel wider and taller (offsets 217.5, 174.315). At 400 %, drawn
  nearest-neighbour, none was grey.
- A toolbar made half a pixel taller, which is what its 43 device-independent pixels come to on
  a display scaled 150 % (64.5), makes the surface start at 43.5.

## Decision

- **The pan stays exact, what is drawn is snapped.** `Viewport.OffsetX` and `OffsetY` are the
  pan, as before. `Viewport.DrawnOffsetX` and `DrawnOffsetY` are the offsets the image is drawn
  with: the same at a scale that is not whole, and at a whole scale (the ladder's 100, 200,
  300, 400, 600, 800, 1200 and 1600 %, or a fit that is a whole number) the offset whose product with the scale is the nearest whole number of physical pixels, a
  half going up, and on an axis the image overflows never one past the end of the range.
  `RenderPlanner.For` translates by them, and `Viewport.PixelAt` reads through them, so the
  read-out goes on naming the pixel drawn (ADR 0052).
- **A scale a last bit from a whole number is that number.** Four wheel notches in and four out
  give 1.0000000000000002; within 1e-9 of a whole number, from 1 up, the scale is taken as it,
  but for a fit a last bit under a whole number, which stays the fit: taken as whole, the image
  would be wider than the view by that bit and a scrollbar would show at `Fit` (the residue of
  CR-011, by another way in). Such a fit reads `100%` and is drawn on whole pixels all the same:
  what is drawn counts a scale within the tolerance as whole. The same tolerance decides a half when an offset is rounded, and
  the last whole pixel of a range that ends a last bit under one.
- **The two bars of the main window are laid out rounded to whole physical pixels**
  (`UseLayoutRounding` on the toolbar's and the status bar's `Border`, switched off again on the
  panel inside each). The surface between them then starts and ends on a whole physical pixel.
  The surface itself, its layers and its images stay unrounded (ADR 0012). When the window
  comes onto a display scaled differently, `MainWindow.OnDpiChanged` asks for the two bars to be
  measured again, since their heights were rounded to the other display's pixels.

## Alternatives rejected

- **Round the offsets themselves after every movement.** Less code and one pair of offsets. But
  the half pixel a rounding drops is gone for the next movement: growing the window one pixel
  at a time at `100%` would move the image's far edge only, where R-027 holds its centre still,
  and a pan in steps smaller than a pixel would never move it.
- **Snap at `100%` only.** At 200 % to 1600 % the squares would still start anywhere inside a
  screen pixel, and the states ADR 0052 had to give a rule for, a screen pixel's centre exactly
  on the edge between two image pixels, would still come from the zoom's own steps.
- **Lay the whole window out rounded.** One attribute on the window; it would move every text
  and button of the two bars by a fraction of a pixel and change the widths
  `PairLabelLayout` works out for the file names, for the sake of two heights.
- **Take the surface's position on screen into the translation.** It would correct an origin
  that is off the pixel grid whatever put it there, but needs the surface's place on screen at
  every layout pass, and a second place where device pixels are counted (ADR 0011).

## Consequences

- R-022 says it. `ViewportTests` pins the drawn offsets (the review's case, a fractional view,
  every whole stop, the scales that are not whole, a centred image, the end of the range, the
  scale a last bit from whole) and that twenty resizes of one pixel still hold the centre;
  `RenderPlanTests` the translation; `CompareViewTests` renders the checkerboard at `100%` on
  a surface of odd width and height and compares every screen pixel of an area with the pixel
  stored; `MainWindowTests` lays the window's content out and asks for a whole origin and a
  whole height, also with the toolbar half a pixel taller.
- Measured again on screen after the change, the same window and pair: the status bar is 28
  pixels high and the surface 764 × 450; in each of the seven states at `100%`, 0 of the 154 440
  screen pixels (155 230 on the larger window) is anything but 0 or 255; with the toolbar half a
  pixel taller it is laid out 44 high and the surface starts at 44.
- The read-out against the screen, measured again as for ADR 0052 (two pairs, ten states each,
  19 980 screen pixels): one differs, the screen pixel 0.00028 of an image pixel past an edge
  after ten wheel notches. At a whole scale no screen pixel's centre falls on an edge any more,
  so the edge rule of ADR 0052 now matters only at a scale that is not whole.
- An image smaller than the view, with an odd number of pixels of margin to share, is drawn
  with one pixel more after it than before it; the pan still says half and half (R-021).
- On screen, at a whole scale, an anchor or a centre holds to within half a physical pixel; the
  pan itself holds exactly (R-023, R-024, R-027), and the scrollbars and the minimap show the
  pan.
- 200 % and 300 % stay smooth (R-025): a square there is still blended with its neighbours
  along its edges, evenly now on every side.
- Not measured: a display scaled 125 % or 150 %, none being at hand. There the surface's
  origin rests on WPF's layout rounding, checked here only by making the toolbar half a pixel
  taller at a scale of 1. Nor a window dragged from one display to another scaled differently:
  whether WPF measures the bars again by itself there is not known, so the window asks for it,
  and a test pins only that it asks, by calling `OnDpiChanged`.
- Five tests written for ADR 0052 set offsets at 400 % that are no longer drawn as set
  (300.375, 300.55, 504.375). Four moved to 450 %, where the image is drawn at its offsets and
  a centre can still fall on an edge; the fifth, the odd-width state, now pins the drawn offset
  504.5 and the columns read through it. The odd-heights test reads its first row from the
  drawn offset. Two cases of the surface's test moved to 450 % and 1550 % for the same reason.
