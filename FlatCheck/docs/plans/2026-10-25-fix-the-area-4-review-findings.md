# 2026-10-25 Fix the area-4 review findings

## Context

A high-effort code review of the window after area 4 of the documentation backfill (merged
into `main` as 8809cc6; the reviewed range 5e910f2..813355f) left eight findings. The owner
handed them over as this plan, with the process to follow: requirement first, then the test (a
behaviour change's test fails first), then the code; the gate green at each commit; `/validate`
on the real masters after any change to `Report/` or `Analysis/`, with every one of the figures
identical; no console golden may change, and any changed line of the 43 `Golden/parity_*.txt`
is a finding to stop on. `docs/design/two-applications.md` (the tab mechanics) and
`docs/design/code-map.md` (the `Report/` entries) are read first.

Branch `claude/Fix-the-area-4-review-findings`, in its own worktree.

## The findings

1. **The "sequential, not parallel" rule is enforced per tab.** Ratio, Netezire, Sintetic and
   Split await `MainViewModel.WhenIdleAsync`, but the Expo and Histogram tabs
   (`ExposureTabViewModel.RecomputeAsync`, `HistEqTabViewModel.RecomputeAsync`, attached at
   `MainViewModel.cs:221-224`) start at once on a click during the opening chain; the Histogram
   tab even lists the 24 MP master Structure is reading at that moment. And the click-runs never
   wait for each other (a Netezire run and a Ratio pair can run together). Design first, to the
   owner, before any code (the rule "Discuss before designing"): measure the peak memory of two
   overlapping runs on a real master, then propose (a) the same `waitIdle` seam for Expo and
   Histogram, or (b) one shared gate in `MainViewModel` (for example a `SemaphoreSlim` every
   heavy run takes); recommend one, wait for the answer, record it in an ADR, then extend
   R-081/R-085 and test it as R-082 is tested now.
2. **The Ratio busy text lies while a pick waits.** `RatioTabViewModel.RecomputeAsync`
   (`RatioTabViewModel.cs:299`) shows "Compar cele doua mastere de flat..." with an indeterminate
   bar for up to ~2 minutes while nothing is compared yet. Show a waiting text (in `BusyDetail`
   or a first `BusyMessage`) until the chain ends; the wording belongs in `SessionMessages`,
   Romanian. If item 1 extends the wait to other tabs, give them the same treatment.
3. **No test checks that `MainViewModel.OpenAsync` passes `WhenIdleAsync` to the Ratio tab**:
   `R082_APickDuringTheOpeningChainWaitsForIt` injects `waitIdle` itself. Add a
   `MainViewModelTests` case that holds the chain open and checks that a Ratio pair picked
   meanwhile does not finish until the chain does.
4. **`ExposureReport.NotRawAdvice` repeats `WriteNotRaw`'s two reason sentences with drifted
   wording** ("(langa zero/negativ) ->" vs "(langa zero) -", "--expo are nevoie" vs "Modul are
   nevoie"). Share the reasons as lines, as `VerdictLines` does for the verdict; keep only the
   "solutions" paragraph different (the console names `flatcheck flat.NEF --expo`, the window
   the application). The console text stays byte-identical.
5. **`ExposureReport.ChannelsSummary` recomputes the per-channel percentage** that `Write`
   computes inline. One helper for both, as `StructureReport.CornerSharePct` is.
6. **`LightReport.TooWideText` omits the console's focal-length note** ("Decizia s-a luat doar
   pe focala, presupunand cel mai mare senzor", in `WriteTooWide`). Let `TooWideText` take the
   `SuitabilityResult` and add that sentence without the `--fov` advice; update
   `LightTabViewModel` and the tests.
7. **`LightReport.FieldText` duplicates `WriteSuitability`'s three-way branch.** Extract the
   value part once; the console pads it, the window shows it plain. Console output unchanged.
8. **`ExposureReportTests.TheWindowsAdviceIsTheConsolesVerdictBlock` covers six levels but no
   clipping branch and not the not-a-raw path.** Add those cases with the file's `Clipped()`
   helper.

## Order of work

1. The worktree and the branch; this plan.
2. The memory measurement for item 1 on the Samyang ISO 1600 f/2 session (the 24 MP master
   `pp_flat_stacked.fit`, the ISO 400 f/2 master as the Ratio pair, `DSC_0061.NEF` for Expo,
   the six-frame light series): each heavy run alone, then the overlaps the window allows
   today, one test host process per scenario, `Process.PeakWorkingSet64`.
3. Items 4, 5, 7 (report refactors; the console text byte-identical, the goldens the proof),
   item 8 (tests only), item 6 (requirement, failing test, code), item 3 (test only), item 2
   (requirement, failing test, code), each its own commit with the gate green.
4. Item 1's facts, options and recommendation to the owner; wait. After the answer: the ADR,
   the requirement rows, the tests, the code, and item 2's treatment of any tab the wait now
   covers.
5. `/wrap-up`: `/validate` (every figure identical), CHANGELOG, `check-design.ps1 -Since main`,
   the merge proposal.
