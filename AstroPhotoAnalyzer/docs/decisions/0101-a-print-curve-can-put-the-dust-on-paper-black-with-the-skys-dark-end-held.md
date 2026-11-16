# 0101. A print curve can put the dust on paper black, with the sky's dark end held

Date: 2026-11-16
Status: accepted 2026-11-16
Supersedes: 0032 in part (decision 3, for the dust: the curve never moves the dust)

## Context

ADR 0032 decided that a suggested curve moves neither the dust nor the sky: lifting a sky too
dark for paper is the re-stretch, and a sigmoid that darkened the shadows buried the dust. The
owner asked on 2026-11-16 for the opposite case. Starting from a bright stretch (0.17) on a
paper with Dmax 2.4, the paper's black is L\* 3.6 and the dust sits at 7.7: there is room to
pull the dust down and gain contrast between the dust and the sky, which a darker stretch
cannot give, since it lowers the sky with the dust. The request: a checkbox on the Curves
Editing tab, ticked by default, "Put dust on paper black".

### Measured on 2026-11-16

Throwaway tests over the core in the task's worktree, patch L\*, on the reference images of
ADR 0088. The plate-solved FITS `starless_rbf0.5_stretch0.17.fit` at Dmax 2.4 (paper black
3.60), as it is: dust 7.67, sky 17.58, their difference 9.91, the dust zone's middle half 7.67
to 12.91, the sky's wide range 14.01 to 19.99, noise 2.042 levels on 41 smooth tiles.

**Where the curve's stretch falls.** The dust's point put at the grey of paper black, three
ways, each with the upper part of `Blanda` / `Medie` / `Puternica`; the sky's noise is the
median over the same 41 tiles of the image as it is, in levels:

| | dust after | dust-sky difference | sky noise, the three strengths | sky's dark end (wide low) | steepest slope under the sky |
| --- | --- | --- | --- | --- | --- |
| today's curves | 7.67 | 10.0 to 10.2 | 2.29, 2.45, 2.62 | 13.8 | 1.2 to 1.5, at the sky |
| the dust's point alone | 4.2 | 13.4 to 13.6 | 3.11, 3.30, 3.43 | 12.0 | 1.6 to 1.7, at the sky |
| and a point held at the sky's 10 % (L\* 14.0) | 4.5 | 13.1 to 13.2 | 2.08, 2.16, 2.23 | 13.6 | 1.95, at L\* 10.5, in the dust |
| and a point held at the sky's 25 % (L\* 15.5) | 4.4 | 13.2 to 13.3 | 2.25, 2.30, 2.39 | 13.3 | 1.81, at L\* 11.1 |

Without a held point the gain is paid by the sky: its noise grows 31 to 37 % over today's same
curve and its dark end falls two L\*, which shows in the whole image as darker corners. Held at
the sky's 10 %, the sky's noise is under today's and the stretch moves into the lighter part
of the dust, where two tiles read 1.8 times the noise. `NoiseAnalyzer.Analyze` run again on a
curved image keeps 1 to 9 smooth tiles of the 41, so its figure cannot be used; the same tiles
were measured instead.

**The dust does not land where its grey is sent.** In every row above the dust ends at 4.2 to
4.5, not 3.6: its figure is a percentile of patch means, and the curve is applied per channel
to pixels that are not grey.

**Aimed by measuring, the spline needs help near black.** A bisection on the dust point's
output until the measured dust is paper black, the sky's 10 % held:

| the helper point, as a share of the dust's input and output | the dust reaches | the curve's lowest slope under the sky | its steepest in the dust |
| --- | --- | --- | --- |
| none | 4.35 (77 % of the move; further the spline is not monotone) | 0 | 2.01 |
| 0.25, 0.33, 0.5, 0.6 | 3.66, 3.67, 3.66, 3.63 (98 to 99 %) | 0 | 2.35 to 2.48 |
| 0.67 | 3.60 | 0.01 | 2.52 |
| 0.75 | 3.60 | 0.05 | 2.55 |
| 0.85 | 3.60 | 0.13 | 2.60 |

**The slope at the sky's anchor misreads the sky's noise once the dust is lowered.** The final
curves on the reference FITS, the noise measured on the 35 tiles that lie in the sky's wide
range, as a multiple of what they read before the curve:

| curves | slope at the sky's anchor | mean slope over the sky's wide range | measured on the sky's tiles |
| --- | --- | --- | --- |
| without the move | 1.13, 1.20, 1.26 | 1.11, 1.17, 1.22 | 1.12, 1.19, 1.28 |
| dust lowered (Dmax 2.4) | 0.76, 0.79, 0.82 | 0.96, 0.99, 1.01 | 0.95, 0.99, 1.04 |
| dust lifted (Dmax 2.0) | 1.12, 1.15, 1.18 | 1.07, 1.10, 1.13 | 1.09, 1.15, 1.21 |

A lowered dust bends the curve right at the sky's anchor, and the slope there would say the
sky's noise drops by a fifth when it stays where it was. In the other two rows the slope at the
anchor is the closer figure, and the larger. The two tiles in the lighter dust read 2.1 and 2.3
times their noise under the lowered curves, against the mean stretch of 2.15 the evidence gives
(+115 %), and 0.75 under the lifted ones, against 0.75.

