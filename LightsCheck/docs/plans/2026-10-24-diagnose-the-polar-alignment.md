# Diagnose the polar alignment from the series

Approved and saved on 2026-10-24. Branch
`claude/Diagnose-the-polar-alignment` in its own worktree
(`.claude/worktrees/Diagnose-the-polar-alignment/LightsCheck`), from `main` at 05fffee.

## Context

The owner asked whether a session's polar alignment was poor, by how much and what it cost, and
whether the drift of R-023 is contaminated by the small post-pause steps. Measured on the Veil
registration on 2026-10-24 (ADR 0016, accepted the same day): the drift is contaminated (nine
post-pause presses of 3 to 5 px halve the RA drift and inflate the DEC drift; the clean figure is
-0.19 / -0.96 px/min, 1.9 px per exposure); the polar error brackets between 4.2' (the DEC drift
over the sidereal rate) and 10.4' (adding the field rotation over the rate times cos DEC); the RA
drift of 5.8 "/min is mostly not the alignment (at most the rotation times tan DEC, 1.3 "/min);
the alignment costs at most 0.57 px per 120 s exposure, an elongation of 1.01. The requirements
R-049 (drift from the pairs without a pause, with a fallback) and R-050 (the bracket, the cost,
the verdict, the rows) are `accepted`; their tests `R049_*` and `R050_*` in `SeriesAnalyzerTests.cs`
and `SeriesTests.cs` fail to compile on the members they name (61 errors). `validation.json`
carries six `null` keys on V-008 and V-013, noted in `validation.md`.

## Steps

### 1. Core, numbers (`src/LightsCheck.Core/Analysis/SeriesAnalyzer.cs`, `Thresholds.cs`, `Wcs.cs`)

- `Thresholds.cs`, series section after `WalkingStreakVisible`: `DriftMinPairs = 3` (clean pairs
  needed before the fallback) and `PolarDriftFwhmFraction = 1.0 / 3` (the alignment's drift per
  exposure under a third of the FWHM adds under 1.03 of elongation), each with the why.
- `Wcs` gains a trailing `double? DecDeg = null` (CRVAL2); every existing construction compiles.
- `SeriesAnalyzer.Analyze` gains `double? arcsecPerPixel = null`.
- `SeriesAnalysis` gains `bool DriftApproximate`, `MountMove? StarDriftPerMin`, `double? DeclinationDeg`,
  `PolarErrorMinArcmin`, `PolarErrorMaxArcmin`, `PolarDriftMinPx`, `PolarDriftMaxPx`, `PolarElongation`,
  `PolarRaDriftMaxArcsecPerMin` (all `double?`) and `PolarAlignment PolarAlignment`; a new enum
  `PolarAlignment { Unknown, Ok, Marginal, Attention }` beside `WalkingNoise`.
