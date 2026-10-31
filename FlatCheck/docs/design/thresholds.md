# Thresholds, the residual scale and the exit codes

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): the rule that every metric is a ratio and every
> threshold lives in `Th` is CLAUDE.md's rule "Ratios" and ADR 0012; the residual scale and the fifth test
> are ADR 0015; the illumination offset and the tilt are
> ADR 0014. This note keeps the table of every threshold, the wording each band carries, the
> mechanics of the verdict ladders and the exit codes. **Covers:** `Thresholds.cs` (`Th`),
> `Stats.Verdict` / `Severity` / `ResidualVerdict`, `FlatCheckCli.ExitFor`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## Thresholds

Every threshold lives in `Th` (the rule "Ratios"; ADR 0012 holds the reason, the phase-8 clean-up that put
the last inline ones there and the `--expo` midpoints). Full picture:

| metric | mode | OK < | PROBLEMA ≥ | defined in |
|---|---|---|---|---|
| corner spread | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| azimuthal asymmetry (max) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| residual after radial model | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| tilt (linear term) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| worst tile (`pata maxima`) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| local spot (`pete locale`) | structure | 2% = warning only, never a verdict | | `Th.LocalSpotWarn` |
| vignetting vs the lens profile (nearest) | structure, `--raw`/`--lens` | *residual scale* through `2^|D| - 1`: 0.03 / 0.14 / 0.26 stops; the report also prints what `--light`'s 2 / 5 % would say of the same corner error, and the reference's own spread (`Th.ProfileDatabaseSpreadStops` = 0.2 stops = 15 %) - informative, never a verdict | | `Th.Resid*` via `Stats.DepthVerdict`; `Stats.DepthOnLightScale` for the note |
| \|radial component\| | light | 2.0% | 5.0% | `Th.LightRadOk/Bad` |
| field of view (diagonal) | light | 20° | 40° = refused | `Th.LightFovOk/Max` |
| focal length, no pixel size | light | 120 mm | below 60 mm = refused | `Th.LightFocalOk/Min` |
| standard error of the mean | light, series | 2.0 pts | | `Th.LightSeriesSemMax` |
| \|corr(radPct, planePct)\| | light, series | 0.70 | | `Th.LightSeriesSkyCorr` |
| frame count | light, series | ≥ 5 | | `Th.LightSeriesMinFrames` |
| request vs the profile used (focal, f-stop) | synthetic, structure `--raw`/`--lens` | 1 mm or 5 % on the focal, 0.5 stops on the aperture = warning only, never a verdict | | `Th.ProfileFocalTolMm/Frac`, `Th.ProfileStopsTol` |
| centre clipping | expo | 0.05% | - | `Th.ExpoClip` |
| saturated pixel (what clipping counts) | expo | at or above 98 % of white | - | `Th.ExpoClipWhite` |
| centre level | expo | 50-65% | - | `Th.ExpoLo/Hi` |
| centre level (sky) | expo `--sky` | 15-50% | - | `Th.ExpoSkyLo/Hi` |

### The residual scale

ADR 0015 holds the decision and its reasons: the five deviation tests measure one quantity and
share one four-band scale instead of pass/fail thresholds, boundaries strict `<` and `NaN` to
`REFA`, the verdict the worst of the five, the worst tile as the fifth test, why the five read
one defect with different numbers (the 12 % gradient figures), why the scale is an upper bound
on the flat error and not the error, and why the console prints the scale itself. ADR 0014
holds why the illumination-centre offset is informative and the tilt is the test, with the
non-monotonic measurement and the Samyang figures. What the bands tell the user:

| band | verdict | meaning |
|---|---|---|
| < 2% | `PERFECT` | quality is perfect |
| 2-10% | `BUN` | usable; background extraction removes what is left |
| 10-20% | `ACCEPTABIL` | usable, but run `--light` to confirm before a hard stretch - if your field is narrow enough for it, see "The validity limit" |
| > 20% | `REFA` | reshoot the flats |

**`reziduu dupa model rad.` is the one that matters most.** It measures the departure from the
radial model directly, over the whole frame. Corners, azimuthal and tilt are partial views of
the same deviation: the corners catch it only where the four boxes sit, the azimuthal test only
inside the inscribed circle, and the tilt only the part of it that is a plane. The worst tile is
its complement: the residual trims extremes, the worst tile *is* the extreme.

`Stats.Severity` spans both ladders (`OK`/`PERFECT` = 0, `BUN` = 1, `SUSPECT`/`ACCEPTABIL` = 2,
`PROBLEMA`/`REFA` = 3), and the exit code is derived across all modes. Only `--light` speaks
`OK`/`SUSPECT`/`PROBLEMA` (the `--ratio` mode's informative label on `Th.RatioOk/Bad` left with
the mode, ADR 0067).

## Exit codes

Since phase 8 the tool is usable from a script:

| code | meaning |
|---|---|
| 0 | OK / PERFECT / BUN - usable as-is |
| 1 | error - missing file, bad argument, invalid FITS, failed fit, `--expo` on a file that isn't a raw flat, or `--light` on a field too wide for the test |
| 2 | SUSPECT / ACCEPTABIL / NEDETERMINAT (and `--expo`'s two "acceptable but..." bands) |
| 3 | PROBLEMA / REFA |

Verdict severity is monotonic, so "anything non-zero" means "not clean, go look".
`--histeq` has no verdict and does not go through `ExitFor`: 0 means the picture was
written, 1 that it could not be. `--synthetic` has the same shape: 0 written, 1 refused (a colour cube, a focal
length, f-stop or crop factor nobody could supply, or a lens whose file carries no
vignetting model, ADR 0071), 2 the lens could not be chosen from
the text (none, or several - the candidates are printed). `--lenses` is always 0.
`--against` goes through `ExitFor` on the verdict of the *difference*.
