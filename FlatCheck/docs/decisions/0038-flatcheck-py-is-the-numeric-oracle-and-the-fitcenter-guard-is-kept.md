# 0038. flatcheck.py is the numeric oracle; the one divergence, the FitCenter singularity guard, is kept

Date: 2026-09-25
Status: superseded by 0047

## Context

Retroactive: recorded on 2026-09-25 from docs/design/python-parity.md; the decision itself dates
from the original port. The C# tool was ported from `flatcheck.py` (Python/numpy/astropy) to be
numerically identical. `crosscheck.py` runs both implementations over identical FITS across 43
scenarios and compares twice - the full text, and every numeric token as a float - after checking
once that both report the same version. Current status: 42 of 43 scenarios agree numerically,
26 byte-identical and 16 differing only in wording. The one numeric divergence: on a perfectly
constant frame `FitCenter`'s quadratic coefficients are floating-point noise (~1e-15) and the
vertex determinant is ~1.3e-29; Python's `np.linalg.solve` does not raise on a matrix that is
only numerically singular, returns garbage, and places the illumination peak at (136, 136) on a
256x256 raster, reporting a 6.35 % offset.

## Decision

`flatcheck.py` is the numeric oracle: a formula change is checked against it by dumping the
fixtures (`FLATCHECK_DUMP_FIXTURES=... dotnet test --filter DumpsFixtures`) and running
`python crosscheck.py`. Where parity is tested, a formula change is mirrored in Python: the
attribution rewrite is mirrored in full (`_structure_body`, `split_by_rotation`,
`world_map_path`, `_provenance`; four `--ratio` scenarios of 32 numbers each), and so are
`radial_model` / `_ring_fit`. The C# guard `|det| < 1e-15` in `FitCenter`, which marks the vertex
unusable and prints `nedeterminat`, is kept: do not "fix" the C# to match Python.

## Alternatives rejected

- **Matching Python on the singular case.** The guard is what makes the degenerate case behave;
  a real flat always has vignetting and a determinant far from zero, so the case cannot occur
  there. Since 1.2.0 the offset no longer votes, so both implementations reach the same verdict
  anyway (it used to be the difference between OK and SUSPECT).
- **Maintaining the Python in parallel for every feature.** It is prior art for features and the
  reference for numbers, not maintained beyond what parity tests: `--histeq`, `--smooth`,
  `--synthetic`, `--lenses`, `--against` exist only in C# (a picture, a file, a list, an
  already-verified battery on a ratio).

## Consequences

- Intentional divergences that stay: no 4-panel matplotlib figure; no `suggest_shutter(...)`;
  `Percentile` on an empty list returns 0 instead of raising; `libraw (exact)` / `din libraw`
  against `rawpy (exact)` / `din rawpy` (same library); no per-channel histograms in the console;
  `--sat=4095` against `--sat=4095.0`; the `--light` diagnosis capitalised ("Fondul CADE/CRESTE").
- Coverage of the 43 scenarios must be kept: all four modes, all four residual bands, both
  gradient directions, non-square frames, BITPIX 16/32/-32, `structure float 0..1` (the levels
  line that used to print `0 / 0`), `structure dust shadow` (the local spot warning and position),
  the exposure bands either side of each boundary, `expo cfa BAYERPAT` and `expo cfa BOTTOM-UP`
  (both byte-identical).
- `LargeScalePtp` keeps `Img.BlockMean`'s top-left anchoring because that is what `flatcheck.py`
  does; the half-pixel centre convention (known issue 5) is likewise not changed.
- Guards: `FitCenter_OnAConstantImage_FallsBackToTheGeometricCentre`, `crosscheck.py` (exits
  non-zero on any unexplained numeric difference or version desync), `CrossCheckFixtures`.
- Source note: docs/design/python-parity.md.
