# 2026-10-20 Tell a light leak from the glow by the warming of the series

## Context

Check 2 reads the master's nine regions and says `scurgere de lumina sau glow` when an edge or
corner sits more than `Th.LeakAdu` (1 ADU-eq) above the centre; it cannot tell the two apart,
and it misses a leak that stays under that threshold. The owner's deliberate leak set of
2026-10-20 (`Flats and Darks/Darks/Lightleaks/darks`, 30 darks of 120 s at ISO 1600, eyepiece
open, a lamp moved three times) read `OK` (+0.48 ADU-eq top-left) although the last five frames
carry +1.9 ADU raw (0.47 ADU-eq) of light, 35 sd over the series' own scatter. The measurements
of 2026-10-19/20 on the three real series (Smida, the library, the leak set) show what separates
the three causes with figures the core already computes per frame:

- **Glow** is dark current: an edge's excess over the centre grows in proportion to the hot
  pixels' amplitude ratio (the warming of check 3). Library, left edge: slope 0.53 ± 0.04 on the
  ratio, intercept 0.11 ADU; top-left: slope 0.88, intercept 0.02.
- **A readout offset** is constant and sits in the biases too. Bottom edge: slope -0.16,
  intercept 0.46 ADU in the library and 0.38 at Smida; the Veil biases hold 0.58 there.
- **A leak** is constant or changes when the source changes, and is not in the biases. Leak
  set, frames 1-25: top edge intercept 1.00 ADU with -0.04 in the bias (the band of the raised
  mirror); the frame mean jumps +0.25 ADU at frames 20/22 and +1.9 ADU at 27-30 while the
  warming is smooth (1.05 → 1.13).
- The per-frame scatter of the frame mean around its fit on the warming: Smida 0.10 ADU-eq
  (20 frames, 2 ADU quantisation at sigma 1.5), library 0.017, leak set 0.023 (clean frames);
  the largest clean residual is 2.2 scales. An ordinary least-squares fit on the leak set is
  tilted by the lit frames (slope 4.3 instead of 1.4, scale 0.57 instead of 0.08); a Theil-Sen
  fit followed by clipping passes is not.

Outcome: check 2 names the frames that carry light over what the warming explains, splits the
verdict region's excess into the part that grows with the warming (glow) and the constant part,
takes the biases' share out of the constant part when there are biases, and chooses its words
from that; the per-frame table shows each frame's excess light. Nothing else changes.

## Decisions (ADR 0012, `proposed`; the approval of this plan is the owner's yes, then `accepted 2026-10-20`)

1. **The warming is the reference, not the time.** Every per-frame figure is fitted against the
   frame's hot-pixel amplitude ratio (`DarkFrameFigures.AmpRatioMedian`), because glow is
   proportional to the dark current by construction, so its residual is zero whatever the shape
   of the warming curve; against time the warming is not linear and would leave residuals of
   its own. Rejected: a trend in time.
2. **Theil-Sen with clipping passes.** Slope = median of the pairwise slopes (0 when fewer than
   two distinct ratios), intercept = median of y - slope x, scale = 1.4826 x the median absolute
   residual; up to three passes drop the frames whose residual exceeds the limit and refit on
   the rest; the final residuals of every frame are read against the last fit. Rejected: least
   squares (tilted by the lit frames, figures above); a per-frame residual against the master
   alone (the master is the median, so a leak in a third of the frames moves it).
