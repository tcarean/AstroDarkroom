# Mode 3: end-to-end on a calibrated light (--light), the validity limit and series mode

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `Analysis/LightAnalyzer.cs`, `Analysis/LightSuitability.cs`, `Analysis/LightSeriesAnalyzer.cs`, `Runs/LightRun.cs`, `Runs/LightSeriesRun.cs`, `Runs/SeriesFinder.cs`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 3. End-to-end - `flatcheck light.fit --light`, or `flatcheck process/ --light`

The only mode that closes the loop; 1 and 2 are proxies. Input is a light frame
or stack **calibrated with the flat but with no background extraction**, or a
**directory**, which runs the whole series (see "Series mode" below).

**But it does not apply to every dataset, and the mode now refuses rather than
guessing.** Read "The validity limit" below before touching anything here - it is the
single most important thing about this mode, and it was missing until a real session
made the tool report a confident false positive.

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

The plane/radial split works **only while the light-pollution dome is quasi-linear
across the frame**. Over a wide field the dome *curves inside the frame*, and the
radially symmetric part of that curvature has exactly the shape of vignetting. Two
things with the same shape cannot be separated by a fit, however well written. This is
a geometric degeneracy, not an implementation limit - do not try to fix it with a
better fit.

It was measured, not assumed. On `Milkyway-Core/2026-08-12-Smida` (Tokina 20 mm f/2.8
on a D3300, ~70° diagonal, Milky Way core low in the sky), a single frame reported
`+3.13 % SUSPECT, under-correction`. Across all 32 calibrated subs of that session the
same figure swept smoothly from `+3.13 %` through `-19.20 %` to `+35.73 %` in 81
minutes, with only 1.36 points between consecutive frames. Correlation between the
reported radial component and the sky gradient: **-0.94**, R² = 0.88. Eighty-eight
percent of what the tool called "flat" was sky.

`LightSuitability.Check` therefore gates the mode, deriving the diagonal field of view
in layers, most trusted first:

| source | when |
|---|---|
| `--fov <deg>` | stated by the user; beats everything |
| WCS `CDELT1` / `CD1_1` | a plate solve is present; measured, not deduced |
| `XPIXSZ` + `FOCALLEN` (+ binning) | astro cameras; the exact field |
| `FOCALLEN` alone | DSLRs - Siril does **not** write `XPIXSZ` for them |
| nothing | **runs anyway**, with a note |

Levels: `< Th.LightFovOk` (20°) `Ok`, up to `Th.LightFovMax` (40°) `Marginal` (runs, with
a warning), above that `TooWide` - **refused**, exit 1, no figure produced at all.

Two decisions here are load-bearing and easy to break:

- **Unknown is not a refusal.** If a missing header meant "too wide", every synthetic
  fixture in the test suite - and every minimal FITS - would become unanalysable, and the
  guard would do more harm than the problem it fixes. Pinned by
  `AnEmptyHeaderRunsInsteadOfRefusing`.
- **The focal-only fallback reports no field figure.** The field depends on the sensor,
  which is unknown at that point, so printing a number would be inventing one. It grades
  the focal length directly against thresholds computed for the largest plausible sensor
  (full frame, 43.27 mm diagonal), which is conservative: on a smaller sensor a good frame
  can come out `Marginal`. That is what `--fov` is for. Pinned by
  `TheFocalOnlyFallbackIsGradedOnTheFocalLength`.

`--fov` is not a force flag. It tells the tool the truth; it does not ask it to ignore
the physics, and a wide field declared by hand is refused exactly like a deduced one.

#### Series mode - `flatcheck process/ --light`

A single frame's `radPct` is the sum of two things: the flat error, which is **the same
in every frame by construction** (the same master flat was divided out of each), and a
sky radial term, which changes through the session. So the series carries a discriminator
the single frame does not.

`LightSeriesRun.Execute` loads each matching file in turn - never more than one full
frame in memory, since a 24 MP frame runs to hundreds of MB - runs the unchanged
`LightAnalyzer.Analyze` on it, streams a table row, and hands the results to
`LightSeriesAnalyzer.Combine`, which is pure.

Of the five runs it is the only one that had anything worth duplicating - a loop, with
state, progress and a memory trick that is not visible from the signature. It therefore
has two outputs rather than one: rows go into the caller's `TextWriter` as frames finish
(so the console does not sit silent for two minutes), and the same frame goes out through
an optional `onFrame` callback (so the window fills its table live). Cancellation is
checked *between* frames; a frame already started runs to the end, which at ~0.5 s each
is a fine response time.

The verdict is given **only if all three conditions hold**:

| condition | threshold |
|---|---|
| frame count | ≥ `Th.LightSeriesMinFrames` (5) |
| standard error of the mean (`σ/√n`, ddof=1) | < `Th.LightSeriesSemMax` (2.0) |
| \|Pearson(radPct, planePct)\| | < `Th.LightSeriesSkyCorr` (0.70) |

Otherwise the verdict is **`NEDETERMINAT`** and the report says which condition failed
and why. Three details that are not obvious:

- The correlation test is **skipped** unless the sky gradient actually varies across the
  series (`Th.LightSeriesSkySpanMin`, 10 points). On a clean session where it sits
  constant, the correlation is noise and would reject good data.
- The comparisons are written `!(x < threshold)`, not `(x >= threshold)`, so **NaN falls
  on the bad side** - same convention as `Stats.Verdict`. A one-frame series has no
  spread, `sem` is NaN, and it must not pass silently.
- `planePct` being large is **informative only**, never a gate. The plane is an indirect
  proxy - the real cause of non-separation is curvature - and a badly calibrated threshold
  there would reject good data. The field-of-view guard covers the cause.

**What was tried and rejected:** extrapolating to zero sky gradient. Regressing
`radPct ~ planePct` over the real session gives an intercept of `+122 %` - an
extrapolation far outside the data, whose gradient never goes near zero (it spans 49-88 %).
The correlation is a good diagnostic and a useless estimator; do not turn it into one.

**File selection.** `SeriesFinder.Find` enumerates the directory and matches the pattern
**in-process**, rather than `Directory.GetFiles(dir, pattern)`: Win32 matching still
carries the 8.3 legacy where `*.fit` also catches `.fits`. Sorting is ordinal on the file
name (`StringComparer.Ordinal`, the same as Python `sorted()`), chosen so the cross-check
of the day could not report a difference that did not exist; `parity_light_series_*.txt` pin
the order now. The glob supports `*` and `?` only; character classes are literal. The WPF
app reuses the same matcher for the
names it accepts, so `pp_flat_stacked*.fit` there means exactly what `--pattern` means
here.

The default pattern `pp_light_*.fit` excludes `r_pp_light_*` because the glob is anchored
at the start, and that is correct rather than incidental: registered frames are
geometrically transformed, so the sensor-fixed flat error is shifted relative to the
centre the radial model is fitted around. Including them would corrupt the measurement
silently.

Thresholds are all in `Th` (`LightRadOk`/`LightRadBad`, the `LightFov*`/`LightFocal*`
guard, and the `LightSeries*` family).

