# Show the axes, the focus verdict and the dithering tables in the window

Branch `claude/Show-the-axes-the-focus-verdict-and-the-dithering-tables-in-the-window`, its
worktree under `.claude/worktrees/`. Requirements R-036 (report) and R-037 (window), drafted and
accepted in chat on 2026-10-09; ADR 0012 (proposed; this plan's approval makes it accepted). The
failing tests are written: `R036_*` in `SeriesTests` and `SeriesAnalyzerTests`, `R037_*` in
`MainViewModelTests` (plus `R035_*` adjusted to the paragraph list); V-010 in
`docs/validation.json` with `expected: null`.

## Context

Reading the window on the Veil session, the owner asked for: the mount axis in the `dX`/`dY`
headers of Cadre; the RA/DEC sentence of the Respinse explanation on its own line in bold; a
verdict on the FOCUS card (the only card without one; ROUA and DITHERING close with a `verdict`
row); the DITHERING figures of the Dithering tab as tables. ADR 0008 keeps every word in the
report, so the report gains structured outputs (tables, paragraphs, labels) and the window
binds them; the console keeps its one-line rows and gains only the FOCUS verdict row (ADR 0012).

## Steps

### 1. Core, `Report/SeriesReport.cs` (words and shapes)

- Records next to `ReportRow`: `ReportParagraph(string Text, bool Emphasis)` and
  `ReportTable(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows)`.
- `AxisLabels(SeriesAnalysis s)` → `(string X, string Y)`: `("X = DEC", "Y = RA")` for `Axis.Y`,
  `("X = RA", "Y = DEC")` for `Axis.X`, `("X", "Y")` otherwise (the switch moves here from
  `MainViewModel.Populate`). `PositionHeaders(s)` → `("dX (DEC)", "dY (RA)")`, the reverse, or
  `("dX", "dY")`.
- `FocusRows`: append `new("verdict", ft, fv)` from the existing `Focus(s, r)` (the pair the
  summary row uses), so the console prints `verdict : BUN - FWHM median ...` like ROUA's.
- `RejectedExplanation` becomes `IReadOnlyList<ReportParagraph>`: paragraph 1 the elongation
  and the two thresholds, 2 where the marginal frames are, 3 what the trail and its angle are
  and when the axis word is given, 4 (Emphasis) `Dara pe RA vine din urmarire: ... Dara pe DEC
  vine din alinierea polara, vant sau o atingere a monturii. Oblica: amandoua, sau o lovitura.`
  Same words as today, split; still built once from `Th`.
- `DitherTables(SeriesResult r)` → five `ReportTable`s, cells with the existing helpers
  (`RunReport.F`, `Px`, `Opt`, `Minutes` without its leading `, ` for the minutes column):
  1. `cat de des`: columns `la fiecare`, `de cate ori`; one row per `FramesBetweenMoves` key,
     sorted: `2 cadre` / `2 ori` (`1 cadru`, `1 data` in the singular, as `HowOften` words it);
     no rows without moves.
  2. `cat de mult`: `marime`, `miscari`; first row `median` / `Px(MedianMove)`, then one per
     `Sizes` class: `5-10 px`, `10-20 px`, `20+ px` / count.
  3. `directie`: `axa`, `numai pe ea`, `semn +`, `semn -`; rows `X = DEC` (or `X = RA`, or `X`)
     with `MovesOnXOnly`, `MovesXPlus`, `MovesXMinus`, the Y row likewise, then `ambele` /
     `MovesOnBoth` / `` / ``.
  4. `acoperire`: `raza`, `cu alt cadru sub raza`, `pozitii distincte`, `grupuri de 3+ consecutive`;
     rows `3 px` / `WithinTight` / `-` / `-`, `5 px` / `WithinMid` / `DistinctMid` / `RunsMid.Count`,
     `10 px` / `WithinLoose` / `DistinctLoose` / `RunsLoose.Count`.
  5. `grupuri in acelasi loc`: `raza`, `cadre`, `cate`, `minute`; one row per run at 5 px then
     at 10 px (`5 px` / `8-12` / `5` / `11`), or `5 px` / `niciunul` / `-` / `-` for a radius
     without runs.
- `DitherScalarRows(r)`: `pauze > 5 s` (as in `DitherRows`), `cel mai lung interval intre doua
  miscari` (as is), `acoperire` → `cel mai apropiat alt cadru median 0.6 px; izolate (>= 10 px
  de orice): 0`, `intindere` → `X 40 px, Y 16 px`, `verdict` (as is). `DitherRows` (the console)
  is untouched; the shared pieces become small private helpers so the words stay in one place.

### 2. Window

- `ViewModels/MainViewModel.cs`: public records `ParagraphVm(string Text, bool Emphasis)` and
  `TableVm(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows)`;
  properties `DxHeader`, `DyHeader` (from `PositionHeaders`), `MapXLabel`/`MapYLabel` from
  `AxisLabels`, `RejectedExplanation` as `IReadOnlyList<ParagraphVm>` (a static map of the
  core's list), `DitherRows` from `DitherScalarRows`, `DitherTables` from `DitherTables`;
  `RaiseAll` names the new ones.
- `Views/FramesView.xaml`: the two header `TextBlock`s bind `DxHeader` and `DyHeader`.
- `Views/RejectedView.xaml`: an `ItemsControl` over `RejectedExplanation`, each item a wrapped
  `Muted` `TextBlock` whose style has a `DataTrigger` on `Emphasis` setting `FontWeight` Bold
  and the normal foreground, so the bold line stands out.
- `Views/DitherView.xaml`: the right card keeps the `DitherRows` list and gains, between the
  rows, an `ItemsControl` over `DitherTables`; each table is its `Title` (the `Head` style),
  then a header row and the rows, each row an `ItemsControl` with a horizontal `StackPanel`
  panel and `TextBlock` cells of fixed width (first column 130, others 110), the header in the
  `Head` style and the cells in `Cell`. Order on the card: pauses row, longest-stretch row,
  tables 1 to 3, coverage row, tables 4 and 5, spans row, verdict row is simpler as: the
  rows first, then the tables; keep that (rows, then tables) to avoid interleaving two lists.
- `SummaryView.xaml`: nothing; the FOCUS card's new row gets its chip from the `ReportRow`
  template.

### 3. Tests, goldens, validation

- `tests/LightsCheck.Tests/ValidationTests.cs`: `case "tables"`: expected `Null` → run the
  series, print every table's title and rows to the console (so the figures can be recorded)
  and return; else `CheckTables`: for each property of `expected` (a table title) the rows,
  each an array of cell strings, equal `DitherTables(r)`'s rows of that title.
- Regenerate the two series goldens; expected diff: one added line each,
  `verdict : BUN - FWHM median ...` at the end of FOCUS. Any other changed line is a finding.
- Measure V-010 on Veil: run the validation case (`Category=Validation`, filter V-010) and
  copy the printed rows into `expected`, `measuredOn`, and the V-010 row of `validation.md`;
  `Report/` is numeric code by `docs/skills.md`, so `/validate` runs on all ten cases and the
  drift table is pasted.

### 4. Record

- `docs/requirements.md`: R-036, R-037 → `implemented`. ADR 0012 → `accepted 2026-10-09`.
- `docs/design/series-analysis.md` (the report section: the tables, paragraphs, labels and
  scalar rows beside the rows); `docs/design/window.md` (the new bindings, how a table is laid
  out, the bold paragraph).
- `README.md`: FOCUS gets a verdict line; the Cadre headers name the axis; Dithering tab shows
  tables; Respinse explanation with the bold line.
- `CHANGELOG.md`: Added (tables, bold line, axis headers), Changed (FOCUS verdict row in the
  console too).
- Save this plan as `docs/plans/2026-10-09-show-the-axes-the-focus-verdict-and-the-dithering-tables.md`;
  then `/wrap-up`.

## Verification

1. `scripts/verify.ps1 -SkipTests` green while editing; the full gate before the commit.
2. `dotnet test LightsCheck.sln -c Release --filter "FullyQualifiedName~R036|FullyQualifiedName~R037|FullyQualifiedName~R035"` green.
3. The golden diffs read line by line.
4. `LIGHTSCHECK_DUMP_SCREENS` with `WindowSmokeTests`: look at `tab-summary.png` (FOCUS chip),
   `tab-frames.png` (headers), `tab-dither.png` (tables), `tab-rejected.png` (bold line).
5. The Veil run for V-010 with its printed rows pasted.

## Outcome (2026-10-09)

Followed as written. Two things to note. ROUA already closed with a `verdict` row, so only
FOCUS gained one, as a `verdict` row rather than the chip on its trend row the requirement draft
first said (ADR 0012 records why). The table cells of the Dithering tab are 118 px wide with a
wrapped header, in the theme's `Faint` style for headers and the mono font for cells, since the
`Head` and `Cell` styles live in `FramesView.xaml`. V-010 recorded the five tables on Veil from
the validation test's own print-out (expected null); its figures are V-008's, arranged.
