# 0079. The window shows a loader instead of the tabs until the opening analyses end

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

Opening a folder starts three things by themselves: the structure run, then the light series
(the opening chain, one gate, R-081, ADR 0061), and beside them the Synthetic tab's prefill,
which loads the lens catalog. Until now the seven tabs were shown at once, each behind its own
waiting screen, and a click in another tab queued behind the chain. The review of the "no
filters" box (ADR 0078, 2026-10-30) found two races that this openness allows: a tick on the
Structure tab made while the chain still waits for the gate reaches a tab that has neither its
folder nor its settings yet, so it writes nowhere and the run then overrides it from the file;
and a flip during the chain's light series starts a recompare outside the gate, whose result
can land on the next folder's tab if the folder is changed meanwhile. Both are of the same class
as the Exposure tab's run-overlap finding (2026-10-28), which ADR 0077 closed the same day by
locking the folder button during the long runs.

## Decision

While the opening analyses run, the window shows no tab. In their place a panel lists the
three steps - the structure of the master (with the lens profile), the end-to-end test on the
lights, the lens list for the Synthetic tab - each with its state (waiting, running, done, or
the tab's own refusal or failure title), the running step's waiting line and progress bar, and
the folder's path. `MainViewModel.IsRunning`, which already locks the folder button, now lasts
until the prefill has ended too, and the tabs appear only when it drops. The owner chose this
over per-tab guards on 2026-10-30: nothing can be clicked, ticked or picked before every run
of the opening has read the folder and finished. One guard stays in code beside it: a run of
the Structure tab that returns to the UI thread after its folder was closed shows nothing
(`_session` is compared after the await), since a recompare started after the chain and a
folder changed during it are still possible.

## Alternatives rejected

- **Guards per tab** (a `Structure.Attach` before the chain, token checks after every await, a
  box disabled until its run has started). Each closes one window and leaves the next open;
  the Exposure tab's open finding is one of them.
- **Keep the tabs and disable only the box during the chain.** The lists of the Exposure and
  Histogram tabs stay clickable, and a pick there still queues behind the chain with a waiting
  screen the user did not ask for; the loader says what is running instead.
- **Hide the tabs for the chain only, not the prefill.** The Synthetic tab would appear with its
  fields filling in; the owner asked that everything be done before anything can be pressed.

## Consequences

- R-120 states the panel; R-081, R-084 and R-085 keep their gate rule, which a click can no
  longer exercise from the screen during the chain (it still holds for two click-runs).
- `OpeningStep` is a small view-model over an existing tab's busy properties; it computes
  nothing. The steps' labels and state words are `T` pairs.
- A folder opened over a running one no longer drops `IsRunning` when the older chain ends:
  the flag follows the latest opening, which also clears it when it stops at the problem card.
  From the screen this cannot happen (the button is locked, and the card's Siril run exists only
  without a chain); a direct call, as the tests make, can, and the counter is for that.
- While the chain waits for the gate a click's run of the previous folder still holds, the
  panel sits with its rows on `waiting` (the prefill's on `running...`) until that run ends;
  before, the tabs were on screen meanwhile. The prefill's row follows the prefill's own task.
- The waiting line a click's run shows while another holds the gate no longer names the
  opening analyses, which cannot hold it while a tab is on screen: it names the running one.
- Guarded by `R120_*` in `MainViewModelTests` and a render in `WindowSmokeTests`.
