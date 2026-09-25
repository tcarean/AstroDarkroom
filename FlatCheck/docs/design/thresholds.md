# Thresholds, the residual scale and the exit codes

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `Thresholds.cs` (`Th`), `Stats.Verdict` / `Severity` / `ResidualVerdict`, `FlatCheckCli.ExitFor`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## Thresholds

Every threshold lives in `Th`. Full picture:

| metric | mode | OK < | PROBLEMA ≥ | defined in |
|---|---|---|---|---|
| corner spread | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| azimuthal asymmetry (max) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| residual after radial model | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| tilt (linear term) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| worst tile (`pata maxima`) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| local spot (`pete locale`) | structure | 2% = warning only, never a verdict | | `Th.LocalSpotWarn` |
| vignetting vs the lens profile (nearest) | structure, `--raw`/`--lens` | *residual scale* through `2^|D| - 1`: 0.03 / 0.14 / 0.26 stops; the report also prints what `--light`'s 2 / 5 % would say of the same corner error, and the reference's own spread (`Th.ProfileDatabaseSpreadStops` = 0.2 stops = 15 %) - informative, never a verdict | | `Th.Resid*` via `Stats.DepthVerdict`; `Stats.DepthOnLightScale` for the note |
| the five structure tests on `(A+B)/2` | ratio | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| world-fixed gradient per orientation | ratio | 1.0% | 3.0% | `Th.RatioOk/Bad` - informative label only |
| \|radial component\| | light | 2.0% | 5.0% | `Th.LightRadOk/Bad` |
| field of view (diagonal) | light | 20° | 40° = refused | `Th.LightFovOk/Max` |
| focal length, no pixel size | light | 120 mm | below 60 mm = refused | `Th.LightFocalOk/Min` |
| standard error of the mean | light, series | 2.0 pts | | `Th.LightSeriesSemMax` |
| \|corr(radPct, planePct)\| | light, series | 0.70 | | `Th.LightSeriesSkyCorr` |
| frame count | light, series | ≥ 5 | | `Th.LightSeriesMinFrames` |
| worst tile of the removed component | smooth | 2 % = warning only, never a verdict | | `Th.SmoothKeptWarn` |
| request vs the profile used (focal, f-stop) | synthetic, structure `--raw`/`--lens` | 1 mm or 5 % on the focal, 0.5 stops on the aperture = warning only, never a verdict | | `Th.ProfileFocalTolMm/Frac`, `Th.ProfileStopsTol` |
| fine texture of the input | smooth, `--scale auto` | 0.20 % = "nothing to smooth" below | | `Th.SmoothTextureMin` |
| fine texture of the output | smooth, `--scale auto` | 0.05 % = texture gone below | | `Th.SmoothTextureGone` |
| drop in azimuthal asymmetry or tilt | smooth, `--scale auto` | 0.5 pts = scale too large above | | `Th.SmoothScaleLoss` |
| centre clipping | expo | 0.05% | - | `Th.ExpoClip` |
| centre level | expo | 50-65% | - | `Th.ExpoLo/Hi` |
| centre level (sky) | expo `--sky` | 15-50% | - | `Th.ExpoSkyLo/Hi` |

### The residual scale

The five structure deviation tests - corner spread, azimuthal asymmetry, residual after
the radial model, tilt, and the worst tile - all measure *the same thing* from different
angles: how much of the frame fails to be explained by a purely radial function. All five
are fractional amplitudes in percent, so they share one four-band scale rather than a
pass/fail threshold:

| band | verdict | meaning |
|---|---|---|
| < 2% | `PERFECT` | quality is perfect |
| 2-10% | `BUN` | usable; background extraction removes what is left |
| 10-20% | `ACCEPTABIL` | usable, but run `--light` to confirm before a hard stretch - if your field is narrow enough for it, see "The validity limit" |
| > 20% | `REFA` | reshoot the flats |

Boundaries are strict `<`, so each one falls into the worse band. `NaN` degrades to
`REFA`, same rule as `Verdict`.

**`reziduu dupa model rad.` is the one that matters most**, and the console output says
so inline. It measures the departure from the radial model directly, over the whole frame.
Corners, azimuthal and tilt are partial views of the same deviation: the corners catch it
only where the four boxes sit, the azimuthal test only inside the inscribed circle, and
the tilt only the part of it that is a plane. The worst tile is its complement: the
residual trims extremes, the worst tile *is* the extreme.

**The five do not read the same defect with the same number**, and that is by shape, not
by miscalibration. Measured on a 3:2 frame with a 12% gradient: tilt 12.0, corners 11.5,
residual 10.4, azimuthal 7.3 (its rings stop at the inscribed circle), worst tile 6.0 (a
per-pixel deviation, half the peak-to-peak). The verdict is the worst of the five, so a
gradient is carried by the tilt and the corners, a compact blotch by the worst tile, and
a broad blotch inside the circle by the residual and the azimuthal test.