3. **A frame is lit** when its residual exceeds `Th.LeakFrameSigmas` (5) x the scale and
   `Th.LeakFrameAdu` (0.1 ADU-eq). The floor keeps quantisation-level bumps on a quiet series
   out (the library's 5 x 0.017 = 0.085 would otherwise flag nothing-sized frames); on the leak
   set it keeps the lamp's 0.06 ADU-eq positions out and takes 0.17 (frame 26) and 0.45 ADU-eq
   (27-30) in; at Smida 5 x 0.10 = 0.5 ADU-eq rules. The fit needs `Th.LeakFitMinFrames` (6)
   frames with a ratio; under that the row reads `NEMASURAT`.
4. **The split of an edge.** For each of the eight regions, the per-frame offset to the centre
   (reference plane, raw ADU, the frames that are not lit) is fitted on the ratio: the glow part
   is the slope (the value at ratio 1, the master's temperature), the constant part the
   intercept; with biases, the bias mean map's offset of the same region is the readout part and
   what remains of the constant is the leak. The cause of the verdict region is the largest of
   the parts in absolute value: `Glow`, `Readout` or `Leak` with biases, `Glow` or `Constant`
   (leak or readout, undecidable) without. Rejected: a second exposure length (leak and glow
   both scale with it); a geometric test for the mirror's band (needs a model of the shadow; the
   map shows it to the eye, the lit-frame test catches a moving source).
5. **Check 2's verdict** goes `ATENTIE` on lit frames as well as on the master's offset; the
   words of the offset case follow the cause. Check 6's `glow` row shows the glow part, not the
   whole offset; its verdict logic does not change (R-040).

## Requirement drafts (step 1 of /new-feature; `draft` until the tests exist)

- **R-021 (amended).** Add to the verdict: `ATENTIE` also when R-028 names lit frames; the
  brighter case's words follow R-029's cause: glow `glow al senzorului (creste cu incalzirea):
  dark-urile raman bune`; leak `scurgere de lumina (constanta, absenta din bias): refa
  dark-urile`; readout `offset de citire (e si in bias), nu lumina`; constant without biases
  `nu creste cu incalzirea: scurgere de lumina sau offset de citire, fara bias-uri nu se pot
  deosebi`. The `OK` sentence ends with `; niciun cadru cu lumina peste incalzirea seriei`
  (or `; lumina pe cadre NEMASURATA`).
- **R-026 (amended).** `darkscheck-darks.csv` adds `edge_top_right`, `edge_bottom_left`,
  `edge_bottom_right` after `edge_top_left`, and `excess_light` (ADU-eq, empty when unmeasured).
- **R-028 (new).** Light per frame: over the decoded darks that have an amplitude ratio
  (R-023), at least `Th.LeakFitMinFrames` (6), the frame mean above black is fitted on the
  ratio by Theil-Sen (slope the median of pairwise slopes, intercept the median of
  y - slope x, scale 1.4826 x the median absolute residual), with up to three passes that
  leave out the frames over the limit; a frame whose final residual exceeds
  `Th.LeakFrameSigmas` (5) x the scale and `Th.LeakFrameAdu` (0.1 ADU-eq) is lit. Check 2 gets
  the row `lumina pe cadre` (`niciun cadru peste 5 x abaterea tipica (0.02 ADU-eq) si peste
  0.1 ADU-eq fata de dreapta mediei pe incalzire`, or the lit frames with their excess, or
  `NEMASURAT: sub 6 cadre cu pixeli fierbinti`), the verdict reads `ATENTIE: N cadre au lumina
  peste ce da incalzirea seriei (26: +0.20, 27: +0.49, ... ADU-eq); scoate-le din darks\ sau
  refa seria`, and `TABEL PE CADRE` gets the column `lumina` (the residual in ADU-eq, `-` when
  unmeasured). Example: eight synthetic darks warming 0.8 → 1.2 whose last two carry +0.6 ADU
  everywhere read lit frames 7 and 8 at +0.5 .. +0.7 and `ATENTIE`; the Lightleaks set reads
  frames 26 to 30 at +0.17 .. +0.49 ADU-eq; Smida and the library read none.
- **R-029 (new).** The split: for each of the eight regions, the per-frame offset to the centre
  on the reference plane (R-021's regions, raw ADU) over the frames R-028 does not name is
  fitted on the ratio; the glow part is the slope, the constant part the intercept, both in
  ADU-eq; with biases the bias mean map's offset of the region is the readout part and the
  constant minus it the remainder. The cause of the verdict region is the part largest in
  absolute value (`Glow`, `Readout`, `Leak`; without biases `Glow` or `Constant`). Check 2
  gets the row `glow / constant` (`la stanga-sus: glow +0.30 (creste cu incalzirea), constant
  +0.19, din care in bias -0.01, ramane +0.20 ADU-eq`; without biases `constant +0.19 ADU-eq
  (fara bias: lumina sau offset de citire)`; `NEMASURAT` under 6 frames) and check 6's `glow`
  row reads the glow part (`+0.30 ADU-eq la stanga-sus, partea care creste cu incalzirea
  (constant +0.19 in afara ei; dark - bias)`). Example: a left edge of 3 x the frame's hot
  factor reads glow 2.6 .. 3.4, constant within ±0.5, `Glow`; a constant +3 ADU left edge with
  flat biases reads constant 2.5 .. 3.5, remainder 2.5 .. 3.5, `Leak`; the same edge in the
  biases too reads readout 2.5 .. 3.5, remainder within ±0.5, `Readout`; the library's left
  edge reads glow 0.13 and constant 0.03 ADU-eq.
- **R-040 (amended).** The `glow` row is R-029's glow part (falls back to the offset when the
  split is unmeasured); the verdict keeps judging the offset.

## Steps

0. Worktree and branch (the kit's rule "Worktrees"):
   `git worktree add .claude/worktrees/Tell-a-light-leak-from-the-glow-by-the-warming -b claude/Tell-a-light-leak-from-the-glow-by-the-warming main`;
   everything below happens there; this plan is saved as
   `docs/plans/2026-10-20-tell-a-light-leak-from-the-glow-by-the-warming.md`.
1. `docs/requirements.md`: the drafts above (R-028, R-029 `draft`; R-021, R-026, R-040 amended).
2. Failing tests, `tests/DarksCheck.Tests/DarkSeriesTests.cs` (the `SessionFixture` /
   `SyntheticDark` knobs: `Hot` + `HotFactor` for the warming, `Extra` for an edge or a uniform
   offset, `biases:` with its own `Extra`):
   `R028_FramesWithLightOverTheWarmingAreNamedAndTheTableShowsEachFramesExcess`,
   `R029_AnEdgeIsSplitIntoGlowAndConstantAndTheBiasNamesTheReadoutOffset`; the existing R-021
   and R-026 tests extended (verdict words, CSV columns). `tests/DarksCheck.Wpf.Tests`: the
   Cadre table's new cell. Run them, paste the red output.
3. `docs/decisions/0012-a-light-leak-is-told-from-the-glow-by-the-warming-of-the-series.md`
   (`proposed` → `accepted 2026-10-20` on this plan's approval), with the figures of Context.
4. Core (`DarksCheck.Core`, nothing computed elsewhere):
   - `Analysis/DarkSeriesAnalyzer.cs`: `EdgeFigures` gets `TopRight`, `BottomLeft`,
     `BottomRight` (the `Regions` are already computed per frame at line ~332).
   - `Analysis/LeakAnalyzer.cs` (new): `TheilSen`, `FrameLight` (the fit, the passes, the lit
     frames, the per-frame excess in ADU-eq), `EdgeSplits` (the eight splits with the bias part),
     the `LeakCause` enum; pure functions over `DarkFrameFigures[]`, `Normalisation`, the bias
     `Regions`.
   - `Thresholds.cs`: `LeakFitMinFrames = 6`, `LeakFrameSigmas = 5`, `LeakFrameAdu = 0.1`,
     each with the measured figures in its summary.
   - `Analysis/DarkVerdicts.cs`: `LeakVerdict` gains `FrameLight? Frames`,
     `EdgeSplit? Split` (the verdict region's), `LeakCause Cause`; `Judge` calls `LeakAnalyzer`
     and goes `Warning` on lit frames; `PatternVerdict.GlowAdu` becomes the glow part when
     measured.
   - `Report/DarkReport.cs`: the two rows of check 2, the verdict's words, check 6's `glow`
     row, the `lumina` column (`DarkTableRow` in `Report/ReportRows.cs`, the header and the
     note). `Report/DarkOutputs.cs`: the four CSV columns.
5. Window: `FrameRowVm` and `FramesView.xaml` take the `lumina` cell (the rows of Rezumat and
   the Harti notes bind by label and need nothing).
6. `tests/DarksCheck.Tests/ValidationTests.cs`: optional keys `litFrames` (exact),
   `frameLightScale`, `glowPart`, `constantPart` (adu tolerance), `leakCause` (exact);
   `docs/validation.json`: V-008, kind `darks`, session
   `Flats and Darks/Darks/Lightleaks/darks`, `expected: null`; `docs/validation.md`: the row,
   unmeasured, and the set described (conditions, the three positions, no biases).
7. Gate green (`scripts/verify.ps1`); goldens regenerated (`darks.txt`, `lights.txt`,
   `lights-only.txt`: the two new rows of check 2, the `OK` sentence's tail, check 6's `glow`
   row, the table's header, note and column), every changed line tied to the change at
   wrap-up; `/validate` with the drift table pasted (V-003 to V-007 must not move; their new
   keys and V-008's figures filled from the output, dated in `validation.md`).
8. Record: statuses `implemented`, CHANGELOG (Added), `README.md` (check 2's rows and words,
   the table column, in Romanian), `docs/design/dark-analysis.md` (Covers adds
   `LeakAnalyzer.cs`; a paragraph on the fits and the split), then `/wrap-up`.

## Verification

- `dotnet test DarksCheck.sln --filter "FullyQualifiedName~R028|FullyQualifiedName~R029|FullyQualifiedName~R021|FullyQualifiedName~R026"` red at step 2, green at step 7.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1` green before the commit.
- `dotnet run --project src/DarksCheck.Cli -c Release -- "E:\Photografie\Astro\Flats and Darks\Darks\Lightleaks\darks"`:
  check 2 `ATENTIE` naming frames 26 to 30, `lumina pe cadre` and `glow / constant` rows,
  `lumina` column; the same on the library (`OK`, glow at the left/top-left, bottom constant)
  and on Smida (`OK`, no lit frame); pasted in the session.
- `/validate`: V-001 to V-007 unchanged within tolerance; V-008 measured.
- The window on the leak set: Rezumat's check 2 card shows the new rows, Cadre the column.

## Open point for the owner (not blocking)

V-008 runs without biases, so its `leakCause` is `Constant`/`Glow`, never `Leak`. If the Veil
biases (same ISO; `E:\Photografie\Astro\Veil\2026-10-06-3h\biases`, 30 frames) are copied by
the owner into `Lightleaks\biases\`, V-008 also pins the readout/leak split on real data (the
run of 2026-10-20 with them: top edge constant 1.00 ADU, bias -0.04; bottom 0.43, bias 0.58).
The tool never writes there, so this is the owner's move; the plan works either way.

## What deviated

- V-008 was measured in the same task (the leak set decodes in 22 s), so `validation.json`
  never carried `expected: null` and `ValidationTests` needed no guard for it; its figures
  came from the test's own mismatch messages, one correction (`masterSigma`).
- `FrameLight` carries `LitExcessAdu` beside `LitFrames`, so the report lists the lit frames
  without mapping file indices back to positions.
- The owner had renamed the library folder to its label (`...-15.8celsius-idx67`) after
  2026-10-16: the validation cases' paths follow it, their expected labels do not, and V-005,
  V-006 and V-007 now fail on the label alone (`-idx67-idx67`): a finding, not a figure
  change (`docs/validation.md`, History 2026-10-20).
- The golden `lights.txt` scenario has four darks, not six, so all three goldens read
  `NEMASURAT` on the two new rows; the measured rows are seen on the real sessions only.
- The owner copied the Veil biases into the leak set's folder right after the first commit, so
  V-008 runs on the session root with biases (the split's bias part is -0.01 ADU-eq top-left:
  the light there is not readout); its cause stays `Glow` for the top-left region, the light
  being in the lit frames. The owner also asked for the two other libraries he keeps (13.3 and
  9.5 °C, folders renamed on their labels) to be in the validation list: V-009 and V-010.

