# Add false colours to the `--histeq` picture, after Siril's rainbow table

## Context

`--histeq` and the window's Histogram tab render a frame the way Siril 1.4.4's "Histogram"
display does (ADR 0025). Siril has a second toggle beside that display, **color-map** (the
"rainbow" / false-colour rendering): the equalised 8-bit layer is passed through a 256-entry
hue table. The owner asked for the same mode in FlatCheck, after Siril's algorithm.

Measured on 2026-10-26 (the Siril 1.4.4 source, tag `1.4.4`, `src/gui/image_display.c` and
`src/algos/colors.c`; copies in the session scratchpad `siril-src/`):

- `make_index_for_rainbow`: for `i` in 0..255, `h = 300/360 - i * (300/360)/255`, `s = v = 1`,
  `hsv_to_rgb(h, s, v)` (integer sectors, `i = (int)h`, `p/q/t`), each channel
  `round_to_BYTE(x * 255)`. Level 0 is **magenta**, not black; 51 blue, 102 cyan, 153 green,
  204 yellow, 255 red; 1 is (250,0,255), 64 (0,65,255), 128 (0,255,125), 254 (255,5,0). The
  `rgb_to_hsv` call in that function is dead code (h, s, v are overwritten).
- It is applied in `remap(vport)` **after** the histogram-equalisation LUT and after the
  negative toggle, per layer (vport). The RGB tab (`remaprgb`) does not colour the composite:
  it takes the R byte of the R layer's colour, the G byte of the G layer's, the B byte of the
  B layer's (`(bufr & 0xFF0000) | (bufg & 0xFF00) | (bufb & 0xFF)`).
- On the real masters (console `--histeq` PNG, then the table in Python): the Samyang ISO1600
  f/2 sky master shows round, slightly off-centre rings of equal rank; the Tokina tablet +
  paper master shows its rounded-square shape at a glance, where the grey picture is a blur;
  the NEF shows coloured grain. Siril's literal RGB mix gives colours outside the palette
  wherever the channels differ in shape (the NEF), and nothing can be read from them; the
  colouring of one layer is clean.
- Cost: a 256 x 3 table and one pass; a coloured full-resolution raster is 3 bytes per pixel,
  so the Mosaic view on a 24 MP frame is 72 MB and a channel 18 MB, against ~18 MB for all
  the grey rasters today (ADR 0025).

## Owner's decisions (2026-10-26, all five recommendations accepted)

1. False colours apply to the **one-layer views only**: Mosaic, R/G1/G2/B, mono. On the RGB
   composite the chip is disabled and the legend says why. Not Siril's literal RGB mix, not
   the mean of the four.
2. The console gets a **modifier flag `--rainbow`** beside `--histeq` (Siril's own name for the
   state), which writes the one-layer view in false colours: the Mosaic on a CFA frame, the
   channel on mono.
3. The console **shrinks first and colours after**: every colour in the file is a level of the
   table. The window colours the full-resolution raster.
4. The legend is **a sentence from the core** (`HistEqReport`) under the picture and in the
   console report: magenta = lowest, blue, cyan, green, yellow, red = highest. No colour bar.
5. The window's toggle is a **chip "False colours" / "Culori false"** beside the view chips,
   off by default, kept across view and file changes within the session. Coloured bitmaps go
   in the per-view cache; the peak with the coloured Mosaic on a 24 MP master is measured in
   the task and recorded in the ADR (if it hurts, the coloured Mosaic leaves the cache).

Judgment calls not put to the owner (stated here): `--rainbow` without `--histeq` is a parse
error ("--rainbow goes with --histeq"), unlike `--sky`, because it is a modifier of one mode's
picture and silently writing the plain picture would hide a typo. The flag is not a mode, so
R-003's dispatch order and the `VersionTests` mode lists do not change. No `validation.json`
case: the mode produces a picture, not a figure (as ADR 0025).

## Requirement (draft, step 1 of /new-feature)

| ID | Status | Requirement |
| --- | --- | --- |
| R-104 | draft | `--histeq --rainbow` and the window's "False colours" chip colour a one-layer equalised raster through Siril 1.4.4's rainbow table: `h = 300/360 - i * (300/360)/255`, `s = v = 1`, `hsv_to_rgb` with integer sectors, `round_to_BYTE`; level 0 is magenta (255,0,255), 51 blue, 102 cyan, 153 green, 204 yellow, 255 red. It applies after equalisation to the Mosaic, the channel and the mono views, never to the RGB composite; the console writes the Mosaic on a CFA frame and the channel on mono, shrunk first and coloured after, and prints the legend. `--rainbow` without `--histeq` is a parse error, exit `1`. Exit `0` when the picture was written, `1` otherwise; no verdict. |

