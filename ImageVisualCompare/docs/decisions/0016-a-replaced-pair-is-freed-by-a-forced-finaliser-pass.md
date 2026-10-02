# 0016. A replaced pair is freed by a forced finaliser pass before the new one is shown; the memory readout ticks every 2 s

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Memory, measured": the "after release"
row of the table and the two paragraphs after it), the `MemoryHousekeeping.ReleaseImageBuffers`
comment, the `ShowPair` and `_memoryTimer` comments in `MainWindow.xaml.cs`, and `README.md`
("Memorie"); the decision itself dates from a6e5f10 (the base version, 2026-09-08), and the
figures were written up in 591f433 (the documentation commit, 2026-09-08). Measured on a
5794 x 3637 16-bit pair of about 108 MB a file, a headless load of both images left the process
at 173 MB private while the managed heap never rose above 1 MB: WIC keeps decoded pixels in
unmanaged memory, 84 MB of Bgra32 an image. Rendering adds what WPF's render thread keeps (the
scaled intermediates of the Fant resampling path), up to a bounded plateau of about 570 MB
private, and the render thread does its work after the UI thread has finished.

## Decision

- When a pair replaces another, `MainWindow.ShowPair` first drops the outgoing one (`_pair` set
  to null, `Compare.Clear()` takes both bitmaps off the view), calls
  `MemoryHousekeeping.ReleaseImageBuffers()`, and only then puts the incoming pair on the view.
- `ReleaseImageBuffers` is a forced, blocking collection of every generation
  (`GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true)`),
  `GC.WaitForPendingFinalizers()`, and the same collection again. The finaliser pass frees the
  unmanaged buffer behind a discarded bitmap; the collections either side make it run now.
  Nothing compacts the large object heap.
- The memory readout, `MemoryHousekeeping.Snapshot()` (the managed heap and the process's
  `PrivateMemorySize64`, shown as `managed 2 MB, process 560 MB` in the README's example), is
  read once when a pair is shown and then every 2 s by a `DispatcherTimer` (`_memoryTimer`,
  `TimeSpan.FromSeconds(2)`), never on a view change.

## Alternatives rejected

- **Large object heap compaction.** There is nothing on the large object heap to compact: the
  managed heap never rose above 1 MB while the process held 173 MB (the `ReleaseImageBuffers`
  comment).
- **Leave the old pair to the collector.** Its finaliser pass would run "whenever the collector
  next feels like it" (the same comment), so the outgoing pair could still be held when the
  incoming one is shown, which the order in `ShowPair` exists to prevent.
- **Read the memory on every view change.** The render thread does its work after the UI
  thread, so a reading taken during a zoom is always stale and reports less than the process
  really uses (the `_memoryTimer` comment).

## Consequences

- Measured effect: 173 MB private back down to 12 MB, and the working set from 199 MB to 38 MB
  (`CLAUDE.md`, "Memory, measured"); that table is kept as history in `docs/validation.md`
  (ADR 0005).
- Replacing a pair runs two blocking full collections and a finaliser pass on the UI thread.
- `ShowPair` runs after the incoming pair has been decoded, so during that decode the outgoing
  pair is still held and on screen; what the order guarantees is that the two are not held
  together once the incoming pair is shown.
- The readout lags the process by up to 2 s.
- Guards: none by unit test (`MemoryHousekeeping` and `MainWindow` have none); validation cases
  V-001 and V-002 of `ValidationTests` guard the private memory a load adds and what the release
  gives back, within 60 MB (ADR 0005). The order in `ShowPair` and the timer are seen only by
  launching the application (ADR 0026).

Source: CLAUDE.md ("Memory, measured", "Testing"); src/ImageVisualCompare.Wpf/MainWindow.xaml.cs;
src/ImageVisualCompare.Wpf/Imaging/MemoryHousekeeping.cs;
README.md ("Memorie", "Ce îți arată bara de stare").
