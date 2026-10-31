# 0026. --smooth is a robust local quadratic on tile medians, per Bayer position, with no radial model

Date: 2026-09-25
Status: superseded by 0080

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-smooth.md and
docs/design/smooth-validation.md; the decision itself dates from 2026-09-16. A flat shot
through fabric on a monitor carries the diffuser's texture into the lights as soft cells ~300
sensor px across at a few percent (tokina_pemonitor: fine part 1.19 % at sigma 64, deepest
pixels -27.0 % / +35.5 %). The mode writes a second master with everything smooth kept and
texture, dust, blotches and noise removed. Three designs were tried on five real masters (Siril
float, 6016 x 4016, RGGB, `STACKCNT` 30); two failed.

## Decision

Per Bayer position (mono: the whole frame): tile medians on ~150 sensor px with the partial
last row and column kept at their real centres (`Img.TileMedians`); a local quadratic at every
tile over a Gaussian neighbourhood of sigma = `ScalePx` (300 sensor px, the one knob); 3 rounds
of two-sided clipping at `max(2.5 MAD, 2 % of the level)`; per pixel, the bilinear blend of the
four surrounding tiles' quadratic predictions; `Mosaic.Merge`. Nothing in the method knows a
centre. It reports what was removed (`Prepare(input) / Prepare(smoothed)`: p1/p99,
`LargeScalePtp`, `MaxTileDeviation`, `LocalSpots`) and the structure battery before and after;
no verdict, a warning when the removed component's worst tile reaches `Th.SmoothKeptWarn`
(2 %). It refuses a raw, a colour cube and overwriting its input; writes the input's type back
(16 stays 16, else -32), the raw cards verbatim and two `HISTORY` lines; renormalises nothing;
exits 0 written / 1 otherwise.

## Alternatives rejected

- **A radial synthetic flat.** It would drop the texture and also everything the lens does
  besides vignetting; on the Tokina 20 the four corners differ by 6-16 %.
- **A global polynomial (degree 2-10) over a ring-median radial model's residual.** Mid-scale
  structure on every master (monitor blotches at 1000-1500 px, a decentred ring, LCD blobs):
  degree 3 cannot follow it (tile RMS 0.5-1.7 %), degree 6+ oscillates in the corners (kept
  non-radial ptp 22 %, worst tile 10 %).
- **A ring-median radial model times a local-linear field.** The radial model puts rings into
  the file: beyond the inscribed circle the median jumps ring to ring, and a 150-px field cannot
  absorb 22-px rings (±0.11-0.13 % ring amplitude in the removed component on tokina_pemonitor).
- **Clipping on the MAD alone.** 8 % of the Samyang master's tiles (tile RMS 0.06 %) were
  rejected and the smooth 0.5 % structure with them; with the 2 % floor the four clean masters
  reject nothing and tokina_pemonitor rejects 57-69 tiles per position (dust, bright spot, cells).
- **Fitting partial tiles one full tile from their neighbour.** A 0.8 % error band along the
  synthetic's right edge; with real centres the maximum error went from 1.05 % to 0.19 %.

## Consequences

- Acceptance figures at sigma_t = 2, to re-run on the five masters when the code changes: every
  verdict and vignetting depth unchanged, the five tests within half a point; removed
  0.12-0.39 % ptp on the four clean masters, 3.68 % on tokina_pemonitor (local spot +6.19 % ->
  +0.62 %); rings ±0.02 %; CFA levels within 0.1 %; 4-5 s per 24 MP master; synthetic floor 0.19 %.
- The cells' ~30 % rms residual at their own correlation length is fundamental to any finite kernel.
- The report and the tab must keep saying what the mode cannot know: lens versus source
  (smooth monitor blotches and sky gradients stay; only `--ratio` separates them) and that
  dust may be wanted for a same-session flat. Siril's scripts name `pp_flat_stacked` and will
  not pick the file up; the window's `pp_flat_stacked_netezit.fit` is never the session's master.
- No Python counterpart; the numpy prototype matched to the second decimal and was not kept.
  The small-scale synthetic lives in `SmoothAnalyzerTests`.
- Source notes: docs/design/modes-smooth.md, docs/design/smooth-validation.md.
