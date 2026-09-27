# Code map: what each file is for and why it is shaped that way

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** every project and file; the per-file rationale the code comments do not repeat.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

Quantitative diagnostic for astrophotography flat frames. Answers four questions:
is the master flat radially symmetric, is the flat *setup* clean, does the flat
actually correct a real light, and is a raw flat exposed correctly. A fifth mode answers
nothing and shows: the frame through histogram equalisation, Siril's "Histogram" display
mode, where every grey level holds the same number of pixels and any non-uniformity is
at full contrast. See "5. Histogram equalisation" in `modes-histeq.md`. A sixth mode
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

Five projects (`FlatCheck.Core`, `FlatCheck`, `FlatCheck.Wpf`, `FlatCheck.Tests`,
`FlatCheck.Wpf.Tests`). The split happened in 2.0.0, when a second application appeared over the
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
    **A lensfun lens's identity includes its calibration crop** (`LensChoice.Calibration`,
    "APS-C 1.528" / "full frame 1"; `Where` = mount + calibration is what every list, table,
    report and FITS `HISTORY` shows, `Title` = name + calibration what the verdict sentences
    say): the database holds 20 lenses twice, same name and mount, calibrated on two
    formats, and with the crop outside the key the two collapsed and `Nearest` took the
    first in file order - on the Nikkor 35/1.8G DX the poor one (0.19 stops at the DX
    corner against 0.51 from the hidden D780 entry and 0.61 from Adobe; `docs/design/lens-profiles.md`
    §5 and §8). The label is on every lensfun lens, not only the duplicated ones, so a name
    cannot change when the database gains an entry; Adobe stubs carry none (the body is the
    mount, the SFF is not in the file name). Pinned by
    `Lensfun_TheSameLensCalibratedOnTwoFormatsIsTwoLenses`,
    `ALensfunLensCalibratedOnTwoFormatsIsTwoRows` and the two tab tests of the same name.
    **A lensfun pair at 10 and 1000 m with identical coefficients is one profile with an
    unknown distance** (`LensfunReader.CollapseCopiedDistances`, `DistanceM = NaN`, printed
    "distanta nespecificata"): that is how lensfun marks "measured once, distance not
    recorded", and the old "infinit" was a copied value presented as a fact - the same
    Nikkor entry has the floor of Adobe's 0.36 m profile, not the 1.5 m one. A single entry
    at a stated distance keeps it; a pair with different coefficients keeps both.
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
    The members of every `XxxResult`, with their units, which of them vote and which are
    informative, and the `docs/validation.json` keys that read them, are tabulated under
    "Result records (Analysis/)" at the end of this note.
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
    what keeps the golden files, the parity ones included, still. `Compare` is the second half
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
    Every outcome type of this folder, member by member with its unit, and the `TextWriter`
    rule stated as a fact, are tabulated under "Run outcomes (Runs/)" at the end of this note.
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
  ported from, **frozen at 2.2.0 on 2026-09-26 and not maintained** (ADR 0047). Prior art
  only; nothing is mirrored into it any more. The numbers it confirmed live on as
  `Golden/parity_*.txt` (`ParityGoldenTests`); `crosscheck.py` beside it is the retired
  comparison tool. See `python-parity.md`.
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

