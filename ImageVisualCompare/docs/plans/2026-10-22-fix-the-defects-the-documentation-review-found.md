# Fix the defects the documentation review found

## Context

The max-effort code review of the documentation backfill (merged into `main` as 3326fe4) confirmed
eight code defects; the backfill fixed only the comments and listed the defects in
`docs/plans/2026-10-21-document-every-type-and-method.md`, "What deviated". On 2026-10-22 the
owner first got seven prompts for separate sessions, then asked: "Oare nu le poti implementa pe
toate aici?" So they are done here, in this session, on one branch. That deviates from the kit's
"One task per session", at the owner's word; it is noted in "What deviated".

The outcome: each defect fixed, test first, with its requirement, decision and design note in
step; the comments that since 3326fe4 describe each defect as it is change with its fix.

## The owner's answers (2026-10-22, recorded as ADRs)

- **R-005, where a WIC failure becomes a message:** in `WicImageLoader`, which turns WIC's own
  failures into the exception types `ImagePairLoader.IsExpectedLoadFailure` already accepts, as
  `FitsImageLoader.CreateBitmap` does; R-005's list stays.
- **Float TIFF: like a FITS.** Reported `32-bit float RGB`, shown as stored through `FitsScale`
  with R-016's rule for 0..1 against 0..65535.
- **DPI: PerMonitorV2,** declared in an `app.manifest`.
- **"−" under the lowest stop: to the floor,** never zooming in; at the floor it does nothing.
- **WPF objects get unit tests,** on an STA thread run by a helper of the test project (no
  package), never shown; ADR 0026 is superseded in part, launching stays required.

Decided by me, stated here (the owner may object at approval):
- Ctrl+O and Open… do nothing while a pair loads (R-006, R-063).
- A FITS `BZERO` or `BSCALE` that parses to NaN or an infinity makes the header invalid,
  `FITS: the header is not valid.` (R-019); a unit with NAXIS = 0 has no data, whatever its
  PCOUNT (the standard: "no data follow the header").
- The minimap maps clicks and draws its rectangle in the coordinates of its inner `Overlay`
  canvas; at the far edge the 2-DIP rectangle is moved inside, not shrunk (R-041, R-042).
- A float TIFF's alpha is shown as 255, as a FITS's is.

## Facts measured (2026-10-22, scratch probe `floatpair` on `main` at 3326fe4)

- `stacked_GraXpert_AI.tiff` and `stacked_GraXpert.tiff` (6016 x 4016, RGB float32, 96 bpp):
  reported `96-bit`, IsFloat false. WIC's levels against the samples as stored: 0 % equal,
  mean +73.5 and +73.2; medians 62 -> 135 and 67 -> 141; stored 12 -> 62, 32 -> 100, 64 -> 137.
- The comparison the owner makes, before/after GraXpert in `2026-07-10-Cartisoara`: `stacked.tif`
  (16-bit RGB) median R 70 G 72 B 64; `stacked_GraXpert.tiff` R 142 G 140 B 140 in the app,
  67 as stored.
- `stacked_GraXpert_AI.fit` is not the TIFF's twin: Siril 1.4.4 wrote it two days later with a GHS
  stretch in its HISTORY (0 % of the samples equal).
- Detection: the 96 bpp RGB float frame is `PixelFormat` "Default (96 bpp)", 3 masks, not equal to
  `PixelFormats.Default`; its TIFF `SampleFormat` (`/ifd/{ushort=339}`) is [3,3,3] and
  `BitsPerSample` [32,32,32]. Integer TIFFs give none or [1,1,1]. Hand-written gray and RGBA
  float32 TIFFs decode as `Gray32Float` and `Rgba128Float`.
