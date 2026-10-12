# 0013. A dither's move on the sky comes from the WCS with Siril's rows flipped; the arrow words are SynScan's default keys

Date: 2026-10-09
Status: accepted 2026-10-09; the SynScan default keys and the no-input choice superseded by 0014

## Context

The owner wanted to see, per frame, which SynScan arrow the dither was, and asked whether a
plate solve could tell. The series already knows which image axis is RA (R-025) but not the
sense along it: east or west, north or south. The WCS of Siril's solved stack
(`result-bge-cc.fit` on Veil: `CDELT1 < 0`, `PC1_2 = -1`, `PC2_1 = +1`) gives a sense, but
Siril stores FITS rows bottom-up while LightsCheck reads the raws top-down, so the Y sense
could be flipped. Measured on 2026-10-09 with `siril-cli` plate solves of the raw lights
`pp_light_00001` and `pp_light_00061` (private config copy, FlatCheck ADR 0055): the field
centre moved 0.2431 degrees west and 0.2208 degrees south between them, while LightsCheck's
registration had the stars shift +128.2 px in X and -142.2 px in Y. Applying the WCS to the
pointing's move `(-dx, -dy)` gives the wrong RA sign; applying it to `(-dx, +dy)` gives
-0.2440 and -0.2216 degrees, the measurement. So the Y axis is flipped between the two
frames, and in LightsCheck's pixels `+X` is north and `+Y` is west. The drift of Veil,
-0.48 px/min along Y, is then stars sliding east, what a mount that lags the sky does; and
the nine DEC presses of the night were all to the south. (Corrected 2026-10-12: stars sliding
east in the frame are the camera moving west against them; a mount that lags the sky makes them
slide west, so the drift is not a lag. The senses stand: four more plate-solved frames agree
with them, `docs/validation.md`, History, 2026-10-12.) Which arrow moves the mount which
way is a setting of the SynScan app; its default, pasted by the owner from a user forum, is
up = north, down = south, left = east, right = west.

## Decision

The core keeps the whole WCS of the solved FITS at the root (`Wcs`: `CDELT` and the `PC`/`CD`
matrix) and maps a registered move's star shift `(dx, dy)` to the mount's move on the sky as
the WCS applied to `(-dx, +dy)`, in pixels of the plate scale, positive east and north. The
words name the sense (`E`/`V`, `N`/`S`) and the SynScan default arrow (`stanga` east,
`dreapta` west, `sus` north, `jos` south) with no input to change them; an owner who reversed
the keys in the app reads the words reversed, and `README.md` says so. Without a WCS the
axis from the trails gives the components without a sense; without an axis, nothing. A
component under `Th.AxisStep` is not worded, the same bar a component clears to count in the
direction tables (R-020). The cell goes in the frame table, the CSV and the window's Cadre
tab, and in a tooltip on the step bars worded by the core.

## Alternatives rejected

- **Two inputs for the arrows, remembered between runs** (the owner's first choice, option
  A in chat). The SynScan default settles the mapping for the owner's app; two inputs and a
  settings file would exist for a reversed setting nobody has. Easy to add if one appears.
- **Trusting the stack's WCS without the flip.** It gives the wrong RA sense on Veil, as the
  two-frame measurement shows; the decision is measured, not assumed.
- **Plate-solving the raws inside LightsCheck.** It needs Siril or a solver and a catalogue at
  run time; the stack's WCS the Siril workflow already leaves at the root is enough.

## Consequences

`R038` pins the mapping on the Veil shift with a hand-made `Wcs` and on the fixture with a
solved FITS at the root; V-011 pins the words on the Veil session. A later session whose
solved FITS was not written by Siril (rows top-up) would read its senses reversed: the flip
is Siril's, stated here and in the design note, and the place to revisit if the workflow
changes.
