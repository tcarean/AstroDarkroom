# Code map: what each file is for and why it is shaped that way

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): the layout is in CLAUDE.md ("Layout"), the
> decisions with their reasons in `docs/decisions/`, and this note keeps the per-file mechanics
> the code comments do not repeat, plus the result and outcome tables. **Covers:** every file
> under `FlatCheck.Core/` (`Version.cs`, `ThirdParty.cs` and `PreviewReader.cs` only by pointer);
> the console's three files, `FlatCheck/Options.cs`, `FlatCheckCli.cs` and `Program.cs`; the
> window's `Interop/`, `Presentation/`, `Session/`, `ViewModels/` and `Views/`; of the two test
> projects, what their project files set up, `FlatCheck.Tests/FlatCheck.Tests.csproj` and
> `FlatCheck.Wpf.Tests/FlatCheck.Wpf.Tests.csproj`; and `Icon/`. It only points elsewhere for
> `Directory.Build.props`, `Theme/Dark.xaml`, `lensfun/`, `flatcheck.py`, `README.md`, `LICENSE`
> and `docs/`, and describes neither the tests themselves nor the window's `Mvvm/` and `App`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

What the tool is and what its five modes answer is the first paragraph of CLAUDE.md; each
mode has its own note (`modes-*.md`, mapped by the index). The dependency rule, the hand-rolled
FITS and PNG and the three packages are ADR 0008, 0009, 0010 and 0070 with `dependencies.md`; what each
of the two applications owns is ADR 0011 with `two-applications.md`. The target is .NET 8.


## Structure

Five projects (`FlatCheck.Core`, `FlatCheck`, `FlatCheck.Wpf`, `FlatCheck.Tests`,
`FlatCheck.Wpf.Tests`). What each folder owns is CLAUDE.md ("Layout"); why the 2.0.0 split was
a library extraction and not a copy is ADR 0011.

- `Directory.Build.props` - `<Version>` and `<Copyright>`, shared by every project: ADR 0039
  and ADR 0040.
