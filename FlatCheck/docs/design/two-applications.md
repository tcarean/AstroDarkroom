# The two applications: console and window over one core

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): that neither application computes is CLAUDE.md
> and ADR 0011, the Siril session rules of the window are ADR 0033 to 0036, the WPF lessons
> ADR 0037, and this note keeps the mechanics of the window's tabs and the few reasons that have
> no other home. **Covers:** in full, the table of what each application does with the core's
> outputs (`FlatCheck/`) and `FlatCheck.Wpf/` `ViewModels` and `Views` (the tab mechanics below);
> by pointer, `FlatCheck.Wpf/Session` (ADR 0033 to 0035, 0055; `SessionSettings.cs` in full,
> under the Structure tab below, ADR 0078; the opening panel, `ViewModels/OpeningStep.cs` and
> `Views/OpeningPanel.xaml`, ADR 0079), `Presentation` and `Theme`
> (ADR 0036, 0037; the tabs' control, ADR 0074) and `Runs/RunOutcome.cs` (ADR 0011, 0036).
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## The two applications

Two front ends over one core, neither computing a number: CLAUDE.md (first paragraph and
"Layout") and ADR 0011, which also records why the 2.0.0 split was a library extraction, why a
run writes its report into a `TextWriter` the caller supplies, and why it never writes a map to
a path of its own. What differs is only what each application does with the three outputs:

| | console | window |
|---|---|---|
| input | one file (or a folder, with `--light`) | one Siril session folder; the Histogram tab lists the flat master and every raw or FITS in `flats/`; the Netezire tab takes the session's master and writes beside it. A folder without a master in either `masters/` or `process/` can be preprocessed from the window, through the user's own `siril-cli` |
| report | written to stdout | shown as panels, saved as `Test result.txt` |
| map | written to `-o <path>` (`--smooth` writes a second, `-after`), always on the automatic scale | shown from memory (the Netezire tab offers both behind a toggle), on a switchable scale with a legend and a shape guide, saved as `flatcheck.png` - the file on the automatic scale, like the console's |
| smoothed flat | written to `--smooth <path>` | written to `pp_flat_stacked_smoothed.fit` beside the master, on a button, after asking about an existing file |
| synthetic flat | written to `--synthetic <path>`, the lens from `--lens` text; `--against` compares two files | written to `pp_flat_stacked_synthetic.fit` beside the master, the lens from a searchable list, focal / f-stop / crop prefilled from the master's header and a raw in `flats/`; the comparison against the file follows the write by itself, and "Compara cu masterul" runs it alone on a file written earlier |
| result | exit code | coloured verdict |
| options | `--fov`, `--sat`, `--black`, `--pattern`, `--sky`, `--rainbow`, `--scale`, `--lens`, `--raw`, `--source`, `--focal`, `--fstop`, `--crop`, `--profiles` | `--sky` (a checkbox), the false colours (a checkbox on the Histogram tab, ADR 0063), the smoothing scale (three chips), the lens list, its three prefilled fields and the sensor-format chips behind the crop; the Structure tab's profile comparison takes the first raw in `flats/` by itself |

The console keeps the options because it is the scriptable one (ADR 0033); `--fov` and
`--sat` exist for telling the tool something the header does not say, and adding them to the
window would be four more controls in service of the rarer case.

### What the window adds, and what it does not

What the window adds - a workflow, never an analysis, a threshold or a wording - is ADR 0011
and ADR 0033. The "Despre" window, which formulates nothing, is ADR 0040. The three viewing
aids around the residual map (the scale switch with its caps from `Th.Resid*`, the legend, the
collapsed shape guide), why the guide is documentation and not a classifier, why End-to-end
gets the legend and no shape guide, why the guide texts mirror the README by hand and why the
guide's ring row was rewritten are ADR 0036 (the ring row also ADR 0018). Two mechanics the
ADRs do not spell out: the scale switch defaults to ±10 %, and the guide's text is in
`Session/SessionMessages.cs`; Structure gets all three aids.

**The End to end tab explains itself in place, not in a folded guide** (ADR 0064): a `Faint`
line under each of the four series figures, a tooltip on each header of the frames table and a
paragraph under the map and its legend. Mechanics the ADR does not spell out: each summary
figure is a two-row grid, the label and the value on the first row, the note on the second in
column 0 only, which is what keeps it out from under the value; the headers get
`Background="Transparent"` so the whole header, not only the glyphs, raises the tooltip, and
`ToolTipService.ShowDuration="30000"`; the map note is the fifth row of the right column's
grid, under the legend, hidden with the figures on a refusal (`HasNumbers`). The nine texts
are `SessionMessages.Light*Note` / `Light*Tip`, surfaced by `LightTabViewModel` as plain
properties with no backing field, so `Reset` has nothing to clear and a refusal nothing to
hide. The thresholds in them are interpolated from `Th` at the call, with the same `F0` / `F2`
formats as `LightSeriesReport`, under the invariant culture the application sets (R-090).

The Exposure tab's histogram card is drawing and wording too (ADR 0011): the paragraph under
the charts is `ExposureReport.HistogramReading`, the shaded target band *is* `ExposureBands`,
the band that voted (ADR 0012), and the collapsed guide's text is in `SessionMessages` like the
map guide's. `Histograms` is a **replaceable list, not an `ObservableCollection`**: picking a
file starts a run nobody awaits, and a second pick (or the Sky toggle) can start another over
it - scalar properties just let the last one win, but a collection cleared and refilled by two
runs at once ends up with duplicated rows and nulls in it. The light series is the opposite
case and does need `ObservableCollection`, because it appends rows as frames finish. The Sky
box is set from the folder's `SessionSettings` in `Attach`, before any file is chosen (so no
run starts), and its setter writes them back (R-119, below under the Structure tab).

