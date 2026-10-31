# Mode 6: the smoothed flat (--smooth) and the scale advisor

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): ADR 0026 holds the method, its rejected
> designs and its acceptance figures, ADR 0027 the scale advisor, `smooth-validation.md` the
> measurements. **Covers:** by pointer, `Analysis/SmoothAnalyzer.cs`,
> `Analysis/SmoothScaleAdvisor.cs`, `Report/SmoothReport.cs`, `Img.TileMedians`, `Mosaic.Merge`;
> in full, the refusal and header mechanics of `Runs/SmoothRun.cs` and `FitsWriter.cs`, and the
> console's second map.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 6. Smoothed flat - `flatcheck flat.fit --smooth netezit.fit [--scale 300]`

Not a test: it **writes a second master**, the input with everything smooth kept and everything
else removed. ADR 0026 holds the method (tile medians, a robust local quadratic per Bayer
position, the bilinear blend, `Mosaic.Merge`), why not a radial synthetic flat, the two designs
that failed on the real masters and why no radial model may be reintroduced, the clip floor
(`SmoothAnalyzer.ClipFloor`, 2 % of the level, load-bearing), what the report prints and its one
warning (`Th.SmoothKeptWarn`), the refusals, the acceptance figures on the five masters, and the
two limits the report and the tab keep stating (lens versus source; dust may be wanted).
`smooth-validation.md` is the full record of the measurements. What the ADR does not spell out:

- **The scale trade.** What the cells leave behind is fundamental to any finite kernel (ADR
  0026); the scale trades it against real 300-px structure: the Nikkor's decentred ring is
  partly taken at 300 and kept at 150.
- **How the refusals are recognised** (`SmoothRun`): a raw is a frame with no `Cards` (a single
  uncalibrated frame; `--expo` is for raws), a colour cube is `Planes > 1` (the reader averages
  the planes, and the file written back would be mono). The exit code follows the `--histeq`
  rule, 0 written and 1 refused, and does not go through `ExitFor`.
- **The header written back** keeps the input's cards verbatim so that `BAYERPAT` survives and
  Siril treats the file as the CFA master it is; `FitsWriter` appends the two `HISTORY` lines.
- **The console's two maps:** `-o` is the removed component and the `-after` suffix
  (`FlatCheckCli.AfterMapPath`, R-006) the smoothed flat's own residual.
- **The scale can be proposed** (`--scale auto`, the tab's `Propune scara`): ADR 0027 holds the
  rule, the two guards, the three thresholds, what was tried as a guard and failed, the picks on
  the five masters and the known limits. The window's button waits for the gate like the other
  one (ADR 0036, ADR 0061, R-086), selects the chip and writes nothing.
