# Close the four questions the documentation backfill left

## Context

The backfill of 2026-10-22..24 (`docs/plans/2026-10-22-document-every-type-and-method-and-translate-the-comments.md`)
changed no code and left four questions on `main`: area 4's and area 5's code question (NaN and
infinity pass the number parameters), area 5's process question (the rule "Languages" still
describes Romanian C# comments), and the two code questions of area 5's review (a failed save
closes the window; the map legend computes a share the core already computes). On 2026-10-24 the
owner accepted the recommendations for items 1, 2 and 4 and, asked in this session, chose for
item 3 the message beside the button. Outcome: four commits on `claude/Close-the-backfill-questions`
(worktree `.claude/worktrees/Close-the-backfill-questions`, cut from `main` at 532b459), each
with the gate green, then `/wrap-up` with the merge proposal.

Facts checked on `main` at 532b459:

- `Parameters.Validate()` (`AstroPhotoAnalyzer.Core/Parameters.cs`) checks the target
  background (`TargetBackgroundError`, ADR 0040) and the passepartout-frame rule, nothing else.
  `Options.TryNum` and `ParameterFields.ParseNumber` read "NaN", "Infinity" and "-Infinity"
  (`NumberStyles.Float`, invariant culture); the applications' range checks (`<=`, `>`) are
  false on NaN, so NaN passes every number field, and "Infinity" passes `--sizes`, `--aspect`
  (both applications) and the console's `--distance`, `--passepartout`, `--frame`; the aspect
  with an infinite side gives a ratio of infinity, 0 or NaN. `AnalyzerCli.Run` calls `Validate`
  before reading (exit 1, `EROARE: ` + message); `ParameterFields.TryBuild` calls it and shows
  the message. The console's R-025 test (`CliTests`) shows the "refused before reading" pattern
  on an empty file.
- `MainViewModel.SaveReport` (`AstroPhotoAnalyzer.Wpf/ViewModels/MainViewModel.cs:167`) opens a
  `StreamWriter` with no try; `MainWindow.SaveReport` calls it from the click handler; no
  `DispatcherUnhandledException` handler. The Curbe tab's `Save` (`CurvesTab.cs:462`) is the
  pattern: catch, `SaveError = MainViewModel.Describe(e)`, else `SaveMessage`; `CurvesView.xaml`
  gives the two a fixed two-line `Grid` so that the message moves nothing.
- `MapViewModel.From` (`MapViewModel.cs:150`) computes `100.0 * classes.Count(c => c == cls) /
  classes.Length`; `DustMap.FractionOf` (`DustAnalyzer.cs:206`) divides by `PatchCount = Cols *
  Rows`, the length of `Classes`: the same figure. `PrintStretchSimulator` and `ValidationTests`
  already use `FractionOf`.
