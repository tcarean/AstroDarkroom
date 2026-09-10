# Plan: per-channel histograms on the Exposure tab, and naming the CFA channels

Read `CLAUDE.md` first. Everything below obeys it: nothing is computed or worded outside
`FlatCheck.Core`, no new dependency, no version bump, no commit, GPL header on every new
file, user-facing text in Romanian, code documentation in English.

## Why

The Exposure tab shows one number per Bayer position and a sentence saying the tool does
not know which position is R, G or B. A median says *where* a channel sits; it does not
show the shape: whether the centre peak has headroom under white, whether a tail already
touches saturation, how far the corners fall below the centre. A histogram per channel
puts the whole distribution on the same axis the verdict uses, and the target band, the
clipping zone and the decision figure can be drawn onto it. That is what "explain how
this file is exposed" means in practice: the same numbers, but *seen*.

### The channel order, measured

Measured on a real D3300 NEF (`DSC_0209.NEF`, an ordinary daylight colour scene), through
the same libraw that `RawReader` uses:

| what libraw says | value |
|---|---|
| `cdesc` | `RGBG` |
| `FC(row, col)` for rows/cols 0..1 (rawpy `raw_pattern`) | `[[0, 1], [3, 2]]` |
| therefore CFA[0,0], CFA[0,1], CFA[1,0], CFA[1,1] | **R, G, G, B** = `RGGB` |

Empirical cross-check on the same file, no metadata: the two positions libraw calls G
have **identical** medians (357 and 357 ADU) while the other two differ from them and
from each other (R 268, B 270 at the median; 331 vs 372 at the mean). Two positions with
one filter, two with different ones - consistent with RGGB and with nothing else that
has two greens on a diagonal.

Two consequences shape the plan:

- **The pattern is per camera, so it is read from the file, never hardcoded.** RGGB is
  the common case but GBRG, GRBG and BGGR all exist. A raw carries it (`filters` +
  `cdesc`); a FITS written by Siril's Conversion tab carries `BAYERPAT` (plus
  `XBAYROFF`/`YBAYROFF`). Anything else stays "unknown", with the current `CFA[y,x]`
  labels - the tool must not guess.
- **Note that libraw's `cdesc` index 3 is the second G**, so the D3300's `filters` value
  is not the textbook `0x94949494` (where both greens are index 1) but one where
  FC(1,0) = 3. The decoder goes through `cdesc` for every position; it must not assume
  index 1 = G.

The synthetic fixtures already simulate RGGB (`FitsTestData.WithBayerMosaic` and
`TestImages.FlatCfa` put `rGain` at even/even and `bGain` at odd/odd), so the existing
assertion "brightest = index 1, the first green" reads correctly once the labels exist.

## Decisions already taken (do not reopen)

| decision | value | why |
|---|---|---|
| where the histogram is computed | `FlatCheck.Core`, inside `ExposureAnalyzer.Analyze`, into `ExposureResult` | nothing is computed outside the core. The analyzer already visits every pixel once; the bins are filled in that same loop. |
| histogram axis | percent of `(white - black)`, 0..100, **200 bins of 0.5 %**, clamped: below black -> bin 0, `>= white` -> last bin | the same axis the verdict is on. The bands from `Th` become intervals on it directly, every channel and every file is comparable, and a saturated tail is visible as mass in the last bin. |
| two histograms per channel | the whole channel, and the **central 20 % window** of that channel | the tab's core claim - "the global median is dragged down by the corners" - becomes visible: the whole-channel curve is wide, the centre curve is a narrow peak sitting at the decision figure. |
| mono / processed frames | one channel, both curves, over the whole frame | mirrors the analyzer's `!isCfa` branch |
| vertical normalisation | each curve to its own peak | the chart shows shape and position; the verdict never uses counts. The centre window has 4 % of the pixels, so on a shared scale it would be a flat line. |
| channel naming | a `CfaPattern` read from the file; labels `R`, `G1`, `G2`, `B` when known, `CFA[y,x]` when not | see "The channel order, measured" |
| console output | **unchanged for every existing golden file**. The per-channel line gains the colour name only when the pattern is known, and no existing fixture carries `BAYERPAT`. No histogram in the console. | goldens untouched; `crosscheck.py` untouched. One **new** golden + crosscheck scenario with `BAYERPAT` pins the labelled line, mirrored in `flatcheck.py`. |
| where the bands come from | `ExposureBands.For(sky)` in the core, used by `Classify` **and** by the chart | one definition. The shaded band is by construction the band that votes. Today `Classify` picks among six `Th` constants inline; that selection is the one thing a view-model must not repeat. |
| the per-file reading | `ExposureReport.HistogramReading(r)` - Romanian prose derived from the result | wording lives in `Report/`; the console does not print it (not actionable in text; the window is the audience). Same status as `RunReport.MapLegend`. |
| the "how to read the histogram" guide | `SessionMessages` + a collapsed disclosure panel, same pattern as `HowToReadPanel` | it is documentation shown in the window, and that has one home already. Its table is mirrored in the README **by hand**. |
| drawing | a custom `FrameworkElement` with `OnRender`, no NuGet | one-dependency rule. A bar chart with three shaded zones and a marker is a page of `DrawingContext` calls. |
| channel colours | fixed hues for R/G/B in a small `ChannelPalette`; unknown pattern -> the accent colour for all four | colours must not come from `VerdictPalette` - they are not verdicts. Unknown must not *look* like RGB. |
| `Sky` toggle | nothing new: it already re-runs the analysis; the bands and the reading re-derive from the new result | |
| cost | 4 channels x 2 curves x 200 ints, filled in the existing loop; a few extra percentiles on lists the analyzer already builds | not measurable next to loading a 24 MP raw |
| `RunOutcome` | untouched | the histograms travel inside `ExposureResult` |

