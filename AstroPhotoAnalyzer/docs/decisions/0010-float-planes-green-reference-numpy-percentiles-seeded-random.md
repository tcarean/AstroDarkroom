# 0010. Pixels are float planes in 0..1 with green as the reference; percentiles follow numpy; the random source is seeded

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `docs/specification.md` §3 ("The raster", "Statistics")
and `docs/implementation-plan.md` ("Decisions taken before the first line": pixel storage, the
FITS reader); the decision itself dates from phase 1, 2026-09-20. The reference image is 23 MP of
16-bit RGB. The numbers the tool was built to reproduce were computed in the originating
conversation with Python and numpy. The noise and the plateau sample random tiles, and their
figures must not change from one run, or one runtime, to the next.

## Decision

- A `Raster` is one or three `float[]` planes with values in 0..1, row 0 at the top, normalised
  from the file's integers (`/255`, `/65535`, `/4294967295`) or taken as is from float data and
  clipped. `float`, not `double`: 277 MB instead of 554 MB for 23 MP RGB, and the 24 bits of a
  float's mantissa keep every one of the 65536 16-bit levels (`RasterOps.Level16` round-trips
  them all).
- The reference channel, the one every single-channel measurement uses (the background level,
  the noise, the stars, the plateau), is the green plane on colour and the only plane on mono.
- Percentiles use numpy's default, linear interpolation between ranks.
- `SeededRandom` is xorshift64\*, written in the core, so a seed gives the same tiles on every
  runtime and every year.

## Alternatives rejected

- **`double` planes.** Twice the memory for no precision a 16-bit file can use.
- **Another percentile definition** (nearest rank, or another interpolation). The acceptance
  numbers would differ by the definition alone, and a drift would no longer mean a change in the
  measurement.
- **`System.Random` with a seed.** Its sequence belongs to the runtime; a seed is reproducible
  only as long as the implementation behind it stays the same.

## Consequences

- A change of seed, generator or percentile definition moves the noise, plateau and percentile
  figures: a validation case on the reference image (`docs/validation.md`).
- Guards: `RasterOpsTests` (the 16-bit round trip), `StatsTests`, the golden files.

Source: docs/specification.md §3; docs/implementation-plan.md.
