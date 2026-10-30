# Fix the flaky window test of the Exposure tab by exposing the pick's run

Plan for the task branch `claude/Fix-the-exposure-tab-run-overlap`, worktree
`FlatCheck/.claude/worktrees/Fix-the-exposure-tab-run-overlap`, cut from `main` (33813f6).
Approved by the owner on 2026-10-30.

## Context

`ExposureTabTests.PickingAnotherFileRecomputes` failed once in a full `scripts/verify.ps1` run
on 2026-10-27 (`Assert.NotEqual(first, vm.Centre)`: the dim file's run showed the bright file's
centre) and passed alone three times after. The owner asked for the cause, a deterministic
reproduction and a fix the project's way, with one standing instruction: nothing is fixed that
the real window cannot do, and where it can, the least-risk fix.

## Measured facts (2026-10-30, Release, the task worktree)

1. Setting `Selected` starts a run nobody awaits (`Recompute`: `_ = RecomputeAsync()`); the
   tests then `await vm.RecomputeAsync()`, a second run on the same file. The tests attach no
   `HeavyRunGate`, so the two runs compute at once; `TabViewModel.RunGuardedAsync` numbers its
   runs but guards only how a run ends, and the work lambda writes `Report`, `Populate` and
   `HasResult` inside the work, so the first file's unawaited run can land after the second
   file's awaited run.
2. The test's own body, 30 rounds alone: 1 failure. The first file 2048x2048 and the second
   128x128, no gate: the stale run overwrote the new pick 3 of 3. The same two picks with a
   `HeavyRunGate`: the latest pick landed last 3 of 3, because a run holds the lease through
   its writes and the semaphore's waiters are served in order. The window always attaches the
   gate (`MainViewModel.OpenAsync`), so there the last pick wins.
3. The Exposure tab's busy overlay sits in the result column (`ExposureView.xaml`), so the list
   and the Sky switch stay clickable during a run; the gate orders the runs they start.
4. The folder button is disabled by `MainViewModel.IsRunning`, which only the opening chain
   sets. The Exposure lambda does not re-check its token after its computation, so a folder
   change during an Exposure run lands the old folder's figures on the reset tab (reproduced
   deterministically with a queued `SynchronizationContext`). The owner chose to leave this as
   it is (ADR 0077). Corrected after the review of 2026-10-30: the plan first said Exposure was
   alone among the click-run tabs; Smoothing and Synthetic check the token on the pool thread
   at the end of the computation and have the same hole in a narrower window, Histogram and
   Split check after the hop and do not.

## Decision (the owner, 2026-10-30)

Fix the flake only, by exposing `Pending` on the Exposure tab: `Recompute` keeps the run it
starts in `Pending` (the shape `HistEqTabViewModel`, `SmoothTabViewModel` and
`SyntheticTabViewModel` already have), and the window tests await it instead of starting a
second run. No token check after the await, no lock on the folder button.

## Steps

1. The test first: `ExposureTabTests.APickKeepsItsRunInPendingUntilItLands` (a pick's run is
   in `Pending`, not complete until the figures are there; the Sky switch's run too). It does
   not compile before the change.
2. `ExposureTabViewModel.Pending`, set by `Recompute`; `RecomputeAsync` becomes private, since
   no test calls it once they all await `Pending` (the review found the smoke tests among them).
3. Every `await vm.RecomputeAsync()` that follows a `Selected` or `Sky` setter in
   `ExposureTabTests`, and the `Task expo` of `MainViewModelTests`, awaits `Pending`.
4. `docs/design/two-applications.md`: the Exposure tab keeps its run in `Pending`; the gate,
   not the overlay, orders two picks on the Exposure tab. ADR 0077. A CHANGELOG line under
   `Internal`.
5. The gate, the commit on the branch, the merge proposal.