## Part 1 - Core

### 1.1 `FlatCheck.Core/CfaPattern.cs` (new file, GPL header)

The colour of each of the four Bayer positions of the raster *as loaded* - after any
crop the reader applied.

```csharp
/// <summary>
/// Culoarea celor patru pozitii Bayer ale rasterului INCARCAT - [0,0], [0,1], [1,0],
/// [1,1] - sau null cand fisierul nu spune. Nu se ghiceste niciodata: ordinea difera
/// intre aparate (RGGB e cea mai des intalnita, dar exista si GBRG, GRBG, BGGR).
/// </summary>
internal sealed class CfaPattern
{
    internal readonly char[] Colours;   // exact 4 litere din {R,G,B}, exact doua G
    internal readonly string Source;    // "libraw" / "BAYERPAT" - pentru raport

    /// <summary>"RGGB" -> pattern; null pe orice altceva (litere straine, 3 sau 4 G, lungime gresita).</summary>
    internal static CfaPattern Parse(string letters, string source)

    /// <summary>
    /// Din libraw: filters + cdesc, evaluate la originea zonei VIZIBILE (rowOffset =
    /// TopMargin, colOffset = LeftMargin), fiindca RawReader decupeaza marginile si un
    /// offset impar ar muta faza mozaicului. FC(row, col) e macroul din dcraw:
    ///     (filters >> ((((row << 1) & 14) + (col & 1)) << 1)) & 3
    /// iar rezultatul e un INDEX in cdesc - la Nikon indexul 3 e al doilea G, deci nu
    /// se presupune ca 1 = G. Null pentru filters 0 (mono), 1 (Leaf) si 9 (X-Trans),
    /// si cand cdesc are sub 4 caractere.
    /// </summary>
    internal static CfaPattern FromLibRaw(uint filters, string cdesc, int rowOffset, int colOffset)

    /// <summary>Acelasi mozaic vazut de la (dy, dx): pentru XBAYROFF / YBAYROFF din FITS.</summary>
    internal CfaPattern Shift(int dy, int dx)

    /// <summary>"R", "G1", "G2", "B" - G-urile numerotate in ordinea rasterului.</summary>
    internal string Name(int i)

    internal string Letters => new string(Colours);   // "RGGB"
}
```

`Shift` is a lookup: position `[y,x]` of the shifted pattern is position
`[(y + dy) & 1, (x + dx) & 1]` of the original.

### 1.2 `ImageLoader.cs` - `LoadedFrame`

```csharp
/// <summary>Ordinea culorilor in mozaic, sau null cand fisierul n-o spune.</summary>
internal CfaPattern Cfa;
```

Same philosophy as `Scale`: null is the correct answer for a file that does not say, not
an error.

### 1.3 `RawReader.cs`

In `Decode`, after the sizes are read and before the pixels are copied:

```csharp
LibRawImageParams p = d.ImageParams;
CfaPattern cfa = CfaPattern.FromLibRaw(p.Filters, p.Cdesc, s.TopMargin, s.LeftMargin);
```

and `Cfa = cfa` in the returned frame. `d` is the `LibRawData` already marshalled for
`RawData` and `Color`; `ImageParams` is a field of it (verified by reflection on
`Sdcb.LibRaw 0.21.1.7`: `Filters : UInt32`, `Cdesc : String`). Locals only - **no new
field on the class**, or `HoldsNoLibrawTypedFields_SoTheAssemblyStaysLazilyLoaded` fails
and FITS-only installs break. Extend the file-header comment's list of "what is asked of
libraw" with `idata.filters` / `idata.cdesc`.

### 1.4 `FitsReader.cs`

In `LoadWithHeader`, after the header dictionary exists:

```csharp
string pat = HStr(hdr, "BAYERPAT", "");
CfaPattern cfa = CfaPattern.Parse(pat, "BAYERPAT")
                 ?.Shift(HInt(hdr, "YBAYROFF", 0), HInt(hdr, "XBAYROFF", 0));
```

`HStr` already strips the FITS quotes. Siril writes `BAYERPAT = 'RGGB'` for a CFA frame
that came through Conversion, and it writes nothing for a debayered one - which is
exactly right, since there is no mosaic left to name. A `.fit` from the fixtures has no
card, so it lands on null and nothing changes.

### 1.5 `Analysis/ExposureBands.cs` (new file, GPL header)

The ladder `Classify` uses, as a value, so the chart can draw the same one.

```csharp
/// <summary>
/// Benzile verdictului pe fereastra centrala, in procente din white. Exista ca valoare
/// fiindca doua locuri le folosesc - Classify si histograma din fereastra - si a doua
/// alegere intre constantele din Th ar fi fost o a doua definitie a verdictului.
/// </summary>
internal readonly struct ExposureBands
{
    internal readonly bool Sky;
    internal readonly double TooLow;   // sub asta PREA JOS; NaN pe cer... vezi mai jos
    internal readonly double Lo, Hi;   // tinta
    internal readonly double TooHigh;  // peste asta PREA SUS
    internal readonly double Target;   // mijlocul tintei, pt sugestia in stopuri

    internal static ExposureBands For(bool sky) => sky
        ? new(true,  Th.ExpoSkyLo, Th.ExpoSkyLo, Th.ExpoSkyHi, Th.ExpoSkyTooHigh, Th.ExpoSkyTarget)
        : new(false, Th.ExpoTooLow, Th.ExpoLo, Th.ExpoHi, Th.ExpoTooHigh, Th.ExpoTarget);
}
```

