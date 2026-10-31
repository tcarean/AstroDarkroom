# Remember the filter and sky switches per session folder

Plan for the task branch `claude/Remember-the-filter-and-sky-switches`, worktree
`FlatCheck/.claude/worktrees/Remember-the-filter-and-sky-switches`, cut from `main` (7af5bdd9).

## Context

The owner asked (2026-10-30): a checkbox `I didn't use any filters on my lens` at the top of the
Structure tab's section `VIGNETTING against the lens profile`, checked by default. Unchecked, the
vignetting comparison is no longer counted in the verdict (any filter on the lens changes the
vignetting, so nothing can be compared), the rest of the section is hidden except the value
`the master photographed`, and an explanation follows it. Flipping the box redoes the comparison
and the verdict: unchecked shows no old table; checked again builds the table and counts it. The
state of this box and of the Exposure tab's `Sky flat` box is saved in `flatcheck.json` in the
Siril session folder and restored at the next opening. Two languages.

The design (four decisions, measured facts) was put to the owner on 2026-10-30 and accepted,
with the correction that a flip redoes the comparison rather than showing anything stale:
ADR 0078 (written, `accepted 2026-10-30`). Requirements R-118 and R-119 are in
`docs/requirements.md` as `draft`. The acceptance tests are written and fail to compile
(the API does not exist): `SessionSettingsTests.cs` (new), `R118_*` in `StructureTabTests.cs`,
`R119_*` in `MainViewModelTests.cs`, the render `tab-structura-filtru` in `WindowSmokeTests.cs`.