- The review's crash cases: a 72-byte PNG with IHDR 40000 x 30000 throws OverflowException
  (0x80070216) at `CachedBitmap`; a VP8L WebP with a garbage bitstream throws COMException
  0xC00D36BE at the probe, a lossy one at the decode ("The file does not conform to the relevant
  file format specification."). The WebP codec is installed here.
- No test creates a window or control today; `AppIconTests` reads resources of the built Wpf dll
  with `LoadLibraryEx`, the pattern for reading its manifest.
- Baseline load through the application's loader (process start and the medians included):
  `stacked_GraXpert.tiff` 1.4 s and 0.8 s, `stacked.tif` 0.6 s and 0.7 s.
- `docs/skills.md`: a loader change gets its fixture from `TempImages`, never a binary file; a
  change under `Wpf/` launches the application on the real pair before the commit; Core and
  `Imaging/` are numeric code, so `/validate` runs at wrap-up.

## Design checks (2026-10-22, read from the code)

A planning agent launched for this review stopped at the usage limit before reading anything, so
the risky points were checked by hand:
- **The STA helper.** The generated `App.InitializeComponent()` is public; it sets `StartupUri`,
  which only `Run` uses, and loads `App.xaml` with `Theme.xaml` merged. `new App()` plus
  `InitializeComponent()`, never `Run`, gives `MainWindow` and `MinimapView` their resources. The
  test project already references the Wpf project and sees its internals; nothing else in it
  creates an `Application`. The helper's thread is a background STA thread that runs
  `Dispatcher.Run()`; a test body is posted with `Dispatcher.InvokeAsync` and awaited, so its
  exceptions fail the test.
- **The `MainWindow` test.** A window never shown raises no `Loaded`, so the startup open does not
  run, and `DarkTitleBar.Apply` only subscribes to `SourceInitialized`. The test body runs
  synchronously on the dispatcher: the first click leaves `OpenPairAsync` suspended at its first
  `await` (the probe's `Task.Run`), and the second click comes while it is. The fake loader's
  `LoadAsync` waits on a gate that is never opened, so `ShowPair`, `SetPair` and the memory timer
  never run after the test.
- **The `MinimapView` test.** `Show` sets the minimap's size (190 x 124.4 for 5938 x 3888);
  `Measure`, `Arrange` and `UpdateLayout` make `ActualWidth` 190 and the `Overlay`'s 188. The seam
  is `RequestCentre(Point)` made internal, still fed the minimap's own coordinates
  (`e.GetPosition(this)`), translating them into the `Overlay`'s itself; the XAML field
  `ViewportRect` is internal already.
- **The manifest test.** `AppIconTests` enumerates the resources of the built Wpf assembly by
  type; RT_MANIFEST is type 24, so the test needs no resource ID. The apphost gets the
  assembly's Win32 resources, which is why the exe and the dll both carry today's default
  manifest; the running process's awareness is checked at the launch.
- **The float TIFF.** `frame.Metadata` is queried only when `frame.Decoder` is a
  `TiffBitmapDecoder` (a PNG's metadata has no `/ifd`); whether it is available on a
  `DelayCreation` frame in `Probe` is the first thing `R090_...` shows. `WicImageLoader.Load`
  gains an optional `CancellationToken`, looked at before each band, as `FitsImageLoader.Load`
  does. Bands of rows through `CopyPixels` keep the float buffer small; the time is measured on
  `stacked_GraXpert.tiff` against the baseline.
- **Existing tests stay as they are:** `ZoomLadder` does not change, no test covers `ZoomOut`
  below the ladder, and `R011_Probe_ReportsTheSameThingAsAFullLoad` uses integer formats.

## Setup

- From `E:\Photografie\Code\ImageVisualCompare`:
  `git worktree add .claude/worktrees/Fix-the-defects-the-documentation-review-found -b claude/Fix-the-defects-the-documentation-review-found main`
- `<wt>` = that worktree's `ImageVisualCompare` folder. Edits with the Edit tool on absolute paths
  there; commits `git -C <wt> commit -q -m "..."`, the hook's `== OK` expected, the message
  length checked first (100 characters at most).
- The five real files copied into `<wt>/tests/testdata/large/` (ADR 0005), for `/validate`.
- This plan saved as `<wt>/docs/plans/2026-10-22-fix-the-defects-the-documentation-review-found.md`.
- Each fix: the requirement first, then its test, run and seen failing (output kept for "What
  deviated"), then the code, then the comments since 3326fe4 and the design note, then the gate.
- ADR numbers in commit order, from 0031 (the highest today is 0030). A partial supersession
  (the kit's ADR 0024) writes `Supersedes: NNNN in part (<part>)` in the new ADR, the part without
  parentheses, and appends `; <part> superseded by NNNN` to the old ADR's Status line, the part
  without a semicolon; `check-decisions.ps1` matches the numbers.

## Commits on `claude/Fix-the-defects-the-documentation-review-found`

1. **The plan.**
2. **R-005, WIC's failures are messages** (ADR 0031, superseding ADR 0015 in part: "turning a new
   exception type into a message means adding it to `IsExpectedLoadFailure`").
   - `WicImageLoader`: around the decoder and the frame in `Probe` and `Load`, a `COMException`
     becomes a `FileFormatException` carrying WIC's message; in `Load`, an `OverflowException`
     from the `CachedBitmap` becomes `NotSupportedException("An image of W × H is too large to
     show.")`, the FITS wording without its prefix. `IsExpectedLoadFailure` and R-005's list
     stay; R-005 gains the two cases and an example.
   - Tests (`ImagePairLoaderTests`, real loader): `R005_AnImageTooLargeToDecode_IsAMessageNamingIt`
     (a PNG whose IHDR says 40000 x 30000, written by `TempImages` with its CRCs and a small
     IDAT); `R005_ADamagedWebP_IsAMessage_AtTheProbeOrAtTheDecode` (Theory: the lossless case
     fails at the probe, the lossy one at the decode; bytes as the review built them).
   - Comments since 3326fe4: `IImageLoader.Probe`/`LoadAsync`, `WicImageLoader`'s class
     summary, `ImagePairLoader.LoadAsync`'s `<returns>`, `IsExpectedLoadFailure`.
   - `imaging.md`: a paragraph on how each loader turns its decoder's failures into the types
     the pair loader expects.
3. **R-006, no second open while a pair loads** (ADR 0032, superseding ADR 0026 in part: "the
   WPF layer is checked by launching the application, not by unit tests").
   - `tests/ImageVisualCompare.Tests/WpfThread.cs`: one long-lived STA thread that creates the
     application's resources once (`new App()` + `InitializeComponent()`, never `Run`) and runs
     the test bodies on its dispatcher; the WPF test classes share one xUnit collection.
   - `MainWindow`: an internal constructor taking `IFileDialogService` and `ImagePairLoader` (the
     public one chains to it with the real services); `OpenPairAsync` returns at once while an
     open runs. R-006 and R-063 say so.
   - Test `MainWindowTests.R006_ASecondOpenWhileAPairLoads_AsksForNothing`: a counting dialog
     service, a loader whose decode waits on a gate never opened; `OpenButton` clicked twice
     (`ButtonBase.ClickEvent`); one dialog. Ctrl+O reaches the same `OpenPairAsync`, but the test
     cannot hold Ctrl down and no key is sent to the running application, so that path is
     covered by reading only, and reported so.
   - Comment: `MainWindow.OnPreviewKeyDown`. Notes: `shell.md` (the guard), `testing.md` (the
     helper; `MainWindow` is no longer wholly untested).
4. **FITS header edge cases** (R-014, R-019).
   - `FitsHeader.Number`: a value that parses to NaN or an infinity throws `InvalidHeader()`;
     `DataBlockBytes`: NAXIS = 0 is no data.
   - Tests (`FitsHeaderTests`): `R019_ABZeroOrBScaleThatIsNotFinite_IsAnInvalidHeader` (Theory:
     NaN, 1D999, -Infinity); `R014_ReadLayout_AnEmptyPrimaryWithAPCount_IsSteppedOver` (Theory:
     PCOUNT 1 and 5000, the IMAGE extension behind it found).
   - Comments: `Number`, `DataBlockBytes`. Note: `imaging.md`.
5. **"−" never zooms in** (R-024; ADR 0033, superseding ADR 0020 in part: "`−` past the low end of
   the ladder").
   - `Viewport.ZoomOut`: when the ladder's answer is not below the scale, the floor
     (`MinScale`). `ZoomLadder` unchanged, so its tests stay.
   - Test (`ViewportTests`): `R024_ZoomOut_UnderTheLowestStop_GoesToTheFloor_NeverIn` (a 5938 x
     3888 image whose fit is under 0.02, from 0.018 to the fit, and from the fit nowhere).
   - Comments: `Viewport.ZoomOut`, `CompareView.ZoomOut`, `ZoomLadder.Previous`.
6. **The minimap inside its border** (R-041, R-042).
   - `MinimapGeometry.Layout`: the 2-unit rectangle moved inside the area at the far edge.
   - `MinimapView`: lays out and maps clicks in `Overlay`'s coordinates and size;
     `RequestCentre` becomes internal and translates the minimap's coordinates itself.
   - Tests: `MinimapGeometryTests.R041_AtTheFarEdge_TheRectangleStaysInsideTheArea`; on the
     STA thread, `MinimapViewTests.R042_AClickOnTheThumbnailsCorner_CentresOnTheImagesCorner` and
     `R041_TheRectangleIsDrawnInsideTheBorder`.
   - Comments: `MinimapGeometry.Layout`, `MinimapView`. Note: `rendering.md` gains a minimap
     paragraph and the two files in its Covers.
7. **Per-monitor DPI awareness** (R-022; ADR 0034).
   - `src/ImageVisualCompare.Wpf/app.manifest` with `dpiAware` true/PM and `dpiAwareness`
     PerMonitorV2, PerMonitor; `<ApplicationManifest>` in the Wpf csproj.
   - Test `R022_TheApplicationDeclaresPerMonitorDpiAwareness`: reads the RT_MANIFEST resource of
     the built Wpf assembly as `AppIconTests` reads the icon.
   - Comments: `CompareView`'s class summary, `ZoomToActualSize`, `ReadDeviceScale`. Note:
     `rendering.md` (and `app.manifest` in its Covers).
8. **Float TIFF like a FITS** (R-090, R-091 in a new area, R-010 and R-011 amended; ADR 0035,
   superseding ADR 0022 in part: "the conversion of float samples").
   - Core: a small function that turns a row of float samples into Bgra32 through
     `FitsScale.ToByte`, gray to all three channels, alpha 255, counting the finite samples above
     `FitsScale.RescaleThreshold`; unit tested in Core.
   - `WicImageLoader`: a float frame (the named float formats, or a TIFF whose `SampleFormat` is
     3) is described with `IsFloat` and its channels, and decoded through `FormatConvertedBitmap`
     to a float format, in bands, into a `WriteableBitmap`; a second pass at 65535 when
     `FitsScale.IsRescaled`, with `ValueRange`, as `FitsImageLoader` does.
   - `TempImages.WriteFloatTiff` writes gray, 96 bpp RGB and 128 bpp RGBA float32 TIFFs as
     libtiff does. Tests (`WicImageLoaderTests`): `R090_AFloatTiff_IsReportedAsFloat` (Theory),
     `R091_AFloatTiff_IsShownAsStored` (0.5 is 128, NaN 0), `R091_AFloatTiffInZeroTo65535_IsReadSo`.
   - Comment: `ImageInfo.IsFloat`. `CLAUDE.md`, rules "A FITS is shown as stored" and "Formats are
     reported, never branched on". Note: `imaging.md`.
9. **Wrap-up:** README (formats, the float TIFF, Ctrl+O during a load, a file that cannot be read,
   100% on every monitor), CHANGELOG (`Changed`: the float TIFF; `Fixed`: the other six; `Internal`:
   the WPF tests), `code-map.md`, and this plan's "What deviated" with the measured figures.

## Verification

- **Each new test seen failing** before its fix, the output kept; the gate green at every commit
  (256 tests now, plus the new ones).
- **`/validate`** (Core and `Imaging/` change): the four cases, no drift expected; no case has a
  float TIFF.
- **The float TIFF on real files:** the probe's medians for `stacked.tif` against
  `stacked_GraXpert.tiff` (expected about 70 against 67), and the load time against the baseline.
- **The launch** from the worktree's build: the real pair (screenshot); the before/after GraXpert
  pair (the status bar `16-bit RGB · 32-bit float RGB`); the PNG of 40000 x 30000 (the message
  dialog); the running process's DPI awareness read by P/Invoke (per-monitor, where it was
  system). A monitor at another scaling is the owner's to set; until then that case is
  unchecked.
- **The whole diff** read before each commit; `check-design.ps1 -Since main` notes read against it.

## Then

`/wrap-up`, the merge proposed (`git merge --no-ff` in `E:\Photografie\Code`, the owner's call),
then the worktree removed and the branch deleted.

## What deviated

- **One session for two tasks.** The documentation backfill and these fixes ran in the same
  session, at the owner's word, against the kit's "One task per session"; the eight fixes are one
  task on one branch, a commit each.
- **The design review was done by hand.** The planning agent stopped at the usage limit before
  reading anything; the risky points were read in the code instead ("Design checks" above), and
  each held.
- **A sixth ADR, 0036.** The choice to ignore a second open, rather than queue it or let it
  cancel the first, was settled through this plan's approval; a choice settled in chat gets an
  ADR, so it has one, written at the wrap-up. The ADRs came out 0031 WIC's failures, 0032 the WPF
  tests, 0033 "−", 0034 DPI, 0035 the float TIFF, 0036 the ignored open.
- **`IsAboveThreshold` moved** from `FitsRowDecoder` to `FitsScale`, so the float TIFF and the
  FITS reader share the rule that decides the rescale; not in the plan, no behaviour changed
  (the R-016 tests of `FitsRowDecoderTests` pass as before).
- **"−" at exactly 2 %.** The rule is "when no stop is below the scale", which covers the
  lowest stop itself: with the fit under 2 %, "−" at 2 % now goes to the fit, where it did
  nothing. R-024 says so.
- **A circular figure, measured again.** The first check of the float TIFF on real data compared
  the application's levels with levels computed from WIC's float conversion, the path the
  application now takes too, so its "100 % equal" proved nothing about that conversion. The
  floats were then read straight from the strips of the two uncompressed exports and compared
  with WIC's `Rgb128Float`: bit for bit equal, 72 480 768 of 72 480 768 samples in each.
- **Not checked by running:** `Ctrl+O` during a load (the test clicks the button; the key path
  reaches the same method and is read), and a move between monitors scaled differently (all the
  owner's monitors were at 100 %; run later, next bullet).
- **The move between monitors, run on 2026-10-23,** after the merge, with the owner's right-hand
  monitor set to 150 % and the build of `main` (cf8e0cc), on the real pair (V-001) with the
  divider at the centre: the window opened at 96 dpi on the primary, was moved and maximised on
  the 150 % monitor by a script, and `GetDpiForWindow` then gave 144, so Windows no longer
  stretches it and `OnDpiChanged` brought the new scale. After `Ctrl+1`, a 48 × 48 patch of the
  captured window, 300 px left of its centre and so in A, matched `de_printat.png` at scale 1
  with a mean difference of 0.14 levels (0 would be exact; the residual was not traced), against
  1.41 for the same patch resampled 1.5× nearest-neighbour, the proxy for a window stretched by
  Windows; a control capture at fit matched neither (3.51). PowerShell's own `GetDpiForMonitor`
  reports the system's 96 dpi for every monitor, since its thread is system-DPI-aware; the
  script sets its thread per-monitor aware first.
- **Each test seen failing before its fix:**
  - R-005: `OverflowException`, "The image data generated an overflow during processing.", for
    the 40000 x 30000 PNG; `COMException` 0xC00D36BE for both WebPs, the lossless one at the
    probe, the lossy one at the decode.
  - R-006: `Expected: 1, Actual: 2` dialogs.
  - FITS: the three non-finite headers read without an exception; the empty primary with a
    `PCOUNT` of 1 and of 5000 gave "FITS: no image found in the file."
  - R-024: from 0.018, "−" went to 0.02; the fit was 0.01533.
  - Minimap: the rectangle ended at 101.94 on a 100-unit area; a click at (1, 1) centred on
    31.25 image pixels; the rectangle ended at 190 on the 188-DIP thumbnail.
  - R-022: the embedded manifest was the compiler's default, with no `PerMonitorV2`.
  - Float TIFF: reported `32-bit gray`, `96-bit` and `32-bit RGBA`; 0.5 showed as 188 and
    0.25 as 137; the file counting to 65535 was all white.
- **Measured on 2026-10-22:**

  | Commit | What | Gate | Tests |
  |---|---|---|---|
  | 783c6a9 | the plan | `== OK in 30s` | 256 |
  | 649532c | R-005, WIC's failures as messages | `== OK in 30s` | 259 |
  | 8f0f52e | R-006 and the WPF test thread | `== OK in 27s` | 260 |
  | c7ca752 | the FITS header's two edge cases | `== OK in 27s` | 265 |
  | d3fb235 | "−" never zooms in | `== OK in 30s` | 267 |
  | 95750f8 | the minimap inside its border | `== OK in 41s` | 270 |
  | bfec5fc | per-monitor DPI awareness | `== OK in 26s` | 271 |
  | c4294bd | the float TIFF as a FITS | `== OK in 25s` | 280 |

  - **The float TIFF on real data:** `stacked_GraXpert.tiff` is `32-bit float RGB`, the medians
    69/67/67 against `stacked.tif`'s 70/72/64 (142/140/140 through WIC's conversion); the load
    takes 1.4 to 1.5 s against 0.8 to 1.4 s before, the process start included.
  - **DPI awareness of the running application,** `GetProcessDpiAwareness` and the main window's
    context: 3326fe4 system aware (1), this branch per-monitor (2) with the v2 context.
  - **The launches,** from the branch's build: the real pair after the R-006, minimap and DPI
    commits, the same picture each time, closed with exit code 0; the before/after GraXpert pair,
    `6016 × 4016 · A 16-bit RGB · B 32-bit float RGB`, both halves equally bright; the 40000 x
    30000 PNG, the dialog "Could not read ihdr-40000x30000.png." and "An image of 40000 × 30000
    is too large to show.", the process still running.
  - **`/validate`:** the four cases passed with no drift; the figures are in
    `../validation.md`, History, 2026-10-22.
  - **The doc counter,** `code` group: 46 types and 157 methods, none undocumented or missing a
    tag but `OpenPairAsync`'s plain `Task`.
- **The code review of 2026-10-23,** `/code-review` of the branch at high effort, 7 findings, all
  fixed on the branch at the owner's word: `DataBlockBytes` reads `BITPIX`, `GCOUNT` and `PCOUNT`
  before returning 0 for a unit with no axes, so a negative or non-integer one is still an
  invalid header (a theory of three cases, seen failing first); the TIFF tag path takes only
  32-bit samples, so a half or a double TIFF keeps WIC's path (R-090 says so); `Bitmaps.Create`
  and `Bitmaps.TooLarge` in `Imaging/` serve both loaders; `Decode` writes a band in one
  `WritePixels`; `Win32Resources` serves `AppIconTests` and `AppManifestTests`; the WebP test
  asserts the codec's reason, or WIC's unknown-format reason without the codec; `WriteFloatTiff`
  refuses two channels. After them: the gate `== OK in 29s`, 283 tests; `stacked_GraXpert.tiff`
  still equals the stored levels on 100 % of the samples, median 67.