On the sky ladder there is no "acceptable but low" band, so `TooLow == Lo` - that is the
honest representation, and it draws as a band with no yellow zone below it.

Refactor `ExposureAnalyzer.Classify` to read from `ExposureBands.For(r.IsSky)` instead of
the six inline `Th` references. Behaviour identical; `PanelBands` and `SkyBands` pin every
boundary on both sides. Also use `Target` in `ExposureReport` and in the window's
`AdviceText` instead of picking `Th.ExpoTarget`/`Th.ExpoSkyTarget` by hand.

### 1.6 `Analysis/ExposureAnalyzer.cs` - the histograms and the labels

New type, in this file (it belongs to no other mode):

```csharp
/// <summary>Histograma unui canal pe axa procentuala a verdictului: 200 de cosuri de 0.5 %.</summary>
internal sealed class LevelHistogram
{
    internal const int Bins = 200;
    internal readonly int[] Counts = new int[Bins];
    internal int Total;

    /// <summary>Cosul unui nivel; sub black -> 0, la sau peste white -> ultimul.</summary>
    internal static int BinOf(double pct)
    internal void Add(double pct)
}

/// <summary>Un canal, cu tot ce arata fereastra despre el. Un singur element pe mono.</summary>
internal sealed class ChannelLevels
{
    internal int Position;              // 0..3 = CFA[y,x], sau 0 pe mono
    internal string Label;              // "R", "G1", "G2", "B" sau "CFA[0,1]"; "mono" pe mono
    internal char Colour;               // 'R', 'G', 'B' sau '\0' cand nu se stie
    internal double MedianPct;          // mediana globala a canalului (era ChannelMedians[i])
    internal double P99Pct;             // coada de sus a canalului intreg
    internal double ClipPct;            // % pixeli >= 98 % white pe canalul intreg
    internal double CentreMedianPct;    // aceeasi fereastra 20 % ca la decizie, pe canalul asta
    internal double CentreP1Pct, CentreP99Pct;
    internal bool IsBrightest;
    internal LevelHistogram Whole, Centre;
}
```

`ExposureResult` gains:

```csharp
internal CfaPattern Cfa;                 // null = necunoscut
internal ChannelLevels[] Channels;       // 4 pe CFA, 1 pe mono
internal ExposureBands Bands;            // For(IsSky), ca fereastra sa nu aleaga singura
internal string ChannelLabel(int i)      // Cfa?.Name(i) ?? $"CFA[{i / 2},{i % 2}]"
```

`Analyze` gains a trailing optional parameter `CfaPattern cfa = null`. Inside:

- The existing per-position loop also does `hist[c].Add(pct)` for the whole-channel
  histograms, where `pct = (v - black) / rng * 100`. `black`/`rng` are known before the
  loop already.
- The centre-window loop currently runs once, for `bright`. Run the same window
  (`chH*0.4..0.6`, `chW*0.4..0.6`) for all four positions, collecting per-position
  lists; the brightest one's list is the one the existing `cenMed`/`cenClip` come from, so
  those figures must stay bit-identical (`ReadingTheScaleFromARawComputesExactlyWhatSatAndBlackDo`
  and the goldens pin them). The extra three windows cost 3 x 4 % of the frame.
- Fill `Channels` from `meds`, the new per-position lists, and the histograms. On the mono
  branch: one `ChannelLevels` over the whole frame, `Label = "mono"`, `Colour = '\0'`.
- `ChannelMedians`, `BrightestChannel`, `CfaSpread`, `IsCfa` **stay** as they are - the
  report, the CLI and the tests read them. `Channels[i].MedianPct` is the same value in
  percent; do not make one derive from the other in a way that changes rounding.

Every existing figure is computed exactly as before. The new work is added *beside* the
old loops, not folded into them, so a diff shows nothing moved.

### 1.7 `Runs/ExposureRun.cs`

`ExposureAnalyzer.Analyze(f.Image, sat, black, sky, f.Scale, f.Cfa)`. One argument.

### 1.8 `Report/ExposureReport.cs`

**The console line.** When `r.Cfa == null` the output is byte-identical to today. When
known:

```
    nivel median GLOBAL per canal CFA (ordinea R/G/B din BAYERPAT):
        R  (CFA[0,0]):  27.6%
        G1 (CFA[0,1]):  44.4%  <- cel mai luminos
        G2 (CFA[1,0]):  44.4%
        B  (CFA[1,1]):  21.3%
```

`ChannelNames` goes away in favour of `r.ChannelLabel(i)`; the `CFA[y,x]` suffix keeps the
position visible so a reader can still relate the line to the mosaic.

**New, exposed as values** (the window shows them; the console does not print them):

```csharp
/// <summary>Cum se citeste histograma fisierului asta: 3-5 propozitii deduse din rezultat.</summary>
internal static string HistogramReading(ExposureResult r)

/// <summary>Nota scurta de sub eticheta unui canal, sau "" cand n-are nimic de spus.</summary>
internal static string ChannelNote(ExposureResult r, ChannelLevels c)

/// <summary>Eticheta benzii tinta, pentru desen: "tinta 50-65 %" / "tinta sky 15-50 %".</summary>
internal static string BandLabel(ExposureBands b)
```

