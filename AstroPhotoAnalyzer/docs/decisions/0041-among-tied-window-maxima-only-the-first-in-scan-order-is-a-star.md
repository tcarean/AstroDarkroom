# 0041. Among tied window maxima only the first in scan order is a star, and the window is the threshold's

Date: 2026-10-23
Status: accepted 2026-10-23
Supersedes: 0016 in part (ties allowed: a flat peak was measured once per tied pixel)

## Context

ADR 0016 made a star candidate "the maximum, ties allowed, of its 25 x 25 window". The code
review of area 2 (2026-10-23) found that a flat two-pixel peak (two adjacent pixels at the
same value, both maxima of their windows) therefore passed twice and was measured as two stars
(item 14), and that `Thresholds.StarWindow` was read by no code: `StarAnalyzer` had its own
`Half = 12`, against ADR 0012 and R-070 (item 12). The owner answered on 2026-10-23: "astea vrem
sa le fixam pe toate, conform recomandarilor tale": ties stay allowed, but among equal maxima in
a window only the first in scan order is a star; `StarAnalyzer` reads `Thresholds.StarWindow`,
with `Half = StarWindow / 2` and the border buffer from it.

## Decision

A candidate is still the maximum of its window, ties allowed, but a pixel equal to it that comes
earlier in scan order (a row above, or the same row to its left) disqualifies it: a flat peak is
counted once, at its first pixel (R-070). The window's side is `Thresholds.StarWindow` (25), the
half-width `StarWindow / 2`, and the border left out is that half-width.

## Alternatives rejected

- **Strict maxima only.** A flat two-pixel peak, common on a sampled star whose centre falls
  between two pixels, would be no star at all.
- **Merge tied candidates after the scan.** More code for the same answer; the scan-order rule
  decides in the window test itself.
- **Leave `Half = 12` and delete the threshold.** The threshold is where every window size is
  quoted (R-070, `thresholds.md`); the code should read it.

## Consequences

- The star figures of the validation cases may move a little where flat peaks were counted
  twice; the drift table of the task's plan records them.
- Guards: `R070_AFlatTwoPixelPeakIsMeasuredOnce`, `R070_TheWindowIsTheThresholdsStarWindow`.
