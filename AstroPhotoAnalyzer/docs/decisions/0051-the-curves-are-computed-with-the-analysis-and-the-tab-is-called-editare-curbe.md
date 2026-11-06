# 0051. The curves are computed with the analysis, and the tab is called Editare Curbe

Date: 2026-10-27
Status: accepted 2026-10-27; the `Calculeaza` button that recomputed the gallery at the area's size superseded by 0084
Supersedes: 0032 in part (decision 7, the suggestions computed on demand)

## Context

ADR 0032 (2026-10-05) made the suggested curves a second run beside the analysis, started only
by the tab's `Calculeaza` button: the run costs a few seconds (the previews are rendered at the
gallery's size) and is not diagnosis, so the tab stayed empty until asked. In use the owner
opens the tab for the curves every time, and asked on 2026-10-27 for them to be there already
("La incarcarea aplicatiei as vrea sa fie automat calculata curba pentru print. Cand ajung pe
tabul asta sa fie deja calculata"), and for the tab to be renamed "Curves Editing", in Romanian
"Editare Curbe". One fact shaped the implementation: a WPF `TabControl` lays out only the
selected tab's content, so before the tab is first shown the view has never reported the
gallery area's size, which the previews are rendered to fit.

## Decision

The tab is "Editare Curbe" (`Curves Editing`). `MainViewModel.RunAsync` starts the curves'
computation as soon as the analysis has filled the other tabs, and does not wait for it: the
busy overlay goes, the tab says "se calculeaza curbele..." until the gallery is there. The
computation is `ComputeAsync` as before, at the size the view last reported or, before the tab
was ever shown, at `Thresholds.CurvePreviewDefaultWidth` x `Height` (1600 x 900, a Full HD
screen under the parameter bar); the view only shrinks a preview to the area, never enlarges
it. `Calculeaza` stays, and computes the gallery again at the area's current size. The tab
view-model does not start the computation itself: `Populate` keeps the result as before, so the
tab's tests drive it synchronously, and the sequence "analysis, then curves" belongs to the
view-model that owns the run.

## Alternatives rejected

- **Starting the computation in `CurvesTabViewModel.Populate`.** Every test that populates the
  tab and then calls `Compute` would start a pool-thread run it then drops, and `Calculeaza`
  would be enabled while that run was on; the trigger belongs with the run's sequence.
- **Computing inside the analysis run, under the busy overlay.** The window would stay blocked
  for the seconds the previews take, for curves the user may not look at on this image; the
  other tabs are ready first and the curves follow.
- **Recomputing when the tab first reports its real size.** The default previews fit the
  maximised window on a Full HD screen as it is; on a larger screen they show smaller than the
  area, and one press of `Calculeaza` renders them at full size. A computation on every
  `SizeChanged` would also run again at every resize.
- **Removing `Calculeaza`.** It is the only way to render the previews at the gallery's real
  size after the first computation, and after a resize.

## Consequences

- R-110, R-111 and R-146 name the tab Editare Curbe and say the gallery comes with the
  analysis; `ViewModelTests.R146_TheCurvesAreComputedWithTheAnalysisWithoutAPress` awaits
  `MainViewModel.CurvesComputation` after `OpenAsync` and finds the gallery, at the default size,
  and no computation after a run that fails. The smoke and English renders of the tab await the
  same task instead of calling `Compute`; the empty render of the tab is gone, since after an
  analysis the tab is never empty.
- `CLAUDE.md`, `README.md`, `docs/design/window.md` and `extending.md` no longer say "on
  request" for the curves; the saved FITS is still written only on request (ADR 0032).
- A screen larger than Full HD shows the first previews smaller than the gallery area until
  `Calculeaza` is pressed; `docs/design/known-limitations.md` does not list it, since the button
  is on the tab.
