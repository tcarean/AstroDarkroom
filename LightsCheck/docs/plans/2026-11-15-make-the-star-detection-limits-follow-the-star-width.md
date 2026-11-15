# Make the star detection's pixel limits follow the stars' width

## Context

`FrameAnalyzer.Analyze` (`src/LightsCheck.Core/Analysis/FrameAnalyzer.cs`) calls a detected
component a star when its area is between `Th.MinStarArea` (5) and `Th.MaxStarArea` (400)
pixels. From stars about 7 px wide the brightest pass 400 px and are left out (ADR 0056: a
winged star of 7 px at a peak of 200 sigma, a Gaussian of 10 px from 60 sigma, 15 px at every
peak the shapes are read at). The width read stays right (adaptive moments, good to 15 px), but
the figures rest on fewer and fainter stars and the bright count falls; FOCUS says so on the
`wide stars` row (R-088, `Th.WideStarFwhm` 7 px). The halo is read in fixed rings of 3, 5 and
10 px (`Th.HaloCore`, `HaloInner`, `HaloOuter`, `StarShapes.Halo`), which mean something else
on wide stars. The owner wants these limits free of the pixel scale: the area limit and the
halo's rings follow the stars' width, if a measurement shows it can be done without letting
anything else in among the stars. ADR 0056 left this as a task of its own; this is it.

Two stages, the owner's decision between them. **Stage 1 changes no code in the repository.**
`prototype/` stays frozen. No figure of `docs/validation.json` moves without the owner's word.

Branch `claude/Make-the-star-detection-limits-follow-the-star-width`, worktree
`LightsCheck/.claude/worktrees/Make-the-star-detection-limits-follow-the-star-width`, made from
`main` in `E:\Photografie\Code`. This plan is saved there as
`docs/plans/2026-11-15-make-the-star-detection-limits-follow-the-star-width.md` and committed
first (the only commit of stage 1).

## What the code does today (read for this plan)

- `Components.Find` labels the smoothed residual above 5 sigma once; each `Component` carries
  area, box, peak, raw peak, flux, centroid. Everything below works on that one list.
- Trails: area > 250, box diagonal > 80, area < 0.25 x box + 60. A trail of 251 to 400 px is
  also a star today (CR-013 of `docs/review/2026-11-13-code-review.md`, open).
- Stars: area 5..400, by flux. From them: the count (`Detected`, which the clouds flag and the
  stars' trend read), the bright count, the 400 shapes (unsaturated, peak over 20 sigma), the
  halo of the 40 brightest shapes, the 600 registration stars (saturated included).
- The frame's width exists only after the shapes; the shapes need the star list. Hence the
  owner's question of two passes.
- Downstream of the halo: the dew verdict (`Th.DewFwhmRise` 15 % and `Th.DewHaloRise` 0.02 on
  the first against the last good frames).

## Stage 1: measure (code outside the repository)

### The harness

A C# console project in the session's scratchpad (`area-bench/`), as last time
(`docs/plans/2026-11-14-measure-the-star-width-right-at-any-pixel-scale.md`): it compiles the
worktree's `src/LightsCheck.Core/**/*.cs` by link, with `Analysis/FrameAnalyzer.cs` and
`Thresholds.cs` (and `StarShape.cs` if the halo needs it) left out of the link and replaced by
scratch copies that carry a switch for the variant, so a whole `SeriesRun` runs with each
variant. Packages and versions those of `Directory.Packages.props`; `bin/` and `obj/` in the
scratchpad; Release build; timing in-process (E: is a hard disk, decode time kept out). Scripts
written with the Write tool, not heredocs. Before any variant is trusted the harness must
reproduce today's figures: the six frames of V-002 to V-007 as recorded, and ADR 0056's count
of drawn stars that pass (all of 196 up to 7 px; at 10 px only at 25 sigma; none at 15 px).

### The variants

The area limit (the first list is where the measurement starts; a variant the figures suggest
is added and said so):

| | Rule | Needs the frame's width? |
| --- | --- | --- |
| A0 | Today: 5..400 px | no |
| A1 | The cap as a multiple of the frame's width squared: `c x W^2`, `c` = 64 (what 400 px is to the Veil's 2.5 px stars); with and without 400 px as a floor | yes |
| A2 | The cap per component, from its own peak: at most `m` times the footprint a star of the frame's width has above the detection threshold at that peak (`m` read off real stars, which have wings) | yes |
| A3 | No cap on area; a component is a star when the core measurement settles on it at a width near the frame's | yes |
| A4 | No cap at all, as the upper bound: everything that would come in | no |

`Th.MinStarArea` (5 px) is reported too (drawn faint stars lost, noise let in, per width), and
kept unless the figures say otherwise.