Above the charts sits a switch, `HistogramSourceViewModel`, on the `MapScaleViewModel`
pattern: two radio chips, raw data or the camera's preview (ADR 0024 for what the preview is
and why it never votes). Flipping it rebuilds the rows from the **retained** `ExposureResult`;
nothing is re-read or re-analysed. It is disabled when the file carries no preview (every FITS,
and a raw whose preview would not decode), and disabling it forces the selection back to raw.
The chips' `GroupName` is `ExposureHistogramSource`, unique in the window (ADR 0037). Under
the charts, the table that pairs the raw centre median with the preview's, per colour, is
shown on both views - it is the payoff, so it is never hidden behind the switch. `Populate` is
`internal` so a test can hand in a result carrying a preview built from a JPEG the test encoded
itself (`TestImages.JpegOf`); no raw file is needed anywhere in the suite.

The Structure tab carries a fourth block, `HowToReadPanel`, under the verdict: **which
half of the screen decides what.** The numbers answer "how much" and own the verdict; the
map answers "what kind" and never votes. The screen's own layout suggests the opposite -
two columns look like two views of one thing - so it is said in words. Same collapsed
pattern and the same `GuideRow` template as the map guide, text in `SessionMessages`
beside it, state in `HowToReadViewModel`. The disclosure toggle those three share lives in
`Dark.xaml` as `Style x:Key="Disclosure"`; it had been hand-written twice before that.

