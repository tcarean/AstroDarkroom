# Mode 2: attribution by 180-degree rotation (--ratio)

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `Analysis/RatioAnalyzer.cs`, `Report/RatioReport.cs`, `Runs/RatioRun.cs`, `Img.CropCentred` / `Rotate180`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 2. Attribution by 180° rotation - `flatcheck A.fit --ratio B.fit`

**This mode was rewritten. It used to answer "is the flat setup clean?" with
OK/SUSPECT/PROBLEMA. It cannot, and the rewrite is the consequence.** Measured on three
real master pairs whose geometry was known with certainty:

| pair | old verdict | ground truth |
|---|---|---|
| two correctly rotated stacks, real ~1.2% sky gradient | `SUSPECT`, "fix the source" | correct as a measurement, but the gradient had **already** been cancelled from the master by that very rotation. Not actionable. |
| two correctly rotated stacks, session whose master carries 11% of non-radial structure | `OK`, "build your library on it" | the defect is camera-fixed, and to camera-fixed things this test is blind by construction. |
| two stacks from the **same** orientation, i.e. no test performed at all | `OK`, "build your library on it" | nothing was measured. |

Two false greens out of three. The cause is not threshold calibration: **two stacks from
the same orientation and two rotated stacks from a genuinely clean setup produce
statistically identical data** - in both cases `A/B` is only noise. The information that
would separate them is not in the pixels, so no threshold can extract it.

What *can* be computed from the two files, and what nothing else in the tool can:

```
S = (A + B) / 2     the CAMERA-fixed part - the external gradient cancels because it is
                    inverted in B. This IS the master you get by stacking both
                    orientations, so its structure verdict is the verdict of your flat.
                    (Verified: 4.02% computed here vs 4.00% measured on the real
                    pp_flat_stacked.fit built from all 30 frames.)

W = A / B           the WORLD-fixed part, at double amplitude. Informative: it says how
                    much the rotation removed, not whether the setup is good.
```

So the mode's verdict is **`StructureAnalyzer` run on `S`** - the whole mode-1 battery,
reused unchanged - and `W` is reported as information. `Th.RatioOk/Bad` survive only as an
informative label on `W`; they no longer decide anything.

**The error bar is derived, not calibrated.** `log(A/B)` must be exactly antisymmetric
under a 180° rotation: a world-fixed contamination goes entirely into the odd part and
zero into the even part, while noise splits with equal variance. So the even part is an
unbiased estimator of the noise in the odd part, built from the same data, with no
assumption and no constant to tune. Validated on a pair with no rotation at all
(odd/even = 1.17x) against a rotated one (2.59x). Both figures are halved before
reporting, since `W` carries the contamination at 2x.

Three details that are load-bearing:

- **Each stack is normalised to a median of 1 before averaging.** That is Siril's
  `multiplicative normalized input`. Without it, a twilight sky fading 12% between the
  two stacks would weight A more heavily than B and the gradient would not fully cancel.
- **The odd/even split runs on the tile grid, and on a centred crop.** On raw pixels both
  halves are dominated by shot noise, which splits equally, so the ratio would be 1
  regardless of signal. And `Img.BlockMean` drops partial blocks only at the *end*, so its
  grid is anchored top-left and a 180° rotation lands up to one tile off - measured, that
  moves the even/odd ratio by ~20%. `Img.CropCentred` exists for this and **is not used by
  `LargeScalePtp`**, where the top-left anchoring is what `flatcheck.py` does and parity is
  tested.
- **`StructureAnalyzer.AnalyzePrepared`** takes an already-prepared raster, so `S` is built
  at analysis resolution rather than full resolution. `Prepare` is only block means, hence
  linear, so the mean of the prepared rasters equals the prepared mean - at a thirtieth of
  the memory.

**What the mode cannot do**, stated in the report itself because it is not visible in the
numbers:

- **It cannot verify from pixels that you rotated.** Hence `RatioRun` reads `STACKCNT` and
  `DATE-OBS` from both headers and prints them: two stacks that start one right after the
  other had no time to be rotated. A missing header is not an error - a raw has none - and
  prints "necunoscut".
- **"Camera-fixed" does not mean "harmless".** Lens vignetting divides out of the lights
  correctly. A strap over the front element, dew, or a crooked hood rotate with the camera
  too, look identical here, and are not there at light time. Telling them apart needs the
  same test on a **second session with the same lens**: the camera-fixed signature must
  come out the same. That comparison is not in the tool, and it is the check that actually
  caught the 11% case above.
- **Blind spot**, wider than the source used to claim: not just contamination radially
  symmetric about the optical axis (a monitor backlight centred on the lens), but
  **anything that rotates with the camera**. The counter-test for the panel case is a
  *distance* test - one set at 30 cm, one at 60 cm - since lens vignetting is
  distance-invariant and panel contamination is not.

Writes **two** maps: the residual of `S` at the `-o` path, since that is the one matching
the verdict, and `A/B` beside it with a `-lume` suffix (`FlatCheckCli.WorldMapPath`).
`RunOutcome.SecondaryPng` exists for this and is null for every other mode.