**The gentlest curve lands the dust lowest.** Aimed on `Puternica`, the dust read 3.589 under
`Blanda` against a paper black of 3.596, and 8.962 against 8.991 when lifted.

**What the aimed move does on each image**, with the 10 % held:

| image, Dmax | the dust's figure from | dust, paper black | outcome |
| --- | --- | --- | --- |
| reference FITS 0.17, 2.4 | the dust zone | 7.67, 3.60 | lowered onto 3.60; under paper black 0.2 % -> 2.2 % of the frame |
| reference FITS 0.17, 2.0 | the dust zone | 7.67, 8.99 | lifted onto 8.99; under paper black 3.7 % -> 2.3 % |
| FITS 0.10, 2.4 | the dust zone | 3.51, 3.60 | lifted by 0.08 |
| FITS 0.10, `de_printat.png`, Rosetta, Veil, Andromeda at 2.0 | | , 8.99 | paper black is above the sky's dark end: no curve can |
| `de_printat.png`, 2.4 | the darkest sky | 4.41, 3.60 | lowered onto 3.60, the slope at the sky 0.94 |
| Rosetta, 2.4 | the darkest sky | 6.15, 3.60 | 30 % of the move; the sky falls 8.19 -> 7.62, the slope at the sky 0.55 |
| Veil, 2.4 | the darkest sky | 6.43, 3.60 | 18 % of the move; slopes of 2.3 to 3.1 in the sky's darkest tenth |

## Decision

1. **The dust move.** For print, and only when asked, every suggested curve also puts the dust
   on the paper's black. The window asks through a checkbox, ticked at every start; the core's
   `CurveRun.Execute` takes it as a parameter that is off unless given, so every other caller
   gets ADR 0032's curves.
