# The two applications: console and window over one core

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): that neither application computes is CLAUDE.md
> and ADR 0011, the Siril session rules of the window are ADR 0033 to 0036, the WPF lessons
> ADR 0037, and this note keeps the mechanics of the window's tabs and the few reasons that have
> no other home. **Covers:** in full, the table of what each application does with the core's
> outputs (`FlatCheck/`) and `FlatCheck.Wpf/` `ViewModels` and `Views` (the tab mechanics below);
> by pointer, `FlatCheck.Wpf/Session` (ADR 0033 to 0035), `Presentation` and `Theme` (ADR 0036,
> 0037) and `Runs/RunOutcome.cs` (ADR 0011, 0036).
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
| input | one file (or a folder, with `--light`) | one Siril session folder - **except the Ratio tab**, which takes two files chosen by hand, normally from two *other* folders; the Histogram tab lists the flat master and every raw or FITS in `flats/`; the Netezire tab takes the session's master and writes beside it. A folder without a master in either `masters/` or `process/` can be preprocessed from the window, through the user's own `siril-cli` |
| report | written to stdout | shown as panels, saved as `Rezultat testare.txt` |
| map | written to `-o <path>` (`--ratio` writes a second, `-lume`; `--smooth` a second, `-dupa`), always on the automatic scale | shown from memory (`--ratio` and the Netezire tab offer both behind a toggle), on a switchable scale with a legend and a shape guide, saved as `flatcheck.png` - the file on the automatic scale, like the console's |
| smoothed flat | written to `--smooth <path>` | written to `pp_flat_stacked_netezit.fit` beside the master, on a button, after asking about an existing file |
| synthetic flat | written to `--synthetic <path>`, the lens from `--lens` text; `--against` compares two files | written to `pp_flat_stacked_sintetic.fit` beside the master, the lens from a searchable list, focal / f-stop / crop prefilled from the master's header and a raw in `flats/`; the comparison against the file follows the write by itself, and "Compara cu masterul" runs it alone on a file written earlier |
| result | exit code | coloured verdict |
| options | `--fov`, `--sat`, `--black`, `--pattern`, `--sky`, `--scale`, `--lens`, `--raw`, `--source`, `--focal`, `--fstop`, `--crop`, `--profiles` | `--sky` (a checkbox), the smoothing scale (three chips), the lens list, its three prefilled fields and the sensor-format chips behind the crop; the Structure tab's profile comparison takes the first raw in `flats/` by itself |

The console keeps the options because it is the scriptable one (ADR 0033); `--fov` and
`--sat` exist for telling the tool something the header does not say, and adding them to the
window would be four more controls in service of the rarer case.

### What the window adds, and what it does not

What the window adds - a workflow, never an analysis, a threshold or a wording - is ADR 0011
and ADR 0033. The "Despre" window, which formulates nothing, is ADR 0040. The three viewing
aids around the residual map (the scale switch with its caps from `Th.Resid*`, the legend, the
collapsed shape guide), why the guide is documentation and not a classifier, why End-to-end
gets the legend only, why the guide texts mirror the README by hand and why the guide's ring
row was rewritten are ADR 0036 (the ring row also ADR 0018). Two mechanics the ADRs do not
spell out: the scale switch defaults to ±10 %, and the guide's text is in
`Session/SessionMessages.cs`; Structure and Ratio get all three aids.

The Exposure tab's histogram card is drawing and wording too (ADR 0011): the paragraph under
the charts is `ExposureReport.HistogramReading`, the shaded target band *is* `ExposureBands`,
the band that voted (ADR 0012), and the collapsed guide's text is in `SessionMessages` like the
map guide's. `Histograms` is a **replaceable list, not an `ObservableCollection`**: picking a
file starts a run nobody awaits, and a second pick (or the Sky toggle) can start another over
it - scalar properties just let the last one win, but a collection cleared and refilled by two
runs at once ends up with duplicated rows and nulls in it. The light series is the opposite
case and does need `ObservableCollection`, because it appends rows as frames finish.

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
Ratio and Netezire tabs, which reuse `StructureRows.Tests`, are untouched. `Populate` is
`internal` and sets `HasResult` for the smoke test. The one thing the window cannot do
that the console can is `--lens` by hand: a body without a raw in `flats/` gets the
explanation, not a lens picker (ADR 0031: the raws are always in `flats/`).

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
`Func<bool>` on `MainViewModel`, `SirilCliIntegrationTests`). Three mechanics the ADR does
not spell out:

