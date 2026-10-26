# Add the histogram and false-colour display modes for a FITS, as Siril shows them

Branch `claude/Add-the-histogram-and-false-colour-modes`, worktree
`.claude/worktrees/Add-the-histogram-and-false-colour-modes` (40 characters: under the Windows
path limit met before). Approved by the owner on 2026-10-26, after the four questions below were
answered in the same session.

## Context

A linear stack shows nearly black in the tool (ADR 0029: "nothing stretches ... and that is
accepted"), so two variants of one processing cannot be told apart by eye, only by the readout.
The owner asked on 2026-10-26 for an option, for a FITS in its linear state, to compare the two
files through Siril's "Histogram" display mode (histogram equalisation, per RGB channel) and
through Siril's false colours (the rainbow colour map), both "implemented as in Siril", looking
at FlatCheck's implementation. ADR 0029 had deferred exactly this: "a stretch on a key: a feature
of its own (requirements, a decision on which stretch)".

What FlatCheck does (its ADR 0025 and 0063, `docs/design/siril-histeq-source.md`, Siril tag
1.4.4): one histogram per layer of 65 536 bins on the 16-bit value, zeros skipped but counted in
the denominator, `index[0] = 0`, `index[i] = round_to_BYTE(cumulative(i) / pixels * 255)` with
the cumulative including bin `i` and `round_to_BYTE(x) = (byte)min(255, (int)(x + 0.5))`; float
data quantised as `round(v * 65535)`. The rainbow is a hue from 300° (level 0, magenta) to 0°
(level 255, red), saturation and value 1, through Siril's `hsv_to_rgb`, applied after the
equalisation to one layer only, never to the RGB composite (the composite would mix the R byte of
R's colour with the G byte of G's and leave the palette). FlatCheck averages a three-plane FITS
into one plane and bins on the layer's own min..max; this project binds to Siril itself (absolute
16-bit bins), since Siril is the display the tool is compared against (ADR 0039).

What the code holds today: after a load only the frozen 8-bit Bgra32 bitmap survives
(`LoadedImage(ImageInfo, BitmapSource)`); `FitsRowDecoder.Convert` turns each sample straight
into a level through `FitsScale.Level` (`(int)(v + 0.5) >> 8`), one loop per BITPIX, and
`FloatSamples.ToBgra32` does the same for a float TIFF. A 23 MP float FITS decodes in 0.59 to
0.61 s a pass (ADR 0029), and is already decoded twice when it counts to 65 535.

The owner's answers of 2026-10-26, each the recommended option:

1. **Which files**: every FITS and every float TIFF (the files "shown as stored", ADR 0029 and
   0035). No linearity detection: no keyword says it, and a threshold would be a guess.
2. **False colours on an RGB file**: the mean of the three planes, equalised as one layer, then
   the table; a mono file is itself the layer.
3. **Switching**: both files are decoded again with the mode, zoom, pan and divider kept; no
   16-bit copy (+138 MB per 23 MP RGB image) and no second bitmap per mode (+89 MB each).
4. **Controls**: two toolbar buttons, "Histogram" and "False colours", lit when active and
   disabled when the pair does not allow them, plus the keys `H` and `F` anywhere in the window.

