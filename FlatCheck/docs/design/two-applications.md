# The two applications: console and window over one core

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `FlatCheck/`, `FlatCheck.Wpf/` (Session, ViewModels, Views, Presentation), `Runs/RunOutcome.cs`; what the window adds and what it must not.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## The two applications

Since 2.0.0 there are two front ends over one core: `flatcheck.exe` (console) and
`FlatCheck.Wpf.exe` (window). **Neither reimplements a number.** Both call `Runs/`, which
loads the file, calls the analyzer, writes the report into a `TextWriter` and encodes the
map. What differs is only what each does with the three outputs.

| | console | window |
|---|---|---|
| input | one file (or a folder, with `--light`) | one Siril session folder - **except the Ratio tab**, which takes two files chosen by hand, normally from two *other* folders; the Histogram tab lists the flat master and every raw or FITS in `flats/`; the Netezire tab takes the session's master and writes beside it. A folder without a master in either `masters/` or `process/` can be preprocessed from the window, through the user's own `siril-cli` |
| report | written to stdout | shown as panels, saved as `Rezultat testare.txt` |
| map | written to `-o <path>` (`--ratio` writes a second, `-lume`; `--smooth` a second, `-dupa`), always on the automatic scale | shown from memory (`--ratio` and the Netezire tab offer both behind a toggle), on a switchable scale with a legend and a shape guide, saved as `flatcheck.png` - the file on the automatic scale, like the console's |
| smoothed flat | written to `--smooth <path>` | written to `pp_flat_stacked_netezit.fit` beside the master, on a button, after asking about an existing file |
| synthetic flat | written to `--synthetic <path>`, the lens from `--lens` text; `--against` compares two files | written to `pp_flat_stacked_sintetic.fit` beside the master, the lens from a searchable list, focal / f-stop / crop prefilled from the master's header and a raw in `flats/`; the comparison against the file follows the write by itself, and "Compara cu masterul" runs it alone on a file written earlier |
| result | exit code | coloured verdict |
| options | `--fov`, `--sat`, `--black`, `--pattern`, `--sky`, `--scale`, `--lens`, `--raw`, `--source`, `--focal`, `--fstop`, `--crop`, `--profiles` | `--sky` (a checkbox), the smoothing scale (three chips), the lens list, its three prefilled fields and the sensor-format chips behind the crop; the Structure tab's profile comparison takes the first raw in `flats/` by itself |

The console keeps the options because it is the scriptable one, and because `--fov` and
`--sat` exist for telling the tool something the header does not say. Adding them to the
window would be four more controls in service of the rarer case.

### What the window adds, and what it does not

It adds a workflow: one folder instead of four invocations, `pp_flat_stacked.fit` found
by convention instead of by typing, structure and end-to-end started on open. It does not
add an analysis, a threshold or a wording. If a change here needs a new number, that number
belongs in `Analysis/`, and the console gets it too.

It adds a "Despre" window (button in the top bar, or F1), which adds no number either:
version, copyright and the warranty notice are the console's own strings, and the
third-party list is `ThirdParty.Components` from the core. See "Licensing" for why the
window has to exist at all.

It also adds three things around the residual map, none of which is a number: a **scale
switch** (auto / ±2 / ±10 / ±20 %, default ±10, caps from `Th.Resid*` - see "PNG output"),
a **legend** stating the real ends of the displayed scale, and a collapsed **shape guide**
saying what each shape on the map has meant on real masters. The guide is documentation,
not a classifier - the tool still does not decide what a shape is, and must not start:
a blotch can be a light leak or a cloud, and the pixels do not tell them apart. Its text
is in `Session/SessionMessages.cs` and mirrors the README's table **by hand**; no test
binds the two. Structure and Ratio get all three; **End-to-end gets the legend only**,
because its map is the fitted radial term, graded against `Th.LightRad*` rather than the
residual scale, so those caps would mean nothing on it.

The guide's ring row was **rewritten** after the artefact described under `RadialModel`
was found: it used to say rings meant a decentred round obstruction, which was the reading
of an artefact, and sent the author looking for an object that had never been there. Don't
restore the old wording from git history - `TheRingRowDoesNotClaimARingMeansADecentredObstruction`
fails if you do.

