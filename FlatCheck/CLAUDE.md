# FlatCheck

Quantitative diagnostic for astrophotography flat frames. Answers four questions:
is the master flat radially symmetric, is the flat *setup* clean, does the flat
actually correct a real light, and is a raw flat exposed correctly. A fifth mode answers
nothing and shows: the frame through histogram equalisation, Siril's "Histogram" display
mode, where every grey level holds the same number of pixels and any non-uniformity is
at full contrast. See "5. Histogram equalisation" under "The six modes". A sixth mode
*produces* something: a second master flat with everything smooth kept and everything
else - diffuser texture, dust, blotches - removed. See "6. Smoothed flat". A seventh
*produces* one too, from nothing: a radial flat written from the lens's published
vignetting profile (Adobe LCP read from the machine, or the lensfun database shipped beside
the exe), plus `--against`, which reads how much and in what shape two flats differ. See
"7. Synthetic flat".

Two applications - a console tool and a WPF window - over one core library. The core
hand-parses FITS and hand-writes PNG (via `ZLibStream` from the BCL), and reaches for a
package where hand-rolling would be dishonest work: libraw (`Sdcb.LibRaw`) for camera raw
files, ImageSharp for the JPEG preview the camera embeds in them. .NET 8. See
"The dependencies" below before adding another, and "The two applications" for what each
one owns.

## Structure

Four projects. The split happened in 2.0.0, when a second application appeared over the
same numbers - see "The two applications" below for why it was a library extraction and
not a copy.

- `Directory.Build.props` - `<Version>` and `<Copyright>`, shared by every project. The
  **single** place to edit either on the C# side. See "Versioning".
