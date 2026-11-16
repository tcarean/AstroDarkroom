# Put the dust on paper black in the suggested curves

## Context

The suggested curves never move the dust or the sky (ADR 0032, decision 3). The owner starts from
a bright stretch (0.17) and prints on a paper with Dmax 2.4, where the paper's black is L\* 3.6 and
the dust sits at 7.7: there is room to pull the dust down and gain contrast against the sky, which
no darker stretch gives (it lowers the sky with it). Asked for: a checkbox on the Curves Editing
tab, ticked by default, **Put dust on paper black**.

Settled with the owner on 2026-11-16:

- the sky is protected: a point at the dark end of the sky is held in place, and the dust goes
  **as far as it can**, always, whatever the strength (`Blanda`, `Medie`, `Puternica`, `Maxima`
  change only the part above the sky, as today);
- when the dust is already under paper black, the box **lifts** it onto paper black;
- where the box can do nothing it is shown **unticked and disabled**, with the reason beside it,
  never ticked and idle;
- the costs are shown **in percent**, not on the noise scale.

Measured on 2026-11-16 (throwaway tests over the core, in this worktree; patch L\*):

| image, Dmax | dust source | dust | paper black | what the move does |
| --- | --- | --- | --- | --- |
| reference FITS 0.17, 2.4 | dust zone | 7.67 | 3.60 | lowers to 3.66; on paper the dust-sky difference 9.9 -> 14.0 (+41 %); under black 0.2 -> 2.2 % of the frame |
| reference FITS 0.17, 2.0 | dust zone | 7.67 | 8.99 | lifts to 8.99; under black 3.7 -> 2.3 % |
| FITS 0.10, 2.4 | dust zone | 3.51 | 3.60 | lifts by 0.08, nothing to gain |
| FITS 0.10, 2.0; `de_printat.png`, Rosetta, Veil, Andromeda at 2.0 | | | 8.99 | no room: paper black is above the sky's dark end |
| `de_printat.png`, Rosetta, Veil at 2.4 | darkest sky | | 3.60 | not offered (the owner's choice): on Rosetta the move drags the sky down 0.6 and flattens the curve at the sky by 45 %, on Veil it stretches the sky's darkest tenth 2.3 to 3.1 times |

- **The sky's dark end must be held.** Without it the sky's noise grows 31 to 37 % over today's
  same curve and the sky's dark end falls from 14.0 to 12.0; held at the sky's 10 % percentile
  (L\* 14.0) the sky's noise stays under today's (`Blanda` 2.29 -> 2.08 LSB on the same 41 tiles);
  held at the 25 % percentile it is 2.25.
- **The dust's point must be aimed by measuring.** Put at the grey of paper black, the dust lands
  at 4.5: its figure is a percentile of patch means and the curve is per channel.
- **A second point under the dust is needed to get there.** Aimed, the plain spline stops being
  monotone near black at 77 % of the move (dust 4.35); with a point half-way under the dust it
  reaches 98 % (dust 3.66).

Branch `claude/Put-the-dust-on-paper-black`, worktree
`.claude/worktrees/Put-the-dust-on-paper-black`, at `main` (f8641aa9), nothing committed. Window
only: the console, the saved report and the golden files do not change.

## Requirements (drafts; approving this plan accepts them)

- **R-184** (core). The dust move, for print only. Available when the dust's figure comes from the
  dust zone (R-044, `DustSource.DustClass`), the dust's anchor is above 0, the sky's dark end (the
  `CurveSkyHoldCut` percentile, 10 %, of the sky patches, as the image is) lies above both the
  dust's anchor and the grey of paper black, and the dust is more than `CurveDustAimTolerance`
  (0.05 L\*) away from paper black; otherwise it is refused with a sentence that says which.
  Applied, every suggestion of the set gets the same three points under the sky: the dust's point
  with its output aimed, by bisection on the curved subsample, so that the dust's measured L\* is
  the paper's black; the sky's dark end mapped to itself; and, only when the spline would not be
  suggestible without it (R-140), a point at `CurveDustHelperFraction` (0.5) of the dust's input
  with the same share of its output. If the spline is still not suggestible, the move is the
  largest part of the way that is. The sky's point and the points above it are R-142's.
  Example: the reference FITS at Dmax 2.4, `Puternica`: 0.045 > 0.017, praf 0.090 > 0.034,
  cer inchis 0.140 > 0.140, cer 0.169 > 0.169, then as today; dust 7.7 -> 3.7, sky 17.6 -> 17.7.
- **R-185** (window). Curves Editing tab: after the two radio buttons, the checkbox
  `Put dust on paper black` (`Pune praful pe negrul hartiei`), ticked at every start of the
  window; the choice is kept when another image is opened and not between runs. It is enabled
  and shows the choice only for `Print` on an image where R-184 is available; otherwise it is
  unticked and disabled, with R-184's reason (or "only for print") beside it. Each of the two
  galleries (moved, not moved) is computed the first time it is asked for and kept, so going
  back and forth computes nothing again. The evidence of a moved curve adds the lines below.
- Amended, same task: R-142 (the dust moves only by R-184), R-143 (the noise in percent; the new
  lines; the first paper check), R-145 (`HISTORY` names the move), R-146 (the checkbox, the
  footnote), R-147 (the two extra rows and the note).

## Texts (plain words; the pair is what gets written)

| where | today | proposed |
| --- | --- | --- |
| checkbox | - | `Put dust on paper black` / `Pune praful pe negrul hartiei` |
| its tooltip | - | `For print: the curve also moves the dust onto the paper's black, so the dust stands out from the sky as much as the paper allows. The sky stays where it is.` / `Pentru print: curba muta si praful pe negrul hartiei, ca sa se desprinda de cer cat permite hartia. Cerul ramane pe loc.` |
| reason, Screen | - | `only for print: a screen has no paper black` / `doar pentru print: ecranul nu are negrul hartiei` |
| reason, no dust zone | - | `not on this image: {the report's reason the darkest sky decides}` / `nu pe imaginea asta: {...}` |
| reason, no room | - | `not on this image: the sky itself is too close to the paper's black (its dark part at L* 6.3, paper black at L* 9.0); lift the background when you redo the stretch, as the Tones tab says` / `nu pe imaginea asta: cerul insusi e prea aproape de negrul hartiei (partea lui inchisa la L* 6.3, negrul hartiei la L* 9.0); ridica fundalul cand refaci stretch-ul, cum spune tabul Tonuri` |
| reason, already there | - | `the dust is already on the paper's black (L* 3.6)` / `praful e deja pe negrul hartiei (L* 3.6)` |
| evidence, noise | `zgomotul din cer creste de 1.26 ori (cat de tare urca curba in dreptul cerului), la 2.58 niveluri` | `the noise in the sky grows by 26 %` / `zgomotul din cer creste cu 26 %` (or `drops by` / `scade cu`), for every curve, moved or not |
| evidence, new 1 | - | `the dust on the paper's black: from L* 7.7 to 3.7 (paper black is L* 3.6 at Dmax 2.40)` / `praful pe negrul hartiei: de la L* 7.7 la 3.7 (negrul hartiei e L* 3.6 la Dmax 2.40)`; when stopped short, plus `; lower than that the curve would no longer rise smoothly` / `; mai jos de atat curba n-ar mai urca lin` |
| evidence, new 2 | - | `on paper, the difference between the dust and the sky grows by 41 %` / `pe hartie, diferenta dintre praf si cer creste cu 41 %` |
| evidence, new 3 | - | `the noise in the dust grows by 111 %` / `zgomotul din praf creste cu 111 %` (or drops) |
| evidence, new 4 | - | `solid black on paper (under the paper's black): 0.2 % of the image before, 2.2 % after` / `negru plin pe hartie (sub negrul hartiei): 0.2 % din imagine inainte, 2.2 % dupa` |
| first paper check, moved | `...Curba nu muta cerul si praful.` on a sky too dark or a dust under black | on the sky alone; ends `The curve does not move the sky.` / `Curba nu muta cerul.` |
| footnote, moved | `Curbele nu muta praful si cerul: ...` | `The curves do not move the sky. The dust is put on the paper's black, and the dark part of the sky is held in place. A sky too dark for paper is lifted when you redo the stretch (see the Tones tab). ...` / `Curbele nu muta cerul. Praful e pus pe negrul hartiei, iar partea inchisa a cerului e tinuta pe loc. Un cer prea intunecat pentru hartie se ridica atunci cand refaci stretch-ul (vezi tabul Tonuri). ...` (the last sentence as today) |
| curve window, rows | - | the held point is the row `dark sky` / `cer inchis`, the point under the dust `under the dust` / `sub praf`; neither has an L\* |
| curve window, note | `Primul si ultimul rand sunt capetele curbei.` | plus `"Under the dust" keeps the curve smooth towards black; "dark sky" is the dark part of the sky, held in place.` / `„Sub praf" tine curba lina spre negru; „cer inchis" e partea inchisa a cerului, tinuta pe loc.` |
| `HISTORY` | - | one more piece: `dust put on paper black (L* 3.6, Dmax 2.40)` / `praful pus pe negrul hartiei (L* 3.6, Dmax 2.40)` |

The figures: on paper the dust cannot print darker than paper black, so the difference is
`sky - max(dust, paper black)`, before and after; the dust's noise is how much wider the stretch
from the dust's point to the held point becomes. The second paper check (the noise over the print
target) keeps its level scale: its threshold is the Denoise tab's, and changing that scale is a
task of its own.