`HistogramReading` builds, in order, from the result:

1. Which channel limits, by name when known: *"Canalul cel mai luminos e G1, deci el
   decide."* When unknown: *"Canalul cel mai luminos e CFA[0,1] - nu stiu ce culoare are
   - deci el decide."*
2. Where the centre peak sits against the band, with the distance in points:
   *"Varful ferestrei centrale e la 51.6 %, in tinta 50-65 %."* / *"... la 42.0 %, cu 8
   puncte sub tinta."* / *"... peste tinta."*
3. Headroom: *"Cel mai luminos 1 % din centru ajunge la 61.2 % din white: mai sunt ~0.7
   stopuri pana la saturatie."* - `log2(100 / CentreP99Pct)`, a ratio, so
   scale-invariant like everything else. When `CentreClipPct > Th.ExpoClip`: *"Centrul
   atinge deja saturatia (0.12 % din pixeli), deci PREA SUS oricare ar fi mediana."*
4. Whole-channel vs centre: *"Mediana intregului canal e cu 7.2 puncte sub centru - asta
   e vignetarea, nu subexpunere; curba lata din fundal o arata."*
5. If any *other* channel clips on the whole frame (`ClipPct > Th.ExpoClip`), name it.
6. On `!IsRaw`: a single sentence - the histogram is of a file that is not a raw flat
   (already calibrated / debayered), so nothing in it is an exposure.

`ChannelNote`: `"cel mai luminos - decide expunerea"` on the brightest; `"clipping X %
pe tot canalul"` when that channel clips; otherwise `""`.

### 1.9 `flatcheck.py` - mirror the label line only

- `load_raw_for_expo`: on a raw, derive the four letters from `raw.raw_pattern` and
  `raw.color_desc` (`color_desc[raw_pattern[y][x]]`), source `"rawpy"`; on FITS read
  `BAYERPAT` + `XBAYROFF`/`YBAYROFF` from the header. Return it as a sixth value.
- `mode_expo`: when known, print the same heading and `R  (CFA[0,0])` labels as 1.8;
  unchanged otherwise. Label source must read `BAYERPAT` on FITS in both implementations
  so the crosscheck text matches; on raw the C# says `libraw` and Python `rawpy` - add
  that to the intentional-divergence list, next to the scale-source line.
- No histogram, no reading - those are window-only values.

## Part 2 - Window

### 2.1 `ViewModels/Rows.cs` - `ChannelHistogramRow`

```csharp
/// <summary>Un canal pe cardul de histograme. Tot ce e aici e deja formatat sau deja normalizat.</summary>
public sealed class ChannelHistogramRow
{
    public string Label { get; init; }        // "G1"
    public string Position { get; init; }     // "CFA[0,1]" ("" pe mono)
    public Brush Colour { get; init; }        // din ChannelPalette
    public string Median { get; init; }       // "44.4 %"
    public string Centre { get; init; }       // "51.6 %"
    public string Note { get; init; }         // ExposureReport.ChannelNote
    public bool IsBrightest { get; init; }
    public double[] Whole { get; init; }      // 200 valori, 0..1, normalizate la propriul varf
    public double[] CentreCurve { get; init; }
    public double Marker { get; init; }       // CentreMedianPct, in % - linia verticala
    // benzile, copiate pe fiecare rand ca sablonul sa nu aiba nevoie de RelativeSource
    public double BandLo { get; init; }
    public double BandHi { get; init; }
    public double TooLow { get; init; }
    public double TooHigh { get; init; }
    public string BandLabel { get; init; }
    public bool HasNote => !string.IsNullOrEmpty(Note);
}
```

The band values are copied from `r.Bands` onto every row so the `DataTemplate` binds
locally. A `RelativeSource AncestorType=UserControl` binding would also work, but it
breaks silently when the template is reused, and `WindowSmokeTests` would only catch it
if the trace listener sees it.

### 2.2 `Presentation/ChannelPalette.cs` (new file, GPL header)

```csharp
internal static class ChannelPalette
{
    internal static readonly Color R = Color.FromRgb(0xE0, 0x6C, 0x75);
    internal static readonly Color G = Color.FromRgb(0x7D, 0xC3, 0x83);
    internal static readonly Color B = Color.FromRgb(0x61, 0xAF, 0xEF);
    internal static readonly Color Unknown = Color.FromRgb(0x4E, 0xA8, 0xDE);   // = AccentBr

    internal static Brush BrushFor(char colour)   // 'R'/'G'/'B'/anything else; frozen
}
```

Not in `VerdictPalette`: these are not verdicts and must never be routed through
`Stats.Severity`. Not in `Dark.xaml` either: the choice is made from a `char` that comes
from the result, which is C#, not XAML.

### 2.3 `Presentation/HistogramCanvas.cs` (new file, GPL header)

A `FrameworkElement` that draws one row. Dependency properties, all with
`FrameworkPropertyMetadataOptions.AffectsRender`:

