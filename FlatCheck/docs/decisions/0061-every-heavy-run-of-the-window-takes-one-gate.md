# 0061. Every heavy run of the window takes one gate

Date: 2026-10-25
Status: accepted 2026-10-25

## Context

The window's "sequential, not parallel" rule (the `MainViewModel` header) said that two chains
loading 24 MP frames at once could pass a gigabyte, and kept it per tab: the opening chain ran
Structure then the light series one after the other, and Ratio, Netezire, Sintetic and Split
awaited a `WhenIdleAsync` seam that completed when the chain ended. The review of area 4 of the
documentation backfill (2026-10-25) found the rule enforced only there: the Expo and Histogram
tabs started at once on a click during the chain, the Histogram tab offering the very master
Structure was reading, and no click-run waited for another (a Netezire run and a Ratio pair
could run together). Measured the same day on the Samyang ISO 1600 f/2 session (24 MP masters,
one test host process per scenario, Release, workstation GC, `Process.PeakWorkingSet64`,
baseline 59 MB):

| scenario | peak working set MB |
| --- | --- |
| Structure alone | 809 |
| Ratio alone | 1113 |
| HistEq alone (the master) | 729 |
| Expo alone (a NEF with its preview) | 1249 |
| Smooth alone | 1135 |
| Light series alone (6 frames) | 1079 |
| Structure then Ratio, sequential | 1257 |
| Structure + Ratio | 1497 |
| Structure + HistEq | 1274 |
| Structure + Expo | 1772 |
| Light series + Ratio | 2193 |
| Light series + HistEq | 1838 |
| Ratio + Smooth (two click-runs) | 1739 |
| Structure + Ratio + HistEq | 2148 |

One run alone peaks at 0.7-1.25 GB, above the "high hundreds of MB" `known-issues.md` recorded
from a 3000x2000 frame; two at once roughly add up; the click-versus-click overlap the rule never
mentioned is as large as the chain overlaps it forbade.

## Decision

One `HeavyRunGate` (a `SemaphoreSlim(1, 1)` behind a lease) owned by `MainViewModel`, taken by
every heavy run: the opening chain once, from the structure run to the end of the series, and
each run a click starts around its computation - Ratio, Expo, Histogram, Netezire (the run and
the proposal), Sintetic (the write and the comparison), and the Split's analysis of the part
masters. The Split waits for the gate before its copies start, as it waited for the chain, and
does not hold it through the Siril runs, which are other processes. The gate wait takes the
run's token, so a superseded or cancelled run leaves the queue without computing; the waiting
screen says it is waiting while another run holds the gate (`TabViewModel.EnterGateAsync`). The
`WhenIdleAsync` seam is gone; the tests hand in a gate they hold. A chain started on a new folder
while a run of the previous folder still computes waits for it the same way, a cancellation of
that wait being no fault. The owner chose this on 2026-10-25 over the per-tab seam.

## Alternatives rejected

- **The same `WhenIdleAsync` seam for Expo and Histogram.** Two more lines each, but it leaves
  every click-versus-click overlap (1.7 GB measured on Ratio + Smooth) and the new-folder
  overlap, where a Ratio run of the old folder computes beside the new chain.
- **The chain's two runs each taking the gate.** Uniform, but a click-run queued behind
  Structure would run before the light series, so a pick during the chain no longer waited for
  the whole chain as it had; the chain takes the gate once instead.
- **Holding the gate through the Split's Siril runs.** The runs are other processes and take
  up to twenty minutes; the other tabs would be dead meanwhile for no memory saved.

## Consequences

- No two heavy runs of the window load frames at once, whatever the user clicks; a click during
  the chain waits for the whole chain, a second click waits for the first, and the waiting
  screen says so. A new folder's chain may wait a few seconds for a run of the old folder.
- R-081 (implemented), R-082, R-084 (implemented) and R-085 name the gate; `HeavyRunGateTests`
  pins the gate itself, `R081_*` in `MainViewModelTests` the chain holding it and waiting for
  it, `R082_*`, `R084_*` and `R085_*` the tabs taking it; `TabViewModelTests` the waiting text.
- Every tab's `Attach` takes the gate (null in a test that wants the run at once);
  `MainViewModel`'s constructor takes one too, for the tests that hold it.
- `known-issues.md` point 7 carries the 2026-10-25 figures.
