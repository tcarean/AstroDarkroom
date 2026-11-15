# 0099. A patch lighter than the average sky is not dust, however dark its surroundings make it look

Date: 2026-11-15
Status: accepted 2026-11-15

## Context

The classes are relative (ADR 0026, ADR 0033, ADR 0085, ADR 0086): a patch is dust when it is
darker than what surrounds it, the band's local level or the modelled sky, by a multiple of the
sky's noise. On 2026-11-15, while the ranges of the dust and the sky were being designed, the
owner challenged the rule: where bright clouds surround something lighter than the sky, that
something is called dust, which is absurd for a figure meant to be the darkest part of the
print. `docs/design/known-limitations.md` already recorded the cause on the sky path (a
nebula lifts the model of its cell, and what lies beside it falls under the model).

### Measured on 2026-11-15

With a scratchpad probe over the core, on the 32 px patches, baryta at Dmax 2.4. "Lighter than
the sky" is over the median of the sky class. Three second conditions were tried, each kept
beside the present one: A, the patch is at or under the median of the sky class; B, at or under
the modelled sky at its own place; C, at or under the 25 % percentile of the sky class.

| image (path) | dust patches | of them lighter than the sky | left by A | dust L\* now -> A | sky L\* now -> A |
| --- | --- | --- | --- | --- | --- |
| `de_printat.png` (sky) | 1754 | 46.4 % | 941 | 4.4 -> 4.4 | 9.0 -> 9.1 |
| `starless_rbf0.5_stretch0.17.fit` (band) | 2252 | 13.8 % | 1941 | 8.1 -> 7.7 | 17.6 -> 17.6 |
| `starless_rbf0.5_stretch0.1.fit` (band) | 2210 | 16.2 % | 1852 | 3.7 -> 3.5 | 8.9 -> 8.9 |
| `Andromeda_starless_WithWCS.fit` (sky) | 249 | 68.7 % | 78 | 1.5 -> 1.5 | 2.9 -> 3.0 |
| `Orion_with_WCS.fit` (sky) | 158 | 61.4 % | 61 | 10.9 -> 10.9 | 13.9 -> 13.9 |
| `Rosetta_starless_no_WCS.fit` (sky) | 554 | 81.0 % | 105 | 6.1 -> 6.2 | 8.1 -> 8.3 |
| `Veil_Starless.fit` (sky) | 167 | 0 % | 167 | 6.4 -> 6.4 | 9.3 -> 9.3 |
| the Smida frame of ADR 0085, with its WCS (band) | 2158 | 17.7 % | 1775 | 5.0 -> 4.5 | 12.0 -> 12.0 |
| the same frame as a TIFF, no WCS (sky) | 2149 | 24.4 % | 1625 | 4.9 -> 4.3 | 12.1 -> 12.2 |

The last two columns are the probe's, which sent every patch A removes to the sky class. With
the destinations of the Decision, Rosetta reads 6.1 and 8.2 instead of 6.2 and 8.3 (the code's
figures, recorded as V-010 in `docs/validation.md`); `de_printat.png`, Andromeda and Orion, measured both ways,
differ by under 0.05 L\*.

- B changes nothing on the sky path (the model there is the level the nebula lifted, so every
  dust patch is under it by definition) and leaves 4 to 11 % of the class lighter than the sky
  on the band path. C leaves 14 patches on Andromeda and 38 on Rosetta.
- With A the middle half of the dust class moves further from the middle half of the sky
  class on the frames whose class decides (the reference FITS: 7.7 to 12.9 against 15.4 to
  19.0; it was 8.1 to 14.3).
- On the Milky Way frames the dust's figure, the class's 25 % percentile, goes down by 0.2 to
  0.6 L\*: the light patches leave the class, so its dark quarter sits lower. On the Smida
  frame the class still decides on both paths (8.1 % and 7.4 % of the patches) and the two
  paths stay 0.2 L\* apart.
- On the sky path nearly half of the patches A removes are far over the sky: 339 of 813 on
  `de_printat.png` and 243 of 449 on Rosetta are over the sky's median by more than the noise
  threshold, some by 14 to 17 L\*. Sending all of them to the sky class moves Rosetta's sky
  from 8.14 to 8.25 and the class's 90 % percentile from 14.5 to 15.4; sending those to the
  nebula class and the rest to the sky gives 8.19 and 14.3. On the band path every removed
  patch was inside the band (311 on the reference FITS, none outside).

## Decision

The owner chose rule A on 2026-11-15, and accepted its draft the same day: a dust patch stays
dust only when it is also at or under the median of the patches classed sky, on the image as it
is. A patch that fails it takes the class it would otherwise have: on the band path, band
inside the band and sky outside it; on the sky path, nebula when over that median by more than
the noise threshold (`SkyNoiseK` sigmas), sky otherwise. With no patch classed sky the
condition is not applied. The texts call that median "the average sky" (`cerul mediu`), the
owner's word for it.