## Design

**Core, numbers** (`AstroPhotoAnalyzer.Core`)

- `Thresholds.cs`: `CurveSkyHoldCut` (0.10), `CurveDustHelperFraction` (0.5),
  `CurveDustAimTolerance` (0.05), each with the measurement above as its comment.
- `Analysis/CurveAdvisor.cs`: a `DustMove` record (the dust's output, the held input, whether the
  helper is used, whether it stopped short) and its refusal; `DustMoveProblem(...)` beside
  `Problem`; `AimDust(sub, map, anchors, labels, paperBlack)`, the bisection, each probe
  `ToneCurve.Apply(sub)` + `DustAnalyzer.PatchLstars` + `DustAnalyzer.Measure` (about 16 probes of
  the tonal subsample; the seven-image measurement, two Dmax each, took 84 s with the analyses).
  `Curves(anchors, labels, DustMove move = null)`: `PointsOf` adds the three points, so the two
  existing caps (ADR 0039) already judge the spline with them in place. `ShadowChartLimit` finds
  the band's point by its label instead of by position. `CurveEvidence` gains the four figures;
  `Measure` fills them from the `DustStats` it already computes.
- `Runs/CurveRun.cs`: `Execute(a, purpose, maxWidth, maxHeight, dustOnPaperBlack = false,
  progress)`. The default keeps every existing caller and test on today's curves. `CurveSet`
  gains `DustOnPaperBlack` (applied) and `DustMoveRefusal` (always computed for print, so the
  window knows whether to enable the box); a refused move returns the plain set. One more log
  line for the aim (`docs/design/logging.md`).

