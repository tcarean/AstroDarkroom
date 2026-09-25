# Mode 4: exposure of a raw flat (--expo), the CFA order, the histograms and the camera preview

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `Analysis/ExposureAnalyzer.cs`, `Analysis/ExposureBands.cs`, `CfaPattern.cs`, `Mosaic.cs`, `RawReader.cs`, `PreviewImage.cs`, `PreviewReader.cs`, `Report/ExposureReport.cs`, `Runs/ExposureRun.cs`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 4. Exposure - `flatcheck flat_brut.fit --expo [--sky]`

Splits the frame into the 4 Bayer positions (`ch[(y&1)*2 + (x&1)]`) and takes the
median of each. If `max/min > 1.15` the file is treated as CFA and the brightest
channel is used (the brightest channel clips first, so it is the limiting one);
otherwise the frame is treated as mono/processed and the whole frame is used.

#### The channel order

Since the histogram card, the four positions get their colours - but **only when the file
says which is which**, and it is never guessed. A raw carries `filters` + `cdesc`; a FITS
from Siril's Conversion tab carries `BAYERPAT` (plus `XBAYROFF`/`YBAYROFF`, which shift
the mosaic phase and go through `CfaPattern.Shift`). Anything else stays `CFA[y,x]`.

**`ROWORDER` is part of the order.** `BAYERPAT` describes the sensor's top-left corner
seen top-down, but Siril writes every converted or stacked frame bottom-up
(`ROWORDER = 'BOTTOM-UP'`), and `FitsReader` takes the rows in file order. On an *even*
height the first row in the file is an odd sensor row, so the phase moves by one row -
`RGGB` reads as `GBRG` - and `CfaPattern.ForRowOrder` moves it, as Siril does when it
debayers. On an odd height the bottom row has the top row's parity and nothing moves;
`TOP-DOWN` or no card, likewise. Found on the Tokina sky-flat master (6016x4016): without
the shift `--histeq` called position [1,0] "G2" at a third of the green level, which was
the red channel of a blue twilight sky - `--expo` on the source NEF, through libraw, had
R exactly there. No figure moved, since every metric is per position; only the two labels
and the `--histeq` composite were wrong. Pinned by
`ABottomUpFrameOfEvenHeightShiftsTheOrderByOneRow` and the `expo cfa BOTTOM-UP` scenario.

The
order differs between cameras - RGGB is commonest, but GBRG, GRBG and BGGR all exist - so
a label on the wrong channel would look measured when it is not. It changes no figure:
the limiting channel is the brightest one whatever colour it is.

**The index-3 trap.** `FC(row, col)` returns an index into `cdesc`, not a colour, and
libraw puts the second green at index 3 on Nikon. Measured on a real D3300 NEF:
`filters = 0xB4B4B4B4`, `cdesc = "RGBG"`, FC gives 0/1/**3**/2 -> RGGB. A decoder that
assumes 1 = G mislabels half the mosaic. `CfaPatternTests` carries that exact vector
alongside the four textbook masks.

#### The histograms

Each channel also carries two distributions - the whole channel, and the same central 20%
window the verdict is read from - as `LevelHistogram`: 200 bins of 0.5% on the *percentage*
axis, clamped at both ends so a saturated tail stays visible in the last bin rather than
falling off. The axis is deliberately the one the verdict lives on, so `ExposureBands`
becomes intervals on the chart directly.

They are filled in the loops the analyzer already ran, and **nothing existing moved**: the
brightest channel's centre list is still the one `CentreMedianPct` and `CentreClipPct` come
from, bit for bit. The other three windows are new work and cost 3 x 4% of the frame.

The window normalises each curve to its own peak (the centre window holds 4% of a channel's
pixels; on a shared scale the curve that decides would be a flat line). **The chart never
votes** - it explains where the deciding number came from. The console prints none of this.

#### The camera's preview

The window can switch the same charts to **what the camera showed on its screen**: the JPEG
preview embedded in the raw, which is the image the camera's own histogram was computed
from. The gap is the whole point. Measured, centre window of the brightest channel, on two
sky flats from this library:

| file | raw | camera screen | screen peak |
|---|---|---|---|
| Nikkor 35 DX, ISO 800 | 29.0 % | 81.2 % | 84.7 % |
| Samyang f/2.0, ISO 1600 | 32.1 % | 98.0 % | 100.0 % (pixels at 255) |

The second one looked burnt on the camera and had 1.6 stops of headroom in the file.

**Where the bytes come from.** `RawReader.Decode` reads libraw's thumbnail *list*
(`d.ThumbnailList`: count, and per entry format, size, flip) - metadata already marshalled -
and picks by rule: the largest JPEG whose long side is at most 3000 px; failing that the
largest JPEG (decoded scaled, see below); failing that a bitmap libraw already decoded;
failing that null. Only the winner is unpacked; the bytes are copied while the file is open,
so nothing is reopened. On the D3300 that picks the 1620x1080 entry, which agrees with the
6000x4000 one to 0.4 points at a twelfth of the time. Indices are **not** stable across
cameras, hence the enumeration. Two libraw facts that look like bugs: JPEG entries report
`Bits = 0` (the 8-bit check is for the bitmap path only), and asking past `ThumbCount`
throws error -9.