- Drift: a pair is a drift pair when `dither.Step[i]` exists and `cadence.PauseSeconds[i] <= Th.DitherPause`
  (so `Analyze` reads the cadence's pauses, already a parameter); with at least `Th.DriftMinPairs`
  of them the mean step per minute is theirs; otherwise the old rule (not a move) with
  `DriftApproximate = true`. `DriftPairs` is the count used. Everything downstream (drift per
  exposure, dash, walking word) reads whichever drift exists, unchanged.
- `StarDriftPerMin`, only for a clean drift: the stars' motion per minute as a `MountMove`, through
  the same mapping the moves use (`wcs.SkyOffsetDeg(-driftX, +driftY)` is the pointing, ADR 0013;
  the stars are its negative, so `(-ra / RaScaleDeg, -dec / DecScaleDeg, Signed: true)`); with the
  axis from the trails, the magnitudes unsigned; null without an axis.
- Polar, from `StarDriftPerMin`, the scale and the median exposure (a local const
  `SiderealArcsecPerMin = 902.46`): `decArcsec = |DecPx| x scale`; `PolarErrorMinArcmin =
  decArcsec / 902.46 x 3437.75`; `PolarDriftMinPx = |DecPx| x exposureMin`. With `wcs.DecDeg`
  and `RotationPerHourDeg` (`rho = |rot| x 60` "/min): `A = rho / (902.46 cos d) x 3437.75`,
  `PolarErrorMaxArcmin = sqrt(min^2 + A^2)`, `PolarRaDriftMaxArcsecPerMin = rho tan d`,
  `PolarDriftMaxPx = sqrt(decArcsec^2 + RaMax^2) x exposureMin / scale`, `PolarElongation =
  sqrt(s^2 + L^2 / 12) / s` with `s = SessionFwhm / 2.355`, `L = PolarDriftMaxPx` (null without a
  FWHM). Verdict against `thr = Th.PolarDriftFwhmFraction x SessionFwhm`: `Attention` when the
  lower drift is at or over `thr`; else `Ok` when the upper is known and under it; else `Marginal`
  when the upper is known; else `Unknown`. All null (and `Unknown`) without a clean drift, an
  axis, a scale or a FWHM for the verdict.

### 2. Core, words (`src/LightsCheck.Core/Report/SeriesReport.cs`)

- `DriftRows`: the first row's parenthesis becomes `(39 perechi fara pauza)` or `(29 perechi cu
  pauza, aproximativ: pasii mici de dithering intra in cifra)`; after `rotatie de camp` two rows:
  - `aliniere polara`: `eroare intre 4.2' si 10.4' (driftul pe DEC de 1.1"/min; rotatia de 2.1'/h
    la DEC +31.6 grade); drift din aliniere cel mult 0.57 px intr-o expunere de 120 s (0.37 px
    sigur), alungire adaugata cel mult 1.01 pe FWHM 3.12 px`; equal bounds to one decimal read
    `eroare de 5.7'`; lower bound only: `eroare de cel putin 5.7' (driftul pe DEC de 1.5"/min;
    fara plate solve nu se stie declinatia pentru limita de sus); drift din aliniere cel putin
    0.50 px intr-o expunere de 120 s`; approximate or no pairs: `nemasurabila (dithering la fiecare
    cadru: nicio pereche de cadre consecutive fara pauza; o serie de 10+ cadre fara dithering ar
    masura-o)`; no axis: `- (fara axa RA)`; no scale: `- (fara scara)`.
  - `drift pe RA`: `5.8"/min, stelele spre est; din aliniere cel mult 1.3"/min, restul e urmarirea
    (viteza, eroarea periodica, flexiune), nu alinierea` (the sense from the sign of `RaPx`, `est`
    positive, dropped when unsigned); without a solve `1.8"/min (fara plate solve nu se stie cat e
    din aliniere)`; `-` when the alignment row is not measured. When the RA drift is under the
    alignment's share: `... din aliniere cel mult 1.3"/min: poate fi toata din aliniere`.
- `SummaryRows`: after `dithering`, `new("aliniere polara", text, verdict)` with `OK` / `LA LIMITA` /
  `ATENTIE` (null when `Unknown`): `eroare intre 4.2' si 10.4', costa cel mult 0.57 px intr-o
  expunere`; Attention `eroare de cel putin 22.9', costa cel putin 2.0 px intr-o expunere`;
  Marginal `eroare intre a' si b', costa intre x si y px intr-o expunere`; lower bound only `eroare
  de cel putin 5.7', costa cel putin 0.50 px intr-o expunere (fara plate solve nu se stie limita
  de sus)`; otherwise the same `nemasurabila (...)` / `- (...)` as the drift row, through one helper.
- Reuse `Px`, `Signed`, `RunReport.F`, `Opt`; a `PolarReason(s)` helper for the unknown cases.

### 3. Core, chain (`src/LightsCheck.Core/Runs/SeriesRun.cs`)

- Move the pixel-scale block above the analyzer call and pass `scale`.
- `ReadWcs` reads `CRVAL2` (`FitsReader.HDbl(h, "CRVAL2", double.NaN)`, NaN to null) into `DecDeg`.

### 4. Window: nothing to code

