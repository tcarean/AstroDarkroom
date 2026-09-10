# Plan: a fifth mode - the histogram-equalised view of a flat ("Histogram" tab, `--histeq`)

Read `CLAUDE.md` first. Everything below obeys it: nothing is computed or worded outside
`FlatCheck.Core`, no version bump, no commit, GPL header on every new file, user-facing
text in Romanian, code documentation in English. **No Python parity** for this mode, by
decision: `flatcheck.py` and `crosscheck.py` are not touched, and `CrossCheckFixtures` gets
no scenario.

The plan is written so that each numbered step is a self-contained unit of work with its
own tests; the dependency graph is in Part 6. A step that says "new file" also means "GPL
notice first" - `LicenseHeaderTests` walks every project and fails otherwise.

## What Siril actually does (measured in the source, tag `1.4.4`)

The thing the display-mode combo calls **"Histogram"** is not a chart. It is the image
itself, rendered through a transfer function derived from its own cumulative histogram:
**histogram equalisation**. Every grey level on screen receives the same number of pixels.
The relevant code, quoted from `src/gui/image_display.c` and `src/gui/histogram.c` at
`1.4.4`:

```c
typedef enum { LINEAR_DISPLAY, LOG_DISPLAY, SQRT_DISPLAY, SQUARED_DISPLAY,
               ASINH_DISPLAY, STF_DISPLAY, HISTEQ_DISPLAY } display_mode;   // siril.h
```

```c
// histogram.c - one histogram per layer, 65536 bins (65535 + 1), zeros skipped
size_t get_histo_size(fits *fit) { ... return (size_t)USHRT_MAX; }
gsl_histogram* computeHisto(fits *fit, int layer) {
    size = get_histo_size(fit);
    gsl_histogram *histo = gsl_histogram_alloc(size + 1);
    gsl_histogram_set_ranges_uniform(histo, 0,
        fit->type == DATA_FLOAT ? 1.0 + 1.0 / size : size + 1);
    ...
        if (buf[i] == 0) continue;           // ushort and float alike
        gsl_histogram_increment(histo_thr, (double) buf[i]);
```

```c
// image_display.c, remap(vport) - the HISTEQ branch
if (gui.rendering_mode == HISTEQ_DISPLAY) {
    compute_histo_for_gfit();
    histo = com.layers_hist[vport];                 // THIS layer's histogram
    hist_nb_bins = gsl_histogram_bins(histo);
    nb_pixels = (double)(gfit.rx * gfit.ry);        // all pixels, zeros included
    index = gui.remap_index[0];
    index[0] = 0;
    hist_sum = gsl_histogram_get(histo, 0);
    for (i = 1; i < hist_nb_bins; i++) {
        hist_sum += gsl_histogram_get(histo, i);
        index[i] = round_to_BYTE((hist_sum / nb_pixels) * UCHAR_MAX_DOUBLE);
    }
    set_viewer_mode_widgets_sensitive(FALSE);       // lo/hi cutoff sliders are OFF
}
...
if (gfit.type == DATA_USHORT)      dst = index[src[src_i]];
else if (gfit.type == DATA_FLOAT)  dst = index[roundf_to_WORD(fsrc[src_i] * USHRT_MAX_SINGLE)];
```

So, in full:

1. **Per layer.** `remap(vport)` runs once per layer with that layer's own histogram; the
   RGB view is composited afterwards (`remaprgb`) from the three 8-bit buffers. On a
   debayered image each colour is equalised **independently**. On an undebayered file (a
   raw opened as CFA, or a FITS from the Conversion tab - the default in Siril 1.x) there is
   one layer: the mosaic is equalised as a single grey image, R/G/B populations and all.
2. **65536 bins.** 16-bit data bins on the ADU value itself; float data is first quantised
   as `round(v * 65535)`. A 12-bit NEF therefore uses bins 0..4095 and the rest stay empty;
   the mapping is the same as binning on the data's own range.
3. **LUT = CDF x 255**, cumulative *including* the bin itself, rounded to a byte;
   `index[0]` forced to 0. Pixels equal to zero are left out of the histogram but still
   count in the denominator - so an image with many dead pixels never reaches 255. On a
   flat there are no zeros and this is moot; we mirror it anyway because it is free.
4. **The cutoff sliders do nothing** in this mode, and the negative / rainbow toggles
   are applied after the LUT. Neither is in scope.

Why it shows what the linear view hides: a flat is a smooth, narrow distribution. The CDF
climbs from 0 to 1 across the vignetting range only, so the LUT is extremely steep there,
and a 0.3 % ripple in the illumination is spread over dozens of grey levels. The price is
that **amplitude information is destroyed by construction**: a 0.5 % pattern and a 10 %
pattern of the same shape render identically, and a perfectly clean flat shows its own
noise at full contrast. That is exactly the split the tool already makes for the residual
map - *the picture shows the shape, the numbers decide* - and the mode has to say so on
screen. **It can never vote.**

## Decisions (taken here; reopen only with a measurement)

