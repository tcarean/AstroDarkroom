# 0063. The histeq picture's false colours are Siril's rainbow table on one layer, never on the composite

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

`--histeq` and the window's Histogram tab mirror Siril 1.4.4's "Histogram" display (ADR 0025).
Siril has a second toggle beside it, **color-map** (`RAINBOW_COLOR`, the false-colour
rendering), and the owner asked for the same, after Siril's algorithm. Read in the source at
tag `1.4.4` (`src/gui/image_display.c`, `src/algos/colors.c`; quoted in
`docs/design/siril-histeq-source.md`): `make_index_for_rainbow` builds a 256-entry table with
`h = 300/360 - i * (300/360)/255`, `s = v = 1`, `hsv_to_rgb` with integer sectors and
`round_to_BYTE`, so level 0 is magenta, 51 blue, 102 cyan, 153 green, 204 yellow, 255 red; it
is applied in `remap(vport)` to the equalised 8-bit layer, after the negative toggle, one layer
at a time; the RGB tab (`remaprgb`) takes the R byte of the R layer's colour, the G byte of the
G layer's and the B byte of the B layer's. Measured on 2026-10-26 on the real masters (the
console's `--histeq` picture, then the table in Python): the Samyang ISO1600 f/2 sky master
shows round, slightly off-centre rings of equal rank; the Tokina tablet-and-paper master shows
its rounded-square shape at a glance, where the grey picture is a blur; the NEF shows coloured
grain. Siril's literal RGB mix gives colours outside the palette wherever the channels differ
in shape (the NEF), and nothing can be read from them; one layer coloured is clean. A coloured
raster is 3 bytes per pixel: the full-resolution Mosaic of a 24 MP frame is 72 MB, a channel
18 MB, against ~18 MB for every grey raster today.

## Decision

The false colours are Siril's table, built exactly as `make_index_for_rainbow` and
`hsv_to_rgb` (`RainbowLut` in `HistEqAnalyzer.cs`), applied after equalisation to a
**one-layer** raster only: the Mosaic, a channel, or the mono frame. The RGB composite is never
coloured; in the window the chip is disabled on it and a line under the picture says so
(`HistEqReport.RainbowNotOnRgb`, the window's alone; the console prints only the table's legend). The console gets a
modifier flag, `--rainbow`, Siril's name for the state, which writes the Mosaic on a CFA frame
and the channel on mono, shrunk first and coloured after, so every colour in the file is a
level of the table; `--rainbow` without `--histeq` is a parse error. The legend is one sentence
from the core (`HistEqReport.RainbowLegend`), under the picture and in the report: magenta the
lowest level, then blue, cyan, green, yellow, red the highest. The window's chip, "False
colours" / "Culori false", is off by default and kept across view and file changes within the
session; the coloured bitmaps enter the per-view cache.

## Alternatives rejected

(Restated 2026-10-26, after the merge, on the Decision above - "the console ... writes the Mosaic on
a CFA frame", "`--rainbow` without `--histeq` is a parse error", "the coloured bitmaps enter the
per-view cache", "never coloured" for the composite: four points the owner confirmed when asked
again, none changing what is done. The console's coloured picture on a CFA frame stays the shrunk Mosaic although it uses the
middle of the table only - ranks 48..204 of 256 on the Tokina tablet-and-paper master, p1 51,
p99 202, blue to yellow - because the shape reads as well and the window shows the full palette
at 100 % and on the channels; a channel instead would be our choice of layer, not Siril's.
`--rainbow` without `--histeq` stays an error, unlike `--sky` without `--expo`, which changes
thresholds and not the file written. The coloured Mosaic stays in the window's cache (62 ms,
+73 MB, 958 MB with everything cached, against 1.1 GB for `--expo`). On a CFA frame of unknown
order the grey mean of the four positions stays uncoloured: it is no layer of Siril's, and the
Mosaic beside it is.)

- **Siril's literal RGB mix on the composite.** It is what Siril's RGB tab does, but on the
  NEF it produced colours outside the palette wherever the channels differ, and nothing can be
  read from them (the measurement of 2026-10-26).
- **Colouring the mean of the four positions.** A picture Siril does not have, and the Mosaic
  already is the whole frame as one layer, which is what Siril shows on an undebayered raw.
- **Colouring before shrinking** (the console). The block mean of colours is a colour outside
  the table; the colour of the mean grey is a level of it.
- **A colour bar under the picture.** Siril has none; a sentence from the core says which end
  is which, which is all the bar would add.
- **A silent `--rainbow` without `--histeq`**, like `--sky` without `--expo`. It modifies one
  mode's picture only; writing the plain picture would hide a typo.

## Consequences

- `R104_TheRainbowTableIsSirils` pins ten levels of the table by hand-computed values;
  `R104_AColouredRasterIsTheTableAtEveryPixel` pins the application;
  `R104_TheConsoleColoursTheShrunkMosaicOnCfaAndTheChannelOnMono` pins which raster the
  console writes and that it is shrunk first; the golden `histeq_rainbow` pins the report; the
  window tests pin the chip's availability and persistence.
- The quoted Siril source in `siril-histeq-source.md` gains the rainbow functions and stays
  true for tag 1.4.4 only.
- Cost in the window, measured on the Samyang 24 MP master (Release, 2026-10-26): colouring
  the Mosaic takes 62 ms and the peak from 795 MB (the grey Mosaic shown) to 868 MB; with the
  Mosaic and the four channels coloured and cached, 958 MB, cleared on a new file. Acceptable
  beside the 1.1 GB of `--expo`; the coloured Mosaic stays in the cache. Figures and the
  mechanics in `docs/design/modes-histeq.md`.
- The console's picture on a CFA frame, the shrunk Mosaic, uses the middle of the table only
  (blue to yellow on the Tokina master): a block averages the ranks of three colour
  populations. The shape reads as well; the window at 100 % and the channel views show the
  full palette. A new fact after the decision, put to the owner at wrap-up; kept (the
  Restated note above).
- Amplitude stays destroyed by construction (ADR 0025): the colours are ranks, not values, and
  the mode still never votes.
