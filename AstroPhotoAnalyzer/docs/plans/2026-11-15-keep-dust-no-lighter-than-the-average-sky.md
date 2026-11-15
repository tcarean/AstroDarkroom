# Keep dust no lighter than the average sky

## Context

The dust class is relative: a 32 px patch is dust when it is darker than what surrounds it (the
band's local level or the modelled sky) by a multiple of the sky's noise. On 2026-11-15 the
owner challenged it: beside a bright object, patches lighter than the sky are called dust.
Measured the same day on nine real frames (ADR 0099, already written on the branch): 14 to 81 %
of the dust class is lighter than the sky on every frame but the Veil. The owner chose the fix
and accepted its requirement draft and its two texts, with the word "average / mediu" for the
sky's median:

> A dust patch stays dust only when it is also at or under the median of the patches classed
> sky. One that fails takes the class it would otherwise have: band inside the band, sky
> outside it; on the sky path nebula when over that median by more than the noise threshold,
> sky otherwise. With no patch classed sky the condition is not applied.

Branch `claude/Keep-dust-no-lighter-than-the-typical-sky`, worktree
`.claude/worktrees/Keep-dust-no-lighter-than-the-typical-sky`. Already there, uncommitted: the
draft sentence in R-044, ADR 0099, and the failing tests (5 fail for the right reason:
`R044_APatchLighterThanTheSkyIsNotDustHoweverBrightItsSurroundings`,
`R044_ALaneOfTheBandLighterThanTheSkyIsBandNotDust`,
`R044_TheDustDefinitionSaysNoLighterThanTheAverageSky`, the changed ending in
`R044_WithABandTheDustAndTheSkyAreTheClassesAndTheCloudsHaveRows`, the guide sentence in
`ViewModelTests`). The ranges feature waits on its own branch.

## Steps

1. **Core, the rule** — `AstroPhotoAnalyzer.Core/Analysis/DustAnalyzer.cs`
   - New `internal static void KeepDustNoLighterThanTheSky(DustMap map, float[] L, bool[] band)`
     with its XML comment: the median of the patches classed `Sky`
     (`Stats.PercentileSorted(Stats.Sorted(...), 50)`, the statistic `Measure` reports); return
     when none is sky; every `Dust` patch over it becomes `Band`/`Sky` by `band[i]` on the band
     path (`band` not null), or `Band` when over the median by more than `map.NoiseThreshold`
     and `Sky` otherwise on the sky path (`band` null).
   - Called as the last line of `Classify` and of `ClassifyBySky`, after the thresholds are on
     the map. No new constant in `Thresholds.cs`.
   - The file's header comment and the two methods' summaries say so.
   - One more test with the code: a hand-built map with dust and band patches and no sky is
     left unchanged (`R044_WithoutASkyPatchTheSecondConditionIsNotApplied`).
2. **Core, the words** — `AstroPhotoAnalyzer.Core/Analysis/PrintStretchSimulator.cs`,
   `DustClassDefinition` (the report, Anexa B and the map's legend read it):
   - band: `..., dar nu mai deschise decat cerul mediu` / `..., but no lighter than the average sky`
   - sky: `dalele cu 3.2 L* mai intunecate decat cerul din jurul lor (de 3 ori zgomotul lui), dar
     nu mai deschise decat cerul mediu` / `the patches 3.2 L* darker than the sky around them
     (3 times its noise), but no lighter than the average sky`
3. **Window, the guide** — `AstroPhotoAnalyzer.Wpf/ViewModels/ResultTabs.cs`, paragraph
   "Praful si cerul.", one sentence before "Pe orice cale, L\*-ul cerului e...":
   - RO: `Pe orice cale, o dala mai deschisa decat cerul mediu nu e praf, oricat de luminos ar fi
     ce are in jur: marginea stearsa a unei nebuloase sau o fasie din banda mai luminoasa decat
     cerul raman nebuloasa, banda sau cer.`
   - EN: `On either path, a patch lighter than the average sky is not dust, however bright its
     surroundings: the faint edge of a nebula, or a lane of the band lighter than the sky, stays
     nebula, band or sky.`
4. **Tests green** — the five failing tests pass; the rest of the suite stays green. A test that
   fails only because a synthetic scene's classes moved is read, not edited: if its assertion is
   about the old rule, I stop and say so.
5. **Golden files** — `APA_UPDATE_GOLDEN=1`, then the whole diff read: the definition line (8
   of 16 goldens) and, on the scenes whose classes move (`dark-sky` at least), the class shares
   and the dust and sky figures that follow. Every changed line tied to the rule and reported;
   a line the rule cannot explain stops the task.
6. **`/validate`** on the reference images, the drift table pasted. Expected to move: the class
   shares everywhere but the Veil; the dust on V-004 and V-008 (8.1 to 7.7, 3.7 to 3.5) with the
   exact target and perhaps the recommended row; the sky by about 0.1 L\* on the sky-path cases;
   the curve anchors and the sky's colour by what the run shows. **The drift table goes to the
   owner; `docs/validation.json` and `validation.md` are updated only on their yes.**
7. **Documents**
   - `docs/requirements.md`: R-044's sentence without "draft", its examples and R-043's
     re-measured from the real output.
   - `docs/design/measurements.md` (B5), `docs/design/known-limitations.md` (the halo bullet's
     figures re-measured; two new limits: sky-level patches under the lifted model and under the
     median stay dust; a cloud lighter than the average sky on the bright side of a gradient is
     not dust), ADR 0099's consequences completed with the measured drift.
   - `README.md`, "how it decides" section, one sentence after the two paths: `On either path a
     patch lighter than the average sky is never dust, however bright its surroundings: the
     faint edge of a nebula, or a lane of the band lighter than the sky, stays nebula, band or
     sky.`
   - `CHANGELOG.md`, `[Unreleased]`, Changed.
8. **Wrap-up** — `scripts/verify.ps1` green (after `--no-incremental -warnaserror`), the map
   window's render checked for the longer legend line (`APA_DUMP_SCREENS`), the diff read back,
   commits on the branch, a code review offered, then the merge proposed. The plan is saved as
   `docs/plans/2026-11-15-keep-dust-no-lighter-than-the-average-sky.md`.

## What may come back to the owner

- The drift table (step 6): how far the recommended background moves on the Milky Way frames.
- Any golden line the rule does not explain (step 5).
- Whether the change warrants a version bump (the verdict's figures change).

## Verification

- `dotnet test AstroPhotoAnalyzer.sln -c Release --filter "FullyQualifiedName~R044"` green.
- The gate: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`.
- `dotnet test AstroPhotoAnalyzer.sln -c Release --filter "Category=Validation"` and the
  `MEASURED` lines against `docs/validation.json`.
- The window on `Rosetta_starless_no_WCS.fit`: the Dust / sky map no longer paints the
  nebula's edge as dust (a render through `APA_DUMP_SCREENS`, or the app itself).
