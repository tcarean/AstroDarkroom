# Plan: ring artefacts in the radial model, and the "vignetting edge" metric

Read `CLAUDE.md` first. Nothing is computed outside `FlatCheck.Core`, every formula is
mirrored in `flatcheck.py` and cross-checked, no version bump, no commit, GPL header on
every new file, user-facing text in Romanian without diacritics.

This plan has three parts. **Part A is a correctness fix and comes first.** Part B corrects
documentation that Part A makes wrong. Part C is the metric that was originally asked for
("taisul vignetarii"); it is kept because it was asked for, but the evidence gathered while
designing it says it is *not* justified by the data that motivated it - read A before
deciding whether to build C.

## What was found, in order

The ISO800 master of the Nikkor 35 DX showed concentric rings in its residual map. They
were read as a decentred, hard-edged light falloff, and a metric was prototyped to detect
hard edges from the radial profile. Measured on the real masters, that metric gave 0.03 %
on ISO400 and 0.98 % on ISO800 - a clean 30x separation - which looked like confirmation.

It was not. The same metric on a **synthetic frame with no edge at all** - smooth
vignetting times an 11 % linear gradient - gave 3.05 %. And the same metric computed from
the ring **mean** instead of the ring **median** gave 0.05 % on ISO800, the same as ISO400.
The "steps" in the ISO800 profile were produced by the median, not by the flat.

The mechanism, verified on synthetics: a linear gradient makes every ring's pixel
distribution bimodal (the left arc dark, the right arc bright). Inside the inscribed
circle the two arcs are mirror images, so the median lands exactly on 1 and nothing
happens. **Beyond the inscribed circle the ring is cut into partial arcs**, the two modes
are no longer balanced pixel for pixel (the half-pixel centring of known issue 5 is enough),
and the median falls into the sparse gap between the modes - so it jumps from one bin to
the next by an amount that scales with the gradient. The interpolated model inherits the
jumps, and `img / model` shows them as **concentric rings at the periphery**, centred on
the sensor, plus a hard circular boundary at the inscribed radius. The real tool draws
exactly this on the synthetic (`scratchpad/rings/gradient11-tool.png` during the session).

Consequences:

- The rings in the ISO800 map are an artefact of `Metrics.RadialModel`. The gradient
  itself is real (present in every frame, does not flip under rotation, changed each time
  the camera was handled); the earlier reading of the rings as "decentred hard-edged
  falloff / directional light / obstruction" is withdrawn. The "70 % radius differs left
  vs right" figure was the same gradient seen through a different window, not decentring.
- The four verdict numbers are **not** affected: `LargeScalePtp` is restricted to the
  inscribed radius, where the median is exact. Measured with the fix below: residual ptp
  3.374 → 3.325 (ISO400), 4.006 → 3.980 (ISO800), 12.059 → 12.068 (synthetic gradient).
  Only the **map** lies, and the map guide written last session repeats the lie in its
  "cercuri concentrice" row.
- The edge metric has no real positive example. Its one apparent positive was the
  artefact.

## Part A - build the radial profile from the 180°-symmetrised raster

### A.1 The change

In `StructureAnalyzer.AnalyzePrepared`, replace

```csharp
var (_, _, model) = Metrics.RadialModel(img, cx, cy);
var resid = Img.Divide(img, model);
```

with

```csharp
var sym = Img.Symmetrise(img);                       // (img + rot180(img)) / 2
var (_, prof, model) = Metrics.RadialModel(sym, cx, cy);
var resid = Img.Divide(img, model);                  // the residual is still of img
```

and add to `Img.cs`:

```csharp
/// <summary>
/// Media dintre raster si rotitul lui cu 180 de grade. Anuleaza exact partea impara
/// (orice gradient liniar) si pastreaza neatinsa partea para - deci si orice profil
/// radial. Un profil radial NU poate fi impar, asa ca nu se pierde nimic din ce
/// modelul are voie sa contina, si se scoate exact ce il facea sa sara.
/// </summary>
internal static Img Symmetrise(Img a)
```

`Rotate180` exists (attribution mode). Note the half-pixel detail and leave it alone:
`Rotate180` mirrors about `((H-1)/2, (W-1)/2)` while the rings are about `(H/2, W/2)`.
That is known issue 5's territory, it is identical in both implementations, and it does
not matter here - the symmetrised raster is only the *source of the profile*.

Why symmetrise rather than switch the ring statistic to the mean: the mean also cures the
bimodality (measured, same numbers), but it gives up the median's robustness to stars,
hot pixels and dust on single frames, which the analyzer accepts as input. Symmetrising
keeps the median and removes only what cannot be radial. Cost: one extra raster at
analysis resolution (~12 MB, transient).