| property | type | drawn as |
|---|---|---|
| `Whole` | `double[]` | filled polygon under the curve, `Fill` at ~30 % opacity |
| `Centre` | `double[]` | filled polygon, `Fill` at full opacity, 1 px outline |
| `Fill` | `Brush` | the channel colour |
| `TooLow`, `BandLo`, `BandHi`, `TooHigh` | `double` (% of width) | zones: `< TooLow` faint red, `TooLow..BandLo` faint yellow, `BandLo..BandHi` faint green, `BandHi..TooHigh` faint yellow, `> TooHigh` faint red. `NaN` = zone absent. |
| `ClipFrom` | `double`, default 98 | a hatched/solid red strip from 98 % to 100 % - the analyzer's clipping definition |
| `Marker` | `double` | a 1.5 px vertical line at the centre median, in `TextBr`, with the value printed beside it |
| `BandLabel` | `string` | printed inside the green zone, `Faint` size |

Also draws the x-axis ticks at 0 / 25 / 50 / 75 / 100 with `% din white` at the right,
using `FormattedText` in the theme's UI font. Everything must render with **every
property null / default** - the element exists in the template before any file is
picked - and with arrays of any length (the bin count is `Whole.Length`, not a constant
copied from the core). Height is set by the template (~96 px); width stretches.

Zone colours are private constants in this file, semi-transparent over `PanelBg2`; they
are chart furniture, not the verdict palette, and must not be confusable with it (the
verdict chip above the card is the only place the verdict colour appears).

### 2.4 `ViewModels/ExposureTabViewModel.cs`

New members:

```csharp
public ObservableCollection<ChannelHistogramRow> Histograms { get; } = new();
public string Reading { get => ...; private set => Set(...) }          // ExposureReport.HistogramReading
public string ChannelOrderNote { get => ...; private set => Set(...) } // vezi mai jos
public HistogramGuideViewModel HistogramGuide { get; } = new();
```

In `Populate(r)`:

- `Histograms.Clear()`, then one row per `r.Channels[i]`: label/position/colour from the
  channel, `Median`/`Centre` formatted `F1` + ` %`, `Note` from `ExposureReport.ChannelNote`,
  `Whole`/`CentreCurve` = `Normalise(hist.Counts)` (divide by max; all-zero -> all-zero),
  `Marker = c.CentreMedianPct`, the band fields from `r.Bands`, `BandLabel` from
  `ExposureReport.BandLabel(r.Bands)`.
- `Reading = ExposureReport.HistogramReading(r)`.
- `ChannelOrderNote`: when `r.Cfa != null` -> *"Ordinea R/G/B e citita din fisier
  (libraw: RGGB). Canalul cel mai luminos clipeaza primul, deci el e cel limitant."*; when
  null -> today's sentence, unchanged: *"Nu stiu care pozitie CFA e R, G sau B - si nici
  nu conteaza: ..."*. This replaces the hardcoded `TextBlock` in the XAML.
- `Channels` (the mono-spaced list) keeps working but uses `r.ChannelLabel(i)`; delete
  the local `ChannelNames` copy.
- `AdviceText` reads `r.Bands.Target` instead of choosing between the two `Th` targets.

`Reset()` clears `Histograms`, `Reading`, `ChannelOrderNote`. `HistogramGuide.IsExpanded`
survives `Reset()` like the map guide's does: it is a viewing preference.

`Normalise` is formatting, not analysis - it is the same kind of step as turning a
double into `"44.4 %"`. It stays in the view-model.

### 2.5 `ViewModels/MapGuideViewModel.cs` - `HistogramGuideViewModel`

Next to `HowToReadViewModel`, same shape: `Intro` from `SessionMessages.HistogramGuideIntro()`,
`Rows` from `SessionMessages.HistogramGuideRows()` as `GuideRow`s, `IsExpanded` collapsed
by default. Public, for binding.

### 2.6 `Session/SessionMessages.cs` - the guide text

```csharp
internal static string HistogramGuideIntro()
internal static IReadOnlyList<(string Shape, string Meaning)> HistogramGuideRows()
```

Intro: what the axis is (percent of white after black, the same axis as every figure on
the tab), and that the chart explains, it does not vote - the verdict is the centre
median of the brightest channel and nothing else.

Rows, in reading order:

| row | meaning |
|---|---|
| doua curbe pe canal | the faint wide one is the whole channel; the solid narrow one is the central 20 % window. Each is scaled to its own peak, so heights are not comparable between the two - positions are. |
| linia verticala | the centre median - the one figure that decides. On the brightest channel it is the number in the VERDICT card. |
| banda verde | the target band, from the same thresholds as the verdict. It moves with "Sky": a sky flat is *supposed* to sit lower. |
| benzile galbene si rosii | acceptable-but / too low / too high, on the same ladder. |
| dunga rosie din dreapta | 98-100 % of white: clipping. Any solid-curve mass there is PREA SUS whatever the median says - the analyzer counts those pixels separately. |
| de ce curba slaba e lata | that width is vignetting, not exposure: the corners of a flat are darker by construction. The distance between the two peaks is the vignetting depth. Do not "fix" it by exposing more - the centre would clip. |
| de ce canalele au varfuri diferite | Bayer filters pass different amounts of the light's colour. On a warm panel R may lead, on a blue-hour sky B usually does; the brightest one limits. The tool does not white-balance - that is the point. |
| ce NU e | the camera's histogram, which is of the *processed* JPEG (debayer, white balance, gamma) and sits far to the right of this one. |

The README's table (Part 4) mirrors these by hand; there is no test binding them, as with
the map guide.

### 2.7 `Views/HistogramGuidePanel.xaml` + `.xaml.cs` (new files, GPL header)