**Core, words** (`Report/CurveTexts.cs`): the refusals, the four evidence lines, the percent
noise line, the first paper check's two forms, the footnote's two forms, the note of the curve
window, the `HISTORY` piece.

**Window** (`AstroPhotoAnalyzer.Wpf`)

- `ViewModels/CurvesTab.cs`: `DustOnPaperBlack` (the choice, true at start, kept across images),
  `DustBoxChecked` / `DustBoxEnabled` / `DustBoxReason` for the view; two slots (moved, plain),
  each filled by `ComputeAsync` the first time it is wanted, both dropped by `Reset` and by a
  finer preview size; `CurveWindowViewModel.From` matches the L\* columns by class label, so the
  two extra rows stay blank there.
- `Views/CurvesView.xaml`: the checkbox and its reason in the top row; the footnote bound to the
  core's sentence instead of the fixed text.

## Order of work

1. `docs/requirements.md`: R-184, R-185 as `draft`, the five amended rows.
2. Failing tests: `CurveTests` (`R184_...`: the move reaches paper black with the sky's point
   unmoved; each refusal; the helper only when needed; the plain call unchanged), `CurvesTabTests`
   (`R185_...`: ticked and enabled on a dust-zone scene, unticked and disabled on a sky-path
   scene and on Screen, the second toggle computes nothing, the choice survives another image);
   a `Scenes.cs` scene with a dust zone over 5 % if none has one. `docs/validation.json`: case
   V-017, curves on the reference FITS at Dmax 2.4 with `dustOnPaperBlack`, `expected` null;
   `ValidationTests.CurveFigures` reads the parameter and adds the move's figures. Paste the
   failing run.
