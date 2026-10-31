# 0078. A filter on the lens turns the profile comparison off, and the window remembers its switches per session folder

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

The sixth test of the Structure tab compares the master's vignetting depth with the lens's
published profile (ADR 0030, R-016). The profile describes the bare lens. Any filter mounted on
it changes the vignetting: a mount shades the corners, a glass darkens the edges, a stack of
rings does both; so with a filter on, the master's depth is no longer the published profile plus
the error of the flat, and a difference in stops says nothing about the flat. The owner asked
(2026-10-30) for a checkbox at the top of the card, `I didn't use any filters on my lens`, checked
by default, whose unchecking drops the comparison from the verdict, hides the rest of the card
except the master's own depth, and explains why; and for that box and the Exposure tab's `Sky
flat` box to be remembered per Siril session folder, in `flatcheck.json` in its root.

Measured from the owner's diagnostic logs of 2026-10-28, eight structure runs on real masters:
load and analysis 2.6 to 5.2 s, the comparison 24 to 322 ms (one 2.8 s, the first load of the
catalog), report and map 0.3 to 0.7 s. `StructureRun.Compare` already exists apart from
`Execute` so the window can compare without reloading the master (ADR 0030), and the
comparison's master depth is the structure result's own `VignetteStops`
(`VignettingComparison.cs`), so it can be shown without a comparison.

## Decision

1. **Unchecked, the run is the plain one.** With the box unchecked the Structure tab runs
   exactly `flatcheck master`, without `--raw`, as when `flats/` holds no raw: the verdict is
   the five tests', the saved `Test result.txt` is the console's plain report byte for byte
   (R-081, R-090), no profile is searched (so the Adobe paragraph of ADR 0073 is absent), and
   the card holds the master's own depth and a paragraph from `StructureReport` saying why
   nothing is compared. The console gets no flag: its user leaves `--raw` out.
2. **Flipping the box redoes the comparison and the verdict on the kept result.** A new
   `StructureRun.Recompare` in `Runs/` takes the `StructureResult` already computed, the
   master's header and the query (or none), builds the outcome, writes the report; the tab
   saves the report again and keeps its map. Unchecked, every row goes and the five tests'
   verdict stands; checked, the table is built and the worse of shape and depth is the verdict.
   The master is not reloaded. The box is disabled while the tab runs.
3. **The switches live with the session.** `flatcheck.json` in the session root holds
   `noFilters` (default `true`) and `skyFlat` (default `false`), read when a folder that passes
   the checks opens and applied to both tabs before any run, written whole through a temporary
   file and a move each time a switch changes, and never when none did. A missing, unreadable
   or malformed file, or a key of another type, is the default; a failed write is a `Warn` line
   and the value holds for this run (`SessionSettings`, window state, nothing computed).

## Alternatives rejected

- **Keep comparing and only hide the table.** The saved report would hold a table the screen
  says is meaningless, and the verdict in the file would differ from the one on screen; a
  comparison with a filter on is as void in the file as on the card.
- **Re-run the whole structure on a flip.** 3 to 6 s under the busy overlay to redo 24 to 322 ms
  of work; the window already avoids reloading the master for the map's scale and for the
  Split's comparisons, and `Compare` was separated from `Execute` for exactly this.
- **Keep the old comparison on screen when unchecked.** The owner refused it: a table of
  stale `REDO` rows the user has just declared irrelevant.
- **The settings in `%LOCALAPPDATA%`, per user.** A filter and a sky flat are properties of the
  session's setup, not of the user: two folders of the same user differ, and a folder reopened
  a month later must come back as it was.
- **A console flag for the filter.** The console's `--raw` is already opt-in; a `--no-filters`
  that only turns `--raw` off would be a second way to say nothing.

## Consequences

- `StructureTabViewModel` keeps its `StructureResult`, the master's path and the raw's path
  between runs, to recompare without a load; `Reset` drops them with the result.
- `SirilSession.SettingsPath` names the file; the window writes one more file in the session
  root, only once a switch is touched.
- The texts (`StructureReport.FilteredLensNote`, the label in `StructureView.xaml`) are in
  both languages, without diacritics; the console's report and the goldens are unchanged.
- Guarded by R-118 and R-119: `StructureTabTests`, `MainViewModelTests`,
  `SessionSettingsTests`, a render in `WindowSmokeTests` (`tab-structura-filtru`).
