# Parity with flatcheck.py, the numeric oracle

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `flatcheck.py`, `crosscheck.py`, `scripts/crosscheck.ps1`, `CrossCheckFixtures.cs`; the intentional divergences.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## Parity with Python

The C# port was written to be numerically identical to `flatcheck.py`. **Treat
`flatcheck.py` as the numeric oracle when changing a formula**, and re-run the
cross-check afterwards:

```bash
FLATCHECK_DUMP_FIXTURES=/tmp/xc dotnet test FlatCheck.sln --filter DumpsFixtures
```

```bash
python crosscheck.py /tmp/xc
```

`crosscheck.py` runs both implementations over identical FITS across 43 scenarios and
compares twice: the full text, and every numeric token as a float. It also checks once,
up front, that both report the same version. It exits non-zero on any unexplained numeric
difference or on a version desync.

**Current status: 42 of 43 scenarios agree numerically** - 26 byte-identical, 16
differing only in wording. Coverage spans all four modes, all four bands of the residual
scale, both gradient directions, non-square frames, BITPIX 16/32/-32, a float master
divided by 65535 as Siril writes it (`structure float 0..1`, which pins the levels line
that used to print `0 / 0`), a dust shadow (`structure dust shadow`, which pins the local
spot warning and its position), the exposure bands either side of each boundary, and a CFA
frame carrying `BAYERPAT` (`expo cfa BAYERPAT`, byte-identical), which is what pins the
channel-naming line across the two implementations, and the same frame with
`ROWORDER = 'BOTTOM-UP'` (`expo cfa BOTTOM-UP`, byte-identical), which pins that both
move the phase by one row on an even height.

The attribution rewrite is mirrored in `flatcheck.py` in full - `_structure_body` split
out of `mode_structure` exactly as `StructureReport.WriteBody` was, plus
`split_by_rotation`, `world_map_path` and `_provenance`. Four `--ratio` scenarios cover it,
and they compare 32 numbers each. If you change a formula in `RatioAnalyzer`, change it
there too, or every one of them turns into a diff.

Intentional divergences:

- `--histeq` exists only in C#. No Python counterpart, no `crosscheck.py` scenario, no
  `CrossCheckFixtures` entry: the mode produces a picture, not a number to compare.
- `--smooth` exists only in C#, for the same reason: it produces a file.
- `--synthetic`, `--lenses` and `--against` exist only in C#: a file, a list, and the
  already-verified structure battery on a ratio.
- No 4-panel matplotlib figure (residual PNG only).
- No EXIF read beyond the pixels, so no `suggest_shutter(...)` shutter-speed
  suggestions - libraw hands over `shutter`, but nothing consumes it yet.
- `Percentile` on an empty list returns 0 instead of raising.
- The scale-source line reads `libraw (exact)` where Python reads `rawpy (exact)`. Same
  library; `rawpy` is its Python wrapper. For the same reason, on a raw the channel-order
  line reads `din libraw` against `din rawpy`. On a FITS both read `BAYERPAT`, so that
  line is identical - which is what the `expo cfa BAYERPAT` scenario checks.
- No per-channel histograms in the console; they exist only in the window, and so does
  their wording (`ExposureReport.HistogramReading` and friends have no console caller).
- `--sat` is echoed as `--sat=4095` rather than Python's `--sat=4095.0`.
- The `--light` interpretation capitalises the diagnosis - "Fondul CADE/CRESTE spre
  margini" against Python's lower case. The sign *is* the diagnosis, so the emphasis
  is deliberate. This accounts for the remaining wording-only differences in the four
  `--light` scenarios whose radial component is large enough to print a diagnosis.

**One numeric divergence, and the C# is the correct one.** On a *perfectly constant*
frame `FitCenter`'s quadratic coefficients are pure floating-point noise (~1e-15) and
the vertex determinant comes out at ~1.3e-29. C# guards with `|det| < 1e-15`, marks the
vertex unusable and prints `nedeterminat`. Python has no such guard: `np.linalg.solve`
does not raise on a matrix that is only *numerically* singular, so it returns garbage and
places the illumination peak at (136, 136) on a 256x256 raster, reporting a 6.35% offset.

Since 1.2.0 the difference is confined to the informative line: the offset no longer
votes, so both implementations reach the same verdict and Python's false positive changes
nothing. It used to be the difference between OK and SUSPECT.

This cannot occur on a real flat, which always has vignetting and therefore a
determinant far from zero. Do not "fix" the C# to match; the guard is what makes the
degenerate case behave. Pinned by
`FitCenter_OnAConstantImage_FallsBackToTheGeometricCentre`.

