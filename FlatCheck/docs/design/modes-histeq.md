# Mode 5: histogram equalisation (--histeq), Siril's Histogram display

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): what Siril does, how `HistEqLut` mirrors it,
> the two resolutions and the measured cost are ADR 0025; this note keeps the mechanics and the
> reading of the picture. **Covers:** `Analysis/HistEqAnalyzer.cs` (`HistEqLut` by pointer, the
> shrink and the channels in full), `Report/HistEqReport.cs`, `Runs/HistEqRun.cs`; the Siril
> source it mirrors is quoted in `siril-histeq-source.md`.
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
