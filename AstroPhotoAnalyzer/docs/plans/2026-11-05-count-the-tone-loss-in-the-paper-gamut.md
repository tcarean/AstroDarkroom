# Count the tone loss in the paper's gamut warning

Date: 2026-11-05. Branch `claude/Count-the-tone-loss-in-the-paper-gamut`, worktree
`.claude/worktrees/Count-the-tone-loss-in-the-paper-gamut` (from `main`, the kit's rule "Worktrees").

## Context

On 2026-11-03 the owner compared the Soft proof tab's gamut warning with Lightroom Classic's
(relative intent, black point compensation, simulate paper) on the Smida Milky Way TIFF
(`E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida\Processed_1mmPTFE_Flats\finale\finalisti\starless_denoised_syqon_studio_in_max_0.75_stretch0.12-logd4_Gentle_sat0.25.tif`,
sRGB) through the WhiteWall profile
(`E:\Photografie\Print and ICC profiles\Whitewall\WhiteWall_ICC_Inkjet_Hahnemuehle_Photo_Rag_Baryta.icc`,
v4.3, paper black L* 3.38). Lightroom marks 0.93 % of the image, the tool 0.45 %, and only
0.24 % is common. Measured with a scratchpad probe over a copy of the core, the Lightroom
screenshot registered on the reduced image (scale 0.560):

- The tool's rule (R-144, ADR 0032) counts the chroma only: a*, b* moved by more than 3 after
  the relative round trip, no black point compensation (BPC). Lightroom's extra pixels are the
  darkest coloured dust (L* median 6.4, p10 3.6, p90 12.5; chroma 11): their a*, b* move by 2,
  but the paper prints them 4 to 6 L* lighter. The tool's extra pixels (L* 13 to 19, chroma 30
  to 37) move by 3.0 to 3.2 without BPC and by about 1.1 with it, which is why Lightroom, set
  on BPC, leaves them. The two warnings are two halves of one loss: a dark saturated brown the
  paper cannot make, replaced by a lighter, duller one.
- The profile's own `gamt` tag is not what Lightroom uses (36 % marked, IoU 0.03).
- The best match: the whole colour difference (Delta E76, L* included) through the relative
  round trip with BPC towards the paper and back, over 4: 1.33 % marked, 71.5 % of Lightroom's
  pixels hit, 50 % of the rule's pixels in Lightroom's, IoU 0.417 (3.0: IoU 0.260, 3.1 %;
  3.5: 0.353; 4.5: 0.415, 0.89 %; 5.0: 0.359). Without BPC, Delta E76 over 6 is as good (IoU
  0.423) but counts the paper-black clip the Tonuri tab already counts. Lightroom's exact rule
  is not published; no rule matched it exactly.

The owner chose, on 2026-11-05, to change the rule to the whole difference with BPC over 4, in
the Soft proof warning and in the Editare Curbe figure, which stay one rule (R-161).

## The change

1. **`Thresholds.cs`**: `CurveGamutDeltaAb` (3) becomes `CurveGamutDeltaE` (4), its comment the
   provenance above (the 2026-11-03 measurement; Lightroom as the reference). `CurveGamutGrid`
   (33) and `CurveGamutExactMargin` (1.0) stay; the margin's comment gets the re-measurement
   below.
2. **`Analysis/GamutAnalyzer.cs`**: the round trip becomes sRGB -> XYZ D50 -> BPC towards the
   paper (`XYZ x (W - K) / W + K`, K the device black through A2B1 as the neutral of its Y, the
   same K `ProofProfile` uses) -> B2A1 -> A2B1 -> inverse BPC (`(XYZ - K) x W / (W - K)`) ->
   Lab; the displacement is `(dL, da, db)`. `GamutTable` holds `Dl`, `Da`, `Db`; `Exact` returns
   the three; `Interpolated` returns the three; `Outside` compares the norm of the three with
   `CurveGamutDeltaE`, the hybrid of ADR 0042 unchanged. `Build` gets the paper black the way
   `ProofProfile.Open` does: lift that computation (the device black through A2B1, neutral of
   its Y) into one internal static helper in `SoftProof.cs` that both call, and the two
   scalings into two one-line helpers beside it, so the proof's compensation and the figure's
   cannot drift apart. The file's head comment is rewritten for the new rule.