Goes under "Mode 5: histogram equalisation" in `docs/requirements.md`.

## Steps, in order

Branch `claude/Add-false-colours-to-histeq`, worktree
`.claude/worktrees/Add-false-colours-to-histeq` (short, the Windows path limit).

1. **Requirement.** R-104 as above, `draft`. Save this plan as
   `docs/plans/2026-10-26-add-false-colours-to-histeq.md`.
2. **ADR 0063** `proposed` → `accepted` on the owner's plan approval (the skill's step 3: a
   choice answered in this session). Title: "The histeq picture's false colours are Siril's
   rainbow table on one layer, never on the composite". Context: the measurements above;
   decision: the five points; rejected: Siril's literal RGB mix (off-palette colours on the
   NEF), the mean of the four coloured, colouring before shrinking (mean of colours), a colour
   bar; consequences: the tests below, the measured peak.
3. **Failing tests** (run them, paste the red output):
   - `FlatCheck.Tests/HistEqAnalyzerTests.cs`: `R104_TheRainbowTableIsSirils` (Theory on the
     ten pinned levels), `R104_AColouredRasterIsTheTableAtEveryPixel` (a 256-level ramp).
   - `FlatCheck.Tests/RunsTests.cs`: `R104_TheConsoleColoursTheShrunkMosaicOnCfaAndTheChannelOnMono`
     (`HistEqRun.Execute(path, w, rainbow: true).Png` decodes to the table applied to
     `r.Mosaic` / `r.Channels[0].Equalised`, same size as the plain picture).
   - `FlatCheck.Tests/HistEqReportTests.cs`: the display line says false colours; the legend
     names magenta first and red last, in both languages.
   - `FlatCheck.Tests/OptionsTests.cs`: `--rainbow` parses; `--rainbow` without `--histeq`
     fails with the message.
   - `FlatCheck.Tests/GoldenOutputTests.cs`: `R104_Histeq_Rainbow_WritesTheColouredMosaic`
     (the CFA fixture, `--histeq --rainbow`, exit 0, `AssertGolden("histeq_rainbow", ...)`);
     the scenario added to `EnglishOutputTests` (`Scenarios`, `Arguments`).
   - `FlatCheck.Wpf.Tests/HistEqTabTests.cs`: the chip gives another bitmap, cached per
     (view, colour); on RGB the chip is unavailable and the image is the plain composite; the
     state survives a new `Populate` (file change) and a `Rebuild`; `Reset` clears it.
   - `FlatCheck.Wpf.Tests/WindowSmokeTests.cs`: a `tab-histeq-rainbow` screen.
4. **Core** (`FlatCheck.Core`, nothing computed elsewhere):
   - `Analysis/HistEqAnalyzer.cs`: `internal static class RainbowLut` beside `HistEqLut`:
     the 256 x 3 table built exactly as `make_index_for_rainbow` + `hsv_to_rgb` (private
     `HsvToRgb`, `HistEqLut.RoundToByte` reused), `Colour(byte[] grey) -> byte[] rgb`
     (3 bytes per pixel, the layout `RasterBitmaps.Rgb` and `PngWriter.Encode` take).
   - `Runs/HistEqRun.cs`: `Execute(path, report, bool rainbow = false)`; when set, `Png` is
     `EncodeRainbow(r)` = `PngWriter.Encode(RainbowLut.Colour(r.IsCfa ? r.Mosaic :
     r.Channels[0].Equalised), r.W, r.H)` (the shrunk Mosaic is the channels' size by
     construction). `EncodeRainbow` internal for the tests; `RunsTests` already pins that
     `Execute` returns the same bytes as the encoder.
   - `Report/HistEqReport.cs`: `Write(r, path, w, rainbow)` and `DisplayLine(r, rainbow)`
     add `, false colours (Siril's rainbow table)`; `RainbowLegend()` the sentence of
     decision 4, with "the RGB view keeps its own colours" for the window; `ViewCaption`
     unchanged.
5. **Console** (`FlatCheck/`): `Options.Rainbow` in `BoolFlags`, the cross-argument check
   after the loop; `FlatCheckCli.RunHisteq` passes it; `WriteUsageEnglish` /
   `WriteUsageRomanian` get the flag line under `--histeq` (`help.txt` and `en_help.txt`
   change by that line only: intended, reported at wrap-up).