- `docs/` - measurements that outlived the plans they were made for. Until 2026-09-25 the
  plans were deleted once done, because their step lists went stale at the first later
  change; since ADR 0001 an approved plan is kept in `docs/plans/`, with a "What deviated"
  section added at wrap-up, and is history, never updated to match the code later. What
  does not go stale - a measurement made on a date on named files, a design tried and
  rejected - is folded into this file or into the header comment of the code it explains,
  and the long form, when there is one, lands here: `siril-histeq-source.md` (the Siril 1.4.4 code the
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
feature plans since were run to completion and removed, under the rule in force then; the
plans since 2026-09-25 are in `docs/plans/`.


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
`Th.LightRadOk` / `Th.LightRadBad`, the ratio mode's world label on `Th.RatioOk` / `Th.RatioBad`.
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
the smooth and against reports print the battery without renaming a test.

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

**`RatioResult`** (`RatioAnalyzer.cs`, mode `--ratio`; the run fills the four metadata members
from the FITS headers).

| Member | Unit | Meaning | Role |
| --- | --- | --- | --- |
| `Sensor` | `StructureResult` on `(A + B) / 2`, prepared (its `RawMedian` / `RawMax` are not ADU) | the camera-fixed part | **the mode's verdict is `Sensor.Verdict`** |
| `WorldPtpPct` | percent, per orientation (odd part / 2) | the external gradient | informative |
| `WorldErrPct` | percent | its error bar (even part / 2) | informative |
| `WorldCornerPct` | percent, per orientation | corner spread of the world map | informative |
| `WorldLabel` | light-scale word on `Th.RatioOk` / `Th.RatioBad` | coloured label of the world figure | **does not vote** |
| `World` | `Img`, `A / B` normalised to median 1 | the second map's source | the map |
| `StackCountA`, `StackCountB` | count, `STACKCNT`, 0 = unknown | how many frames each stack has | informative |
| `DateObsA`, `DateObsB` | string, `DATE-OBS`, null = unknown | when each was shot | informative |
| `Verdict` | residual-scale word | `Sensor.Verdict` | the vote |

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

| Member | Unit | Meaning |
| --- | --- | --- |
| `IsCfa`, `CfaSpread`, `Cfa` | as in `ExposureResult` | |
| `SourceH`, `SourceW` | sensor pixels | the input frame |
| `Shrink` | factor; 1 = not shrunk | how much each channel was reduced for the console's PNG |
| `Channels` | `HistEqChannel[]`, 4 on CFA, 1 on mono | per channel: `Position`, `Label`, `Colour`; `H`, `W` and `Equalised` (shrunk grey raster, bytes); `FullH`, `FullW` and `Full` (full-resolution grey raster the window shows); `Min`, `Max` (input range of the non-zero pixels, file units); `NonZero` (pixel count in the histogram); `LevelsUsed` (of the 256 output levels) |
| `H`, `W` | shrunk pixels | common size of every shrunk view |
| `CompositeRgb` | bytes, `H * W * 3` | RGB on a CFA with known order, triplicated grey otherwise |
| `Mosaic` | bytes, `H * W`, or null on mono | the equalised mosaic as one layer |
| `FullH`, `FullW`, `CompositeFullRgb`, `MosaicFull` | full-resolution counterparts (`MosaicFull` is `SourceH * SourceW`) | what the window shows at 100 % |
| `ChannelCount` | count | |

**`SmoothResult`** (`SmoothAnalyzer.cs`, mode `--smooth`). No verdict of its own: the words
come from the structure battery run before and after.

| Member | Unit | Meaning | `validation.json` key (kind `smooth`) |
| --- | --- | --- | --- |
| `Smoothed` | `Img`, full resolution, the input's shape; never null | the product that `SmoothRun.WriteFits` writes | |
| `IsCfa`, `CfaSpread`, `SourceH`, `SourceW` | as above | | |
| `ScalePx` | sensor pixels, the kernel's sigma | the smoothing scale used | the case's `scalePx` is the input, not a figure |
| `TilePx`, `SigmaTiles` | sensor pixels; tiles | tile side, and the sigma in tiles | |
| `Positions` | `SmoothPositionFit[]`, 4 on CFA, 1 on mono | per Bayer position: `Tiles`, `Rejected`, `FitFailed` (counts), `TileRms` (fraction of the level) | |
| `Removed` | `Img`, analysis raster, `Prepare(input) / Prepare(smoothed)` | what was taken out; the first map | |
| `Width`, `Height` | pixels of the analysis raster | | |
| `RemovedP1Pct`, `RemovedP99Pct` | percent | tails of `Removed` | |
| `RemovedPtpPct` | percent, p95 - p5 over tiles | | `removedPtpPct` |
| `RemovedMaxTilePct` | percent | the most deviating tile of `Removed` | `removedMaxTilePct` |
| `RemovedSpotPct`, `RemovedSpotX`, `RemovedSpotY` | signed percent; pixels of the analysis raster | deepest compact spot removed, and where | `removedSpotPct`; the case-level `spotAt` records X, Y and is not compared by the test |
| `RemovedFlagged` | bool, `RemovedMaxTilePct >= Th.SmoothKeptWarn` | more than texture was removed | |
| `InputTexturePct`, `OutputTexturePct` | percent (`Metrics.FineTexture`) | was there texture, and did it go | `inputTexturePct`, `outputTexturePct` |
| `Before`, `After` | `StructureResult` on the analysis rasters of input and output | the battery both sides | `beforeVerdict` / `afterVerdict` read `.Verdict`; `beforeVignetteStops` / `afterVignetteStops` read `.VignetteStops` |

**`SmoothScaleAdvice`** (`SmoothScaleAdvisor.cs`, the proposed scale): `Kind`
(`SmoothAdviceKind`: `NoTexture`, `Proposed`, `Undetermined`), `InputTexturePct` (percent),
`ScalePx` (sensor pixels; `NaN` unless `Proposed`), `Trials` (one `SmoothScaleTrial` per rung:
`ScalePx`, `TextureAfterPct` in percent, `AzimuthalDelta` / `TiltDelta` / `CornerDelta` in
percent points after minus before, `RemovedPtpPct`, `RemovedMaxTilePct`, `TextureGone`,
`StructureLost`, `Acceptable = TextureGone && !StructureLost`).

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
unconditionally, and the same holds for `RatioRun`, `ExposureRun`, `LightRun`, `LightSeriesRun`,
`HistEqRun`, `SmoothRun`, `SyntheticRun` and `AgainstRun`. The runs that can refuse (`LightRun`,
`LightSeriesRun`, `SmoothRun`, `SyntheticRun`) write the refusal text into the same writer. No
flag suppresses it, and nothing returns the text as a string. So the console passes its stdout
and streams; the window passes a `StringWriter` and saves what came back as
"Rezultat testare.txt", the same text by construction; and **a caller who wants the figures and
no text passes `TextWriter.Null`**, which is exactly what `ValidationTests` does for the
structure, profile, smooth and against cases. Verified against the code on 2026-09-25: this is
true today for all nine runs.

**`RunOutcome<TResult>`** (`RunOutcome.cs`), for the modes that always answer.

| Member | Meaning |
| --- | --- |
| `Result` | the analyzer's record, never null |
| `Png` | the residual map, already encoded; null when the mode has no map (`--expo`) |
| `SecondaryPng` | a second map when the mode has two; only `--ratio` (the world map), null everywhere else |

| Run and signature | Returns | `Png` | `SecondaryPng` |
| --- | --- | --- | --- |
| `StructureRun.Execute(path, report, query = null)`; `StructureRun.Compare(result, reference, query)` for the second half alone | `RunOutcome<StructureOutcome>`; `StructureOutcome` | `Structure.Residual` map | null |
| `RatioRun.Execute(pathA, pathB, report)` | `RunOutcome<RatioResult>` | `Sensor.Residual` map (camera-fixed part) | `World` map (`A / B`) |
| `ExposureRun.Execute(path, sat?, black?, sky, report, withPreview = false)` | `RunOutcome<ExposureResult>` | null | null |
| `HistEqRun.Execute(path, report)`; `EncodeChannel(result, i)` and `EncodeMosaic(result)` on demand | `RunOutcome<HistEqResult>` | `CompositeRgb` as PNG | null |
| `AgainstRun.Execute(pathA, pathB, report)` | `RunOutcome<AgainstResult>` | `Structure.Residual` map (the ratio's residual) | null |

The runs that can refuse have their own outcome type, each with `Refused => Result == null`
(or `Advice == null`):

| Outcome | Members | Meaning |
| --- | --- | --- |
| `LightRunOutcome` from `LightRun.Execute(path, fov?, report)` | `Suitability` (`SuitabilityResult`, always set), `Result` (`LightResult` or null), `Png` (`RadialMap`, or null), `Refused` | refused when the field of view is `TooWide`; the refusal text is `LightReport.WriteTooWide` |
| `LightSeriesRunOutcome` from `LightSeriesRun.Execute(dir, pattern, fov?, report, onFrame = null, ct = default)` | `Suitability`, `Result` (`LightSeriesResult` or null), `Png` (mean `RadialMap`), `Files` (the series, always set), `Refused` | refused on the first frame's field of view; an empty series throws instead of refusing; `onFrame(i, n, name, result)` fires per frame as the table streams |
| `SmoothRunOutcome` from `SmoothRun.Execute(path, scalePx, report)` (or the overload on a loaded frame) | `Result` (`SmoothResult` or null), `Frame` (`LoadedFrame`, for the cards and `Bitpix` the writer copies), `Png` (`Removed` map), `SecondaryPng` (`After.Residual` map), `Refusal` (text or null), `Refused` | refused on a raw (no FITS cards) or a colour cube (`Planes > 1`); the file is written by the caller through `SmoothRun.WriteFits(outcome, sourcePath, Stream)`, `OutputBitpix` keeps 16 and turns everything else into -32 |
| `SmoothAdviceOutcome` from `SmoothRun.Advise(path, report)` | `Advice` (`SmoothScaleAdvice` or null), `Frame`, `Refusal`, `Refused` | the same two refusals |
| `SyntheticRunOutcome` from `SyntheticRun.Execute(referencePath, request, sources, focal?, fstop?, crop?, report)` (or the overload on a loaded frame) | `Result` (`SyntheticFlatResult` or null), `Request` (`SyntheticRequest`: `LensText`, `Source`, `Lens`, `FocalMm` + `FocalSource`, `FStop` + `FStopSource`, `CameraCrop` + `CropSource`, each value with where it came from), `Frame`, `Refusal`, `RefusedChoice` (true when the refusal was the lens choice: none given, no match, ambiguous), `Refused` | refused on a colour cube, on an unresolvable lens, or on a missing focal, f-stop or (lensfun only) crop; the file is written by the caller through `SyntheticRun.WriteFits(outcome, referencePath, Stream)`, always -32 |