Where the change reaches: structure mode, and the attribution mode's verdict (which is
`AnalyzePrepared` on the combined master). `AzimuthalAsymmetry` uses per-sector medians,
which are not bimodal; leave it. The `--light` map is not a radial-model residual; leave it.

Update the `RadialModel` comment in `Metrics.cs` and the `RadialModel` paragraph in
`CLAUDE.md` ("Shared pipeline and algorithms"): the profile is taken from the symmetrised
raster, and why. Add the mechanism above to "Known issues and gotchas" as a *fixed* note,
next to issue 5, because the two share the half-pixel cause and someone will meet it again.

### A.2 Python mirror (mandatory - every structure and ratio scenario compares this)

In `flatcheck.py`, `_structure_body` (and any other caller of `radial_model` feeding a
residual): `sym = (img + img[::-1, ::-1]) / 2` and `radial_model(sym, cx, cy)`; the
residual stays `img / model`. `img[::-1, ::-1]` is exactly `Rotate180`.

### A.3 Golden files and cross-check

`FLATCHECK_UPDATE_GOLDEN=1 dotnet test FlatCheck.sln`, then **read the diff**:
`structure_clean` must not move at all (a symmetric field is its own symmetrisation);
`structure_gradient`, `structure_reshoot` and the `ratio_*` files may move in the residual
and verdict lines by fractions of a point, never across a band. Any bigger move means the
residual was computed from `sym` instead of `img` - the map would then hide the very
gradient the test measures.

Then `FLATCHECK_DUMP_FIXTURES=... dotnet test --filter DumpsFixtures` and
`python crosscheck.py` - must end with `DIFERENTE NUMERICE : 0` as today.

### A.4 Tests

`MetricsTests`:

