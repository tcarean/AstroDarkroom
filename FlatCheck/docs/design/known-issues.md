# Known issues and gotchas

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): items 5 and 8 are ADR 0013 and ADR 0024 and
> stay here as numbered pointers, since other notes and tests cite them by number. **Covers:**
> behaviour that is deliberate or not worth changing, by pointer in the files its items name:
> `FitsReader.cs`, `ImageLoader.cs`, `RawReader.cs`, `PreviewReader.cs`, `Metrics.cs`,
> `Stats.cs`, `Analysis/StructureAnalyzer.cs`, `PngWriter.cs` and, for item 9, the run bodies of
> `FlatCheck.Wpf/ViewModels/ExposureTabViewModel.cs`,
> `SyntheticTabViewModel.cs`, `HistEqTabViewModel.cs` and `SplitTabViewModel.cs`; read before
> "fixing" any of it.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## Known issues and gotchas

Verified by running the current build unless marked otherwise. The CLI-contract and
correctness bugs that used to live here were fixed in phase 8.

1. **Card parsing is naive.** It splits the value at the first `/`, which corrupts any
   string value containing a slash (e.g. a path or date in a `COMMENT`-adjacent card).
   Fine for the keys actually used: numbers, and strings without a slash (`BAYERPAT`,
   `ROWORDER`, `INSTRUME`, `DATE-OBS`).
2. **File extension is only half-validated.** The seven raw extensions dispatch to
   `RawReader`; *everything else* goes to `FitsReader.Load`, so a `.jpg` surfaces as a
   FITS error, `FITS: fisierul nu incepe cu SIMPLE.` since the magic check of ADR 0057,
   rather than "unrecognised format". Python raises
   `Format nerecunoscut` instead. Left as is deliberately: the error still points at the
   right file, and tightening it would change behaviour no test asks for.
3. **`CornerStats` can throw on tiny images.** With `h < 8`, `h/2 - bh` goes negative
   and indexing throws. Not reachable with real frames.
4. **`_crcTable` used to be lazily initialised**, and that was a real race, not a
   theoretical one: the field was assigned *before* the table was filled, so a second
   thread arriving in that window saw a non-null table of zeros and wrote a wrong CRC -
   a corrupt PNG. Reproduced: 8 threads, 2000 rounds, 5261 wrong CRCs out of 32000. In
   the window it was reachable, since a scale chip on one tab can encode while Structure is
   still running. It is now a `static readonly` built by a static method, which the CLR
   publishes only when complete; `EncodingOnManyThreadsGivesTheSameBytesAsSerially` pins
   the determinism. Kept in this list so nobody reintroduces the lazy form.
5. **The corner-spread metric has a non-zero floor.** ADR 0013 holds it: the analysis centre
   is `(w/2, h/2)` while the true middle of pixel indices `0..w-1` is `(w-1)/2`, so even a
   perfectly radial field reports a spread, fading as ~1/n and harmless at the analysis
   raster; why it is not a bug to fix; and the test that pins it. The parity goldens pin the
   convention now, so changing it would move every one of them for nothing.
6. **`RadialModel`'s `centers`/`prof` return values are discarded** by
   `StructureAnalyzer` - they fed the Python profile panel, which this port dropped.
7. **Memory is still heavy**, though phase 8 halved the worst of it: `Percentile` now
   has a `double[]` overload, so taking the full-resolution median no longer copies
   twice. Measured peak was ~330 MB on a 3000x2000 frame before that change. On the 24 MP
   D3300 masters, measured 2026-10-25 as peak working set of one test host process (ADR 0061
   holds the table): 809 MB structure, 729 MB histeq, 1079 MB a six-frame light series,
   1113 MB the ratio mode of the time, 1135 MB the smoothing mode of the time, 1249 MB expo with the preview decoded; two runs at once add
   up (1.3-2.2 GB), which is why the window lets one heavy run compute at a time. Runtime is
   fine (1.3 s structure / 0.6 s expo / 0.5 s light on 3000x2000; 1-5 s per run on 24 MP).
8. **A raw whose only preview is full-size costs a decode of it.** ADR 0024 holds the figures
   (~9 MB and ~0.5 s with `TargetSize` on a 6000x4000 preview, because the time is spent in
   entropy decoding whatever the target) and the copy of the chosen preview's bytes that every
   raw load makes, in modes that never look at them; that is the price of opening the file
   once.
9. **A folder change during an Exposure run lands the old folder's figures.** The folder button
   is live during an Exposure run (`MainViewModel.CanChooseFolder` locks it only for the
   opening chain and while the Synthetic tab is busy, R-117), and the Exposure tab's run
   lambda does not re-check its token after its computation: a run that was computing when the
   folder changed writes its report, figures and `HasResult` on the tab the new folder reset,
   under the new folder's list with nothing selected, until the next pick. Reproduced
   deterministically on 2026-10-30; the owner left it as it is, since the run takes about four
   seconds on a 24 MP raw, too short for the folder to be changed under it (ADR 0077, where
   the one-line check is the alternative rejected). The Synthetic lambda checks
   the token on the pool thread at the end of the computation and would land its figures the
   same way in the narrower window between that check and the continuation, but the button is
   locked while it runs; the Histogram lambda and the Split's run body check after the hop
   back and land nothing.
