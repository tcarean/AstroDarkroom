# PNG output: the residual map, its scale and the encoder

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `PngWriter.cs` (`EncodeResidualMap`, `MapRange`, `FixedCapsPct`), `Report/RunReport.cs` (`MapScaleLabel`, `MapLegend`).
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
disk use. `FixedRange(cap)` is symmetric, `1 ± cap/100`, and exists because the automatic
scale stretches colour over each image's own interval: measured on two masters of the same
lens, full red was `+3 %` on one and `+10 %` on the other, and nothing on screen said so.
Two auto-scaled maps can only be compared by shape.

**The fixed caps are not new numbers.** `PngWriter.FixedCapsPct` is
`{ Th.ResidPerfect, Th.ResidUsable, Th.ResidCheck }` and `DefaultCapPct` is `Th.ResidUsable`.
Don't duplicate the values; `TheFixedCapsAreTheResidualScaleBoundaries` is what stops the
two from drifting.

**But a cap is not a band, and the docs used to say it was.** The cap is a per-pixel
deviation from the model; the bands are peak-to-peak amplitudes. A one-sided +10% blotch
reaches the 10% cap right at the `BUN` boundary; a 10% gradient runs from -5% to +5% and
reaches only half of it, saturating at 20%, i.e. at `REFA`. Measured: at a 10% gradient
the map's p1/p99 sit at -4.9/+4.8%. There is no cap that is exact for both, so the README
explains it under "Scara culorilor" and the numbers stay as they are.

**The console was deliberately left alone**, and so was every file written to disk. A
legend line in the console would move every golden file, the parity ones included - for a
line a console user cannot act on.
The pieces are shaped so a `--scala` flag would need no new number: the caps are in the
core and the wording is in `RunReport` (`MapScaleLabel`, `MapLegend`), which is also why
those two functions have no console caller today.
`EncodeResidualMapWithTheAutoRangeIsByteIdenticalToTheOldEntryPoint` pins that the
parameterless overload still encodes what it always did.

`--ratio` is the only mode that writes **two** maps. The primary one, at the `-o` path, is
the residual of the camera-fixed master, because that is the one the verdict describes;
the secondary goes beside it with a `-lume` suffix (`FlatCheckCli.WorldMapPath`) and shows
the *shape* of the external gradient. The window offers the same two behind a radio
toggle, from `RunOutcome.Png` / `SecondaryPng`.

The encoder writes signature + IHDR (8-bit, colour type 2 = RGB) + a single IDAT
(each row prefixed with filter byte 0, deflated via `ZLibStream`) + IEND, with a
hand-rolled CRC-32.

**Verified three ways**: `PngWriterTests` decodes the output with an independent
reader written in the test assembly (`PngDecoder` - deliberately not
`System.Drawing`, which is Windows-only and obsolete on .NET 8), checks every chunk
CRC, and pins CRC-32 against the standard `"123456789"` -> `0xCBF43926` vector and
the well-known empty-IEND constant `0xAE426082`. The bytes were also confirmed to
decode in a real platform decoder, and are byte-identical to what the tool produced
before the refactor began.

Unlike the Python version, there is no 4-panel figure - only the residual map.
The console numbers are the same; the map exists so you can see the *shape* of a
problem (horizontal/vertical/diagonal gradient).

