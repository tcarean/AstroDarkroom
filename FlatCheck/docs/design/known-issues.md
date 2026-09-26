# Known issues and gotchas

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** behaviour that is deliberate or not worth changing; read before "fixing" any of it.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## Known issues and gotchas

Verified by running the current build unless marked otherwise. The CLI-contract and
correctness bugs that used to live here were fixed in phase 8; what remains is
behaviour that is either deliberate or not worth changing.

1. **Card parsing is naive.** It splits the value at the first `/`, which corrupts any
   string value containing a slash (e.g. a path or date in a `COMMENT`-adjacent card).
   Fine for the numeric keys actually used.
2. **File extension is only half-validated.** The six raw extensions dispatch to
   `RawReader`; *everything else* goes to `FitsReader.Load`, so a `.jpg` surfaces as a
   FITS parse error rather than "unrecognised format". Python raises
   `Format nerecunoscut` instead. Left as is deliberately: the error still points at the
   right file, and tightening it would change behaviour no test asks for.
3. **`CornerStats` can throw on tiny images.** With `h < 8`, `h/2 - bh` goes negative
   and indexing throws. Not reachable with real frames.
4. **`_crcTable` used to be lazily initialised**, and that was a real race, not a
   theoretical one: the field was assigned *before* the table was filled, so a second
   thread arriving in that window saw a non-null table of zeros and wrote a wrong CRC -
   a corrupt PNG. Reproduced: 8 threads, 2000 rounds, 5261 wrong CRCs out of 32000. In
   the window it was reachable, since the Ratio tab can encode while Structure is still
   running. It is now a `static readonly` built by a static method, which the CLR
   publishes only when complete; `EncodingOnManyThreadsGivesTheSameBytesAsSerially` pins
   the determinism. Kept in this list so nobody reintroduces the lazy form.
5. **The corner-spread metric has a non-zero floor.** The analysis centre is
   `(w/2, h/2)`, but pixel indices run `0..w-1`, whose true middle is `(w-1)/2`. That
   half-pixel offset puts the top-left corner fractionally farther from the centre than
   the bottom-right, so even a *perfectly* radial field reports a spread. It scales as
   ~1/n: measured 2.88% at 100 px, 1.44% at 200, 0.56% at 512, 0.28% at 1024
   (cross-checked against a numpy reimplementation). At the ~1500x1000 analysis raster
   the tool actually uses it is ~0.2%, far below the 2% PERFECT band, so it is
   harmless in practice - but it is a systematic floor, not noise, and it would swamp
   the threshold on a small enough frame. Pinned by
   `CornerStats_HasAHalfPixelCentringArtifact_ThatHalvesAsTheFrameDoubles`.
   **Not a bug to fix**: changing the centre convention would move every parity golden for
   nothing.
6. **`RadialModel`'s `centers`/`prof` return values are discarded** by
   `StructureAnalyzer` - they fed the Python profile panel, which this port dropped.
7. **Memory is still heavy**, though phase 8 halved the worst of it: `Percentile` now
   has a `double[]` overload, so taking the full-resolution median no longer copies
   twice. Measured peak was ~330 MB on a 3000x2000 frame before that change; a 24 MP
   D3300 frame still runs into the high hundreds of MB. Runtime is fine
   (1.3 s structure / 0.6 s expo / 0.5 s light on 3000x2000).
8. **A raw whose only preview is full-size costs a decode of it.** The window's exposure
   tab decodes the camera preview; when a camera offers no mid-size one, the largest is
   decoded with `TargetSize`, which was measured at ~9 MB and ~0.5 s on a 6000x4000
   preview - the time is spent in entropy decoding regardless of the target. Every raw
   load also copies the chosen preview's bytes (a few MB at most), including modes that
   never look at them; that is the price of opening the file once.