**The Structure tab compares with the profiles in the same run, when `flats/` holds a
raw.** `SirilSession.ReferenceRawPath` (the first raw, through `LensIdentification.FirstRaw`,
the shared rule of ADR 0030 - the Sintetic tab's prefill reads the same file) makes the run
exactly `flatcheck master --raw <that raw>` with the window's `ProfileCatalog`, so the saved
report stays the console's and the verdict appears once, already combined - not PERFECT on
five tests and REFA two seconds later. The catalog is awaited before the run (a second or two
on the first open, under the busy overlay, which says so). Without a raw the run is the plain
one, the report identical to `flatcheck master`, and the card under TESTE shows
`StructureReport.GapText` computed by `StructureRun.Compare` on the master's header alone.
The rows are `LensProfileRow` (public copy of the core's `ProfileRow`), rendered by a
template local to `StructureView.xaml`; `Tests` stays five, so the scale heading and the
Netezire tab, which reuses `StructureRows.Tests`, are untouched. `Populate` is
`internal` and sets `HasResult` for the smoke test. The one thing the window cannot do
that the console can is `--lens` by hand: a body without a raw in `flats/` gets the
explanation, not a lens picker (ADR 0031: the raws are always in `flats/`).

**The box at the top of the card, `NoFilters`, turns that comparison off** (R-118, ADR 0078).
`RunAsync` takes the folder's `SessionSettings` and sets the box from them without writing or
recomparing (the field and a `Raise`, not the setter), then `Compares(rawPath)` is "a raw, a
catalog, and the box on": off, the run is the plain `Execute` with no query, `Gap` returns null
(no `Compare` on the header either: the card has its own words), `searched` is null so the Adobe
block stays empty, and `Populate`'s "nothing compared" branch shows `MasterDepth(o.Structure)`
with `FilteredLensNote()` instead of the gap. The tab keeps what a flip needs - `_session`,
`_ct`, `_structure`, `_masterPath`, `_rawPath` - and the setter writes the settings and starts
`RecompareAsync`: the same guarded run, `StructureRun.Recompare` on the kept result (the
comparison or the plain outcome, and the report), then `Populate` and `SaveReport`, the report
alone; the raster, the map cache and `Map` are untouched, and `flatcheck.png` on disk is
already right. `Save` is `SaveReport` plus the PNG. The box is bound `IsEnabled` to `!IsBusy`,
so a flip never lands in the middle of the tab's own run; `Reset` drops the kept pieces and the
settings, not the box, which the next folder's file sets. The recompare is deliberately outside
the heavy-run gate (ADR 0061): it loads no pixels - the master's header, the raw's metadata and
the catalog only - so it may run beside the chain's light series. After the await, both `RunAsync`
and `RecompareAsync` return when `_session` is no longer the run's: a folder closed meanwhile
(`Reset` nulls it) gets nothing on screen and no file rewritten. One assumption to know: the
recompare identifies the lens on `LoadInfo`'s header, the primary HDU's, where `Execute` read
the image HDU's cards; the pre-existing gap path did the same, and Siril's masters are
single-HDU, so the two agree on every real master. In the XAML the master's depth sits in its own panel, shown
on `ProfileMaster`, and the note is the card's last element, shown on `FilteredNote`.

**The lens picker under the box, on a list found by range alone** (R-121, ADR 0080). A manual
lens leaves the core a range and nothing else, so `ShotIdentity.CandidatesByRangeOnly` lists
every lens of the mount with that range, and the window cannot tell which is on the camera.
`Populate` calls `ShowLensPicker(shot)` in both branches: when the core says there is a choice
(`ShotIdentity.OffersLensChoice`: range only, more than one candidate; the prompt
`StructureReport.LensChoicePrompt` speaks exactly then) the picker gets the no-choice entry
(`LensOption` with a null key, "the nearest profile decides") and one entry per candidate in
the rows' order, labelled `SourceLabel Name [Where]`, and the selection lands on the entry whose
key is `shot.ChosenLens?.Key`, else on the no-choice entry - through the field and a `Raise`, as
the box does, since a run shows a choice and does not make one. The "nothing compared" branch
passes the shot too, with the box on: a chosen lens whose profile makes no row (a lensfun lens
on a body the lensfun table does not know, so no crop) leaves the card on the gap with the
picker still up, so the choice can be undone from the screen; otherwise the key kept in
`flatcheck.json` would lock the card on the gap at every reopen. With the box off, and in
`Reset`, the picker is emptied and `OffersLensChoice` hides it. The `SelectedLens` setter, a
pick on screen, ignores the null the `ComboBox` pushes whenever the list is replaced (the field
never takes it), writes `_settings.Lens = value.Key` and starts `RecompareAsync`, exactly the
box's path; `QueryAsync`
puts `_settings.Lens` on the query as `LensKey`, so the run at open and every recompare carry
the folder's choice. The core does the rest (`StructureRun.Compare`, `lens-profiles.md`
section 11): the chosen candidate alone is compared, the candidates stay whole on the identity
so the picker keeps every entry, and the report's summary says which lens was chosen and which
were left out. A key that matches no candidate is ignored there, so the card shows every row
with the no-choice entry selected and the stale key stays in the file until the next pick. The
picker is bound `IsEnabled` to `!IsBusy` like the box.

**While the opening runs, a panel stands in for the tabs** (R-120, ADR 0079). `MainViewModel`
holds three `OpeningStep`s - the structure run, the end-to-end run, the Synthetic prefill - each a
label (a `T` pair read at bind time), the tab it runs on and a state the view-model sets around
its awaits: `Running` before `Structure.RunAsync` and `Light.RunAsync` in `RunChainAsync`, `Done`
after each, the prefill's `Running` right after `Synthetic.Attach`, whose task is captured there
and watched by `WatchPrefillAsync`: the row turns `Done` the moment that task ends, if the folder
is still the open one (`_session` by reference, so a reopen of the same path is not confused),
and `OpenAsync` awaits the watcher after the chain; read later, `Synthetic.Pending` would be the
next folder's. `ResetTabs` puts all three back to `Waiting`. `StatusWord`
is the state in a word or, once done, the tab's own `ErrorTitle` when it has one (a refusal such
as no lights reads as the tab will show it). `Views/OpeningPanel.xaml` binds the rows to the
tab's `BusyMessage`, `BusyDetail`, `Progress` and `ProgressKnown`, the same properties its own
`BusyOverlay` draws. `IsRunning` now lasts until the prefill has ended and follows the latest
opening: `OpenAsync` counts openings, only the latest clears the flag in its `finally`, and an
opening that stops before the chain (a blank folder, the problem card) clears it at once, so an
older chain ending under a newer opening - a direct call, as the tests make; the screen cannot -
does not show the tabs early. In `MainWindow.xaml` the
panel is visible on `IsRunning` and the tabs add `IsRunning == False` to their trigger.

