# Divide the harmonics out on every ring

Branch `claude/Divide-the-harmonics-out-on-every-ring`, worktree
`.claude/worktrees/Divide-the-harmonics-out-on-every-ring`, project folder `FlatCheck/` inside it.

## Context

The structure residual map of `E:\Photografie\Astro\online\RosetteNebula\masters\pp_flat_stacked.fit`
(Canon 40D, 17 flats, verdict GOOD 3.76 %) draws a hard circle on the inscribed radius: bright
left and right of it, dark above and below, the inside almost flat.

Measured on 2026-11-06 with a Python replica of `Metrics.RadialModel` (not the C#):

- The ring fit is right. The relative 2θ amplitude per r² is constant, 0.034-0.042e-6, from the
  centre to 1.36 of the inscribed radius; `q` is well estimated. The model's form is not the
  problem.
- The master's azimuthal profile mixes 2θ and 4θ: at the inscribed radius a2 = +1.43 % and
  a4 = -0.60 % of the level (the sides +0.9 %, the top and bottom -1.9 %, a broad hump and two
  narrow notches, not a sinusoid).
- On a full ring the code takes the plain pixel median. The median of `1.43 cos 2θ - 0.60 cos 4θ`
  is +0.60 % above its mean (analytic). On the full rings the profile therefore sits 0.2 to 0.6 %
  above the mean level, growing with r. On the partial rings the harmonics are divided out
  first and the median is the mean level. The profile drops 0.66 % on the first partial ring
  instead of the 0.17 / 0.22 % of its neighbours: that step is the circle.
- The Tokina sky master (pure 2θ, a4 ≈ 0) never showed it: a sinusoid's median is its mean, which
  is what `radial-model-even-term.md` section 2 assumes ("inside the inscribed circle ... no
  bias"). True for one harmonic, false for a mix.
- Experiment, the correction applied on every ring with the same `q`: the step becomes -0.24 %
  (continuous), the circle disappears, Rosette p95-p5 3.90 -> 3.78 %, worst 32 px tile
  3.74 -> 3.51 %. Five library masters move under 0.1 points (the Tokina sky step 0.00 -> -0.20,
  its neighbours' value). On synthetics (1200x800, model against the truth, worst pixel):
  Rosette amplitudes (a2 +4.6 %, a4 -1.9 % at the corner) 0.557 % -> 0.058 %; a pure 11 % saddle
  0.070 -> 0.070; a pure 3 % 4θ 0.019 -> 0.019.

The owner's decision on 2026-11-06, after the measurement: "Da, fă-o aici" (yes, do it here). A
formula change
(`Metrics.cs`): the rule "Formula changes re-validate" applies, and the parity goldens will move.

## The change

`FlatCheck.Core/Metrics.cs`, `RadialModel`: the profile is the median of
`v / (1 + r² Σ q·harmonic)` on every ring once `q` exists, not only on the rings beyond the
inscribed circle. The estimate of `q` (from `RingQFrom` = 0.75 of the inscribed radius outward,
whole and partial rings, one median) does not change. Near the centre `q·r²` is negligible, so
the inner profile is the plain median within noise. Nothing else in the model moves.

## Steps, in the project's order

1. **Requirement.** `docs/requirements.md` R-014: reword ("with the even 2θ + 4θ correction on
   every ring, one relative amplitude per r² estimated on the rings from 0.75 of the inscribed
   radius outward"), add the example (the Rosette figures: a 2θ + 4θ mix of +1.4 / -0.6 % at the
   inscribed radius leaves a model error under 0.1 % against the truth; 0.56 % with the median on
   the full rings), status `accepted` -> `implemented`.
2. **Fixture.** `FlatCheck.Tests/FitsTestData.cs`: `WithEvenHarmonics(b, w, h, a2, a4)` =
   `b · (1 + rn² (a2 cos 2θ + a4 cos 4θ))`, rn = 1 at the corner, both terms peaking on the x
   axis as on the Rosette master. Doc comment says what it reproduces.
3. **Failing test first.** `FlatCheck.Tests/MetricsTests.cs`:
   `R014_RadialModel_OnASaddleWithAFourfoldTerm_RecoversTheRadialPartOnTheFullRingsToo`: the
   2400x1600 scene with a2 = 0.046, a4 = -0.019, model against the truth (same pattern as
   `RadialModel_OnASaddle_RecoversTheRadialPartOutToTheCorner`), worst error < 0.2 %. Run it
   red (expected ~0.56 %), keep the before/after figures in its comment. Also, in
   `StructureAnalyzerTests`, the end-to-end guard
   `OnASaddleWithAFourfoldTerm_TheResidualHasNoRingsAndReadsTheInjectedStructure` with
   `WorstRingStep` < 0.5 % and `ResidualPtpPct` in the injected range; its measured values go in
   the comment (it may already pass before the fix: then it is a guard, and the Metrics test is
   the regression test).
4. **Code.** The condition `center < rIns ||` goes from the profile loop; the explanatory
   comment block above `RadialModel` ("Inside the inscribed circle the profile is the median per
   ring, as always") and its `<summary>` are rewritten to say the correction is on every ring
   and why (median ≠ mean of a 2θ + 4θ mix, measured 2026-11-06 on the Rosette master). The
   existing saddle, four-fold, gradient and blotch tests must stay green unchanged.
5. **Goldens.** `FLATCHECK_UPDATE_GOLDEN=1 dotnet test FlatCheck.sln --filter "GoldenOutputTests|EnglishOutputTests"`,
   then read the whole diff and tie every changed line to this change (fixtures with even
   non-radial content may move at the second decimal; a line that moves for another reason is
   a finding). Then `dotnet test --filter ParityGoldenTests` without the update flag: every
   changed line of a `parity_*.txt` is a finding by the rule "Golden files": list the lines and
   their figures to the owner and wait; recapture with `--filter ParityGoldenTests` only after
   the yes, and record the change in `docs/design/python-parity.md` under the divergences
   (a C#-only call, like the rest).
6. **Re-validate.** `/validate` from the worktree with `FLATCHECK_TESTDATA_LARGE` on the real
   masters; paste the drift table; re-record in `docs/validation.json` every figure that drifts
   past its tolerance (the structure cases V-001 to V-015 and the `against` cases that carry a
   residual), with a dated History line in `docs/validation.md` saying old, new and why.
   Expected from the replica: under 0.1 points on every master, most under 0.05.
7. **Rosette's figures.** The master is outside `Flats and Darks`, so it is not a validation
   case; its before/after figures (console output of the worktree's build, map before and after)
   go in the ADR and the design note. Recommendation: do not copy it into the masters folder;
   the owner can say otherwise.
8. **Design note.** `docs/design/radial-model-even-term.md`: section 2's claim corrected,
   section 4 (steps 1 and 4) says the correction is on every ring, a new section 8 "The fourth
   face: the median of a 2θ + 4θ mix on a full ring (2026-11-06)" with the Rosette measurement,
   the analytic check, the synthetic table and the library table; the Covers line gains the new
   tests; `docs/design/README.md` row if its one-liner names the partial rings.
9. **ADR 0091** `the-harmonic-correction-applies-on-every-ring` (check main's latest number at
   merge time): context (the Rosette master, the measurement), decision, alternatives rejected
   (a0 from the per-ring fit: design A of 2026-09-17, a blotch moves it; the plain median
   everywhere: the 2026-09-17 artefact; the azimuthal mean instead of the median: loses the
   robustness to compact things), consequences (goldens, validation drift, the parity
   recapture). ADR 0017's decision line "Full rings keep the pixel median" gets a
   "Superseded in part by 0091" note, the way ADR 0024 allows.
10. **CHANGELOG** under Fixed: the circle on the residual map of a master whose azimuthal
    profile mixes 2θ and 4θ (the Rosette master) - the harmonic correction now applies on every
    ring (R-014, ADR 0091). README: nothing describes the model's internals; check and leave.
11. **Gate and close.** `scripts/verify.ps1` green (build `--no-incremental -warnaserror` before
    the final run), a code-review subagent on the diff, then commit on the branch, one line,
    and propose the merge; the owner merges.

## Verification

- Step 3 red before step 4, green after; the four existing radial-model tests unchanged and
  green.
- The worktree's `flatcheck.exe` on the Rosette master with `-o`: the map has no circle (send
  the PNG); the console figures beside the current 3.76 / 3.72.
- The golden diff read line by line; the parity diff reported and waited on.
- The `/validate` drift table pasted, `ValidationTests` green after the re-record.
- `scripts/verify.ps1` green.

## Deviations, recorded at wrap-up

- Step 9 said ADR 0017's decision line gets a note; the kit's ADR 0024 puts a partial
  supersession on the Status line only, which is what was done (`; the plain median on the
  whole rings superseded by 0091`).
- Step 10 said the README describes nothing of the model's internals; it did (the sentence on
  the division "beyond the inscribed circle"), and `docs/design/pipeline.md` too. Both were
  updated after the code review.
- The synthetic figures are the C#'s 0.513 -> 0.039 % (the plan quoted the replica's
  0.557 -> 0.058 from a 1200x800 bench); the end-to-end guard passed its 0.5 % ring-step bound
  before the change too (0.463), so the model-against-truth test is the regression test.
- The validation drift was not all under 0.1 points: V-015's worst tile moved 3.931 -> 4.065
  and V-001's local spot flipped sign at a tie; both explained in `docs/validation.md`
  (History, 2026-11-06) and ADR 0091, re-recorded with the owner's yes of the same day.
- `R012_Structure_PrintsTheScaleAndNamesTheMostImportantMetric` quotes the clean fixture's
  residual line and was brought from 0.29 to 0.28 with the goldens.
- The code review (one round) found twelve points, all in documents and comments; the code
  was confirmed. The parity recapture and the validation re-record were put to the owner in
  one question and answered with one yes ("Da, ambele": yes, both).
