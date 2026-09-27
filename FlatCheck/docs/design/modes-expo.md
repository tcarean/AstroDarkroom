# Mode 4: exposure of a raw flat (--expo), the CFA order, the histograms and the camera preview

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): the decision metric, the levels and the bands
> are ADR 0022, the channel order ADR 0023, the camera preview ADR 0024; this note keeps the
> histograms and a few mechanics. **Covers:** in full, `Analysis/ExposureBands.cs` and the
> `LevelHistogram` half of `Analysis/ExposureAnalyzer.cs`; by pointer, the rest of
> `ExposureAnalyzer.cs`, `CfaPattern.cs`, `Mosaic.cs`, `RawReader.cs`, `PreviewImage.cs`,
> `PreviewReader.cs`, `Report/ExposureReport.cs`, `Runs/ExposureRun.cs`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 4. Exposure - `flatcheck flat_brut.fit --expo [--sky]`

ADR 0022 holds the whole of the decision: the split into the four Bayer positions and the 1.15
spread that makes a file CFA, the brightest channel as the limiting one, the median of the
central 20 % window against the global median, the `white` ladder guessed on a FITS and the
file's own levels on a raw (libraw's `color.maximum`, `black + median(cblack)`, and why both
black fields are read), the `--sat` / `--black` precedence, the sanity gate that refuses a
calibrated or debayered file, the bands with and without `--sky` and the `log2(57 / cenMed)`
correction. Two mechanics it does not spell out: the position index is `ch[(y&1)*2 + (x&1)]`,
and a raw's levels reach the analyzer as an `ExposureScale`.

#### The channel order

ADR 0023 holds it: a position is named only from `filters` + `cdesc` (libraw) or `BAYERPAT`
(FITS, shifted by `XBAYROFF` / `YBAYROFF` through `CfaPattern.Shift`), moved by one row by
`CfaPattern.ForRowOrder` when `ROWORDER = 'BOTTOM-UP'` and the height is even, else
`CFA[y,x]`; the Tokina sky-flat finding, libraw's index-3 trap on Nikon (`FC` gives 0/1/3/2 into
`"RGBG"`), and why no figure depends on a name. The mechanics behind the row shift: `BAYERPAT`
describes the sensor's top-left corner seen top-down, `FitsReader` takes the rows in file order,
so on a bottom-up file of even height the first row in the file is an odd sensor row. Guards:
`CfaPatternTests`, `ABottomUpFrameOfEvenHeightShiftsTheOrderByOneRow`, the `expo cfa
BOTTOM-UP` parity scenario.

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

ADR 0024 holds it: the gap between the raw and the camera's screen (29.0 % against 81.2 %,
32.1 % against 98.0 % on two library sky flats), how `RawReader.Decode` picks the preview from
libraw's thumbnail list, why decoding is opt-in and the console never decodes, the measured cost
of the three decode paths, no ICC transform (ADR 0010), why the preview side has no bands and no
verdict (the power-law fit that predicts over 100 %), what the comparison pairs, and what
`ScreenReading` claims. Two mechanics it does not spell out:

- `d.ThumbnailList` (count, and per entry format, size, flip) is metadata already marshalled,
  so the rule runs without unpacking anything; only the winner is unpacked, while the file is
  open.
- `ExposureReport.ScreenComparison` reads the colour off `Channels[i].Colour`, never off
  `Cfa`: a raw judged `!IsCfa` has a single "mono" channel while `Cfa` may still be set.