**The two switches are the folder's** (R-119, ADR 0078): `Session/SessionSettings.cs`, one
JSON object in `SirilSession.SettingsPath` (`flatcheck.json` in the root) with `noFilters` and
`skyFlat`, and since R-121 the Structure tab's chosen lens under `lens`, a string written only
when a lens is chosen (`ReadText` gives null for an absent, empty or mistyped key), `System.Text.Json` from the BCL. `MainViewModel.OpenAsync` reads it once after
`Check()` passes and hands it to `Exposure.Attach` and, through `RunChainAsync`, to
`Structure.RunAsync`; each tab keeps the object and its setter writes it whole (a temporary file
and `File.Move`, a `Warn` line on failure) before it recomputes. `None` is the object without a
path, which the tabs hold before a folder and in the tests, so a setter never checks for null.
`Read` never throws: not a file, not an object, a key absent or of another type is the default.

**Without Adobe's profiles the card says so first** (R-113, ADR 0073). `Populate` takes the
sources the run searched (`query.Sources`; null without a raw, when the run compares nothing and
does not wait for the catalog, which the Sintetic tab's prefill loads at every opening anyway)
and shows the core's `AdobeMissing` and `AdobeSearched`, empty unless that search found no Adobe
file. The block sits at the top of the card's `StackPanel`, above the rows or the gap: the
paragraph in the window's red for a message (`#F0857E`), then `AdobeInstall`, the link to
`AdobeDngConverterUrl` and the folders searched. The link is a `Hyperlink`, whose
`RequestNavigate` bubbles to `MainWindow` and is opened by `Presentation/ExternalLink`, the
code the "Despre" window's links use too (ADR 0037: links are handled once, on their window).
The console prints none of it, so the saved report stays the console's. `ProfileCatalog`
searches once per window, so the text asks for a restart after an install.

#### Two places for the master

ADR 0034 holds the whole of it: Siril's two conventions for where `pp_flat_stacked.fit` lands
and the two archive sessions that prove both exist, `SirilSession.FlatMasterCandidates`,
`FindFlatMaster()` and `FlatMasterPath`, the three owner's decisions (when both exist the
newest wins and `FlatMasterLookup.Other` names the older one; only the master is required;
nothing else moved), the alternatives rejected and the guards.

#### Running the Siril script from the window