3. ADR 0101, "A print curve can put the dust on paper black, with the sky's dark end held":
   supersedes ADR 0032's decision 3 in part (its status line says so); the measurements above;
   the alternatives rejected (no hold, the hold at 25 %, a scaled move per strength, the darkest
   sky as dust, lower only). Accepted by this plan's approval.
4. Core: thresholds, `CurveAdvisor`, `CurveRun`, `CurveTexts`; `verify.ps1 -SkipTests` green.
   Before fixing `CurveDustHelperFraction`, measure 0.5, 0.67 and 0.75 on the reference FITS and
   keep the one that gets closest; the ADR records the three.
5. Window: view-model, view; the smoke render of the tab with the box in both states.
6. `/validate`: the drift table (V-008, V-009, V-011, V-012, V-014 must not move: the plain path
   is untouched); fill V-017 from the measured output, dated in `docs/validation.md`.
7. Record: requirements to `implemented`; `CHANGELOG.md` (Added); `README.md`, "Suggested
   contrast curves"; `docs/design/measurements.md` (F1), `window.md`, `pipeline.md`,
   `logging.md`; the `ToneCurve` line of `CLAUDE.md`'s "Read before touching".
8. `/wrap-up`: the gate, a subagent code review and one on its fixes, the commits on the branch,
   then the merge proposal. After the merge, with the owner's word: the wiki page of the tab.
   The change seems to warrant a minor version bump; the owner decides.

## Verification

- `scripts/verify.ps1` green (build with warnings as errors, both test projects, the design
  check).
- `dotnet test --filter "FullyQualifiedName~R184|FullyQualifiedName~R185"` red before step 4,
  green after step 5.
- `/validate` with the pasted drift table: no existing case moves, V-017 filled.
- The window on the reference FITS with Dmax 2.4 (`APA_DUMP_SCREENS` render of the tab, and a
  run by hand): the box ticked, the dust row at 3.7, the four percent lines; unticked gives
  today's curves point for point; on `de_printat.png` the box is unticked and disabled with its
  reason; a saved FITS carries the `HISTORY` piece and reads back as the curve applied.

## What deviated (2026-11-16)

