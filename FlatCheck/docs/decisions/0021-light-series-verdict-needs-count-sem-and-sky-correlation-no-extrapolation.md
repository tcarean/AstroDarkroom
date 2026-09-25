# 0021. A light-series verdict needs frame count, SEM and sky correlation; no extrapolation to zero sky

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-light.md; the decision itself dates
from the original design of series mode. A single frame's `radPct` is the sum of the flat error,
identical in every frame because the same master was divided out of each, and a sky radial term
that changes through the session. A series therefore carries a discriminator a single frame
does not. `LightSeriesRun.Execute` loads one file at a time (a 24 MP frame runs to hundreds of
MB), runs the unchanged `LightAnalyzer.Analyze`, streams a table row and hands the results to
`LightSeriesAnalyzer.Combine`, which is pure. The question was when the combined figure may be
called a verdict.

## Decision

A verdict is given only if all three hold: frame count >= `Th.LightSeriesMinFrames` (5);
standard error of the mean (`σ/√n`, ddof=1) < `Th.LightSeriesSemMax` (2.0);
|Pearson(radPct, planePct)| < `Th.LightSeriesSkyCorr` (0.70), this last test skipped unless the
sky gradient spans at least `Th.LightSeriesSkySpanMin` (10 points) across the series.
Otherwise the verdict is `NEDETERMINAT` and the report names the failed condition. Comparisons
are written `!(x < threshold)` so NaN falls on the bad side (a one-frame series has NaN `sem`
and must not pass). `planePct` being large is informative, never a gate. Files are selected by
`SeriesFinder.Find`, which matches the glob in-process (`*` and `?` only), sorts ordinally, and
by default takes `pp_light_*.fit`, which excludes `r_pp_light_*`.

## Alternatives rejected

- **Extrapolating to zero sky gradient.** Regressing `radPct ~ planePct` over the real session
  gives an intercept of +122 %, far outside data whose gradient spans 49-88 % and never nears
  zero; the correlation is a diagnostic, not an estimator.
- **Running the correlation test on a session with a constant sky.** There the correlation is
  noise and would reject good data; hence the span gate.
- **Gating on `planePct`.** The plane is an indirect proxy - the cause of non-separation is
  curvature - and a badly calibrated threshold there would reject good data; the field-of-view
  guard (ADR 0020) covers the cause.
- **`Directory.GetFiles(dir, pattern)`.** Win32 matching carries the 8.3 legacy where `*.fit`
  also catches `.fits`.
- **`fnmatch` on the Python side.** Character classes would then differ between the two
  implementations; they are literal in both.
- **Including registered frames.** They are geometrically transformed, so the sensor-fixed flat
  error is shifted from the centre the radial model is fitted around; the measurement would be
  corrupted silently.

## Consequences

- Rows go to the caller's `TextWriter` as frames finish and through an optional `onFrame`
  callback; cancellation is checked between frames, and a started frame runs to the end
  (~0.5 s each).
- Never more than one full frame in memory; keep it that way.
- Ordinal sorting must stay identical to Python `sorted()`, or `crosscheck.py` reports
  differences that do not exist.
- The WPF app reuses the same matcher, so `pp_flat_stacked*.fit` there means what `--pattern`
  means on the console.
- All thresholds live in `Th` (`LightSeries*` family).
- Source note: docs/design/modes-light.md ("Series mode").
