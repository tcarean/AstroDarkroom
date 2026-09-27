# Mode 2: attribution by 180-degree rotation (--ratio)

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): the rewrite, its measurements and its three
> load-bearing details are ADR 0019. **Covers:** by pointer, `Analysis/RatioAnalyzer.cs`,
> `Report/RatioReport.cs`, `Runs/RatioRun.cs`, `Img.CropCentred` / `Rotate180`; in full, the
> second-session check the report cannot make.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 2. Attribution by 180° rotation - `flatcheck A.fit --ratio B.fit`

ADR 0019 holds the whole of it: why the old OK/SUSPECT/PROBLEMA on `A/B` gave two false greens
out of three on real pairs, what `S = (A + B) / 2` (the camera-fixed part, the verdict through
`StructureAnalyzer`) and `W = A / B` (the world-fixed part, informative) are, the error bar
derived from the even part of `log(A/B)`, the three load-bearing details (median normalisation
before averaging, the odd/even split on the tile grid of `Img.CropCentred`, `AnalyzePrepared`
at analysis resolution), what the report says the mode cannot do (`STACKCNT` / `DATE-OBS`, the
blind spot, the distance test) and the two maps (`-lume`, `RunOutcome.SecondaryPng`). ADR 0016
records why `LargeScalePtp` keeps the top-left grid `CropCentred` was made to avoid.

One check with no other home: "camera-fixed" does not mean "harmless". A strap over the front
element, dew, or a crooked hood rotate with the camera too, look identical here, and are not
there at light time. Telling them apart needs the same test on a **second session with the
same lens**: the camera-fixed signature must come out the same. That comparison is not in the
tool, and it is the check that actually caught the 11 % case ADR 0019 measured.