How the frame's width `W` is known before the stars are (the owner's question), measured both
ways: (P1) a first reading of the core (`StarShapes.Measure`, unchanged) on the brightest
unsaturated components with no cap on area, its median the width, then the star list, then the
shapes, a component measured in both read once; (P2) one pass, the width read off the
components themselves (area against peak), no shape measurement before the list. Reported: how
far `W` of the first reading is from the frame's final width, on nebula-rich frames and on
frames of 15 px stars, and the time added per frame against the about 4.7 s of a frame.

The halo's rings: H0 today (3, 5, 10 px); H1 each star's own core width times 1.2, 2 and 4
(what 3, 5 and 10 px are to 2.5 px stars); H2 the frame's width times the same.

Trails: T0 today; T1 a trail is no star (CR-013); and whether 250 px, 80 px and the slack of
60 px meet big stars under each area variant.

### Synthetic frames (the answer is known)

Whole frames through `FrameAnalyzer.Analyze` (mosaic, green, background, detection), the sky
noise of a Veil frame (sigma 14 ADU):

- Stars of 3, 5, 7, 10 and 15 px, Gaussian and Moffat (beta 2.5), at peaks of 25, 60, 200, 500
  sigma and near saturation, 196 per cell: **how many of the drawn stars are counted, measured
  for shape and kept for registration**, today and with each variant; the frame's width, bright
  count and halo read.
