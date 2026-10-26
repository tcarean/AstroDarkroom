# Mode 5: histogram equalisation (--histeq), Siril's Histogram display

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): what Siril does, how `HistEqLut` mirrors it,
> the two resolutions and the measured cost are ADR 0025; the false colours are ADR 0063; this
> note keeps the mechanics and the reading of the picture. **Covers:**
> `Analysis/HistEqAnalyzer.cs` (`HistEqLut` and `RainbowLut` by pointer, the shrink and the
> channels in full), `Report/HistEqReport.cs`, `Runs/HistEqRun.cs`; the Siril source it mirrors
> is quoted in `siril-histeq-source.md`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 5. Histogram equalisation - `flatcheck flat.NEF --histeq [-o poza.png]`

ADR 0025 holds the whole of the decision: not a test, Siril 1.4.4's "Histogram" display
mirrored per layer (the inclusive CDF times 255, zeros skipped but counted, float quantised),
the bins on the channel's own `[min, max]`, the CFA decision through `Mosaic.LooksCfa`, the
composite by `CfaPattern` colour, every raster at full and at shrunk resolution, the console's
composite against the window's `Mozaic` default, no verdict and no `Th` entry, the rejected
block-meaning before equalising, the two costs fixed after the first cut, the measured cost, and
why there is no parity scenario. The Siril code is quoted in `siril-histeq-source.md`; the lo/hi
cutoff sliders are disabled in that mode. Mechanics and readings the ADR does not spell out:

**Why the bins can sit on `[min, max]`.** Our rasters are doubles at whatever scale the file
had; on integer data the mapping is provably identical to Siril's 0..65535 bins (no two ADU
values share a bin, and the CDF at a value is the count at or below it either way), on float it
is finer. Equalisation runs at full resolution per channel, and the 8-bit result is then shrunk
with `Img.Prepare`'s `k` rule (`HistEqAnalyzer.ShrinkFactor`), so a D3300 channel lands at
1500 x 1000, the residual map's size. `Mosaic`, the whole frame as one layer, is shrunk by `2k`
so it comes out the same size. On mono: one channel, `Mosaic` null.

**The window's default and the console's file do not have to agree**, since the window saves no
file here.

**What the test compares.** `ATinyRippleInvisibleInTheLinearViewBecomesManyGreyLevels` compares
against the linear view *on the file's scale*, not against a min..max stretch: on a field whose
distribution is nearly uniform across its range, equalisation *is* that stretch; what the mode
adds over it is adaptivity, and that is a property of real, noisy flats rather than of the
synthetic.

**The master beside a frame.** The window's list puts the session's `pp_flat_stacked.fit` first.
A shape on a frame and not on the master was transient and averaged out; a shape on both
is fixed to the setup and is what the Structure numbers measure. Because the transform is
rank-based, the master's lower noise makes a shared pattern look *sharper* there, not
fainter (`HistEqReport.MasterVersusFrame()`).

**Cost beside the other modes** (Release, D3300 24 MP, the same files): structure takes 3.3 s /
611 MB and `--expo` 3.2 s / 1.1 GB against the 2-3 s ADR 0025 records for this mode; loading
dominates. The console's PNG is several times the residual map's size because equalised noise
compresses badly.

### The false colours (`--rainbow`, the "False colours" chip; ADR 0063)

`RainbowLut` is Siril's 256-entry table, built once (`Table`, 768 bytes) and applied by
`Colour(grey)` to a one-layer 8-bit raster, three bytes per pixel in the layout
`PngWriter.Encode` and `RasterBitmaps.Rgb` take. Nothing coloured lives in `HistEqResult`:
the console colours the shrunk raster in `HistEqRun.EncodeRainbow` (the `Mosaic` on CFA, the
channel on mono), the window the full one in `HistEqTabViewModel.RefreshImage`, cached per
(view, coloured) beside the grey bitmaps. The chip's state is `HistEqViewViewModel.Rainbow`;
`RainbowAvailable` is false on the RGB composite, where the chip is disabled and the bitmap
stays the plain composite while the legend still says why. The legend and the display line
come from `HistEqReport` (`RainbowLegend`, `DisplayLine(r, rainbow)`), never from the
view-model.

**What the picture shows.** Bands of one hue are contours of equal rank, so the shape of the
vignetting reads at a glance: on the Tokina tablet-and-paper master a rounded square, on the
Samyang sky master round rings slightly off-centre (measured 2026-10-26, the pictures in ADR
0063's context). The colours are ranks: the amplitude is as gone as in grey.

**The shrunk mosaic uses the middle of the table only.** The console's picture on a CFA frame
is the mosaic shrunk by `2k`: each block averages the ranks of three colour populations, and
since the red population sits low in the single-layer histogram and the greens high, no block
reaches 0 or 255. On the Tokina master the file runs from blue to yellow (157 of 256 colours
used), not from magenta to red; the shape is as readable, the ends of the palette are not
there. At 100 % in the window the mosaic shows the chequerboard in the full palette; a
channel view is full-range in both.

**Cost, measured on the Samyang 24 MP master** (Release, the view-model path, 2026-10-26):
load and equalise 1.8 s with a 764 MB peak; the grey Mosaic bitmap takes the peak to 795 MB;
colouring the Mosaic 62 ms and 868 MB (the 72 MB raster plus the bitmap); a channel 19 ms;
with the Mosaic and all four channels coloured and cached, 958 MB. The cache is cleared on a
new file, so that is the ceiling per file.