**It is an upper bound on the flat error, not the flat error**, and the report used to
claim otherwise - the sentence said "adica exact eroarea de flat". What it measures is the
sum of two things with opposite consequences: a part fixed to the camera (the optics;
divides out of the lights correctly, harmless) and a part fixed in the world (the
illumination; does not reproduce, harmful). On a single master the two cannot be
separated - the information is not there. `--ratio` is what separates them, and
`WhyResidualMatters` now says so and points at it. This is why a lens with genuine
non-radial vignetting reads as "error" here and is not one: measured on this library, a
Nikkor 35 DX master carries 4.00% of corner spread of which only ~1.3% was the sky.

The structure mode prints the scale itself, under the heading
`SCARA celor cinci teste de abatere de mai sus`, so the numbers can be read without
consulting these docs.

The illumination-centre offset is **not** on this scale, and since 1.2.0 it is not a test
at all: it prints under `INFORMATIV (nu sunt teste)` with no verdict. It measures a
position, which is the wrong shape for this tool. The vertex is `-b/(2d)`, a
gradient-over-curvature ratio - not a fraction, and not scale-invariant. Measured, with
the same 11% gradient held fixed: 2.38% at 2 stops of vignetting, 9.49% at 0.5, 31.16% at
0.15, 57.40% at 0.08 - and then **0.00%** at 0.05, because the vertex leaves the frame and
the out-of-frame guard returns the sensor centre. So the metric was not merely
miscalibrated, it was **non-monotonic**: past that point the worse the defect, the cleaner
the report, which is a silent false negative on exactly the flattest lenses. The fixtures
never caught it because `FitsTestData.Radial` defaults to `k = 0.45`, i.e. 0.86 stops -
5.7x deeper than a real Samyang 135 at f/2.8.

`inclinare` is that same defect taken as an amplitude, which is what the scale wants, and
it still catches genuine decentring: a radial field centred off the sensor centre produces
a linear term of about `offset x curvature`, so the tilt weights the offset by what it
actually costs photometrically. On the real Samyang master that prompted the change (0.15
stops, an 11% twilight-sky gradient) the offset said 42.36% `PROBLEMA` and the mode said
`REFA`; the tilt says 12.36% `ACCEPTABIL`, in line with the other tests
(12.62 / 7.05 / 6.05 at the time). Pinned by `FitCenter_TiltIsInvariantToTheVignettingDepth`,
`FitCenter_WhenAFlatFieldMakesTheVertexEscape_TheTiltStillReadsTheGradient` and
`OnAFlatLensWithAGradient_TheTiltCarriesTheVerdict`.

`Stats.Severity` still spans both ladders (`OK`/`PERFECT` = 0, `BUN` = 1,
`SUSPECT`/`ACCEPTABIL` = 2, `PROBLEMA`/`REFA` = 3), and the exit code is derived across
all modes. Since the rewrite `--ratio` gives a **residual-scale** verdict, like structure,
because its verdict *is* a structure verdict; only `--light` still speaks
`OK`/`SUSPECT`/`PROBLEMA`. The `OK`/`SUSPECT`/`PROBLEMA` words survive in `--ratio` output
only as the informative label on the world-fixed component.

The `--ratio` mode has **no verdict thresholds of its own** since the rewrite: its verdict
is the structure verdict of the combined master, so it lives on this same scale. The
1%/3% pair survives only as a label on the world-fixed component, which is informative
and does not vote.

Since phase 8 every threshold lives in `Th` - `--light` no longer has 2.0/5.0 inline
and `--expo` no longer keeps its bands as locals. `Th.ExpoLo`/`Th.ExpoHi` used to be
dead *and wrong* (50/60 while the code used 50/65); they now hold the real values.
`Th.ExpoTarget` (57) and `Th.ExpoSkyTarget` (30) are the midpoints used for the
suggested-stops arithmetic.


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
written, 1 that it could not be. `--smooth` likewise: 0 means the smoothed flat and its
maps were written, 1 that the input was refused (raw, cube) or the file could not be;
with `--scale auto`, 2 means no file was written because there was nothing to smooth or
the scale was undetermined - "read this before you decide", the meaning 2 has elsewhere.
`--synthetic` follows the same shape: 0 written, 1 refused (a colour cube, or a focal
length, f-stop or crop factor nobody could supply), 2 the lens could not be chosen from
the text (none, or several - the candidates are printed). `--lenses` is always 0.
`--against` goes through `ExitFor` on the verdict of the *difference*.

Since the `--ratio` rewrite, mode 2 exits on the residual-scale band of the combined
master rather than on `OK`/`SUSPECT`/`PROBLEMA` - which changed real outcomes: the ISO800
pair in the flats library went from `OK`/0 to `ACCEPTABIL`/2.