The rows bind through `SeriesReport.DriftRows` / `SummaryRows` (ADR 0008); `VerdictPalette`
already colours `OK`, `LA LIMITA` and `ATENTIE`. `WindowSmokeTests` must still render with no
binding error (the Wpf tests run in the gate).

### 5. Tests and goldens

- `tests/LightsCheck.Tests/ValidationTests.cs`, series block: read the six keys only when present
  and not null (`TryGetProperty` + `ValueKind != Null`): `driftPairs` (int), `driftApproximate`
  (bool), `polarErrorMinArcmin` / `polarErrorMaxArcmin` (tolerance `arcmin`), `polarDriftMaxPx`
  (`px`), `polarAlignment` (string).
- Goldens `series.txt` and `series-classes.txt` regenerated (`LIGHTSCHECK_UPDATE_GOLDEN=1`); expected
  changes only: the drift row's parenthesis `(4 perechi fara pauza)`, the two new DRIFT rows (lower
  bound only, the goldens have no solve), the REZUMAT row `aliniere polara` with no verdict word.
  Every other changed line is a finding (the rule "Golden files").
- `RepoInvariantTests`: R-049 and R-050 become `implemented` with the tests already named.

### 6. Docs and figures

- `docs/design/series-analysis.md`: the drift paragraph (pairs without a pause, the fallback, the
  star drift on the sky) and a section "The polar alignment" (the formulas, what is bounded and why,
  the verdict). `Covers:` unchanged.
- `README.md`: the DRIFT SI ROTATIE bullet gains `aliniere polara` and `drift pe RA`; the REZUMAT
  list gains the row; the walking-noise paragraph's "driftul" is unchanged.
- `CHANGELOG.md` under Unreleased: Added (the polar alignment rows and verdict, R-050) and Changed
  (the drift over the pairs without a pause, R-049; the Veil figures that move).
- ADR 0015: a dated note under its table ("1.1 to 1.3 px per exposure" was over the 45 pairs;
  ADR 0016 measures 1.9 over the 39 clean pairs), the kit's ADR 0024 way.
- `/validate` with `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro` (minutes): paste the drift
  table; V-008's `driftYPerMin`, `driftPerExposurePx`, `walkingStreakPx` move to the measured
  values with a dated History line (the owner accepted the move in ADR 0016; the figures are the
  run's, pasted); fill the six null keys of V-008 and V-013 from the run; V-013's drift figures
  should not move (fallback). `measuredOn` updated.
- Requirements R-049, R-050 to `implemented`; the plan saved to `docs/plans/`.

### 7. Verification

1. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -SkipTests` green while editing.
2. `dotnet test LightsCheck.sln --filter "FullyQualifiedName~R049|FullyQualifiedName~R050"` green.
3. Full gate `scripts/verify.ps1` green (goldens regenerated and their diff read line by line first).
4. `/validate`: 13 cases, the drift table pasted in the session; no figure moves except the ones
   named above.
5. `scripts/check-design.ps1 -Since main` green; `/wrap-up` (commit on the branch, merge proposed).

## What deviated

- The tool's upper bound on Veil is 12.0', not the 10.4' to 10.7' of the plan's context: R-023's
  rotation rate is end to end (2.5'/h, the first and the last frame each carrying the dithers'
  projection of up to 40"), where the measurement behind ADR 0016 fitted a line (2.13'/h). The
  lower bound is 4.2' in both; the cost reads 0.64 px per exposure instead of 0.57. Recorded in
  `docs/validation.md`; the rotation rule was not changed.
- V-013 records `polarAlignment: "Unknown"` and leaves out the three numeric polar keys: the
  session cannot give them, and a `null` would read as unmeasured.
- The fixture test of R-049 uses steps of about 1.1 px: a 3 px step on the synthetic star grid
  registers as a grid spacing (the vote picks 139.7 px), a fixture artefact noted in the test.
- ADR 0015's note went in as a `(Corrected ...)` paragraph after its table, the kit's ADR 0024
  form, not the plain dated note the plan named.
