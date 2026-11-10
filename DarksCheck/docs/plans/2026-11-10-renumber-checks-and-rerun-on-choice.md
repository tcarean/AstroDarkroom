# Renumber the checks in the Summary's order, one statement per line in two verdicts, rerun on a choice

## Context

The owner read the window and asked for four changes:

1. The Summary shows the checks as 9, 2, 3 ... 8 (ADR 0041 moved check 9 under the session
   card). Renumber them in the order they are shown, and make the numbers on the other tabs match.
2. Check 2's verdict (light leaks) is hard to read as one block: one statement per line.
3. Check 3's verdict (warming): when it has more than one statement, one per line.
4. Choosing the lights (Match with lights tab) or the partner (Index tab) should start the
   analysis by itself, with no button.

The owner's answers (2026-11-10): the new numbers apply **everywhere** (console, Report tab,
Summary, tabs, hints, README, developer docs; old ADRs and plans keep theirs); **SESSION stays
first, unnumbered (check 1), then 2 to 9**; the two **"Analyse again" buttons are removed**.

Branch `claude/Renumber-checks-and-rerun-on-choice`, worktree
`.claude/worktrees/Renumber-checks-and-rerun-on-choice` (from `E:\Photografie\Code\DarksCheck`,
off `main` 74de1685). This plan is saved as
`docs/plans/2026-11-10-renumber-checks-and-rerun-on-choice.md`.

## The new numbers

| Old | New | Check |
| --- | --- | --- |
| 1 | 1 | session: metadata, cadence, match (the SESSION card, unnumbered) |
| 9 | 2 | how well the dark fits the lights |
| 2 | 3 | light leaks and differences across the frame |
| 3 | 4 | the camera's warming during the series |
| 4 | 5 | the noise of each frame |
| 5 | 6 | hot pixels |
| 6 | 7 | fixed pattern |
| 7 | 8 | dead and saturated pixels |
| 8 | 9 | how many darks are enough |

## 1. Renumbering (ADR 0046)

**Core** (`src/DarksCheck.Core/Report/`):
- `ReportRows.cs`: `ReportSection` gets a stable key, `ReportCheck Check` (an enum: `Lights`,
  `Leaks`, `Warming`, `Noise`, `HotPixels`, `Pattern`, `DeadSaturated`, `Enough`, `Index`), so
  nothing finds a section by its number any more.
- `DarkReport.Sections` returns the sections in the new order with the new titles
  (`2. HOW WELL THE DARK FITS THE LIGHTS` / `2. CAT DE BINE SE POTRIVESTE ...`, `3. LIGHT
  LEAKS ...`, ... `9. HOW MANY DARKS ARE ENOUGH`, `INDEX`). `DarkReport.Write` prints them in
  that order, so the console and the Report tab read like the Summary.
- The hints that name a check: `FrameTableNote`, `EndColumnTip`, `WarmingChartNote` ("(check 3)"
  becomes "(check 4)", "verificarea 4").

**Window** (`src/DarksCheck.Wpf/`):
- `MainViewModel.Populate`: `Sections` in the core's order (the `IsLightsCheck` reordering goes);
  `HotRows`, `LightRows`, `IndexRows` and the maps' leak rows (now `sections[0]`) are found by
  `ReportCheck`, not by `"5."`, `"9."`, `"INDEX"` or an index.
- `Views/HotPixelsView.xaml`: `6. HOT PIXELS`, `9. HOW THE NOISE FALLS ...`;
  `Views/LightsView.xaml`: `2. HOW WELL ...`; `Views/FramesView.xaml` tooltips "(check 2)" ->
  "(check 3)", "(check 4)" -> "(check 5)", both languages.

**Goldens** (`tests/DarksCheck.Tests/Golden/*.txt`, all 14 but `help`/`inventory`): regenerated.
The intended diff is only the sections moved and renumbered plus the hints. Checked by a
scratchpad script: the old golden, with the transform applied (move section 9 under the header,
renumber, map the hints), must equal the new golden byte for byte; then the diff read whole.

**Docs, one numbering in everything living**: a scratchpad script does a single pass with the
table above over `README.md`, `CHANGELOG.md` (all of it is `[Unreleased]`),
`docs/requirements.md`, `docs/design/*.md`, `docs/validation.md` (not its dated history
entries), `src/**` comments, `tests/**` comments and names (`R056_Check9...` ->
`R056_Check2...`); lists and ranges ("checks 2 to 8", "checks 3, 4 and 8", "checks 2 to 9") are
flagged and fixed by hand; the whole diff is read back. The README's console example and its
`#### N.` sections are reordered. Untouched: `docs/decisions/` (bar the new ADR and a dated
note on ADR 0041, whose "the console keeps its order" this supersedes), `docs/plans/`,
`prototype/`, `docs/validation/2026-08-12-smida/` (the hand analysis keeps its own numbers).
`CLAUDE.md`: the list of the nine checks in the report's order, with "ADR 0046".

## 2. One statement per line in the verdicts of checks 3 and 4 (new numbers; ADR 0047)