The condition is still relative: a rank inside the image's own sky class, no constant in L\*
and no new threshold.

## Alternatives rejected

- **B, at or under the modelled sky at the patch's place.** Removes nothing on the sky path,
  where the complaint came from (Rosetta keeps 81 % of its dust class lighter than the sky).
- **C, at or under the sky's 25 % percentile.** Leaves 14 dust patches on Andromeda and 38 on
  Rosetta, and takes real Rift patches on the Milky Way frames (the Smida frame: 468 against
  383 with A) for no gain in the verdict.
- **Every removed patch back to the sky class.** One sentence shorter, but it puts patches
  14 to 17 L\* over the sky into the class the sky's figure, its colour and the coming ranges
  are measured on.
- **Every removed patch to the nebula class on the sky path, the sky class untouched.** Put to
  the owner on 2026-11-15, when the validation run showed the corner flag below; the owner
  kept the rule as it is. It would leave the sky's figure, its colour and the corner flags
  exactly as before the rule, and paint as nebula the removed patches that sit at the sky's
  level (206 of 449 on Rosetta, 474 of 813 on `de_printat.png`), moving the curves' nebula
  anchor further down (not measured).
- **Leave the classes and fix only what the window shows (the ranges).** The map and the dust's
  share of the frame would still call the nebula's edge dust.

## Consequences

- `DustAnalyzer` applies the condition after the classes of either path; R-044 states it with
  the re-measured examples. The dust definition of the report and of the map's legend, and the
  Tonuri guide, say so in both languages.
- The verdict is stricter on Milky Way frames: the dust's L\* is lower by a few tenths.
  Measured with the code on 2026-11-15: on the reference FITS (V-004, Dmax 2.0) the dust is
  7.67 against 8.11, the recommended row stays 0.19 (its dust 9.33 against 9.82, paper black
  9.0), the exact target moves from 0.184 to 0.186 and the Dmax from which the dust as it is
  stays above black from 2.05 to 2.07. The sky-path cases whose class lost patches move in
  their class shares and the sky by 0.05 to 0.06 L\*; no recommended row moves.
- The sky class gains the removed patches that sit near the sky, and the nebula class the
  rest, so the figures measured on those classes move a little: the sky's colour by 0.1 to
  0.2 levels (`de_printat.png`: red minus blue -2.12 to -2.00, chroma 0.98 to 0.95; Rosetta:
  3.01 to 3.20, 1.40 to 1.48), and the band or nebula anchor of the curves down by 0.5 to 1.2
  L\* (`de_printat.png` 25.3 to 24.8, Rosetta 34.8 to 33.6).
- One colour flag flips on the validation images: Rosetta's top-left corner is now reported as
  cooler than the rest of the sky. The corner's red did not move (23.84 levels); the sky's rose
  from 26.71 to 26.88 with the 206 patches it took, so the corner's red is 3.04 levels under it
  against 2.87, with the limit at 3.0. The section's verdict was ATENTIE before and stays so.
  The owner accepted it on 2026-11-15: the corner sat on the limit already.
- The 5 % floor (`DustClassMinFraction`) counts the class after the condition. On
  `de_printat.png` the class goes from 7.8 % to 4.2 % of the patches, so the reason the report
  gives for taking the darkest sky changes, from the class's dark quarter being lighter than
  the darkest sky to too little dust; the figure, 4.4, is the same. The reference FITS keeps
  8.9 % and the Smida frame 8.1 % and 7.4 %, so their class still decides.
- On the sky path a removed patch far over the average sky is nebula, never bright nebula,
  whatever its height: it is under the level of its own surroundings. On Rosetta the 449
  removed patches have a 90 % percentile 12.5 L\* over the sky's median and a maximum of 16.8,
  with the bright nebula starting 13.7 over the modelled sky: fewer than a tenth of them.
- A dark cloud that is darker than its surroundings and lighter than the image's average sky
  (a lane on the bright side of a strong gradient) is no longer dust; it could never have been
  the darkest part of the print.
- The sky-level patches beside a bright object that fall under the lifted model and under the
  sky's median stay dust (Rosetta keeps 105): the condition narrows that known limit, it does
  not remove it.
- Guarded by `R044_APatchLighterThanTheSkyIsNotDustHoweverBrightItsSurroundings`,
  `R044_ALaneOfTheBandLighterThanTheSkyIsBandNotDust` and
  `R044_TheDustDefinitionSaysNoLighterThanTheAverageSky`.