- `FlatCheck.Core/` - everything that computes and everything that formats. Knows nothing
  about consoles or windows, and has no entry point, so it *cannot* reach `Console` even
  by accident. Before the split, "Analysis/ and Report/ are pure" was a convention held by
  goodwill; now it is a compile boundary.
  - `FitsReader.cs` - the hand-rolled FITS reader. `Load(byte[])` is the real
    entry point; `Load(string)` is a thin file wrapper, which is what lets the
    reader be tested without touching disk. `HInt`/`HDbl`/`HStr` read single cards out of
    a header dictionary with a default, so a missing card is never an error - which is
    what lets `--light` and `--ratio` degrade instead of throwing on a raw, which has no
    FITS header at all. `LoadHeader(path)` reads the header blocks alone, for the window's
    Sintetic tab, which wants `FOCALLEN` and `INSTRUME` out of a 96 MB master at open.
  - `FitsWriter.cs` - the hand-rolled FITS writer, the reader's pair, with one consumer:
    `--smooth`. One primary 2-D HDU, `BITPIX -32` or `16` (with `BZERO 32768`), the
    input's raw cards copied minus the structural ones it rewrites, `HISTORY` at the end.
    Writes into a `Stream` for the same reason the report is a `TextWriter` - 96 MB of
    float need not sit in memory twice; `Encode` over a `MemoryStream` is for tests. It
    copies the **raw 80-character cards** (`LoadedFrame.Cards`, kept by `FitsReader.LoadFull`
    alongside the dictionary), not the parsed dictionary, which loses comments and
    truncates strings at `/` (known issue 1). `LoadedFrame` also carries `Planes`
    (`NAXIS3`) and `Bitpix`, both so `SmoothRun` can refuse a cube and write the input's
    type back.
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
    `FNumber`, `Camera`, `NikonLensId` - null where the format is silent (a Siril FITS has
    no f-number; a D3300 raw with third-party glass has no lens name but has the Nikon
    lens ID). Nothing below it knows where the
    pixels came from, and that is what keeps the two formats from growing separate
    behaviour. Mirrors `load_image_with_header` in `flatcheck.py`. `LoadInfo(path)` is the
    same dispatch **without pixels**: `Image` null, the shot fields filled - on a FITS from
    the header alone, on a raw from libraw's metadata at `OpenFile`, before `Unpack`
    (measured: tens of milliseconds, not seconds). It is what the lens identification and
    the Sintetic tab's prefill read, so neither decodes 24 MP for an f-number.
  - `RawReader.cs` - the only file that touches a third-party library. Wraps libraw to
    get the undebayered Bayer mosaic, the exact white/black levels, and the CFA colour
    order out of a camera raw. `IsRawPath` is the extension gate; `RawExtensions` exposes
    the same list to the WPF app's `flats/` filter, so a camera the reader learns is a
    camera the list offers. `ShootingInfo` reads focal length, f-number, make/model and
    the Nikon lens ID out of `ImageOtherParams` / `IParams` / `LensInfo` - locals only, for
    the lazy-loading reason above.
  - `CfaPattern.cs` - which colour each of the four Bayer positions is, or `null` when the
    file does not say. Decoded from libraw's `filters` + `cdesc`, or from a FITS
    `BAYERPAT` (+ `XBAYROFF`/`YBAYROFF`, and `ROWORDER`, which on a bottom-up file of
    even height moves the phase by one row). Never guessed - see "The channel order"
    under mode 4. Used only for *naming*; no figure depends on it.
  - `Mosaic.cs` - the CFA rule shared by `--expo`, `--histeq` and `--smooth`:
    `PositionOf(y, x)`, `CfaSpreadMin` (1.15) with `LooksCfa`, `Split` into the four
    position rasters, `Extract` (one position, for the mode that works them one at a time
    and has no use for four copies), `Merge` (the inverse, back to the *original* size -
    an odd last row or column is filled from the nearest line of the same parity, so a
    CFA keeps its colours and a flat never gets a line of zeros), and `Label`/`ColourOf`
    over a `CfaPattern`. It used to be inline in `ExposureAnalyzer`;
    two copies of `(y & 1) * 2 + (x & 1)` and of `1.15` would have diverged at the first
    adjustment. `ExposureAnalyzer` uses the rule, not `Split` - it fills its histograms in
    the same pass it reads the pixels, and a second copy of the data would cost memory.
  - `PreviewImage.cs` - the camera's own rendering of the frame, in two forms:
    `EmbeddedPreview` (the bytes as they sit in the raw, captured while the file is open)
    and `PreviewImage` (decoded RGB, made only on request). Neither may decide anything -
    see "The camera's preview" under mode 4.
  - `PreviewReader.cs` - the only file that names a `SixLabors.ImageSharp` type, exactly as
    `RawReader.cs` is the only one that names libraw. `Decode(EmbeddedPreview)` returns
    null on any failure, including a missing assembly; it is also the swap point if the
    package ever has to change.
  - `Thresholds.cs` - `Th`. Note it does **not** hold every threshold; see below.
  - `Img.cs` - the 2-D raster plus its pure transforms: `BlockMean`, `Prepare`,
    `Divide`, `Symmetrise` (the mean of a raster and its 180° rotation, which is what the
    radial model is fed - see `RadialModel` under "Shared pipeline"), and for the
    attribution mode `CropCentred` + `Rotate180`. Those last two
    exist because that mode is the only one that compares a raster with its own 180°
    rotation, and `BlockMean` drops partial blocks only at the *end* - so its grid is
    anchored top-left and a rotation lands up to one tile off. Deliberately **not** used
    by `LargeScalePtp`: there the top-left anchoring is what `flatcheck.py` does, and the
    numeric parity is tested. `TileMedians` is the third grid, for `--smooth` only: tile
    *medians*, **partial last row and column kept**, and the real centre of every tile
    returned - a partial 8-px tile fitted one full tile from its neighbour put a 0.8 %
    error band along the edge of the synthetic; with real centres the whole-frame error
    is 0.19 %.
  - `Stats.cs` - numeric primitives: `Percentile`, `Interp`, `Solve`,
    `Verdict`, `Rank`, `Severity`, and the verdict vocabulary (`ResidualBands`,
    `NotApplicable`).
  - `Metrics.cs` - the pure geometry and metrics: `FitCenter`, `RadialModel`,
    `CornerStats`, `AzimuthalAsymmetry`, `LargeScalePtp`, `MaxTileDeviation` (the worst
    tile - the fifth structure test), `LocalSpots` (the deepest compact spot after the
    large-scale structure is subtracted - informative, with a warning), `TileBackground`,
    plus `TileSize` - the tile side the large-scale metrics use, pulled out because
    `LargeScalePtp`, `MaxTileDeviation`, `LocalSpots` and the attribution mode all need
    it and two copies of the formula would have diverged at the first adjustment of
    `ntiles`.
  - `Version.cs` - `VersionInfo.Value`/`Banner`/`Copyright`, read from the assembly. See
    "Versioning".
  - `ThirdParty.cs` - the third-party components as a *value*: name, role, licence,
    copyright notice, URL and the NuGet packages that bring each one in. It is what the
    window's "Despre" shows, and it exists because LGPL-2.1 §6 wants LibRaw's notice
    displayed wherever the program displays its own - see "Licensing". `ThirdPartyTests`
    binds it to the `PackageReference`s in the Core csproj in both directions and to the
    headings of `THIRD-PARTY-NOTICES.md`, so **a new package needs an entry here too**;
    see "The dependencies".
  - `LensProfiles/` - the lens vignetting profiles behind the synthetic flat.
    `VignettingModel` is the factor `V(x, y)` as a value, with the two coordinate
    conventions verified from sources (its header quotes them): Adobe's r in units of the
    focal length, lensfun's `pa` r at the half-diagonal of a 36x24 frame over the
    calibration crop. `LcpReader` (Adobe XMP, both the attribute and the element form,
    with the piecewise tables) and `LensfunReader` (`<lens>` + `<camera>` for crop
    factors) are pure over text; `ProfileLocator` is the only file that looks at the
    machine. `LensProfile` / `LensCatalog` hold the list: **lensfun parsed up front,
    Adobe lazily** - 3531 `.lcp` files and 36 s to parse them all on the author's
    machine, against 2 s for the list from file names (`AdobeProfileFile.FromName`: the
    `<Camera> (<Lens>) - RAW.lcp` convention, with the fixed-lens phones and compacts as
    camera-named stubs). A file is parsed when its lens is chosen, or when a Nikon lens
    ID is looked up, and then only the files of the camera's mount, in parallel.
    `SensorFormats` is the six nominal crops (full frame 1.0 ... 1" 2.7) with the sensor
    sizes as notes - the fallback for a body the lensfun camera table (1053 entries, the
    one list of cameras, maintained upstream) does not have; the refusal text lists them
    and the window offers them as chips.
    `LensIdentification` is the **one** identification of the shot - focal, f-stop, crop,
    camera, each with its source, and the lens candidates - from a reference frame (the
    master, or the console's raw) and a raw beside it, both through `LoadInfo`. It used to
    live half in `SyntheticRun.Complete` and half in the Sintetic tab's prefill, and the
    Structure tab's comparison needed exactly the same rule; two copies would have chosen
    two lenses for one folder. Candidates: the Nikon lens ID narrowed by the MakerNote
    range, the lensfun lenses of the same makers, the camera's mount, `PreferCamera`; and,
    when the ID matches nothing but the raw gives a full range, **the lenses of the mount
    whose name says exactly that range** (`CandidatesByRangeOnly`) - a manual Samyang 135
    f/2 on a D3300 carries the "non-CPU lens" ID 74, which no profile has, and the range
    135 mm f/2.0 from the camera's menu; the report says the list may hold another lens
    of the same range. `FirstRaw` is the "raw beside the master" rule both tabs share.
  - `Analysis/` - one analyzer per mode, each a pure function into an `XxxResult`:
    `StructureAnalyzer`, `RatioAnalyzer`, `LightAnalyzer`, `ExposureAnalyzer`,
    `HistEqAnalyzer` (with `HistEqLut`, the Siril transfer table - see mode 5),
    `SyntheticFlatBuilder` (mode 7: a `VignettingModel` and a frame size in, the raster
    and its depth at four reference points out), `AgainstAnalyzer` (in `Runs/AgainstRun.cs`:
    two rasters prepared, normalised to median 1, their ratio through the structure battery),
    `VignettingComparison` (the sixth structure test: the master's corner depth against
    each candidate lens's nearest profile, measured with `CornerStats` on the model raster
    built at analysis size - see "The sixth test" under mode 1),
    `SmoothAnalyzer` (mode 6; its header comment carries the two rejected designs and the
    measurements - read it before touching the method) and `SmoothScaleAdvisor` (proposes
    the smoothing scale from the master - `Metrics.FineTexture` on the input, then one
    smoothing per rung of the three-chip ladder, with the azimuthal asymmetry and the tilt
    as the "too large" guards; pure, and `Decide` is separately pure over hand-made
    trials). Beside
    them, `ExposureBands` - the verdict ladder as a *value*, because two places need it
    (`Classify`, and the window's histogram) and a second selection among the `Th.Expo*`
    constants would have been a second definition of the verdict. `ExposureAnalyzer` also
    carries `LevelHistogram` and `ChannelLevels`, the per-channel distributions; they are
    filled in the loop the analyzer already ran and change no existing figure. Beside them
    `PreviewLevels` and `AnalyzePreview`, the same two types over the camera's own
    rendering - three channels, no bands, no verdict, and `Analyze` fills
    `ExposureResult.Preview` only when handed one.
    No I/O, no printing - this is where every number is decided. Two of them are not
    `Img -> Result`, deliberately: `LightSuitability` is `header -> SuitabilityResult`
    (it decides whether `--light` applies at all, before any pixel is read), and
    `LightSeriesAnalyzer` is `[LightResult] -> LightSeriesResult` (it combines
    per-frame results; the file loading stays in `Runs/`, like every other mode).
    `RatioAnalyzer` is `Img × Img -> RatioResult` and is the one analyzer that *calls*
    another: the verdict of the attribution mode is `StructureAnalyzer` run on the
    combined master, through `AnalyzePrepared` - the overload that skips `Img.Prepare`
    because the caller has already prepared its input. `Prepare` is only block means,
    hence linear, so the mean of the prepared rasters equals the prepared mean, at a
    thirtieth of the memory.
  - `Report/` - one report per mode, each a pure `XxxResult + TextWriter -> text`
    function. All the Romanian output lives here and nowhere else. `SyntheticReport` also
    carries the `--lenses` list, the refusals, and the two texts the window's sources panel
    shows (`AdobeWhere`, `LensfunWhere`), so console and window say the same about where
    profiles come from. `LightSeriesReport`
    is split into `WriteHeader`/`WriteRow`/`WriteSummary` so a 32-frame run can stream
    its table instead of sitting silent for two minutes. `StructureReport.WriteBody` is
    split off for the same reason in the other direction: the attribution mode prints its
    own header and then wants that exact body - minus the ADU line, which means nothing on
    a raster normalised to a median of 1. `RunReport` holds the two lines
    that frame every mode - the version banner and the "harta salvata" line - because
    both applications write them and two copies would have diverged. It also holds
    `MapScaleLabel` and `MapLegend`, the two bits of wording that go with the map; they
    have no console caller today, and live here anyway so a future `--scala` reuses them
    instead of rewriting them. And `WarrantyLines` + `LicenseUrl`: the GPLv3 §5(d)
    notice, which the console prints at `--help` and the window shows in "Despre" -
    one text, so the two cannot describe the same licence differently.
    Several pieces are exposed as values rather than only written: `StructureReport.Scale()`,
    `StructureReport.Advice()`, `StructureReport.TestNote()` (one sentence per deviation
    test, keyed by the `StructureResult` field name so reordering the rows cannot unpair a
    note from its metric), `StructureReport.Levels()` (the median / max line, formatted
    for a float master as well as an ADU one - both applications print it), the three
    `LocalSpot*` pieces (`LocalSpotValue` with the sign named in words, `LocalSpotNote`
    with the warning threshold read from `Th.LocalSpotWarn`, `LocalSpotWarning` or null -
    all three printed by the console too, see "1. Structure"), `LightReport.Diagnosis()`, `ExposureReport`'s
    `HistogramReading` / `ChannelNote` / `BandLabel` (the wording around the exposure
    histograms) and `ScreenComparison` / `ScreenReading` / `NoPreviewReason` (the raw-vs-screen
    table and its explanation) - all window-only, like `MapScaleLabel`, and for the same
    reason, and from
    `StructureReport` also owns the profile block: `ProfileRows`, `MasterDepth`,
    `ComparisonSummary`, `ComparisonNote`, `DepthScaleRows` / `DepthScaleNote` (the depth
    scale as four bands in stops, `Th.Resid*` through `Stats.StopsForError`, and the sentence
    that it is the residual scale translated, not a new threshold - the user asked whether it
    was a second scale), `GapText` / `GapMeaning` (one sentence per
    `ComparisonGap`), `ShapeDetail` / `DepthDetail` (the two halves of the verdict; the
    console pads them into `VerdictDetail`, the window labels them itself) and
    `DepthAdvice`, which replaces `Advice` when the depth carried the verdict and reads the
    sign as the cause. From
    `RatioReport` the seven that the Ratio tab shows verbatim - `Provenance`, `WorldValue`,
    `Precondition`, `WorldMeaning`, `SensorCaveat`, and the two window-only notes under the
    world-fixed rows, `WorldNote` (what "per orientation" means, and that the coloured
    label does not vote, with its thresholds read from `Th.RatioOk`/`Th.RatioBad`) and
    `WorldCornerNote`. Those two rows were the last on the Ratio screen without a
    description; `EveryRowOnTheScreenCarriesADescription` now covers every row on it. The console formats them into columns, the WPF app puts
    them in cards and under a coloured chip; the wording has one definition either way.
    `WorldValue` and `WorldLine` are split for exactly that reason: the console wants the
    figure with its label glued on, the window shows the label as a separate chip, and
    without the split the verdict appeared twice on one row. `HistEqReport` has no verdict
    to write; what it exposes as values is `Caveat()` (what the picture cannot show),
    `MasterVersusFrame()` and `ViewCaption()` - window-only, like the others. `SmoothReport`
    prints "what was removed" and the structure battery before/after; it reuses the
    structure report's own labels through `StructureReport.TestLabel`/`TestKeys`/`Test`
    (added for it, so the two reports cannot name a test differently) and
    `LocalSpotValue(pct, x, y)`, the overload that formats a spot measured on another
    raster. That overload also rounds to zero *before* formatting: .NET keeps the sign of
    a negative zero, and `-0.00001` printed as `-+0.00`.
  - `Runs/` - the whole chain of one mode: load -> analyze -> report -> encode the map.
    `StructureRun`, `RatioRun`, `ExposureRun`, `LightRun`, `LightSeriesRun`, `HistEqRun`,
    `SmoothRun`, `SyntheticRun`, `AgainstRun`, plus `SeriesFinder` (which files a series
    run covers). `StructureRun.Execute` takes an optional `StructureQuery` (profile
    sources, a raw beside the master, or `--lens` text, plus the overrides) and returns a
    `StructureOutcome`: the `StructureResult`, the `VignettingComparison` (null without a
    query; with one, present even when it could not compare - `Gap` says why) and the
    combined verdict. Without a query the output is byte-identical to before, which is
    what keeps the golden files and `crosscheck.py` still. `Compare` is the second half
    on its own, so the window can explain a missing raw without reloading the master. `SyntheticRun` is the third run that can refuse and the second whose
    product is a file (`WriteFits` on the caller's stream, like `SmoothRun`); its
    `SyntheticRequest` carries every value with the source it came from, so the report
    can print "cerut" beside "profil folosit". `SmoothRun` is the
    second run that can refuse (a raw, a colour cube - `LightRun`'s shape) and the first
    whose product is a file: `WriteFits(outcome, sourcePath, Stream)` is called by the
    console on a `FileStream` and by the window after the overwrite question, never by
    the run itself, for the reason `RunOutcome.cs` gives for the map. `OutputBitpix` is
    "same type in, same type out" (16 stays 16, everything else becomes -32), and nothing
    is renormalised. `HistEqRun` also carries the
    per-view encoders (`EncodeChannel`, `EncodeMosaic`) the window calls on demand; the
    console only ever writes the composite. This is the layer both applications
    call, and the reason neither of them re-derives a number. `ExposureRun` is the only one
    with an opt-in: `withPreview` decodes the camera preview, and defaults to false because
    only the window shows it. Two shape decisions are
    load-bearing and explained at length in `Runs/RunOutcome.cs`: the report is **written
    into a `TextWriter` the caller supplies** (so the console keeps streaming a 32-frame
    table, and anyone who wants the string passes a `StringWriter`), and the residual map
    comes back as **PNG bytes** rather than being written to disk (the window wants to
    show it, not save it). `RunOutcome.SecondaryPng` was added for the attribution mode,
    the only one that produces two maps, and is null everywhere else.
  - `PngWriter.cs` - the hand-rolled PNG encoder and the residual colour map.
    `Encode(rgb, w, h)` is the real entry point; `EncodeResidualMap` is what both
    applications use, and `WriteResidualMap` is a thin file wrapper over it. It prints
    nothing - the "harta salvata" line comes from `RunReport`. `EncodeGrey` writes a grey
    raster as RGB with equal components; the encoder keeps its single colour type.
- `FlatCheck/` - the console app. `AssemblyName` is `flatcheck`, so the binary is
  `flatcheck.exe`, not `FlatCheck.exe`. Three files, and that is the point: what is left
  here is what belongs to a command line and to nothing else.
  - `Program.cs` - a one-line `Main` shim, plus a header that points at `README.md`
    rather than restating it. It used to carry a full copy of the user documentation;
    it drifted, and the copy was the wrong one - see the convention below.
  - `FlatCheckCli.cs` - `Run(args, stdout, stderr)`: mode dispatch, writing the map(s) to
    disk, and the exit code. All output goes through the injected `TextWriter`, never
    `Console` directly. `WorldMapPath` lives here rather than in the core because naming a
    second output file is a console concern: the window never writes either map to a path
    of its own choosing.
  - `Options.cs` - CLI parsing.
- `FlatCheck.Wpf/` - the window app, `net8.0-windows`. See "The two applications".
  - `Session/` - `SirilSession` (what a session folder means on disk: which file is the
    master, which are raw flats, what counts as a flat master by name - and **where the
    master may be**: `masters/` or `process/`, see "Two places for the master" under "What
    the window adds"; also `SmoothedMasterPath` and `SyntheticMasterPath`, the two files
    the writing tabs put beside the master, neither ever the session's master) and
    `SessionMessages` (the explanations shown when the input is not usable, the
    Ratio tab's in-UI documentation, and the residual map's shape guide), plus the four
    files behind the "run the script from here" button - see "Running the Siril script"
    under "What the window adds": `SirilInstall` (where `siril-cli.exe` and the `.ssf`
    scripts are; the config parsing is pure, `Locate()` is the only thing that touches the
    machine), `SirilScript` (commands and input folders read out of a `.ssf`),
    `SirilLogLine` (one stdout line, sorted into log / progress / other) and
    `SirilScriptRun` (the `Process` wrapper, plus the `SirilRunner` delegate the tests
    replace). `FlatFrames()`
    lists raws *and* FITS from `flats/` (via `FitsReader.IsFitsPath`), and
    `HistEqCandidates()` puts the flat master in front of them for the Histogram tab, and
    between the two the smoothed master **when the file exists** - by the exact path
    `SmoothedMasterPath` names, never by pattern, so a `_netezit` beside the *older* of
    two masters is not listed (it is another master's smoothing). Three kinds of row,
    `HistEqKind`. The only part
    of the app with real rules, and the part with the most tests. It has no `RatioCandidates`: the Ratio tab deliberately
    proposes nothing, because the two masters it needs live in two *different* session
    folders - see "The two applications".
    `ProfileCatalog` is the lens-profile catalog loaded once for the whole window (in the
    background, on first request, retried after a failure) - it was the Sintetic tab's
    until the Structure tab needed the same list; `MainViewModel` builds it and hands it
    to both, and a `Func<string, LoadedFrame>` beside it lets the tests put a fabricated
    raw in place of a file in `flats/`.
  - `ViewModels/` - one per tab over a shared `TabViewModel` (busy / result / error) -
    `SyntheticTabViewModel` being the seventh, with `LensRow` (a public row over an internal
    `LensChoice`) and `SyntheticGuideViewModel` (the sources panel, collapsed by default) -
    plus `PreprocessViewModel` (the script block on the problem card: list, default, blocker,
    warning - decides nothing about flats) and `SirilRunViewModel` (one script run as the
    run window sees it: log text, progress, current command, outcome), plus
    `MainViewModel` for the folder, `AboutViewModel` for the "Despre" window (constant
    properties only - `VersionInfo`, `RunReport.WarrantyLines`, `ThirdParty.Components`
    copied into public `ComponentRow`s, headings from `SessionMessages`; it formulates
    nothing), and two small companions of the residual map shared by
    the Structure and Ratio tabs: `MapScaleViewModel` (the scale chips, which translate a
    choice into a `PngWriter.MapRange` and decide nothing) and `MapGuideViewModel` (the
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
    whether that is the user's sensor - `SyntheticReport.FormatMatch`, the sentence the
    console prints at "format" and the tab repeats under the written depths.
    `HistEqViewViewModel` is the fourth, on the Histogram tab, and the only one
    whose option list depends on the result (six chips on a CFA frame, `Mozaic` first and
    default, one on mono). `ImageZoomViewModel` beside it holds the zoom *state* - fit or
    a factor, plus the effective factor the control writes back - so a test can drive it
    and the tab can reset it on a new file (and deliberately not on a chip change). `StructureRows` is not a view-model but a builder: the five `MetricRow`s of the
    deviation battery, built once for **both** the Structure and the Ratio tab, since Ratio
    runs the same analyzer on the combined master. They are `public` because WPF binding
    reads public properties by reflection - and that has a useful consequence: a public
    view-model **cannot** expose an internal core type, so the compiler itself stops a
    `StructureResult` from leaking into XAML. `MapScaleOption.CapPct` is `internal` for
    exactly that reason, while `Label` and `IsSelected` are what XAML binds.
    `TestNote` earns its place the hard way: two of the five rows - the azimuthal
    asymmetry and the tilt - sat in the window showing a number, a verdict and no word
    about what they measured, for a whole version. The two notes that did exist were
    written inline in `StructureTabViewModel`, so nothing counted the missing three.
    `EveryTestRowCarriesItsOwnDescription` now fails if a row loses its note. The console
    has no caller: its explanations are grouped under the scale, and five notes threaded
    between the rows would drown the figures.

    **The Ratio tab was the same battery composed a second time**, and that is why it kept
    falling behind: five hand-written `Grid` blocks in XAML with the labels as literals and
    ten string properties behind them, so the fifth test had to be added twice and the
    notes reached only one tab. Both tabs now render `StructureRows.Tests(result)` through
    the shared `MetricRowTemplate`, and `TheRowsAreTheSameOnesTheStructureTabShows`
    compares the two tabs' labels, notes and key flags directly. Ratio's list is a
    **replaceable `IReadOnlyList`, not an `ObservableCollection`**, for the reason
    `Histograms` is: `Choose` starts `RecomputeAsync` without awaiting it, so two quick
    picks put two analyses in flight - a collection cleared and refilled by both ends up
    with duplicated rows, a replaced reference just lets the last one win. Since the
    per-run token below, only the latest run reaches the screen at all, but the list
    shape stays: it is right whatever the timing. Structure keeps its
    `ObservableCollection` because `MainViewModel.OpenAsync` cancels the previous run first.

    **Each Ratio run has its own token, linked to the session's**, the Histogram tab's
    pattern: a new pick cancels the previous run's token, and the superseded lambda stops
    at its check after the await without touching anything. It used to run on the
    session's token alone, so a re-pick during a run let both results land, in whichever
    order they finished. In the app the UI thread serialised the two continuations; in
    the tests there is no synchronisation context, the two continuations ran in parallel
    on the pool, both mutated `_mapCache` (a `Dictionary`) from two threads, and
    `ChoosingTheSecondFileComputes` failed once in a while - the guard turned the
    exception in the latest run into `HasResult = false`. `RatioRun.Execute` takes no
    token, so an analysis already started still runs to the end (memory overlaps for its
    duration, as on the Histogram tab); its result is dropped. `Pending` exposes the run a
    pick started, so a test can await one it did not start;
    `ASecondPickSupersedesTheFirst` pins that the last pair wins.

    `StructureRows.Informative` does the same for the three no-verdict rows (vignetting,
    illumination centre, local spots), so Ratio now shows everything the console's
    `--ratio` report prints under `INFORMATIV`, plus the dust warning under its verdict.
    The one line deliberately left out is `median / max ADU`: it belongs to the structure
    mode's header, not to `WriteBody`, because on Ratio's combined master - normalised to a
    median of 1 - it would mean nothing, and the console does not print it there either.
  - `Views/SirilRunWindow.xaml` - the live log of a script run, modal over the main
    window; starts the run on `Loaded`, confirms before closing mid-run (closing kills
    Siril), scrolls the log to the end. Rendered by `WindowSmokeTests` in three states.
  - `Views/` - the XAML. `BusyOverlay`, `MessagePanel` and `ReportPanel` bind to the
    inherited `TabViewModel`, so the same three controls serve every tab; `SyntheticView`
    is the one with text boxes (`Style x:Key="Field"` in `Dark.xaml`, keyed like `LogBox`
    because a TextBox is not an ordinary control of this app);
    `MapGuidePanel` binds to `MapGuide.*` and serves the two tabs that have a map guide.
    Headings that COUNT something are literals in XAML and no test reads them: the
    Structure tab said "SCARA CELOR PATRU TESTE" for a whole version after the tests
    became five, and it was caught by looking at a `FLATCHECK_DUMP_SCREENS` render, not by
    the suite. `TheScaleHasFourBandsAndMarksTheCurrentOne` now also asserts the test count,
    which is the number that heading quotes.
    Every set of radio chips needs its own `GroupName` (`StructureMapScale`,
    `RatioMapScale`, `ExposureHistogramSource`, `HistEqView`): WPF groups radio buttons by
    name across the whole window, so a shared name makes one tab's chips uncheck
    another's. `HistEqView` is the one tab whose `BusyOverlay` sits inside the right-hand
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
  - `Views/AboutWindow.xaml` - the "Despre" window: icon, banner, copyright, the GPLv3
    warranty notice and the third-party list. Opened from the `Despre` button in the top
    bar and from **F1**, both through `ApplicationCommands.Help` (whose default gesture is
    F1), so no view-model has to open a window. Modal, centred on its owner, `IsCancel`
    on the close button. Hyperlinks are handled once, on the window, through the routed
    `Hyperlink.RequestNavigate` - the component rows live in a `DataTemplate` in
    `Dark.xaml`, and a resource dictionary has no code-behind to put a handler in.
    `WindowSmokeTests` renders the window's *content*, not the window: an unshown
    `Window` renders transparent (the existing `fereastra` dump is blank for the same
    reason), while the root panel inherits the DataContext and walks the same bindings.
  - `Presentation/AppIcon.cs` - the largest frame of `flatcheck.ico`, chosen through
    `BitmapDecoder`: an `<Image>` handed the `.ico` directly takes whichever frame comes
    first, which at 72 px on screen can be the 16 px one scaled up.
  - `Presentation/RasterBitmaps.cs` - a byte raster straight into a frozen `BitmapSource`
    (`Gray8` / `Rgb24`), no PNG. The maps still go through `PngWriter` because the PNG *is*
    the file the console writes and the window shows those exact bytes; the equalised
    views have no file on the window side, and at sensor resolution a PNG of equalised
    noise would be 15-20 MB and seconds per view.
  - `Presentation/` - `VerdictPalette` (colour derived from `Stats.Severity`, never from
    its own list of verdict names), `ChannelPalette` (R/G/B hues for the exposure
    histograms - deliberately **not** routed through `Stats.Severity`: a red channel is
    not a worse channel, and an unknown mosaic order paints all four the neutral accent so
    the screen cannot imply a measurement that was never made), `HistogramCanvas` (the
    hand-drawn chart) and the value converters.
  - `Theme/Dark.xaml` - the palette, the control styles, the converters and the row
    templates. The converters live there rather than in `App.xaml` because a
    `StaticResource` inside a dictionary resolves in *that* dictionary. It also styles
    `ToolTip`: WPF's default tooltip is light, the implicit `TextBlock` style paints text
    light, and the two met as white on white on the Sintetic tab's sensor chips - the
    only tooltips in the app so far. Any new tooltip gets the dark one for free.
  - `Interop/DarkTitleBar.cs` - the one P/Invoke: Windows draws the title bar, not WPF.
- `FlatCheck.Tests/` - xUnit tests for the core and the console, reaching them via
  `InternalsVisibleTo`. `Golden/` holds the characterization snapshots.
- `FlatCheck.Wpf.Tests/` - xUnit tests for the window app, `net8.0-windows`. Separate
  project rather than a folder, so `FlatCheck.Tests` - which carries the parity story -
  stays portable. Borrows `FitsTestData`, `TempWorkspace` and `TestImages` from it by
  `<Compile Link>` rather than copying them - the last one for `JpegOf`, which encodes a
  camera preview in the test so the Exposure tab can be exercised with the source switch
  on without a real raw anywhere in the suite.
- `flatcheck.py` - the original Python/numpy/astropy prototype the C# tool was
  ported from. Prior art only for *features*; **not maintained in parallel** - don't
  feel obliged to mirror C#-side changes into it, except where parity is tested (see
  "Parity with Python"), which now includes the `--light` guard, series mode and the
  rewritten attribution mode. It is, however, the reference for
  *numeric* behaviour: see "Parity with Python" below.
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
- `lensfun/` - a verbatim copy of the lensfun database (`data/db`, 59 XML files,
  5.2 MB, CC BY-SA 3.0), committed, with `LENSFUN-VERSION.txt` (upstream commit and date),
  `LICENSE.txt` and `update-lensfun.py` (dev-time refresh; GPL header, scanned by
  `LicenseHeaderTests`). Both application csproj files copy it next to the exe at build
  and publish; the tool reads `lensfun/*.xml` beside its own exe and nothing is embedded.
  Not git-ignored: a fresh clone must build, and the user's decision (2026-09-17) was
  "whole database, not embedded, folder beside the exe". Updating is replacing the files.
- `README.md` - **the** user-facing documentation, in Romanian, and the single source
  for it. See the note in "Conventions".
- `LICENSE` - the verbatim GNU GPL v3 text. See "Licensing" below.

- `docs/` - measurements that outlived the plans they were made for. **Implementation
  plans are deleted once done**: their step lists go stale at the first later change and
  git history keeps them anyway (`git log --all -- PLAN-HISTEQ.md`). What does not go
  stale - a measurement made on a date on named files, a design tried and rejected - is
  folded into this file or into the header comment of the code it explains, and the long
  form, when there is one, lands here: `siril-histeq-source.md` (the Siril 1.4.4 code the
  `--histeq` mode mirrors), `smooth-validation.md` (the five real masters, the three
  designs and the acceptance tables behind `--smooth`), `radial-model-even-term.md` (the
  second face of the ring artefact: the sky session that exposed it, the five designs
  measured and rejected, the exact algorithm of `RadialModel` / `RingFit` and its
  before/after figures) and `lens-profiles.md` (the synthetic flat: the Adobe and lensfun
  model definitions quoted from their sources, the catalog and reader rules, the output
  format, `--against`, and the acceptance figures). **`docs/` is maintained, in one
  precise sense**: each file is the evidence behind a decision that is still in force, and
  each says at the top which code it covers and what it was measured on. A change to that
  code means the evidence is re-run and the file updated - or, if the decision goes, the
  file goes with it. A measurement left standing next to code it no longer describes is
  the same trap as a stale plan, only better hidden. (The one file that needs no re-run on
  our side is the Siril source excerpt: it stays true for the Siril tag it quotes, and
  changes only if the mode starts mirroring another version.)

The test refactoring that produced the current suite (eight phases, 2026) and the seven
feature plans since were run to completion and removed, per the rule above.

## Conventions

- **User-facing text (CLI messages, `--help`, window labels, code comments) is in
  Romanian** - keep new text consistent with this. Documentation *about* the code
  (this file, `docs/`, an implementation plan while it lives) is in English.
- **Nothing is computed outside `FlatCheck.Core`.** If a change needs a new number, a new
  threshold or a new sentence of diagnosis, it belongs in `Analysis/`, `Thresholds.cs` or
  `Report/` - never in a view-model. Both applications then get it, and get the same one.
  A view-model formats; it does not decide.
- **One external dependency, and it took a measured argument to earn it.** The FITS
  reader and PNG encoder stay hand-rolled. Don't add a NuGet package to replace them,
  and don't add a second one without the same standard of evidence; the point of this
  tool is that it is a single copyable binary. See "The one dependency".
- **Every metric is a ratio**, so ISO/gain cancels out. Don't add gain-dependent
  corrections - if a new metric isn't scale-invariant, that's a design smell.
- **Thresholds are empirical rules of thumb, not physical constants.** They are a
  starting point to be calibrated against a known-good setup.
- **Never bump the version and never commit** - both are the user's call. See
  "Versioning" and "Committing" below.
- **Every source file starts with the GPL notice.** New files included. See
  "Licensing" below.
- **User-facing documentation lives in `README.md`, and only there.** Do not restate the
  modes, the thresholds, the workflow traps or the `--light` validity limit in a
  docstring or a file header. It was duplicated between `README.md` and `flatcheck.py`'s
  docstring once, the two diverged, and the README was the wrong one - it still described
  a single-file `FlatCheck.cs`, a `csharp/` directory, three modes, and a port that had
  never been compiled. Code comments explain *why the code is the way it is*; the README
  explains *how to use the tool*.

## The dependencies

### The rule

**Dependencies are discussed before they are added, not avoided.** The tool still leans
toward hand-rolled where hand-rolled is honest - the FITS reader and the PNG encoder stay
as they are, and nothing replaces them. But a package is a normal thing to reach for when
it does a real job better than we would. Bring the candidate, its licence, its size and its
alternatives to the author, and let them decide. **Do not add one silently**, and do not
add one to save yourself an afternoon on something the BCL already does.

Two constraints do not bend:

- **Licence compatibility is not negotiable.** The tool is GPLv3-or-later, so anything
  linked in has to be GPLv3-compatible, and the reason it is has to be written down. See
  `THIRD-PARTY-NOTICES.md`, which is the file that records those decisions, and "Licensing"
  below.
- **The single-copyable-binary promise.** Pure-managed packages keep `PublishSingleFile`
  simple. Per-RID native binaries cost the same care `Sdcb.LibRaw` already needs, which is
  a point against, not a veto.

**A new package means three places, and a test enforces two of them.** Besides the
`PackageReference` itself: an entry in `THIRD-PARTY-NOTICES.md` (the *why* of the
compatibility) and an entry in `FlatCheck.Core/ThirdParty.cs` (the name, role, licence,
copyright notice and URL that the window's "Despre" **displays**).
`ThirdPartyTests.EveryPackageTheCoreLinksIsListed` fails if a package in the Core csproj
is not claimed by exactly one component, or a component claims a package that is gone;
`EveryComponentHasAnEntryInTheNoticesFile` fails if the notices file has no heading for
it. The README's table under "Componente terțe" is the fourth copy and is kept by hand.

**A data component is the exception the test knows about.** The lensfun database is
shipped as files, not linked as a package, so its `ThirdPartyComponent` claims no package
(`IsData`), and `EveryComponentNamesItsLicenceAndUrl` allows exactly one such component.
It is still displayed in "Despre": CC BY-SA asks for attribution, and that list is where
the program shows its notices. The Adobe profiles are deliberately **not** a component:
they are read from where Adobe installed them and never copied - the notices file says
so, under its own heading, so that nobody adds them.
The list is displayed rather than only shipped because the single-file exe carries no
notices file beside it, and because LGPL-2.1 §6 asks for it - see "Licensing".

### `Sdcb.LibRaw`

Plus its `runtime.win64` / `runtime.linux64` native packages. It exists for
one reason: `--expo` on a camera raw, which was the last feature gap against
`flatcheck.py`.

**The alternatives were measured, not assumed.** There is no pure-managed .NET decoder for
a Nikon NEF - the pixels are Huffman-coded with a predictor and passed through a
linearization curve out of the MakerNote. What NuGet actually offers: `FC.SDK.Raw` is pure
managed but Canon-only; `NDCRaw` shells out to `dcraw.exe`; `HurlbertVisionLab.LibRawWrapper`
is C++/CLI and Windows-only; Vintasoft / Accusoft / LEADTOOLS / Atalasoft are commercial
*and* demosaic, which destroys the exact thing `--expo` measures; ImageSharp, SkiaSharp
and Magick.NET have no raw mosaic access at all. `Sdcb.LibRaw` binds the same libraw that
`rawpy` binds, so the numbers are bit-identical rather than close - verified on a real
D3300 NEF: same pixel sum (35598956557), same mean to twelve decimals, same four CFA
medians, same report text apart from the scale-source label.

Three properties keep the cost contained, and all three are worth preserving:

- **It is reached from exactly one file.** `RawReader.cs` is the only thing that names a
  `Sdcb.LibRaw` type. Everything downstream still sees an `Img`.
- **It is loaded lazily**, and that survives even deleting `Sdcb.LibRaw.dll` itself, not
  just the native DLLs: a FITS run exits 0, a raw run exits 1 with a message naming what
  is missing rather than letting a `0x8007007E` reach the user. `RawReader.Load` catches
  `DllNotFoundException` and friends for that.

  What makes it lazy is narrower than it looks, and was measured rather than assumed.
  `RawReader`'s **type** is loaded on every run, FITS included, because `ImageLoader` asks
  it `Handles`. Method bodies are only resolved at JIT time, so `Decode` costs nothing
  until a raw is actually opened - but **fields are resolved when the type is loaded**. A
  static field of a libraw *reference* type turns out to be harmless (the CLR defers), a
  field of a libraw *value* type breaks every FITS run. So: no libraw-typed fields on
  `RawReader`, of either kind. `HoldsNoLibrawTypedFields_SoTheAssemblyStaysLazilyLoaded`
  pins it; nothing else would notice.
- **Nothing is developed.** Only `raw_image` (cropped to the visible area, like rawpy's
  `raw_image_visible`), `color.maximum` and the black levels, plus `idata.filters` /
  `idata.cdesc` for the mosaic order and the embedded preview bytes. No demosaic, no white
  balance, no gamma - all of which would make the measurement meaningless.

**Its licence takes a decision, so it is recorded rather than assumed.** The C# wrapper is
MIT. The native `runtime.*` packages are LibRaw itself, offered as **LGPL-2.1-only OR
CDDL-1.0**. We take the LGPL option: CDDL is not GPL-compatible, so choosing it would break
the tool's own licence. LGPL-2.1 §3 permits applying GPL v2 "or any later version" to a
copy, which is what brings it into a GPLv3 work.

### `SixLabors.ImageSharp`

Decodes the JPEG preview the camera embeds in a raw - the image the camera's own screen
histogram was computed from. Reached from exactly one file, `PreviewReader.cs`, the same
containment `RawReader.cs` has.

It is not there to replace anything hand-rolled: the PNG encoder still writes our own
bytes. It is there because the alternative was a hand-rolled JPEG decoder - Huffman, DCT,
chroma subsampling, YCbCr, progressive mode - which is an order of magnitude beyond an
encoder that only writes, for a picture that never votes on a verdict.

**The licence needs reading, and it is fine here.** ImageSharp 3.x and later are under the
Six Labors Split License 1.0, which is a *conditional dual* licence, not a restrictive one.
It grants Apache-2.0 terms when "You are consuming the Work in for use in software licensed
under an Open Source or Source Available license"; a commercial licence is required only
for the other cases, essentially proprietary use above 1M USD revenue. FlatCheck is GPLv3,
so it qualifies on the first condition with nothing to change. Apache-2.0 then flows into
GPLv3 one-directionally, which the ASF states plainly: "Apache 2 software can therefore be
included in GPLv3 projects."

Two consequences worth keeping:

- **Pin the version.** The licence text is "Version 1.0, June 2022" and does not say which
  licence version governs which release. The grant travels with the version taken, so
  pinning is legal hygiene as well as reproducibility.
- **It is pinned to 3.1.x, not 4.x, and that is about the build, not the licence.** From
  4.0.0 the package ships an MSBuild task that validates a licence key at compile time: a
  warning on Debug, an **error on Release** without a `sixlabors.lic` or
  `$(SixLaborsLicenseKey)`. Six Labors grants that key free to open-source projects, but on
  application, and anyone cloning this repo would need their own to publish. 3.1.x carries
  the identical Split License 1.0 text, no task, and the only API difference that matters
  here - it never applies an ICC profile on decode - is the behaviour wanted anyway. Moving
  to 4.x means obtaining the key *and* setting `ColorProfileHandling.Preserve` explicitly.
- **Because GPLv3 forces every derivative to stay GPLv3**, and GPLv3 is an open source
  licence, everyone downstream qualifies on the same condition. The eligibility does not
  break as the tool is passed on.

`StbImageSharp` (Unlicense/MIT, zero dependencies, ~identical output measured against an
independent decoder on real previews) was the alternative, and would have carried no licence
footnote at all. ImageSharp was chosen for robustness on the awkward cases a camera preview
can be - progressive, CMYK/YCCK, unusual sampling - and for scaled decoding. If its licence
ever moves, StbImageSharp is the drop-in fallback and the seam is one file wide.

## The two applications

Since 2.0.0 there are two front ends over one core: `flatcheck.exe` (console) and
`FlatCheck.Wpf.exe` (window). **Neither reimplements a number.** Both call `Runs/`, which
loads the file, calls the analyzer, writes the report into a `TextWriter` and encodes the
map. What differs is only what each does with the three outputs.

| | console | window |
|---|---|---|
| input | one file (or a folder, with `--light`) | one Siril session folder - **except the Ratio tab**, which takes two files chosen by hand, normally from two *other* folders; the Histogram tab lists the flat master and every raw or FITS in `flats/`; the Netezire tab takes the session's master and writes beside it. A folder without a master in either `masters/` or `process/` can be preprocessed from the window, through the user's own `siril-cli` |
| report | written to stdout | shown as panels, saved as `Rezultat testare.txt` |
| map | written to `-o <path>` (`--ratio` writes a second, `-lume`; `--smooth` a second, `-dupa`), always on the automatic scale | shown from memory (`--ratio` and the Netezire tab offer both behind a toggle), on a switchable scale with a legend and a shape guide, saved as `flatcheck.png` - the file on the automatic scale, like the console's |
| smoothed flat | written to `--smooth <path>` | written to `pp_flat_stacked_netezit.fit` beside the master, on a button, after asking about an existing file |
| synthetic flat | written to `--synthetic <path>`, the lens from `--lens` text; `--against` compares two files | written to `pp_flat_stacked_sintetic.fit` beside the master, the lens from a searchable list, focal / f-stop / crop prefilled from the master's header and a raw in `flats/`; the comparison against the file follows the write by itself, and "Compara cu masterul" runs it alone on a file written earlier |
| result | exit code | coloured verdict |
| options | `--fov`, `--sat`, `--black`, `--pattern`, `--sky`, `--scale`, `--lens`, `--raw`, `--source`, `--focal`, `--fstop`, `--crop`, `--profiles` | `--sky` (a checkbox), the smoothing scale (three chips), the lens list, its three prefilled fields and the sensor-format chips behind the crop; the Structure tab's profile comparison takes the first raw in `flats/` by itself |

The console keeps the options because it is the scriptable one, and because `--fov` and
`--sat` exist for telling the tool something the header does not say. Adding them to the
window would be four more controls in service of the rarer case.

### What the window adds, and what it does not

It adds a workflow: one folder instead of four invocations, `pp_flat_stacked.fit` found
by convention instead of by typing, structure and end-to-end started on open. It does not
add an analysis, a threshold or a wording. If a change here needs a new number, that number
belongs in `Analysis/`, and the console gets it too.

It adds a "Despre" window (button in the top bar, or F1), which adds no number either:
version, copyright and the warranty notice are the console's own strings, and the
third-party list is `ThirdParty.Components` from the core. See "Licensing" for why the
window has to exist at all.

It also adds three things around the residual map, none of which is a number: a **scale
switch** (auto / ±2 / ±10 / ±20 %, default ±10, caps from `Th.Resid*` - see "PNG output"),
a **legend** stating the real ends of the displayed scale, and a collapsed **shape guide**
saying what each shape on the map has meant on real masters. The guide is documentation,
not a classifier - the tool still does not decide what a shape is, and must not start:
a blotch can be a light leak or a cloud, and the pixels do not tell them apart. Its text
is in `Session/SessionMessages.cs` and mirrors the README's table **by hand**; no test
binds the two. Structure and Ratio get all three; **End-to-end gets the legend only**,
because its map is the fitted radial term, graded against `Th.LightRad*` rather than the
residual scale, so those caps would mean nothing on it.

The guide's ring row was **rewritten** after the artefact described under `RadialModel`
was found: it used to say rings meant a decentred round obstruction, which was the reading
of an artefact, and sent the author looking for an object that had never been there. Don't
restore the old wording from git history - `TheRingRowDoesNotClaimARingMeansADecentredObstruction`
fails if you do.

The Exposure tab adds the same kind of thing around its histograms: a card of one chart per
channel, a paragraph derived from the result (`ExposureReport.HistogramReading`), and a
collapsed guide whose text is in `SessionMessages` and mirrors the README **by hand** - no
test binds the two, exactly like the map guide. All of it is drawing and wording; the
numbers come from `ExposureAnalyzer` and the bands from `ExposureBands`, so the shaded
target band *is* the band that voted. `Histograms` is a **replaceable list, not an
`ObservableCollection`**: picking a file starts a run nobody awaits, and a second pick (or
the Sky toggle) can start another over it - scalar properties just let the last one win,
but a collection cleared and refilled by two runs at once ends up with duplicated rows and
nulls in it. The light series is the opposite case and does need `ObservableCollection`,
because it appends rows as frames finish.

Above the charts sits a switch, `HistogramSourceViewModel`, on the `MapScaleViewModel`
pattern: two radio chips, raw data or the camera's preview. Flipping it rebuilds the rows
from the **retained** `ExposureResult`; nothing is re-read or re-analysed. It is disabled
when the file carries no preview (every FITS, and a raw whose preview would not decode), and
disabling it forces the selection back to raw. The chips' `GroupName` is
`ExposureHistogramSource`, unique in the window for the same reason the map scale ones are.
Under the charts, a three-row table pairs the raw centre median with the preview's, per
colour, on both views - it is the payoff, so it is never hidden behind the switch.
`Populate` is `internal` so a test can hand in a result carrying a preview built from a JPEG
the test encoded itself (`TestImages.JpegOf`); no raw file is needed anywhere in the suite.

The Structure tab carries a fourth block, `HowToReadPanel`, under the verdict: **which
half of the screen decides what.** The numbers answer "how much" and own the verdict; the
map answers "what kind" and never votes. The screen's own layout suggests the opposite -
two columns look like two views of one thing - so it is said in words. Same collapsed
pattern and the same `GuideRow` template as the map guide, text in `SessionMessages`
beside it, state in `HowToReadViewModel`. The disclosure toggle those three share lives in
`Dark.xaml` as `Style x:Key="Disclosure"`; it had been hand-written twice before that.

**The Structure tab compares with the profiles in the same run, when `flats/` holds a
raw.** `SirilSession.ReferenceRawPath` (the first raw, through `LensIdentification.FirstRaw`
- the Sintetic tab's prefill reads the same file) makes the run exactly `flatcheck master
--raw <that raw>` with the window's `ProfileCatalog`, so the saved report stays the
console's and the verdict appears once, already combined - not PERFECT on five tests and
REFA two seconds later. The catalog is awaited before the run (a second or two on the first
open, under the busy overlay, which says so). Without a raw the run is the plain one, the
report identical to `flatcheck master`, and the card under TESTE shows
`StructureReport.GapText` computed by `StructureRun.Compare` on the master's header alone.
The rows are `LensProfileRow` (public copy of the core's `ProfileRow`), rendered by a
template local to `StructureView.xaml`; `Tests` stays five, so the scale heading and the
Ratio and Netezire tabs, which reuse `StructureRows.Tests`, are untouched. `Populate` is
`internal` and sets `HasResult` for the smoke test. The one thing the window cannot do
that the console can is `--lens` by hand: a body without a raw in `flats/` gets the
explanation, not a lens picker (the user's decision, 2026-09-18: the raws are always there).

#### Two places for the master

Siril has two conventions for where `pp_flat_stacked.fit` lands, and both exist in the
author's archive: the scripts repo (`OSC_Preprocessing_WithoutDark.ssf` and its siblings)
stacks with no `-out=`, so the master stays in `process/` beside the calibrated frames;
the scripts shipped with the 1.4 install (`OSC_Preprocessing.ssf`, `Mono_Preprocessing.ssf`,
the `Extract_*` ones) use `-out=../masters/pp_flat_stacked`. `2026-06-18-doar-cer-acasa` is
the first kind, `2026-08-12-Smida` the second, and the window refused the second for a
whole version because `FlatMasterPath` was a fixed path into `process/`.

`SirilSession.FlatMasterCandidates` lists both paths, `FindFlatMaster()` picks the one
that exists, and `FlatMasterPath` is that or null. Three decisions, all the user's:

- **When both exist, the newest wins** (by last-write time) - that is what the last
  preprocessing produced - and `FlatMasterLookup.Other` names the older one so the
  Structure tab can show `SessionMessages.TwoFlatMasters` under the source line. Two
  masters in one folder are two runs, possibly from two sets of flats, and the user has
  to know which one the numbers are about. The Histogram tab's master entry is the same
  file, through the same lookup.
- **Only the master is required.** `Check()` no longer demands `process/`: a folder with
  `masters/` and no `process/` (frames deleted to save space, master kept) runs
  Structure, and the End-to-end tab explains by itself that it has no series.
  `NoProcessDir` now means "neither folder exists", i.e. nothing was ever run.
- **Nothing else moved.** The light series stays in `process/` under both conventions;
  the console takes explicit paths; the core knows no folders. The Ratio dialog starts
  in the folder the open session's master is in, since that is where the stacks are.

The problem message for a missing master prints both paths it looked at.

#### Running the Siril script from the window

The problem card (no preprocessing run at all, or no flat master in either `masters/` or
`process/`) carries a **Ruleaza in Siril**
button. It runs `siril-cli.exe -d <session> -s <script>` from the user's own install and
shows the output live in `SirilRunWindow`; when that window closes after an exit code of 0,
`MainViewModel.ReopenAsync` runs `Check()` again, so whether the master appeared is decided
by the same code as always, never by the run window. Nothing here is a number, so nothing
is in the core.

What was measured before it was built, on Siril 1.4.4, and what follows from it:

- **stdout is live through a pipe.** Timed with a Python reader: the gaps between lines
  match the work, not a flush at exit. So a plain `Process` with redirected stdout is
  enough; the named-pipe mode (`-p`) was not needed. Exit code 0 on success, 1 when the
  script stops at a command; stderr stays empty but is read anyway.
- **Three quarters of the output is progress.** 1735 lines for a 3+3+3-frame session, 1324
  of them `progress: <label>, x%` or the ANSI-prefixed update form
  (`ESC[A ESC[2K Tprogress: 66.67%`). `SirilLogLine` turns those into a bar and shows only
  the `log:` lines as text, which is what Siril's own log pane shows. The debug prints
  ("Block 0: ...", "seqfile ... already exists") are dropped. The parser is pinned on
  recorded lines, not on what Siril ought to print.
- **Re-running overwrites cleanly.** The script run a second time over a complete
  `process/`, and a third time over one with the master and the `pp_light*` files deleted,
  rebuilt every file and exited 0; the only complaints were `seqfile ... already exists,
  not overwriting`, about the `.seq` index files, which is harmless. So "Opreste" mid-run
  leaves a folder the next run fixes, and the cancel message says so.
- **Siril prints only the verb** (`Running command: cd`). `SirilRunViewModel` counts those
  lines and reads the full command out of the script (`SirilScript.Commands`), so the
  window and the failure verdict can say `comanda 2 din 20:  cd biases`.

Four decisions:

- **The script is chosen on the card, not in the run window.** The window is the log of a
  run already started; a window that first asks and then runs would be two screens in
  one. The list is what Siril's script menu shows - the `.ssf` entries of `selected_scripts`
  plus the `.ssf` files under `script_path`, from `%LOCALAPPDATA%\siril\config.<ver>.ini` -
  and `OSC_Preprocessing_WithoutDark.ssf` is preselected when present. **Nothing is
  bundled**: the tool runs exactly the file Siril would, from wherever Siril keeps it (on
  this machine the scripts repo Siril clones into `%LOCALAPPDATA%\siril-scripts`, not the
  install's `scripts/` folder, which holds only the six stock ones).
- **WithoutDark is the default because it is the user's script** (a camera without
  darks), not because of where it writes: since "Two places for the master" above, the
  stock `OSC_Preprocessing.ssf` works just as well. `SirilSession.PreprocessingScript` is
  the name the messages quote.
- **Inputs are checked before launching.** `SirilScript.RequiredFolders` reads the `cd
  <name>` commands with a plain name out of the chosen script (`cd ../process` and `cd ..`
  are not inputs), and `SirilSession.MissingInputFolders` refuses a folder that lacks any
  of them or has one empty - Siril would otherwise die at line three with a failed `cd`
  and the user would read the log to learn why. It is a rule of form, not an understanding
  of the script; a script that enters folders differently is simply not pre-checked.
- **Siril open is a warning, not a refusal.** `SirilInstall.IsGuiRunning` looks for a
  `siril` process (`siril-cli`, ours, has a different name). Two processes writing the same
  `process/` corrupt it, but the tool cannot know which folder the GUI has open. Checked when
  the card is built and again on the click.

`MainViewModel` takes the two machine questions - `Func<SirilInstall>` and `Func<bool>` -
as optional constructor arguments so the tests do not depend on what the running machine
has installed; the parameterless constructor uses `SirilInstall.Locate` and
`IsGuiRunning`. `SirilCliIntegrationTests` do use the real `siril-cli` when it is there
(a probe script that loads the fixture master, and one that fails on `cd nu-exista`) and
pass without checking anything when it is not - xunit 2 has no dynamic skip.

The scale switch is why `StructureTabViewModel` and `RatioTabViewModel` keep the residual
**raster** rather than only the PNG bytes: the raster is already in the result, whereas
pre-encoding four PNGs per run in the core would be ~7 MB and three encodings nobody may
look at. `RunOutcome` was deliberately not touched. Re-encoding happens on the UI thread,
which was measured rather than assumed: on the real 24 MP masters (1504x1004 analysis
raster) a cap costs 90-255 ms, once, and is then cached per option. Moving it to `Task.Run`
would need a generation counter so a stale result cannot land after a fresh one - more
complexity than the delay it removes.

Every **file** stays on the automatic scale whatever chip is selected, so both applications
keep writing the same bytes for the same master; the chips are a viewing aid. That changed
one existing test, on purpose - see
`OnTheAutoScaleTheDisplayedMapIsTheSameFileThatWasSaved`, which used to assert that
unconditionally.

**The Ratio tab is the one exception to "one folder in".** It proposes nothing and starts
empty, because the two masters it needs are two *shoots*, hence two session folders - the
open one is at most one of them. It used to list `pp_flat*.fit` from `process/` and
preselect two, which in a normal session offered the individual frames; a pair of single
frames yields a confident, wrong answer, because one frame's noise is the same order as
the signal. So it asks for two files explicitly, and carries a panel that explains what
the mode needs and what it cannot tell you - the only tab that must document itself before
it can be used at all.

The same trap survived one layer down until it was found in use: the two file dialogs
filtered on `pp_flat*.fit`, which in a real `process/` is mostly `pp_flat_00001.fit` and
its ninety siblings, and `SirilSession.IsFlatMasterName` accepted exactly what the filter
offered. So the tab still *proposed* the pair that yields the confident wrong answer.
`RatioPattern` is now `pp_flat_stacked*.fit` - the stack, not the frames - the dialog
builds its filter from that constant rather than repeating the string, and the refusal
message says why an individual frame is not a master. The `*` after `stacked` is there
because two masters brought into one folder must be renamed, and
`pp_flat_stacked_0deg.fit` is still a stack. `RatioTabViewModel`'s text properties are deliberately *instance*
properties even though their values are constant: `{Binding}` resolves through the
DataContext and never sees a static one, so the documentation would have rendered blank.

**The Histogram tab lists the flat master first, then the smoothed master when it exists,
then `flats/`, and lets a click interrupt a click.** The master is there for one question
- did what the individual frames show average out? - and it is not preselected and not run
at folder open, because the Structure run is reading that same 24 MP file at that moment.
The smoothed one is there for the other question - what did the smoothing take out, what
did it leave - and it is a **third kind of row**, not a second master: chip `NETEZIT`,
`IsMaster` false, its own note under the picture (`HistEqReport.SmoothedVersusMaster`,
which also says why a smoothed flat looks *noisier* under a rank-based transform). It can
also appear **during the session**: `SmoothTabViewModel.Saved` fires once, with the path,
right after the file is written (not on a refused overwrite, not on a write error), and
`MainViewModel` wires it to `HistEq.FileWritten` in its constructor - the one wire between
two tabs, placed there so neither knows the other. `FileWritten` rebuilds the list from
`HistEqCandidates()` (one definition of the list), keeps the selection by path without
re-running, and re-runs only if the selected file *is* the one rewritten - the picture
would otherwise be of bytes no longer on disk. It ignores any other path and a missing
session, and `Reset` drops the session for that reason. Rebuild, not insert: `ClosesGroup`
(the separator under the last of master + smoothed) is an `init` flag set by whoever sees
the whole list. A Histogram read overlapping a Netezire write is accepted, not prevented:
the write gets `IOException`, lands in `SaveError`, and the numbers stay. The loader covers the picture, not the
tab: the list stays clickable, a new pick cancels the running frame's own token and starts
over, and a superseded run may not touch the screen. That last part is general and lives in
`TabViewModel.RunGuardedAsync`: it numbers its runs and lets only the latest one clear the
busy state, show an error or drop the result; the other tabs never overlap runs, so for
them nothing changes. `TabViewModelTests` pins it with a stub tab. The per-view PNGs are
encoded on the UI thread and cached per chip, for the same measured reason as the map.

**Structure and the light series run one after the other, not in parallel.** A 24 MP frame
runs into the high hundreds of MB in analysis (known issue 7) and the series holds a loaded
frame besides; two chains at once could pass a gigabyte on an ordinary machine. The time
saved would not be worth it.

**The Netezire tab runs on a button and never on open.** It writes a ~96 MB file beside
the master, and a file is not something to produce because a folder was looked at. Its
guide (`SessionMessages.SmoothGuideRows`, mirrored in the README by hand) is the one guide
that starts *expanded*: it is the button's precondition. Three scale chips
(`SmoothScaleViewModel`: half, default, double of `SmoothAnalyzer.DefaultScalePx`, so no
second list of numbers) and the button; the button awaits `MainViewModel.WhenIdleAsync`
first, so a click during the structure run does not overlap it on the same 24 MP master.
The file is written *after* the numbers are on screen, and an existing file is asked
about through a `Func<string, bool>` the `MainViewModel` constructor takes (a `MessageBox`
in the app, a fixed answer in tests); a "no" keeps the old file and says so, the result
stays. The output is `pp_flat_stacked_netezit.fit` in the master's own folder
(`SirilSession.SmoothedMasterPath`, `SmoothedSuffix`), and **it must not become the
session's master**: `FlatMasterCandidates` are exact paths, so the Structure tab keeps
measuring the shot master and keeps showing the texture the smoothing was made for. It
does match `RatioPattern`, which is right - a smoothed stack is a stack - and it is listed
by the Histogram tab as its own kind of row, see above. The tab keeps the
two rasters (removed component, smoothed flat's residual) behind a radio toggle, cached
per (map, cap) like Ratio; `MapScaleViewModel` took a constructor argument for its
default cap so this tab can start at ±2 % (`Th.ResidPerfect`) - what is removed is small,
and at ±10 % the map would be white. `Populate` is `internal` and sets `HasResult`, so the
smoke test renders the tab from an analyzer result without writing a file. The second
button, `Propune scara`, runs `SmoothRun.Advise` the same guarded way, fills the rung
rows and the verdict from `SmoothReport`'s advice strings, selects the proposed chip
through `SmoothScaleViewModel.SelectScale`, and writes nothing; `PopulateAdvice` is
`internal` for the smoke test.

### Three things about the WPF project that were learned the hard way

- **No `<InvariantGlobalization>`**, however tempting the symmetry with the console. WPF
  needs culture data to bind anything at all: without it the window throws
  `Cannot find non-neutral culture related to 'en-us'` while *loading* XAML - not on some
  path, on every start. The invariance that is actually needed is narrower and is done in
  code: `App.ForceInvariantNumbers` pins `DefaultThreadCurrentCulture`, so a Romanian
  Windows does not turn `0.24` into `0,24` in a file whose whole point is being identical
  to the console's output. `CultureTests` pins both halves.
- **The dispatcher is captured once per run, not asked per frame.** `LightSeriesRun`'s
  callback arrives on a pool thread and has to marshal onto the UI thread to touch an
  `ObservableCollection`. Reading `Application.Current` each time means the answer can
  change mid-series, half the rows land on one thread and half on another, and rows are
  lost silently - six frames analysed, five rows in the table. Measured, in a test run
  where the WPF `Application` appeared partway through.
- **`WindowSmokeTests` exists because the unit tests cannot see XAML.** A wrong resource
  key, a missing converter, a binding to a property that isn't there - all compile, and
  some fail at load. The suite renders every tab with real results into a
  `RenderTargetBitmap` and asserts that no binding error was traced. The
  `InvariantGlobalization` crash above passed 100 unit tests and never started; it would
  fail here. `FLATCHECK_DUMP_SCREENS=<dir>` also saves the renders as PNGs - useful for
  looking at the window without running it, and not a golden comparison: how a window
  looks is a job for eyes.

## The core idea

For a photographic lens with no optical train (the author's setup is a Samyang
135 f/2 on a Nikon D3300), a *correct* flat is by construction a **purely radial**
function centred on the optical axis. Vignetting is rotationally symmetric; dust
at f/2 is below threshold.

Therefore any departure from radial symmetry is **not vignetting - it is error**
(non-uniform light source, light leak, tilted camera, ambient light). "Good flat"
does not mean "looks flat"; it means "the departure from radial symmetry is below
noise". That is a number, and it can be measured.

A crucial consequence, and an easy thing to break: the radial model and the
azimuthal asymmetry are computed around the **sensor centre** (`w/2, h/2`), *not*
around the fitted illumination peak. Centring on the fitted peak would let the fit
absorb exactly the linear gradient you are trying to detect (a gradient laid over
quasi-quadratic vignetting merely shifts the vertex), and the test would silently
cancel itself out. `FitCenter` never supplies the centre the other metrics use - it
supplies the *tilt*, which is a test, and the vertex offset, which since 1.2.0 is only
informative (see "The residual scale").

## Input contract

Modes 1-3 want a **calibrated** FITS - flat-dark/offset already subtracted. In the
Siril workflow that is `pp_flat_stacked.fit` in `process/` or `masters/` (the `pp_` prefix means
calibrated). An uncalibrated stack still has a ~150-600 ADU pedestal which
flattens the profile and under-reports vignetting by ~1%, and **all tests will
report OK anyway**, because the profile stays radially symmetric - only the
amplitude is wrong. Not detectable, only avoidable.

Mode 4 (`--expo`) is the exception: it wants a **raw, uncalibrated, single**
frame with the CFA mosaic intact and the original ADU scale, because stacking
normalises away the absolute level. The simplest way to satisfy that is to hand it the
NEF itself. Failing that, in Siril it means the **Conversion** tab (add NEFs → Convert),
*not* Open + Save, which debayers and normalises. The mode detects and rejects both
failure modes (see "Raw-file sanity gate").

Accepted formats: FITS (`.fit`/`.fits`/`.fts` - though the extension is never
actually checked, see Known issues), BITPIX 8/16/32/-32/-64, mono or colour; and
camera raw (`.nef`/`.cr2`/`.cr3`/`.arw`/`.dng`/`.raf`), where the extension **is**
what dispatches. All four modes accept both, exactly like `flatcheck.py`'s
`load_image_with_header`. A raw carries no FITS header, so `--light` sees an empty
dictionary and lands in the guard's `Unknown` layer - runs, with a note.

## The seven modes

Dispatch order in `FlatCheckCli.Run` is `--lenses` → `--synthetic` → `--against` → `--smooth` → `--histeq` → `--expo` → `--ratio` →
`--light` → structure (default). The modes are mutually exclusive; the first match wins.
The WPF app has no dispatch: each tab is one mode.

Each mode's chain lives in `Runs/`, in the core, and both applications call it - see
"The two applications".

### 1. Structure (default) - `flatcheck flat.fit`

Is the master flat radially symmetric? Runs on the final master.

1. `FitsReader.Load` → `Prepare` (2×2 block-mean, then downsample toward ~1200 px).
2. `FitCenter` → one paraboloid fit on `log(I)`, two outputs. The **tilt** (the
   peak-to-peak amplitude of the fitted linear term, `2·sinh(|b|+|c|)`) is a test, on
   the shared residual scale. The **vertex offset** from the sensor centre, as a
   percentage of the half-diagonal, is printed informatively and carries no verdict -
   see "The residual scale" for why that changed in 1.2.0.
3. `CornerStats` → median of four 5% corner boxes and a central box. Reports
   vignetting in stops (informative only, *not* a test) and the spread between
   the four corners (a test - the corners should all be equal on a radial field).
4. `RadialModel` around the sensor centre → residual `img / model`, then
   `LargeScalePtp` of that residual (tile-averaged, `p95 - p5`, **whole frame** - see
   "Shared pipeline" for why the inscribed-circle restriction went) and
   `MaxTileDeviation` (the single worst tile, whole frame - what the 5% trims of the
   residual throw away).
5. `AzimuthalAsymmetry` → per-ring peak-to-peak across 12 sectors; the max is the
   metric.
6. Verdict = the worst of the five tests - corner spread, azimuthal asymmetry,
   residual, tilt, worst tile - on the shared residual scale (see
   "The residual scale" below). Prints that scale, and marks
   `reziduu dupa model rad.` as the metric that matters most. Writes the residual
   heat map PNG.
7. `LocalSpots` → the deepest *compact* spot after the large-scale structure is
   subtracted (fine tiles minus their 7x7 neighbourhood median), with its position.
   **Informative, with a warning above `Th.LocalSpotWarn`** (2%), never a verdict: a
   dust shadow is correct for the session whose lights carry the same shadow and wrong
   for a library flat, and the tool cannot know which use is intended. The wording is
   `StructureReport.LocalSpotWarning`, shown by the console under the informative rows
   and by the window under the verdict. Measured on the real Nikkor 35 f/2.8 master the
   dust shadow visible in the map is -0.31%, well under the warning - which is the
   measurement behind "dust at f/2 is below threshold" above.

**Why a fifth test.** `LargeScalePtp` trims 5% of the tiles at each end so that it
measures structure rather than noise - and that same trim drops any defect covering under
5% of the frame, however deep. Measured on a 24 MP synthetic: a +10% blotch the size of a
fingertip in a corner read 0.04% residual and `PERFECT`, with the blotch plainly visible
in the map. The worst tile reads 9.3% on it. On a gradient the worst tile reads half the
tilt (it is a per-pixel deviation, the tilt is peak-to-peak), so it never carries the
verdict for a gradient; it exists for the compact case. Pinned by
`ACompactBlotchInACorner_IsNoLongerReportedAsPerfect`.

What the mode gives is an **upper bound** on the flat error, not the error: everything it
measures is the sum of a camera-fixed part (harmless - the flat divides it out) and a
world-fixed part (harmful). One master does not contain the information to separate them;
mode 2 does. The report says so, and the bound is still the right thing to run first,
because it is the only test that costs no extra shooting.

**The sixth test: the master's vignetting against the lens profiles** (`--raw <flat.NEF>`
or `--lens <text>`; the window does it by itself when `flats/` holds a raw). The five tests
measure departure from radial symmetry and are blind by construction to a *radial* excess:
the tablet masters of 2026-09-18 (Tokina 20 mm f/2.8, four orientations at 90°) read
PERFECT on all five with 1.26 stops of vignetting, against 0.66-0.92 from the two profile
databases - the panel's angular falloff, made radial by the rotation, and `--ratio` is
just as blind. The only external reference the tool has is the lens's published profile,
so `VignettingComparer.Analyze` puts the master beside every candidate lens's nearest
profile (focal, then f-stop in stops, then farthest distance - `LensCatalog.Nearest`).
Decisions, all measured:

- **The profile's depth is measured with the master's own operator.** Not the polynomial
  at the corner point: the model is built as a raster at the analysis size
  (`SyntheticFlatBuilder.Build`) and `Metrics.CornerStats` runs on it, so the box median
  and the half-pixel artefact (known issue 5) are the same on both sides. On a master that
  *is* the model the difference is 0.00 (`AMasterThatIsTheModelReadsZeroStopsAndPerfect`);
  what remains is the master's 2x2 block mean, under a thousandth of a stop.
- **No new threshold.** The difference in stops goes through `Stats.DepthErrorPct`
  (`2^|D| - 1`, the corner error of a light calibrated with the wrong flat) onto the
  residual scale, exactly as `--against`'s depth does: 0.03 / 0.14 / 0.26 stops. The
  arithmetic moved from `AgainstResult` into `Stats` when the second consumer appeared.
- **The verdict is the nearest profile's, not the worst.** The two databases differ by
  ~0.2 stops on the same lens (`docs/lens-profiles.md` §8); a master that coincides with
  one of them is not wrong, and "worst" would have called the good monitor masters REFA
  (0.15 off Adobe, 0.03 off lensfun). The block prints every profile, marks the nearest,
  and states the spread between them.
- **The run's verdict is the worse of shape and depth**, as in `AgainstResult`, and the
  exit code follows it. When the depth carried it the advice is `DepthAdvice`, which reads
  the sign: plus = the master falls faster than the model, the source falls off with angle
  (a panel too close or without a real diffuser; frosted sheets do not diffuse); minus =
  the source fills the corners (the sky dome, ambient light, a reflection). Neither
  `--ratio` nor more rotations help.
- **When it cannot compare it says why and votes nothing** (`ComparisonGap`: no profiles
  on the machine, no raw and no `--lens`, an ID nothing matches, no focal, no f-stop -
  Siril writes none - or lensfun-only profiles without a crop). Unknown is not a refusal,
  the `--light` rule.
- **Without `--raw`/`--lens` nothing changes**: same text, same golden files, the 15
  `crosscheck.py` structure scenarios untouched. `--help` moved, and was re-approved.

Measured on the library (2026-09-18, `--raw` on the first raw of each `flats/`; the
figures are in the README's table): tablet 7 and 15 sheets +0.34 / +0.33 against the
nearest (lensfun D7100 entry, 0.91) - REFA, exit 3; the sky master -0.39 against Adobe
0.66 - REFA; monitor close 0.00 against lensfun 7D2 - PERFECT; on-monitor -0.07 - BUN;
Nikkor 35 DX +0.11 / +0.10 - BUN; Samyang 135 f/2 by range (see `LensIdentification`)
+0.11 / +0.10 against the Nikkor 135 f/2 DC's Adobe file, +0.15 against its own lensfun
entry - BUN; Samyang at f/2.8 +0.01 - PERFECT. Pinned by `VignettingComparisonTests`,
the profile half of `StructureReportTests`, the goldens `structure_with_profiles`
(k = 0.6 fixture: BUN on shape, REFA on depth, exit 3) and `structure_lens_no_fstop`, and
in the window `StructureTabTests` with a fabricated raw plus two smoke renders.

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

### 3. End-to-end - `flatcheck light.fit --light`, or `flatcheck process/ --light`

The only mode that closes the loop; 1 and 2 are proxies. Input is a light frame
or stack **calibrated with the flat but with no background extraction**, or a
**directory**, which runs the whole series (see "Series mode" below).

**But it does not apply to every dataset, and the mode now refuses rather than
guessing.** Read "The validity limit" below before touching anything here - it is the
single most important thing about this mode, and it was missing until a real session
made the tool report a confident false positive.

`TileBackground` takes the 15th percentile inside each tile (a low percentile is
robust to stars and nebulosity, which only add signal), giving a local background
estimate. It then fits, simultaneously:

```
bg = a + b·X + c·Y + d·R² + e·R⁴          X, Y normalised by the half-diagonal
```

The plane (`b`, `c`) absorbs the light-pollution gradient; the radial terms
(`d`, `e`) capture residual vignetting, i.e. flat error. Fitting both at once is
what makes the test work under a polluted sky - the two have different shapes and
separate cleanly. The source carries an explicit warning: **do not** substitute
percentiles on circular annuli, because p20 on a ring picks up the dark side of
the sky gradient and reports it as false vignetting.

Four iterations of one-sided sigma-clipping (`res < 2.5σ`, σ from the MAD ×
1.4826) reject tiles containing nebulosity. One-sided is deliberate: contaminants
are positive.

The reported figure is `radPct = -(d + e)/a × 100` (R² = 1 at the corner). **The
sign is the diagnosis:**

- `radPct > 0` - background **falls** toward the edges → **under-correction**
  (different aperture at flats than at lights, or contaminated flat).
- `radPct < 0` - background **rises** toward the edges → **over-correction**,
  almost always a missing/wrong flat-dark: you divided by a flat that still
  contains an offset.

#### The validity limit

The plane/radial split works **only while the light-pollution dome is quasi-linear
across the frame**. Over a wide field the dome *curves inside the frame*, and the
radially symmetric part of that curvature has exactly the shape of vignetting. Two
things with the same shape cannot be separated by a fit, however well written. This is
a geometric degeneracy, not an implementation limit - do not try to fix it with a
better fit.

It was measured, not assumed. On `Milkyway-Core/2026-08-12-Smida` (Tokina 20 mm f/2.8
on a D3300, ~70° diagonal, Milky Way core low in the sky), a single frame reported
`+3.13 % SUSPECT, under-correction`. Across all 32 calibrated subs of that session the
same figure swept smoothly from `+3.13 %` through `-19.20 %` to `+35.73 %` in 81
minutes, with only 1.36 points between consecutive frames. Correlation between the
reported radial component and the sky gradient: **-0.94**, R² = 0.88. Eighty-eight
percent of what the tool called "flat" was sky.

`LightSuitability.Check` therefore gates the mode, deriving the diagonal field of view
in layers, most trusted first:

| source | when |
|---|---|
| `--fov <deg>` | stated by the user; beats everything |
| WCS `CDELT1` / `CD1_1` | a plate solve is present; measured, not deduced |
| `XPIXSZ` + `FOCALLEN` (+ binning) | astro cameras; the exact field |
| `FOCALLEN` alone | DSLRs - Siril does **not** write `XPIXSZ` for them |
| nothing | **runs anyway**, with a note |

Levels: `< Th.LightFovOk` (20°) `Ok`, up to `Th.LightFovMax` (40°) `Marginal` (runs, with
a warning), above that `TooWide` - **refused**, exit 1, no figure produced at all.

Two decisions here are load-bearing and easy to break:

- **Unknown is not a refusal.** If a missing header meant "too wide", every synthetic
  fixture in the test suite - and every minimal FITS - would become unanalysable, and the
  guard would do more harm than the problem it fixes. Pinned by
  `AnEmptyHeaderRunsInsteadOfRefusing`.
- **The focal-only fallback reports no field figure.** The field depends on the sensor,
  which is unknown at that point, so printing a number would be inventing one. It grades
  the focal length directly against thresholds computed for the largest plausible sensor
  (full frame, 43.27 mm diagonal), which is conservative: on a smaller sensor a good frame
  can come out `Marginal`. That is what `--fov` is for. Pinned by
  `TheFocalOnlyFallbackIsGradedOnTheFocalLength`.

`--fov` is not a force flag. It tells the tool the truth; it does not ask it to ignore
the physics, and a wide field declared by hand is refused exactly like a deduced one.

#### Series mode - `flatcheck process/ --light`

A single frame's `radPct` is the sum of two things: the flat error, which is **the same
in every frame by construction** (the same master flat was divided out of each), and a
sky radial term, which changes through the session. So the series carries a discriminator
the single frame does not.

`LightSeriesRun.Execute` loads each matching file in turn - never more than one full
frame in memory, since a 24 MP frame runs to hundreds of MB - runs the unchanged
`LightAnalyzer.Analyze` on it, streams a table row, and hands the results to
`LightSeriesAnalyzer.Combine`, which is pure.

Of the five runs it is the only one that had anything worth duplicating - a loop, with
state, progress and a memory trick that is not visible from the signature. It therefore
has two outputs rather than one: rows go into the caller's `TextWriter` as frames finish
(so the console does not sit silent for two minutes), and the same frame goes out through
an optional `onFrame` callback (so the window fills its table live). Cancellation is
checked *between* frames; a frame already started runs to the end, which at ~0.5 s each
is a fine response time.

The verdict is given **only if all three conditions hold**:

| condition | threshold |
|---|---|
| frame count | ≥ `Th.LightSeriesMinFrames` (5) |
| standard error of the mean (`σ/√n`, ddof=1) | < `Th.LightSeriesSemMax` (2.0) |
| \|Pearson(radPct, planePct)\| | < `Th.LightSeriesSkyCorr` (0.70) |

Otherwise the verdict is **`NEDETERMINAT`** and the report says which condition failed
and why. Three details that are not obvious:

- The correlation test is **skipped** unless the sky gradient actually varies across the
  series (`Th.LightSeriesSkySpanMin`, 10 points). On a clean session where it sits
  constant, the correlation is noise and would reject good data.
- The comparisons are written `!(x < threshold)`, not `(x >= threshold)`, so **NaN falls
  on the bad side** - same convention as `Stats.Verdict`. A one-frame series has no
  spread, `sem` is NaN, and it must not pass silently.
- `planePct` being large is **informative only**, never a gate. The plane is an indirect
  proxy - the real cause of non-separation is curvature - and a badly calibrated threshold
  there would reject good data. The field-of-view guard covers the cause.

**What was tried and rejected:** extrapolating to zero sky gradient. Regressing
`radPct ~ planePct` over the real session gives an intercept of `+122 %` - an
extrapolation far outside the data, whose gradient never goes near zero (it spans 49-88 %).
The correlation is a good diagnostic and a useless estimator; do not turn it into one.

**File selection.** `SeriesFinder.Find` enumerates the directory and matches the pattern
**in-process**, rather than `Directory.GetFiles(dir, pattern)`: Win32 matching still
carries the 8.3 legacy where `*.fit` also catches `.fits`. Sorting is ordinal on the file
name (`StringComparer.Ordinal` / Python `sorted()`) - if the two implementations listed
frames in different orders, `crosscheck.py` would report differences that do not exist.
The glob supports `*` and `?` only; character classes are literal in both, so `fnmatch`
is deliberately not used on the Python side. The WPF app reuses the same matcher for the
names it accepts, so `pp_flat_stacked*.fit` there means exactly what `--pattern` means
here.

The default pattern `pp_light_*.fit` excludes `r_pp_light_*` because the glob is anchored
at the start, and that is correct rather than incidental: registered frames are
geometrically transformed, so the sensor-fixed flat error is shifted relative to the
centre the radial model is fitted around. Including them would corrupt the measurement
silently.

Thresholds are all in `Th` (`LightRadOk`/`LightRadBad`, the `LightFov*`/`LightFocal*`
guard, and the `LightSeries*` family).

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

### 5. Histogram equalisation - `flatcheck flat.NEF --histeq [-o poza.png]`

Not a test. The frame rendered the way Siril 1.4.4's "Histogram" display mode renders it,
so that patterns invisible in a linear view can be seen - and, on the window's tab, so
that a frame can be put beside the master it went into. No verdict, no threshold, no `Th`
entry, exit code 0 when the picture was written. **It never votes**, and the report and
the tab both say what it cannot show.

**What Siril does**, read in `src/gui/image_display.c` (`remap()`, the `HISTEQ_DISPLAY`
branch) and `src/gui/histogram.c` (`computeHisto()`) at tag `1.4.4`; the quoted code is
in `docs/siril-histeq-source.md`:

1. One histogram **per layer**, 65536 bins, pixels equal to zero skipped. The RGB view is
   composited afterwards from the per-layer 8-bit buffers, so on a debayered image each
   colour is equalised on its own CDF; on an undebayered file (a raw opened as CFA, the
   default) the mosaic is one layer.
2. `index[i] = round_to_BYTE(cumulative(i) / nb_pixels * 255)`, cumulative *including*
   bin `i`, `index[0] = 0`, `nb_pixels` = all pixels, zeros included - so a frame with
   many dead pixels never reaches 255. Float data is first quantised as `round(v * 65535)`.
3. The lo/hi cutoff sliders are disabled in that mode.

**What we do the same, and what differs.** `HistEqLut.Build` mirrors 1 and 2, including
the `(byte)(x + 0.5)` rounding (`TheLutIsTheInclusiveCdfTimes255_RoundedLikeSiril` pins a
hand-computed vector: `{1, 2, 2, 3}` -> `64, 191, 255`). Bins sit on the channel's own
`[min, max]` of non-zero values rather than on 0..65535, because our rasters are doubles at
whatever scale the file had; on integer data the mapping is provably identical (no two ADU
values share a bin, and the CDF at a value is the count at or below it either way), on
float it is finer. Equalisation runs at full resolution per channel, and the 8-bit result is
then shrunk with `Img.Prepare`'s `k` rule (`HistEqAnalyzer.ShrinkFactor`) - so a D3300
channel lands at 1500 x 1000, the residual map's size. Block-meaning *before* equalising
would give a stronger stretch (noise averaged out before the CDF) but is not what Siril
shows; deliberately not built.

**Channels.** The CFA decision is `Mosaic.LooksCfa` on the four position medians, the same
rule as `--expo`. On a CFA frame the four positions are equalised separately and the result
carries: `CompositeRgb` (R from the position that *is* red, G the mean of the two greens, B
from blue - looked up by `CfaPattern` colour, never by position, and the mean of the four
greys when the order is unknown), one grey per position, and `Mosaic`, the whole frame as
one layer shrunk by `2k` so it comes out the same size. On mono: one channel, `Mosaic` null.

**Two resolutions, on purpose.** Every raster exists twice: shrunk (`Equalised`, `Mosaic`,
`CompositeRgb`, the `Img.Prepare` rule) for the console's PNG and the tests, and at full
resolution (`Full`, `MosaicFull`, `CompositeFullRgb`) for the window, which shows the latter
directly as a bitmap so that 100 % is one sensor pixel and the `Mozaic` view shows the R/G/B
chequerboard exactly as Siril does. The shrunk ones are derived from the full ones
(`TheShrunkRastersAreTheBlockMeansOfTheFullOnes`), never recomputed. A first cut showed the
shrunk rasters in the window too, through PNG, and the user asked what zooming into
block means was for; nothing. The window's default view is `Mozaic` - the one that matches
Siril on an undebayered raw - while the console keeps writing the composite; the two do
not have to agree, since the window saves no file here.

**Why it shows what linear hides, and what it destroys.** A flat is a narrow distribution
relative to the file's scale, so the CDF is steep across the vignetting range. Measured on
a synthetic with 5 % vignetting: a 0.3 % ripple is a third of a grey level in the linear
view and ~15 levels equalised (`ATinyRippleInvisibleInTheLinearViewBecomesManyGreyLevels`).
The price is that amplitude is gone by construction - a 0.5 % and a 10 % pattern of the
same shape render identically, and a clean flat shows its own noise at full contrast. That
is `HistEqReport.Caveat()`, printed by the console and shown under the picture. Note the
comparison the test makes is against the linear view *on the file's scale*, not against a
min..max stretch: on a field whose distribution is nearly uniform across its range,
equalisation *is* that stretch; what the mode adds over it is adaptivity, and that is a
property of real, noisy flats rather than of the synthetic.

**The master beside a frame.** The window's list puts the session's `pp_flat_stacked.fit` first.
A shape on a frame and not on the master was transient and averaged out; a shape on both
is fixed to the setup and is what the Structure numbers measure. Because the transform is
rank-based, the master's lower noise makes a shared pattern look *sharper* there, not
fainter (`HistEqReport.MasterVersusFrame()`).

**Cost, measured** (Release, D3300 24 MP, same files as the modes beside it): the float
master takes ~2-3 s and 690 MB peak against 3.3 s / 611 MB for structure; the NEF ~2-3 s
and 516 MB against 3.2 s / 1.1 GB for `--expo`. Keeping the full-resolution rasters for
the window costs ~18 MB of that. Loading dominates. Two things were expensive in
the first cut and are documented where they were fixed: the four position medians for the
CFA decision were sorted copies of 6 M values (~2 s) and now come off the 65536-bin
histogram (`HistEqLut.Median`, within a bin of exact), and the shrink went through `double`
rasters (1.1 GB peak on the master) and is now a byte block mean with the same arithmetic.
The console's PNG on that master: 150-380 ms and 3.6 MB for the shrunk RGB composite -
equalised noise compresses badly, so the file is several times the residual map's. The
window encodes nothing (see `RasterBitmaps`).

No Python counterpart and no `crosscheck.py` scenario, by decision: there is no number to
compare.

### 6. Smoothed flat - `flatcheck flat.fit --smooth netezit.fit [--scale 300]`

Not a test either: it **writes a second master**, the input with everything smooth kept
and everything else removed - the diffuser's texture, dust shadows, compact blotches,
noise. It exists for a flat shot through fabric on a monitor, whose texture reaches the
lights as a blanket of cells at a few percent. `docs/smooth-validation.md` is the full
record; the short version:

- **Why not a radial synthetic flat.** It would drop the texture and also everything the
  lens does besides vignetting, and on the Tokina 20 the four corners differ by 6-16 %.
- **What it does**, per Bayer position (mono: the whole frame): tile medians on ~150
  sensor px with partial tiles kept (`Img.TileMedians`); a local **quadratic** fit at every
  tile over its Gaussian neighbourhood, sigma = `ScalePx` (**300 sensor px**, the one
  knob); 3 rounds of two-sided clipping at `max(2.5 MAD, 2 % of the level)`; per pixel,
  the bilinear blend of the four surrounding tiles' quadratic predictions; `Mosaic.Merge`.
- **No radial model, deliberately.** Two designs with one failed on the real masters and
  the analyzer's header says how: a global polynomial over the residual cannot follow the
  mid-scale structure every master has (monitor blotches at 1000-1500 px, a decentred
  ring), and a ring-median model **puts rings into the file** - beyond the inscribed
  circle the median jumps ring to ring (the artefact `RadialModel` documents), and a
  150-px field cannot absorb 22-px rings. Nothing in the method knows a centre, so rings
  are impossible by construction. Don't reintroduce one.
- **The clip floor is load-bearing.** Relative to the MAD alone, 8 % of a very clean
  master's tiles (Samyang, tile RMS 0.06 %) were rejected and the smooth 0.5 % structure
  with them. `ClipFloor = 0.02` of the level fixes it; the four clean masters now reject
  nothing, the textured one rejects its dust, its bright spot and its deepest cells.
- **What it reports** is *what was removed* - `Prepare(input) / Prepare(smoothed)`: p1/p99
  per pixel, `LargeScalePtp`, `MaxTileDeviation`, `LocalSpots` on it - plus the structure
  battery before and after (`AnalyzePrepared` on both prepared rasters). No verdict; a
  warning when the removed component's worst tile reaches `Th.SmoothKeptWarn` (2 %).
- **Measured on five real masters** (the numbers in `docs/smooth-validation.md` and in the analyzer):
  every verdict and vignetting depth unchanged, the five tests within half a point;
  removed 0.12-0.39 % ptp on the four clean masters, 3.7 % on the textured one, whose
  compact spot goes from +6.19 % to +0.62 %; rings ±0.02 %; CFA levels within 0.1 %; 4-5 s
  per 24 MP master. On a synthetic with known truth the floor is 0.19 % over the whole
  frame, corners included. What the cells leave behind (~30 % in rms at their own
  correlation length) is fundamental to any finite kernel; the scale trades it against
  real 300-px structure (the Nikkor's ring is partly taken at 300, kept at 150).
- **What it cannot know:** lens versus source. A monitor's blotches and a sky gradient
  stay, because they are smooth; only `--ratio` separates them. Dust may be wanted (a
  same-session flat). The report and the tab say both.
- **Refuses** a raw (a single uncalibrated frame; `--expo` is for raws, and `Cards` is
  empty there) and a colour cube (`Planes > 1`: the reader averages the planes and the
  file written back would be mono). Exit 0 when the file was written, 1 otherwise - the
  `--histeq` rule; it does not go through `ExitFor`. Refuses to overwrite its input.
- **Output:** the input's type and scale, the input's cards verbatim (`BAYERPAT` included,
  so Siril treats it as the CFA master it is), two `HISTORY` lines. The console writes the
  two maps at `-o` and `-dupa` (`FlatCheckCli.AfterMapPath`): the removed component, and
  the smoothed flat's own residual. Siril's scripts name `pp_flat_stacked` explicitly and
  will not pick the file up by themselves; the report says so.
- **The scale can be proposed** (`--scale auto`, the tab's `Propune scara`), by
  `SmoothScaleAdvisor`. `Metrics.FineTexture` on the prepared input - the structured part
  of the component under ~128 sensor px (a double box blur of `W/47`, so the same fraction
  of the frame on a fixture as on the sensor; a fixed 32 px leaked vignetting curvature
  on a 256-px fixture), pixel noise subtracted in quadrature - says whether there is
  texture at all: `Th.SmoothTextureMin` 0.20 % against 0.04-0.06 % on the four clean
  masters and 0.69 % on the textured one. If there is, one smoothing per rung of the
  three-chip ladder; a rung is acceptable when the output's texture is under
  `Th.SmoothTextureGone` (0.05 %: 0.09 at 150, 0.02 at 300 on the textured master) and
  neither the **azimuthal asymmetry** nor the **tilt** dropped by more than
  `Th.SmoothScaleLoss` (0.5 points) - those two are the tests the texture does not touch,
  whereas corners, residual and worst tile *drop when the texture leaves* (1.47 points at
  the worst tile) and cannot be guards; the removed component's own large-scale figure
  was tried and missed the Nikkor's ring at 600 px (0.24 %) where the azimuthal drop was
  unambiguous (-0.79). The smallest acceptable rung is proposed; none means
  `NEDETERMINAT`, no texture means `NIMIC DE NETEZIT`, and in both cases the console
  writes no file and exits 2. On the five masters: 300 on the textured one, "nothing"
  on the four others. It proposes, never decides: lens versus source is still invisible,
  and a real feature only in the corners escapes both guards - the report says so. The
  window's button waits for the opening chain like the other one, selects the chip, and
  writes nothing.

No Python counterpart and no `crosscheck.py` scenario: the product is a file. The method
was chosen and validated with a numpy prototype that reproduced these figures; the C#
port matches it to the second decimal on the five masters, and the prototype was not
kept.

### 7. Synthetic flat - `flatcheck ref.fit --synthetic out.fit --lens <text>`, `--lenses`, `--against`

Not a test: it **writes a radial flat from the lens's published vignetting profile** -
the lens's model, not a measurement of the user's copy - and `--against` reads how much
and in what shape a real flat differs from it (or from any other flat). Built after the
Tokina 20 mm session showed that no illumination available here gives the vignetting
depth to better than half a stop: sky 0.27 stops at the corner (the dome brightens
toward the horizon all around the zenith and fills the corners), monitor up close
0.78-0.85 (the panel's angular falloff), the two public databases 0.66 (Adobe) and 0.83
(lensfun). `docs/lens-profiles.md` is the evidence file.

**Two sources, one list, both read by our own code** (no new package - `System.Xml.Linq`):

- **Adobe LCP**, from the machine only, never copied:
  `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0\**` and
  `%APPDATA%\Adobe\CameraRaw\LensProfiles`; `--profiles <dir>` replaces both (the tests run
  on hand-written XML that way, `LensProfileTestData`). The model, from the "Adobe Camera
  Model" document (Vignette Model, p. 8; descriptors, p. 18): `V = 1 + a1 r² + a2 r⁴ + a3 r⁶`,
  `r² = ((u-u0)/fx)² + ((v-v0)/fy)²` with `FocalLengthX = fx / Dmax`, `ImageXCenter = u0 / Dmax`,
  and when `FocalLengthX` is absent the nominal focal length in mm - so with the sensor's
  long side at `36 / SensorFormatFactor` mm, `fx / Dmax = f · SFF / 36`: **r is the distance
  in mm over the focal length in mm.** Cross-checked inside the same file: the
  `VignetteModelPiecewiseParam` tables end at r = 0.7094, which is the DX corner
  (14.1 mm / 20 mm), where the table reads 0.603 and the polynomial 0.597. "These model
  parameters are identical for all color channels" - the model is achromatic, and the
  report says so. Aperture is APEX (`f = 2^(Av/2)`). Some profiles are decentred
  (`ImageXCenter` 0.4938 on the 14-20 f/2) and the model honours it.
- **lensfun**, the `pa` model: same polynomial, r normalised to the half-diagonal of a
  36x24 frame divided by the lens entry's `<cropfactor>` (`mod-color.cpp`:
  `hugin_scale_in_millimeters = hypot(36,24)/CropFactor/2`, then rescaled to focal units
  by `(f / that)^(2,4,6)`; `modifier.cpp:79` normalises everything to focal units). So it
  needs the user's camera crop, taken from lensfun's own `<camera>` entries by `INSTRUME`
  (D3300: 1.523) or from `--crop`; on a body with a different crop the corner can sit past
  r = 1 (1.06 here) and the report says "extrapolare mica". The polynomial *is* the
  vignetting factor in both (lensfun multiplies to apply, divides to correct).
- **The camera's crop sets the frame's physical size for Adobe too.** The first cut took
  the profile's `SensorFormatFactor` - the calibration body - for every camera: 1.5 % off
  on a D3300 with the DX file (1.523 vs 1.5; the Adobe synthetic read 0.69 stops instead of
  0.67), the whole corner off on a full frame with a DX-calibrated file, silently. Now
  everything goes through mm (`36 / crop` for your frame, `36 / SFF` for the profile's
  focal-in-pixels and its decentring, see `VignettingModel`'s header), the SFF is the
  fallback when the crop is unknown and the report says so, a crop more than 3 % from the
  SFF prints "profil calibrat pe alt format", and `SyntheticFlatResult.Extrapolation`
  (corner r over `CalibrationCornerRadius`) prints "extrapolare MARE" above 1.1 - a
  sixth-order polynomial 50 % past its data goes negative, so a DX file on a full frame is
  refused in words, not numbers - all of it in one sentence, `SyntheticReport.FormatMatch`
  ("profil calibrat pe APS-C (crop 1.5); senzorul tau APS-C (1.523): acelasi format",
  or "alt format, raza se recalculeaza prin mm; coltul tau cade la 1.50 x coltul de
  calibrare: extrapolare MARE"), printed by the console at "format" and shown by the tab
  when a lens is picked and again under the written depths. Adobe's own piecewise table ends at 14.19 mm, the corner
  of a 23.64 mm sensor (crop 1.523), which is the evidence the real crop is what Adobe
  used. `SyntheticRun.PreferCamera` picks a per-body Adobe file ("NIKON D750 (lens)") over
  the mount's generic one when the reference names that body, since those carry the
  body's SFF. Console and window agree, and the tab's chips are only for a body the
  lensfun table lacks: the note under the fields says so and the chosen chip's crop lands
  in the box.

**Decisions.** Nearest profile, reported, no interpolation: nearest focal, then nearest
f-stop in stops, then the farthest distance (flats are at infinity); the report prints
"cerut" and "profil folosit". The same lens exists per mount in Adobe, so the mount is
chosen by the reference frame's camera maker word (`INSTRUME` / libraw) and an exact
name match beats partial ones; what still ties is listed and the exit code is 2. Focal
from `FOCALLEN` or libraw, f-stop from libraw only (Siril writes no f-number) or
`--fstop`; `LoadedFrame` grew `FocalMm`, `FNumber`, `Camera`, `NikonLensId` for it, filled
by both readers (`FitsReader.LoadHeader` reads the header alone, for the window's prefill
on a 96 MB master). libraw gives no lens *name* on a D3300 with a third-party lens, but it
gives the Nikon lens ID (139 for the Tokina 11-20), which Adobe's profiles carry in
`AlternateLensIDs`, and - in `LensInfo.MakerNotes`, where the top-level `MinFocal` /
`MaxFocal` are zero - the lens's range, 11-20 mm f/2.8. **The ID alone is not enough**: 139
is also the Nikkor 18-200 f/3.5-5.6 and the Sigma 18-35 f/1.8, and the first cut took the
alphabetically first hit, a Nikkor, for a raw shot with the Tokina. `LensSpec.Parse` reads
the range out of a lens *name* ("11-20mm F2.8", "18-200mm f3.5-5.6G", "135mm f/2.0";
nothing out of "Zeiss Batis 2/25", which then contradicts nothing) and
`LensCatalog.Narrow` drops the hits that contradict the raw (1 mm or 5 % on a focal, a
quarter stop on the aperture). Only Adobe files carry Nikon IDs, so the lensfun lenses of
the same maker(s) as the surviving hits, narrowed by the same range, join the candidates -
without that the narrowed list showed the Adobe half only and the user asked where the
lensfun Tokina had gone. `SyntheticRun.CandidatesFromRaw` is the one place both
applications start from; the window then preselects a single survivor and, when several
survive (the two Adobe generations of the Tokina 11-20 and its two lensfun entries),
narrows the list to them and says so (`OfferCandidates`) rather than picking one. For
this lens the choice is moot: the atx-i 11-20 F2.8 CF (2020) is the AT-X 11-20 F2.8 PRO
DX (2015) in a new barrel, Adobe's two files carry byte-identical vignetting parameters
(checked), and the raw's primary ID 139 is the atx-i file's `LensID` while the AT-X file
has 122 with 139 as an alternate - so the user's copy is the atx-i. Output: float, 1.0 on the
axis, the reference frame's cards minus the structural ones (`BAYERPAT` included, so Siril
takes it as a CFA master), `HISTORY` naming source, lens, profile and model; nothing
renormalised. Exit 0 written, 1 refused (cube, or focal / f-stop / crop unknown), 2 the
lens could not be chosen. `--lenses` lists lenses with focals and apertures only when the
filtered list is at most `SyntheticReport.ListDetailsMax` (40) long, because the details
need the Adobe file parsed.

**`--against`** (`AgainstAnalyzer`, `AgainstRun`, `AgainstReport`): both frames prepared,
each normalised to median 1, and **three** structure analyses - A, B and the ratio A / B -
printed as a table of three columns (`AgainstReport.Rows`: vignetting in stops, illumination
offset, local spot, then the five tests with their verdicts). The first version showed the
battery on the ratio alone, and "vignetare -0.42 stopuri" could not be read without knowing
that the master has 0.27 and the synthetic 0.69 (0.66 since the crop correction); the user asked for the three values side by
side. In the third column the figures are about the *difference*: the vignetting is the
depth difference (positive = A falls more toward the corner), the five tests say whether the
difference is radial, the map shows its shape. The rows carry their own notes
(`AgainstReport.TestNote`), written about the difference - the Structure tab's notes said
"a property of the lens, not an error" under a row that here *is* the error. Not `--ratio`,
which separates camera-fixed from world-fixed by rotation.

**The verdict of the difference has two parts, and the exit code follows the worse.** The
shape is the five tests on the ratio. The depth is the vignetting difference in stops, and
it got a verdict after the sky master - 0.42 stops shallower than the model, "clearly
enormous" to the user - came out ACCEPTABIL on shape alone. No new threshold: a difference
of D stops leaves `2^|D| - 1` of error in the corner of a light calibrated with the wrong
flat (`AgainstResult.DepthErrorOf`), a fractional amplitude like every other figure, so it
goes through `Stats.ResidualVerdict` unchanged - under 0.03 stops PERFECT, 0.14 BUN, 0.26
ACCEPTABIL, else REFA (`AgainstReport.DepthScale`, derived from `Th.Resid*` by
`StopsFor`, never written out). On the Tokina masters (2026-09-18, Adobe at crop 1.523):
sky -0.39 → 31.5 % REFA, close monitor +0.19 → 13.8 % ACCEPTABIL, on-monitor +0.12 → 8.3 %
BUN, Adobe vs lensfun 0.18-0.25 → ACCEPTABIL; the row's note says that under ACCEPTABIL the
difference is within what the two databases disagree by (~0.2 stops). The depth row is the only one with a verdict in the
difference column - the five tests carry theirs on A and B, and "PERFECT next to 0.11 % on a
ratio" said nothing the shape verdict at the top does not. The golden `against_radial`
pair is 0.30 stops apart, hence REFA and exit 3 there. The window shows the same rows
through `CompareRow` (public): label and note on the left in the one elastic column, the
three values in shared-size `Auto` columns packed on the right - the first cut gave the
values the width and let the note run under them. The four depth rows of the written flat
carry notes too (`SyntheticReport.DepthRows`; the console prints label and value only),
through their own `DepthRowTemplate` in `SyntheticView.xaml` - label and wrapping note in
the left column, the value alone on the right - because the shared `MetricRowTemplate`
runs the note under the value, which is what the structure tabs want and this one does not.

**Measured on the Tokina masters against the Adobe synthetic** (corner, stops, 2026-09-18
with the camera's crop): the close-monitor master +0.19, the sky master -0.39, the
on-monitor master +0.12; Adobe against the lensfun 7D2 entry -0.18 with a PERFECT ratio
(both radial), against the D7100 entry -0.25. These are the acceptance numbers of the mode,
and they say which real master was wrong (the sky one, by 0.4 stops) and by how much the
panel adds at mid-field (0.1).

**What the report says it cannot contain:** the chromatic part of the falloff (on the sky
master R falls ~25 % deeper than B), body-specific shading, dust, any non-radial
structure, the user's own copy; and that the two databases differ by ~0.2 stops on the
same lens. Pinned by `VignettingModelTests` (the piecewise cross-check, the decentred
model, the lensfun normalisation at r = 1), the reader tests on hand-written XML, the
catalog tests, `SyntheticFlatTests` (a synthetic is PERFECT for the structure battery;
against itself it is PERFECT at zero stops), `SyntheticRunTests`, and the golden files
`synthetic_written`, `synthetic_lensfun`, `synthetic_ambiguous`, `lenses`, `against_radial`.

**The window's Sintetic tab** (`SyntheticTabViewModel`, `SyntheticView`): a searchable
list with a source chip and the mount per row, focal / f-stop / crop text boxes prefilled
from the master's header and from the first raw in `flats/` (after the opening chain, in
the background; a raw libraw cannot open leaves the f-stop empty and says so), the
sources panel **collapsed by default** (it is documentation, not a precondition: where the
Adobe profiles are, which lensfun copy is in use with its commit and date, how to replace
it - `SyntheticReport.AdobeWhere` / `LensfunWhere`, so console and window say the same),
the write button on the Netezire pattern (waits for idle, asks before overwriting, `Saved`
event, never the session's master: `SirilSession.SyntheticMasterPath`), and the comparison
- `AgainstRun` on the master and the file just written: the verdict, the three-column rows
and the map on a switchable scale. **The comparison follows the write by itself**
(`RunAsync` chains `CompareAsync(Report)`, which appends to the write's report instead of
starting a second banner) after a real write - not after a refused overwrite, whose old
file may be another lens's, and not after a write error. The first cut needed a second
click, and the screen after "Scrie" - depth table, no comparison - was reported as a bug.
"Compara cu masterul" stays for a file written in an earlier session. The catalog is located once, in the
background, on the first `Attach` (`Func<ProfileSources>` in the `MainViewModel`
constructor for the tests). `SyntheticTabTests` and two smoke renders pin it.

## Shared pipeline and algorithms

**`Prepare(data, targetLong = 1200)`** - `BlockMean(data, 2)` then
`BlockMean(img, k)` with `k = max(1, (int)(max(H,W) / 1200.0))`.

The 2×2 block-mean is what makes the tool work directly on an undebayered CFA
master: each 2×2 RGGB block contains exactly 1 R, 2 G, 1 B, so it is a *fixed*
linear combination and the radial profile survives intact. This is why you can
feed it `pp_flat_stacked.fit` straight out of Siril.

Note the integer truncation of `k`: the ~1200 px target is loose. A 6000×4000
D3300 frame becomes 3000×2000 after the 2×2, then `k = (int)(3000/1200) = 2` →
**1500×1000**, not 1200. Anything landing between 1200 and 2400 is left alone.
This matches Python exactly, so it is parity-correct - just don't be surprised.
`BlockMean` truncates partial blocks (`a.H / k`), dropping the last rows/columns.

**`Percentile(list, p)`** - sorts a copy and interpolates linearly between the two
neighbouring order statistics, matching `numpy.percentile`'s default. Returns 0 on
an empty list (numpy would raise - a deliberate divergence, but it means a
degenerate input yields a silent 0 rather than an error).

**`Solve(A, b, n)`** - Gauss-Jordan with partial pivoting; returns `null` if
singular (pivot < 1e-15). Used for both least-squares fits, which are set up as
**normal equations** (`AᵀA x = Aᵀb`) accumulated in-place rather than via a QR/SVD
`lstsq`. This is less numerically stable than numpy's `lstsq` but adequate for
6- and 5-parameter fits on well-conditioned normalised coordinates.

**`FitCenter(img)`** - fits a paraboloid to `log(I)` over
`[1, xn, yn, xn², yn², xn·yn]` on a strided subsample (`step = max(1, min(h,w)/200)`),
and returns `(cx, cy, tiltPct, vertexValid)`.

`tiltPct = 2·sinh(|b| + |c|)·100` is the peak-to-peak amplitude of the **linear** term
over the frame: `|b| + |c|` is its value at the corner (where `xn = yn = 1`), and `sinh`
converts exactly from log space to intensity instead of the linear approximation
`2(|b| + |c|)`. It is a fraction, so it is scale-invariant like every other metric, and
it recovers an injected gradient to within ~1%. The cross term `f` (astigmatism/saddle)
is deliberately left out: it is not a tilt, and the residual test catches it anyway.

The vertex comes from `M = [[2d, f], [f, 2e]]`. It falls back to the geometric centre -
with `vertexValid = false`, so the report can say `nedeterminat` instead of printing a
fake `0.00` - if the system is singular or the vertex lands outside the frame. If the
normal equations themselves are singular the tilt is `NaN`, which degrades to `REFA`
like every other NaN in the tool.

**`RadialModel(img, cx, cy, nbins = 80, pct = 50)`** - bins pixels by radius into
80 equal-width bins out to `rmax`, takes the median per bin (bins with < 8 pixels
are dropped), then rebuilds a 2-D model by linear interpolation of that profile.
`Interp` clamps at both ends, matching `np.interp`.

**It is fed `Img.Symmetrise(img)`, not `img`** - the mean of the raster and its 180°
rotation. The residual is still `img / model`, so the gradient the map exists to show is
untouched. This is a fix, not a refinement: beyond the inscribed circle a constant-radius
ring is only partial arcs, a gradient makes their pixel distribution bimodal, and the
median falls into the sparse gap between the two modes - so it jumps from bin to bin by
an amount set by the gradient. The interpolated model inherited the jumps and the map drew
them as **concentric rings plus a hard circular edge on the inscribed radius, on a frame
that contained neither**. Reproduced on a synthetic that is nothing but smooth vignetting
times an 11 % ramp; measured on it, the worst ring-to-ring step went from 2.47 % to 0.11 %.
The verdict numbers barely moved at the time (`LargeScalePtp` was then restricted to the
inscribed radius, where the median was already exact - the golden files shifted by
0.01-0.03 points and no band changed), but the map is the only place a *shape* is read,
and it was lying. Symmetrise
rather than switch the ring statistic to the mean: the mean cures it equally well but gives
up the median's robustness to stars, hot pixels and dust, and the analyzer accepts single
frames. Pinned by `RadialModel_OnAGradient_TheProfileJumpsUnlessTheRasterIsSymmetrised`
and `OnAGradient_TheResidualHasNoRingsOutToTheCorner`, both of which assert the *un*fixed
behaviour too, so removing the symmetrisation as redundant fails loudly.

**The same artefact has a second face, and symmetrising does not touch it.** Symmetrise
removes the *odd* part only. An *even* non-radial term - a saddle (`xy`) or a falloff
steeper along one axis than the other (`x² - y²`), which is what a twilight sky leaves in a
70° field and what a 180° rotation cannot cancel - goes through it untouched, and the median
on the left/right arcs beyond the inscribed circle absorbed it into the profile: the model's
slope kinked from 0.21 to 0.71 % per bin exactly on that circle and the map drew a crease
there, with two lens-shaped lobes bounded by sharp arcs. Found on the Tokina 20 sky master
(15 + 15 frames rotated 180°, a -6 % sky saddle at the corners), reproduced arc for arc on a
smooth synthetic with the fitted coefficients, which is what proved the arcs were the
model's: the error of the map against the true residual was 8.9 % outside the circle.

So on the partial rings the profile is now the median of `v / (1 + q_c r² cos 2θ + q_s r² sin 2θ)`,
where `q` is the *relative* 2θ amplitude per `r²` - a ratio, like every other figure in the
tool, since a sky term multiplies and grows with `r²`. `q` is estimated on each identifiable
partial ring by `Metrics.RingFit` - on the medians of 15° sectors, by least median of
squares, so a compact blotch that symmetrising turns into an antipodal pair (which *is* a 2θ
signal on its rings) cannot enter it - then smoothed with a running median over 7 rings; in
the corners, where the arcs can no longer separate a 2θ term from the constant (a purely
geometric criterion, `RingFitMinDet` on the correlation determinant of the basis), `q` is
carried from the last identifiable rings. Inside the circle nothing changed: the ring median
is exact on a full ring and immune to anything compact. `a0` stays a pixel median
everywhere, so the profile's noise and robustness are what they were.

Measured: on the sky master the sharpest step between 8-px tiles of the residual went
3.58 → 0.23 % on the synthetic and 2.71 → 0.44 % on the master; the true-residual error
on the synthetic 8.9 → 0.5 %; the four clean masters in the library moved by 0.0-0.3
points and no verdict changed; the monitor master with its blotches - genuine even
structure, -2 to -5 % on its partial rings, seen by two independent estimators - reads
2 points more on the worst tile, which is what the median had been hiding in the profile.
The golden files moved by 0.01-0.05 points. The header comment of `RadialModel` lists the
four designs that were measured and rejected on the way (a0 straight from a per-ring fit,
a global quadratic even correction, absolute amplitudes held from the last ring, a 4θ
term) and why each failed; read it before changing the method, and read
`docs/radial-model-even-term.md` for the full specification of `RingFit` (constants,
sector rule, LMedS, the smoothing across rings) with every measurement behind it. Pinned by
`RadialModel_OnASaddle_RecoversTheRadialPartOutToTheCorner` (the model against the same
scene without the saddle, under 1 % everywhere) and by
`ACompactBlotchInACorner_IsNoLongerReportedAsPerfect`, which is the test the rejected
designs failed. `flatcheck.py` mirrors it in full (`radial_model`, `_ring_fit`) and all 43
`crosscheck.py` scenarios still agree.

**A "vignetting edge" metric was prototyped on the same occasion and rejected; don't
build it.** The rings on the Nikkor ISO800 map had been read as a hard-edged, decentred
falloff, and a metric for hard edges in the radial profile (max deviation from a
Savitzky-Golay smoothing of the ring medians) read 0.03 % on ISO400 and 0.98 % on ISO800,
a clean 30x separation - which was the artefact above, not the flat: on a synthetic with
no edge at all it read 3.05 %, and from the ring *mean* it read 0.05 % on both masters.
With the symmetrised profile both real masters read 0.06 %, so there is no real positive
in the library. Its dynamic range was also too narrow to threshold (synthetics at
1504x1004: smooth 0.02, a soft 10 % knee 0.24, a 1 % hard step 0.38, a 3 % hard step
1.16), and small rasters sit above that margin by discretisation alone (64x64 fixture
0.80 %, 256x256 0.15 %). If a real hard edge ever turns up, that is the starting point;
until then the metric has nothing to measure.

A radial profile cannot be odd, so nothing the model is entitled to keep is lost. What is
lost is a half-pixel's worth of accuracy: `Rotate180` mirrors about `((H-1)/2, (W-1)/2)`
while the rings are measured about `(H/2, W/2)`, so a field radial about the latter is
perturbed by ~0.3 % on a 512² raster and ~2.8 % on 64², fading as ~1/n. Same cause as
known issue 5, negligible at analysis resolution, and pinned by
`Symmetrise_OnTheFixtureFieldCostsOnlyTheHalfPixelArtefact`.

**`AzimuthalAsymmetry(img, cx, cy, nsect = 12, nrings = 10)`** - 10 rings spanning
`0.15·r_ins` to `r_ins`, 12 angular sectors. Per ring: median of each sector, then
`(max - min)/median × 100`. Restricted to the **inscribed** radius because only
there is every azimuth sampled - out in the corners the comparison would be
invalid. Rings needing at least `nsect - 1` populated sectors.

**`LargeScalePtp(resid, ntiles = 40)`** - block-means the residual into ~40-wide tiles
over the **whole frame** and returns `p95 - p5`. The tile averaging is essential: noise
falls as `√(px/tile)` while structure survives. Without it the metric would measure noise,
not error.

It used to keep only the tiles inside the inscribed circle, because beyond it the radial
profile came from partial arcs and was not trusted. The symmetrisation above fixed exactly
that (`OnAGradient_TheResidualHasNoRingsOutToTheCorner` asserts the model is smooth out to
the corner), so the restriction had become a leftover with a real cost: on a 3:2 frame the
circle holds 53% of the tiles, the corners - where a gradient is largest and where light
leaks live - were invisible to the "most important" metric, and the same gradient read
6.5% along the long axis and 9.7% along the short one, because the circle's diameter is
the short side. Whole-frame it reads 10.4 / 10.6, close to the tilt's 12, and the floor on
a clean radial field stays under 0.1% at analysis resolution. Pinned by
`LargeScalePtp_ReadsAGradientTheSameAlongEitherAxis` and
`OnAThreeByTwoFrame_TheResidualReadsAGradientTheSameAlongEitherAxis`. `flatcheck.py`
mirrors it, and the golden files moved (0.24 → 0.29 on the clean fixture, 9.43 → 10.36 on
the gradient one, which crossed into `ACCEPTABIL` - the verdict there was already
`ACCEPTABIL` through the corners).

**`MaxTileDeviation(resid, ntiles = 40)`** - the same tile grid, `max |tile - 1|`, whole
frame. The fifth test; see "Why a fifth test" under mode 1. Floor 0.13-0.37% on clean
fields, 0.4% at 10% pixel noise.

**`LocalSpots(resid, ntiles = 80, win = 3)`** - finer tiles (80 across, ~18 px at analysis
resolution, so a 60 px dust shadow is not diluted), each minus the median of its 7x7
neighbourhood, and the largest |difference| wins, with sign and position. A gradient is
smooth so the neighbourhood median follows it (0.46% at a 12% ramp, 0.68% at 25%); a dust
shadow, ring or small leak stays whole (4.99% on a 5% shadow, 4.71% with a 12% ramp and 3%
noise on top). **Only tiles with a whole neighbourhood are evaluated** - a 3-tile border is
skipped - because at the edge the truncated neighbourhood's median no longer follows a
gradient and the difference comes from the gradient, not a spot: measured on the 256 px
golden fixture, -0.76% in the corner on a *clean* field and -1.61% under a 60% gradient,
one step from a false dust warning. A spot right on the edge is caught by the worst tile
anyway. Ties within 1e-12 keep the first tile in row-major order, in both
implementations: on a symmetric scene two tiles come out equal to the last bit and C# and
numpy sum in different orders, which is how `crosscheck.py` caught a position flip on the
diagonal fixture. Not a test; the warning threshold is `Th.LocalSpotWarn`.

**The sign is kept, and the report names it in words.** Negative means darker than the
surroundings (dust, dew, an obstruction), positive means brighter (a light leak, a
reflection). `StructureReport.LocalSpotValue` appends `(mai intunecat)` / `(mai luminos)`,
and says nothing at exactly zero, where a constant residual has no direction to state. The
row was printing a bare signed number, and the first question asked once the figure existed
was whether a minus meant a bug - which is the answer to whether the wording was needed.
`StructureReport.LocalSpotNote` carries the explanation and **states the threshold, read
from `Th.LocalSpotWarn` rather than written out**, so a recalibration cannot leave the text
quoting the old value; it is the only place the user learns where the figure starts to
matter. Both applications print that note, the window with the newlines flattened.

**What it can misread, measured on two real masters.** The metric finds the deepest
*compact* departure, but a large-scale feature with a ridge steeper than the 7x7
neighbourhood (~250 px on the sensor) also survives the high-pass. On
`Nikkor 35mm DX/f2.8-sky/ISO400` the extreme is -0.44 % and the five deepest tiles all sit
at `x=117` with different `y`: a vertical stripe down the left edge, i.e. the edge of the
frame's large dark region, not a spot. On the ISO800 master of the same lens the extreme is
`+0.36 %` in the top-right corner, the same kind of thing. Both are a fifth of the warning
threshold, so nothing fired and nothing should have - but on a master with a harder edge
this could warn about dust that is not there. The counter-measure if it ever does: check
whether the extreme is round or elongated before trusting it, since only the round case is
dust. Deliberately not automated - that needs a shape threshold nobody has calibrated, and
the figure is informative, not a verdict.

**Verdict ladder** - `Verdict(val, ok, bad)` → `OK` / `SUSPECT` / `PROBLEMA`;
`Rank` maps those to 0/1/2 and the mode verdict is the worst of its tests. Note
that `NaN` compares false against both bounds and therefore degrades to
**PROBLEMA** - which is how degenerate inputs (too few tiles, no valid rings)
surface. Matches Python.

## Thresholds

Every threshold lives in `Th`. Full picture:

| metric | mode | OK < | PROBLEMA ≥ | defined in |
|---|---|---|---|---|
| corner spread | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| azimuthal asymmetry (max) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| residual after radial model | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| tilt (linear term) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| worst tile (`pata maxima`) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| local spot (`pete locale`) | structure | 2% = warning only, never a verdict | | `Th.LocalSpotWarn` |
| vignetting vs the lens profile (nearest) | structure, `--raw`/`--lens` | *residual scale* through `2^|D| - 1`: 0.03 / 0.14 / 0.26 stops | | `Th.Resid*` via `Stats.DepthVerdict` |
| the five structure tests on `(A+B)/2` | ratio | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| world-fixed gradient per orientation | ratio | 1.0% | 3.0% | `Th.RatioOk/Bad` - informative label only |
| \|radial component\| | light | 2.0% | 5.0% | `Th.LightRadOk/Bad` |
| field of view (diagonal) | light | 20° | 40° = refused | `Th.LightFovOk/Max` |
| focal length, no pixel size | light | 120 mm | below 60 mm = refused | `Th.LightFocalOk/Min` |
| standard error of the mean | light, series | 2.0 pts | | `Th.LightSeriesSemMax` |
| \|corr(radPct, planePct)\| | light, series | 0.70 | | `Th.LightSeriesSkyCorr` |
| frame count | light, series | ≥ 5 | | `Th.LightSeriesMinFrames` |
| worst tile of the removed component | smooth | 2 % = warning only, never a verdict | | `Th.SmoothKeptWarn` |
| fine texture of the input | smooth, `--scale auto` | 0.20 % = "nothing to smooth" below | | `Th.SmoothTextureMin` |
| fine texture of the output | smooth, `--scale auto` | 0.05 % = texture gone below | | `Th.SmoothTextureGone` |
| drop in azimuthal asymmetry or tilt | smooth, `--scale auto` | 0.5 pts = scale too large above | | `Th.SmoothScaleLoss` |
| centre clipping | expo | 0.05% | - | `Th.ExpoClip` |
| centre level | expo | 50-65% | - | `Th.ExpoLo/Hi` |
| centre level (sky) | expo `--sky` | 15-50% | - | `Th.ExpoSkyLo/Hi` |

### The residual scale

The five structure deviation tests - corner spread, azimuthal asymmetry, residual after
the radial model, tilt, and the worst tile - all measure *the same thing* from different
angles: how much of the frame fails to be explained by a purely radial function. All five
are fractional amplitudes in percent, so they share one four-band scale rather than a
pass/fail threshold:

| band | verdict | meaning |
|---|---|---|
| < 2% | `PERFECT` | quality is perfect |
| 2-10% | `BUN` | usable; background extraction removes what is left |
| 10-20% | `ACCEPTABIL` | usable, but run `--light` to confirm before a hard stretch - if your field is narrow enough for it, see "The validity limit" |
| > 20% | `REFA` | reshoot the flats |

Boundaries are strict `<`, so each one falls into the worse band. `NaN` degrades to
`REFA`, same rule as `Verdict`.

**`reziduu dupa model rad.` is the one that matters most**, and the console output says
so inline. It measures the departure from the radial model directly, over the whole frame.
Corners, azimuthal and tilt are partial views of the same deviation: the corners catch it
only where the four boxes sit, the azimuthal test only inside the inscribed circle, and
the tilt only the part of it that is a plane. The worst tile is its complement: the
residual trims extremes, the worst tile *is* the extreme.

**The five do not read the same defect with the same number**, and that is by shape, not
by miscalibration. Measured on a 3:2 frame with a 12% gradient: tilt 12.0, corners 11.5,
residual 10.4, azimuthal 7.3 (its rings stop at the inscribed circle), worst tile 6.0 (a
per-pixel deviation, half the peak-to-peak). The verdict is the worst of the five, so a
gradient is carried by the tilt and the corners, a compact blotch by the worst tile, and
a broad blotch inside the circle by the residual and the azimuthal test.

**It is an upper bound on the flat error, not the flat error**, and the report used to
claim otherwise - the sentence said "adica exact eroarea de flat". What it measures is the
sum of two things with opposite consequences: a part fixed to the camera (the optics;
divides out of the lights correctly, harmless) and a part fixed in the world (the
illumination; does not reproduce, harmful). On a single master the two cannot be
separated - the information is not there. `--ratio` is what separates them, and
`WhyResidualMatters` now says so and points at it. This is why a lens with genuine
non-radial vignetting reads as "error" here and is not one: measured on this library, a
Nikkor 35 DX master carries 4.00% of corner spread of which only ~1.3% was the sky.

The structure mode prints the scale itself, under the heading
`SCARA celor cinci teste de abatere de mai sus`, so the numbers can be read without
consulting these docs.

The illumination-centre offset is **not** on this scale, and since 1.2.0 it is not a test
at all: it prints under `INFORMATIV (nu sunt teste)` with no verdict. It measures a
position, which is the wrong shape for this tool. The vertex is `-b/(2d)`, a
gradient-over-curvature ratio - not a fraction, and not scale-invariant. Measured, with
the same 11% gradient held fixed: 2.38% at 2 stops of vignetting, 9.49% at 0.5, 31.16% at
0.15, 57.40% at 0.08 - and then **0.00%** at 0.05, because the vertex leaves the frame and
the out-of-frame guard returns the sensor centre. So the metric was not merely
miscalibrated, it was **non-monotonic**: past that point the worse the defect, the cleaner
the report, which is a silent false negative on exactly the flattest lenses. The fixtures
never caught it because `FitsTestData.Radial` defaults to `k = 0.45`, i.e. 0.86 stops -
5.7x deeper than a real Samyang 135 at f/2.8.

`inclinare` is that same defect taken as an amplitude, which is what the scale wants, and
it still catches genuine decentring: a radial field centred off the sensor centre produces
a linear term of about `offset x curvature`, so the tilt weights the offset by what it
actually costs photometrically. On the real Samyang master that prompted the change (0.15
stops, an 11% twilight-sky gradient) the offset said 42.36% `PROBLEMA` and the mode said
`REFA`; the tilt says 12.36% `ACCEPTABIL`, in line with the other tests
(12.62 / 7.05 / 6.05 at the time). Pinned by `FitCenter_TiltIsInvariantToTheVignettingDepth`,
`FitCenter_WhenAFlatFieldMakesTheVertexEscape_TheTiltStillReadsTheGradient` and
`OnAFlatLensWithAGradient_TheTiltCarriesTheVerdict`.

`Stats.Severity` still spans both ladders (`OK`/`PERFECT` = 0, `BUN` = 1,
`SUSPECT`/`ACCEPTABIL` = 2, `PROBLEMA`/`REFA` = 3), and the exit code is derived across
all modes. Since the rewrite `--ratio` gives a **residual-scale** verdict, like structure,
because its verdict *is* a structure verdict; only `--light` still speaks
`OK`/`SUSPECT`/`PROBLEMA`. The `OK`/`SUSPECT`/`PROBLEMA` words survive in `--ratio` output
only as the informative label on the world-fixed component.

The `--ratio` mode has **no verdict thresholds of its own** since the rewrite: its verdict
is the structure verdict of the combined master, so it lives on this same scale. The
1%/3% pair survives only as a label on the world-fixed component, which is informative
and does not vote.

Since phase 8 every threshold lives in `Th` - `--light` no longer has 2.0/5.0 inline
and `--expo` no longer keeps its bands as locals. `Th.ExpoLo`/`Th.ExpoHi` used to be
dead *and wrong* (50/60 while the code used 50/65); they now hold the real values.
`Th.ExpoTarget` (57) and `Th.ExpoSkyTarget` (30) are the midpoints used for the
suggested-stops arithmetic.

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
legend line in the console would move every golden file *and* break `crosscheck.py`'s text
parity with `flatcheck.py` unless mirrored there - for a line a console user cannot act on.
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

## Parity with Python

The C# port was written to be numerically identical to `flatcheck.py`. **Treat
`flatcheck.py` as the numeric oracle when changing a formula**, and re-run the
cross-check afterwards:

```bash
FLATCHECK_DUMP_FIXTURES=/tmp/xc dotnet test FlatCheck.sln --filter DumpsFixtures
```

```bash
python crosscheck.py /tmp/xc
```

`crosscheck.py` runs both implementations over identical FITS across 43 scenarios and
compares twice: the full text, and every numeric token as a float. It also checks once,
up front, that both report the same version. It exits non-zero on any unexplained numeric
difference or on a version desync.

**Current status: 42 of 43 scenarios agree numerically** - 26 byte-identical, 16
differing only in wording. Coverage spans all four modes, all four bands of the residual
scale, both gradient directions, non-square frames, BITPIX 16/32/-32, a float master
divided by 65535 as Siril writes it (`structure float 0..1`, which pins the levels line
that used to print `0 / 0`), a dust shadow (`structure dust shadow`, which pins the local
spot warning and its position), the exposure bands either side of each boundary, and a CFA
frame carrying `BAYERPAT` (`expo cfa BAYERPAT`, byte-identical), which is what pins the
channel-naming line across the two implementations, and the same frame with
`ROWORDER = 'BOTTOM-UP'` (`expo cfa BOTTOM-UP`, byte-identical), which pins that both
move the phase by one row on an even height.

The attribution rewrite is mirrored in `flatcheck.py` in full - `_structure_body` split
out of `mode_structure` exactly as `StructureReport.WriteBody` was, plus
`split_by_rotation`, `world_map_path` and `_provenance`. Four `--ratio` scenarios cover it,
and they compare 32 numbers each. If you change a formula in `RatioAnalyzer`, change it
there too, or every one of them turns into a diff.

Intentional divergences:

- `--histeq` exists only in C#. No Python counterpart, no `crosscheck.py` scenario, no
  `CrossCheckFixtures` entry: the mode produces a picture, not a number to compare.
- `--smooth` exists only in C#, for the same reason: it produces a file.
- `--synthetic`, `--lenses` and `--against` exist only in C#: a file, a list, and the
  already-verified structure battery on a ratio.
- No 4-panel matplotlib figure (residual PNG only).
- No EXIF read beyond the pixels, so no `suggest_shutter(...)` shutter-speed
  suggestions - libraw hands over `shutter`, but nothing consumes it yet.
- `Percentile` on an empty list returns 0 instead of raising.
- The scale-source line reads `libraw (exact)` where Python reads `rawpy (exact)`. Same
  library; `rawpy` is its Python wrapper. For the same reason, on a raw the channel-order
  line reads `din libraw` against `din rawpy`. On a FITS both read `BAYERPAT`, so that
  line is identical - which is what the `expo cfa BAYERPAT` scenario checks.
- No per-channel histograms in the console; they exist only in the window, and so does
  their wording (`ExposureReport.HistogramReading` and friends have no console caller).
- `--sat` is echoed as `--sat=4095` rather than Python's `--sat=4095.0`.
- The `--light` interpretation capitalises the diagnosis - "Fondul CADE/CRESTE spre
  margini" against Python's lower case. The sign *is* the diagnosis, so the emphasis
  is deliberate. This accounts for the remaining wording-only differences in the four
  `--light` scenarios whose radial component is large enough to print a diagnosis.

**One numeric divergence, and the C# is the correct one.** On a *perfectly constant*
frame `FitCenter`'s quadratic coefficients are pure floating-point noise (~1e-15) and
the vertex determinant comes out at ~1.3e-29. C# guards with `|det| < 1e-15`, marks the
vertex unusable and prints `nedeterminat`. Python has no such guard: `np.linalg.solve`
does not raise on a matrix that is only *numerically* singular, so it returns garbage and
places the illumination peak at (136, 136) on a 256x256 raster, reporting a 6.35% offset.

Since 1.2.0 the difference is confined to the informative line: the offset no longer
votes, so both implementations reach the same verdict and Python's false positive changes
nothing. It used to be the difference between OK and SUSPECT.

This cannot occur on a real flat, which always has vignetting and therefore a
determinant far from zero. Do not "fix" the C# to match; the guard is what makes the
degenerate case behave. Pinned by
`FitCenter_OnAConstantImage_FallsBackToTheGeometricCentre`.

## Exit codes

Since phase 8 the tool is usable from a script:

| code | meaning |
|---|---|
| 0 | OK / PERFECT / BUN - usable as-is |
| 1 | error - missing file, bad argument, invalid FITS, failed fit, `--expo` on a file that isn't a raw flat, or `--light` on a field too wide for the test |
| 2 | SUSPECT / ACCEPTABIL / NEDETERMINAT (and `--expo`'s two "acceptable but..." bands) |
| 3 | PROBLEMA / REFA |

Verdict severity is monotonic, so "anything non-zero" means "not clean, go look".
`--histeq` has no verdict and does not go through `ExitFor`: 0 means the picture was
written, 1 that it could not be. `--smooth` likewise: 0 means the smoothed flat and its
maps were written, 1 that the input was refused (raw, cube) or the file could not be;
with `--scale auto`, 2 means no file was written because there was nothing to smooth or
the scale was undetermined - "read this before you decide", the meaning 2 has elsewhere.
`--synthetic` follows the same shape: 0 written, 1 refused (a colour cube, or a focal
length, f-stop or crop factor nobody could supply), 2 the lens could not be chosen from
the text (none, or several - the candidates are printed). `--lenses` is always 0.
`--against` goes through `ExitFor` on the verdict of the *difference*.

Since the `--ratio` rewrite, mode 2 exits on the residual-scale band of the combined
master rather than on `OK`/`SUSPECT`/`PROBLEMA` - which changed real outcomes: the ISO800
pair in the flats library went from `OK`/0 to `ACCEPTABIL`/2.

## Known issues and gotchas

Verified by running the current build unless marked otherwise. The CLI-contract and
correctness bugs that used to live here were fixed in phase 8; what remains is
behaviour that is either deliberate or not worth changing.

1. **Card parsing is naive.** It splits the value at the first `/`, which corrupts any
   string value containing a slash (e.g. a path or date in a `COMMENT`-adjacent card).
   Fine for the numeric keys actually used.
2. **File extension is only half-validated.** The six raw extensions dispatch to
   `RawReader`; *everything else* goes to `FitsReader.Load`, so a `.jpg` surfaces as a
   FITS parse error rather than "unrecognised format". Python raises
   `Format nerecunoscut` instead. Left as is deliberately: the error still points at the
   right file, and tightening it would change behaviour no test asks for.
3. **`CornerStats` can throw on tiny images.** With `h < 8`, `h/2 - bh` goes negative
   and indexing throws. Not reachable with real frames.
4. **`_crcTable` used to be lazily initialised**, and that was a real race, not a
   theoretical one: the field was assigned *before* the table was filled, so a second
   thread arriving in that window saw a non-null table of zeros and wrote a wrong CRC -
   a corrupt PNG. Reproduced: 8 threads, 2000 rounds, 5261 wrong CRCs out of 32000. In
   the window it was reachable, since the Ratio tab can encode while Structure is still
   running. It is now a `static readonly` built by a static method, which the CLR
   publishes only when complete; `EncodingOnManyThreadsGivesTheSameBytesAsSerially` pins
   the determinism. Kept in this list so nobody reintroduces the lazy form.
5. **The corner-spread metric has a non-zero floor.** The analysis centre is
   `(w/2, h/2)`, but pixel indices run `0..w-1`, whose true middle is `(w-1)/2`. That
   half-pixel offset puts the top-left corner fractionally farther from the centre than
   the bottom-right, so even a *perfectly* radial field reports a spread. It scales as
   ~1/n: measured 2.88% at 100 px, 1.44% at 200, 0.56% at 512, 0.28% at 1024
   (cross-checked against a numpy reimplementation). At the ~1500x1000 analysis raster
   the tool actually uses it is ~0.2%, far below the 2% PERFECT band, so it is
   harmless in practice - but it is a systematic floor, not noise, and it would swamp
   the threshold on a small enough frame. Pinned by
   `CornerStats_HasAHalfPixelCentringArtifact_ThatHalvesAsTheFrameDoubles`.
   **Not a bug to fix**: changing the centre convention would break parity with
   `flatcheck.py`.
6. **`RadialModel`'s `centers`/`prof` return values are discarded** by
   `StructureAnalyzer` - they fed the Python profile panel, which this port dropped.
7. **Memory is still heavy**, though phase 8 halved the worst of it: `Percentile` now
   has a `double[]` overload, so taking the full-resolution median no longer copies
   twice. Measured peak was ~330 MB on a 3000x2000 frame before that change; a 24 MP
   D3300 frame still runs into the high hundreds of MB. Runtime is fine
   (1.3 s structure / 0.6 s expo / 0.5 s light on 3000x2000).
8. **A raw whose only preview is full-size costs a decode of it.** The window's exposure
   tab decodes the camera preview; when a camera offers no mid-size one, the largest is
   decoded with `TargetSize`, which was measured at ~9 MB and ~0.5 s on a 6000x4000
   preview - the time is spent in entropy decoding regardless of the target. Every raw
   load also copies the chosen preview's bytes (a few MB at most), including modes that
   never look at them; that is the price of opening the file once.

## Versioning

Every run prints `flatcheck <version>` as its **first line** - every mode, `--help`, and
failed runs too, so a pasted report always says which version produced it. There is no
`--version` flag; it would be redundant.

Both applications print it from `RunReport.WriteBanner`, so the WPF app's saved
`Rezultat testare.txt` opens on the same two lines as a console run, and a bump moves both
at once.

The version lives in **two** places:

| where | what |
|---|---|
| `Directory.Build.props` | `<Version>` - the single source of truth for C# |
| `flatcheck.py` | `__version__` |

`Directory.Build.props`, not a csproj, and that changed in 2.0.0 for a concrete reason:
there are now three assemblies. `VersionInfo` reads the one it lives in - Core - but the
console exe and the WPF exe carry their own, which is what Windows shows in file
properties. Two places to edit means, sooner or later, two different values. So the
property sits above all of them and every project inherits it.
**Don't move `<Version>` or `<Copyright>` back into a csproj**; `VersionTests` checks that
no project overrides either.

`FlatCheck.Core/Version.cs` does **not** hardcode it. `VersionInfo.Value` reads the
assembly's `AssemblyInformationalVersion`, which MSBuild derives from `<Version>`, and
trims any `+sourcerevision` suffix. Don't reintroduce a literal there.

**The user bumps the version manually**, per feature or fix, choosing the increment
themselves. **Never bump it on your own initiative** - not as part of a change, not
"while you're in there". If a change seems to warrant a bump, say so and let them decide.

**Bumping the version breaks nothing.** That is deliberate, and worth preserving: the
banner is scrubbed to `flatcheck <VERSIUNE>` before any comparison, in both places that
compare whole output.

- `GoldenOutputTests.RunCli` scrubs it, the same way it already scrubs absolute paths -
  both change without the *behaviour* changing. So `Golden/*.txt` carry the placeholder
  and survive a bump untouched.
- `crosscheck.py` scrubs it in `norm()`, and checks version agreement once up front in
  `check_versions()`. Without that, a desync would turn all 43 scenarios into diffs -
  42 diffs to learn one thing.

What *does* fail, and should:

| situation | what fails |
|---|---|
| both places bumped | nothing |
| only one place bumped | exactly one test, `PythonReportsTheSameVersionAsTheCSharp`; `crosscheck.py` prints one `[VERSIUNE]` line and exits 1 |
| `<Version>` removed from `Directory.Build.props`, or re-added to a csproj | the plumbing tests in `VersionTests` |
| the banner stops being printed | the golden files, because they carry a placeholder line the output no longer produces |

The scrubbing does not weaken coverage: `VersionTests` asserts the banner's exact content
and its position on line 1, for every mode, for `--help`, and for failed runs.

## Licensing

The tool is **GPLv3-or-later**. The full text is in `LICENSE` at the repo root (the
verbatim FSF `gpl-3.0.txt` - don't reword it, and don't reflow it).

**Third-party code has its own file.** `THIRD-PARTY-NOTICES.md` lists every package linked
in, its licence, and - the part that matters - *why* that licence is compatible with
GPLv3. Two of the current entries take a real decision rather than inheriting one: LibRaw
is dual-licensed and only one of its two options is GPL-compatible, and ImageSharp's
licence is conditional and has to be shown to apply. A package added without an entry there
is a package whose compatibility nobody checked. See "The dependencies" above for the rule.

Five things follow, and all five are pinned by tests:

- **Every source file starts with the notice**, above the file's own header comment:
  the SPDX line, the copyright, and all three paragraphs of the standard GNU notice
  (permission, no-warranty, where-to-get-a-copy). `//` for C#, `#` for Python (below the
  shebang), and for XAML a single `<!-- ... -->` block wrapping the unprefixed text -
  `--` cannot repeat inside an XML comment. XAML counts as source: it is written by hand
  and it describes the interface. New files get the notice too - it is the first thing to
  write, not an afterthought. `LicenseHeaderTests` walks all four projects plus the two
  `.py` files and fails on any file missing it, which is the only thing keeping the
  convention alive as files get added; its `Projects` list is explicit rather than
  discovered, so adding a project is a decision. The SPDX line is there so licence
  scanners can read the file without parsing English.
- **Every run prints the copyright on line 2**, right under the version banner, in every
  mode, at `--help`, and on failed runs. Same reason as the banner: a pasted report says
  on its own whose tool produced it.
- **`--help` carries the warranty disclaimer** and points at `LICENSE` - GPLv3 §5(d),
  which wants a program that displays legal notices to display that one too. It lives
  there rather than under the banner on purpose: five lines on top of every report,
  every run, would drown the numbers. The text is `RunReport.WarrantyLines` in C#, which
  `WriteUsage` prints line by line and the window's "Despre" shows as one paragraph -
  `Golden/help.txt` pins the console side, `AboutTests` the window side; the argparse
  `epilog` (`__warranty__`) in Python.
- **The window's "Despre" is its §5(d) notice**, and it also carries the third-party
  list with each component's copyright line. That second part is LGPL-2.1 §6: when a
  program displays copyright notices at runtime - and this one does, on line 2 of every
  report and in the window's top bar - it must include the library's notice among them,
  with a reference to the LGPL. The list comes from `ThirdParty.Components`; LibRaw's
  copyright line there is the one from LibRaw 0.21.1's `COPYRIGHT` file, the version the
  runtime packages ship. `LibRawIsTakenUnderTheLgplAndCarriesItsCopyright` pins both the
  licence choice and the notice.
- **One source of truth per language for the copyright string**: `<Copyright>` in
  `Directory.Build.props` (read back through `AssemblyCopyrightAttribute` by
  `VersionInfo.Copyright` - the literal in `ReadCopyright` is only a fallback), and
  `__copyright__` in `flatcheck.py`. `VersionTests` binds all three together.

Unlike the version banner, the copyright line is **not** scrubbed anywhere: it is
constant, so the golden files carry it verbatim and `crosscheck.py` compares it as
ordinary text. That is deliberate - if the line ever stops being printed, the golden
files fail. `--help` is not one of `crosscheck.py`'s 43 scenarios (argparse and
`WriteUsage` produce entirely different text), so the two warranty notices are worded
the same by choice, not by test.

The year and the author are the user's call, exactly like the version. Don't roll the
year forward on your own initiative.

## Committing

**Do not commit.** Make the changes, run the tests, and hand over a commit message for
the user to use. They commit themselves.

This also means: don't `git add`, don't stage, don't amend. Leave the working tree as it
is so they can review the diff before deciding.

A good commit message here follows the existing log: a short lowercase subject line, then
a body that explains *why* rather than restating the diff - what the change is for, what
was deliberately left alone and why, and anything a future reader would otherwise have to
rediscover. No `Co-Authored-By` trailer.

## Build & test

```bash
dotnet build FlatCheck.sln -c Release
```

**The solution no longer builds on Linux**, because `FlatCheck.Wpf` targets
`net8.0-windows`. That is a deliberate cost, not an oversight: the console stays portable
if you build its project directly, and there is no CI workflow in the repo, so nothing
else notices.

```bash
dotnet build FlatCheck/FlatCheck.csproj -c Release
```

```bash
dotnet test FlatCheck.sln
```

Two test projects run: `FlatCheck.Tests` (core + console, portable) and
`FlatCheck.Wpf.Tests` (window, Windows-only).

The golden-output tests snapshot every mode's console text. When you change output
*on purpose*, re-approve them and read the resulting diff:

```bash
FLATCHECK_UPDATE_GOLDEN=1 dotnet test FlatCheck.sln
```

To look at the window without running it - handy when the Windows session is locked and a
screen capture would catch the lock screen:

```bash
FLATCHECK_DUMP_SCREENS=/tmp/ecrane dotnet test FlatCheck.Wpf.Tests --filter WindowSmokeTests
```

## Run

Self-contained binary:

```bash
dotnet publish FlatCheck/FlatCheck.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The last two flags are what keep the "single copyable binary" promise now that libraw is
in: without them its four native DLLs sit next to the exe as separate files. With them
the whole thing is one ~67 MB `flatcheck.exe` again. `linux-x64` works the same; other
RIDs (arm64, macOS) have no libraw binary in these packages, so FITS-only there.

Note the explicit project path: `dotnet publish` on the solution would now also try the
WPF project, which cannot be single-filed the same way.

**The `lensfun/` folder rides beside the exe**, in the build output and in the publish
folder (both csproj files copy it): the single copyable binary is now "the exe, plus the
`lensfun` folder if you want the lensfun half of the lens list". Without the folder every
mode still runs; `--lenses` says which folder it looked at, and the window's panel too.
The Adobe half needs nothing shipped: it is read from where Lightroom / Camera Raw / DNG
Converter installed it.

The window app publishes without `PublishSingleFile`:

```bash
dotnet publish FlatCheck.Wpf/FlatCheck.Wpf.csproj -c Release -r win-x64 --self-contained
```

```bash
FlatCheck.Wpf.exe "D:\Astro\2026-08-12-Smida"   # optional: the session folder
```

```bash
flatcheck process/pp_flat_stacked.fit                  # 1. structure (or masters/pp_flat_stacked.fit, Siril 1.4 stock scripts)
flatcheck s_0deg/process/pp_flat_stacked.fit --ratio s_180deg/process/pp_flat_stacked.fit   # 2. attribution
flatcheck process/pp_light_00001.fit --light           # 3. end-to-end
flatcheck flat_brut.fit --expo                         # 4. exposure
flatcheck DSC_0060.NEF --expo                          # 4. exposure, straight off the raw
flatcheck sky_flat.fit --expo --sky                    # 4b. sky flat
flatcheck flat_brut.fit --expo --sat 4095 --black 150  # override, mostly for FITS
flatcheck flat.fit -o harta.png                        # custom PNG path
flatcheck process/pp_flat_stacked.fit --raw flats/DSC_0001.NEF   # 1 + the sixth test: vignetting vs the lens profiles, lens and f-stop from the raw
flatcheck --lenses --lens "Tokina 11-20"               # 7. which lenses have a vignetting profile
flatcheck process/pp_flat_stacked.fit --synthetic process/pp_flat_stacked_sintetic.fit --lens "Tokina AT-X 11-20mm F2.8 PRO DX" --fstop 2.8
flatcheck process/pp_flat_stacked.fit --against process/pp_flat_stacked_sintetic.fit   # how much the master differs from the model
```

Remember: the flag must come *after* the file path.
