# 0035. Each lift of the simulation is linearised through one table, not a lifted raster

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

The print-background simulation (ADR 0025) lifts the stride-4 subsample once per step of a
ladder of 56 targets plus the grid rows and re-measures the dust, the sky and every cloud on
each. Until now a step built a lifted raster (`RasterOps.Gamma`, a 16-bit power table with
linear interpolation, 16 MB per step on the plate-solved FITS) and then linearised every lifted
sample again with `ColorMath.SrgbToLinear`, a `Math.Pow(x, 2.4)` per sample: about 250 million
`Pow` on a 23 MP image, and for the grid rows a third pass of three `Pow` and a cube root per
pixel plus a sort of the whole L\* plane for a median the row never shows. The code review of
ADR 0025/0026 named it: the validation case V-004 had gone from 11 to 23 s, and the window sat
on the "praf" stage longer than on everything else. Measured on 2026-10-09 before any change:
the simulation alone took 17.1 s on `starless_rbf0.5_stretch0.17.fit` and 15.7 s on
`de_printat.png`, of which the 56 lifted patch passes took 10.6 s and the 17 grid-row L\*
passes about 5 s; the console run on the FITS took 27.2 s (median of three).

## Decision

A step of the ladder builds no lifted raster. One table per step, `LiftTable`, holds
`SrgbToLinear(Math.Pow(k / 65535, gamma))` for every 16-bit level and linearises a sample by
linear interpolation between its two neighbouring levels **on the linear value**; the patch
luminances are summed straight from it (`DustAnalyzer.PatchLstars(sub, table)`), and a grid
row's two pixel shares come from the L\* plane built through the same table
(`RasterOps.LstarPlane(sub, table)`, `TonalAnalyzer.LstarShares`), without the median and the
histogram the row does not carry. The as-is row, the map, the curves' anchors and every other
measurement keep the exact path (`ExactLinearizer`, `SrgbToLinear` on the sample as it is). The
two paths are structs behind one interface and the two per-sample loops are generic on it, so
there is one loop body and no delegate call per sample.

Measured on the same day, before the code was written (a scratch probe over the two reference
images, every ladder step): no patch moves by more than 7.63e-6 L\*, one float ulp of an L\*
between 32 and 64; the exact target of the FITS moves from 0.18358056279 to 0.18358056116.
After the change `/validate` printed every one of the 1365 figures of the twelve cases
identical to the run before it, and the console report on the FITS is byte-identical. The
simulation alone takes 2.6 s on the FITS and 2.75 s on the PNG; the console run on the FITS
14.6 s (median of three; the rest is reading, stars, noise and JPEG). The twelve validation
cases run in about 2.5 min instead of 3.7.

## Alternatives rejected

- **Bisection on the dust instead of a ladder.** The ladder serves the exact target, the two
  Dmax neighbours and 114 cloud rows from the same 56 lifts (ADR 0025); a bisection per cloud
  would be 2500 lifts.
- **Cache `Pow` per distinct sample value.** A float FITS has millions of distinct values on
  the subsample; the cache would be as large as the raster and miss most of the time.
- **A table on the lifted float.** It would keep the lifted raster and add a second
  interpolation for nothing; the table on the input level already answers for both steps.
- **Compare the pixel shares in luminance, without L\*.** It would save the cube root per pixel
  of the grid rows (1.5 s of the 2.6 s left on the FITS) but moves the comparison off the L\*
  the report prints; not worth a second formula for the same threshold.
- **Lift the as-is row through the table at gamma 1.** Interpolation for nothing, and the as-is
  figures the window shows would no longer be bitwise what they were.

## Consequences

- The two interpolations differ most in the first 16-bit steps at a small gamma (a dark sample
  lifted to a bright target), where the power curve's own step is widest and the old path's
  interpolation of it was already crude; no real sample of the reference images sits there
  (`LiftTableTests` holds the measured bounds: 2e-7 in luminance at an exact level, 1e-5 L\*
  per patch against the lifted-raster path).
- `RasterOps.Gamma` stays for the preview and the window's map, which need the lifted image.
- The validation figures did not move and did not need re-recording; `docs/validation.md`
  records the timing before and after.