- What must stay out, drawn among them at each width: diffuse knots (wide flat blobs at 2 to 6
  times the stars' width), pairs of stars touching, a satellite trail as wide as the stars
  crossing the field (alone, and through stars).
- The halo: the same stars with a faint skirt holding 10 and 30 % of the light (dew-like), and
  the same stars 30 % wider with no skirt (a focus change), per width: what H0, H1 and H2 read,
  and what the dew rule would say.

### Real frames

- The ten frames ADR 0056 read: `Veil/2026-10-06-3h` frames 1, 4, 17, 23, 72, 83;
  `Veil/2026-11-06` `DSC_0001`, `DSC_0050`; `Milkyway-Core/2026-08-12-Smida` `DSC_0002`,
  `DSC_0020`; plus the frames of the three sessions that carry a trail today.
- Per frame and variant: **every component between 400 px and the new limit**, sorted by the
  program into saturated star, unsaturated star (the core settles near the frame's width), pair
  (two peaks), diffuse (nebula knot, galaxy), trail; a sheet of cut-outs per frame saved in the
  scratchpad and looked at, so the sorting is checked by eye. Then how the width, its 90th
  percentile, the elongation, the star and bright counts, the halo and the registration list
  move against today.
- The same sky at another pixel scale, since no real session has stars wider than 4.7 px: a
  crop of a clean Veil frame and of a Smida frame drawn 2 and 3 times larger (and the Veil frame
  of 2026-11-06 binned 2 x 2), run as a frame without a colour order. A rule free of the pixel
  scale finds the same stars at every scale; reported per variant as stars found and lost
  against the 1 x frame. Its limit is said with it (the noise of an enlarged frame is smoother
  than a real one's).

### Whole sessions and `docs/validation.json`

- V-002 to V-007: all fifteen figures per variant (`detected`, `bright`, `fwhm`, `fwhmP90`,
  `elongation`, `elongatedFraction`, `angle`, `coherence`, `halo`, `fwhmCentre`, `fwhmEdge`,
  `trails`; background, sigma and channels cannot move).
- V-008, V-013, V-014 (`SeriesRun` in the harness, about 8 minutes for the three per variant),
  for today's rule and the one or two variants worth recommending: every recorded figure side
  by side, not stopping at the first difference: classes and bad frames, registered frames,
  moves, pauses, spots, median move, rotation, drift, session FWHM, focus held, dew, the
  walking figures and words, the polar figures, the pattern, and V-009 to V-011's words, tables
  and moves (the registration list changes when big saturated stars enter it, so the dithering
  figures are read as well).

### The checkpoint

In chat, in plain words, in Romanian: the tables, the options, one recommendation. With it:
whether `wide stars` and `Th.WideStarFwhm` go or stay, by what stays unmeasurable; the texts
that would change, original and proposal side by side, both languages; whether CR-013 is fixed
here; whether the change seems to warrant a version bump (the owner's call). Then the session
**waits for the owner's answer**.

## Stage 2: after the owner's choice

1. **Requirements.** R-014 (the area rule, the halo's rings), R-015 and R-016 where the choice
   touches them, R-088 removed or rewritten.
2. **Failing tests first**, in `tests/LightsCheck.Tests/FrameAnalyzerTests.cs` and
   `SeriesTests.cs`: bright wide stars drawn at 10 and 15 px are counted, measured and bright
   (red today); a knot and a trail stay out; the halo of a wide star reads as a narrow one's.
   The tests that pin the 400 px limit or the `wide stars` row follow their requirements.
3. **Code**, all in `LightsCheck.Core`: `Analysis/FrameAnalyzer.cs`, `Thresholds.cs`,
   `Analysis/StarShape.cs` (the halo), `Analysis/FrameMeasurement.cs` (doc comments),
   `Report/SeriesReport.cs` (`FocusRows`) and `Report/FrameReport.cs` if a text moves. XML
   documentation on everything added or edited.
4. **Golden files** regenerated, every changed line read and tied to the change.
5. **Documents.** `docs/design/frame-analysis.md` (steps 6 and 7, the constants, what was
   measured), `series-analysis.md` where it quotes the row; a new ADR (0059 if still free) with
   the tables; dated notes in ADR 0056 and wherever a stated value moves; CR-013's status if it
   was fixed.
6. **`/validate`** on the three sessions, the drift table pasted; `docs/validation.json`
   re-recorded only at the owner's word, with a History entry in `docs/validation.md`.
7. **CHANGELOG**, **README**, and the wiki page `LightsCheck.md` in
   `E:\Photografie\AstroDarkroomWiki`, on a branch of that clone made from `master`.
8. **`/wrap-up`**, a subagent's review of the branch (and one of the fix commit), then the
   merge proposed and asked. No merge and no push without the owner's word.

## Verification

- Stage 1: the harness reproduces today's figures before any variant (above); every table in
  the checkpoint comes from a pasted run.
- Stage 2: the new tests red before the code and green after; `scripts/verify.ps1` green, with
  a `--no-incremental -warnaserror` build before the last run; the golden diff read whole;
  `/validate` with the drift table; a run of the console on the three real sessions.

## What stage 1 measured, and the owner's answer (2026-11-15)

The figures are in ADR 0059; the harness stayed in the session's scratchpad. In short: a star
covers the same number of its widths squared at every width (3 to 13, more when saturated), so
400 px are 64 widths squared at 2.5 px and 20 at 4.5 px; with 64 the clumps and the cloud of
the Smida frames come in, with 20 they do not. The halo of the fixed rings reads the width
again above 3 px. A third size in pixels turned up, `Th.MaxFwhm` (15 px), which left a frame of
15 px stars without a width.

The options went to the owner with a recommendation: the limit of area at 20 widths squared
and never under 400 px, shapes kept to three widths and never under 15 px, the halo's rings
from each star's own width, a trail no longer a star (CR-013), the `wide stars` row and
`Th.WideStarFwhm` removed, and the frame report's halo line reworded as shown, in both
languages. The owner's answer: "Da, recomandarea". The version stays as it is; a bump was said
to seem warranted and is the owner's call.

## What deviated from this plan, and why

- `Th.MaxFwhm` joined the task: without it the limit of area would have counted the stars of
  15 px and still measured none.
- The variant per component's peak (A2) and the one that asks for a core near the frame's
  width (A3) were measured and dropped; the second throws out the stars of a doubled frame.
- The halo's rings follow each star's width, not the frame's (the plan's H1, where the plan
  leaned to neither): Smida's stars are half as wide again in the centre as at the edge.
- The half-scale comparison was run on a crop of three frames, not on the whole Veil frame of
  2026-11-06, with the enlarged ones; the frames with trails were seven of the eighteen that
  carry one.
- A whole session ran in about a minute and a half in the harness, not eight, so four
  variants were run on the three sessions.
- The drawn knot of the requirement's example is four star widths across at 25 sigma, not six
  at 12: the background's tiles take a fainter, wider knot out before the detection sees it.
- `docs/validation.json` is not re-recorded in this task's first commit: V-002, V-004 and V-005
  stop at their halo, as the change means them to, and wait for the owner's word.
- A subagent reviewed the branch and the wiki page after the first commit: no defect in the
  code; ten mutants of the rule, of which six passed every test. Six tests followed (the two
  floors, the lower side of the 20, a frame without a first width, the first width as a middle
  value, the rings of each star's own width, a bright trail among wide stars), after which each
  of the ten fails a test, and the documents were put in step with each other (the Smida
  frames whose trails were stars, "the brightest included", the unreleased CHANGELOG entry that
  still announced the row, the parts superseded in ADRs 0056 and 0006). What the review found
  and no change answers is in ADR 0059's Consequences: the first width follows bright patches
  once they are as many as the bright stars.
- A second subagent reviewed what followed the first, at the owner's word ("Review intai"): no
  defect and no wrong recorded figure. The upper limit of a kept shape's width had no test at
  all, and the bright streak of the trail test could not be measured for shape, so three of
  its assertions passed either way; two tests answer that, with objects the reviewer's probe
  found. A streak close to a row or a column is not called a trail by the rule of R-015, and
  among wide stars the larger limit of area now counts it as a star: said in ADR 0059, in
  R-015 and in the wiki, not changed.