The words stay exactly as they are: the console text does not change (the goldens prove it).
`DarkReport.Leak` and `DarkReport.Drift` build their verdict as a list of statements; the row's
`Value` is the verdict word + `": "` + the statements joined by `"; "`, as today, and
`ReportRow.Lines` holds the statements when there are two or more (its doc comment: the
statements of `Rest`, without the verdict word). A statement ends at each `"; "` outside
brackets; the warning's "also," / "in plus," stays at the start of its line. The window's
`ReportRow` template already draws `Lines` one under the other, the chip beside the first.

Example, check 4 on the `redo` session, Romanian. Now:

> REFA: 7 din 12 cadre sunt la peste 2.0 grade de sfarsitul seriei, deci masterul nu apartine
> niciunei temperaturi; aparatul a pornit mai cald decat locul in care a stat: refa seria dupa ce
> aparatul a stat vreo 40 min in locul unde se trag dark-urile (de trei ori cele 12 min ale
> seriei); 5 cadre (1 .. 3, 11, 12) sunt prea departe de master ca Siril sa le potriveasca cu -opt
> (in afara 0.7 .. 1.4; cel mai departe 1.93): seria a pornit calda; arunca-le

After, in the window:

```
[REFA] 7 din 12 cadre sunt la peste 2.0 grade de sfarsitul seriei, deci masterul nu apartine niciunei temperaturi
       aparatul a pornit mai cald decat locul in care a stat: refa seria dupa ce aparatul a stat vreo 40 min ... (de trei ori cele 12 min ale seriei)
       5 cadre (1 .. 3, 11, 12) sunt prea departe de master ca Siril sa le potriveasca cu -opt (in afara 0.7 .. 1.4; cel mai departe 1.93): seria a pornit calda
       arunca-le
```

Check 3, OK: `[OK] nicio margine si niciun colt nu difera de centru cu peste 1.0 ADU (...)` /
`niciun cadru nu are lumina in plus`. A one-statement verdict (`NEMASURAT`) stays one line.

(As built: one splitter in the core, `ReportRow.StatementsOf`, cuts the two verdicts' `Rest` at
the same `; `, instead of rebuilding `Leak` and `Drift` as lists: the cause words carry a `; ` of
their own. The lines and the rule are the plan's; ADR 0047.)

## 3. A choice of lights or partner runs the folder again (ADR 0048)

- `MainViewModel`: `ChoosePartnerAsync(string? folder)` and `ChooseLightsFromAsync(string?
  folder)` set the choice (null clears it) and, with a folder open, run it again through
  `RerunAsync`. Every choice the dialog returns runs, the same folder again included (as the
  problem card's button does, R-077); clearing runs too. The setters no longer write
  `PartnerChanged` / `LightsFromChanged` (both texts go from `WindowTexts`).
- `MainWindow.xaml.cs`: the partner and lights dialogs and the two clear buttons call these;
  `ChoosePartnerAndRerun` (problem card) uses `ChoosePartnerAsync`; `RerunFromView` goes.
- `IndexView.xaml(.cs)`, `LightsView.xaml(.cs)`: the `Analyse again` button and its handler go.
  A changed setting in the top bar takes effect when the folder is opened again.

## Requirements first, then tests, then code

- `docs/requirements.md`: R-025 (the sections and their order), R-071 (the Summary follows the
  console's order; the two verdicts' lines), R-073 and R-122 (a choice runs again, no button, no
  status line), R-087 (no hint of `Analizeaza din nou`), plus the renumbering.
- Tests (written before the code, red first):
  - `DarkSeriesTests` / `LightsAndIndexTests`: the sections' titles and keys in the new order in
    both languages (R-025); the warming verdict of the `redo` fixture carries 4 lines whose
    `"; "` join is `Rest`, the leak OK verdict 2 lines (R-071). Existing lookups by `"3."` and
    `Sections(r)[0]` move to `ReportCheck`.
  - `MainViewModelTests`: the Summary cards in the core's order (R-071, replaces the "check 9
    first" test); choosing a partner after a run runs again and the INDEX rows carry it,
    clearing runs again without it (R-073); the same for the lights (R-122). Tests that index
    `vm.Sections[n]` move to the new order.
  - `LanguageWindowTests`: the hint assertion of `Analyse again` goes with the button (R-087).
  - Goldens regenerated as above.
- Docs: `docs/design/window.md` (no reordering, lookups by key, the rerun, three buttons on the
  Index tab), `docs/design/dark-analysis.md` (order, `ReportCheck`, the verdict lines),
  `docs/design/logging.md` (`Rerun` leaves the button list), README window section, CHANGELOG
  `Changed` lines, ADRs 0046-0048.

## Verification

- `scripts/verify.ps1` green (build with `--no-incremental -warnaserror` before the last run).
- The golden transform check above; the golden diff read whole and its lines reported at wrap-up.
- `scripts/check-design.ps1 -Since main`.
- No figure or threshold changes, so `/validate` is not needed (the validation tests read
  figures, not titles).
- The window on the synthetic session, screenshots dumped with `DARKSCHECK_DUMP_SCREENS`: the
  Summary reads SESSION, 2 ... 9, INDEX; the verdicts of checks 3 and 4 on separate lines; the
  Lights and Index cards without `Analyse again`.

## Left for later

- The AstroDarkroom wiki's DarksCheck pages name the old numbers (and show the old buttons).
- A version bump seems warranted (the console's report changes order); the owner's call.