**Decoding is opt-in.** `ExposureRun.Execute(..., withPreview)` defaults to false. The
console never decodes; the structure, ratio and light modes never decode; the window passes
true. Measured with ImageSharp on the D3300: 16 ms / 5 MB for the 1620 px preview, 460 ms /
69 MB for the 6000 px one at native size, 485 ms / 9 MB with `TargetSize` - so `TargetSize`
buys memory, not time, and the mid-size selection is what makes it cheap.

**No ICC transform.** A preview run through an embedded colour profile is a different image
from the one on the screen. On ImageSharp 3.1.x the decoder never applies one; on 4.x there
is `ColorProfileHandling.Convert` and it would have to be set to `Preserve` explicitly. The
reader also sets `SkipMetadata`, so the profile is not even read.

**The preview side has no bands and no verdict.** `PreviewRows` sets all four band fields to
`NaN`, `HistogramCanvas` skips `NaN` zones, and nothing else was touched. A target band on
the screen's axis would say "expose until the screen reads 50-65 %", which is the exact
mistake this view exists to cure. It also cannot be computed: mapping the raw target onto
the screen needs the transfer curve, a flat contains only a narrow slice of it, and fitting
a power law to that slice predicts values over 100 % - the same ill-posed extrapolation as
the rejected `radPct`-to-zero-sky one. Measured on a D3300 frame with full tonal range,
fitting a power law on a flat-like slice and predicting the screen value at raw 57 %
(measured: 95.7 %): fit on 23-31 % predicts 90.5 %, on 20-35 % predicts **100.9 %**, on
10-60 % predicts 98.2 % - moving the window by a few points flips the sign of the error,
because the real curve saturates at 100 and no power law does. And the curve is per
channel anyway (white balance multiplies R by 2.355 and B by 1.086 on that file), so one
raw target would land in three places on the three charts. `ThePreviewNeverChangesTheVerdict`
pins that attaching a preview leaves every verdict figure bit-identical.

**The comparison pairs centre with centre, by colour.** `ExposureReport.ScreenComparison`
reads the colour off `Channels[i].Colour`, never off `Cfa`: a raw judged `!IsCfa` has a
single "mono" channel while `Cfa` may still be set. The two greens are averaged. Whole-frame
medians are not compared - the raw one is pulled down by vignetting in a way the preview's
is not, so that gap would be partly the lens.

`ScreenReading` states the mechanism and the measurement, not a law: the screen saturates
before the sensor *when* the white balance multiplies the channel by 1 or more and the tone
curve reaches 255, which on every camera measured here it did, by a third of a stop to a
stop. So "not touching the edge" is a guarantee; "touching the edge" is not information.

The decision metric is the median of a **central 20% × 20% window** of the
brightest channel, **not** the global median. This is the single most important
design point of this mode: a flat has a wide distribution *by definition* - that
is what vignetting is - so the global median is dragged down by the corners. Follow
the global median and you will overexpose until the centre clips, which destroys
vignetting information exactly where the signal is highest. The global median is
still printed, but labelled informative.

`white` is auto-detected from the frame max (≤1.5 → 1.0; ≤4300 → 4095; ≤17203 →
16383; else 65535) and can be overridden with `--sat` / `--black`. To find the
true white level: shoot one deliberately overexposed frame; its max *is* saturation.

**On a camera raw nothing is guessed.** The file carries both levels, and `RawReader`
hands them to the analyzer as an `ExposureScale`, which is reported as
`sursa: libraw (exact)`. Precedence is Python's, and the order matters: `--sat` beats the
file *and* takes over the source line (the reported scale is no longer the file's), while
`--black` is overridden separately - so a bare `--sat` leaves the file's black in place.
No file scale means the `AutoWhite` ladder, unchanged. Pinned by
`AScaleReadFromTheFileBeatsTheGuess` and the two override tests beside it.

White comes from libraw's `color.maximum` and black from `color.black + color.cblack[i]`,
median of the four - which is what `rawpy.black_level_per_channel` reports. The split
between the two fields is libraw's business and it varies: on Nikon NEFs it moves the
common part into `black` and leaves `cblack` at zero, so reading only one of them would
silently give zero on some cameras. On a D3300 at 12 bits that comes out 4095 / 150.

**Raw-file sanity gate.** Before issuing a verdict, the mode rejects the file if
either `cenMed < 1.0` (level near zero → already calibrated or normalised) or the
CFA spread `< 1.02` (the four Bayer positions are near-identical → the file is
debayered). This catches the common "opened the NEF in Siril and hit Save"
mistake. When it fires, the mode prints an explanation and returns **without a
verdict**.

Verdict bands on the central window (`Th.Expo*`):

| condition | verdict |
|---|---|
| clipping > 0.05% or > 80% | PREA SUS |
| < 30% | PREA JOS |
| < 50% | ACCEPTABIL, dar cam jos |
| 50-65% | **OK** |
| 65-80% | ACCEPTABIL, dar cam sus |

With `--sky` the bands change to 15-50% OK, > 60% (or clipping) too high, < 15%
too low. Sky flats are *supposed* to sit modestly: the blue-hour sky darkens
continuously and is full of faint stars, so master SNR comes from the *number* of
frames, not from pushing one frame to 55%. Without `--sky` a sky flat at 22% would
be wrongly reported as underexposed.

Suggested corrections are `log2(57/cenMed)` stops - 57 being the midpoint of the
50-65 target band.