6. **Window** (`FlatCheck.Wpf/`):
   - `ViewModels/HistEqViewViewModel.cs`: `bool Rainbow` (raises `Changed`),
     `bool RainbowAvailable => Selected.Kind != Composite`, raised on `Select` and `Rebuild`;
     the value survives `Rebuild`.
   - `ViewModels/HistEqTabViewModel.cs`: the cache keyed by `(option, rainbow)`; a coloured
     view is `RasterBitmaps.Rgb(RainbowLut.Colour(grey), w, h)`; `Legend` property
     (`HistEqReport.RainbowLegend()` when the picture is coloured, else `""`); `Reset`
     clears the cache and the chip.
   - `Views/HistEqView.xaml`: a `CheckBox` after the radio chips (the `Sky` one in
     `ExposureView.xaml` is the pattern), `{l:T En='False colours', Ro='Culori false'}`,
     `IsChecked` → `View.Rainbow`, `IsEnabled` → `View.RainbowAvailable`; a `Faint`
     `TextBlock` under the caption bound to `Legend` with `NotNullToVis`.
   - `Session/SessionMessages.cs`: one `HistEqGuideRows` row, "the False colours chip":
     contours of equal rank, magenta → red, why not on RGB.
7. **Goldens.** Capture `histeq_rainbow` and `en_histeq_rainbow` with
   `FLATCHECK_UPDATE_GOLDEN=1 ... --filter "GoldenOutputTests|EnglishOutputTests"`; read them
   whole; `git status` must show the two new files plus `help.txt` / `en_help.txt` changed by
   the usage line only. `histeq_cfa`, `histeq_mono` and the 43 parity goldens must not move.
8. **Documentation.** `docs/design/siril-histeq-source.md`: quote `make_index_for_rainbow`,
   `hsv_to_rgb`, the `RAINBOW_COLOR` case of `remap` and `remaprgb`'s masking line, with the
   same "true for tag 1.4.4" caveat; `docs/design/modes-histeq.md`: a section on the false
   colours (one layer only, shrink then colour, the cache and the measured peak), `Covers:`
   unchanged. `README.md`: §5 (what the chip and `--rainbow` do, the legend, why not on RGB),
   the "Opțiuni" block (the flag line), the guide mirror table (the new row; the README says
   the two are kept in step by hand). `CHANGELOG.md` `[Unreleased]` / `Added`. Requirement
   R-104 → `implemented`.
9. **Measure and close.** Window peak memory and the colouring time with the coloured Mosaic
   on the Samyang 24 MP master (against ADR 0025's 690 MB), into the ADR's consequences;
   `/validate` (Analysis/ and Report/ were touched; the figures must not move, the change is
   additive) with the drift table pasted; `scripts/check-design.ps1 -Since main`; the gate;
   `/wrap-up`, commit on the branch, propose the merge.

## Verification

- Step 3: `dotnet test FlatCheck.sln --filter "FullyQualifiedName~R104"` red, output pasted.
- Steps 4-6: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -SkipTests`
  green after each file; then `--filter "FullyQualifiedName~R104"` green.
- Step 6 by eye: `FLATCHECK_DUMP_SCREENS=<dir> dotnet test FlatCheck.Wpf.Tests --filter
  WindowSmokeTests`, open `tab-histeq-rainbow.png`; and the window on the Samyang session
  (`dotnet run --project FlatCheck.Wpf -- "E:\Photografie\Astro\Flats and Darks\Samyang\ISO1600\f2.0-sky"`),
  the chip on Mosaic, a channel and RGB.
- Step 7: the console on the Tokina paper master with `--histeq --rainbow`, the PNG compared
  by eye with the Python rendering of 2026-10-26 (the same table, so identical pixels).
- Step 9: the full gate `scripts/verify.ps1` green; `/validate` drift table with no moved
  figure.

## Outcome (2026-10-26)

Followed as written, with three deviations:

- **The window was not driven by hand on the Samyang session** (no desktop automation in the
  session): the `tab-histeq-rainbow` smoke screen and `R104_TheFalseColoursChipColoursOneLayerViewsAndNotTheComposite`
  cover the chip on Mosaic, a channel and RGB instead.
- **The console's PNG was not compared pixel for pixel with the Python rendering**: that
  rendering coloured the composite's green byte, the console colours the shrunk Mosaic, so
  the two are different rasters. Checked instead: every pixel of the console's file is a row
  of the table (157 distinct colours, all in it) and the picture by eye.
- **The window's cost was measured through a temporary test** in `FlatCheck.Wpf.Tests`
  (deleted, not committed) that ran `HistEqRun.Execute` on the Samyang master and toggled
  the chip in `HistEqTabViewModel`; the figures are in `modes-histeq.md` and ADR 0063.

A fact found after the decisions, put to the owner at wrap-up: the console's coloured Mosaic
on a CFA frame uses the middle of the table only (blue to yellow on the Tokina master),
because a shrunk block averages the ranks of three colour populations; ADR 0063 records it. Answered
2026-10-26: kept, with three more confirmations (the Restated note in ADR 0063); nothing in
the code changed.