- **The helper point sits at 0.75, not 0.5.** Step 4's measurement: at 0.5 the move stops at
  98 % (the dust at 3.66); at 0.67, 0.75 and 0.85 it is whole (3.60); 0.75 leaves a margin
  (the curve's lowest slope under the sky 0.05 against 0.01). So the example of R-184 reads
  sub praf 0.067 > 0.024, praf 0.090 > 0.032, and the dust lands on 3.6, not 3.7.
- **The dust is aimed on the gentlest curve of the set**, where it lands lowest: aimed on the
  strongest, `Blanda` left it 0.01 to 0.03 L\* under paper black.
- **The noise of the sky of a moved curve is not the slope at the sky's point alone.** Measured
  after the first validation: with the dust lowered that slope reads 0.76 to 0.82 where the
  sky's tiles measure 0.95 to 1.04. A moved curve takes the larger of the slope and the mean
  slope over the sky's wide range; a plain curve keeps the slope (ADR 0101, decision 10).
- **The texts.** The Dmax prints as the program writes it everywhere, `2.4`, not `2.40`. The
  refusal for an image without a dust zone got a subject: `not on this image: the darkest part
  of the sky stands in for the dust (...)` / `nu pe imaginea asta: drept praf e luata partea cea
  mai inchisa a cerului (...)`, the report's reason in the brackets. A move that stops short says
  `further than that` / `mai departe de atat`, which is right for a lift too. Two refusals the
  plan did not list: the dust already at full black, and a dust not darker than the sky's dark
  part. The tooltip says `the black of the paper` in English (an apostrophe cannot stand in a
  XAML text). The curve window's note names `sub praf` only on a curve that has that row.
- **Four tests of the plain gallery in `CurvesTabTests` untick the box first.** The band scene
  they use can have the move, and the box is ticked at start; their assertions are unchanged.
- **The position in the gallery is kept** when a gallery is replaced by another of the same
  image (the box, the purpose, a finer preview); it used to go back to the original on every
  computation.
- **What the two reviews changed** (commits after the first). A kept gallery is judged by the
  reduction step of its previews, not by the area's size, which changes with the evidence under
  the image; the footnote no longer fails on the first change to the screen; a move that stopped
  with the dust still under paper black keeps the paper check, and the footnote and the
  `HISTORY` then say the dust was moved towards paper black, not put on it; a refusal found only
  once the move is asked for stays for the image; the curve window's note explains a row only
  on a curve that has it. The refusal kept for the image has no test: no scene was found on
  which the move leaves no monotone curve.
- **After the owner tried the window** (2026-11-16). The curve's check on the noise fires only
  when the curve is the cause; chart labels that would overlap start one under the other; a
  dust within the tolerance of paper black counts as arrived; six `paperChecks` counts of V-008
  and V-017 went from 1 to 0 at the owner's word. Asked what the box does for the screen: nothing,
  a screen has no paper black; a screen target would be another feature.
- **The review list of 2026-11-13, checked against the branch at the owner's word.** Closed here:
  CR-022 (a disabled check box dimmed, theme-wide), CR-031 (the tabs' sentences wrap inside the
  tab, in both tabs) and CR-050 (the save writes the curve it was pressed on). Added: CR-070 to
  CR-072. The two radio tooltips reworded as shown to the owner. The unread progress stage and
  the box's dead `Foreground` setter, which the cleanup list would have counted, were taken out.
- **Four differences from the Design section above.** The two points a move adds are told
  apart by `CurvePoint.Extra`, not by their labels (`ShadowChartLimit`, the curve window's L\*
  columns). The box is `DustOnPaperBlack`, `DustBoxEnabled` and `DustBoxReason`; there is no
  `DustBoxChecked`. The move's figures come from `CurveAdvisor.DustFigures`, called by the run,
  not from `Measure`. `AimDust` takes the measurement as a delegate, so the tests can pass a
  grey dust.
- **A third review, of the whole branch, at the owner's word.** One state bug: a gallery kept
  from a small area was not refined when the area grew after the other gallery had been
  (`SetGallerySize` now compares with the gallery on screen). The paper check of a dust left
  under paper black names the sky only when the sky is too dark too. Tests added for the
  rows `cer inchis` and `sub praf` through the run and the tab, on a striped frame whose sky
  has its own spread, and for a purpose changed during the first computation. CR-073 added.
  A test meant to reach "no smooth curve" was dropped: with the sky's dark end 0.0003 above
  the dust the move is still one, cut to 0.08 L\*, its steepest slope held near 3.4 by the
  spline itself.
- **The refusal for an image without a dust zone, reworded at the owner's word** (the owner left
  the wording to the session): three sentences of its own instead of the report's reason in
  brackets. `not on this image: no dust was found in it, so there is nothing to set apart from
  the sky` / `nu pe imaginea asta: nu s-a gasit praf in ea, deci nu e nimic de desprins de cer`;
  `not on this image: it has too little dust (0.8 % of the image, and at least 5 % is needed), so
  the darkest part of the sky is taken in its place` / `nu pe imaginea asta: are prea putin praf
  (0.8 % din imagine, si e nevoie de cel putin 5 %), asa ca in locul lui e luata partea cea mai
  inchisa a cerului`; `not on this image: the darkest part of the sky is darker than the dust, so
  it is taken in the dust's place` / `nu pe imaginea asta: partea cea mai inchisa a cerului e mai
  inchisa decat praful, asa ca ea e luata in locul lui`.
- **The validation records made exact, at the owner's word**: 191 figures of eleven cases, each
  inside the tolerance, recorded as measured (`docs/validation.md`, History); V-016 left, two of
  its figures differing between two runs (CR-074).