Measured (the owner's logs of 2026-10-28, eight runs on real masters): load + analysis
2.6-5.2 s, the comparison 24-322 ms. So a flip recomputes the comparison on the kept result and
never reloads the master.

## Steps

1. **Core, `FlatCheck.Core/Report/StructureReport.cs`**: `MasterDepth(StructureResult r)`
   overload (`Depth(r.VignetteStops)`, the same figure `VignettingComparison` carries as
   `MasterStops`) and `FilteredLensNote()`, a `T(en, ro)` paragraph (no diacritics): with a
   filter on the lens the comparison has no meaning - any filter changes the vignetting, a mount
   shading the corners, a glass darkening the edges - so the master's depth is no longer the
   lens's published profile plus the flat's error and nothing can be compared; the verdict is
   the five tests' alone. Both with XML doc comments. Nothing in the console's report changes.
2. **Core, `FlatCheck.Core/Runs/StructureRun.cs`**: `Recompare(StructureResult r, LoadedFrame
   master, string path, TextWriter report, StructureQuery query)` -> `StructureOutcome`: with a
   query, `Compare(r, master, query)`; without, `new StructureOutcome(r, null, null, null)`;
   then `StructureReport.Write(o, path, report)`, timed by a `RunClock` ("structure recompare")
   and logged like `Execute`. The window calls `Runs/` only, as CLAUDE.md asks.
3. **Window, `FlatCheck.Wpf/Session/SessionSettings.cs`** (new, on the pattern of
   AstroPhotoAnalyzer's `WindowSettings`): `internal sealed class SessionSettings` with
   `const string FileName = "flatcheck.json"`, keys `noFilters` / `skyFlat`, `static Read(string
   path)` (missing, unreadable, not an object, a key absent or of another type -> defaults
   `true` / `false`, never an exception), `static None` (no path: remembers nothing), `FilePath`,
   `bool NoFilters`, `bool SkyFlat`, `Write()` (temp file + `File.Move(overwrite: true)`;
   `IOException` / `UnauthorizedAccessException` -> `Log.Warn(e, ...)` naming the file, under
   `Loggers.Window` or a `Window.Session` logger; nothing without a path). `System.Text.Json`
   from the BCL, no package. `SirilSession.SettingsPath => Path.Combine(Root, FileName)`.
4. **Window, `StructureTabViewModel.cs`**:
   - `public bool NoFilters` (field `true`); setter: `Set`, then `_settings.NoFilters = value;
     _settings.Write(); _ = RecompareAsync();`. `public string FilteredNote` (bound).
   - `RunAsync(session, ct, SessionSettings settings = null)`: `_settings = settings ??
     SessionSettings.None`; `_noFilters = _settings.NoFilters; Raise(nameof(NoFilters))` (no
     write, no recompare); `compares = rawPath != null && _catalog != null && NoFilters`; keep
     `_session`, `_ct`, `_structure` (the `StructureResult`), `_masterPath`, `_rawPath` for
     the recompare. Unchecked: `query = null`, `gap = null` (no Compare on the header), the
     Adobe block empty because `searched` is null.
   - `internal Task RecompareAsync()`: nothing without a result (`_structure == null`);
     `RunGuardedAsync` with the "looking for the lens profile" message when it compares;
     catalog `EnsureAsync` when comparing; `Task.Run`: `WriteBanner`, `StructureRun.Recompare`,
     `WriteMapSaved`; then `Report`, `Populate(o, gap, sources)`, `HasResult = true`,
     `SaveReport(session)` (the text only: split `Save` into the report and the map; the map
     bytes and `Map` untouched). Without a comparison and `NoFilters` off: no gap; with it on and
     no raw: the gap as today.
   - `Populate(o, gap, searched)`: in the "nothing compared" branch, `ProfileMaster =
     NoFilters ? "" : StructureReport.MasterDepth(o.Structure)`; `FilteredNote = NoFilters ? ""
     : StructureReport.FilteredLensNote()`; `ProfileGap` only when `NoFilters`. In the compared
     branch `FilteredNote = ""`.
   - `Reset()`: clears `FilteredNote`, `_structure`, paths, `_settings = None`; leaves
     `NoFilters` as it is (the next folder's file sets it).
5. **Window, `ExposureTabViewModel.cs`**: `Attach(session, ct, gate = null, SessionSettings
   settings = null)`: `_settings = settings ?? None; _sky = _settings.SkyFlat;
   Raise(nameof(Sky))` before the list (no recompute: no file chosen yet). `Sky` setter: after
   `Set`, `_settings.SkyFlat = value; _settings.Write();` then `Recompute()`. `Reset()`:
   `_settings = None`, `Sky` untouched.
6. **Window, `MainViewModel.cs`**: in `OpenAsync` after `Check()` passes:
   `SessionSettings settings = SessionSettings.Read(session.SettingsPath)` and an Info line
   (`settings noFilters=.. skyFlat=.. file=present|absent`); pass it to `Exposure.Attach` and
   through `RunChainAsync` to `Structure.RunAsync`.
7. **Window, `Views/StructureView.xaml`**: inside the profile card's `StackPanel`, first: the
   `CheckBox` (`IsChecked="{Binding NoFilters, Mode=TwoWay}"`, `IsEnabled="{Binding IsBusy,
   Converter={StaticResource Not}}"`, `TextBlock` label `{l:T En='I didn\'t use any filters on
   my lens', Ro='Nu am folosit niciun filtru pe obiectiv'}` wrapping; XAML `T` texts cannot
   hold an apostrophe unescaped - use the `\'` form the Exposure view uses). Then the Adobe
   block, the gap, and a restructured body: the `the master photographed` grid in its own
   panel visible on `ProfileMaster` (`NotNullToVis`); the rows, summary, scale and notes in
   the `HasProfileRows` panel as today; last, the `FilteredNote` paragraph (`Muted`, wrap,
   `NotNullToVis`). The XAML comment explains the order.
8. **Tests**: move the three `R119_*` tests from `AppStartupTests` (where the edit landed, after
   line 663) into `MainViewModelTests`, which owns `RawFrame()`. Run the four test files, then
   `WindowSmokeTests` with `FLATCHECK_DUMP_SCREENS` to look at `tab-structura-filtru.png`
   (send it to the owner). `scripts/verify.ps1 -SkipTests` while editing.
9. **Docs**: R-118 / R-119 -> `implemented`; `docs/design/two-applications.md` (the Structure
   tab paragraph: the box, `Recompare`, what is kept between runs; the Exposure paragraph: the
   sky box; a paragraph on `SessionSettings` under the session folder, and its `Covers`
   pointer to `Session/`); `CHANGELOG.md` Added; `README.md`: a paragraph under "The sixth
   test" (the box, what unchecked means) and in "The window" (the two boxes remembered in
   `flatcheck.json`); `docs/plans/2026-10-30-remember-the-filter-and-sky-switches.md` = this
   plan. No golden changes expected (the console is untouched); no `/validate` (no formula
   moved; `Report/` gains texts only - say so at wrap-up).
10. **Gate** `scripts/verify.ps1`, read the diff, commit on the branch, `/wrap-up`, propose the
    merge (check main's latest ADR number first; ADR 0078 is free on main today).

## Verification

- The four new test groups green: `dotnet test FlatCheck.Wpf.Tests --filter "R117|R118"`, then
  the whole gate.
- The smoke render `tab-structura-filtru.png`: the box unchecked, the master's depth, the
  paragraph, no row, no gap; `tab-structura-profil.png` unchanged but for the box.
- By hand if the owner wants: open a real session (the Tokina sky master), untick, see the verdict
  drop the depth and `Test result.txt` rewritten without the profile table; close and reopen, the
  box stays unticked; `flatcheck.json` holds `{"noFilters":false,"skyFlat":false}`.

## Deviations (2026-10-30, at wrap-up)

- `StructureTabViewModel.Pending` was added (the Histogram tab's pattern): the setter's
  un-awaited recompare raced a second, awaited one on `Test result.txt` in the tests, so the
  tests await `Pending` instead of calling `RecompareAsync` twice.
- `RunAsync` sets the box from the settings only when settings are given: without them (a test,
  a direct call) the box stays as it was set, which the tests rely on.
- `docs/design/code-map.md`, `modes-structure.md` and `lens-profiles.md` got a line each, since
  `check-design.ps1 -Since main` listed them beside `two-applications.md`.
- `/validate` was not run: `Report/` gained two texts and no formula moved.

## Part 2: the review's findings (approved by the owner on 2026-10-30)

A subagent review of commit 2670084 found seven things (the chat of 2026-10-30). The owner chose,
for the two races (a tick while the chain waits for the gate is lost; a recompare's result can
land on the next folder's tab), an opening panel that hides the tabs until every opening run
has ended, and took the recommendations for the rest:

1. **Opening panel** (R-120, ADR 0079): `ViewModels/OpeningStep.cs` (label, tab, state),
   `MainViewModel.OpeningSteps` (structure, end-to-end, the Synthetic prefill), the states set
   around each awaited run, `IsRunning` lasting until the prefill ends and following the latest
   opening (a generation counter), `Views/OpeningPanel.xaml` shown on `IsRunning` with the tabs
   collapsed meanwhile. R-081, R-084, R-085 amended for the gate rule a click can no longer reach.
2. **Guard** (finding 1): after the await in `StructureTabViewModel.RunAsync` and
   `RecompareAsync`, return when `_session` is no longer the run's session.
3. **Busy line** (finding 3): `RecompareMessage` - "Comparing the vignetting with the lens
   profile..." or "Redoing the verdict without the comparison...".
4. **Temp file** (finding 4): deleted in `SessionSettings.Write`'s catch; the failed-write test
   asserts it.
5. **Header HDU** (finding 5): a sentence in `two-applications.md`; no code change.
6. **Gate** (finding 6): the recompare is deliberately outside the gate; said in the note.
7. **Tests** (finding 7): a folder closed during a recompare leaves nothing on screen; `IsBusy`
   at once after a flip; the opening panel's states and its render.

## Part 3: the second review (approved by the owner on 2026-10-30)

A second subagent review of 10cd978 found ten things; the owner took 1-9 (10, Romanian render
names in the tests, is a project-wide habit, left): the latest opening clears `IsRunning` on
its early exits too; the prefill's row follows the prefill's own task, captured after `Attach`
(`WatchPrefillAsync`); the older-opening test holds the gate from the test, as the R-081
test does, so it is deterministic (a first version held B's structure run on its master's
header through `loadInfo`, which deadlocked the test thread: `SplitTabViewModel.Attach` reads
that header synchronously through the same function); R-084 and R-085 amended as R-081 was, `WaitingForRunningAnalysis` reworded (it
can no longer name the opening analyses), two README sentences fixed; the `ReopenAsync`
attribution corrected in ADR 0079, R-120 and the code comment; the gate wait with every row on
`waiting` said in the ADR, R-120 and the README; a dropped run logged at Debug; the accent
brush from the theme.

## Part 4: the third review (approved by the owner on 2026-10-30)

Nine small things, all taken: the older-opening test asserts the chain's two rows only and
awaits B's prefill to see its row done while the chain still waits (the test of fix 2 of part
3; fix 3, a cancelled opening no longer awaiting the newer folder's prefill, is covered by
reading: both prefills wait on the shared catalog and the only deterministic hold deadlocks
through `SplitTabViewModel.Attach`); the design note, the message's comment and the README
sentence brought in line; the record of part 3 corrected (the older-opening test holds the
gate, not the master's header); `_session = null` on the blank-folder exit; the dropped-run comment
says what `RunGuardedAsync`'s own "done" line then means (the Info line is left: the run did
end); the sweep of "waits for the opening chain" in comments, a design note, R-086 and three
test summaries, which now say "the gate another run holds" (the chain itself keeps its name).

## Part 5: the fourth review (2026-10-30)

Four nits, none blocking, taken without a further review: one `RunAsync` summary in
`SmoothTabViewModel` still said "after the opening chain"; the list above missed its own fix 5;
two test locals named `chain` were renamed `other`, as `MainViewModelTests` names the lease;
the renamed Netezire test says its holder is the opening chain, by a direct call.

## Part 6: main merged into the branch (2026-10-30)

Main took R-117 and ADR 0077 (the Exposure run-overlap task) while this branch was reviewed:
the branch renumbered to R-118..R-120 and ADRs 0078/0079, then merged `main` in. Three
conflicts, all "both sides appended": the R-117 row beside R-118..R-120, the constructor hooks
of `CanChooseFolder` beside the opening steps, the R-117 test beside the R-119/R-120 tests.
ADR 0079 now says ADR 0077 closed the Exposure finding.
