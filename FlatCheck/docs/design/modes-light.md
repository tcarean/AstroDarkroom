# Mode 3: end-to-end on a calibrated light (--light), the validity limit and series mode

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): the field-of-view guard is ADR 0020 and the
> series verdict ADR 0021; this note keeps the fit and the sign of its figure. **Covers:** in
> full, `Analysis/LightAnalyzer.cs` and `Runs/LightRun.cs`; by pointer,
> `Analysis/LightSuitability.cs`, `Analysis/LightSeriesAnalyzer.cs`, `Runs/LightSeriesRun.cs`,
> `Runs/SeriesFinder.cs`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 3. End-to-end - `flatcheck light.fit --light`, or `flatcheck process/ --light`

The only mode that closes the loop; 1 and 2 are proxies. Input is a light frame
or stack **calibrated with the flat but with no background extraction**, or a
**directory**, which runs the whole series (see "Series mode" below). It does not apply to
every dataset, and the mode refuses rather than guessing: read "The validity limit" below
before touching anything here.

`TileBackground` takes the 15th percentile inside each tile (a low percentile is
robust to stars and nebulosity, which only add signal), giving a local background
estimate. It then fits, simultaneously:

```
bg = a + b·X + c·Y + d·R² + e·R⁴          X, Y normalised by the half-diagonal
```

The plane (`b`, `c`) absorbs the light-pollution gradient; the radial terms
(`d`, `e`) capture residual vignetting, i.e. flat error. Fitting both at once is
what makes the test work under a polluted sky - the two have different shapes and
separate cleanly. The source carries an explicit warning: **do not** substitute
percentiles on circular annuli, because p20 on a ring picks up the dark side of
the sky gradient and reports it as false vignetting.

Four iterations of one-sided sigma-clipping (`res < 2.5σ`, σ from the MAD ×
1.4826) reject tiles containing nebulosity. One-sided is deliberate: contaminants
are positive.

The reported figure is `radPct = -(d + e)/a × 100` (R² = 1 at the corner). **The
sign is the diagnosis:**

- `radPct > 0` - background **falls** toward the edges → **under-correction**
  (different aperture at flats than at lights, or contaminated flat).
- `radPct < 0` - background **rises** toward the edges → **over-correction**,
  almost always a missing/wrong flat-dark: you divided by a flat that still
  contains an offset.

#### The validity limit

ADR 0020 holds the whole of it: why the plane/radial split is a geometric degeneracy over a wide
field and not an implementation limit (the Smida session, 32 subs, correlation -0.94 with the
sky gradient), the layers `LightSuitability.Check` derives the field of view from, the `Ok` /
`Marginal` / `TooWide` levels on `Th.LightFovOk/Max`, why an unknown field runs with a note, why
the focal-only fallback prints no field figure, and why `--fov` is not a force flag. Guards:
`AnEmptyHeaderRunsInsteadOfRefusing`, `TheFocalOnlyFallbackIsGradedOnTheFocalLength`.

The positive counterpart of the Smida run, measured on 2026-11-01 with the 2.3.0 console on
`Veil/2026-10-06-3h/process` (Samyang 135 f/2.8 on the D3300, ~12 degrees of diagonal, 87
`pp_light_*.fit` calibrated with bias and flat, `FOCALLEN=135` and no `XPIXSZ`, so the focal-only
fallback graded it `Ok` and the field printed as undetermined): mean +1.13 %, spread
-1.30 .. +4.31 %, SEM 0.15, correlation not applied (the sky gradient went 6.5 .. 13.1 %, a span
of 6.6 points), verdict OK. Over frames 37-87 the sky gradient doubled while the radial figure
stayed near +0.4 %: at that field the plane and the radial terms separate. The radial column is
still not noise around a constant: -0.3 % over frames 1-9, a smooth hump to +4.31 % over frames
10-36 (mean +3.0 %), 0.30 points between consecutive frames. A single frame at 12 degrees can
therefore carry 3-4 points of sky, and the SEM, which assumes independent frames, understates
that; only the series mean is a measurement. The same master's structure run is GOOD on the
five shape tests and REDO on depth against the lensfun profile (+0.95 stops); the lights show
the flat corrects the vignetting to ~1 % in the corner, so the profile was wrong for that setup,
not the flat. Not automated (`docs/validation.md`, "Not automated").

#### Series mode - `flatcheck process/ --light`

ADR 0021 holds the whole of it: why a series carries a discriminator a single frame does not,
`LightSeriesRun.Execute`'s one-frame-at-a-time loop (the one run of the five with state and
progress) with its two outputs, the caller's `TextWriter` and the `onFrame` callback, and where
cancellation is checked; the three conditions for a verdict (`Th.LightSeriesMinFrames`,
`Th.LightSeriesSemMax`, `Th.LightSeriesSkyCorr`, the last skipped below
`Th.LightSeriesSkySpanMin`), why `NaN` fails a condition, why `planePct` is never a gate, the
rejected extrapolation to zero sky, and `SeriesFinder.Find` (in-process glob, ordinal sort, the
default pattern that excludes registered frames). Two mechanics it does not spell out:

- The comparisons are written `!(x < threshold)`, not `(x >= threshold)`, the same convention as
  `Stats.Verdict`; that is what puts `NaN` on the bad side.
- The ordinal sort (`StringComparer.Ordinal`, what Python `sorted()` did) was chosen so the
  cross-check of the day could not report a difference that did not exist;
  `parity_light_series_*.txt` pin the order now (ADR 0047).