A copy of `HowToReadPanel` bound to `HistogramGuide.*`, `Disclosure` style, `Card`,
`GuideRowTemplate`. Disclosure text: *"Cum citesc histogramele"*.

### 2.8 `Theme/Dark.xaml` - `ChannelHistogramRowTemplate`

```xml
<DataTemplate x:Key="ChannelHistogramRowTemplate">
  <Grid Margin="0,0,0,12">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="118" /><ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <!-- stanga: pastila colorata cu eticheta, pozitia, mediana / centrul, nota -->
    <StackPanel ...>
      <Border Background="{Binding Colour}" CornerRadius="4" Padding="8,2" HorizontalAlignment="Left">
        <TextBlock Text="{Binding Label}" Foreground="#16161A" FontWeight="Bold" />
      </Border>
      <TextBlock Style="{StaticResource Faint}" Text="{Binding Position}" />
      <TextBlock Style="{StaticResource Number}" HorizontalAlignment="Left" Text="{Binding Centre}" />
      <TextBlock Style="{StaticResource Faint}" Text="{Binding Median}" />
      <TextBlock Style="{StaticResource Faint}" Text="{Binding Note}"
                 Visibility="{Binding HasNote, Converter={StaticResource BoolToVis}}" />
    </StackPanel>
    <!-- dreapta: desenul -->
    <local:HistogramCanvas Grid.Column="1" Height="96"
        Whole="{Binding Whole}" Centre="{Binding CentreCurve}" Fill="{Binding Colour}"
        Marker="{Binding Marker}" BandLo="{Binding BandLo}" BandHi="{Binding BandHi}"
        TooLow="{Binding TooLow}" TooHigh="{Binding TooHigh}" BandLabel="{Binding BandLabel}" />
  </Grid>
</DataTemplate>
```

A `DataTrigger` on `IsBrightest` makes the label chip's border `TextBr` so the deciding
channel is the one that stands out, without a second colour system.

### 2.9 `Views/ExposureView.xaml`

Between the `NIVELURI` card and `SCALA SI CANALELE`:

```xml
<TextBlock Style="{StaticResource Heading}" Margin="2,22,0,8" Text="HISTOGRAME PE CANAL" />
<Border Style="{StaticResource Card}">
  <StackPanel>
    <ItemsControl ItemsSource="{Binding Histograms}"
                  ItemTemplate="{StaticResource ChannelHistogramRowTemplate}" />
    <TextBlock Style="{StaticResource Faint}" Margin="0,4,0,0"
               Text="curba slaba = tot canalul, curba plina = centrul 20 %, linia = mediana centrului, dunga rosie = clipping (peste 98 % din white)" />
    <TextBlock Style="{StaticResource Muted}" Margin="0,12,0,0" Text="{Binding Reading}" />
  </StackPanel>
</Border>
<local:HistogramGuidePanel Margin="0,10,0,0" />
```

In `SCALA SI CANALELE`, replace the hardcoded `Faint` sentence with
`Text="{Binding ChannelOrderNote}"`. Widen the right column's `MaxWidth` from 720 to
~820 so four rows of 96 px chart read comfortably; the `ScrollViewer` already handles
the height.

## Part 3 - Tests

### 3.1 `FlatCheck.Tests/CfaPatternTests.cs` (new)

- `FromLibRaw` on the textbook values with `cdesc = "RGBG"`: `0x94949494` -> `RGGB`,
  `0x16161616` -> `BGGR`, `0x61616161` -> `GRBG`, `0x49494949` -> `GBRG`.
- **The Nikon case**: `0xB4B4B4B4` with `"RGBG"` -> `RGGB` (FC(1,0) = 3, i.e. the second G
  through `cdesc`). This is the vector that would fail a decoder assuming index 1 = G.
- Offsets: `(rowOffset 1, colOffset 0)` turns `RGGB` into `GBRG`; `(0, 1)` into `GRBG`;
  `(1, 1)` into `BGGR`; `(2, 2)` leaves it.
- `filters` 0, 1, 9 -> null; `cdesc` of length 3 -> null.
- `Parse`: `"RGGB"` ok; `"rggb"` ok (Siril writes upper case, but be lenient); `"RGGG"`,
  `"RGB"`, `"RGBW"`, `""`, null -> null.
- `Shift(1, 0)` of `RGGB` = `GBRG`; `Shift(2, 2)` identity.
- `Name`: `RGGB` -> `R, G1, G2, B`; `GRBG` -> `G1, R, B, G2`.

### 3.2 `FitsReaderTests.cs`

- A FITS with `BAYERPAT = 'RGGB'` loads with `Cfa.Letters == "RGGB"` and `Source == "BAYERPAT"`.
- With `XBAYROFF = 1` as well -> `GRBG`.
- Without the card -> `Cfa == null`, and nothing else in `LoadedFrame` changes.

`FitsTestData.Card("BAYERPAT", "'RGGB'")` - the helper pads the value; the quotes go in
the value string.

### 3.3 `RawReaderTests.cs`

Extend the gated `ARealRawDecodesToAnUndebayeredMosaicWithAScaleFromTheFile`:

```csharp
Assert.NotNull(f.Cfa);
Assert.Equal("libraw", f.Cfa.Source);
Assert.Equal(2, f.Cfa.Letters.Count(c => c == 'G'));
```