ADR 0035 holds the whole of it: the "Ruleaza in Siril" button on the problem card and
`SirilRunWindow`, what was measured on Siril 1.4.4 before it was built (live stdout through a
pipe, the share of progress lines, clean re-runs, the verb-only command echo), the owner's
five decisions, the alternatives rejected, and the test seams (`Func<SirilInstall>` and
`Func<bool>` on `MainViewModel`, `SirilCliIntegrationTests`). ADR 0055 holds the private
settings file every run gets with `-i`, and the measurements behind it. Four mechanics the
ADRs do not spell out:

- `SirilSession.PreprocessingScript` is the script name the messages quote. The parameterless
  `MainViewModel` constructor uses `SirilInstall.Locate` and `SirilInstall.IsGuiRunning`, which
  looks for a `siril` process (`siril-cli`, ours, has a different name).
- `SirilScript.RequiredFolders` takes the `cd <name>` commands with a plain name only:
  `cd ../process` and `cd ..` are not inputs. Without the pre-check Siril dies at line three
  with a failed `cd` and the user reads the log to learn why.
- stderr is read on the same channel as stdout; it stayed empty in every measured run whose
  settings loaded.
- The private settings folder is deleted on a thread-pool continuation
  (`ConfigureAwait(false)` after the wait), so it goes even when `SirilRunWindow` closes without
  waiting. The Split's temp folder is different: its delete needs the dispatcher, hence the
  `MainWindow.OnClosing` wait below.

**Every tab takes the one open folder.** The Ratio tab, which took two files chosen by hand
from two other folders, left with its mode on 2026-10-26 (ADR 0067; ADR 0033 holds what it
was). One mechanic it taught stays everywhere: a tab's constant texts are instance properties,
because `{Binding}` resolves through the DataContext and never sees a static one.

**One gate for every heavy run.** `MainViewModel` owns the `HeavyRunGate` and hands it to
every tab's `Attach`; `RunChainAsync` holds it from the structure run to the end of the series
(one lease, so a click during the chain waits for the whole chain), and each click-run takes it
around its computation through `TabViewModel.EnterGateAsync`, in a `using`: Expo,
Histogram, Netezire (run and proposal), Sintetic (write and comparison). The Split, which has
no overlay, waits for the gate before its copies start and holds it only around its analysis
of the part masters, not through the Siril runs. The gate wait takes the run's own token, so a
run superseded or cancelled while queued leaves the queue without computing; the chain's wait,
cancelled by another folder, returns quietly. While another run holds the gate, the waiting
screen's first line is `SessionMessages.WaitingForRunningAnalysis()`, not the run's own message,
which would claim work that has not begun: `EnterGateAsync` swaps the line in when the gate is
held at entry and puts the run's message back once in, and only for the latest run (the
generation counter of `RunGuardedAsync`), so a superseded run waiting beside the new one does
not write over its message; the Split puts the same text in its phase line. A tab attached
without a gate (the tests) runs at once.

**The folder button follows the long runs.** `MainViewModel.CanChooseFolder` is false during
the opening chain (`IsRunning`) and while the Smoothing or the Synthetic tab is busy
(`TabViewModel.IsIdle`, heard through their `PropertyChanged`), so a run that takes longer
than a folder change cannot be cut by one and land on the new folder's tabs (R-117, ADR 0077
for the measured times). The Exposure, Histogram and Split runs leave the button live: a
folder change cancels their token, the Histogram and Split runs stop at the check after the
hop back, and the Exposure run's few seconds are `known-issues.md` item 9.

**An opening that fails past the check.** `SirilSession.Check()` looks only at what exists;
listing `flats/` or `process/` can still throw afterwards (a folder the user may not list, a
broken junction). `MainWindow` fires `OpenAsync` without awaiting it, so nothing else would
see such an exception: `OpenAsync` runs the tabs' `Attach` and the opening chain in one `try`,
and an exception there is logged at Error and shown as a card
(`SessionMessages.FolderUnreadableTitle` / `FolderUnreadable`, with the message and its
causes), the tabs reset and the session dropped, unless another folder was opened meanwhile
(then a Debug line only). The Debug line that describes the folder runs after the card or the
tabs are up and writes a folder it cannot list as `unreadable`, so it never takes the card
down with it (R-080, R-110; why a card and not a crash is ADR 0072). The Synthetic prefill reads the reference raw inside its own
`try` for the same reason: it is awaited only at the end of `OpenAsync`, through a watcher that
assumes it never faults (R-120).