Decided here, stated as assumptions (the owner's approval of the plan is the yes):

- The readout under the cursor keeps reading the shown bitmap (R-051): in histogram mode it
  shows the equalised levels, which is what is on screen; in false colours it shows the one
  level the colour stands for (`A 143`), found by inverting the rainbow table, which is one to
  one (consecutive levels differ by about 5 in one channel).
- The mode asked for holds across `Ctrl+O`: the next pair is loaded with it when both files are
  shown as stored, and linear otherwise, with the buttons disabled.
- The histogram's bin is the 16-bit value the level is already computed from, and bin 0 is
  skipped whole (a sample exactly 0, and a positive one below 1/131 070 that rounds to 0) and
  counted in the denominator. Siril skips only the exact zeros; the difference is measured on
  the real file in step 3 and recorded in the ADR.
- No `--mode` command-line option: not asked for.
- Integer PNG and TIFF files keep WIC's conversion and the linear display, whatever mode is
  asked (ADR 0022 stands: nothing after the load reads the pixel format; the availability
  reads what the loader did, a new `ImageInfo.ShownAsStored`).

## Requirements (step 1 of /new-feature: draft, accepted with the plan)

New area "Display modes", `docs/requirements.md`, status `draft` until the tests exist:

- **R-110** The pair is shown in one of three display modes: `Linear` (as today), `Histogram`
  and `FalseColours`. `Histogram` and `FalseColours` can be asked for only when both files are
  shown as stored (a FITS, R-015, or a float TIFF, R-091; `ImageInfo.ShownAsStored`); for any
  other pair the buttons are disabled, the keys do nothing and the mode is `Linear`. Example:
  `result_3813s.fit` against `result_3813s_Graxpert_AI.fit` allows all three;
  `result_3813s.fit` against `de_printat.png` allows `Linear` only.
- **R-111** In `Histogram` each channel is equalised on its own histogram as Siril 1.4.4's
  "Histogram" display does: 65 536 bins on the 16-bit value of R-015/R-091 (`FitsScale.SixteenBit`),
  bin 0 left out of the counts but counted in the pixels, the lookup `index[0] = 0` and
  `index[i] = RoundToByte(cumulative(i) / pixels × 255)` with bin `i` included and
  `RoundToByte(x) = min(255, (int)(x + 0.5))`; a gray file is equalised once and shown on all
  three channels. Example: a 4-pixel plane holding the 16-bit values 1, 2, 2, 3 shows the levels
  64, 191, 191, 255; a plane of 16-bit values 1, 2, 2, 3 with six more pixels at 0 shows 26, 77,
  77, 102 and 0.
- **R-112** In `FalseColours` one layer is equalised as in R-111 and each level is shown as
  Siril's rainbow colour: hue `300° − level × 300°/255`, saturation 1, value 1, through Siril's
  `hsv_to_rgb`, `RoundToByte` on each channel. The layer is the plane of a gray file and, for an
  RGB file, the mean of its three 16-bit values rounded to the nearest (`(r + g + b + 1) / 3`).
  Example: level 0 is `255 0 255`, 51 is `0 0 255`, 102 is `0 255 255`, 153 is `0 255 0`, 204 is
  `255 255 0`, 255 is `255 0 0`, 1 is `250 0 255`, 64 is `0 65 255`, 128 is `0 255 125`, 254 is
  `255 5 0`.
- **R-113** A switch decodes both files again in the new mode behind the progress overlay, one
  after the other as an open does (R-003, R-006: a switch while a pair is opening or switching
  does nothing), then puts the new bitmaps in place of the old with the zoom, the pan and the
  divider unchanged and the minimap redrawn; the outgoing pair is released first (R-081). A
  float file that counts to 65 535 is counted on that scale (R-016). The mode holds for the next
  pair opened when it allows it, otherwise the next pair is `Linear`. Example: at 152 % with the
  divider at 0.3 of the width, `H` reloads the real pair and leaves both at 152 % and 0.3.
- **R-114** The toolbar has, after `100%`, the buttons `Histogram` and `False colours`, each lit
  while its mode is shown; clicking the lit one returns to `Linear`. The keys `H` and `F`,
  without a modifier, do the same wherever the focus is. The status bar's pair line (R-050)
  ends with `  ·  Histogram` or `  ·  False colours` in those modes and with nothing in
  `Linear`; the hint (R-054) gains ` · H histogram · F false colours`. In `FalseColours` the
  readout of R-051 shows one number per image, the level the colour stands for: `A 143`,
  `B 141`, and `—` where an image does not reach.

Rows amended: R-051 (the false-colour readout), R-054 (the hint), R-060 (the toolbar order),
R-063 (the shortcuts). The Romanian wordings: `Histogramă`, `Culori false`,
` · H histogramă · F culori false`.

## Decision (step 3: ADR 0040, `proposed` in the file, `accepted` by this approval)

`docs/decisions/0040-the-display-modes-mirror-sirils-histogram-and-rainbow-at-decode.md`:
the modes, computed at decode time in Core over the 16-bit row (Siril's absolute bins, not
FlatCheck's min..max), the false colours on the mean of the planes, the switch as a re-decode,
the availability as `ShownAsStored`. Alternatives rejected, with the owner's answers and the
figures above: a 16-bit copy per image; a bitmap per mode; a linearity heuristic; a channel
picker; colouring the composite; equalising the 8-bit levels (a linear stack has nearly all of
them in 0 to 3); a stretch chosen by the tool. It cites ADR 0029 ("a feature of its own") and
says why ADR 0022 is not broken.

## Steps

0. **Worktree and plan.** `git worktree add .claude/worktrees/Add-the-histogram-and-false-colour-modes -b claude/Add-the-histogram-and-false-colour-modes main`
   from `E:\Photografie\Code`; copy the five validation files into the worktree's
   `tests/testdata/large/` (`docs/skills.md`, /validate); save this plan under `docs/plans/`.
   The requirement rows (`draft`) and ADR 0040 (`accepted 2026-10-26`, the choices answered in
   this session) go in with the first commit.

1. **Core: the 16-bit row and the lookup** (`src/ImageVisualCompare.Core/`).
   - `FitsScale.SixteenBit(double)`: the rounding and clamp `Level` does, returned as an int 0
     to 65 535 (NaN and negatives 0); `Level` becomes `(byte)(SixteenBit(v) >> 8)`. Tests: the
     existing `FitsScaleTests` plus the new values.
   - `FitsRowDecoder.ReadPlaneRow(int fileRow, int plane, Span<ushort> sixteenBit)`: the one
     loop per BITPIX, now producing 16-bit values and counting `SamplesAboveThreshold`;
     `ReadFileRow(fileRow, bgra)` keeps its signature and behaviour (`ReadPlaneRow` then the
     linear lookup), so R014 to R017 and R019 pin that nothing moved.
   - `FloatSamples.ToSixteenBit(ReadOnlySpan<float>, bool gray, Span<ushort>, double fullScale)`
     returning `above`; `ToBgra32` stays, written over it, so R091's tests pin it too.
   - New `DisplayMode.cs` (the enum), `SampleHistogram.cs` (65 536 counts, `Count(ushort)`
     skipping 0, `Pixels`, `ToLookup()`), `LevelLookup.cs` (`byte[65536]`; `Linear` = `i >> 8`;
     `RoundToByte`), `RainbowTable.cs` (the 256 colours, `Colour(level)`, `LevelOf(r, g, b)`),
     `DisplayRenderer.cs` (`DisplayRenderer(DisplayMode, int width, int planes)`;
     `NeedsCount`; `Count(ReadOnlySpan<ushort> planesRow)`; `Prepare()`; `Render(planesRow,
     Span<byte> bgra)`: the lookups per channel, the mean layer and the table for
     `FalseColours`, alpha 255, a single plane on all three channels).
   - Tests, Core: `SampleHistogramTests` / `LevelLookupTests` (R-111's two examples; `index[0]
     = 0`; rounding 63.5 → 64, 63.49 → 63, 300 → 255; a uniform ramp comes out a linear ramp;
     every output level holds the same share), `RainbowTableTests` (R-112's ten values;
     `LevelOf` inverts all 256), `DisplayRendererTests` (the channels are independent; the mean
     layer on RGB; the gray file; `Linear` equals `FitsScale.Level`), `FitsRowDecoderTests`
     (`ReadPlaneRow` per BITPIX, 8-bit 255 → 65 535, float 0.5 → 32 768, the rescaled file). All
     named `R111_`, `R112_`, or without an ID where they pin an internal.

2. **Imaging** (`src/ImageVisualCompare.Wpf/Imaging/`).
   - `IImageLoader.LoadAsync(string path, DisplayMode mode, CancellationToken)`; the
     parameterless overload stays as `Linear`. `ImageInfo.ShownAsStored` (default false; true
     from `FitsImageLayout.Describe` and from `WicImageLoader.Describe` when `FloatSamplesOf`
     applies, so `Probe` already knows). `LoadedImage.Mode`: the mode applied.
   - `FitsImageLoader.Load`: passes over the rows as `DisplayRenderer` asks: `Linear` as today;
     otherwise a counting pass at the first scale, a second counting pass when `IsRescaled`,
     then the rendering pass. `WicImageLoader.LoadFloat`/`Decode` the same over the float
     bands; the integer path ignores the mode and returns `Linear`. `RoutingImageLoader` passes
     it through.
   - `ImagePairLoader.LoadAsync(..., DisplayMode mode = Linear)`: after the two probes, the
     mode falls back to `Linear` unless both are `ShownAsStored`; `ImagePair.Mode` and
     `ImagePair.AllowsDisplayModes`.
   - Tests: `FitsImageLoaderTests` R110/R111/R112 on `FitsFixture` images (a 3-plane 16-bit FITS
     in `Histogram`, the bytes hand-computed; a gray float FITS in `FalseColours`; an RGB FITS in
     `FalseColours` uses the mean; a file in 0..65 535 counts on that scale; `ShownAsStored`),
     `WicImageLoaderTests` (a float TIFF in `Histogram`; a 16-bit PNG asked `Histogram` comes
     back `Linear`, `ShownAsStored` false), `ImagePairLoaderTests` R110 (the fallback when one
     file is a PNG; the mode reaches both loads; the recording fake gains the mode).

3. **Measure before the window** (the rule "Measured numbers"), on `result_3813s.fit` in the
   worktree, with a small xunit case under `Category=Validation` or a one-off console run, the
   output pasted into ADR 0040:
   - the linear decode after the refactor against ADR 0029's 0.59 to 0.61 s a pass (a
     regression above about 10 % means the lookup goes back into the per-type loop);
   - a `Histogram` and a `FalseColours` load of the file: seconds and private memory;
   - how many samples fall in bin 0 without being exactly 0 (the simplification above).

4. **Window** (`src/ImageVisualCompare.Wpf/`).
   - `Theme.xaml`: a `ToolToggleButton` style for `ToggleButton` built like `ToolButton`, with
     `IsChecked` showing the accent border and the hover background.
   - `MainWindow.xaml`: `HistogramButton` and `FalseColoursButton` after `ActualSizeButton`.
     `MainWindow.xaml.cs`: `_mode`; `SwitchModeAsync(DisplayMode)` guarded by `_opening`
     (ADR 0036), `LoadWithOverlayAsync` given the mode and, for a switch, a padding answer of
     yes; `ShowPair(pair, keepView)` releasing the old pair as today, then
     `Compare.ReplacePair(pair)`; the buttons' `IsChecked`/`IsEnabled` from the pair; `H` and
     `F` in `OnPreviewKeyDown`; the pair line and the readout per R-114
     (`RainbowTable.LevelOf`). `UiText`: `Histogram`, `FalseColours`, `HistogramSuffix` and the
     hint, English and Romanian (R101 checks both).
   - `CompareView.ReplacePair(ImagePair)`: the two sources and their pixel sizes, the thumbnail
     again, `Sync()`; no `ZoomToFit`, no `Divider.Reset`. Core does the arithmetic already;
     nothing new there.
   - Tests on the WPF thread (`MainWindowTests`, ADR 0032): R113 (a fake loader that records
     the mode; set a zoom and a divider fraction, send `H`, the pair reloads in `Histogram`
     with the same `Viewport.Scale` and `Divider.Fraction`), R114 (the buttons' checked and
     enabled states; a PNG pair ignores `H`; the pair line ends with the suffix; the
     false-colour readout). `ValidationTests` gains an optional `mode` field in a case.

5. **Figures.** `docs/validation.json`: V-005 (`result_3813s.fit` / `result_3813s_Graxpert_AI.fit`,
   `Histogram`) and V-006 (the same, `FalseColours`), `expected` null until measured, then the
   figures of a `/validate` run pasted into `docs/validation.md` with the date; V-001 to V-004
   run again (the linear path was refactored) and must not drift.

6. **Record.** Rows R-110 to R-114 `implemented`; `CHANGELOG.md` Added; `README.md`
   (Romanian): a section "Histogramă și culori false" under "Fișiere FITS", two rows in
   "Controale", the mode and the false-colour readout in "Ce îți arată bara de stare";
   `docs/design/imaging.md` (Covers: the five new Core files; a section "Display modes" on the
   passes), `rendering.md` (`ReplacePair`), `shell.md` (the toggle style, the keys),
   `code-map.md`, `testing.md` (the new test files); `docs/skills.md` /validate (V-005, V-006
   use the two FITS files already listed). Then `/wrap-up`: the gate, the application launched
   on the real FITS pair in all three modes with a screenshot of each looked at (the rule
   "Run the application"), the commit on the branch, the merge proposed.

## Verification

- `scripts/verify.ps1 -SkipTests` green while editing; the full gate before the commit.
- `dotnet test ImageVisualCompare.sln -c Release --filter "Category=Validation"` in the worktree
  with the five files copied: V-001 to V-004 unchanged, V-005 and V-006 measured.
- The application on `result_3813s.fit` vs `result_3813s_Graxpert_AI.fit`: `H` shows the two
  equalised stacks with the same zoom and divider, the status bar says `Histogram`, the readout
  reads the equalised levels; `F` shows the rainbow on both, the readout one level; `H` again
  returns to linear; `Ctrl+O` on a PNG pair disables both buttons. Against Siril 1.4.4 on the
  same file, the Histogram view and the rainbow of one layer look alike (a screenshot each).
- Memory: the status bar after three switches sits where it sat after the open, within the 60
  MB tolerance of R-080.

## Outcome and deviations (2026-10-26)

- Done as planned, in the order of the steps, in one session. The requirement rows went in as
  `draft` and became `implemented` as their tests appeared; ADR 0040 was written `accepted` at
  once, the four choices having been answered in the session before the plan.
- The linear path first went through the 64 KB table of `LevelLookup.Linear` and cost 0.67 to
  0.69 s a load against `main`'s 0.56 to 0.59 s, measured the same day in a throwaway worktree
  of `main`; the plan's rule for a regression above 10 % applied, and `DisplayRenderer`
  gained `RenderLinear`, a shift instead of the lookup: 0.52 to 0.54 s. The figures are in
  ADR 0040.
- `FloatSamples.ToBgra32` stayed, written over `ToSixteenBit` and the linear renderer, so the
  R091 tests of `FloatSamplesTests` did not change.
- `ImageInfo.ShownAsStored` is a positional parameter after `ValueRange`, so no existing
  construction changed.
- Test-side changes to existing tests, each because the seam changed and not for an
  expectation: `IImageLoader.LoadAsync` took the mode before the token, so the one call that
  passed a token positionally names it; the two fakes `RecordingLoader` and `StalledLoader`
  took the new signature; `R101_TheMainWindow_InRomanian` asserts the hint text, which R-054
  now extends with the two keys, so its expected string grew by that tail.
- The keys `H` and `F` are not tested on the WPF thread: a `KeyEventArgs` needs a presentation
  source a window never shown has none of. `ToggleModeAsync` is `internal` and tested; the
  keys were checked by launching the application and posting them to its window.
- The bin-0 simplification (every sample that rounds to 0 is skipped, where Siril skips the
  exact zeros only) is moot on `result_3813s.fit`: no sample is 0, negative or below
  1/131 070.
- Measured by the application on the real FITS pair with `PrintWindow` and posted keys: the
  four screenshots (linear, histogram, false colours, linear again) are described in the
  session's wrap-up; nothing is committed of them.
- The application's status bar showed the first switch adding about 270 MB that stayed until
  the next switch, where the headless probe (the same window, never shown, on the WPF test
  thread) showed nothing retained. The surface's images keep the outgoing bitmaps in their
  render content until the next render pass, after the synchronous release; `ShowPair` now
  shows the incoming pair, lets one render pass through and releases after it, for an open
  as for a switch. ADR 0040 supersedes ADR 0016 in that one part; R-008 and R-081 say the
  new order. Not in the plan: found by the rule "Run the application".
- Nothing calls `CompareView.Clear` once the release moved after the render pass, so it went; `MinimapView.Hide`, which only it called, stays as the minimap's own API, unused for now.