- `SirilSession.PreprocessingScript` is the script name the messages quote. The parameterless
  `MainViewModel` constructor uses `SirilInstall.Locate` and `SirilInstall.IsGuiRunning`, which
  looks for a `siril` process (`siril-cli`, ours, has a different name).
- `SirilScript.RequiredFolders` takes the `cd <name>` commands with a plain name only:
  `cd ../process` and `cd ..` are not inputs. Without the pre-check Siril dies at line three
  with a failed `cd` and the user reads the log to learn why.
- stderr stayed empty in every measured run and is read anyway.

**The Ratio tab is the one exception to "one folder in".** ADR 0033 holds why it proposes
nothing, starts empty and documents itself before it can be used, the `pp_flat*.fit` trap at
both layers (the candidate list, then the file dialogs), `RatioPattern` and why its text
properties are instance properties.

**The Histogram tab lists the flat master first, then the smoothed master when it exists,
then `flats/`, and lets a click interrupt a click.** The list and its order, the master not
preselected and not run at open, the smoothed master as a third kind of row (chip `NETEZIT`,
`IsMaster` false) found by exact path, and the one wire from `SmoothTabViewModel.Saved` to
`HistEq.FileWritten` are ADR 0033 and ADR 0036. What they do not say:

- The master is there for one question - did what the individual frames show average out? -
  and it is not run at folder open because the Structure run is reading that same 24 MP file
  at that moment. The smoothed one is there for the other question - what did the smoothing
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
  and lets only the latest one clear the busy state, show an error or drop the result; the
  other tabs never overlap runs, so for them nothing changes. `TabViewModelTests` pins it with
  a stub tab. The per-view PNGs are encoded on the UI thread and cached per chip, for the
  reason ADR 0036 measured on the map.

**The Netezire tab runs on a button and never on open**, awaits the structure run, writes
after the numbers are on screen, asks about an existing file, and its output never becomes the
session's master: ADR 0036, with ADR 0033 for why the smoothed stack still matches
`RatioPattern`. Mechanics the ADR does not spell out:

- Its guide (`SessionMessages.SmoothGuideRows`, mirrored in the README by hand) is the one
  guide that starts *expanded*: it is the button's precondition.
- Three scale chips (`SmoothScaleViewModel`; ADR 0027 for the ladder) and the button; the
  output is `pp_flat_stacked_netezit.fit` in the master's own folder
  (`SirilSession.SmoothedMasterPath`, `SmoothedSuffix`).
- The tab keeps the two rasters (removed component, smoothed flat's residual) behind a radio
  toggle, cached per (map, cap) like Ratio; `MapScaleViewModel` took a constructor argument
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
  most `SplitPlan.MaxLights` frames) and `flats/` (the part's slice). The frame count is
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
- The estimate line is recomputed on every `Refresh` (chips, script): `SplitPlan.CopiedBytes`
  sums the file sizes of the copy lists, the sensor size comes from the master's header
  through the same `loadInfo` seam as the profile prefill, and the free space from
  `DriveInfo` of the temp root; the words and the model are the core's (`SplitCost`,
  `SplitReport.CostLine`).

### Three things about the WPF project that were learned the hard way

ADR 0037 holds all three - no `<InvariantGlobalization>` and `App.ForceInvariantNumbers` in
its place, the dispatcher captured once per run, `WindowSmokeTests` rendering every tab with
`FLATCHECK_DUMP_SCREENS` for eyes and never a golden - with the failure behind each, the
alternatives rejected and the guards. The console's side of the culture question is
"InvariantGlobalization" in `process.md`.