Plus a comment stating the measured D3300 value (`RGGB`) without asserting it, since
`FLATCHECK_NEF` may point at another camera. `HoldsNoLibrawTypedFields` must still pass
untouched - that is the check that 1.3 used locals.

### 3.4 `ExposureAnalyzerTests.cs`

- `EveryChannelHistogramCountsEveryPixelOfThatChannel`: on `FlatCfa(128,128)`,
  `Whole.Total == 128*128/4` for each of the four, and `Centre.Total` equals the window's
  pixel count `((int)(64*0.6) - (int)(64*0.4))^2 = 169` - the same 169 the clipping test's
  comment already relies on.
- `AConstantChannelLandsInOneBin`: `FlatCfa` at 55 % with `--sat` -> the brightest
  channel's `Whole` has exactly one non-zero bin, at `BinOf(55)`.
- `LevelsAtOrAboveWhiteLandInTheLastBin`, `LevelsBelowBlackLandInTheFirst`.
- `TheCentreHistogramOfTheBrightestChannelIsTheDecisionWindow`: the centre median read
  back from `Channels[bright].CentreMedianPct` equals `r.CentreMedianPct` to 1e-9.
- `OnAMonoFrameThereIsOneChannel`: the debayered fixture -> `Channels.Length == 1`,
  `Label == "mono"`.
- Labels: with `cfa = Parse("RGGB")` -> `R, G1, G2, B`; without -> `CFA[0,0]..`.
- `TheBandsAreTheOnesClassifyUses`: `ExposureBands.For(false)` equals the six `Th`
  values; `For(true)` the sky ones; `TooLow == Lo` on sky.
- Extend `ReadingTheScaleFromARawComputesExactlyWhatSatAndBlackDo` with the new fields:
  `Channels[i].Whole.Counts`, `.Centre.Counts`, `.P99Pct`, `.ClipPct` all equal between
  the two paths.
- Keep every existing assertion. The refactor of `Classify` is pinned by `PanelBands`,
  `SkyBands`, `SkyHasNoAcceptableLowBand` and the two clipping tests.

### 3.5 `FlatCheck.Tests/ExposureReportTests.cs` (new)

- `HistogramReading` on a 55 % `FlatCfa` with `RGGB`: contains `"G1"`, `"in tinta"`,
  `"stopuri"`; on 42 %: contains `"sub tinta"`; on a clipped centre: contains
  `"saturatia"`; on `!IsRaw`: one sentence, no percentage claims.
- `HistogramReading` without a pattern names `CFA[0,1]` and says the colour is unknown.
- `ChannelNote` on the brightest: `"decide"`; on a channel with `ClipPct > Th.ExpoClip`:
  `"clipping"`.
- `BandLabel(For(false))` = `"tinta 50-65 %"`; `For(true)` = `"tinta sky 15-50 %"`.
- `Write` with a pattern prints `R  (CFA[0,0])` and `ordinea R/G/B din BAYERPAT`; without
  one, the output is unchanged (covered by the goldens, but say it here too).

### 3.6 Golden and crosscheck

- Every existing golden file stays **byte-identical**. If `FLATCHECK_UPDATE_GOLDEN`
  turns out to be needed, something leaked into the console path - stop and find it.
- New golden `expo_cfa_bayerpat.txt`: the `expo_cfa` fixture plus a `BAYERPAT = 'RGGB'`
  card. New scenario of the same name in `CrossCheckFixtures` (`DumpsFixtures`) and in
  `crosscheck.py`'s scenario list, so the labelled line is parity-tested against
  `flatcheck.py` after 1.9. Expected status afterwards: 39 of 40 agree numerically.

### 3.7 `FlatCheck.Wpf.Tests`

`SessionFixture.WriteAnalysableRawFlat` gains `string? bayerPat = null`; when given, the
card is appended.

`ExposureTabTests`:

- `PickingAFileFillsFourHistogramRows`: 4 rows, `Whole.Length == Centre.Length == 200`,
  every array's max is 1.0 (or all-zero), exactly one `IsBrightest`.
- `WithoutABayerPatternTheRowsAreLabelledByPosition`: labels `CFA[0,0]..CFA[1,1]`, and
  `ChannelOrderNote` contains `"Nu stiu"`.
- `WithABayerPatternTheRowsAreNamed`: `R, G1, G2, B`, brightest is `G1`, and
  `ChannelOrderNote` contains `"RGGB"` and `"BAYERPAT"`.
- `TheSkySwitchMovesTheBandOnEveryRow`: `BandLo` 50 -> 15, `BandHi` 65 -> 50, `TooLow`
  equals `BandLo` on sky.
- `TheReadingNamesTheDecidingChannelAndTheBand`: `Reading` non-empty, contains the
  brightest label.
- `ADebayeredFileGetsOneMonoRow`: 1 row, label `mono`.
- `ResetClearsTheHistograms`: `Histograms` empty, `Reading` empty; `HistogramGuide.IsExpanded`
  set to true beforehand stays true.

`PresentationTests`: `ChannelPalette.BrushFor` maps `R/G/B` to three distinct frozen
brushes and anything else to `Unknown`.

`WindowSmokeTests`:

- `TheExposureTabRendersAResult`: after the first render, set
  `vm.HistogramGuide.IsExpanded = true` and render again as `tab-expunere-ghid`. Bindings
  inside a collapsed card are never evaluated - the same reason the map guide is
  rendered open.