- `RadialModel_OnAPureGradient_TheProfileDoesNotJump` - the regression test for the
  artefact. Build `FitsTestData.WithLinearGradient(Radial(512,512), 512, 0.12)`, prepare,
  symmetrise, take `prof` from `RadialModel`, and assert the max relative deviation from
  its 9-point Savitzky-Golay smoothing (coefficients `{-21,14,39,54,59,54,39,14,-21}/231`)
  is below 0.2 %. Assert the same construction **without** symmetrising exceeds 0.5 %, so
  the test documents what it protects against and fails if someone "simplifies" the
  symmetrisation away. (Measured on the 512 fixture: 0.642 % before, 0.181 % after; the
  fixture's own discretisation floor is ~0.17 %, which is why the bound is 0.2, not 0.05.)
- `Symmetrise_RemovesAGradientAndKeepsARadialField` - on a radial field `Symmetrise`
  returns the same values (to 1e-12); on a radial field with a gradient it returns the
  radial field.

`StructureAnalyzerTests`:

- `OnAPureGradient_TheResidualMapHasNoRingsBeyondTheInscribedRadius` - after
  `Analyze`, take the residual, and for each of ~20 radii between `rIns` and the corner
  compute the mean of the residual over the ring: the sequence must be smooth (same
  SG-residual bound as above). Before the fix the ring means step.
- `TheVerdictNumbersMoveByLessThanATenthOfAPoint` is **not** a test to write - the golden
  files already pin the numbers; a tolerance test would only duplicate them loosely.

`RatioAnalyzerTests` and the WPF suites need no change; run them.

## Part B - correct the map guide (window + README)

The row `cercuri sau arce concentrice, descentrate` in `SessionMessages.MapGuideShapes`
and the same row in README's "Ce înseamnă formele" table currently say rings mean a
decentred round obstruction. After Part A, rings centred on the sensor cannot appear at
all, and rings centred elsewhere have **no real example** in the library. Rewrite the row
to what is actually known:

| Shape | Meaning |
|---|---|
| `cercuri sau arce concentrice` | Un inel centrat pe senzor nu poate ramane in harta - modelul radial il absoarbe. Daca vezi unul, e centrat in alta parte: ceva rotund din fata obiectivului e in afara axei, sau lumina nu e difuza. Nu s-a vazut inca pe un master real; inelele vazute pana la 2.0 pe mastere cu gradient mare erau un artefact al modelului radial, corectat de atunci. |

Keep the six-row structure (`MapGuideTests.TheGuideNamesEveryShapeItExplains` checks
`cercuri`). Also fix the dipole row's parenthesis if it references rings.

In `CLAUDE.md`, the "What the window adds" paragraph mentions the guide; add one sentence
that the ring row was rewritten after the artefact was found, so nobody restores the old
wording from git history.

## Part C - the edge metric ("taisul vignetarii"), optional

**Recommendation: do not build this now.** Reasons, all measured:

1. Its motivating observation was the artefact fixed in Part A. With the profile taken
   from the symmetrised raster, the two real masters give 0.06 % (ISO400) and 0.06 %
   (ISO800). There is no real master in the library on which the metric reads anything.
2. Its dynamic range is narrow. On synthetic 1504x1004 rasters: smooth 0.02, cos⁴ 0.01,
   2 % per-pixel noise 0.04, a **soft** 10 % knee over 20 % of the radius 0.24, a 1 % hard
   step 0.38, a 3 % hard step 1.16, a 3 % hard step decentred by 3.5 % 0.53. A threshold
   would sit between 0.24 and 0.38 - a 1.6x margin between "lens with a gradual knee" and
   "1 % hard edge", on synthetics only.
3. Small rasters have a discretisation floor above that margin: the 64x64 WPF fixture
   gives 0.80 %, 128x128 0.31 %, 256x256 0.15 %, 512x512 0.17 %. The window's test fixture
   would read "margini dure" on a perfect field unless the metric refuses small rasters.

If it is built anyway, this is the definition that survived the measurements:

- Input: `prof` from `RadialModel(sym, ...)` - Part A already computes it, so the metric
  costs nothing extra. Never the unsymmetrised profile (that is the artefact).
- `Metrics.EdgeSharpness(double[] prof, int[] binPixels)` → max over interior bins of
  `|prof − SG9(prof)| / SG9(prof) × 100`, SG9 = Savitzky-Golay window 9 degree 2 with the
  exact rational coefficients `{-21,14,39,54,59,54,39,14,-21}/231` in both C# and Python
  (bit-identical, no fitting). 'Valid' convolution only: bins 4..n−5. Returns `NaN` when
  fewer than 9 bins remain **or any used bin has fewer than 200 pixels** - the guard that
  keeps small fixtures at `nedeterminat` instead of a false positive.
- `Th.EdgeHard = 0.30` (percent), commented as uncalibrated with the table above and the
  fact that no real positive exists.
- Report: one line under `INFORMATIV (nu sunt teste)`, after the vignetting depth, in
  `StructureReport.WriteBody` (so the ratio mode gets it too):
  `taisul vignetarii       :  0.06 %   (profil neted - lumina difuza)` or
  `... (margini dure - lumina directionala sau obstacol cu umbra neta)`, or
  `nedeterminat (raster prea mic)`. Informative, no verdict, no exit-code effect.
- `StructureResult.EdgeSharpPct`; `StructureTabViewModel.Populate` adds a `MetricRow` to
  `Informative` with the same note as the console (no XAML change - the collection renders).
  The Ratio tab shows no informative rows today; keep it so.
- Python mirror in `_structure_body`; goldens re-approved; a new cross-check scenario
  `structure hard edge` (radial × step 0.97 beyond r > 0.85) in `CrossCheckFixtures` and
  `crosscheck.py`, because none of the existing 39 has an edge.
- Tests: `EdgeSharpness_IsScaleInvariant`, `..._IsBelowTheHintOnASmoothField` (512
  fixture), `..._IsBelowTheHintOnASoftKnee`, `..._ExceedsTheHintOnAHardStep`,
  `..._IsNaNOnATinyRaster` (64x64), and a report test for the three wordings.

## Order of work

1. Part A, with its tests, golden re-approval **read line by line**, and cross-check.
2. Part B.
3. Decide on Part C with the user; if built, in the order listed there.

## Commit message for A + B (adjust to what was done)

```
modelul radial din rasterul simetrizat la 180 de grade

Pe un master cu gradient mare, harta reziduala arata inele concentrice
la periferie si o margine dura pe cercul inscris. Nu erau in flat: le
facea modelul. Dincolo de cercul inscris inelele sunt arce partiale, un
gradient le face distributia bimodala, si mediana cade in golul dintre
moduri - deci sare de la un inel la altul cu o amplitudine data de
gradient, iar img/model mosteneste salturile ca inele. Reprodus pe un
raster sintetic fara nicio margine: vignetare neteda ori un gradient
liniar de 11% da singur inelele.

Profilul se ia acum din (img + rot180(img))/2. Partea impara - orice
gradient - se anuleaza exact, partea para ramane, iar un profil radial
nu poate fi impar, deci modelul nu pierde nimic din ce are voie sa
contina. Reziduul ramane img/model, ca gradientul sa ramana vizibil.
Cifrele de verdict nu se misca dincolo de zecimala a doua: LargeScalePtp
e restrans la cercul inscris, unde mediana era deja exacta. Mediana
ramane in locul mediei ca sa nu se piarda robustetea la stele si praf.

Randul "cercuri concentrice" din ghidul hartii spunea ca inelele
inseamna un obstacol rotund descentrat; era citirea artefactului.
Rescris, in fereastra si in README.
```