**The Histogram tab lists the flat master first, then the smoothed master when it exists,
then `flats/`, and lets a click interrupt a click.** The list and its order, the master not
preselected and not run at open, the smoothed master as a third kind of row (chip `SMOOTHED`, Romanian `NETEZIT`,
`IsMaster` false) found by exact path, and the one wire from `SmoothTabViewModel.Saved` to
`HistEq.FileWritten` are ADR 0033 and ADR 0036. What they do not say:

- The master is there for one question - did what the individual frames show average out? -
  and it is not run at folder open because the Structure run is reading that same 24 MP file
  at that moment; a click on it during the chain waits for the gate (R-085). The smoothed one is there for the other question - what did the smoothing
  take out, what did it leave - and has its own note under the picture
  (`HistEqReport.SmoothedVersusMaster`, which also says why a smoothed flat looks *noisier*
  under a rank-based transform).
- `FileWritten` rebuilds the list from `HistEqCandidates()` (one definition of the list),
  keeps the selection by path without re-running, and re-runs only if the selected file *is*
  the one rewritten - the picture would otherwise be of bytes no longer on disk. It ignores
  any other path and a missing session, and `Reset` drops the session for that reason.
  Rebuild, not insert: `ClosesGroup` (the separator under the last of master + smoothed) is an
  `init` flag set by whoever sees the whole list.
- The loader covers the picture, not the tab: the list stays clickable, a new pick cancels the
  running frame's own token and starts over, and a superseded run may not touch the screen.
  That last part is general and lives in `TabViewModel.RunGuardedAsync`: it numbers its runs
  and lets only the latest one clear the busy state, show an error or drop the result, and
  nothing more: the run's own writes inside the work are the tab's; `TabViewModelTests` pins
  it with a stub tab. The button tabs never overlap runs. The Exposure tab can start two: its
  overlay covers the result column, so the list and the Sky switch stay clickable during a
  run, and a second pick starts a run that queues at the gate while the first holds the lease
  through its writes, so the latest pick lands last (measured 2026-10-30, ADR 0077;
  `SemaphoreSlim` also serves its waiters in order, an implementation detail that matters only
  when both picks queue behind the opening chain). Without a gate the first run's figures can
  land after the second's, which is why the Exposure tab keeps the run its setters start in
  `Pending`, as the Histogram tab does, and the tests await that run rather than start
  another. Each view is a bitmap made on the UI thread straight from the full-resolution
  raster (`RasterBitmaps`), with
  no PNG in between, and cached per chip and per state of the false-colours checkbox
  (`modes-histeq.md`, "The false colours").

**The Netezire tab runs on a button and never on open**, waits for the gate, writes
after the numbers are on screen, asks about an existing file, and its output never becomes the
session's master: ADR 0036. Mechanics the ADR does not spell out:

- Its guide (`SessionMessages.SmoothGuideRows`, mirrored in the README by hand) is the one
  guide that starts *expanded*: it is the button's precondition.
- Three scale chips (`SmoothScaleViewModel`; ADR 0027 for the ladder) and the button; the
  output is `pp_flat_stacked_smoothed.fit` in the master's own folder
  (`SirilSession.SmoothedMasterPath`, `SmoothedSuffix`).