The Exposure tab adds the same kind of thing around its histograms: a card of one chart per
channel, a paragraph derived from the result (`ExposureReport.HistogramReading`), and a
collapsed guide whose text is in `SessionMessages` and mirrors the README **by hand** - no
test binds the two, exactly like the map guide. All of it is drawing and wording; the
numbers come from `ExposureAnalyzer` and the bands from `ExposureBands`, so the shaded
target band *is* the band that voted. `Histograms` is a **replaceable list, not an
`ObservableCollection`**: picking a file starts a run nobody awaits, and a second pick (or
the Sky toggle) can start another over it - scalar properties just let the last one win,
but a collection cleared and refilled by two runs at once ends up with duplicated rows and
nulls in it. The light series is the opposite case and does need `ObservableCollection`,
because it appends rows as frames finish.

Above the charts sits a switch, `HistogramSourceViewModel`, on the `MapScaleViewModel`
pattern: two radio chips, raw data or the camera's preview. Flipping it rebuilds the rows
from the **retained** `ExposureResult`; nothing is re-read or re-analysed. It is disabled
when the file carries no preview (every FITS, and a raw whose preview would not decode), and
disabling it forces the selection back to raw. The chips' `GroupName` is
`ExposureHistogramSource`, unique in the window for the same reason the map scale ones are.
Under the charts, a three-row table pairs the raw centre median with the preview's, per
colour, on both views - it is the payoff, so it is never hidden behind the switch.
`Populate` is `internal` so a test can hand in a result carrying a preview built from a JPEG
the test encoded itself (`TestImages.JpegOf`); no raw file is needed anywhere in the suite.

The Structure tab carries a fourth block, `HowToReadPanel`, under the verdict: **which
half of the screen decides what.** The numbers answer "how much" and own the verdict; the
map answers "what kind" and never votes. The screen's own layout suggests the opposite -
two columns look like two views of one thing - so it is said in words. Same collapsed
pattern and the same `GuideRow` template as the map guide, text in `SessionMessages`
beside it, state in `HowToReadViewModel`. The disclosure toggle those three share lives in
`Dark.xaml` as `Style x:Key="Disclosure"`; it had been hand-written twice before that.