3. **Texts** (the rule "Plain words": original and proposal, edited on the owner's yes with
   this plan):
   - `Report/CurveTexts.cs` evidence line, RO: "culori pe care hartia nu le poate reda (hartia
     le-ar muta culoarea cu peste 3), inainte" -> "culori pe care hartia nu le poate reda (pe
     hartie ar iesi alt ton sau alta culoare, cu peste 4), inainte"; EN: "colours the paper
     cannot reproduce (the paper would move their colour by over 3), before" -> "colours the
     paper cannot reproduce (on paper they would come out another tone or colour, by over 4),
     before". The "after" line unchanged.
   - `Report/ProofTexts.cs` legend, RO: "culori pe care hartia nu le poate reda (gamutul):
     1.4 % din pixeli (hartia le-ar muta culoarea cu peste 3, aceeasi regula ca in tabul Editare
     Curbe)" -> "culori pe care hartia nu le poate reda (gamutul): 1.4 % din pixeli (pe hartie ar
     iesi alt ton sau alta culoare, cu peste 4; aceeasi regula ca in tabul Editare Curbe)"; EN
     likewise ("on paper they would come out another tone or colour, by over 4; the same rule as
     in the Curves Editing tab"). The number stays interpolated from `Thresholds`.
   - The check box, the key hint and the window's own texts do not name the rule: unchanged.
4. **Tests** (`AstroPhotoAnalyzer.Tests/CurveTests.cs`, `SoftProofTests.cs`), first, failing:
   - the three hand-built-table tests take the third array and the threshold 4 (the 2-cube
     examples re-scaled: a corner displaced by 16 in a*: 0.2 -> 3.2 inside, 0.3 -> 4.8 outside;
     the near-threshold test with a corner of 5: 0.6 -> 3.0 and 0.95 -> 4.75 within the margin,
     0.2 -> 1.0 outside it);
   - new `R144_ALightnessShiftAloneCountsAsOutside`: a hand-built table whose corner moves L*
     by 6 and a*, b* by 0 marks the corner;
   - new `R144_WithTheCompensationAGreyUnderThePaperBlackStaysInside`: on the hand-built
     profile (`IccPrinterTestData.V2Lut16`, black at L* 20) a grey at L* 10 is inside (with BPC
     it comes back near itself; without it the fixture would return L* 20, a difference of 10)
     and a saturated red is outside;
   - `R161_ThePaperWarningIsTheCurvesTabsRule` stays as it is (every colour of the fixture
     prints as the grey of its L*: outside under either rule); `SoftProofTests` line 243 takes
     the new name.
5. **Requirements**: R-144 and R-161 rewritten for the rule, the examples with the re-measured
   V-008 and V-015 figures.
6. **ADR** `docs/decisions/0081-...md` (the number checked against `main` at merge): the
   measurement table above, the decision, the alternatives rejected (chroma only as was; the
   `gamt` tag; Delta E over 3; no BPC with 6; lcms), the consequences. Supersedes 0032 in part
   (the chroma-only consequence) and 0042 in nothing (the hybrid stays).
7. **Docs**: `docs/design/measurements.md` F2, `docs/design/soft-proof.md` (the warning
   paragraph and "What was measured"), `docs/design/known-limitations.md` (the chroma-only
   bullet), `docs/design/thresholds.md` row, `README.md` line 155 (the figure now counts the
   tone too), `CHANGELOG.md` under Changed.
8. **Validation**: copy the reference image and the FITS into the worktree's
   `AstroPhotoAnalyzer.Tests/testdata/large/` (ADR 0004); `/validate`; V-008's twenty gamut
   figures and V-015's `pctPaper` move with the rule, every other figure must not; the drift
   table pasted at wrap-up, `docs/validation.json` and `.md` updated with a dated log entry.
   Before that, the hybrid against the exact per-pixel figure, as ADR 0042 did: a scratchpad
   probe on the plate-solved FITS with the lab's two profiles and on `de_printat.png`; the
   margin 1.0 stays if every class is within 0.2 points, else the facts go to the owner.
9. **Memory**: update `apa-gamut-warning-vs-lightroom.md` (decision taken, branch).

## Verification

- The new and adapted tests red before the code, green after; `scripts/verify.ps1` green in
  the worktree (never in the main checkout); the goldens unchanged (the console prints no gamut
  figure; any changed line is a finding).
- `/validate`: only the figures named in step 8 drift, each tied to the rule.
- The probe of this session, re-run on the branch's core against the Lightroom mask, gives the
  1.33 % and IoU 0.42 of the measurement (the same rule, now in the core).
- Window: `APA_DUMP_SCREENS` renders of the Soft proof and Curves tabs show the new legend.
- Then the subagent review of `git diff main...HEAD`, its findings applied, a second review of
  the fix commit, and the merge proposed (the owner decides).

## Record (2026-11-06)

Followed as written, with these deviations:

- The near-threshold test's first point moved from red 0.6 to 0.65: at 0.6 the interpolated
  displacement was exactly 3.0, on the edge of the margin (|3.0 - 4| = 1.0 is not under 1.0),
  so the interpolation decided; 0.65 gives 3.25, inside the margin, as the test needs.
- The hybrid against the exact figure was measured on the three reference pairs and on the
  Smida TIFF through the WhiteWall profile (the latter failed to load: the owner had moved the
  files from `finale/finalisti/` up to `finale/`); the three pairs were enough for the margin.
- The rule as built, scored against the Lightroom mask at the window's reduction, gave 1.37 %
  and IoU 0.48 where the probe's exact per-pixel rule gave 1.33 % and 0.42; both are in ADR
  0081, labelled.
- Two subagent reviews ran, not one: the first found seven document findings and no code
  defect, the second four more in the fixes (a bracket, a wrapped path, the gamt tag's
  threshold, the README's sentence); all applied, in two commits.
- The window renders of `APA_DUMP_SCREENS` showed the Soft proof tab's new legend on the
  hand-built profile ("pe hartie ar iesi alt ton sau alta culoare, cu peste 4").
