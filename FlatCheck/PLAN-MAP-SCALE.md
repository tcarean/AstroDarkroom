# Plan: fixed colour scale and legend for the residual map (window app)

Read `CLAUDE.md` first. Everything below obeys it: nothing is computed or worded outside
`FlatCheck.Core`, no new dependency, no version bump, no commit, GPL header on every new
file, user-facing text in Romanian, code documentation in English.

## Why

The residual map is auto-scaled to each image's own 1st/99th percentile (plus 10 %
padding) and carries no legend. Measured on two real masters of the same lens: full red
meant +3 % in one map and +10 % in the other. The two maps could not be compared by
colour, only by shape, and nothing on screen said so. The user asked for a way to view
the map on a fixed scale, switchable between a few caps, plus a legend that states what
the colours mean.

## Decisions already taken (do not reopen)

| decision | value | why |
|---|---|---|
| where the caps live | `FlatCheck.Core`, derived from `Th.ResidPerfect/Usable/Check` | the caps *are* the residual scale's band boundaries, so a map at ±10 % saturates exactly where the verdict leaves `BUN`. Two copies of the numbers would diverge. |
| the caps | auto, ±2 %, ±10 %, ±20 % | the three band boundaries, plus the current behaviour for reading the *shape* of a near-perfect master |
| default | ±10 % | the band a normal flat is expected to live in; makes two sessions comparable without clicking |
| control | radio chips, one per cap, same pattern as the Ratio tab's map toggle | you see all options and which is active; a cycling button hides both |
| which tabs get chips | Structure and Ratio | both show a residual on the residual scale. The Ratio chip applies to whichever of its two maps is displayed. |
| which tabs get a legend | Structure, Ratio, End-to-end | the legend costs nothing and every map needs it. End-to-end gets **no chips**: its thresholds are `Th.LightRadOk/Bad` (2/5 %), not the residual scale, so the caps would mean nothing there. |
| the shape guide | Structure and Ratio, collapsed by default; text in `SessionMessages` | it is documentation shown in the window, and that already has one home (the Ratio tab's docs). Collapsed, so a run does not open a screen of prose under every map. |
| saved `flatcheck.png` | stays **auto**, unchanged bytes | the console writes auto; both applications must write the same file for the same master. The chips are a viewing aid. |
| console output | **unchanged** | a legend line in the console would change every golden file *and* break `crosscheck.py` text parity with `flatcheck.py` unless mirrored there. Not worth it for a line the console user cannot act on. A `--scala` flag can come later; the Core pieces below are shaped so it would need no new number. |
| chip selection across runs | survives `Reset()` | it is a viewing preference, not a result. The rasters and encoded maps are cleared. |

## Part 1 - Core

### 1.1 `PngWriter.cs`

Add a range type and split the auto range out of `MapResidual`, so the auto range can be
computed once, reported in the legend, and reused without a second encode.

```csharp
/// <summary>Capetele hartii, in unitatile rasterului (in jurul lui Mid = 1.0).</summary>
internal readonly record struct MapRange(double Lo, double Hi);

/// <summary>Scara automata: percentilele 1/99 plus 10 % padding. Neschimbata din 1.0.</summary>
internal static MapRange AutoRange(Img resid)

/// <summary>Scara fixa, simetrica: +/- cap procente in jurul lui 1.0.</summary>
internal static MapRange FixedRange(double capPct) => new(1 - capPct / 100, 1 + capPct / 100);

/// <summary>Plafoanele oferite ca scara fixa: granitele scarii de reziduu.</summary>
internal static readonly double[] FixedCapsPct = { Th.ResidPerfect, Th.ResidUsable, Th.ResidCheck };

/// <summary>Plafonul implicit al ferestrei.</summary>
internal const double DefaultCapPct = Th.ResidUsable;

internal static (byte[] Rgb, double Lo, double Hi) MapResidual(Img resid, MapRange range)
internal static byte[] EncodeResidualMap(Img resid, MapRange range)
```

Keep the existing signatures as thin wrappers: `MapResidual(resid)` =
`MapResidual(resid, AutoRange(resid))`, `EncodeResidualMap(resid)` =
`EncodeResidualMap(resid, AutoRange(resid))`. The auto path must stay **byte-identical**
to today; a test pins it (see 3.1). `Colormap` is untouched: it already takes `lo`/`hi`.

Update the file header comment: the range is now either auto or fixed, the fixed caps are
the residual-scale boundaries, and the note about white pinned at 1.0 still applies to
both.

`Th.Resid*` are `public const`; `FixedCapsPct` can be a static readonly array. Do not add
anything to `Thresholds.cs` - these are not thresholds, they are the same thresholds
reused.

### 1.2 `Report/RunReport.cs` - the wording

All Romanian text lives in `Report/`. Two new pure functions:

```csharp
/// <summary>Eticheta unui chip de scara: "auto" sau "± 10 %".</summary>
internal static string MapScaleLabel(double? capPct)
    => capPct is null ? "auto" : $"± {capPct.Value:F0} %";

/// <summary>Legenda de sub harta, cu capetele reale ale scarii afisate.</summary>
internal static string MapLegend(PngWriter.MapRange r)
    => $"albastru = {(r.Lo - 1) * 100:+0.0;-0.0} %   alb = 0 (exact pe model)   rosu = {(r.Hi - 1) * 100:+0.0;-0.0} %";
```

The legend expresses the ends as percent from 1.0, because every map is centred on 1.0
(see the `PngWriter` header). For a fixed range it reads `albastru = -10.0 %   alb = 0
(exact pe model)   rosu = +10.0 %`; for auto it shows the asymmetric real numbers, e.g.
`-3.2 %` / `+2.8 %`. Numbers use the invariant culture like everything else
(`App.ForceInvariantNumbers` covers the window; pin it in `CultureTests`, see 3.2).

Nothing in the console calls either function. That is deliberate (see the decisions).

### 1.3 Results and runs - no change

`StructureResult.Residual`, `RatioResult.Sensor.Residual`, `RatioResult.World` and
`LightResult.RadialMap` already carry the raster the map was encoded from, and
`RunOutcome.Result` hands the result to the caller. So the window can re-encode from the
raster it already receives. **Do not** add pre-encoded variants to `RunOutcome`; four PNGs
per run would be ~7 MB and three encodes nobody may look at.

The rasters are at analysis resolution (~1504 x 1004 doubles, ~12 MB each). The window
keeping one or two of them alive per tab is fine. Never keep the full-resolution frame.

## Part 2 - Window

### 2.1 `ViewModels/MapScaleViewModel.cs` (new file, GPL header)

One small class shared by the Structure and Ratio tabs, so the chip logic exists once:

```csharp
public sealed class MapScaleOption : ObservableObject
{
    public string Label { get; }        // RunReport.MapScaleLabel(CapPct)
    internal double? CapPct { get; }    // null = auto
    public bool IsSelected { get => ...; set { if (Set(...) && value) Owner.Select(this); } }
}

public sealed class MapScaleViewModel : ObservableObject
{
    public IReadOnlyList<MapScaleOption> Options { get; }   // auto, then PngWriter.FixedCapsPct in order
    public MapScaleOption Selected { get; }                 // starts at PngWriter.DefaultCapPct
    internal event Action Changed;                          // the owning tab re-encodes
    internal PngWriter.MapRange RangeFor(Img resid)         // Selected.CapPct is null ? AutoRange(resid) : FixedRange(cap)
}
```

`CapPct` is `internal` on purpose: XAML binds `Label` and `IsSelected` only, and the class
being public must not leak a Core type (see the note in `TabViewModel.cs`). `MapRange` is
a Core type, so `RangeFor` is `internal` too.

Selecting an option unselects the others in the view-model (not only in the radio group),
so a test can drive it without a window.

### 2.2 `ViewModels/StructureTabViewModel.cs`

- Add `public MapScaleViewModel Scale { get; } = new();` and `public string MapLegend`.
- Keep the raster: `Img _residual` set from `outcome.Result.Residual` in `RunAsync`.
  `Save(session, outcome.Png)` stays exactly as it is - the file is the auto encoding.
- `Map` becomes the encoding of `_residual` at `Scale.RangeFor(_residual)`; recompute on
  `Scale.Changed` and after a run. Cache the bytes per option in a small
  `Dictionary<MapScaleOption, byte[]>` cleared on every run and on `Reset()`, so clicking
  back and forth does not re-encode.
- Encode synchronously on the UI thread, then **measure** on a real 24 MP master (raster
  1504 x 1004). Expected well under 200 ms. If it is not, move the encode into `Task.Run`
  behind a generation counter so a late result from a superseded click is dropped - do not
  add this complexity speculatively.
- `MapLegend = RunReport.MapLegend(range)` alongside every `Map` update.
- `Reset()`: null `_residual`, clear the cache, `Map = null`, `MapLegend = ""`. Do **not**
  reset `Scale.Selected`.

### 2.3 `ViewModels/RatioTabViewModel.cs`

Same as 2.2, with two rasters: `Img _sensorResidual` (`outcome.Result.Sensor.Residual`)
and `Img _world` (`outcome.Result.World`). `Map` is the encoding of whichever
`ShowWorldMap` selects, at the current scale; `MapLegend` follows the displayed map. The
cache key is (map, option). `_map`/`_worldMap` byte fields go away - the rasters replace
them.

The existing test `BothMapsAreAvailableAndTheToggleSwitchesThem` must keep passing
unchanged.

### 2.4 `ViewModels/LightTabViewModel.cs`

Legend only: `public string MapLegend = RunReport.MapLegend(PngWriter.AutoRange(outcome.Result.RadialMap))`
when the run is not refused; `""` on refusal and on `Reset()`. No chips, no raster kept -
`Map = outcome.Png` stays.

### 2.5 XAML

`Views/StructureView.xaml`, map column: insert a row between the heading and the card,
mirroring `RatioView.xaml`'s toggle row:

```xml
<ItemsControl Grid.Row="1" ItemsSource="{Binding Scale.Options}" Margin="0,8,0,8">
  <ItemsControl.ItemsPanel>
    <ItemsPanelTemplate><StackPanel Orientation="Horizontal" /></ItemsPanelTemplate>
  </ItemsControl.ItemsPanel>
  <ItemsControl.ItemTemplate>
    <DataTemplate>
      <RadioButton GroupName="StructureMapScale" Margin="0,0,16,0"
                   Content="{Binding Label}"
                   IsChecked="{Binding IsSelected, Mode=TwoWay}" />
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

Two traps, both real:

- **Group names must differ per tab** (`StructureMapScale` vs `RatioMapScale`). WPF groups
  radio buttons by `GroupName` across the whole window, so a shared name would make the
  Structure chips uncheck the Ratio chips.
- The Ratio toggle's XAML comment explains why it binds one button `OneWay` through the
  `Not` converter. That trap does not apply here - there is no converter - but keep the
  `TwoWay` binding on `IsSelected` and let the view-model own exclusivity.

Below the map card, replace the current fixed sentence with two `TextBlock`s: the legend
(`{Binding MapLegend}`, style `Faint`, on its own line) and the existing explanatory
sentence under it. Same in `RatioView.xaml` (chips row added *above* the existing
world/sensor toggle row, or beside it - pick what reads better at 1360 px wide and check
the smoke-test render) and `LightView.xaml` (legend only, under the map card, respecting
the `HasNumbers` visibility).

A shift of the row indices in the `Grid.RowDefinitions` is expected; count them.

### 2.6 The map guide - what the shapes mean and how to read the map

Under the legend, the Structure and Ratio tabs get a collapsible guide. It is
documentation, not a diagnosis: the tool does not classify the shape, the person does,
and the guide tells them what each shape has meant on real masters. It follows the one
precedent for in-window documentation, the Ratio tab's `SessionMessages.RatioNeedsTwoMasters`.

**Where the text lives.** `FlatCheck.Wpf/Session/SessionMessages.cs`, next to the Ratio
documentation, as two pure functions:

```csharp
/// <summary>Cum se citeste harta reziduala - intro-ul de deasupra listei de forme.</summary>
internal static string MapGuideIntro()

/// <summary>Formele intalnite pe harta si ce au insemnat, in ordinea in care merita cautate.</summary>
internal static IReadOnlyList<(string Shape, string Meaning)> MapGuideShapes()
```

Rows rather than one block of text, so the window can render each shape as a bold name
with its meaning beside it, like `StructureReport.Scale()` renders the scale. Add a
`public sealed class GuideRow { Shape, Meaning }` to `ViewModels/Rows.cs` and a
`GuideRowTemplate` in `Theme/Dark.xaml` modelled on the scale row template.

**The text.** Use these sentences; they are the ones already validated on the ISO400 /
ISO800 pair of the Nikkor 35 DX. Plain ASCII Romanian like the rest of the output (no
diacritics), because the saved report and the console use none and the window should
not look like a different program.

Intro (`MapGuideIntro`):

> Harta e imaginea impartita la modelul radial: alb inseamna exact cat prezice un model
> pur radial in jurul centrului senzorului, rosu mai luminos decat modelul, albastru mai
> intunecat. Tot ce era simetric radial a fost scos, deci ce a ramas NU poate fi
> vignetare. Citeste intai cifrele, care spun CAT, si abia apoi forma, care spune DE CE.
> Intensitatea culorii depinde de scara aleasa mai sus: pe "auto" culorile umplu mereu
> tot intervalul, chiar si cand cifrele sunt minuscule - uita-te la legenda.

Shapes (`MapGuideShapes`), in this order:

| Shape | Meaning |
|---|---|
| `pete fara directie` | Abateri mici, fara o axa preferata. Semnatura difuzorului plus zgomotul cerului. Background extraction le scoate fara efort; cifrele decid daca sunt de ignorat. |
| `dipol (o parte albastra, cealalta rosie)` | Un gradient, deci o directie. Se potriveste cu "inclinare". Sursa neuniforma, lumina ambientala, sau un obstacol/difuzor intins asimetric peste obiectiv. Tabul Ratio spune daca e fix in lume (cerul) sau fix pe aparat (difuzorul, parasolarul). |
| `cercuri sau arce concentrice, descentrate` | Modelul radial absoarbe orice inel centrat pe senzor; un inel care ramane e obligatoriu centrat in alta parte. Caderea de lumina e descentrata: ceva rotund din fata obiectivului - elastic, parasolar, filtru, marginea difuzorului - e in afara axei. Apare de obicei impreuna cu un dipol. |
| `pata locala intr-un colt sau la o margine` | Light leak: vizor, montura, latura difuzorului. Nu se roteste cu cerul, deci Ratio o pune pe aparat. |
| `pata rotunda in centru` | Iluminare contaminata simetric fata de axa: un panou prea aproape, backlight-ul unui monitor. Testul prin rotatie e orb la ea prin constructie; contra-testul e distanta - un set la 30 cm, unul la 60. |
| `granulatie fina peste tot` | Zgomot, nu structura: prea putine cadre sau expunere prea joasa. Cifrele raman mici; nu e nimic de reparat la sursa, doar de stivuit mai mult. |

**How it shows.** Collapsed by default, so the map column does not grow by a screen of
text on every run. Prefer a `ToggleButton` with `Content="Cum citesc harta"` bound
`TwoWay` to a `public bool ShowMapGuide` on the tab view-model, and a panel under it
with `Visibility="{Binding ShowMapGuide, Converter={StaticResource BoolToVis}}"` - both
pieces already exist in the theme, so no new control style is needed. An `Expander` is
fine instead **only** if `Dark.xaml` already styles one; do not add a style for it just
for this. The panel is a `StackPanel`: the intro as a `Faint` wrapped `TextBlock`, then
an `ItemsControl` over `MapGuide` rows with `GuideRowTemplate`.

Put the text on the view-models as instance properties (`public string MapGuideIntro`,
`public IReadOnlyList<GuideRow> MapGuide`), for the same reason the Ratio documentation
is: `{Binding}` resolves through the DataContext and never sees a static. Both Structure
and Ratio tabs expose the same two, from the same `SessionMessages` functions.
`ShowMapGuide` survives `Reset()`, like the chip selection.

The End-to-end tab gets **no guide**: its map is the fitted radial term, a smooth
paraboloid whose sign is already the diagnosis printed beside it. The shape vocabulary
above would not apply.

## Part 3 - Tests

### 3.1 `FlatCheck.Tests/PngWriterTests.cs`

- `EncodeResidualMapWithTheAutoRangeIsByteIdenticalToTheOldEntryPoint` - encode a
  synthetic residual both ways, `Assert.Equal` on the bytes. This is what keeps the saved
  file and the console output unchanged.
- `TheAutoRangeIsP1P99PlusTenPercentPadding` - on a known raster, `AutoRange` returns the
  numbers the old inline code produced (compute them in the test with `Stats.Percentile`).
- `AFixedRangeIsSymmetricAroundOne` - `FixedRange(10)` is `(0.9, 1.1)`.
- `AFixedRangeSaturatesBeyondItsCap` - a 2x2 raster with `0.75, 1.0, 1.25, 1.05` at cap 20:
  decode with `PngDecoder` and assert pure blue, white, pure red, and a pink that is not
  pure red.
- `TheFixedCapsAreTheResidualScaleBoundaries` - `FixedCapsPct` equals
  `{ Th.ResidPerfect, Th.ResidUsable, Th.ResidCheck }` and `DefaultCapPct == Th.ResidUsable`.
  This is the test that fails if someone edits one place and not the other.

### 3.2 `FlatCheck.Tests` - report wording

In the existing report test file that fits best (or a small new `RunReportTests.cs`):

- `MapScaleLabel(null)` is `"auto"`, `MapScaleLabel(10)` is `"± 10 %"`.
- `MapLegend(FixedRange(10))` is exactly
  `"albastru = -10.0 %   alb = 0 (exact pe model)   rosu = +10.0 %"`.
- `MapLegend` on an asymmetric auto range prints both signs and one decimal.

`FlatCheck.Wpf.Tests/CultureTests.cs`: add the legend to whatever it already asserts
under a comma-decimal culture - it must print `-10.0`, never `-10,0`.

### 3.3 `FlatCheck.Wpf.Tests/StructureTabTests.cs`

- `TheDefaultScaleIsTheUsableBand` - after a run, `Scale.Selected.Label == "± 10 %"`.
- `TheSavedMapStaysOnTheAutoScale` - `File.ReadAllBytes(session.ResidualPngPath)` equals
  `PngWriter.EncodeResidualMap(StructureAnalyzer.Analyze(ImageLoader.LoadImage(path)).Residual)`.
  Pins the decision that the file does not follow the chip.
- `ChoosingAScaleChangesTheMapAndTheLegend` - select auto, then ±2: `Map` bytes differ,
  `MapLegend` differs and contains `-2.0 %`.
- `TheLegendOnAutoShowsTheRealRange` - on auto, the legend equals
  `RunReport.MapLegend(PngWriter.AutoRange(residual))` for the same fixture.
- `TheSelectionSurvivesResetButTheMapDoesNot` - select ±20, `Reset()`: `Map == null`,
  `MapLegend == ""`, `Scale.Selected` still ±20.
- `ReselectingAScaleReturnsTheSameBytes` - deterministic (and cached) encoding.

### 3.4 `FlatCheck.Wpf.Tests/RatioTabTests.cs`

- `TheScaleAppliesToBothMaps` - at ±10, toggle `ShowWorldMap`: both maps non-null and
  different; switch to ±2: both change again.
- `TheLegendFollowsTheDisplayedMap` - the legend on auto differs between the sensor map
  and the world map (their auto ranges differ).

### 3.5 `FlatCheck.Wpf.Tests/LightTabTests.cs`

- `AConclusiveRunHasALegend` / `ARefusedRunHasNone`.

### 3.5b The map guide (`SirilSessionTests.cs` or a new `SessionMessagesTests.cs`, plus the tab tests)

- `TheMapGuideNamesEveryShapeItExplains` - `MapGuideShapes()` has six rows, none with
  an empty side, and the shape names include `dipol`, `cercuri` and `pata`.
- `TheMapGuideIntroSaysWhatWhiteMeans` - the intro contains `alb` and `model radial`;
  cheap, but it is what stops the intro from being edited into something that no
  longer explains the colours.
- `TheMapGuideUsesNoDiacritics` - every character of intro and rows is ASCII, like
  the rest of the output.
- Structure and Ratio tab tests: `TheGuideIsAvailableBeforeAndAfterARun` - `MapGuide`
  and `MapGuideIntro` are non-empty on a fresh view-model and unchanged after a run
  (documentation does not depend on a result); `TheGuideStartsCollapsed` -
  `ShowMapGuide` is `false` initially and survives `Reset()` once set.
- `WindowSmokeTests`: in `EveryTabRendersWithRealResultsAndNoBindingErrors`, set
  `vm.Structure.ShowMapGuide = true` and `vm.Ratio.ShowMapGuide = true` before the
  renders, so the guide's bindings and its row template are exercised and the dump
  shows the expanded state. Look at the dump: six rows must fit under the map at
  1360 x 800 without pushing the map off the column; if they do not, the panel
  needs a `MaxHeight` with a `ScrollViewer`, not smaller text.

### 3.6 Existing suites

- `WindowSmokeTests` needs no new test: `EveryTabRendersWithRealResultsAndNoBindingErrors`
  renders the new controls and fails on any binding error. Run it with
  `FLATCHECK_DUMP_SCREENS=<dir>` once and look at `tab-structura.png` and `tab-ratio.png`
  to check the chips fit on one row.
- `LicenseHeaderTests` will fail until the new `.cs` file carries the notice.
- Golden files: **must not change**. If `FLATCHECK_UPDATE_GOLDEN=1` seems necessary, the
  console output was touched by mistake.
- `crosscheck.py`: no change on the Python side is needed because no console text changed.
  Running it is still the right final check.

## Part 4 - Documentation

- `README.md`, section "Ieșire PNG": one paragraph - in the window the map can be viewed
  on a fixed scale of ±2 / ±10 / ±20 % (the band boundaries of the residual scale, ±10
  by default) or on the automatic one; a legend under the map states the real ends of the
  displayed scale; the saved `flatcheck.png` and the console's `-o` file are always on the
  automatic scale. Also add one sentence to the **Structura** and **Ratio** paragraphs in
  the window section, next to where the Ratio toggle is described.
- `README.md`, section "Ieșire PNG": the shape guide belongs here too, as a short table
  with the same six rows - the README is the single source of user documentation, and
  the in-window guide is a copy of it for people who never open the README, exactly as
  the Ratio tab's documentation is. Keep the two in step by hand; there is no test
  binding them, so say so in a comment next to `MapGuideShapes`.
- `CLAUDE.md`, section "PNG output": the range is auto or fixed, fixed caps come from
  `Th.Resid*`, the console stays auto and why (golden files + crosscheck parity). Section
  "What the window adds": add the chips, the legend and the guide to the list of things
  the window adds *without* adding a number, and note that End-to-end deliberately has
  neither chips nor guide. Section "Structure" of the WPF project description: mention
  that `SessionMessages` now also holds the map guide.
- Do not bump `<Version>`. Mention to the user that this is a visible feature and a bump
  is their call.

## Part 5 - Order of work

1. Core: `PngWriter` range split + `RunReport` wording, with the tests in 3.1 and 3.2.
   Run `dotnet test FlatCheck.sln` - everything old must still pass, including golden.
2. `MapScaleViewModel`, then Structure tab + XAML, with tests in 3.3. Run the smoke test
   with a screen dump and look at it.
3. Ratio tab + XAML, tests in 3.4.
4. Light tab legend, tests in 3.5.
5. The map guide: `SessionMessages` text, `GuideRow` + template, the toggle on both
   tabs, tests in 3.5b. Dump the screens again with the guide expanded and look.
6. README and CLAUDE.md.
7. `dotnet test FlatCheck.sln` and `crosscheck.py` one last time. Hand over a commit
   message (lowercase subject, body explains *why*, no trailers). Do not commit.

## Commit message to hand over (adjust to what was actually done)

```
fixed colour scale and legend on the residual map

The map was auto-scaled to each image's own percentiles with no legend,
so full red meant +3 % on one master and +10 % on another of the same
lens, and nothing on screen said so. The window now offers the map on a
fixed ±2 / ±10 / ±20 % scale - the band boundaries of the residual
scale, so saturation means "outside this band" - with ±10 as default and
the automatic scale still available for reading the shape of a
near-perfect master. A legend under every map states the real ends of
the displayed scale.

The caps are derived from Th.Resid* in the core and the wording lives in
RunReport, so the console could grow a --scala flag without a new
number. The console output and the saved flatcheck.png deliberately stay
on the automatic scale: both applications keep writing the same file for
the same master, the golden files do not move, and crosscheck.py parity
with flatcheck.py holds without touching the Python side. End-to-end gets
the legend but no chips, because its thresholds are not the residual
scale.

Under the legend, Structure and Ratio also carry a collapsed guide to
the shapes seen on real masters - blotches, dipole, off-centre rings,
corner patch, central spot, grain - and what each has turned out to
mean. It is documentation, not a classifier: the tool still does not
decide what a shape is, it tells the person what to look for. Same text
as the README's table, kept in step by hand.
```
