# 0077. The Exposure tab keeps its pick's run in Pending, and the folder button locks during the long runs

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

Two more facts went to the owner. The folder button was disabled by `MainViewModel.IsRunning`
alone, which only the opening chain sets, and the Exposure lambda does not re-check its token
after its computation: a folder change during an Exposure run lands the old folder's figures
on the reset tab, reproduced deterministically with a queued `SynchronizationContext` (the
computation done, the token cancelled and the tab reset before its continuation ran). The
review of the branch (2026-10-30, after the first decision) found the hole wider than the
owner was told: the Histogram lambda and the Split's run body re-check the token after the
hop back to the UI thread, but the Smoothing and Synthetic lambdas check it on the pool
thread at the end of the computation, so the same reproduction lands their figures too, in
the narrower window between that check and the continuation (their file-writing runs then end
as cancelled at the write's `Task.Run` and hide them again); only Exposure checks nowhere
after the computation. The owner's standing instruction for the task: fix nothing the real
window cannot do, and where it can, the least-risk fix, without overcomplicating.

Put back to the owner with the wider fact, the owner asked how long the runs take. Measured
2026-10-30 on a copy of the Tokina sky master (24 MP, V-037's session) with the Release
console from the task worktree, wall time of the whole process, three runs each:

| run | cold | warm |
| --- | --- | --- |
| synthetic write (`--synthetic`, Adobe profile) | 10.1 s | 3.6-4.0 s |
| the comparison the window runs after it (`--against`) | | 4.0-4.4 s |
| exposure of the first NEF (`--expo`) | | 3.9-4.1 s |
| smoothing (`--smooth`) | | 6.4-6.7 s |
| the structure run with the raw (the opening chain's first half) | | 8.1 s |

The window's Synthetic button runs the write and then the comparison: about 8 s warm, 14 s
cold. The window's exposure run also decodes the preview, about half a second more (ADR 0024).

## Decision

The owner chose, on 2026-10-30, in two answers:

- Fix the flake only, by `Pending`: `ExposureTabViewModel.Recompute` keeps the run it starts
  in `Pending` (the shape `HistEqTabViewModel`, `SmoothTabViewModel` and
  `SyntheticTabViewModel` already have), and the window tests await that run instead of
  starting a second one beside it. `RecomputeAsync` becomes private: nothing but `Recompute`
  calls it now. No token check after the await in the Exposure lambda, no generation check
  inside the work.
- The Exposure case stays as it is, on purpose: the run takes a few seconds, too short for
  the folder to be changed under it. It is `known-issues.md` item 9.
- A run that takes more than 4-5 seconds locks the folder button while it runs, the simplest
  fix: `MainViewModel.CanChooseFolder` is false during the opening chain and while the
  Synthetic tab is busy. The Smoothing run, measured over the same threshold, is included by
  the same rule; that extension is the assistant's, said at wrap-up (R-117). The lock is per
  tab, one property on the tabs' busy state, so it also holds during the Smoothing tab's
  proposal (one smoothing per step of the scale ladder, not measured) and the Synthetic tab's
  stand-alone comparison (4.0-4.4 s, under the threshold): accepted for the simplicity of one
  rule over a per-run list. The Exposure, Histogram and Split runs leave the button live.

## Alternatives rejected

- **A gate in the tests.** `Attached` handing in a `HeavyRunGate` as the window does would order
  the two runs, but every pick would still compute twice, and the second run would still be
  the one the test awaits rather than the one the user's click starts.
- **A token check after the await, as the Histogram and Split runs.** One line per tab, and it
  would drop the old folder's figures on a folder change; the owner declined it for Exposure,
  whose run is too short to matter, and for the long runs chose the button over a check.
- **Locking the folder button while any tab is busy.** Uniform, but it would grey the button
  for every Exposure pick and Histogram click, which the owner did not want fixed, and it
  contradicts ADR 0061's consequence that a new folder's chain waits for a run of the old
  folder, which still holds for those runs.

## Consequences

- `PickingAnotherFileRecomputes` and the other Exposure tests await the one run a pick starts;
  `APickKeepsItsRunInPendingUntilItLands` pins that `Pending` is that run. The two window
  smoke tests of the Exposure tab await it too.
- A folder change during an Exposure run still lands the old folder's figures on the reset tab
  until a new pick replaces them (`known-issues.md`, item 9). During a Smoothing or a
  Synthetic run the folder button is disabled, so their narrower window cannot be hit from the
  window; `R117_TheFolderButtonIsLockedDuringASmoothingOrSyntheticRun` pins the lock.
- `two-applications.md` says which tabs can overlap runs, what orders them, and what the
  folder button follows.
- Changing the folder was the one way to abandon a Smoothing or Synthetic run started by
  mistake, neither tab having a stop button; now only closing the window is. A Smoothing
  click made during the opening chain locks the button for the chain and the run together,
  about 15 s warm on a 24 MP master. The window's comment on the button already said it: less
  confusing not to be able to than to lose the work.