1. **Faithful first: equalise at full resolution, per channel, then shrink the 8-bit
   result for display.** The alternative - block-mean the data first, then equalise - gives
   a *stronger* stretch (the noise is averaged out before the CDF is built) and is arguably
   better for spotting structure, but it is not what Siril shows and the request was
   Siril's picture. Left as a possible later chip ("intins pe rasterul redus"), not built now.
2. **Channels come from the same CFA rule as `--expo`** (positions `(y&1)*2 + (x&1)`,
   "is CFA" when the spread of the four medians exceeds 1.15), pulled out into a shared
   helper so the two modes cannot drift. Names and colours come from `CfaPattern`, exactly
   as on the Exposure tab, and are never guessed.
3. **Views offered.** On a CFA frame: `RGB` (default - each position equalised on its own
   CDF, R = R, G = mean of the two equalised greens, B = B, i.e. Siril on a debayered
   image), one grey per position (`R`, `G1`, `G2`, `B`, or `CFA[y,x]` when the order is
   unknown), and `Mozaic` (the raw mosaic equalised as one layer - Siril on an undebayered
   file, literally). On a mono frame: one view, no chips. If six chips feel like too many
   in use, drop `Mozaic` first; it is the least informative of the set.
4. **Bins on the channel's own `[min, max]` of non-zero pixels, 65536 of them.** Our
   rasters are `double` at whatever scale the file had (0..4095, 0..65535, 0..1), so
   Siril's absolute bins do not transfer. On integer data this is provably the same mapping
   (no two distinct ADU values share a bin, and the CDF at a value is the count at or
   below it either way); on float data it is finer than Siril's 65535-step quantisation,
   which is the more exact of the two. Scale-invariant by construction, like every other
   figure in the tool.
5. **Rounding as Siril's `round_to_BYTE`:** `(byte)min(255, (int)(x + 0.5))`, not
   `Math.Round`'s banker's rounding. One test pins a hand-computed vector.
6. **Display size = the analysis size rule.** Each channel raster is shrunk with the same
   `k = max(1, (int)(long / 1200))` as `Img.Prepare`, so a D3300 channel (3000 x 2000)
   lands at 1500 x 1000 - the size of the residual map, which the window already shows
   comfortably. The mosaic view uses `2k` so all views come out the same size.
7. **No verdict, no threshold, no `Th` entry, exit code 0 on success.** The console
   writes the RGB composite to `-o` (default `flatcheck.png`, like structure) and prints
   an informative report; the window shows the views from memory and saves nothing.
8. **The list on the tab is the flat master first, then every frame the loader can read
   from `flats/`.** The master is `process/pp_flat_stacked.fit` by the session's own
   convention (`SirilSession.FlatMasterPath`), listed at position 0 when it exists and
   marked as the master; the frames are camera raws *and* FITS (`.fit` / `.fits` /
   `.fts`), because a flat converted through Siril is as valid an input here as the NEF.
   The master is there for one question: **did the imperfections seen on the individual
   frames average out in the stack?** A pattern fixed to the setup (a leak, a gradient
   from the panel, a shadow) is in every frame and survives stacking; a transient one (a
   cloud, a passing shadow, a flicker) is in one frame and is diluted by the other 29.
   Equalisation makes both readable at the same contrast, so the comparison is by shape:
   the master's noise floor is far lower than a frame's, and the same view on it shows
   the shared structure *cleaner*, not weaker. The Exposure tab keeps its raw-only list;
   its reason (exact white/black) does not apply here.
9. **The loader replaces the picture, not the tab.** The list stays clickable while a
   frame is computing; clicking another file cancels the running one and starts over,
   and a superseded run may not touch the screen. This needs one small, general change
   in `TabViewModel` (3.4); the other four tabs never overlap runs and are unaffected.
10. **Verification against Siril itself, not against the plan.** Siril's snapshot button
    saves the *displayed* rendering. Take one of a real NEF in Histogram mode and put it
    beside `flatcheck DSC.NEF --histeq`; they should agree in shape (the snapshot is on the
    mosaic, so compare against the `Mozaic` view). Not a test - eyes - but do it once
    before calling the mode done, and write what you saw in the code comment.

## Part 1 - Core

### 1.1 `FlatCheck.Core/Mosaic.cs` (new file)

The CFA rule that `ExposureAnalyzer` keeps inline, made shareable. **No behaviour change**:
`ExposureAnalyzer` is edited to call these and its golden files must stay byte-identical.

- `internal static int PositionOf(int y, int x) => (y & 1) * 2 + (x & 1);`
- `internal const double CfaSpreadMin = 1.15;` - move the literal from
  `ExposureAnalyzer.Analyze` (line ~236, `cfaSpread > 1.15`) here and reference it.
- `internal static bool LooksCfa(double spread) => spread > CfaSpreadMin;`
- `internal static Img[] Split(Img a)` - four rasters of `H/2 x W/2` (floor), position
  order 0..3. Used by the new analyzer only; `ExposureAnalyzer` keeps its own loop because
  it fills histograms in the same pass and a second copy of the data would cost memory for
  nothing.