- `CLAUDE.md`'s rule "Languages" says "most of them still carry Romanian ones"; after the
  backfill no C# file of the code does. 34 of the test files still hold Romanian comments (a
  function-word scan; the backfill plan counted 29 files, about 246 lines). ADR 0005's Status
  already reads `accepted 2026-10-02; Romanian comments in the C# files superseded by the kit's
  ADR 0035`. `README.md` says nothing about comments. The kit's `GHID.md` (line 291) tells the
  history in the past tense ("tinea comentariile din cod in romana ... se traduc cand fisierul
  isi primeste documentatia"): still true, and the kit's file, not this project's.
- Requirement IDs run to R-123 with gaps that are not reused; the next ADR is 0045. The
  reference images for `/validate` are 12 files, about 1.4 GB without `play/`, in the main
  checkout's `AstroPhotoAnalyzer.Tests/testdata/large/`; a worktree has none.

## Branch and worktree

From the main checkout `E:\Photografie\Code` (on `main`, clean apart from the untracked
`.github/`):

```
git worktree add AstroPhotoAnalyzer/.claude/worktrees/Close-the-backfill-questions -b claude/Close-the-backfill-questions main
```

Every command below runs in the worktree. Save this plan as
`docs/plans/2026-10-24-close-the-backfill-questions.md` in the first commit.

## Item 1: refuse NaN and infinity once, in the core (commit 1, Fixed)

Requirement first, in `docs/requirements.md`: extend **R-024** with: what the syntax lets through
but no analysis can use, NaN and infinity (`double.Parse` reads "NaN", "Infinity",
"-Infinity"), is refused by the core's `Parameters.Validate()`, one Romanian message per
parameter, before any reading, so the console (exit 1, `EROARE: ` in front) and the window show
the same words; the target background keeps R-023's message; the aspect ratio must also be
positive (an infinite height gives 0). Example: `--dmax NaN` is `EROARE: Dmax trebuie sa fie
un numar; a primit NaN, care nu e un numar`, exit 1; `--aspect Infinity:1` names the aspect.
R-025 gets a clause: `Validate` also applies R-024's finiteness checks, in the parameters'
order, before the passepartout rule.

Tests, failing first:

- `ParametersTests`: `R024_NaNAndInfinityAreRefusedByValidateWithOneMessagePerParameter`, a
  theory over Dmax, distance, a width, the aspect (NaN, +inf, -inf, and 0), passepartout, frame;
  the message starts with the parameter's Romanian name, quotes the value as
  `TargetBackgroundError` does (`NaN, care nu e un numar`, `infinit`, `minus infinit`), is
  ASCII; finite values give null; the defaults still validate.
- `OptionsTests`/`CliTests`: `R024_TheConsoleRefusesNaNAndInfinityBeforeReading`: the probe's
  cases (`--dmax NaN`, `--distance NaN`, `--sizes NaN` and `60,Infinity`, `--aspect NaN:1`,
  `Infinity:1`, `1:Infinity`, `--passepartout NaN`, `--frame Infinity`) on an empty `.tif`, each
  exit 1, `EROARE: ` + the core's message, no "nu e o imagine".
- `ParameterFieldsTests`: `R024_NaNAndInfinityInAFieldGiveTheCoresMessage`, cases for Dmax,
  Distance, Sizes (`NaN`, `Infinity`), Aspect (`NaN:1`, `Infinity:1`, `1:Infinity`),
  Passepartout, Frame; `TryBuild` returns null and `error` equals the message the core gives for
  the same `Parameters`.

Code, in `Parameters.cs`:

- `static string FiniteError(string what, double v)`: null when finite (and, for the aspect,
  positive; a second helper or a flag), otherwise `"{what} trebuie sa fie un numar; a primit
  {got}"`, with the `got` wording lifted out of `TargetBackgroundError` into a shared
  `static string Quote(double)` so the two messages spell NaN and infinity alike.
- `Validate()`: after the target background's check, in field order: `DmaxOverride` ("Dmax"),
  `ViewingDistanceM` ("distanta de privire"), each `CandidateWidthsCm` ("latimea de print",
  with its position or value), `TargetAspect` ("raportul de aspect", finite and > 0),
  `PassepartoutCm` ("passepartout-ul"), `FrameCm` ("rama"); then the passepartout rule as
  today. Null and the defaults never trip it.
- Comments made true: `Options.TryNum` and `TryAspect` (the core's `Validate` refuses NaN and
  infinity, not the callers), `ParameterFields.ParseNumber`, `OptionalNumber`, `SizesOf`,
  `AspectOf` (the same), `Parameters.Validate`'s summary (what it now checks).
- `docs/design/console.md`, "Rules between parameters": the finiteness checks; the sentence
  "validate syntax only" gets the NaN/infinity clause. `docs/design/extending.md`, "A new
  parameter": add `Validate`'s finiteness check to the list.
- ADR 0045: "NaN and infinity are refused once, in the core's `Validate`, with one message per
  parameter" (context: the probes of areas 4 and 5; alternatives: in `TryNum` and `ParseNumber`,
  two places; a parse that rejects the words, which hides the reason; consequence: the
  applications keep their range checks and their syntax messages).
- Goldens: no console output changes (the help text is untouched); the gate's golden comparison
  confirms it. `/validate` runs at the end (below) because `Parameters.cs` moved.

## Item 2: reword the rule "Languages" (commit 2, Internal)

- `CLAUDE.md`, rule "Languages": replace "Comments in the C# files are English too (the kit's
  ADR 0035), but most of them still carry Romanian ones: ... Romanian, without diacritics." with:
  the C# comments of the code are English (the kit's ADR 0035; the backfill of 2026-10-22..24);
  the tests' Romanian comments stay until a task of their own, and a comment added to a test
  file follows that file's language; the comments of the csproj files, `Directory.Build.props`
  and the XAML files stay Romanian (ADR 0005). Keep "Never leave a file in two languages". Drop
  the "for a file not yet translated" clause of the rule's first sentence.
- ADR 0005: complete the Status clause with a note the kit's ADR 0024 grammar allows:
  `; Romanian comments in the C# files superseded by the kit's ADR 0035 (translated 2026-10-24;
  the tests', the csproj, props and XAML comments stay Romanian)`, and a `(Restated 2026-10-24:
  ...)` note right after `## Decision` quoting "the code comments of the existing files ... A
  comment added to one of them is Romanian" and saying what holds now. `scripts/check-decisions.ps1`
  (a gate step) checks the header grammar.
- `README.md` and `GHID.md`: nothing to change (checked above); say so in "What deviated" if
  that stays true after the reword.
