# 0025. --histeq mirrors Siril 1.4.4's Histogram display per layer, at full and shrunk resolution

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-histeq.md and
docs/design/siril-histeq-source.md; the decision itself dates from the original design of the
mode, against Siril tag 1.4.4. The mode is not a test: it renders a frame the way Siril's
"Histogram" display mode does, so that patterns invisible in a linear view can be seen, and so
that the window can put a frame beside the master it went into. Siril's code
(`src/gui/image_display.c` `remap()`, `src/gui/histogram.c` `computeHisto()`) does one
histogram per layer with 65536 bins, skips pixels equal to zero, builds
`index[i] = round_to_BYTE(cumulative(i) / nb_pixels * 255)` with the cumulative including bin
`i`, `index[0] = 0`, `nb_pixels` counting zeros, and quantises float data as
`round(v * 65535)`. Measured on a synthetic with 5 % vignetting, a 0.3 % ripple is a third of a
grey level linear and ~15 levels equalised.

## Decision

`HistEqLut.Build` mirrors the per-layer inclusive CDF and the `(byte)(x + 0.5)` rounding, with
bins on the channel's own `[min, max]` of non-zero values (identical on integer data, finer on
float). The CFA decision reuses `Mosaic.LooksCfa` on the four position medians, the `--expo`
rule. On a CFA frame each position is equalised on its own CDF; the composite takes R, G (mean
of the two greens) and B by `CfaPattern` colour, never by position, and the mean of the four
greys when the order is unknown; `Mosaic` is the whole frame as one layer. Every raster exists
twice: at full resolution for the window (100 % is one sensor pixel, the `Mozaic` view shows the
chequerboard as Siril does) and shrunk with `Img.Prepare`'s `k` rule for the console's PNG and
the tests, the shrunk ones derived from the full ones. The console writes the composite; the
window defaults to `Mozaic`. No verdict, no threshold, no `Th` entry; exit 0 when the picture
was written.

## Alternatives rejected

- **Block-meaning before equalising.** It would give a stronger stretch (noise averaged out
  before the CDF) but is not what Siril shows; deliberately not built.
- **Showing the shrunk rasters in the window through PNG.** The first cut did; the user asked
  what zooming into block means was for - nothing.
- **Position medians from sorted copies.** 6 M values each, ~2 s; they now come off the
  65536-bin histogram (`HistEqLut.Median`, within a bin of exact).
- **Shrinking through `double` rasters.** 1.1 GB peak on the master; now a byte block mean with
  the same arithmetic.
- **A Python counterpart and a `crosscheck.py` scenario.** There is no number to compare.

## Consequences

- `TheLutIsTheInclusiveCdfTimes255_RoundedLikeSiril` pins the hand-computed vector
  `{1, 2, 2, 3}` -> `64, 191, 255`; `TheShrunkRastersAreTheBlockMeansOfTheFullOnes` pins the
  derivation; `ATinyRippleInvisibleInTheLinearViewBecomesManyGreyLevels` pins the reason the
  mode exists.
- Amplitude is destroyed by construction - a 0.5 % and a 10 % pattern of the same shape render
  identically, and a clean flat shows its own noise at full contrast. `HistEqReport.Caveat()`
  must keep saying so; the mode never votes.
- The quoted Siril source in `siril-histeq-source.md` stays true for tag 1.4.4 and changes only
  if the mode starts mirroring another version.
- Cost, measured on a D3300 24 MP file in Release: ~2-3 s and 690 MB peak on the float master,
  ~2-3 s and 516 MB on the NEF; the full-resolution rasters cost ~18 MB; the console's PNG
  150-380 ms and 3.6 MB. The window encodes nothing (`RasterBitmaps`).
- Source notes: docs/design/modes-histeq.md, docs/design/siril-histeq-source.md.