- `internal static string Label(CfaPattern cfa, int i) => cfa?.Name(i) ?? $"CFA[{i / 2},{i % 2}]";`
  - the body of `ExposureResult.ChannelLabel`, which then delegates to it.

Tests (in `FlatCheck.Tests/MosaicTests.cs`, new): `Split` puts pixel `(y,x)` at position
`PositionOf(y,x)`; odd sizes drop the last row/column; `Label` with and without a pattern.

### 1.2 `FlatCheck.Core/FitsReader.cs`

- `internal static IReadOnlyList<string> FitsExtensions` = `.fit`, `.fits`, `.fts`, and
  `internal static bool IsFitsPath(string path)` (ordinal-ignore-case on the extension),
  mirroring `RawReader.RawExtensions` / `IsRawPath`. **Listing only** - `ImageLoader`'s
  dispatch is not touched, so known issue 2 (anything unclaimed falls back to FITS) stays
  exactly as documented.

Test in `FitsReaderTests`: the three extensions in either case are FITS, `.NEF` and
`.txt` are not.

### 1.3 `FlatCheck.Core/Analysis/HistEqAnalyzer.cs` (new file)

Pure function, no I/O. Types:

```
internal sealed class HistEqChannel
    int Position;            // 0..3, or 0 on mono
    string Label;            // Mosaic.Label(cfa, position), or "mono"
    CfaColour? Colour;       // whatever CfaPattern hands out today; null when unknown/mono
    int H, W;                // of the DISPLAY raster (after shrinking)
    byte[] Equalised;        // H*W grey, row-major
    double Min, Max;         // input range of the non-zero pixels (in the file's units)
    int LevelsUsed;          // distinct output bytes actually produced, 1..256

internal sealed class HistEqResult
    bool IsCfa; CfaPattern Cfa; int SourceH, SourceW;
    IReadOnlyList<HistEqChannel> Channels;    // 4 on CFA, 1 on mono
    int H, W;                                  // display size shared by every view
    byte[] CompositeRgb;                       // H*W*3 - RGB on CFA, grey tripled on mono
    byte[] Mosaic;                             // H*W grey; null on mono (it IS the channel)
```

`internal static HistEqResult Analyze(Img data, CfaPattern cfa, int targetLong = 1200)`:

1. Four medians of the positions (reuse the loop shape of `ExposureAnalyzer`, or
   `Mosaic.Split` + `Stats.Percentile(double[], 50)`); `IsCfa = Mosaic.LooksCfa(max/min)`.
2. Channel rasters: `Mosaic.Split(data)` on CFA, `{ data }` on mono.
3. Per channel: `Lut(values)` (below), apply it to get `byte[]` at channel resolution.
4. Shrink: build an `Img` of the equalised values as doubles, `Img.BlockMean(img, k)` with
   `k = max(1, (int)(max(h, w) / (double)targetLong))`, then round to bytes. Reuses the
   existing primitive instead of adding a byte-raster one; the temporary double raster is
   the cost, and it is the same size the structure mode already carries.
5. `CompositeRgb`: per pixel `R = eq[R]`, `G = (eq[G1] + eq[G2]) / 2` rounded, `B = eq[B]`,
   looked up **by colour**, not by position (RGGB is only the commonest order). When the
   colour order is unknown (`cfa == null`) the composite is the mean of the four greys -
   a colour assignment would be a guess, and the tool does not guess mosaic orders.
6. `Mosaic`: `Lut` over the whole `data` as one population, applied, shrunk with `2k`.

