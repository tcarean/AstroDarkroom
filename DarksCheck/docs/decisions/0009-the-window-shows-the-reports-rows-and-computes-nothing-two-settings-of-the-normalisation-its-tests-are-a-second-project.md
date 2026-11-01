# 0009. The window shows the report's rows and computes nothing; the two settings of the normalisation; its tests are a second project

Date: 2026-10-16
Status: accepted 2026-10-16

## Context

Plan step 4 asked for a WPF window "like FlatCheck": the same licence, versioning, dark
theme, About window and layout, with the tabs Rezumat, Cadre, Harti, Pixeli fierbinti,
Lights, Index and Raport, the settings the files do not carry (threads, the ISO factor, the
doubling constant, the frame cap), progress and cancel, and the export. The plan's decision 1
copies LightsCheck's WPF pieces rather than sharing a library. The console already prints
every sentence and every figure (R-025), and the rule "Computed only in the core" forbids a
second place that decides numbers or words; until this step `DarkReport.Write` wrote its lines
straight into the text, so a window had nothing to bind but the text. ADR 0004 left two
constants to become settings of the window: the ISO factor of the normalisation and the
degrees per doubling of the dark current.

## Decision

- **The report produces rows, the console prints them.** `DarkReport.Sections` returns the
  checks 2 to 9 and INDEX as `ReportSection(Title, Rows)` of `ReportRow(Label, Value)`;
  `HeaderRows`, `FrameRows` and `LightRows` do the same for the darks' header and the two
  tables; `Write` prints exactly those rows with the same padding as before, so the goldens
  did not move. A row's verdict word (OK, ATENTIE, NEMASURAT) is read off its value, or off
  its label when that is ATENTIE (`ReportRow.Verdict`, `Rest`), in the core, and
  `InventoryReport.HeaderRows` gives the session lines the same way, so the window shows the
  chip and the rest of the text without parsing prose of its own. The window binds the rows as cards; the Raport tab
  holds the console's text byte for byte and the export writes the same files as `--out`
  (R-074). What the window adds is arrangement: the maps as images (the same raster
  `DarkOutputs.MapRaster` gives the PNG), the per-frame charts (the warming, the residual
  sigma, the random banding, the noise curve over its expected line, the lights' ratio with
  the four verdict bounds as guide lines quoting `Thresholds.cs`), the label with a copy
  button, the partner picker.
- **The two settings are a record of the core, `Normalisation`** (the gain per ISO stop,
  2 = linear, and the degrees per doubling, `Th.DoublingCelsius`), carried by `DarkOptions`,
  read by `FrameStack.Load` for every stack, by the lights and the partner through the darks'
  analysis, and by every place that turns a ratio into degrees (`Normalisation.Degrees`). The
  console runs with the defaults and gets no option for them; the report's line `normalizare`
  names a gain other than 2. With the defaults every figure of V-001 to V-006 is unchanged.
- **The window remembers its settings** (R-075): threads, frame cap, gain per stop, degrees
  per doubling and the partner folder, as text in `%LOCALAPPDATA%\DarksCheck\settings.json`,
  the only thing it writes outside the export folder; parsed with the invariant culture, a
  value that is not a positive number falls back to the core's default. A partner chosen after
  a run is applied on the next run, which the Index tab offers (`Analizeaza din nou`).
- **The window's tests are a project of their own**, `tests/DarksCheck.Wpf.Tests`, because
  they must target `net9.0-windows` to load WPF while `DarksCheck.Tests` stays portable; they
  link the session fixture and the synthetic frames from `DarksCheck.Tests` rather than
  copying them; `RepoInvariantTests` reads their sources for the `R###_` names. The same three
  local deviations as LightsCheck's ADR 0008: the view-models silence CA1822, the window
  project allows unsafe blocks for the one `LibraryImport` that darkens the title bar, the
  window tests silence CA1305 for the linked FITS fixture (a WPF project cannot have the
  `InvariantGlobalization` switch; `App.ForceInvariantNumbers` pins the culture by hand,
  FlatCheck's ADR 0037).
- **The copied pieces stay copies** (theme, converters, palette, title bar, MVVM base, icon
  helper, raster bitmaps, `SeriesChart`, the About window, the STA fixture): marked in their
  headers, LightsCheck's to improve first, as the readers are (ADR 0003). `SeriesChart` gained
  a dashed `Reference` series here, for the noise curve's expected line.

## Alternatives rejected

- **The window formatting its own figures from the analysis records.** A second place
  deciding decimals and words; the Raport tab could disagree with the console.
- **The view-model parsing the console text into rows.** It would re-derive what the report
  knows; the rows are the report's to give (LightsCheck's ADR 0012 made the same move).
- **Console options for the two settings.** Nothing in the console's use needs them; the
  defaults are the D3300's, and a second place to set them would need its own golden.
- **The partner applied at once when chosen.** A run takes a minute; the user chooses, then
  decides when to run.
- **A shared WPF library with LightsCheck and FlatCheck.** Decision 1 of the plan.

## Consequences

`WindowSmokeTests` renders every tab with a real result and fails on any binding error; with
`DARKSCHECK_DUMP_SCREENS=<folder>` it saves a PNG per screen. `CultureTests` prove the report
identical under `ro-RO`. `IconTests` tie both executables to `Icon/darkscheck.ico`, drawn by
`Icon/make_icon.py`. The design note `docs/design/window.md` holds the mechanics; the rows
and the normalisation record are described in `dark-analysis.md`. The release publishes the
window as `DarksCheck.Wpf.exe` beside the console (`kit.json`).