2. **The sky is held.** A point at the sky's dark end, the 10 % percentile of the sky patches
   (`CurveSkyHoldCut`, the low end of R-046's wide range), maps to itself. The sky's own point
   stays as ADR 0032 has it.
3. **As far as it can, at every strength.** The dust goes to paper black whatever the strength:
   `Blanda`, `Medie`, `Puternica` and `Maxima` differ above the sky only. The owner's words: in
   the dust the three strengths cannot be told apart, so the dust always gets the most the sky's
   protection allows. Nothing caps the move but the spline staying suggestible (R-140).
4. **Aimed by measuring.** The dust point's output is found by bisection on the curved
   subsample, so that the dust's L\*, measured as the evidence measures it, is the paper's black;
   on the gentlest curve of the set, so that no strength leaves the dust under it.
5. **A helper point, only when needed**, at 0.75 of the dust's input and output
   (`CurveDustHelperFraction`): the lowest share at which the whole move is monotone with some
   margin. When no spline goes all the way, the largest part of the move that is suggestible,
   and the evidence says the dust stopped short.
6. **Both ways.** A dust under paper black is lifted onto it (the owner's choice): on paper it
   printed as paper black anyway, so nothing is lost there and the dust's detail comes back.
7. **Not when the darkest sky stands in for the dust** (the owner's choice): the move would
   stretch the sky's own darkest part and, on two of the three such images, drag the sky.
8. **A box that can do nothing is unticked and disabled**, with the reason beside it (the
   owner's rule): the screen, no dust zone, a dust at full black, a dust not darker than the
   sky's dark end, a paper black not under the sky's dark end, a dust already on paper black,
   and, found only once the move is asked for, a move that leaves no monotone curve. The
   owner's choice itself is kept for the next image.
9. **The costs in percent** (the owner: nobody reads the noise scale). The evidence of a moved
   curve says where the dust went, by how many percent its difference from the sky changes on
   paper (`sky - max(dust, paper black)`), by how many percent the noise in the dust changes (the
   mean stretch from the dust's point to the held one) and the share of the frame under paper
   black before and after; and the noise of the sky is a percentage on every curve, moved or not.
10. **The sky's noise of a moved curve** is the larger of the slope at the sky's anchor and the
    mean slope over the sky's wide range. A curve without the move keeps the slope at the anchor
    (R-143), so no recorded figure changes.
11. **The curve's paper check on the noise fires only when the curve is the cause**: the noise
    was at or under the print target's upper bound before the curve and is over it after. The
    owner saw, on the reference FITS with the box ticked, "the noise in the sky drops by 5 %"
    and under it, in red, "After the curve, the noise in the sky passes the print target:
    denoise before, or choose a gentler variant". The check had only compared the noise after
    the curve (1.95 levels) with the bound (1.5), and the image has 2.04 before any curve; it
    also spoke against the report, which for that noise says that on paper it does not show and
    that a denoise is only for a screen version. The check dates from ADR 0032 and fired on this
    image for every curve; a curve that lowers the noise made the contradiction plain.
12. **A dust that ends within `CurveDustAimTolerance` of paper black has arrived.** With the
    owner's profile (Dmax 2.43, paper black L\* 3.4) the move is cut by a fraction of a tenth,
    and the line read "from L\* 7.7 to 3.4 (paper black is L\* 3.4 ...); further than that the
    curve would no longer rise smoothly". Stopped short now means further from paper black than
    the tolerance.

## Alternatives rejected

- **The dust's point alone, no held point.** Measured: the sky pays, 31 to 37 % more noise and
  a darker dark end.
- **The held point at the sky's 25 %.** 2.25 levels against 2.08 under `Blanda`: the sky is
  less protected, for a slope in the dust of 1.81 against 1.95.
- **A move scaled with the strength.** Three curves that differ where nothing can be seen; the
  owner asked for the full move always.
- **A cap on the noise in the dust.** The owner's rule is the most the sky's protection allows;
  the figure is shown instead.
- **The dust's point at the grey of paper black.** Lands the dust 0.6 to 0.9 L\* short.
- **The slope at the sky's anchor as the sky's noise of a moved curve.** It would print "drops
  by 24 %" where the tiles measure 5 % under to 4 % over.
- **The mean slope over the sky's range for every curve.** Closer on a lowered dust only; on
  the plain curves it is 1 to 4 % under the measurement and would move every recorded gain.
- **No helper point; the helper at half.** 77 % and 98 % of the move.
- **Lower only.** The box's name says where the dust goes, and a lift costs nothing on paper.
- **Moving the darkest sky when there is no dust zone.** Measured on Rosetta and the Veil: it is
  the sky that moves.
- **The box shown ticked where it does nothing.** The owner: nothing ticked that does nothing.
- **Both galleries computed with the analysis.** Twice the run and twice the bitmaps for a
  choice most runs never change; each is computed the first time it is asked for, and kept.
- **Dropping the curve's check on the noise altogether**, since the report says noise over
  the target does not show on paper. Kept for the one case its words fit: a strong curve
  multiplies the noise by up to 1.4 and can be what takes it out of the target.
- **The levels of the Denoise tab in the new lines.** The owner asked for percent. The paper
  check on the noise keeps its levels: its threshold is the Denoise tab's, and changing that
  scale is a task of its own.

## Consequences

- R-184 and R-185; R-142, R-143, R-145, R-146, R-147 and R-178 amended. V-017 guards the move on the
  reference FITS at Dmax 2.4; the other curves cases do not ask for it and do not move.
- `Thresholds`: `CurveSkyHoldCut`, `CurveDustHelperFraction`, `CurveDustAimTolerance`.
  `CurvePoint` gains `Extra`, which marks the two labelled points that are no class (`cer
  inchis`, `sub praf`), so the curve window's L\* columns and the shadow chart skip them.
- The first paper check of a moved curve is on the sky alone, unless the move stopped with the
  dust still under paper black; the sentence under the evidence comes from the core and says
  what the curves shown move, and neither it nor the `HISTORY` of a saved curve says the dust
  is on paper black when the move stopped short.
- A kept gallery is good while the area allows no finer preview than it was made with: the
  area's size itself changes with the evidence under the image, so it cannot be the test.
- With the box ticked, print and screen show different curves, so R-146's "changing the purpose
  computes nothing" holds only with the box off or disabled. Four tests of the plain gallery in
  `CurvesTabTests` untick the box first; their assertions did not change.
- The aim costs a number of probes of the tonal subsample (at most two bisections of 16 steps,
  each after one probe of its range's end, and two more measurements: 36), once per set.
- Three entries of the code review's list (ADR 0092) are closed with this task, because the
  box made two of them common and removed the case of the third: a disabled check box is
  dimmed by the theme (CR-022; the box is disabled on every change to the screen and on every
  image without a dust zone); the tab's red sentence and the box's reason wrap inside the tab,
  in the Colours for paper tab too (CR-031; the box pushed the sentence 175 px further out);
  `Salveaza ca...` writes the curve it was pressed on (CR-050; keeping the position across a
  recompute had removed its case, and the two galleries had added another). Three new entries
  hold what the box added and this task left: CR-070, CR-071, CR-072. A file saved with the
  move and analysed again reads its dust at 3.73 to 3.81 against the paper black of 3.60 (the
  dust zone grows from 1941 to 2019-2082 patches on the darker image), so the saved file does
  not sit on the "dust under paper black" boundary.
- The refusal for an image without a dust zone is one sentence per cause, each whole by itself
  (no dust was found; too little of it, with its share and the share needed; the darkest part
  of the sky darker than the dust), worded at the owner's word of 2026-11-16: the report's
  bracketed reasons, first reused, did not read as sentences beside the box.
  `SimulationResult.DarkestSkyCause` holds the rule once, for the report's reasons and for
  these sentences.
- The two radio buttons' tooltips no longer say "the same curves" (shown to the owner, old and
  new side by side, and accepted on 2026-11-16).
- Known limits, in `docs/design/known-limitations.md`: the quarter of the dust zone darker than
  the dust's figure ends under paper black; the noise in the dust is the stretch's mean, checked
  against two tiles; a move that leaves no monotone curve is found only when asked for.