- The tab keeps the two rasters (removed component, smoothed flat's residual) behind a radio
  toggle, cached per (map, cap); `MapScaleViewModel` took a constructor argument
  for its default cap so this tab can start at ±2 %, where at ±10 % the map would be white.
  `Populate` is `internal` and sets `HasResult`, so the smoke test renders the tab from an
  analyzer result without writing a file.
- The second button, `Propune scara`, runs `SmoothRun.Advise` the same guarded way, fills the
  rung rows and the verdict from `SmoothReport`'s advice strings, selects the proposed chip
  through `SmoothScaleViewModel.SelectScale`, and writes nothing; `PopulateAdvice` is
  `internal` for the smoke test.

**The Split tab is a workflow over the other pieces** (ADR 0054 holds the decisions: the copy
list, the two lights, the reference profile, the labelled mosaic on one scale, the temp folder
kept only after a failure). Mechanics the ADR does not spell out:

- `SplitPlan` (`Session/`) is the whole of the disk rule - which files are frames, whether the
  count divides, what each part gets, where the temp root is - and is tested without Siril.
  The tab copies file by file on a background thread with an `IProgress<double>`, then runs
  `SirilRunViewModel` per part exactly as the problem card does, only shown inline: the run
  view-model got a public `CommandIndex` so the tab can turn "command 7 of 20" into a share
  of the bar. Copy and script are one step each per part, the analysis one more; the phases
  are equal in the bar, not in time.
- It does not use `TabViewModel.RunGuardedAsync` or the busy overlay: that overlay covers the
  tab, and the user must see the phase, the bar, Siril's log and `Opreste`. Its running state
  is `IsRunning`; the chips and the script box are disabled through it.
- `ScriptChoiceViewModel` is the script block of the problem card lifted out of
  `PreprocessViewModel`, which is now a subclass adding the card's lead text; the tab owns a
  second instance. `IsOffered` there means "attached to a session".
- The script's `RunAsync` cancels only through its own token, so the tab registers
  `Run.Cancel()` on its linked token: `Opreste`, a folder change and the window closing all
  kill Siril the same way, and the temp folder is deleted in the `finally` unless a failure
  said to keep it. The window closing is the one that needs care: `MainWindow.OnClosing`
  asks, stops the split and closes itself only after `Split.Pending` completed, because the
  `finally` is a dispatcher continuation and a dispatcher that is shutting down never runs
  it (the review of 2026-09-28 found 17 GB would stay behind).
- What is copied is what the script enters (`SirilScript.RequiredFolders`, exposed by
  `ScriptChoiceViewModel.RequiredFolders`): every such folder whole, except `lights/` (at
  most `SplitPlan.MaxLights` frames), `flats/` (the part's slice) and the session's output
  folders `SirilSession.OutputFolders` (`process/`, `masters/`), never copied. The frame count is
  re-read when the button is pressed, and every copy loses the read-only attribute a raw
  from a card carries, so `Delete()` can remove it.
- On screen "auto" is the file's shared range, not each map's own, so the chips never show a
  picture the file could not; the fixed caps behave as on every other tab. The maps are
  re-encoded per (map, cap) and cached, the Structure tab's rule.
- `Populate` is `internal` for the smoke test, which renders the tab from `SplitRun.Analyze`
  on generated masters.
- The table's horizontal `ScrollViewer` swallowed the wheel even with nothing to scroll
  vertically, so the page stood still with the mouse over the table; `SplitView.ForwardWheel`
  re-raises the wheel event on the parent. Each cell is a fixed-width grid with the value and
  the verdict chip on one line, so the cells of a row sit under their headers.
- The estimate line is recomputed on every `Refresh` (chips, script), and only when nothing
  blocks the button: `SplitPlan.CopiedBytes` enumerates each folder once (whole folders and
  lights times the parts, every flat once), the sensor size comes from the master's header
  through the same `loadInfo` seam as the profile prefill (`SplitCost.Pixels`), and the free
  space from `DriveInfo` of the temp root; the words, the units (Explorer's, 1024^3) and the
  model are the core's (`SplitCost`, `SplitReport.CostLine`). `Attach` and `StartAsync` refresh
  through `Script.Refreshed` alone, so the estimate runs once per change, not twice.
- `MainWindow.OnClosing` keeps cancelling the close while a stopped split is still deleting
  its folder (a second click on X must not win), and the continuation closes the window only
  if no new split started meanwhile.

### Three things about the WPF project that were learned the hard way

ADR 0037 holds all three - no `<InvariantGlobalization>` and `App.ForceInvariantNumbers` in
its place, the dispatcher captured once per run, `WindowSmokeTests` rendering every tab with
`FLATCHECK_DUMP_SCREENS` for eyes and never a golden - with the failure behind each, the
alternatives rejected and the guards. The console's side of the culture question is
"InvariantGlobalization" in `process.md`.
