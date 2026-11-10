# Make the geometry symmetric under a row flip (R-014 amended, R-131, ADR 0095)

## Context

ADR 0094 (2026-11-09) made FlatCheck read a FITS the way Siril shows it and revealed that the
structure figures depend on the orientation a frame is read in: measured on the 15 validation
masters read both ways up, today's figures move by up to 0.20 (azimuthal), 0.16 (residual),
0.29 (tilt), 0.34 (worst tile) and 0.58 points (local spot: another spot of the same size wins).
Three anchors on pixel indices cause it: the radial model's centre at the index `(w/2, h/2)`,
half a pixel below the geometric centre; `Metrics.FitCenter` sampling every `step`-th row and
column from index 0; and the tile grids of `LargeScalePtp`, `MaxTileDeviation`, `LocalSpots` and
`TileBackground` starting at row 0 with the leftover rows at the far edge. With the geometric
centre, full sampling and a centred integer tile grid (measured with temporary switches in this
worktree, `GeometryExperimentTests`, scratchpad `geometry-experiment.csv`), corners, azimuthal
asymmetry, tilt and local spot come out identical both ways up; residual and worst tile keep a
one-row asymmetry when the leftover is odd (1004 = 27 x 37 + 5), 0.11 and 0.44 points on V-009
alone. The owner chose variant B on 2026-11-09: exact symmetry, with half-weight boundary rows
when the leftover is odd, the parity goldens recaptured and the moved validation cases
re-recorded. Cost measured against today: azimuthal <= 0.10, residual <= 0.13, tilt <= 0.23, worst
tile <= 0.26 points; V-012's tilt 1.93 -> 2.00 % turns its verdict PERFECT -> BUN; the 14 structure
and 7 light parity scenarios lose the prototype's confirmation for their figures (their fixtures
are centred on the index `h/2` like today's model).

Branch `claude/Make-the-geometry-symmetric-under-a-flip`, worktree of the same name. The
experiment's switches (`Metrics.Exp*`, `Metrics.Tiles`, `Img.BlockMean(a, k, oy, ox)`,
`FlatCheck.Tests/GeometryExperimentTests.cs`) are replaced by the design below, not kept.

## Design

- **Centre.** `Metrics.Centre(int n) => (n - 1) / 2.0`: the geometric centre of a raster whose
  pixel `i` spans `[i, i + 1)` has its row centres at `(n - 1) / 2`. Used by
  `StructureAnalyzer.Run`, `LightAnalyzer.Analyze` and `FitCenter` (its normalisation
  `(x - Centre(w)) / (w / 2.0)` and its three fallbacks). `R-014`'s "sensor centre `(w/2, h/2)`"
  becomes "the geometric centre `((w-1)/2, (h-1)/2)`".
- **FitCenter samples every pixel** (`step = 1`): symmetric by construction; the cost on the
  1504 x 1004 raster was not measurable in the experiment (whole run 2.4 s vs 3.1 s).
- **Tiles symmetric about the frame centre.** `Img.BlockMeanCentred(Img a, int k)`: `n = H / k`
  tiles per axis, leftover `r = H - n k`, offset `o = r / 2.0`; tile `j` spans edge coordinates
  `[o + j k, o + (j + 1) k)`; a pixel row's weight is its overlap with the tile (0, 1/2 or 1),
  columns likewise, weights multiplied, mean = sum(w v) / sum(w) with sum(w) = k^2. For an even
  leftover it is today's block mean shifted by `r / 2`; for an odd one the first and last rows of
  each edge tile count half. Used by `LargeScalePtp`, `MaxTileDeviation` and `LocalSpots`, whose
  reported position becomes `(int)((wx + 0.5) k + ox)`, `oy` likewise.
- **`TileBackground`** (the `--light` 15th-percentile tiles, where weights have no meaning):
  the same centred grid; when the leftover is odd each edge tile takes the boundary row whole,
  so neighbours share one row (`floor(o)` to `ceil(o + k) - 1`) and the grid is still its own
  mirror. `bx`, `by` are the tile centres `o + (j + 0.5) k`.
- **Unchanged on purpose:** `Img.Prepare`'s 2x2 block mean stays anchored at (0, 0): it must
  follow the Bayer phase, so an odd-sized frame keeps one dropped row at the far edge. Every
  sensor the tool has met is even-sized; R-131 states the guarantee for even sizes.
  `Img.Symmetrise`, `CornerStats`, `RadialModel` and `AzimuthalAsymmetry` are already symmetric
  once the centre is.

## Steps