- `TheExposureTabRendersBeforeAnythingIsPicked` already covers the empty `HistogramCanvas`
  path (all DPs default) - keep it, it is now load-bearing.
- New `TheExposureTabRendersAMonoResult`: the debayered fixture through the view-model,
  rendered, no binding errors - one row with no colour name.

`LicenseHeaderTests` will demand the GPL block on every new file: `CfaPattern.cs`,
`ExposureBands.cs`, `ChannelPalette.cs`, `HistogramCanvas.cs`, `HistogramGuidePanel.xaml`,
`HistogramGuidePanel.xaml.cs`, and the new test files.

### 3.8 The real file

Before handing over, run once against the file the order was measured on:

```bash
FLATCHECK_NEF="E:/Colectie Poze/2026/Balea Lac/RAWs/DSC_0209.NEF" dotnet test FlatCheck.Tests --filter ARealRaw
```

and open the window on a session whose `flats/` holds real NEFs, to look at the four
charts with eyes - the smoke test proves they draw, not that they read well.

## Part 4 - Documentation

**`README.md`**

- Section *4. Expunere*: after the CFA paragraph, a short paragraph: the tool now names the
  channels when the file says which is which - `filters`/`cdesc` on a raw, `BAYERPAT` on
  a FITS from Conversion - and prints `R (CFA[0,0])` etc.; otherwise the positions stay
  anonymous, on purpose, because the order differs between cameras. State the measured
  D3300 value (`RGGB`) as an example, not a rule.
- Section *Cele patru taburi*, the **Expunere** paragraph: the histograms - two curves per
  channel, the band, the clipping strip, the marker - and a table mirroring 2.6's rows,
  headed with the same warning the map guide's table carries: mirrored by hand, no test
  binds the two.
- *Diferențe față de versiunea Python*: the label source reads `libraw` in C# and `rawpy`
  in Python on a raw, same as the scale-source line.

**`CLAUDE.md`**

- *Structure*: `CfaPattern.cs`, `Analysis/ExposureBands.cs`, the `LevelHistogram` /
  `ChannelLevels` types under `ExposureAnalyzer`, `HistogramCanvas` and `ChannelPalette`
  under `Presentation/`, `HistogramGuidePanel` under `Views/`.
- *4. Exposure*: the channel order - measured value, per-camera, read from the file, the
  `cdesc` index-3 trap; the histograms (axis, bins, two curves, computed in the same
  loop); `ExposureBands` as the single definition `Classify` and the chart share.
- *What the window adds, and what it does not*: the histogram card is the third thing
  computed in the core but shown only by the window (after `MapScaleLabel`/`MapLegend`);
  the guide mirrors the README by hand.
- *Parity with Python*: 39 of 40; the new `expo_cfa_bayerpat` scenario; the
  `libraw`/`rawpy` label-source divergence.

## Part 5 - Order of work

Each step builds and tests green on its own; none needs the next.

1. **`CfaPattern` + readers** - 1.1, 1.2, 1.3, 1.4, tests 3.1-3.3. Nothing downstream
   reads `Cfa` yet; the change is invisible to the CLI.
2. **`ExposureBands`** - 1.5 and the `Classify` refactor, test in 3.4. Pure refactor;
   goldens prove it.
3. **Histograms and labels in the analyzer** - 1.6, 1.7, the rest of 3.4. Goldens still
   untouched because the report has not changed.
4. **Report** - 1.8, 3.5, the new golden + crosscheck scenario (3.6), the Python mirror
   (1.9). Run `crosscheck.py`.
5. **Chart primitives** - 2.2, 2.3, `PresentationTests`. No view-model yet; they can be
   exercised from a test alone.
6. **View-model, messages, XAML** - 2.1, 2.4-2.9, 3.7. Then the smoke tests, then
   `FLATCHECK_DUMP_SCREENS` and look.
7. **Docs** - Part 4.
8. `dotnet test FlatCheck.sln`, the `FLATCHECK_NEF` run, `crosscheck.py`. Hand over the
   diff and the commit message; do not commit.

## Commit message to hand over (adjust to what was actually done)

```
histograme pe canal in tabul de expunere, si numele canalelor CFA

Tabul arata acum, pe fiecare pozitie Bayer, histograma intregului canal si a
ferestrei centrale de 20 %, pe axa procentuala a verdictului, cu banda tinta,
dunga de clipping si mediana centrului desenate peste. Sub ele, un text dedus
din rezultat spune cum e expus fisierul, si un ghid pliat spune cum se citeste
desenul. Verdictul nu s-a schimbat si nu se uita la desen.

Canalele se numesc R/G1/G2/B cand fisierul spune ordinea - filters + cdesc pe
raw, BAYERPAT pe FITS - si raman CFA[y,x] altfel. Nu se ghiceste: ordinea e
per aparat. Masurat pe un D3300: RGGB, cu al doilea G pe indexul 3 din cdesc,
deci decodarea trece prin cdesc pentru fiecare pozitie.

Benzile verdictului au acum o singura definitie, ExposureBands, citita si de
Classify si de desen. Histogramele se umplu in bucla pe care analizorul o
facea deja; nimic din ce se calcula inainte nu s-a mutat.

Consola nu tipareste histograme; linia canalelor primeste numele culorilor
doar cand ordinea e cunoscuta, deci niciun fisier golden existent nu se
schimba. Un scenariu nou cu BAYERPAT o pinuieste, in golden si in crosscheck,
iar flatcheck.py tipareste aceeasi linie.
```
