# Mode 6: the smoothed flat (--smooth) and the scale advisor

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `Analysis/SmoothAnalyzer.cs`, `Analysis/SmoothScaleAdvisor.cs`, `Report/SmoothReport.cs`, `Runs/SmoothRun.cs`, `FitsWriter.cs`, `Img.TileMedians`, `Mosaic.Merge`; the measurements are in `smooth-validation.md`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 6. Smoothed flat - `flatcheck flat.fit --smooth netezit.fit [--scale 300]`

Not a test either: it **writes a second master**, the input with everything smooth kept
and everything else removed - the diffuser's texture, dust shadows, compact blotches,
noise. It exists for a flat shot through fabric on a monitor, whose texture reaches the
lights as a blanket of cells at a few percent. `docs/smooth-validation.md` is the full
record; the short version:

- **Why not a radial synthetic flat.** It would drop the texture and also everything the
  lens does besides vignetting, and on the Tokina 20 the four corners differ by 6-16 %.
- **What it does**, per Bayer position (mono: the whole frame): tile medians on ~150
  sensor px with partial tiles kept (`Img.TileMedians`); a local **quadratic** fit at every
  tile over its Gaussian neighbourhood, sigma = `ScalePx` (**300 sensor px**, the one
  knob); 3 rounds of two-sided clipping at `max(2.5 MAD, 2 % of the level)`; per pixel,
  the bilinear blend of the four surrounding tiles' quadratic predictions; `Mosaic.Merge`.
- **No radial model, deliberately.** Two designs with one failed on the real masters and
  the analyzer's header says how: a global polynomial over the residual cannot follow the
  mid-scale structure every master has (monitor blotches at 1000-1500 px, a decentred
  ring), and a ring-median model **puts rings into the file** - beyond the inscribed
  circle the median jumps ring to ring (the artefact `RadialModel` documents), and a
  150-px field cannot absorb 22-px rings. Nothing in the method knows a centre, so rings
  are impossible by construction. Don't reintroduce one.
- **The clip floor is load-bearing.** Relative to the MAD alone, 8 % of a very clean
  master's tiles (Samyang, tile RMS 0.06 %) were rejected and the smooth 0.5 % structure
  with them. `ClipFloor = 0.02` of the level fixes it; the four clean masters now reject
  nothing, the textured one rejects its dust, its bright spot and its deepest cells.
- **What it reports** is *what was removed* - `Prepare(input) / Prepare(smoothed)`: p1/p99
  per pixel, `LargeScalePtp`, `MaxTileDeviation`, `LocalSpots` on it - plus the structure
  battery before and after (`AnalyzePrepared` on both prepared rasters). No verdict; a
  warning when the removed component's worst tile reaches `Th.SmoothKeptWarn` (2 %).
- **Measured on five real masters** (the numbers in `docs/smooth-validation.md` and in the analyzer):
  every verdict and vignetting depth unchanged, the five tests within half a point;
  removed 0.12-0.39 % ptp on the four clean masters, 3.7 % on the textured one, whose
  compact spot goes from +6.19 % to +0.62 %; rings ±0.02 %; CFA levels within 0.1 %; 4-5 s
  per 24 MP master. On a synthetic with known truth the floor is 0.19 % over the whole
  frame, corners included. What the cells leave behind (~30 % in rms at their own
  correlation length) is fundamental to any finite kernel; the scale trades it against
  real 300-px structure (the Nikkor's ring is partly taken at 300, kept at 150).
- **What it cannot know:** lens versus source. A monitor's blotches and a sky gradient
  stay, because they are smooth; only `--ratio` separates them. Dust may be wanted (a
  same-session flat). The report and the tab say both.
- **Refuses** a raw (a single uncalibrated frame; `--expo` is for raws, and `Cards` is
  empty there) and a colour cube (`Planes > 1`: the reader averages the planes and the
  file written back would be mono). Exit 0 when the file was written, 1 otherwise - the
  `--histeq` rule; it does not go through `ExitFor`. Refuses to overwrite its input.
- **Output:** the input's type and scale, the input's cards verbatim (`BAYERPAT` included,
  so Siril treats it as the CFA master it is), two `HISTORY` lines. The console writes the
  two maps at `-o` and `-dupa` (`FlatCheckCli.AfterMapPath`): the removed component, and
  the smoothed flat's own residual. Siril's scripts name `pp_flat_stacked` explicitly and
  will not pick the file up by themselves; the report says so.
- **The scale can be proposed** (`--scale auto`, the tab's `Propune scara`), by
  `SmoothScaleAdvisor`. `Metrics.FineTexture` on the prepared input - the structured part
  of the component under ~128 sensor px (a double box blur of `W/47`, so the same fraction
  of the frame on a fixture as on the sensor; a fixed 32 px leaked vignetting curvature
  on a 256-px fixture), pixel noise subtracted in quadrature - says whether there is
  texture at all: `Th.SmoothTextureMin` 0.20 % against 0.04-0.06 % on the four clean
  masters and 0.69 % on the textured one. If there is, one smoothing per rung of the
  three-chip ladder; a rung is acceptable when the output's texture is under
  `Th.SmoothTextureGone` (0.05 %: 0.09 at 150, 0.02 at 300 on the textured master) and
  neither the **azimuthal asymmetry** nor the **tilt** dropped by more than
  `Th.SmoothScaleLoss` (0.5 points) - those two are the tests the texture does not touch,
  whereas corners, residual and worst tile *drop when the texture leaves* (1.47 points at
  the worst tile) and cannot be guards; the removed component's own large-scale figure
  was tried and missed the Nikkor's ring at 600 px (0.24 %) where the azimuthal drop was
  unambiguous (-0.79). The smallest acceptable rung is proposed; none means
  `NEDETERMINAT`, no texture means `NIMIC DE NETEZIT`, and in both cases the console
  writes no file and exits 2. On the five masters: 300 on the textured one, "nothing"
  on the four others. It proposes, never decides: lens versus source is still invisible,
  and a real feature only in the corners escapes both guards - the report says so. The
  window's button waits for the opening chain like the other one, selects the chip, and
  writes nothing.

No parity golden scenario: the product is a file. The method
was chosen and validated with a numpy prototype that reproduced these figures; the C#
port matches it to the second decimal on the five masters, and the prototype was not
kept.

