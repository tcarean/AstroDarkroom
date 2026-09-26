# The frozen Python prototype and the parity goldens

> Moved from CLAUDE.md on 2026-09-25 (ADR 0001) and rewritten on 2026-09-26, when the cross-check
> was retired (ADR 0047). **Covers:** `flatcheck.py` and `crosscheck.py` (frozen at 2.2.0),
> `FlatCheck.Tests/ParityFixtures.cs`, `ParityGoldenTests.cs`, `CrossCheckFixtures.cs`,
> `Golden/parity_*.txt`; the intentional divergences of the frozen file. A change to the code
> this describes means this note is updated in the same task, or deleted with the decision it
> explains.

## What the cross-check was

The C# port was written to be numerically identical to `flatcheck.py`, the Python/numpy/astropy
prototype. Until 2026-09-26 the prototype was the numeric oracle: `crosscheck.py` ran both
implementations over the same synthetic FITS in 43 scenarios and compared twice, the full text
and every numeric token as a float (tolerance 1e-9), after checking once that both reported the
same version (ADR 0038, superseded). The check lived outside the gate, because it needed a second
toolchain (ADR 0005, superseded), and rule 16 plus the skills demanded it after a formula change.

Its last run, on `main` at 2.2.0 on 2026-09-26, before anything changed:

```
scenarii              : 43
identice la byte      : 26
doar text (numere ok) : 16
divergente cunoscute  : 1
DIFERENTE NUMERICE    : 0 []
```

## What replaced it

The owner stopped maintaining the prototype (ADR 0047). The two files stay in the repository,
frozen at 2.2.0 (`__version__ = "2.2.0"` is the mark of where they froze), unmaintained and not
run by any script, hook or skill. The 43 scenarios became golden files:

- `ParityFixtures.WriteAll` writes the same synthetic FITS the cross-check used, with the same
  names (the former body of `CrossCheckFixtures`).
- `ParityGoldenTests` holds the scenario table, name and arguments exactly as `SCENARIOS` in
  `crosscheck.py` had them, runs the console in-process over each one through
  `GoldenOutputTests.RunCli` (same scrubbing of paths and banner) and compares with
  `Golden/parity_<slug>.txt`, where the slug is the scenario name lower-cased with every run of
  non-alphanumerics turned into `_` (`expo 25% --sky` is `parity_expo_25_sky.txt`). The exit
  code is the last line of each file, `[exit N]`.
- The 43 files were captured on 2026-09-26 from the console, the day the prototype agreed on
  every number, so they carry the independent implementation's last word. A re-approval there
  (`FLATCHECK_UPDATE_GOLDEN=1`) is a C#-only decision from then on, and the diff is read knowing
  that; R-098 says so.
- `CrossCheckFixtures.DumpsFixturesWhenAsked` still dumps the fixtures to the folder named by
  `FLATCHECK_DUMP_FIXTURES`, so `python crosscheck.py <folder>` can be run by hand against the
  frozen prototype for as long as its output means anything. Nothing requires it.

After a formula change, rule 16 asks for `/validate` on the real masters instead: 40 cases in
`docs/validation.json`, several of them with an earlier numpy figure beside them
(`docs/validation.md`, "earlier figure"). The parity goldens catch a synthetic drift in the gate;
`/validate` catches a real one.

## Coverage of the 43 scenarios

Kept whole on purpose (ADR 0038's consequence, carried over): 15 structure, 4 `--ratio`, 11
`--light` (three of them series folders), 13 `--expo`. Across them: all four residual bands
(`grad_bun`, `grad`, `grad_refa`, plus the clean field), both gradient directions and a
diagonal, an inverted (over-corrected) field, a pure sky plane, a constant frame, mild and heavy
vignetting, an offset pedestal, nebulosity in one quadrant, a dust shadow (the local-spot line
and its position), 768x512 and 512x768 frames, BITPIX 16, 32 and -32, a float master divided by
65535 as Siril writes it (the levels line that used to print `0 / 0`), a CFA frame with
`BAYERPAT` and the same with `ROWORDER = 'BOTTOM-UP'` (the phase moves by one row on an even
height), the exposure bands either side of each boundary with and without `--sky`, a 12-bit
frame with `--sat` and `--black`, the `--light` field-of-view refusal and its `--fov` override,
and a series with `--pattern`. About 11 of the 43 share their bytes with a golden in
`GoldenOutputTests`; on 2026-09-26 those printed the same numbers, which was the sanity check
of the capture.

## Intentional divergences of the frozen file

These describe `flatcheck.py` as it froze. They are not to be "fixed" on either side; the C# is
the product, the prototype is history.

- `--histeq`, `--smooth`, `--synthetic`, `--lenses` and `--against` exist only in C#: a picture,
  a file, a file, a list, and the structure battery on a ratio. They never had a scenario.
- Structure with `--raw` / `--lens` exists only in C#; without the two flags the output is
  identical, which the 15 structure scenarios pin.
- No 4-panel matplotlib figure (residual PNG only).
- No EXIF read beyond the pixels, so no `suggest_shutter(...)` suggestions.
- `Percentile` on an empty list returns 0 instead of raising.
- The scale-source line reads `libraw (exact)` where Python read `rawpy (exact)`, and on a raw
  the channel-order line `din libraw` against `din rawpy`. Same library; `rawpy` is its Python
  wrapper. On a FITS both read `BAYERPAT`, so that line was identical (`expo cfa BAYERPAT`).
- No per-channel histograms in the console; they exist only in the window.
- `--sat` is echoed as `--sat=4095` rather than Python's `--sat=4095.0`.
- The `--light` diagnosis is capitalised ("Fondul CADE/CRESTE spre margini") against Python's
  lower case. The sign *is* the diagnosis, so the emphasis is deliberate. This accounted for
  the wording-only differences in the `--light` scenarios that print a diagnosis.

**The one numeric divergence, and the C# is the correct one.** On a *perfectly constant* frame
`FitCenter`'s quadratic coefficients are pure floating-point noise (~1e-15) and the vertex
determinant comes out at ~1.3e-29. C# guards with `|det| < 1e-15`, marks the vertex unusable
and prints `nedeterminat`. Python had no such guard: `np.linalg.solve` does not raise on a
matrix that is only *numerically* singular, so it returned garbage and placed the illumination
peak at (136, 136) on a 256x256 raster, reporting a 6.35 % offset. Since 1.2.0 the offset no
longer votes, so both reached the same verdict; it used to be the difference between OK and
SUSPECT. This cannot occur on a real flat, which always has vignetting and therefore a
determinant far from zero. Do not "fix" the C#; the guard is what makes the degenerate case
behave. Pinned by `FitCenter_OnAConstantImage_FallsBackToTheGeometricCentre` and by
`parity_structure_uniform.txt`.

Two conventions were kept for parity and stay because nothing asks for a change:
`LargeScalePtp` keeps `Img.BlockMean`'s top-left anchoring, and the half-pixel centre
convention of known issue 5 is unchanged.
