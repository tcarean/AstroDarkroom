# 0058. An edge flagged with the colour of the corner beside it, and less than it, is reported as that corner's reach

Date: 2026-10-27
Status: accepted 2026-10-27
Supersedes: 0018 in part (one line per flagged coloured region)

## Context

On the owner's 2026-08-12 core, section 3 gave two lines for one thing: the bottom-right
corner, warm, the horizon's glow, and the bottom edge, warm, which the owner read as a
contradiction, the middle of that edge being the band and its left part dark. Measured on
2026-10-27 in ten slices across the bottom band: the blue deficit is -1.7 to -2.7 levels on the
left 40 % of the width, under the threshold, and -7 to -12 on the right 20 %, next to the
corner; the middle is the Rift's dust, which the sky-pixel measurement of ADR 0056 leaves out.
The edge's median over its sky pixels, -4.5 in blue, mixes a clean left with an affected right:
the flag is the corner's glow reaching along the edge, not a gradient of the whole edge.

## Decision

In the recommendations, an edge flagged coloured whose colour (warm, cool, greenish or other,
by the deltas) is the colour of a flagged coloured corner beside it, and whose largest absolute
delta is smaller than the corner's, has no line of its own: the corner's line names it as the
corner's reach ("reaching along the bottom edge"), and the corner's "because" carries the
edge's deltas. An edge folds into one corner at most, the first in the analyzer's order that
qualifies; an edge flagged without such a corner, or departing more than it, keeps its line,
so that a gradient along a whole edge is never hidden. Appendix E and the Culoare tab keep the
eight rows as measured.

## Alternatives rejected

- **Leave the two lines.** Honest but coarse: it says "the bottom edge" of a glow that sits at
  its right end.
- **Measure the edges in halves, naming the half.** More precise, but two lines for one
  phenomenon (the corner and the half beside it), the goldens and the validation figures moved
  with the geometry.

## Consequences

- On the 2026-08-12 core section 3 has one horizon line, the bottom-right corner's, reaching
  along the bottom edge, and the top-left darkening.
- Guards: `RecommendationsTests` (R-093), the golden files (the dark scene flags its corner
  alone, so they do not move).