- `FlatCheck.Core/` - the compile boundary of ADR 0011.
  - `FitsReader.cs` - the hand-rolled FITS reader (ADR 0008). `HInt`/`HDbl`/`HStr` read single
    cards out of a header dictionary with a default, so a missing card is never an error - which
    is what lets `--light` degrade instead of throwing on a raw, which has no
    FITS header at all. `LoadHeader(path)` reads the header blocks alone, for the window's
    Sintetic tab, which wants `FOCALLEN` and `INSTRUME` out of a 96 MB master at open. Both
    reads refuse the same files with the same `InvalidDataException`, through two shared
    helpers: `RequireSimple` checks the file's magic, the keyword of the first card, before
    anything is parsed (an empty file, one shorter than a keyword, or a block whose first card
    is something else is "nu incepe cu SIMPLE"), and `KeyOf` is the one definition of a card's
    keyword that both card loops use; a header that begins with `SIMPLE` but ends before `END`
    is "header incomplet" in both. `LoadHeader` never answers with an empty dictionary that
    every caller would have to recognise by its count (R-008, ADR 0057). After a header, before
    a sample is read, `LoadFull` checks that the file holds the data area the header declares
    (`NAXISn` product times `|BITPIX| / 8`) and refuses a shorter one with the two byte counts
    ("data cut short"); the check runs for every HDU, the skipped ones too, and counts the data
    alone, so a last HDU without its padding to 2880 bytes is read (R-009). `LoadHeader` does
    not check the data area, so this is the one refusal the two reads do not share: the header
    of such a file is intact and the Sintetic tab can still use it.
  - `FitsWriter.cs` - the hand-rolled FITS writer, with one consumer: `--synthetic`. One primary
    2-D HDU, `BITPIX -32` or `16` (with `BZERO 32768`), the input's raw cards copied minus the
    structural ones it rewrites, `HISTORY` at the end. Writes into a `Stream` for the same
    reason the report is a `TextWriter` - 96 MB of float need not sit in memory twice; `Encode`
    over a `MemoryStream` is for tests. It copies the **raw 80-character cards**
    (`LoadedFrame.Cards`, kept by `FitsReader.LoadFull` alongside the dictionary), not the
    parsed dictionary, which loses comments and truncates strings at `/` (known issue 1).
    `LoadedFrame` also carries `Planes` (`NAXIS3`), so `SyntheticRun` can refuse a cube, and
    `Bitpix`, for the load line of the log (`--smooth` wrote the input's type back until its
    removal, ADR 0080).
  - `IFrameReader.cs` - the contract each input format implements: `Handles(path)` and
    `Load(path) -> LoadedFrame`. The only interface in the codebase, and the reason
    `ImageLoader` knows no reader's API.
  - `ImageLoader.cs` - the **only** place that decides which reader gets a file. Holds one
    stateless instance per format and asks each `Handles`; what nobody claims falls back
    to FITS, and that fallback is an explicit field rather than the last array element, so
    a reorder cannot break it silently. `Load` returns a `LoadedFrame` (pixels, FITS
    header, ADU scale, CFA colour order, embedded preview); the missing fields are the
    right answer per format, not errors - a raw has no FITS header, a FITS has neither an
    ADU scale nor a preview, and either may be silent about the mosaic order. Since the
    synthetic flat it also carries what the file knows about the shot - `FocalMm`,
    `FNumber`, `Camera`, `LensId`, `LensMount`, `LensName` - null where the format is silent
    (a Siril FITS has no f-number; a D3300 raw with third-party glass has no lens name but
    has the Nikon lens ID; a Canon has both, a Pentax the ID alone, ADR 0068). Nothing below it knows where the pixels came from, and that is what keeps the
    two formats from growing separate behaviour. `LoadInfo(path)` is the same dispatch
    **without pixels**: `Image` null, the shot fields filled - on a FITS from the header
    alone, on a raw from libraw's metadata at `OpenFile`, before `Unpack` (measured: tens of
    milliseconds, not seconds). It is what the lens identification (ADR 0030) and the
    Sintetic tab's prefill read, so neither decodes 24 MP for an f-number.
  - `RawReader.cs` - the only file that names a libraw type, loaded lazily, `FillShootingInfo`
    reading its fields as locals: ADR 0009. `IsRawPath` is the extension gate; `RawExtensions`
    exposes the same list to the WPF app's `flats/` filter, so a camera the reader learns is a
    camera the list offers. When LibRaw cannot be loaded, `CannotLoad` refuses the raw with
    R-122's message, which names the Visual C++ runtime on Windows, and logs the loader's
    exception with the probe: ADR 0082, the mechanics in `dependencies.md`.
  - `VcRuntime.cs` - the four DLLs of Microsoft's Visual C++ runtime that LibRaw's DLLs import,
    Microsoft's link to its latest package, and the probe `CannotLoad` runs: the Astro Darkroom
    launcher's list and words, kept here for the tool's own release, which has no launcher
    (ADR 0082).
  - `CfaPattern.cs` - which colour each of the four Bayer positions is, or `null` when the
    file does not say: what it reads (libraw's `filters` + `cdesc`, or `BAYERPAT` with
    `XBAYROFF`/`YBAYROFF` and `ROWORDER`), why it is never guessed and why no figure depends on
    it are ADR 0023.
  - `Mosaic.cs` - the CFA rule shared by `--expo` and `--histeq`:
    `PositionOf(y, x)`, `CfaSpreadMin` (1.15) with `LooksCfa`, `Split` into the four
    position rasters, and `Label`/`ColourOf` over a `CfaPattern` (`Extract` and `Merge`,
    which only `--smooth` used, left with it, ADR 0080). It used to be inline in `ExposureAnalyzer`;
    two copies of `(y & 1) * 2 + (x & 1)` and of `1.15` would have diverged at the first
    adjustment. `ExposureAnalyzer` uses the rule, not `Split` - it fills its histograms in
    the same pass it reads the pixels, and a second copy of the data would cost memory.
  - `PreviewImage.cs` - the camera's own rendering of the frame, in two forms:
    `EmbeddedPreview` (the bytes as they sit in the raw, captured while the file is open)
    and `PreviewImage` (decoded RGB, made only on request). Neither may decide anything
    (ADR 0024).
  - `PreviewReader.cs` - the only file that names a `SixLabors.ImageSharp` type, the null on
    any failure and the swap point: ADR 0010.
  - `Thresholds.cs` - `Th`. Note it does **not** hold every threshold; see below.
  - `Img.cs` - the 2-D raster plus its pure transforms: `BlockMean`, `Prepare`,
    `Divide`, `Symmetrise` (the mean of a raster and its 180° rotation, which is what the
    radial model is fed - see `RadialModel` under "Shared pipeline" in `pipeline.md`). `CropCentred` and `Rotate180`, which only the `--ratio` mode used, left with it
    (ADR 0067); `LargeScalePtp` keeps its top-left grid for parity (ADR 0016). `TileMedians`, the third grid, which
    only `--smooth` used, left with it too (ADR 0080).
  - `Stats.cs` - numeric primitives: `Percentile`, `Interp`, `Solve`,
    `Verdict`, `Rank`, `Severity`, and the verdict vocabulary (`ResidualBands`,
    `NotApplicable`).
  - `Metrics.cs` - the pure geometry and metrics: `FitCenter`, `RadialModel`,
    `CornerStats`, `AzimuthalAsymmetry`, `LargeScalePtp`, `MaxTileDeviation` (the worst
    tile - the fifth structure test), `LocalSpots` (the deepest compact spot after the
    large-scale structure is subtracted - informative, with a warning), `TileBackground`,
    plus `TileSize` - the one tile side the large-scale metrics and the attribution mode
    share (ADR 0016).
  - `Version.cs` - `VersionInfo.Value`/`Banner`/`Copyright`, read from the assembly:
    ADR 0039.
  - `ThirdParty.cs` - the third-party components as a *value*, what the window's "Despre"
    shows, bound to the csproj, to the DLLs of LibRaw's runtime package (`NativeFiles`) and to
    the notices file by `ThirdPartyTests`: ADR 0008, ADR 0040, ADR 0082 and the rule "Package
    notices". `NoticesUrl` is where each licence's whole text is online.
  - `LensProfiles/` - the lens vignetting profiles behind the synthetic flat.
    `VignettingModel` is the factor `V(x, y)` as a value, with the two coordinate
    conventions verified from sources (its header quotes them): Adobe's r in units of the
    focal length, lensfun's `pa` r at the half-diagonal of a 36x24 frame over the
    calibration crop. `LcpReader` (Adobe XMP, both the attribute and the element form,
    with the piecewise tables) and `LensfunReader` (`<lens>` + `<camera>` for crop
    factors) are pure over text; `ProfileLocator` is the only file that looks at the
    machine. `LensProfile` / `LensCatalog` hold the list: lensfun parsed up front, Adobe
    lazily from file-name stubs (`AdobeProfileFile.FromName`), for the measured reason in
    ADR 0028. A lensfun lens's identity includes its calibration crop (ADR 0029):
    `LensChoice.Calibration` is "APS-C 1.528" / "full frame 1"; `Where` = mount +
    calibration is what every list, table, report and FITS `HISTORY` shows, `Title` = name +
    calibration what the verdict sentences say. The label is on every lensfun lens, not only
    the duplicated ones, so a name cannot change when the database gains an entry; Adobe
    stubs carry none. Pinned by `Lensfun_TheSameLensCalibratedOnTwoFormatsIsTwoLenses`,
    `ALensfunLensCalibratedOnTwoFormatsIsTwoRows` and the two tab tests of the same name.
    A lensfun pair at 10 and 1000 m with identical coefficients is one profile with an
    unknown distance (ADR 0029): `LensfunReader.CollapseCopiedDistances` sets `DistanceM =
    NaN`, and the old "infinit" was a copied value presented as a fact - the same Nikkor
    entry has the floor of Adobe's 0.36 m profile, not the 1.5 m one. A single entry at a
    stated distance keeps it; a pair with different coefficients keeps both.
    `SensorFormats` is the six nominal crops (full frame 1.0 ... 1" 2.7) with the sensor
    sizes as notes - the fallback for a body the lensfun camera table (1053 entries, the
    one list of cameras, maintained upstream) does not have; the refusal text lists them
    and the window offers them as chips.
    `LensIdentification` is the **one** identification of the shot - focal, f-stop, crop,
    camera, each with its source, and the lens candidates - from a reference frame (the
    master, or the console's raw) and a raw beside it, both through `LoadInfo`; where it
    came from, how the candidates are chosen (`CandidatesByRangeOnly` for a manual lens) and
    `FirstRaw` are ADR 0030, `PreferCamera` ADR 0029.
  - `Analysis/` - one analyzer per mode, each a pure function into an `XxxResult`:
    `StructureAnalyzer`, `LightAnalyzer`, `ExposureAnalyzer`,
    `HistEqAnalyzer` (with `HistEqLut`, the Siril transfer table - see mode 5),
    `SyntheticFlatBuilder` (mode 7: a `VignettingModel` and a frame size in, the raster
    and its depth at four reference points out), `AgainstAnalyzer` (in `Runs/AgainstRun.cs`:
    two rasters prepared, normalised to median 1, their ratio through the structure battery),
    `VignettingComparison` (the sixth structure test: the master's corner depth against
    each candidate lens's nearest profile, measured with `CornerStats` on the model raster
    built at analysis size - see "The sixth test" under mode 1). Beside them, `ExposureBands` - the verdict ladder as a *value*
    (ADR 0022). `ExposureAnalyzer` also carries `LevelHistogram` and `ChannelLevels`, the
    per-channel distributions; they are filled in the loop the analyzer already ran and
    change no existing figure. Beside them `PreviewLevels` and `AnalyzePreview`, the same
    two types over the camera's own rendering - three channels, no bands, no verdict, and
    `Analyze` fills `ExposureResult.Preview` only when handed one.
    Two of them are not `Img -> Result`, deliberately: `LightSuitability` is
    `header -> SuitabilityResult` (it decides whether `--light` applies at all, before any
    pixel is read), and `LightSeriesAnalyzer` is `[LightResult] -> LightSeriesResult` (it
    combines per-frame results; the file loading stays in `Runs/`, like every other mode).
    `StructureAnalyzer.AnalyzePrepared` is the overload that skips `Img.Prepare` because the
    caller has already prepared its input; `AgainstAnalyzer` runs the battery through it on
    rasters of its own making.
    The members of every `XxxResult`, with their units, which of them vote and which are
    informative, and the `docs/validation.json` keys that read them, are tabulated under
    "Result records (Analysis/)" at the end of this note.
  - `Language/` - `Ui` (the run's language and the `T(english, romanian)` pick) and
    `LanguageArgument` (`--lang`); see `language.md`. `Report/VerdictWords.cs` prints a
    verdict key in the run's language.
  - `Logging/` - the diagnostic log (ADR 0070): `LogSetup` (the configuration the two
    applications apply), `LogText` (how a value and a result are written in a line) and
    `RunClock` (a run's first and last lines with the time of each stage); see `logging.md`.
  - `Report/` - one report per mode, each a pure `XxxResult + TextWriter -> text`
    function. `SplitReport` is the one with a variable number of columns: `Headers`,
    `Rows` of `SplitCell` (value + verdict), the report text with the date line, the map
    order line, the PNG title and labels (uppercase, glyphs only). `SyntheticReport` also carries the `--lenses` list, the refusals, and the two
    texts the window's sources panel shows (`AdobeWhere`, `LensfunWhere`). `LightSeriesReport`
    is split into `WriteHeader`/`WriteRow`/`WriteSummary` so a 32-frame run can stream
    its table (ADR 0011). `StructureReport.WriteBody` is split off for the same reason in
    the other direction: the attribution mode prints its own header and then wants that
    exact body - minus the ADU line, which means nothing on a raster normalised to a median
    of 1. `RunReport` holds the two lines that frame every mode - the version banner and the
    "map saved" line. It also holds `MapScaleLabel` and `MapLegend`, the two bits of
    wording that go with the map; they have no console caller today, and live here anyway so
    a future `--scala` reuses them instead of rewriting them. And `WarrantyLines` +
    `LicenseUrl`, the GPLv3 §5(d) notice (ADR 0040).
    Several pieces are exposed as values rather than only written, so that one definition
    serves both applications (ADR 0011): `StructureReport.Scale()`,
    `StructureReport.Advice()`, `StructureReport.TestNote()` (one sentence per deviation
    test, keyed by the `StructureResult` field name so reordering the rows cannot unpair a
    note from its metric), `StructureReport.Levels()` (the median / max line, formatted
    for a float master as well as an ADU one - both applications print it), the three
    `LocalSpot*` pieces (`LocalSpotValue` with the sign named in words, `LocalSpotNote`
    with the warning threshold read from `Th.LocalSpotWarn`, `LocalSpotWarning` or null -
    all three printed by the console too, see "1. Structure"), `LightReport.Diagnosis()`,
    `ExposureReport`'s `HistogramReading` / `ChannelNote` / `BandLabel` (the wording around
    the exposure histograms), `ScreenComparison` / `ScreenReading` / `NoPreviewReason` /
    `PreviewChannelNote` / `ScreenGap` (the raw-vs-screen table and its explanation) and
    `VerdictWord` / `Advice` / `ChannelsSummary` / `ChannelOrderNote` / `HistogramCaption`
    (the Expo tab's verdict card and caption; `Advice` is the console's `VerdictLines`, the
    one verdict tree, laid out as a head line and a paragraph, and on a file that is not a
    raw flat the console's `NotRawReasons` and typical cause, each reason one paragraph, with
    only the way out the window's; `ChannelsSummary` takes its figure from the same
    `ChannelMedianPct` as the console's channel rows), `LightReport.FieldText` /
    `FieldWarning` / `TooWideText` and `LightSeriesReport.CorrelationValue` / `ReasonsText`
    (the End to end tab's field line, refusal and reasons: `FieldText` is the console's
    `FieldValue` unpadded, the other lines joined into paragraphs, the Structure tab named
    where the console names the structure tests, and the focal-length-only note said without the console's
    `--fov` way out), and
    `StructureReport.CornerShares` / `VignetteNote` / `CentroidNote` (the notes under the
    Structure rows, through the console's ratios) - all window-only, like `MapScaleLabel`,
    and for the same reason. `StructureReport` also owns the profile block: `ProfileRows`,
    `MasterDepth`, `ComparisonSummary`, `ComparisonNote`, `DepthScaleRows` /
    `DepthScaleNote` (the depth scale as four bands in stops, `Th.Resid*` through
    `Stats.StopsForError`, and the sentence that it is the residual scale translated, not a
    new threshold - the user asked whether it was a second scale), `GapText` / `GapMeaning`
    (one sentence per `ComparisonGap`), `ShapeDetail` / `DepthDetail` (the two halves of
    the verdict; the console pads them into `VerdictDetail`, the window labels them itself)
    and `DepthAdvice`, which replaces `Advice` when the depth carried the verdict
    (ADR 0031); the window's card alone also takes `AdobeMissing`, `AdobeInstall`,
    `AdobeSearched` and `AdobeDngConverterUrl`, what a search that found no Adobe file says
    (ADR 0073). The console formats them into columns, the WPF app puts them in cards
    and under a coloured chip; the wording has one definition either way. `HistEqReport` has no verdict to write; what it
    exposes as values is `Caveat()` (what the picture cannot show), `MasterVersusFrame()`
    and `ViewCaption()` - window-only, like the others. `StructureReport.TestLabel`/`TestKeys`/`Test` exist so that the
    against and split reports cannot name a test differently from the structure report (they
    were added for the smoothing report, removed since, ADR 0080); the negative-zero rounding
    of `LocalSpotSize` is ADR 0041.
  - `Runs/` - the chain of one mode, the layer both applications call (ADR 0011):
    `StructureRun`, `ExposureRun`, `LightRun`, `LightSeriesRun`, `HistEqRun`,
    `SyntheticRun`, `AgainstRun`, `SplitRun`, plus `SeriesFinder` (which files a
    series run covers). `StructureRun.Analyze` is the first half of `Execute` (load, analyze,
    compare, no text, no map); `SplitRun` calls it once per master - the whole, then the
    parts - picks the whole's nearest profile as the reference for every column, computes
    one automatic colour range over all the residuals, and returns `SplitResult` with the
    mosaic as its `Png` (ADR 0054). `SplitCost` is the disk estimate shown before a split:
    the copies exact, Siril's `process/` from bytes-per-pixel constants measured on
    2026-09-28, no time. `StructureRun.Execute` takes an optional `StructureQuery` (profile
    sources, a raw beside the master, or `--lens` text, plus the overrides) and returns a
    `StructureOutcome`: the `StructureResult`, the `VignettingComparison` (null without a
    query; with one, present even when it could not compare - `Gap` says why) and the
    combined verdict. `StructureRun.Recompare` redoes the comparison, or the plain outcome,
    on a `StructureResult` already computed and writes the report again, no load and no
    map: the window's "no filters" box (R-118, ADR 0078, `two-applications.md`). A query's
    `LensKey` narrows the comparison to one candidate, the window's lens picker on a list
    found by range alone (R-121, ADR 0081, `modes-structure.md`).
    Without a query the output is byte-identical to before, which is
    what keeps the golden files, the parity ones included, still. `Compare` is the second
    half on its own, so the window can explain a missing raw without reloading the master.
    `SyntheticRun` can refuse (a colour cube - `LightRun`'s shape - and the lens choice) and
    produces a file: `WriteFits(outcome,
    sourcePath, Stream)` is called by the console on a `FileStream` and by the window after
    the overwrite question, never by the run itself (ADR 0011). `SyntheticRequest` carries
    every value with the source it came from, so the report can print "cerut" beside
    "profil folosit". `HistEqRun` also carries
    the per-view encoders (`EncodeChannel`, `EncodeMosaic`) the window calls on demand; the
    console only ever writes the composite. `ExposureRun` is the only one with an opt-in,
    `withPreview` (ADR 0024). The `TextWriter` and PNG-bytes shape of every outcome is
    explained in `Runs/RunOutcome.cs` and decided in ADR 0011.
    Every outcome type of this folder, member by member with its unit, and the `TextWriter`
    rule stated as a fact, are tabulated under "Run outcomes (Runs/)" at the end of this note.
  - `PngWriter.cs` - the hand-rolled PNG encoder (ADR 0008) and the residual colour map.
    `Encode(rgb, w, h)` is the real entry point; `EncodeResidualMap` is what both
    applications use, and `WriteResidualMap` is a thin file wrapper over it. It prints
    nothing - the "map saved" line comes from `RunReport`. `EncodeGrey` writes a grey
    raster as RGB with equal components; the encoder keeps its single colour type.
    `EncodeMosaic` lays several maps in one row on one `MapRange`, under a title band and a
    label band drawn with `BitmapFont`; the text scale follows the tile width.
  - `BitmapFont.cs` - a 5x7 bitmap font (digits, `A`-`Z`, a few signs; lowercase drawn as
    capitals, unknown characters blank but advancing, clipped at the canvas), the only text
    the PNG writer can draw (ADR 0054).
- `FlatCheck/` - the console app. `AssemblyName` is `flatcheck`, so the binary is
  `flatcheck.exe`, not `FlatCheck.exe`. Three files; their contract and how a flag or a mode
  is added are `process.md` ("Extending the console").
  - `Program.cs` - a `Main` shim over `FlatCheckCli.Run`, between the diagnostic log's first
    and last lines (`logging.md`), plus a header that points at `README.md`
    rather than restating it (the convention, and the drift that made it, are `process.md`,
    "Conventions").
  - `FlatCheckCli.cs` - `Run(args, stdout, stderr)`: mode dispatch, writing the map(s) to
    disk, and the exit code. All output goes through the injected `TextWriter`, never
    `Console` directly.
  - `Options.cs` - CLI parsing, and the mode the flags ask for (`Options.Mode`, decided once
    by R-003's precedence; the dispatch and the log both read it, `process.md`).
- `FlatCheck.Wpf/` - the window app, `net8.0-windows`. See `two-applications.md`.
  - `Session/` - `SirilSession` (what a session folder means on disk: which file is the
    master and where it may be, ADR 0034; which are raw flats; `SyntheticMasterPath`, the file the
    Sintetic tab puts beside the master, ADR 0036; `SettingsPath`, the folder's
    `flatcheck.json`), `SessionSettings` (the two switches and the chosen lens the window
    remembers per folder, R-119, R-121, ADRs 0078 and 0081) and `SessionMessages` (the explanations
    shown when the input is not usable and the residual map's shape guide), plus the four
    files behind the "run the script from here"
    button of ADR 0035: `SirilInstall` (where `siril-cli.exe` and the `.ssf` scripts are;
    the config parsing is pure, `Locate()` is the only thing that touches the machine),
    `SirilScript` (commands and input folders read out of a `.ssf`), `SirilLogLine` (one
    stdout line, sorted into log / progress / other) and `SirilScriptRun` (the `Process`
    wrapper, the only place a process is started besides `Presentation/ExternalLink`, which
    opens the windows' links: every run gets a
    private copy of Siril's settings with `-i`, in `%TEMP%\FlatCheck\siril-<guid>`, deleted
    after the exit (ADR 0055); plus the `SirilRunner` delegate the tests replace).
    `SplitPlan` is the disk rule of the Split tab (ADR 0054): the frames, whether the count
    divides, each part's copy
    list (every bias, at most `MaxLights` lights, the part's flats), the temp root under
    `%TEMP%\FlatCheck`, and `Delete()`. `FlatFrames()` lists raws
    *and* FITS from `flats/` (via `FitsReader.IsFitsPath`), and `HistEqCandidates()` builds
    the Histogram tab's list in the order and by the exact-path rule of ADR 0033; three
    kinds of row, `HistEqKind`. The only part of the app with real rules, and the part with
    the most tests.
    `ProfileCatalog` is the lens-profile catalog loaded once for the whole window (in the
    background, on first request, retried only after a load that threw; a folder that could
    not be listed is left out by the locator, not a failure) - it was the Sintetic tab's
    until the Structure tab needed the same list; `MainViewModel` builds it and hands it
    to both, and a `Func<string, LoadedFrame>` beside it lets the tests put a fabricated
    raw in place of a file in `flats/`.
  - `ViewModels/` - one per tab over a shared `TabViewModel` (busy / result / error, and
    `EnterGateAsync`, the waiting text around the gate), `HeavyRunGate` (the one gate every
    heavy run takes, ADR 0061; `MainViewModel` owns it) -
    `SyntheticTabViewModel` being the seventh, with `LensRow` (a public row over an internal
    `LensChoice`) and `SyntheticGuideViewModel` (the sources panel, collapsed by default) -
    plus `ScriptChoiceViewModel` (a Siril script chosen from Siril's list: default, blocker,
    warning - decides nothing about flats), `PreprocessViewModel` (that block on the problem
    card, plus its lead text), `SplitTabViewModel` (the eighth tab: chips, script, the
    copy-run-analyse loop with one bar, the table rows `SplitTableRow` / `SplitTableCell`,
    the `SplitMap` list on a shared `MapScaleViewModel`; ADR 0054) and `SirilRunViewModel`
    (one script run as the run window - and the Split tab - sees it: log text, progress,
    current command and its index, outcome), plus
    `MainViewModel` for the folder, `AboutViewModel` for the "Despre" window (constant
    properties only, ADR 0040), and two small companions of the residual map:
    `MapScaleViewModel` (the scale chips, which translate a choice into a
    `PngWriter.MapRange` and decide nothing; on every tab with a residual map - Structure,
    Sintetic, Split) and `MapGuideViewModel` (on Structure; the
    shape guide's text and its collapsed state). `HistogramSourceViewModel` is the third of
    that kind, on the Exposure tab: raw data or the camera's preview, and it decides nothing
    either. `SensorFormatViewModel` is the same pattern on the Sintetic tab: six chips that
    show the format of whatever crop is in the box (`Follow`: the nearest nominal crop
    within `SensorFormats.Tolerance`, lit without raising `Chosen`) and put their nominal
    crop into it when clicked (`Chosen`). The first cut only wrote and unlit on typing, so
    1.523 from the camera table sat in the box with no chip lit and looked like a value from
    nowhere - the user's words. Under the box `CropNote` always says where the crop came
    from and what format it is; `FormatNote` (recomputed on every lens, crop, focal or
    f-stop change, in the background with a generation counter; the selected lens's Adobe
    file is parsed there, one file) says what the chosen profile was calibrated on and
    whether that is the user's sensor - `SyntheticReport.FormatMatch` (ADR 0029).
    `HistEqViewViewModel` is the fourth, on the Histogram tab, and the only one
    whose option list depends on the result (six chips on a CFA frame, `Mozaic` first and
    default, one on mono); it also holds the false-colours checkbox (`Rainbow`,
    `RainbowAvailable` = the chosen option's `IsOneLayer`; ADR 0063). `ImageZoomViewModel` beside it holds the zoom *state* - fit or
    a factor, plus the effective factor the control writes back - so a test can drive it
    and the tab can reset it on a new file (and deliberately not on a chip change).
    `StructureRows` is not a view-model but a builder: the five `MetricRow`s of the
    deviation battery, built in one place (the two tabs that showed it beside Structure
    are gone, ADR 0067 and ADR 0080). They are `public` because WPF binding
    reads public properties by reflection (the consequence for internal core types is
    ADR 0037).
    `TestNote` earns its place the hard way: two of the five rows - the azimuthal
    asymmetry and the tilt - sat in the window showing a number, a verdict and no word
    about what they measured, for a whole version. The two notes that did exist were
    written inline in `StructureTabViewModel`, so nothing counted the missing three.
    `EveryTestRowCarriesItsOwnDescription` now fails if a row loses its note. The console
    has no caller: its explanations are grouped under the scale, and five notes threaded
    between the rows would drown the figures.

    The builder exists because the battery was once composed twice (the Ratio tab of the
    time, ADR 0033, removed by ADR 0067: five hand-written `Grid` blocks with the labels as
    literals), and the second copy kept falling behind the first. Every tab that shows the
    battery renders `StructureRows.Tests(result)` through the shared `MetricRowTemplate`.
    Structure keeps its `ObservableCollection` because `MainViewModel.OpenAsync` cancels
    the previous run first.

    `StructureRows.Informative` does the same for the three no-verdict rows (vignetting,
    illumination centre, local spots). The one line deliberately left out is
    `median / max ADU`: it belongs to the structure mode's header, not to `WriteBody`,
    because on a raster normalised to a median of 1 it would mean nothing.
  - `Views/SirilRunWindow.xaml` - the live log of a script run, modal over the main
    window; starts the run on `Loaded`, confirms before closing mid-run (closing kills
    Siril), scrolls the log to the end.
  - `Views/` - the XAML. `BusyOverlay`, `MessagePanel` and `ReportPanel` bind to the
    inherited `TabViewModel`, so the same three controls serve every tab; `OpeningPanel`
    binds to `MainViewModel` and stands in for the tabs while the opening runs, one row per
    `ViewModels/OpeningStep` (R-120, ADR 0079, `two-applications.md`); `SplitView` is the
    one without the overlay (its running card has the bar, the log and the stop button) and
    the one whose table has a variable number of columns (an `ItemsControl` of rows, each a
    horizontal `ItemsControl` of fixed-width cells, the header built the same way); `SyntheticView`
    is the one with text boxes (`Style x:Key="Field"` in `Dark.xaml`, keyed like `LogBox`
    because a TextBox is not an ordinary control of this app);
    `MapGuidePanel` binds to `MapGuide.*` and serves the two tabs that have a map guide.
    Headings that count something, radio `GroupName`s and the smoke renders are ADR 0037.
    `HistEqView` is the one tab whose `BusyOverlay` sits inside the right-hand
    column rather than over the whole tab - the list stays clickable while a frame is
    computing, and a new click supersedes the run (see "What the window adds"). Its
    picture sits in `ZoomableImage`, a `UserControl` that owns the pointer interaction
    (wheel about the cursor with no modifier - the user asked for Siril's behaviour after
    a Ctrl+wheel first cut, so the page scrolls with the wheel only outside the picture;
    drag to pan, with a hand cursor only when there is something to pan; double-click
    fit/100 %; keys) and computes the effective factor - "fit" needs the viewport size,
    which only the control knows.
    `LayoutTransform`, not `RenderTransform`, so the scrollable extent follows the zoom;
    `NearestNeighbor` at factor >= 1 so magnified pixels are crisp squares; "fit" is
    capped at 1.0, so a picture smaller than the viewport is shown at its size rather
    than blown up (found on the 128 px test fixtures, where fit came out at 4x).
  - `Views/AboutWindow.xaml` - the "Despre" window (ADR 0040: what it shows and how it is
    opened; ADR 0037: the hyperlink handler and why the smoke test renders its content, not
    the window). Modal, centred on its owner, `IsCancel` on the close button. No taller than
    the work area of its screen, which the main window passes to its constructor: the cards
    in a `ScrollViewer` named `Cards`, the Close button in a row of its own, the fit and its
    log line at `ContentRendered`, the keys passed to the cards only while they scroll (R-123,
    ADR 0083).
  - `Presentation/AppIcon.cs` - the largest frame of `flatcheck.ico`, chosen through
    `BitmapDecoder`: an `<Image>` handed the `.ico` directly takes whichever frame comes
    first, which at 72 px on screen can be the 16 px one scaled up.
  - `Presentation/ExternalLink.cs` - a clicked link's address opened in the browser
    (`UseShellExecute`; a computer without a browser only logs it). The "Despre" window and
    the main window catch `Hyperlink.RequestNavigate` on their root and call it (ADR 0037);
    the main window's catches the Structure tab's link to Adobe's DNG Converter (ADR 0073).
  - `Presentation/RasterBitmaps.cs` - a byte raster straight into a frozen `BitmapSource`
    (`Gray8` / `Rgb24`), no PNG. The maps still go through `PngWriter` because the PNG *is*
    the file the console writes and the window shows those exact bytes; the equalised
    views have no file on the window side, and at sensor resolution a PNG of equalised
    noise would be 15-20 MB and seconds per view.
  - `Presentation/` - `VerdictPalette` (colour derived from `Stats.Severity`, never from
    its own list of verdict names), `ChannelPalette` (R/G/B hues for the exposure
    histograms - deliberately **not** routed through `Stats.Severity`: a red channel is
    not a worse channel; the neutral accent on an unknown order is ADR 0023),
    `HistogramCanvas` (the hand-drawn chart) and the value converters.
  - `Presentation/AccessibleTabControl.cs` - the window's tabs (`Tabs` in `MainWindow.xaml`),
    whose automation peer puts the selected tab's view under the tab's item (R-114, ADR 0074):
    each tab's peer, a subclass of the stock one, adds the children of the template's presenter
    `ContentHost` while its tab is selected, which the stock peer does only through the name
    `PART_SelectedContentHost`, a name that would also move the keyboard focus into a tab on a
    click on its header. The window's own tab style, with the trigger that shows the tabs, is
    `BasedOn` the theme's, keyed by this class.
  - `Theme/Dark.xaml` - the palette, the control styles, the converters and the row
    templates; why the converters and the `ToolTip` style live there is ADR 0037. The tabs'
    style targets `AccessibleTabControl` and names its presenter `ContentHost` (ADR 0074).
  - `Interop/DarkTitleBar.cs` - a P/Invoke: Windows draws the title bar, not WPF.
  - `Interop/ScreenArea.cs` - the other: the work area of the screen a window is on, which WPF
    knows for the primary screen only, and `TopInside`, the arithmetic that keeps "Despre"
    inside it (R-123, ADR 0083).
  - `Loggers.cs` - the names of the window's loggers in the diagnostic log (`logging.md`);
    each tab logs under `Window.<Tab>`, given by `TabViewModel`.
- `FlatCheck.Tests/` - xUnit tests for the core and the console, reaching them via
  `InternalsVisibleTo`.
- `FlatCheck.Wpf.Tests/` - xUnit tests for the window app, `net8.0-windows`. Separate
  project rather than a folder, so `FlatCheck.Tests` - which carries the parity story -
  stays portable. Borrows `FitsTestData`, `TempWorkspace` and `TestImages` from it by
  `<Compile Link>` rather than copying them - the last one for `JpegOf`, which encodes a
  camera preview in the test so the Exposure tab can be exercised with the source switch
  on without a real raw anywhere in the suite.
- `flatcheck.py` and `crosscheck.py` - frozen, ADR 0047; see `python-parity.md`.
- `Icon/` - `flatcheck.ico`, the one icon of both applications: `<ApplicationIcon>` in both
  csproj files (the exe, hence Explorer and a pinned taskbar entry) and, for the WPF app
  only, a linked `<Resource>` plus `Icon=` on `MainWindow` (title bar and the running
  window's taskbar button - without it WPF takes the icon from the process exe, which
  under `dotnet FlatCheck.Wpf.dll` or a test host is `dotnet.exe`). The drawing is the
  core idea: radial vignetting rings and a reticle on the sensor centre. It is generated,
  not drawn by hand, by `make_icon.py` (Pillow, a dev-time tool only - nothing in the
  applications links it), which draws **each size separately**: at 16-24 px the rings
  drop from five to three and the reticle thickens, because a downscaled 256 px drawing
  loses the reticle below a pixel. The `.ico` is committed; rerun the script only when
  the drawing changes. `IconTests` pins that both projects point at this one file and
  that the window's icon loads with all eight sizes; `LicenseHeaderTests` also scans
  `Icon/*.py`.
- `lensfun/` - the lensfun database, committed whole and shipped beside the exe: ADR 0028.
- `README.md` - the user documentation and its single-source rule: `process.md`,
  "Conventions".
- `LICENSE` - the verbatim GNU GPL v3 text (ADR 0040).
- `docs/` - what each folder holds is CLAUDE.md ("Layout"); the rule that a plan is kept
  and a design note is updated or deleted with its code is ADR 0001; the index of the
  notes is `README.md` in this folder.


## Result records (Analysis/)

> Added 2026-09-25 (docs-health audit, gap 1): the member names and shapes of every result,
> read from the code, so a console or window change that emits a number does not need the
> source open. Units are stated once per table. "Votes" means the member feeds a verdict word;
> "informative" means it is printed or shown and nothing decides on it. The verdict words come
> from `Stats.cs`, the thresholds from `Th` (values in `thresholds.md`, never repeated here).

**Two verdict scales, one severity.** The *residual scale* is the four words of
`Stats.ResidualBands`, `PERFECT` / `BUN` / `ACCEPTABIL` / `REFA`, given by
`Stats.ResidualVerdict(pct)` on `Th.ResidPerfect` / `Th.ResidUsable` / `Th.ResidCheck`; every
structure test, the depth verdicts and `--against` speak it. The *light scale* is `OK` /
`SUSPECT` / `PROBLEMA`, given by `Stats.Verdict(val, ok, bad)`; `--light` uses it on
`Th.LightRadOk` / `Th.LightRadBad`.
`NEDETERMINAT` is the series mode's "ran, cannot conclude". `Stats.Severity(word)` maps all of
them onto 0..3 so a "worst of" can be taken across the two scales; an unknown word counts as
the worst. A depth in stops becomes a residual-scale percentage through
`Stats.DepthErrorPct(stops) = (2^|stops| - 1) * 100`, and `Stats.DepthVerdict(stops)` is
`ResidualVerdict` of that.

**"Pixels of the analysis raster"** below means the raster after `Img.Prepare` (2x2 CFA mean,
then the downsample), the one every structure figure is measured on; `Width` / `Height` of the
result say its size. Sensor pixels are named as such where they appear.

### `StructureResult` (`StructureAnalyzer.cs`)

| Member | Unit | Meaning | Role | `validation.json` key (kind `structure`) |
| --- | --- | --- | --- | --- |
| `Width`, `Height` | pixels of the analysis raster | size of the raster the figures are measured on | informative | |
| `RawMedian`, `RawMax` | levels of the input file (ADU on a raw stack, float on a normalised master) | median and maximum of the original raster; on `AnalyzePrepared` they come from the prepared, normalised raster, mean nothing and are not printed | informative | |
| `FitCx`, `FitCy` | pixels of the analysis raster | fitted illumination peak (`Metrics.FitCenter`) | informative | |
| `SensorCx`, `SensorCy` | pixels of the analysis raster | sensor centre, `w/2`, `h/2`; the radial model and the asymmetry are taken around this point, never the peak | informative | |
| `CentroidOffsetPct` | percent of the half-diagonal | distance of the fitted peak from the sensor centre | informative, never a verdict (not scale-invariant) | |
| `CentroidValid` | bool | false = the fitted peak is not usable and the offset prints as "nedeterminat" | informative | |
| `TiltPct` | percent | amplitude of the linear term of the fit, the gradient across the frame | votes: `TiltVerdict` | `tiltPct` |
| `Corners` | levels of the analysis raster, dictionary keyed `stanga-sus`, `dreapta-sus`, `stanga-jos`, `dreapta-jos` | median of each corner box (`Metrics.CornerStats`) | informative; feeds the three below | |
| `CornerMean`, `CentreMedian` | levels of the analysis raster | mean of the four corner medians, and the centre box median | informative; feed `VignetteStops` | |
| `VignetteStops` | stops, `log2(CentreMedian / CornerMean)` | the master's vignetting depth, centre to corner | informative, **not a test**; it is what the sixth test compares against the lens profile | `vignetteStops` |
| `CornerSpreadPct` | percent of `CornerMean` | `(max - min)` of the four corner medians | votes: `CornerVerdict` | `cornerSpreadPct` |
| `AzimuthalMaxPct` | percent | worst ring's peak-to-peak over its 12 sectors, inside the inscribed circle (`Metrics.AzimuthalAsymmetry`); `NaN` when no ring could be measured | votes: `AzimuthalVerdict` | `azimuthalMaxPct` |
| `ResidualPtpPct` | percent | large-scale peak-to-peak of `Residual` over the whole frame (`Metrics.LargeScalePtp`, tiles trimmed 5 % at each end) | votes: `ResidualVerdict`; the most important of the five | `residualPtpPct` |
| `MaxTilePct` | percent | the most deviating tile of `Residual` (`Metrics.MaxTileDeviation`); catches the compact defect the trimmed ptp lets through | votes: `MaxTileVerdict` | `maxTilePct` |
| `LocalSpotPct` | percent, **signed** (minus = darker than its surroundings) | deepest compact spot in `Residual` after the large-scale structure is removed (`Metrics.LocalSpots`) | does not vote; `LocalSpotFlagged` when the magnitude reaches `Th.LocalSpotWarn` gives a warning line | `localSpotPct` |
| `LocalSpotX`, `LocalSpotY` | pixels of the analysis raster | where that spot is | informative | |
| `LocalSpotFlagged` | bool | `!(abs(LocalSpotPct) < Th.LocalSpotWarn)`, so `NaN` lands on the warned side | drives the warning, not the verdict | |
| `Residual` | `Img`, analysis raster, dimensionless (`img / radial model`, 1 = on the model) | the residual map's source; the model is fitted on the 180-degree symmetrised raster, the residual is of the unsymmetrised one | the map | |
| `CornerVerdict`, `AzimuthalVerdict`, `ResidualVerdict`, `TiltVerdict`, `MaxTileVerdict` | residual-scale word | one per test, `Stats.ResidualVerdict` of the percentage beside it | the five votes | |
| `Verdict` | residual-scale word | the worst of the five by `Stats.Severity` | **the mode's verdict** (without a `StructureQuery`) | `verdict` |

`StructureReport.TestKeys` names the five voting members by these exact member names
(`CornerSpreadPct`, `AzimuthalMaxPct`, `ResidualPtpPct`, `TiltPct`, `MaxTilePct`), and
`StructureReport.Test(result, key)` returns the value and its verdict for one key; that is how
the against and split reports print the battery without renaming a test.

### `StructureQuery`, `StructureOutcome` (`Runs/StructureRun.cs`)

`StructureQuery` is what the caller adds to ask for the sixth test; `null` means the five tests
alone, byte-identical output to before the sixth existed.

| Member | Unit | Meaning |
| --- | --- | --- |
| `Sources` | `ProfileSources` | the Adobe and lensfun catalog the comparison reads |
| `RawPath` | path or null | a raw beside the master, for the aperture and the lens ID |
| `LoadInfo` | `Func<string, LoadedFrame>`, default `ImageLoader.LoadInfo` | the metadata reader; tests hand in a fabricated frame instead of a raw |
| `LensText` | string or null | the `--lens` text; null = the lenses the raw points at |
| `Source` | `ProfileSource?` | keep only Adobe or only lensfun candidates; null = both |
| `Focal`, `FStop`, `Crop` | mm, f-number, crop factor; each nullable | overrides for what the raw or the header would have said |

| Member | Unit | Meaning | `validation.json` key (kind `profile`) |
| --- | --- | --- | --- |
| `Structure` | `StructureResult` | the five tests, exactly as without a query | `masterStops` reads `Structure.VignetteStops`; `shapeVerdict` reads `Structure.Verdict` |
| `Profiles` | `VignettingComparison` or null | null without a query; with one, **always present**, even when nothing could be compared (`Gap` says why) | `diffStops` reads `Profiles.Nearest.DiffStops`; `nearest` is `Nearest.Profile.Lens.Name` followed by `Lens.Where` in square brackets; `depthVerdict` reads `Profiles.Verdict` |
| `Shot` | `ShotIdentity` | what was identified from the master, the raw and the overrides (focal, f-stop, crop, camera, candidate lenses) | |
| `RawError` | string or null | the raw beside the master could not be read: the message | |
| `Verdict` | residual-scale word | the worse (by `Stats.Severity`) of `Structure.Verdict` and `Profiles.Verdict`, the latter only when `Compared`; otherwise `Structure.Verdict` | `verdict` |
| `Compared` | bool | `Profiles != null && Profiles.Compared` | |
| `DepthCarriesTheVerdict` | bool | `Compared` and the depth verdict is strictly worse than the shape's: the advice is then the depth's, not the shape's | |

### `ProfileDepth`, `VignettingComparison`, `ComparisonGap` (`Analysis/VignettingComparison.cs`)

One `ProfileDepth` per candidate lens, its nearest profile in focal and f-stop put against the
master. The model's corner depth is measured with the master's own operator: the profile's
raster is built at the analysis size (`SyntheticFlatBuilder.Build`) and `Metrics.CornerStats`
is run on it, so the two sides share the half-pixel artefact and the 5 % box.

| `ProfileDepth` member | Unit | Meaning |
| --- | --- | --- |
| `Profile` | `LensProfile` | the profile used (lens, settings, model) |
| `LensProfiles` | list of `LensProfile` | every profile of that lens, for the "used profile is far from what the raw said" warning; defaults to the one used |
| `CornerStops` | stops | the model's corner depth, `log2(centre / mean of corners)` on its raster |
| `DiffStops` | stops, signed: master minus model | **plus** = the master falls more toward the corner than the model (source falls off with angle: panel without a real diffuser, too close); **minus** = less (source fills the corners: sky dome, ambient, a reflection) |
| `ErrorPct` | percent, `Stats.DepthErrorPct(DiffStops)` | the error left in the corner of a light calibrated with this master, if the model is the truth |
| `Verdict` | residual-scale word, `Stats.DepthVerdict(DiffStops)` | the depth verdict of this one profile |
| `CameraCrop` | crop factor; 0 = the profile's own calibration sensor | the crop the model raster was built with |
| `Extrapolation` | ratio; over 1 = the master's corner lies outside the profile's calibration corner, over `SyntheticFlatResult.SmallExtrapolation` (1.1) = a large one | how far the profile was extrapolated to reach this corner |

| `VignettingComparison` member | Unit | Meaning |
| --- | --- | --- |
| `MasterStops` | stops | `StructureResult.VignetteStops`, copied |
| `Shot` | `ShotIdentity` | as in the outcome |
| `Profiles` | list of `ProfileDepth`, catalog order; empty when not compared | one row per candidate lens |
| `Nearest` | `ProfileDepth` or null | the row with the smallest magnitude of `DiffStops`; a `NaN` row never wins; null when not compared |
| `Verdict` | residual-scale word or null | `Nearest.Verdict`: **the verdict of the nearest profile, not the worst**, because the two profile bases differ by about 0.2 stops between themselves; null when not compared |
| `SpreadStops` | stops, `max - min` of `CornerStops` across the rows | how far the candidates disagree in the corner; 0 with one row, `NaN` with none |
| `Gap` | `ComparisonGap` | why nothing was compared; `None` when it was |
| `SkippedForCrop` | list of `LensChoice` | lensfun lenses left out because the camera's crop is unknown; usually empty |
| `Compared` | bool | `Verdict != null` |

| `ComparisonGap` | Meaning |
| --- | --- |
| `None` | compared |
| `NoProfiles` | no profile on the machine: neither Adobe nor `lensfun/`; or the catalog was never given |
| `NoRaw` | no raw beside the master (and no `--lens`), so no lens ID |
| `NoLensMatch` | the raw carries a lens ID, but no catalog lens matches it (or none of the matches had a usable profile) |
| `NoFocal` | the focal length is unknown: no `FOCALLEN`, no raw, no `--focal` |
| `NoFStop` | the aperture is unknown: Siril does not write it, and there was no raw and no `--fstop` |
| `NoCrop` | only lensfun profiles, and the camera's crop factor is unknown |

The gap order is the order of `VignettingComparer.Analyze`: an empty catalog is reported before
a missing raw, because it is the deeper cause; an unknown catalog (`null`) is not a cause, so
without a raw the answer is "no raw", which is what a person can fix.

### The other analyzers' results, briefly

**`SuitabilityResult`** (`LightSuitability.cs`, before any pixel of `--light` is read).

| Member | Unit | Meaning |
| --- | --- | --- |
| `Level` | `FovLevel`: `Ok`, `Marginal` (runs with a warning), `TooWide` (refuses: above `Th.LightFovMax`), `Unknown` (runs with a note) | whether the plane/radial separation is valid for this field of view |
| `FovDeg` | degrees, diagonal; `NaN` = not computed | the field of view the decision was taken on |
| `FocalMm` | mm; `NaN` unless the decision was taken on the focal length | |
| `Source` | string | where the figure came from (`--fov`, header cards, ...), for the report |
| `CanRun` | bool | `Level != TooWide` |

**`LightResult`** (`LightAnalyzer.cs`, one light frame).

| Member | Unit | Meaning | Role |
| --- | --- | --- | --- |
| `A`, `B`, `C`, `D`, `E` | levels of the analysis raster | coefficients of the plane + radial fit of the tile background | informative |
| `RadPct` | percent, **signed**: `-(D + E) / A * 100` | the radial part of the background; **plus** = the background falls toward the edges = under-correction (the flat misses vignetting); **minus** = it rises = over-correction (offset left in the flat) | votes on its magnitude: `OK` / `SUSPECT` / `PROBLEMA` on `Th.LightRadOk` / `Th.LightRadBad` |
| `PlanePct` | percent | the sky gradient across the frame, to ignore | informative |
| `KeptTiles`, `TotalTiles` | count | tiles kept by the sigma clip, and all tiles | informative |
| `TileRows`, `TileCols` | count | the tile grid | informative |
| `RadialMap` | `Img`, `1 + (D * R2 + E * R2^2) / A` | the map's source | the map |
| `Verdict` | light-scale word | | the vote |

**`LightSeriesResult`** (`LightSeriesAnalyzer.cs`, `[LightResult] -> series`).

| Member | Unit | Meaning | Role |
| --- | --- | --- | --- |
| `Names`, `Frames` | file names and the per-frame `LightResult`s | | informative |
| `MeanRad`, `MinRad`, `MaxRad` | percent | of `RadPct` over the frames | `MeanRad` votes |
| `StdDev` | percent, sample (ddof = 1) | spread of `RadPct` | feeds `Reasons` |
| `Sem` | percent | standard error of the mean | feeds `Reasons` (`Th.LightSeriesSemMax`) |
| `SkyCorr` | correlation coefficient, -1..1; `NaN` when it cannot be computed | correlation of `RadPct` with `PlanePct` across the frames | feeds `Reasons` when `CorrApplies` |
| `SkySpan` | percent points | range of `PlanePct` over the frames | decides `CorrApplies` |
| `CorrApplies` | bool | the sky gradient varies enough for the correlation to mean something | |
| `RadialMap` | `Img` | mean radial map over the frames | the map |
| `Reasons` | string[]; empty = conclusive | why the series is `NEDETERMINAT` | |
| `Verdict` | `NEDETERMINAT` when `Reasons` is not empty, else light-scale word on the magnitude of `MeanRad` | | the vote |
| `Count`, `Conclusive` | count; `Reasons.Length == 0` | | |

**`ExposureResult`** (`ExposureAnalyzer.cs`, mode `--expo`). Every percentage is of `Range`.

| Member | Unit | Meaning | Role |
| --- | --- | --- | --- |
| `White`, `Black`, `Range` | ADU; `Range = White - Black` | the scale the percentages are on | |
| `ScaleSource` | string | `--sat=...`, the raw's metadata, or "auto-detected from max=..." | informative |
| `ChannelMedians` | ADU, 4 Bayer positions | | informative |
| `CfaSpread` | ratio, max / min over the four | | decides `LooksDebayered` |
| `IsCfa` | bool | | |
| `BrightestChannel` | index 0..3 | | |
| `CentreMedianPct` | percent of `Range` | median of the central 20 % window, brightest channel | **this decides the exposure** |
| `GlobalMedianPct` | percent of `Range` | median of the whole frame | informative |
| `CentreClipPct` | percent of the centre pixels | already saturated | feeds the verdict |
| `LevelTooLow` | bool | the level says calibrated / normalised, not raw | gates the verdict (`NotRaw`) |
| `LooksDebayered` | bool | `CfaSpread` too small for a mosaic | gates the verdict (`NotRaw`) |
| `IsSky` | bool | the sky-flat ladder was used (no low shoulder) | |
| `Verdict` | `ExposureVerdict` enum: `NotRaw`, `TooLow`, `AcceptableLow` (panel only), `Ok`, `AcceptableHigh`, `TooHigh` | | the vote |
| `Cfa` | `CfaPattern` or null | the colour order read from the file; null = the file did not say | |
| `Channels` | `ChannelLevels[]`, 4 on CFA, 1 on mono, never null | per channel: `Position`, `Label`, `Colour`, `MedianPct`, `P99Pct`, `ClipPct`, `CentreMedianPct`, `CentreP1Pct`, `CentreP99Pct` (all percent of `Range`), `IsBrightest`, and `Whole` / `Centre` `LevelHistogram`s (200 bins of 0.5 %, `Counts` and `Total`) | informative, the window's histograms |
| `Bands` | `ExposureBands` (`Sky`, `TooLow`, `Lo`, `Hi`, `TooHigh`, `Target`, percent of `Range`) | the ladder that voted, kept in the result so the window draws the band that decided | |
| `Preview` | `PreviewLevels` (`Channels`: exactly R, G, B; `Source`) or null | the camera's own rendering, only when asked for | never votes |
| `IsRaw` | bool | `!(LevelTooLow || LooksDebayered)` | |

**`HistEqResult`** (`HistEqAnalyzer.cs`, mode `--histeq`). No verdict, no vote: a picture.
`RainbowLut`, in the same file, is Siril's false-colour table, applied on demand to a one-layer
raster and never stored in the result (ADR 0063).

| Member | Unit | Meaning |
| --- | --- | --- |
| `IsCfa`, `CfaSpread`, `Cfa` | as in `ExposureResult` | |
| `SourceH`, `SourceW` | sensor pixels | the input frame |
| `Shrink` | factor; 1 = not shrunk | how much each channel was reduced for the console's PNG |
| `OneLayer(full)` | grey raster, width, height | the mosaic on CFA, the channel on mono: what the false colours go on when no channel is chosen, shrunk for the console or whole for the window (ADR 0063) |
| `Channels` | `HistEqChannel[]`, 4 on CFA, 1 on mono | per channel: `Position`, `Label`, `Colour`; `H`, `W` and `Equalised` (shrunk grey raster, bytes); `FullH`, `FullW` and `Full` (full-resolution grey raster the window shows); `Min`, `Max` (input range of the non-zero pixels, file units); `NonZero` (pixel count in the histogram); `LevelsUsed` (of the 256 output levels) |
| `H`, `W` | shrunk pixels | common size of every shrunk view |
| `CompositeRgb` | bytes, `H * W * 3` | RGB on a CFA with known order, triplicated grey otherwise |
| `Mosaic` | bytes, `H * W`, or null on mono | the equalised mosaic as one layer |
| `FullH`, `FullW`, `CompositeFullRgb`, `MosaicFull` | full-resolution counterparts (`MosaicFull` is `SourceH * SourceW`) | what the window shows at 100 % |
| `ChannelCount` | count | |

**`SyntheticFlatResult`** (`SyntheticFlatBuilder.cs`, mode `--synthetic`). No verdict.

| Member | Unit | Meaning |
| --- | --- | --- |
| `Flat` | `Img`, the reference frame's full size | the product that `SyntheticRun.WriteFits` writes |
| `Profile` | `LensProfile` | the profile used (nearest in focal and f-stop) |
| `LensProfiles` | list of `LensProfile` or null | every profile of the lens, for the mismatch warning |
| `CameraCrop` | crop factor; 0 = the profile's calibration sensor | |
| `Width`, `Height` | pixels of the reference frame | |
| `CornerStops`, `LongEdgeStops`, `ShortEdgeStops`, `HalfDiagonalStops` | stops, plus = darker than the axis | the model's depth at the four reference points |
| `CornerRadius` | model units (over 1 on lensfun = extrapolation) | the corner's radius in the model's own coordinate |
| `Extrapolation` | ratio, `CornerRadius / calibration corner radius`; over 1 = extrapolating, over `SmallExtrapolation` (1.1) = a large one | |

**`AgainstResult`** (`Runs/AgainstRun.cs`, `--against`: two flats, prepared and each
normalised to median 1, then their ratio through the structure battery).

| Member | Unit | Meaning | Role | `validation.json` key (kind `against`) |
| --- | --- | --- | --- | --- |
| `A`, `B` | `StructureResult` of each flat (prepared, normalised: `RawMedian` / `RawMax` are not ADU) | each side's own battery and depth | informative | `masterStops` reads `A.VignetteStops`, `syntheticStops` reads `B.VignetteStops` |
| `Structure` | `StructureResult` of `A / B` | the five tests **on the difference**: is it radial or does it have a shape | `Structure.Verdict` votes (the shape) | `ratioResidualPtpPct` reads `Structure.ResidualPtpPct`; `shapeVerdict` reads `Structure.Verdict` |
| `Ratio` | `Img`, analysis raster | the ratio itself | the map is `Structure.Residual` | |
| `Width`, `Height` | pixels of the original A | | | |
| `DepthDiffStops` | stops, signed, `= Structure.VignetteStops`; plus = A falls more toward the corner than B | the depth difference | | `depthDiffStops` |
| `DepthErrorPct` | percent, `Stats.DepthErrorPct` | the corner error of a light calibrated with the wrong one | | `depthErrorPct` |
| `DepthVerdict` | residual-scale word, `Stats.DepthVerdict` | | votes (the depth) | `depthVerdict` |
| `Verdict` | residual-scale word | the worse of `DepthVerdict` and `Structure.Verdict` | **the mode's verdict** | `verdict` |

The `profile` key of the `against` kind is not a member: the test builds it from the synthetic
run's `Result.Profile.Lens.Name` and `Profile.Settings`.

## Run outcomes (Runs/)

> Added 2026-09-25 with the section above. What each `XxxRun.Execute` returns, and the one rule
> they all share about the text.

**The text is always written; there is no way to ask for silence except a null writer.** Every
`Execute` in this folder takes a `TextWriter report` and writes the mode's whole report into it
before returning: `StructureRun.Execute` calls `StructureReport.Write(outcome, path, report)`
unconditionally, and the same holds for `ExposureRun`, `LightRun`, `LightSeriesRun`,
`HistEqRun`, `SyntheticRun` and `AgainstRun`. The runs that can refuse (`LightRun`,
`LightSeriesRun`, `SyntheticRun`) write the refusal text into the same writer. No
flag suppresses it, and nothing returns the text as a string (why the writer is the caller's is
ADR 0011). The window saves what its `StringWriter` got back as "Test result.txt", the same
text by construction; and **a caller who wants the figures and no text passes
`TextWriter.Null`**, which is exactly what `ValidationTests` does for the structure, profile
and against cases. Verified against the code on 2026-09-25: this is true today for all
nine runs (eight since `SmoothRun` left, ADR 0080); `SplitRun`, added on 2026-09-28, follows the same rule (`SplitReport.Write`,
unconditionally).

**`RunOutcome<TResult>`** (`RunOutcome.cs`), for the modes that always answer.

| Member | Meaning |
| --- | --- |
| `Result` | the analyzer's record, never null |
| `Png` | the residual map, already encoded; null when the mode has no map (`--expo`) |

`RunOutcome` carries one map since the `--ratio` mode left (ADR 0067); the mode with two
maps, `--smooth`, had an outcome type of its own and left too (ADR 0080).

| Run and signature | Returns | `Png` |
| --- | --- | --- |
| `StructureRun.Execute(path, report, query = null)`; `StructureRun.Compare(result, reference, query)` for the second half alone | `RunOutcome<StructureOutcome>`; `StructureOutcome` | `Structure.Residual` map |
| `ExposureRun.Execute(path, sat?, black?, sky, report, withPreview = false)` | `RunOutcome<ExposureResult>` | null |
| `HistEqRun.Execute(path, report)`; `EncodeChannel(result, i)` and `EncodeMosaic(result)` on demand | `RunOutcome<HistEqResult>` | `CompositeRgb` as PNG |
| `AgainstRun.Execute(pathA, pathB, report)` | `RunOutcome<AgainstResult>` | `Structure.Residual` map (the ratio's residual) |

The runs that can refuse have their own outcome type, each with `Refused => Result == null`
(or `Advice == null`):

| Outcome | Members | Meaning |
| --- | --- | --- |
| `LightRunOutcome` from `LightRun.Execute(path, fov?, report)` | `Suitability` (`SuitabilityResult`, always set), `Result` (`LightResult` or null), `Png` (`RadialMap`, or null), `Refused` | refused when the field of view is `TooWide`; the refusal text is `LightReport.WriteTooWide` |
| `LightSeriesRunOutcome` from `LightSeriesRun.Execute(dir, pattern, fov?, report, onFrame = null, ct = default)` | `Suitability`, `Result` (`LightSeriesResult` or null), `Png` (mean `RadialMap`), `Files` (the series, always set), `Refused` | refused on the first frame's field of view; an empty series throws instead of refusing; `onFrame(i, n, name, result)` fires per frame as the table streams |
| `SyntheticRunOutcome` from `SyntheticRun.Execute(referencePath, request, sources, focal?, fstop?, crop?, report)` (or the overload on a loaded frame) | `Result` (`SyntheticFlatResult` or null), `Request` (`SyntheticRequest`: `LensText`, `Source`, `Lens`, `FocalMm` + `FocalSource`, `FStop` + `FStopSource`, `CameraCrop` + `CropSource`, each value with where it came from), `Frame`, `Refusal`, `RefusedChoice` (true when the refusal was the lens choice: none given, no match, ambiguous), `Refused` | refused on a colour cube, on an unresolvable lens, or on a missing focal, f-stop or (lensfun only) crop; the file is written by the caller through `SyntheticRun.WriteFits(outcome, referencePath, Stream)`, always -32 |