**The Structure tab compares with the profiles in the same run, when `flats/` holds a
raw.** `SirilSession.ReferenceRawPath` (the first raw, through `LensIdentification.FirstRaw`
- the Sintetic tab's prefill reads the same file) makes the run exactly `flatcheck master
--raw <that raw>` with the window's `ProfileCatalog`, so the saved report stays the
console's and the verdict appears once, already combined - not PERFECT on five tests and
REFA two seconds later. The catalog is awaited before the run (a second or two on the first
open, under the busy overlay, which says so). Without a raw the run is the plain one, the
report identical to `flatcheck master`, and the card under TESTE shows
`StructureReport.GapText` computed by `StructureRun.Compare` on the master's header alone.
The rows are `LensProfileRow` (public copy of the core's `ProfileRow`), rendered by a
template local to `StructureView.xaml`; `Tests` stays five, so the scale heading and the
Ratio and Netezire tabs, which reuse `StructureRows.Tests`, are untouched. `Populate` is
`internal` and sets `HasResult` for the smoke test. The one thing the window cannot do
that the console can is `--lens` by hand: a body without a raw in `flats/` gets the
explanation, not a lens picker (the user's decision, 2026-09-18: the raws are always there).

#### Two places for the master

Siril has two conventions for where `pp_flat_stacked.fit` lands, and both exist in the
author's archive: the scripts repo (`OSC_Preprocessing_WithoutDark.ssf` and its siblings)
stacks with no `-out=`, so the master stays in `process/` beside the calibrated frames;
the scripts shipped with the 1.4 install (`OSC_Preprocessing.ssf`, `Mono_Preprocessing.ssf`,
the `Extract_*` ones) use `-out=../masters/pp_flat_stacked`. `2026-06-18-doar-cer-acasa` is
the first kind, `2026-08-12-Smida` the second, and the window refused the second for a
whole version because `FlatMasterPath` was a fixed path into `process/`.

`SirilSession.FlatMasterCandidates` lists both paths, `FindFlatMaster()` picks the one
that exists, and `FlatMasterPath` is that or null. Three decisions, all the user's:

- **When both exist, the newest wins** (by last-write time) - that is what the last
  preprocessing produced - and `FlatMasterLookup.Other` names the older one so the
  Structure tab can show `SessionMessages.TwoFlatMasters` under the source line. Two
  masters in one folder are two runs, possibly from two sets of flats, and the user has
  to know which one the numbers are about. The Histogram tab's master entry is the same
  file, through the same lookup.
- **Only the master is required.** `Check()` no longer demands `process/`: a folder with
  `masters/` and no `process/` (frames deleted to save space, master kept) runs
  Structure, and the End-to-end tab explains by itself that it has no series.
  `NoProcessDir` now means "neither folder exists", i.e. nothing was ever run.
- **Nothing else moved.** The light series stays in `process/` under both conventions;
  the console takes explicit paths; the core knows no folders. The Ratio dialog starts
  in the folder the open session's master is in, since that is where the stacks are.

The problem message for a missing master prints both paths it looked at.

#### Running the Siril script from the window

The problem card (no preprocessing run at all, or no flat master in either `masters/` or
`process/`) carries a **Ruleaza in Siril**
button. It runs `siril-cli.exe -d <session> -s <script>` from the user's own install and
shows the output live in `SirilRunWindow`; when that window closes after an exit code of 0,
`MainViewModel.ReopenAsync` runs `Check()` again, so whether the master appeared is decided
by the same code as always, never by the run window. Nothing here is a number, so nothing
is in the core.

What was measured before it was built, on Siril 1.4.4, and what follows from it:

- **stdout is live through a pipe.** Timed with a Python reader: the gaps between lines
  match the work, not a flush at exit. So a plain `Process` with redirected stdout is
  enough; the named-pipe mode (`-p`) was not needed. Exit code 0 on success, 1 when the
  script stops at a command; stderr stays empty but is read anyway.
- **Three quarters of the output is progress.** 1735 lines for a 3+3+3-frame session, 1324
  of them `progress: <label>, x%` or the ANSI-prefixed update form
  (`ESC[A ESC[2K Tprogress: 66.67%`). `SirilLogLine` turns those into a bar and shows only
  the `log:` lines as text, which is what Siril's own log pane shows. The debug prints
  ("Block 0: ...", "seqfile ... already exists") are dropped. The parser is pinned on
  recorded lines, not on what Siril ought to print.
- **Re-running overwrites cleanly.** The script run a second time over a complete
  `process/`, and a third time over one with the master and the `pp_light*` files deleted,
  rebuilt every file and exited 0; the only complaints were `seqfile ... already exists,
  not overwriting`, about the `.seq` index files, which is harmless. So "Opreste" mid-run
  leaves a folder the next run fixes, and the cancel message says so.
- **Siril prints only the verb** (`Running command: cd`). `SirilRunViewModel` counts those
  lines and reads the full command out of the script (`SirilScript.Commands`), so the
  window and the failure verdict can say `comanda 2 din 20:  cd biases`.

Four decisions:

- **The script is chosen on the card, not in the run window.** The window is the log of a
  run already started; a window that first asks and then runs would be two screens in
  one. The list is what Siril's script menu shows - the `.ssf` entries of `selected_scripts`
  plus the `.ssf` files under `script_path`, from `%LOCALAPPDATA%\siril\config.<ver>.ini` -
  and `OSC_Preprocessing_WithoutDark.ssf` is preselected when present. **Nothing is
  bundled**: the tool runs exactly the file Siril would, from wherever Siril keeps it (on
  this machine the scripts repo Siril clones into `%LOCALAPPDATA%\siril-scripts`, not the
  install's `scripts/` folder, which holds only the six stock ones).
- **WithoutDark is the default because it is the user's script** (a camera without
  darks), not because of where it writes: since "Two places for the master" above, the
  stock `OSC_Preprocessing.ssf` works just as well. `SirilSession.PreprocessingScript` is
  the name the messages quote.
- **Inputs are checked before launching.** `SirilScript.RequiredFolders` reads the `cd
  <name>` commands with a plain name out of the chosen script (`cd ../process` and `cd ..`
  are not inputs), and `SirilSession.MissingInputFolders` refuses a folder that lacks any
  of them or has one empty - Siril would otherwise die at line three with a failed `cd`
  and the user would read the log to learn why. It is a rule of form, not an understanding
  of the script; a script that enters folders differently is simply not pre-checked.
- **Siril open is a warning, not a refusal.** `SirilInstall.IsGuiRunning` looks for a
  `siril` process (`siril-cli`, ours, has a different name). Two processes writing the same
  `process/` corrupt it, but the tool cannot know which folder the GUI has open. Checked when
  the card is built and again on the click.

`MainViewModel` takes the two machine questions - `Func<SirilInstall>` and `Func<bool>` -
as optional constructor arguments so the tests do not depend on what the running machine
has installed; the parameterless constructor uses `SirilInstall.Locate` and
`IsGuiRunning`. `SirilCliIntegrationTests` do use the real `siril-cli` when it is there
(a probe script that loads the fixture master, and one that fails on `cd nu-exista`) and
pass without checking anything when it is not - xunit 2 has no dynamic skip.

The scale switch is why `StructureTabViewModel` and `RatioTabViewModel` keep the residual
**raster** rather than only the PNG bytes: the raster is already in the result, whereas
pre-encoding four PNGs per run in the core would be ~7 MB and three encodings nobody may
look at. `RunOutcome` was deliberately not touched. Re-encoding happens on the UI thread,
which was measured rather than assumed: on the real 24 MP masters (1504x1004 analysis
raster) a cap costs 90-255 ms, once, and is then cached per option. Moving it to `Task.Run`
would need a generation counter so a stale result cannot land after a fresh one - more
complexity than the delay it removes.

Every **file** stays on the automatic scale whatever chip is selected, so both applications
keep writing the same bytes for the same master; the chips are a viewing aid. That changed
one existing test, on purpose - see
`OnTheAutoScaleTheDisplayedMapIsTheSameFileThatWasSaved`, which used to assert that
unconditionally.

**The Ratio tab is the one exception to "one folder in".** It proposes nothing and starts
empty, because the two masters it needs are two *shoots*, hence two session folders - the
open one is at most one of them. It used to list `pp_flat*.fit` from `process/` and
preselect two, which in a normal session offered the individual frames; a pair of single
frames yields a confident, wrong answer, because one frame's noise is the same order as
the signal. So it asks for two files explicitly, and carries a panel that explains what
the mode needs and what it cannot tell you - the only tab that must document itself before
it can be used at all.

The same trap survived one layer down until it was found in use: the two file dialogs
filtered on `pp_flat*.fit`, which in a real `process/` is mostly `pp_flat_00001.fit` and
its ninety siblings, and `SirilSession.IsFlatMasterName` accepted exactly what the filter
offered. So the tab still *proposed* the pair that yields the confident wrong answer.
`RatioPattern` is now `pp_flat_stacked*.fit` - the stack, not the frames - the dialog
builds its filter from that constant rather than repeating the string, and the refusal
message says why an individual frame is not a master. The `*` after `stacked` is there
because two masters brought into one folder must be renamed, and
`pp_flat_stacked_0deg.fit` is still a stack. `RatioTabViewModel`'s text properties are deliberately *instance*
properties even though their values are constant: `{Binding}` resolves through the
DataContext and never sees a static one, so the documentation would have rendered blank.

**The Histogram tab lists the flat master first, then the smoothed master when it exists,
then `flats/`, and lets a click interrupt a click.** The master is there for one question
- did what the individual frames show average out? - and it is not preselected and not run
at folder open, because the Structure run is reading that same 24 MP file at that moment.
The smoothed one is there for the other question - what did the smoothing take out, what
did it leave - and it is a **third kind of row**, not a second master: chip `NETEZIT`,
`IsMaster` false, its own note under the picture (`HistEqReport.SmoothedVersusMaster`,
which also says why a smoothed flat looks *noisier* under a rank-based transform). It can
also appear **during the session**: `SmoothTabViewModel.Saved` fires once, with the path,
right after the file is written (not on a refused overwrite, not on a write error), and
`MainViewModel` wires it to `HistEq.FileWritten` in its constructor - the one wire between
two tabs, placed there so neither knows the other. `FileWritten` rebuilds the list from
`HistEqCandidates()` (one definition of the list), keeps the selection by path without
re-running, and re-runs only if the selected file *is* the one rewritten - the picture
would otherwise be of bytes no longer on disk. It ignores any other path and a missing
session, and `Reset` drops the session for that reason. Rebuild, not insert: `ClosesGroup`
(the separator under the last of master + smoothed) is an `init` flag set by whoever sees
the whole list. A Histogram read overlapping a Netezire write is accepted, not prevented:
the write gets `IOException`, lands in `SaveError`, and the numbers stay. The loader covers the picture, not the
tab: the list stays clickable, a new pick cancels the running frame's own token and starts
over, and a superseded run may not touch the screen. That last part is general and lives in
`TabViewModel.RunGuardedAsync`: it numbers its runs and lets only the latest one clear the
busy state, show an error or drop the result; the other tabs never overlap runs, so for
them nothing changes. `TabViewModelTests` pins it with a stub tab. The per-view PNGs are
encoded on the UI thread and cached per chip, for the same measured reason as the map.

**Structure and the light series run one after the other, not in parallel.** A 24 MP frame
runs into the high hundreds of MB in analysis (known issue 7) and the series holds a loaded
frame besides; two chains at once could pass a gigabyte on an ordinary machine. The time
saved would not be worth it.

**The Netezire tab runs on a button and never on open.** It writes a ~96 MB file beside
the master, and a file is not something to produce because a folder was looked at. Its
guide (`SessionMessages.SmoothGuideRows`, mirrored in the README by hand) is the one guide
that starts *expanded*: it is the button's precondition. Three scale chips
(`SmoothScaleViewModel`: half, default, double of `SmoothAnalyzer.DefaultScalePx`, so no
second list of numbers) and the button; the button awaits `MainViewModel.WhenIdleAsync`
first, so a click during the structure run does not overlap it on the same 24 MP master.
The file is written *after* the numbers are on screen, and an existing file is asked
about through a `Func<string, bool>` the `MainViewModel` constructor takes (a `MessageBox`
in the app, a fixed answer in tests); a "no" keeps the old file and says so, the result
stays. The output is `pp_flat_stacked_netezit.fit` in the master's own folder
(`SirilSession.SmoothedMasterPath`, `SmoothedSuffix`), and **it must not become the
session's master**: `FlatMasterCandidates` are exact paths, so the Structure tab keeps
measuring the shot master and keeps showing the texture the smoothing was made for. It
does match `RatioPattern`, which is right - a smoothed stack is a stack - and it is listed
by the Histogram tab as its own kind of row, see above. The tab keeps the
two rasters (removed component, smoothed flat's residual) behind a radio toggle, cached
per (map, cap) like Ratio; `MapScaleViewModel` took a constructor argument for its
default cap so this tab can start at ±2 % (`Th.ResidPerfect`) - what is removed is small,
and at ±10 % the map would be white. `Populate` is `internal` and sets `HasResult`, so the
smoke test renders the tab from an analyzer result without writing a file. The second
button, `Propune scara`, runs `SmoothRun.Advise` the same guarded way, fills the rung
rows and the verdict from `SmoothReport`'s advice strings, selects the proposed chip
through `SmoothScaleViewModel.SelectScale`, and writes nothing; `PopulateAdvice` is
`internal` for the smoke test.

### Three things about the WPF project that were learned the hard way

- **No `<InvariantGlobalization>`**, however tempting the symmetry with the console. WPF
  needs culture data to bind anything at all: without it the window throws
  `Cannot find non-neutral culture related to 'en-us'` while *loading* XAML - not on some
  path, on every start. The invariance that is actually needed is narrower and is done in
  code: `App.ForceInvariantNumbers` pins `DefaultThreadCurrentCulture`, so a Romanian
  Windows does not turn `0.24` into `0,24` in a file whose whole point is being identical
  to the console's output. `CultureTests` pins both halves.
- **The dispatcher is captured once per run, not asked per frame.** `LightSeriesRun`'s
  callback arrives on a pool thread and has to marshal onto the UI thread to touch an
  `ObservableCollection`. Reading `Application.Current` each time means the answer can
  change mid-series, half the rows land on one thread and half on another, and rows are
  lost silently - six frames analysed, five rows in the table. Measured, in a test run
  where the WPF `Application` appeared partway through.
- **`WindowSmokeTests` exists because the unit tests cannot see XAML.** A wrong resource
  key, a missing converter, a binding to a property that isn't there - all compile, and
  some fail at load. The suite renders every tab with real results into a
  `RenderTargetBitmap` and asserts that no binding error was traced. The
  `InvariantGlobalization` crash above passed 100 unit tests and never started; it would
  fail here. `FLATCHECK_DUMP_SCREENS=<dir>` also saves the renders as PNGs - useful for
  looking at the window without running it, and not a golden comparison: how a window
  looks is a job for eyes.