- No code; the gate still runs (step 0 drift, decisions check).

## Item 3: a failed save is told beside the button (commit 3, Fixed)

Requirement first: extend **R-111**: "Salveaza raportul" tells its outcome beside the button:
`raport salvat: <path>` in faint text, or, when the file cannot be written (held open by
another program, a folder that refuses writing, a missing folder), the reason in red,
`MainViewModel.Describe`'s text; the window, its tabs and the analysis stay; the two texts are
cleared by the next run. **R-110** names the two-line place under the top bar's buttons.

Test, failing first, in `AstroPhotoAnalyzer.Wpf.Tests/ViewModelTests.cs`:
`R111_ASaveThatCannotWriteIsToldNotThrown`: run on `Scenes.CleanSky()` as the existing save
test does; hold the target open with `FileShare.None` and call `SaveReport` (no exception,
`SaveError` not null and ASCII-safe, `SaveMessage` null); a path under a folder that does not
exist (same); then a good path (`SaveMessage` contains the path, `SaveError` null, file
byte-identical to `WriteFull`); then `RunAsync` clears both. A line in `WindowSmokeTests`
asserting the two `TextBlock`s exist in `MainWindow` (by name), as the Curbe tab's test does
for its gallery area.

Code:

- `MainViewModel`: `public string SaveMessage`, `public string SaveError` (Set/notify as
  `CurvesTab`); `SaveReport` wraps the write in try/catch (`Exception`, as the Curbe tab: the
  dialog can also hand a path that throws `NotSupportedException` or `PathTooLongException`),
  sets one and clears the other; `RunAsync` clears both with `Error`. XML comments updated.
- `MainWindow.xaml`: under the three buttons (Grid.Column 2 becomes a vertical `StackPanel`:
  the buttons row, then a `Grid Height="34"` holding the two `TextBlock`s, named
  `SaveMessageText`/`SaveErrorText`, `Faint` and `BadBr`, `TextTrimming` and tooltip as in
  `CurvesView.xaml`). The Romanian text `raport salvat: ` is a view-model constant interpolating
  the path (explanation, not diagnosis).
- `docs/design/window.md`, `MainViewModel` paragraph and the layout line.

## Item 4: the legend's share is the core's (commit 4, Internal)

- Test first, in `OverlayWindowTests.R130_...` or a new
  `R130_TheLegendsShareIsTheCoresFraction`: for each of the five layers,
  `Assert.Contains($"{name}: {Fmt.Pct(100 * map.FractionOf(cls))} din dale", layer.Legend)`,
  with the class from `MapViewModel`'s palette order (`PatchClass.Dust, Sky, Band, BrightCloud,
  Peak`). It passes on `main` too (the figures agree), so the change is guarded by reading, not
  by a red test: say so in the commit.
- `MapViewModel.From`: `double pct = 100 * map.FractionOf(cls);`; drop `classes.Length` if
  unused (`classes` stays for `ClassAt`). Header: "It computes nothing" becomes true; say the
  share is `DustMap.FractionOf`. `window.md`'s `MapViewModel` row: "legend with the share
  (`DustMap.FractionOf`)". The Preview guide's `1 / ScreenStepBackFactor` and the amber legend's
  difference stay, as `ResultTabs.cs` and `PreviewWindowViewModel.cs` say.

## Verification

- After each item: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
  green, then the commit (one line, English, <= 100 chars, on the `claude/` branch).
- After item 1 (and again at wrap-up if anything numeric moved later): copy the manifest's
  files and the others `docs/validation.json` names from the main checkout's
  `AstroPhotoAnalyzer.Tests/testdata/large/` into the worktree's (not `play/`), then
  `/validate`; paste the drift table in the plan's "What deviated" (expected: 0 drift; the
  checks refuse values no case passes).
- Goldens: read any diff the gate reports; none expected.
- `scripts/check-design.ps1 -Since main` at wrap-up: `console.md`, `window.md`,
  `extending.md`, `code-map.md` read against the change.
- A manual run of the window (`dotnet run --project AstroPhotoAnalyzer.Wpf -- <image>`) is
  optional; the tests cover the view-model, and the XAML is covered by `WindowSmokeTests`.

## Wrap-up

`/wrap-up`: CHANGELOG under `[Unreleased]`: Fixed for items 1 and 3, Internal for items 2 and 4
(each naming the requirement and, for item 1, ADR 0045); the plan's "What deviated" with the
gate times and test counts per commit, the drift table, and the GHID/README check; then the
merge proposal, suggesting `/code-review main...claude/Close-the-backfill-questions` before the
merge. Memory: a pointer file for this task once merged.

## What deviated

(filled at wrap-up)