`internal static byte[] Lut(double[] values, out double min, out double max, out int nonZero)`:
65536 `int` counts on `[min, max]` of the non-zero values (`bin = (int)((v - min) / (max - min) * 65535)`,
clamped); cumulative including the bin; `lut[b] = RoundToByte(cum[b] / N * 255)` where `N`
= **all** values including zeros (Siril's `nb_pixels`). Zeros map to output 0 directly,
everything else through the LUT - which is what Siril's forced `index[0] = 0` amounts to.
`RoundToByte(x) = (byte)Math.Min(255, (int)(x + 0.5))`. Degenerate cases: `max == min` ->
every non-zero pixel maps to 255 (the CDF is 1 at the only value); an all-zero raster ->
all 0. No NaN can come out; if it does, that is a bug, not a verdict.

Memory note for the file header: a 24 MP frame split is 4 x 3 M doubles (~96 MB) plus one
temporary double raster per channel; well inside what structure mode already uses.

### 1.4 `FlatCheck.Core/Report/HistEqReport.cs` (new file)

`internal static void Write(HistEqResult r, string path, TextWriter w)`. Romanian, in
the console's existing register. Lines:

```
== EGALIZARE DE HISTOGRAMA (vederea "Histogram" din Siril) ==
fisier:     <path>
cadru:      6000 x 4000, CFA (ordine RGGB, din libraw)   | sau: mono / ordine necunoscuta
afisare:    1500 x 1000 (bloc 2x2 pe canal), 65536 binuri pe canal

canal   interval intrare (ADU)   niveluri folosite
R          151 ..  2980              256 / 256
G1         152 ..  3877              256 / 256
...

INFORMATIV: modul nu da verdict. Poza arata FORMA a ceea ce nu e uniform - nu si cat de
mare e: o unda de 0.5 % si un gradient de 10 % ies la fel. Cifrele sunt in modul de
structura.
```

Expose as values for the window (same reason `StructureReport.Advice()` is a value):
`Caveat()` (the last paragraph), `FrameLine(r)` and `ChannelLine(r, i)`. `Caveat` is the
one sentence of *meaning* in this mode; it lives here and nowhere else.

Test (`HistEqReportTests`, new): the header names the file, a CFA result prints four
channel rows and its order source, a mono result prints one, `Caveat` contains "nu da
verdict".

### 1.5 `FlatCheck.Core/Runs/HistEqRun.cs` (new file)

```
internal static RunOutcome<HistEqResult> Execute(string path, TextWriter report)
    LoadedFrame f = ImageLoader.Load(path);
    var r = HistEqAnalyzer.Analyze(f.Image, f.Cfa);
    HistEqReport.Write(r, path, report);
    return new RunOutcome<HistEqResult>(r, PngWriter.Encode(r.CompositeRgb, r.W, r.H));
```

Plus two encoders the window calls on demand (the console never does):
`internal static byte[] EncodeChannel(HistEqResult r, int i)` and
`internal static byte[] EncodeMosaic(HistEqResult r)`, both via `PngWriter.EncodeGrey`.
`RunOutcome.SecondaryPng` stays null: there are up to five extra views, not one, and
encoding them eagerly is the ~7 MB-nobody-looks-at problem the map cache exists to avoid.

### 1.6 `FlatCheck.Core/PngWriter.cs`

`internal static byte[] EncodeGrey(byte[] grey, int w, int h)` - expands each byte to
`(g, g, g)` and calls `Encode`. The encoder itself, IHDR colour type 2, CRC table - all
untouched. Test in `PngWriterTests`: decodes with `PngDecoder`, every pixel has R = G = B
= the source byte.

## Part 2 - Console

### 2.1 `FlatCheck/Options.cs`

- `internal bool Histeq;` and `"--histeq"` in `BoolFlags`, set in the same `if` chain.
- Nothing else: `-o` already exists and is the PNG path.

`OptionsTests`: `--histeq` parses; `--histeq f.nef` and `f.nef --histeq` give the same
`Options`; `--histeq --expo f` keeps both flags set (first-wins dispatch is the CLI's
existing rule, and it is pinned in `FlatCheckCli`, not here).

### 2.2 `FlatCheck/FlatCheckCli.cs`

- Dispatch: `--histeq` **first**, before `--expo`, and update the file-header comment
  ("Ordinea de dispecerat: --histeq -> --expo -> --ratio -> --light -> structura").
- `static int RunHisteq(Options o, TextWriter w)`: `HistEqRun.Execute(o.Image, w)`,
  `SaveMap(o.OutPng, run.Png, w)`, return 0. It does **not** go through `ExitFor`: there is
  no verdict to translate, and inventing "OK" would put a success where the exit-code
  table promises "usable as-is".
- `WriteUsage`: one line, `flatcheck <flat.NEF> --histeq -o <poza.png>   # vederea
  "Histogram" din Siril, pe canale`, and one sentence under the paragraphs saying the mode
  is a picture, not a test, and exits 0 when it wrote it.

## Part 3 - Window

### 3.1 `FlatCheck.Wpf/Session/SirilSession.cs` and `SessionMessages.cs`

- `internal IReadOnlyList<string> FlatFrames()` - files in `flats/` for which
  `RawReader.IsRawPath(p) || FitsReader.IsFitsPath(p)`, ordinal by file name, empty when
  the folder is missing. `RawFlats()` stays as it is for the Exposure tab.
- `internal IReadOnlyList<HistEqCandidate> HistEqCandidates()` - what the tab lists:
  `FlatMasterPath` first, **only if the file exists** (in the window it always does,
  since `Check()` refuses the folder without it; the method still has to cope, because
  the session class is tested on its own and never throws), then `FlatFrames()`. A
  `HistEqCandidate` is `(string Path, bool IsMaster)`, an `internal readonly record
  struct` beside the class. Nothing is deduplicated by content: if someone copies the
  master into `flats/`, it is listed twice under two paths, which is the truth.
- `SessionMessages.NoFlatFrames(SirilSession s)` - the explanation when the list is
  empty: what the folder is, which extensions are read. Reachable only when neither the
  master nor any frame exists, i.e. never through `MainViewModel` - but the tab is also
  attached in tests, so the message stays.
- `SessionMessages.HistEqGuideIntro()` and `HistEqGuideRows()` - the collapsed guide,
  mirroring the README table **by hand** like the other two guides. Rows: what the picture
  is (rank transfer, equal pixels per grey); why a clean flat looks noisy at full contrast;
  why amplitude cannot be read off it; what colour casts mean on the `RGB` view (a channel
  whose pattern differs from the others); what the `Mozaic` view is; where to look for the
  number (Structura); and **master versus frame**: the master is the stack of the frames
  below it, so a shape present on a frame and gone on the master was transient and was
  averaged out, while a shape present on both is fixed to the setup and is what the
  Structure numbers measure - and, because equalisation is rank-based, the master's lower
  noise makes a shared pattern look *sharper* there, not fainter. Documentation, not a
  classifier - same rule as the map guide.

`SirilSessionTests`: `FlatFrames` lists `.NEF` and `.fit` and skips `.txt`, ordinal order,
empty without the folder. `HistEqCandidates` puts the master at index 0 with `IsMaster`
true and the frames after it in `FlatFrames` order; without the master file it is just
the frames; with neither it is empty; a `pp_flat_stacked_0deg.fit` in `process/` is
**not** picked up (the tab lists *the* master by convention, not every stack - the Ratio
tab is where two stacks meet).

### 3.2 `FlatCheck.Wpf/ViewModels/HistEqViewViewModel.cs` (new file)

The chip row, on the `MapScaleViewModel` pattern: `HistEqViewOption { Label, IsSelected }`
public for XAML, `internal Kind` (`Composite | Channel | Mosaic`) and `internal Index`
for the owner. `internal void Rebuild(HistEqResult r)` builds the option list from the
result (`RGB` + one per channel + `Mozaic` on CFA; a single option on mono), keeps the
previous selection if a chip with the same label still exists, else falls back to the
first. `internal event Action Changed`. `IsVisible => Options.Count > 1`. Decides nothing.

`GroupName` in XAML: `HistEqView` - unique in the window, for the reason documented on
the other radio groups.

### 3.3 `FlatCheck.Wpf/ViewModels/HistEqTabViewModel.cs` (new file)

Public, over `TabViewModel`. State: `Files` (`ObservableCollection<HistEqFileRow>`, see
3.3a - not bare strings, because the master row has to look different from a frame row),
`Selected` (a `HistEqFileRow`; setter starts `Recompute()` on its `Path`), `Image`
(`byte[]`, bound through `PngToImage`), `View` (3.2), `Frame` / `Channels` strings from
`HistEqReport.FrameLine` / `ChannelLine`, `Caveat` (= `HistEqReport.Caveat()`), `Guide`
(a guide view-model on the `MapGuideViewModel` / `HistogramGuideViewModel` pattern - check
which of the two takes its text in the constructor and use that one; add an overload if
neither does), `Report` (inherited).

Behaviour:

- `Attach(session, ct)`: fill `Files` from `session.HistEqCandidates()` (one row per
  candidate, master first), clear selection, `Fail(...NoFlatFrames)` when empty, keep the
  session token as the *parent* token. Nothing is preselected and nothing runs: the
  master is a 24 MP float FITS, and loading it at folder open would sit on top of the
  structure run that is already reading the same file (the sequential-not-parallel rule).

### 3.3a `FlatCheck.Wpf/ViewModels/Rows.cs` - `HistEqFileRow`

```
public sealed class HistEqFileRow
    public string Path { get; init; }
    public string Name { get; init; }      // file name only
    public string Where { get; init; }     // "process/" for the master, "flats/" for a frame
    public bool IsMaster { get; init; }
    public string Tag => IsMaster ? "MASTER" : "";
```

Formatted in the view-model, like every other row, so a test can assert `"MASTER"` and
`"process/"` rather than a converter's output. The master row is the *same thing* the
Structure tab analyses, and the tab says so in the caption when it is selected: "Acesta e
masterul pe care ruleaza si tabul Structura; compara-l cu cadrele de mai jos ca sa vezi ce
s-a mediat si ce a ramas."
- `RecomputeAsync()`: cancel the previous run's own `CancellationTokenSource`, create a
  new one linked to the parent, then `RunGuardedAsync("Egalizez histograma pe X...", ...)`
  doing `HistEqRun.Execute` on `Task.Run`, then `Populate(outcome.Result)`. `Populate` is
  `internal` for the smoke test, like `ExposureTabViewModel.Populate`.
- `Populate(r)`: retain `_last = r`, clear the per-option PNG cache, `View.Rebuild(r)`,
  set the strings, `RefreshImage()`, `HasResult = true`.
- `RefreshImage()` (on `View.Changed` too): encode the selected view via `HistEqRun`'s
  encoders (or reuse `outcome.Png` for the composite), cache per option - the same
  measured reasoning as the map cache: one encode of 1500 x 1000 is ~100-250 ms on the UI
  thread, once per chip, and a `Task.Run` here would need the very generation counter 3.4
  adds. Keep it synchronous, note the measurement in the comment.
- `Reset()`: cancel, drop `_last`, `Image = null`, clear cache, base.

### 3.4 `FlatCheck.Wpf/ViewModels/TabViewModel.cs` - superseded runs

One general change, small and documented in the file header: `RunGuardedAsync` numbers
its runs (`int _generation`; `int gen = ++_generation` at entry). After `await work()`
returns *or throws*, and before touching `HasResult`, `Fail`, or `EndBusy`, it checks
`gen == _generation` and returns silently if a newer run has started. A superseded run
therefore leaves the busy screen up (the newer run owns it), reports no error, and
clears no result. Cancellation is still not an error.

Why in the base and not in the new tab: the state it protects (`IsBusy`, `HasError`,
`HasResult`) is the base's, and a tab-local guard would have to reach into it. The other
tabs never overlap runs (`MainViewModel` runs them one after the other; the Exposure
overlay blocks the list), so for them the check is always true and nothing changes. A
test in `FlatCheck.Wpf.Tests` (new `TabViewModelTests`) pins it with a stub tab: start A
(blocked on a `TaskCompletionSource`), start B (completes), release A -> `HasResult` is
B's, `IsBusy` is false exactly once, and A's failure (make it throw) shows no error.

### 3.5 `FlatCheck.Wpf/Views/HistEqView.xaml` + `.xaml.cs` (new files, GPL comment block)

Layout copied from `ExposureView`: a 290 px list column on the left (heading
`MASTERUL SI FLAT-URILE`, `ListBox` bound to `Files` / `Selected`), the result on the
right. The list's `ItemTemplate` is a small `DataTemplate` over `HistEqFileRow` (inline in
the view, like `ExposureView`'s): the name, a `Faint` `Where` under it, and a chip with
`Tag` shown only when `IsMaster` (`Visibility` via `BoolToVis`) - the master must read as
"the stack of what is below", not as one more frame. A thin `BorderBr` separator under
the master row, done with a `DataTrigger` on `IsMaster`, keeps the two groups apart
without a second list.
The right column is its own `Grid` and holds, stacked: `MessagePanel`; the empty-state
text ("Alege un flat din lista ca sa-l vad egalizat."); a `ScrollViewer` visible on
`HasResult` with a card containing the chip row (`ItemsControl` of `RadioButton`s bound
to `View.Options`, `GroupName="HistEqView"`, collapsed when `View.IsVisible` is false),
the `Image` (`Source="{Binding Image, Converter={StaticResource PngToImage}}"`,
`Stretch="Uniform"`, `RenderOptions.BitmapScalingMode="HighQuality"` - equalised noise
aliases badly with the default scaler), a `Faint` caption naming the view and its size,
the `Muted` caveat, the frame and channel lines in `MonoFont`; then the guide panel; then
`ReportPanel`. **`BusyOverlay` goes inside the right-hand `Grid`, last child**, so it
covers the picture and not the list (decision 9). The list column does **not** bind
`IsEnabled` to anything.

### 3.6 `FlatCheck.Wpf/Views/HistEqGuidePanel.xaml` (new) - or reuse

If `MapGuidePanel` / `HistogramGuidePanel` bind to a property of a fixed name on the
inherited DataContext, the cheapest route is to give `HistEqTabViewModel` a property of
that same name and type and reuse the panel unchanged; otherwise a copy of
`HistogramGuidePanel` bound to `Guide.*`, using the shared `Disclosure` style and
`GuideRowTemplate`. Decide by reading the two panels, not by preference.

### 3.7 `FlatCheck.Wpf/MainWindow.xaml` and `ViewModels/MainViewModel.cs`

- Fifth `TabItem`, `Header="Histogram"` (the user's name for it; the other headers stay
  as they are), after `Expunere`, hosting `local:HistEqView DataContext="{Binding HistEq}"`.
- `MainViewModel`: `public HistEqTabViewModel HistEq { get; }`, constructed with the
  others; `HistEq.Attach(session, ct)` next to `Exposure.Attach` (a listing, nothing to
  compute); `HistEq.Reset()` in `ResetTabs`. Nothing is started at folder open - the tab
  runs on a click, like Exposure, and for the same memory reason it does not join the
  sequential chain.
- Comments that count tabs ("cele patru taburi", in `MainWindow.xaml`, `MainViewModel.cs`,
  `TabViewModel.cs`, `BusyOverlay.xaml`) become "cele cinci taburi" where they are
  touched anyway; do not go hunting for the rest.

## Part 4 - Tests

### 4.1 `FlatCheck.Tests/HistEqAnalyzerTests.cs` (new)

- **The Siril vector.** Values `{1, 2, 2, 3}` -> counts at three bins, cumulative
  `1, 3, 4` over `N = 4` -> LUT bytes `64, 191, 255` with `round_to_BYTE`. Pins the
  rounding rule and the inclusive cumulative.
- **Uniform in, ramp out.** A raster whose values are a uniform ramp equalises to a
  linear 0..255 ramp (equalisation of a uniform distribution is the identity, up to
  quantisation): max abs error <= 1 level.
- **Gain invariance.** `Analyze(img)` and `Analyze(img * 7.3)` give byte-identical
  rasters and composites - the "every metric is a ratio" rule, here by construction.
- **Flat output histogram.** On a continuous input (radial fixture + smooth ramp, no
  ties), each of the 256 output levels holds `N/256` pixels within a small tolerance -
  the defining property of the mode, and the one that would catch an off-by-one bin.
- **The point of the mode.** `FitsTestData.Radial` with a 0.3 % ripple added (say
  `1 + 0.003 * sin(6*pi*x/W)`), *before* shrinking: in the equalised output the ripple
  spans at least ~20 grey levels peak-to-peak along the row through the centre, where the
  linear 8-bit rendering of the same data spans < 2. Asserts the amplification, not a
  specific picture.
- **Zeros as in Siril.** A raster with 10 % zeros: zeros come out 0, the maximum output is
  `round(0.9 * 255) = 230`, not 255.
- **Channels.** `TestImages.FlatCfa` -> `IsCfa`, four channels, `H/2 x W/2` before
  shrinking, labels from a supplied `CfaPattern` and `CFA[y,x]` without one; the composite
  takes R from the red position *by colour* on a GBRG pattern (the index-3 trap, again);
  mono input -> one channel, `Mosaic == null`, composite grey.
- **Shrinking.** A 3000 x 2000 channel comes out 1500 x 1000; the mosaic view the same
  size as the channels; a 500 x 300 one is left alone (`k = 1`).
- **Degenerate.** Constant raster -> all 255 for non-zero; all-zero -> all 0; no
  exception, no NaN.

### 4.2 `FlatCheck.Tests/MosaicTests.cs` (new) - see 1.1. Plus one assertion in
`ExposureAnalyzerTests` that `ExposureAnalyzer` still reports `IsCfa` at exactly the
`Mosaic.CfaSpreadMin` boundary (a spread of 1.15 is *not* CFA, 1.1501 is).

### 4.3 `FlatCheck.Tests/RunsTests.cs`

`HistEqRun_ProducesTheSameNumbersAsTheAnalyzer`, `..._ReturnsTheSamePngBytesAsEncodingTheComposite`,
`..._ReturnsADecodablePng` (via `PngDecoder`, size = `r.W x r.H`),
`..._WritesTheReportText`, `EncodeChannel_AndEncodeMosaic_AreGreyPngsOfTheSameSize`.

### 4.4 `FlatCheck.Tests/GoldenOutputTests.cs` + `Golden/histeq_cfa.txt`, `Golden/histeq_mono.txt`

`RunCli(ws, f, "--histeq", "-o", ws.PathTo("out.png"))`: exit 0, the PNG exists and
decodes, the text matches the golden file (approve with `FLATCHECK_UPDATE_GOLDEN=1`, then
read the diff). One run on `WithBayerMosaic(Radial)` with `BayerCards("RGGB")`, one on a
plain radial FITS. Also `--histeq` on a folder is refused with the existing "un folder se
poate da doar cu --light" message (the check precedes dispatch; just assert it holds).

### 4.5 `FlatCheck.Tests/VersionTests.cs`

Wherever the banner is asserted "for every mode", add the `--histeq` invocation to that
list. Nothing else there changes.

### 4.6 `FlatCheck.Wpf.Tests/HistEqTabTests.cs` (new)

- The list holds the master first (`IsMaster`, `Tag == "MASTER"`, `Where == "process/"`),
  then the `.NEF` and the `.fit` from `flats/`, not the `.txt`
  (`SessionFixture.WithFlatMaster` + `WithRawFlats` for the fake names,
  `WriteAnalysableRawFlat` for one that actually loads).
- Picking the master row computes on `FlatMasterPath` - the same file the Structure tab
  reads - and the caption names it as the master.
- With the master but no `flats/` folder the list has exactly one row and no error: the
  question "did it average out" still has half an answer.
- Without master and without frames the tab explains (`HasError`, text names `flats/`
  and an extension). Only reachable by attaching the tab directly.
- Nothing selected until the user picks; `HasResult` false.
- Picking computes: `HasResult`, `Image` non-empty and decodable, `View.Options` has
  six entries on the CFA fixture (or five if `Mozaic` is dropped), one on a mono fixture.
- Picking another file regenerates: different `Image` bytes for two different fixtures.
- Switching a chip changes `Image` and does not re-run (`_last` is the same reference;
  expose via an `internal` accessor or assert `IsBusy` never became true).
- Superseding: pick A, immediately pick B without awaiting, await the tab's pending task
  (expose `internal Task Pending` or await `RecomputeAsync()` of B) -> `HasResult` true,
  the frame line names B, `IsBusy` false.
- `Reset` clears everything, including `Image`.

### 4.7 `FlatCheck.Wpf.Tests/MainViewModelTests.cs`, `WindowSmokeTests.cs`, `SirilSessionTests.cs`

- `HistEqGetsItsListButWaitsForAChoice` next to the Exposure one: with `WithFlatMaster()`
  and two fake raws the list has three rows, the first is the master, `HasResult` is
  false and nothing ran (the master is not loaded twice at open). The "opening another
  folder clears the old results" test asserts `HistEq` too.
- Smoke: render `HistEqView` empty (in `EveryTabRendersWithRealResultsAndNoBindingErrors`,
  after `OpenAsync`), then with a result (`Populate` from `HistEqAnalyzer.Analyze` on
  `TestImages.FlatCfa`, name `tab-histeq`), then with the guide expanded
  (`tab-histeq-ghid`), then mono (`tab-histeq-mono`). The binding-error assertion covers
  the new XAML; `FLATCHECK_DUMP_SCREENS` gives the author the pictures to look at.
- `TabViewModelTests` (new) for 3.4.

## Part 5 - Documentation

### 5.1 `README.md`

- "Cele patru moduri" -> "Cele cinci moduri", a new `### 5. Egalizare de histogramă — \`--histeq\``
  section: what it is (the Siril "Histogram" view, per channel), what it shows and what
  it destroys (the amplitude paragraph, verbatim from the caveat), the views, the input
  it wants (any frame from `flats/`, raw or FITS; a master is allowed but the frames as
  shot are the point), that it has no verdict and exits 0.
- "Opțiuni" and "Coduri de ieșire": `--histeq`; "0 = a scris poza" for this mode.
- "Aplicația cu ferestre": the tab table gets a fifth row (`Histogram` | modul 5 pe
  masterul din `process/` sau pe un cadru din `flats/`, raw sau FITS | la alegere, și la
  comutarea vederii); a paragraph on the tab, the master-first list and what comparing
  the master with a frame answers (decision 8, in Romanian: ce e fix pe montaj rămâne în
  stivă, ce a fost trecător se diluează), the chips, the loader that leaves the list live,
  and the guide table with the same "mirrored by hand, no test binds them" note as the
  other two. The guide gets one more row for exactly that comparison.
- "Diferențe față de versiunea Python": `--histeq` exists only in C#.

### 5.2 `CLAUDE.md`

- Opening paragraph: four questions -> five, the fifth being "what does the flat look
  like once every grey level holds the same number of pixels".
- Structure: entries for `Mosaic.cs`, `Analysis/HistEqAnalyzer.cs`, `Report/HistEqReport.cs`,
  `Runs/HistEqRun.cs`, the WPF files, and the `FitsReader.IsFitsPath` note.
- "The four modes" -> "The five modes", with a `### 5. Histogram equalisation` section
  carrying the Siril findings above (the quoted code stays here, in the plan; the section
  keeps the four numbered facts and decisions 1-6), and the sentence that it never votes.
- "The two applications" table: the window's input column gains "the Histogram tab takes
  any frame from `flats/`"; "What the window adds" gains the superseding-run note (3.4).
- "Parity with Python": `--histeq` has no Python counterpart and no `crosscheck.py`
  scenario, by decision.
- "Exit codes": 0 for `--histeq` means the picture was written.
- Dispatch order in mode 1's intro.

### 5.3 Version

Not bumped. The change adds a mode and a tab, which by the log's own convention has been
a minor bump (2.1.0 came with the Exposure histograms); say so in the hand-over and let
the author choose.

## Part 6 - Order and independence

| step | depends on | can run in parallel with |
|---|---|---|
| 1.1 Mosaic | - | 1.2, 1.6, 3.2, 3.4 |
| 1.2 IsFitsPath | - | 1.1, 1.6, 3.2, 3.4 |
| 1.6 EncodeGrey | - | 1.1, 1.2, 3.2, 3.4 |
| 1.3 Analyzer (+4.1) | 1.1 | 1.2, 1.6, 3.x |
| 1.4 Report | 1.3 | 1.6, 3.x |
| 1.5 Run (+4.3) | 1.3, 1.4, 1.6 | 3.1, 3.2, 3.4 |
| 2.x Console (+4.4, 4.5) | 1.5 | 3.x |
| 3.1 Session | 1.2 | 1.x, 2.x, 3.2, 3.4 |
| 3.2 View chips | - | everything |
| 3.4 TabViewModel generation (+its test) | - | everything |
| 3.3 Tab VM (+4.6) | 1.5, 3.1, 3.2, 3.4 | 2.x |
| 3.5-3.7 XAML, MainViewModel (+4.7) | 3.3 | 2.x |
| 5.x Docs | all | - |

Each row is one commit-sized unit with green tests at its end; `dotnet test FlatCheck.sln`
after every one, `FLATCHECK_UPDATE_GOLDEN=1` only at 4.4 and only after reading the diff.

## Measurements to take while implementing (write them into the code comments)

- Time and peak memory of `HistEqAnalyzer.Analyze` on a real 24 MP NEF, split into load /
  histogram+LUT / shrink / encode. Expectation: load dominates (libraw), the rest well
  under a second; if the double temporaries in step 4 of 1.3 push memory past the
  structure mode's, switch the shrink to a byte-raster block mean.
- PNG size and encode time of the composite: equalised noise compresses badly, so expect
  the PNG to be several times the residual map's. If it passes ~10 MB, that is an argument
  for a smaller `targetLong` on this mode, not for a different encoder.
- The Siril snapshot comparison (decision 10), on a real flat with a known pattern.
