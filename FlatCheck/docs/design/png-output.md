# PNG output: the residual map, its scale and the encoder

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): the hand-rolled encoder and its three-way
> verification are ADR 0008, the fixed caps ADR 0012, why files stay on the automatic scale ADR
> 0036. **Covers:** `PngWriter.cs` (`EncodeResidualMap`,
> `MapRange`, `FixedCapsPct`), `Report/RunReport.cs` (`MapScaleLabel`, `MapLegend`); the
> encoder's bytes and `PngWriterTests.cs` by pointer.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## PNG output

`PngWriter.EncodeResidualMap` produces a diverging blue → white → red heat map, with white
pinned at **1.0**. Pinning at 1.0 is valid for every caller: the structure residual is
`img/model ≈ 1`, both attribution maps are centred on 1 (the residual of the combined
master, and `A/B` normalised to median 1), and the `--light` map is `1 + radial/a`. If you
add a caller whose data isn't centred on 1, the map will be misleading.

The **ends** come in as a `MapRange`, of two kinds. `AutoRange` is the 1st/99th percentile
plus 10% padding - the behaviour since 1.0, and the only one the console and every file on
disk use (ADR 0036, which also holds the measurement that made a fixed scale necessary).
`FixedRange(cap)` is symmetric, `1 ± cap/100`. Two auto-scaled maps can only be compared by
shape.

**The fixed caps are not new numbers** (ADR 0012): `PngWriter.FixedCapsPct` is
`{ Th.ResidPerfect, Th.ResidUsable, Th.ResidCheck }` and `DefaultCapPct` is `Th.ResidUsable`;
`TheFixedCapsAreTheResidualScaleBoundaries` is what stops the two from drifting.

**But a cap is not a band, and the docs used to say it was.** The cap is a per-pixel
deviation from the model; the bands are peak-to-peak amplitudes. A one-sided +10% blotch
reaches the 10% cap right at the `BUN` boundary; a 10% gradient runs from -5% to +5% and
reaches only half of it, saturating at 20%, i.e. at `REFA`. Measured: at a 10% gradient
the map's p1/p99 sit at -4.9/+4.8%. There is no cap that is exact for both, so the README
explains it under "The colour scale" and the numbers stay as they are.

**The console was deliberately left alone.** A legend line in the console would move every
golden file, the parity ones included - for a line a console user cannot act on. The pieces
are shaped so a `--scala` flag would need no new number: the caps are in the core and the
wording is in `RunReport` (`MapScaleLabel`, `MapLegend`), which is also why those two functions
have no console caller today. `EncodeResidualMapWithTheAutoRangeIsByteIdenticalToTheOldEntryPoint`
pins that the parameterless overload still encodes what it always did.

`--smooth`'s two maps (the removed component at the `-o` path, the smoothed flat's residual
beside it with the `-after` suffix from `FlatCheckCli.AfterMapPath`) are `modes-smooth.md`'s; the window
offers the same two behind a radio toggle, from `SmoothRunOutcome.Png` / `SecondaryPng`.

The encoder writes signature + IHDR (8-bit, colour type 2 = RGB) + a single IDAT
(each row prefixed with filter byte 0, deflated via `ZLibStream`) + IEND, with a
hand-rolled CRC-32. ADR 0008 holds why it stays hand-rolled and the three ways `PngWriterTests`
verifies the bytes. Two facts the ADR does not spell out: the test's `PngDecoder` avoids
`System.Drawing` because it is Windows-only and obsolete on .NET 8, and the bytes were also
confirmed to decode in a real platform decoder and to be byte-identical to what the tool
produced before the refactor began.

The console numbers say how much; the map exists so you can see the *shape* of a
problem (horizontal/vertical/diagonal gradient).
