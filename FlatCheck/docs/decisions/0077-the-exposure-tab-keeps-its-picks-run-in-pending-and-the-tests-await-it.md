# 0077. The Exposure tab keeps its pick's run in Pending and the tests await it, nothing else

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

`ExposureTabTests.PickingAnotherFileRecomputes` failed once in a full gate run on 2026-10-27
and passed alone after. Setting `Selected` or `Sky` starts a run nobody awaits
(`Recompute`: `_ = RecomputeAsync()`), and the window tests then awaited a second run on the
same file, `await vm.RecomputeAsync()`. The tests attach no `HeavyRunGate`, so the two runs
computed at once; `TabViewModel.RunGuardedAsync` numbers its runs but guards only how a run
ends, while the work lambda writes `Report`, `Populate` and `HasResult` inside the work. The
first file's unawaited run could therefore land after the second file's awaited run.

Measured 2026-10-30 in the task worktree, Release: the test's own body failed 1 round in 30
alone; with the first file 2048x2048 and the second 128x128 and no gate, the stale run
overwrote the new pick 3 of 3; the same two picks with a gate, the latest pick landed last
3 of 3, because the first pick takes the lease synchronously and holds it through its writes
(the semaphore also serves its waiters in order, an implementation detail of `SemaphoreSlim`,
not a contract; it matters only when both picks queue behind the opening chain). The window
always attaches the gate (`MainViewModel.OpenAsync`), so a second pick there, which the
overlay does not stop (it covers the result column, not the list), ends with the latest pick's
figures. The flake is the harness's, not the window's.

Two more facts went to the owner. The folder button is disabled by `MainViewModel.IsRunning`,
which only the opening chain sets, and the Exposure lambda does not re-check its token after
its computation: a folder change during an Exposure run lands the old folder's figures on the
reset tab, reproduced deterministically with a queued `SynchronizationContext` (the
computation done, the token cancelled and the tab reset before its continuation ran). The
review of the branch (2026-10-30, after the decision) found the hole wider than the owner was
told: the Histogram and Split lambdas re-check the token after the hop back to the UI thread,
but the Smoothing and Synthetic lambdas check it on the pool thread at the end of the
computation, so the same reproduction lands their figures too, in the narrower window between
that check and the continuation; only Exposure checks nowhere after the computation. The
owner's standing instruction for the task: fix nothing the real window cannot do, and where
it can, the least-risk fix, without overcomplicating.

## Decision

The owner chose, on 2026-10-30, to fix the flake only: `ExposureTabViewModel.Recompute` keeps
the run it starts in `Pending` (the shape `HistEqTabViewModel`, `SmoothTabViewModel` and
`SyntheticTabViewModel` already have), and the window tests await that run instead of starting
a second one beside it. `RecomputeAsync` becomes private: nothing but `Recompute` calls it
now. Nothing else changes: no token check after the await in the Exposure lambda, no lock
on the folder button, no generation check inside the work. The folder-change case is recorded
in `known-issues.md` as deliberate.

## Alternatives rejected

- **A gate in the tests.** `Attached` handing in a `HeavyRunGate` as the window does would order
  the two runs, but every pick would still compute twice, and the second run would still be
  the one the test awaits rather than the one the user's click starts.
- **A token check after the await, as the sibling tabs.** One line, and it would drop the old
  folder's figures on a folder change during an Exposure run; the owner left the case as it
  is, since the test's flake is not that case and the fix would change the window.
- **Locking the folder button while any tab is busy.** The owner's first thought for a real
  case; it greys the button for every pick and run of every tab and contradicts ADR 0061's
  consequence that a new folder's chain waits for a run of the old folder. Not needed for the
  flake.

## Consequences

- `PickingAnotherFileRecomputes` and the other Exposure tests await the one run a pick starts;
  `APickKeepsItsRunInPendingUntilItLands` pins that `Pending` is that run. The two window
  smoke tests of the Exposure tab await it too.
- A folder change during an Exposure run still lands the old folder's figures on the reset tab
  until a new pick replaces them, and a Smoothing or Synthetic run cancelled between its last
  check and its continuation does the same (`known-issues.md`, item 9).
- `two-applications.md` says which tabs can overlap runs and what orders them.