1. **Requirements.** `docs/requirements.md`: amend `R-014` (the centre); new `R-131` in the
   structure block: a raster and its row mirror (and its column mirror) give the same corner
   spread, azimuthal asymmetry, residual, tilt, worst tile and local-spot magnitude to the last
   bit, and the same `--light` radial and plane figures, for every even-sized frame; how (the
   geometric centre, every pixel in the tilt fit, tile grids symmetric about the frame centre
   with half-weight boundary rows, or a shared boundary row for the percentile tiles, when the
   leftover is odd); the `Prepare` exception.
2. **Tests first** (failing, pasted in the session): `FlatCheck.Tests/MetricsTests.cs`
   `R131_BlockMeanCentred_*` (a 7 x 5 raster with k = 2: leftovers 1 and 1, the edge weights 1/2,
   the sums; an even leftover equals today's block mean shifted); `StructureAnalyzerTests.cs`
   `R131_TheFiguresAreTheSame_WhenTheRasterIsMirrored` on an asymmetric deterministic scene
   (gradient + saddle + a dust shadow + hashed noise) at 1536 x 1024 and at a size whose
   analysis raster leaves odd tile leftovers both ways (e.g. 1480 x 1012 -> 740 x 506, k = 18:
   740 = 41 x 18 + 2, 506 = 28 x 18 + 2; pick one that gives odd leftovers, computed in the
   test), rows and columns mirrored, asserting equality to 1e-9 on the six figures and that the
   spot's position mirrors; `LightAnalyzerTests` the same for `radPct` and `planePct`;
   `FitCenter` on a mirrored raster gives the mirrored vertex and the same tilt.
3. **Code** in `FlatCheck.Core`: `Img.BlockMeanCentred` (documented), `Metrics.Centre`, the
   three tile metrics and `TileBackground` on the centred grid, `FitCenter` on every pixel and
   the geometric centre, `StructureAnalyzer` / `LightAnalyzer` on `Metrics.Centre`. Remove the
   experiment: `Metrics.Exp*`, `Metrics.Tiles`, `Img.BlockMean(a, k, oy, ox)`,
   `GeometryExperimentTests.cs`. Documentation comments on everything added or edited.
4. **Goldens.** Regenerate `GoldenOutputTests|EnglishOutputTests`; read every diff: the figures
   of the structure and light scenarios move (the fixtures are centred on the index `h/2`, half
   a pixel from the geometric centre), the `--expo` and histeq goldens must not change. Then,
   with the owner's yes of 2026-11-09, `ParityGoldenTests`; every changed line tied to the
   centre, the sampling or the tiles, anything else a finding. Record the recapture in
   `docs/design/python-parity.md` (a C#-only call; the prototype's geometry was the index one).
5. **Validate.** `/validate` on the real masters; the drift table pasted; the moved cases
   re-recorded in `docs/validation.json` with a dated History line in `docs/validation.md`
   (old figures in the rows' last column; V-012's verdict change named), as the owner agreed
   when choosing B.
6. **Docs.** ADR `0095-the-geometry-is-symmetric-about-the-frame-centre.md`: context (the
   measurement table), decision, alternatives (A: integer centred grid with the extra row at the
   bottom; C: centre and sampling only; D: keep the dependence), consequences (figures, the
   verdict of V-012, the parity confirmation, `Prepare`); it supersedes the "geometry anchored
   on pixel indices" statements of ADR 0094 in part, and the centre of the ADR that fixed
   `(w/2, h/2)` (find it: `grep -l "w/2" docs/decisions`). Design notes: `pipeline.md` (the
   centre, the tiles, the row-order paragraph's last sentence), `modes-structure.md`,
   `modes-light.md`, `code-map.md` (`Img.cs`, `Metrics.cs`), `radial-model-even-term.md` if it
   names the centre. `README.md`: the sentence "computed around the centre of the sensor" gains
   "the geometric centre"; the --light section if it names tiles. `CHANGELOG.md` `[Unreleased]`
   `Changed`. The plan saved as `docs/plans/2026-11-09-make-the-geometry-symmetric-under-a-flip.md`.
7. **Gate and commits** on the task branch (requirement + tests + code; goldens + validation;
   ADR + docs), a subagent review, its fixes a commit apiece, the merge proposed to the owner.
   The version is the owner's.

## Verification

- `dotnet test FlatCheck.sln -c Release --filter "FullyQualifiedName~R131|FullyQualifiedName~R014"`.
- `GeometryExperimentTests`-style check before deleting it: with the final code, every figure
  equal both ways up on the 15 masters (max |normal - flipped| = 0 to 1e-9 for all six), pasted.
- The golden diffs read whole; `/validate` drift table; the console on the Veil 2026-11-08 master
  (figures within 0.3 of today's, verdict GOOD).
