# Mode 5: histogram equalisation (--histeq), Siril's Histogram display

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `Analysis/HistEqAnalyzer.cs` (`HistEqLut`), `Report/HistEqReport.cs`, `Runs/HistEqRun.cs`; the Siril source it mirrors is quoted in `siril-histeq-source.md`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 5. Histogram equalisation - `flatcheck flat.NEF --histeq [-o poza.png]`

Not a test. The frame rendered the way Siril 1.4.4's "Histogram" display mode renders it,
so that patterns invisible in a linear view can be seen - and, on the window's tab, so
that a frame can be put beside the master it went into. No verdict, no threshold, no `Th`
entry, exit code 0 when the picture was written. **It never votes**, and the report and
the tab both say what it cannot show.

**What Siril does**, read in `src/gui/image_display.c` (`remap()`, the `HISTEQ_DISPLAY`
branch) and `src/gui/histogram.c` (`computeHisto()`) at tag `1.4.4`; the quoted code is
in `docs/siril-histeq-source.md`:

1. One histogram **per layer**, 65536 bins, pixels equal to zero skipped. The RGB view is
   composited afterwards from the per-layer 8-bit buffers, so on a debayered image each
   colour is equalised on its own CDF; on an undebayered file (a raw opened as CFA, the
   default) the mosaic is one layer.
2. `index[i] = round_to_BYTE(cumulative(i) / nb_pixels * 255)`, cumulative *including*
   bin `i`, `index[0] = 0`, `nb_pixels` = all pixels, zeros included - so a frame with
   many dead pixels never reaches 255. Float data is first quantised as `round(v * 65535)`.
3. The lo/hi cutoff sliders are disabled in that mode.

**What we do the same, and what differs.** `HistEqLut.Build` mirrors 1 and 2, including
the `(byte)(x + 0.5)` rounding (`TheLutIsTheInclusiveCdfTimes255_RoundedLikeSiril` pins a
hand-computed vector: `{1, 2, 2, 3}` -> `64, 191, 255`). Bins sit on the channel's own
`[min, max]` of non-zero values rather than on 0..65535, because our rasters are doubles at
whatever scale the file had; on integer data the mapping is provably identical (no two ADU
values share a bin, and the CDF at a value is the count at or below it either way), on
float it is finer. Equalisation runs at full resolution per channel, and the 8-bit result is
then shrunk with `Img.Prepare`'s `k` rule (`HistEqAnalyzer.ShrinkFactor`) - so a D3300
channel lands at 1500 x 1000, the residual map's size. Block-meaning *before* equalising
would give a stronger stretch (noise averaged out before the CDF) but is not what Siril
shows; deliberately not built.

**Channels.** The CFA decision is `Mosaic.LooksCfa` on the four position medians, the same
rule as `--expo`. On a CFA frame the four positions are equalised separately and the result
carries: `CompositeRgb` (R from the position that *is* red, G the mean of the two greens, B
from blue - looked up by `CfaPattern` colour, never by position, and the mean of the four
greys when the order is unknown), one grey per position, and `Mosaic`, the whole frame as
one layer shrunk by `2k` so it comes out the same size. On mono: one channel, `Mosaic` null.

**Two resolutions, on purpose.** Every raster exists twice: shrunk (`Equalised`, `Mosaic`,
`CompositeRgb`, the `Img.Prepare` rule) for the console's PNG and the tests, and at full
resolution (`Full`, `MosaicFull`, `CompositeFullRgb`) for the window, which shows the latter
directly as a bitmap so that 100 % is one sensor pixel and the `Mozaic` view shows the R/G/B
chequerboard exactly as Siril does. The shrunk ones are derived from the full ones
(`TheShrunkRastersAreTheBlockMeansOfTheFullOnes`), never recomputed. A first cut showed the
shrunk rasters in the window too, through PNG, and the user asked what zooming into
block means was for; nothing. The window's default view is `Mozaic` - the one that matches
Siril on an undebayered raw - while the console keeps writing the composite; the two do
not have to agree, since the window saves no file here.

**Why it shows what linear hides, and what it destroys.** A flat is a narrow distribution
relative to the file's scale, so the CDF is steep across the vignetting range. Measured on
a synthetic with 5 % vignetting: a 0.3 % ripple is a third of a grey level in the linear
view and ~15 levels equalised (`ATinyRippleInvisibleInTheLinearViewBecomesManyGreyLevels`).
The price is that amplitude is gone by construction - a 0.5 % and a 10 % pattern of the
same shape render identically, and a clean flat shows its own noise at full contrast. That
is `HistEqReport.Caveat()`, printed by the console and shown under the picture. Note the
comparison the test makes is against the linear view *on the file's scale*, not against a
min..max stretch: on a field whose distribution is nearly uniform across its range,
equalisation *is* that stretch; what the mode adds over it is adaptivity, and that is a
property of real, noisy flats rather than of the synthetic.

**The master beside a frame.** The window's list puts the session's `pp_flat_stacked.fit` first.
A shape on a frame and not on the master was transient and averaged out; a shape on both
is fixed to the setup and is what the Structure numbers measure. Because the transform is
rank-based, the master's lower noise makes a shared pattern look *sharper* there, not
fainter (`HistEqReport.MasterVersusFrame()`).

**Cost, measured** (Release, D3300 24 MP, same files as the modes beside it): the float
master takes ~2-3 s and 690 MB peak against 3.3 s / 611 MB for structure; the NEF ~2-3 s
and 516 MB against 3.2 s / 1.1 GB for `--expo`. Keeping the full-resolution rasters for
the window costs ~18 MB of that. Loading dominates. Two things were expensive in
the first cut and are documented where they were fixed: the four position medians for the
CFA decision were sorted copies of 6 M values (~2 s) and now come off the 65536-bin
histogram (`HistEqLut.Median`, within a bin of exact), and the shrink went through `double`
rasters (1.1 GB peak on the master) and is now a byte block mean with the same arithmetic.
The console's PNG on that master: 150-380 ms and 3.6 MB for the shrunk RGB composite -
equalised noise compresses badly, so the file is several times the residual map's. The
window encodes nothing (see `RasterBitmaps`).

No Python counterpart and no `crosscheck.py` scenario, by decision: there is no number to
compare.

