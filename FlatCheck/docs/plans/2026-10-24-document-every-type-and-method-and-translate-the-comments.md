# Document every type and method, and translate every comment to English

## Context

The kit's rule "Documentation comments" (the kit's ADR 0035) asks for an XML documentation
comment on every type and method in the code; code written before it gets them in a task of its
own. FlatCheck's rule "Languages" translates a file's Romanian comments when it gets them. The
owner asked (2026-10-24): backfill the XML comments, make every other comment in the code English
too, comments only in English from now on, and change no behaviour. Answers given in planning:
**five areas**, the **documentation file on first, all projects**, and **English now, a file
allowed both languages until its area lands**.

Outcome: every type and method in `FlatCheck.Core/`, `FlatCheck/`, `FlatCheck.Wpf/` documented;
every comment in the five projects (C#, XAML, csproj) English; code token-identical to `main`;
the gate green after each area. AstroPhotoAnalyzer's
`docs/plans/2026-10-22-document-every-type-and-method-and-translate-the-comments.md` is the
pattern; this plan says only where FlatCheck differs.

## Facts measured (2026-10-24, `main` at 941deff)

Counter: the backfill scratch tool `doccount` (Roslyn syntax walk; modes `count`, `romanian`,
`width`, `tokens`, `facts`), copied from LightsCheck's session and built in this session's
scratchpad (`...\scratchpad\doccount`).

- **Code** (121 `.cs` files, 21,103 lines): 217 types, 164 to touch (152 undocumented, 12
  missing `<param>`/`<typeparam>`); 857 methods, 834 to touch (462 undocumented, 372
  incomplete: 312 missing a `<param>`, 322 a `<returns>`, 1 a `<typeparam>`); 79 constructors,
  19 plain `Task`s, 12 overrides, 1 `<inheritdoc/>`; 550 properties/events (336 undocumented,
  not required).
- **Romanian:** 3,845 comment lines with a Romanian function word in 119 of the 121 code files,
  written without diacritics; the existing `///` are mostly bare text, no tags. Tests: 899 lines
  in 43 of 78 files. XAML: 162 comments in 21 files. csproj: comments in 5 project files
  (`Directory.Build.props` and `Directory.Packages.props` are already English).
- **Widths:** 275 comment lines run past 100 columns.
- **Compiler pre-check:** no `///` line holds a bare `<` or `&`; two hold `>`, which is legal.
- **Tests that read sources:** `LicenseHeaderTests` (header, unchanged), `RepoInvariantTests`
  (test names, and the `ProcessStart` regex over every source file, comments included: a
  translated comment must not write `Process.Start(` or `new Process(` outside the two allowed
  files), `IconTests` (`<ApplicationIcon>` in the csproj), `VersionTests`, `ThirdPartyTests`,
  `InvariantGlobalizationTests` (runtimeconfig). String literals, assertion messages and Romanian
  output never change.
- **No FlatCheck branch or worktree open.** LightsCheck holds copies of FlatCheck's readers and
  waits for this backfill to recopy them (memory; not this task).

## The documentation file (owner: yes, first, all projects)

First code commit of area 1: `Directory.Build.props` gets `GenerateDocumentationFile` true,
`NoWarn` CS1591, `PublishDocumentationFiles` false, with an English comment why (CS1591 asks for
public members only and the core is internal; nothing reads the XML at run time). Then the
compiler, under the gate's `-warnaserror`, fails CS1570/1572/1573/1574/1587/1734/0419. ADR 0059
records it (as APA's ADR 0038; a value of this project). One sentence in `CLAUDE.md` "Style".
Checked: gate green, a publish of both `kit.json` entries into the scratchpad ships no `.xml`.
Anything firing beyond what the pre-check expects stops the session with a report.

## English from now on (owner: English now, mixed until the area)

Area 1 also writes ADR 0060, "Every comment is English; the backfill translates the rest", and
rewords `CLAUDE.md` "Languages": every new comment English, in any file; a file not yet
translated may hold both languages until its area translates it; the exception ends with area 5,
whose wrap-up removes it from the rule. ADR 0006's status line gets the pointer ADR 0024 allows.

## The five areas: one branch, one session each, merged before the next is cut

| # | Branch `claude/...` | Files | Code lines | Types | Methods | RO lines | Notes to read |
|---|---|---|---|---|---|---|---|
| 1 | `Document-the-core-base`: Core root files, `Metrics.cs`, `Thresholds.cs`, `LensProfiles/` | 24 | 4,620 | 31 | 170 | 1,078 | `radial-model-even-term.md`, `lens-profiles.md`, `thresholds.md` |
| 2 | `Document-the-analyzers-and-reports`: `Analysis/`, `Report/` | 22 | 4,904 | 38 | 213 | 943 | `smooth-validation.md`, `thresholds.md`, the notes `docs/design/README.md` maps |
| 3 | `Document-the-runs-console-and-session`: `Runs/`, `FlatCheck/`, Wpf `Session/ Interop/ Mvvm/ Presentation/`, `App`, `MainWindow` | 35 | 4,999 | 52 | 266 | 872 | `two-applications.md` and the Runs/console notes |
| 4 | `Document-the-view-models`: Wpf `ViewModels/` | 22 | 5,726 | 25 | 144 | 884 | `two-applications.md` |
| 5 | `Document-the-views-and-translate-tests`: Wpf `Views/`, every XAML comment, the tests' comments, the csproj comments; the rule's exception closed | 18 + 21 XAML + 43 tests + 5 csproj | 854 | 18 | 41 | 68 + 899 + 162 XAML | `two-applications.md`, `process.md` |
| | **Total** | | **21,103** | **164** | **834** | **3,845 + tests/XAML** | |

Order: the layers bottom up, so later comments point at settled contracts (`Img`, `Stats`,
`IFrameReader`, `Th`). Worktree names stay under ~41 characters (Windows path limit).

## Assumptions (decided, stated)

1. **Scope:** "toate restul commenturilor din cod" covers the tests, XAML and csproj comments too
   (area 5). Tests stay exempt from XML tags: their comments are translated only.
2. **Tags:** `<summary>` always; `<param>` per parameter (a positional record's on the type);
   `<typeparam>`; `<returns>` for a value, none for a plain `Task`. No new `<exception>`/
   `<remarks>`; a throw a caller must plan for goes in the summary. `<inheritdoc/>` only where a
   project contract (`IFrameReader`, the tab view-model base) says it all; framework overrides,
   converters and constructors get their own text. Event handlers tag `sender` and `e`.
3. **Content:** what a caller needs, every number with its unit or scale (ADU, 0..1 normalised,
   pixels, degrees, fraction, percent), what null/NaN mean; a threshold is named by `cref` into
   `Th`, never quoted. `cref`s fully qualified rather than a new `using` (the token proof). A doc
   block goes above attributes; a local function keeps `//`. Every comment line within 100
   columns; ASCII only.
4. **Translation keeps the meaning:** every number, identifier, ADR/R- ID, quote and CAPITALISED
   emphasis stays; paragraphs stay where they are; Romanian UI output quoted as the user sees it.
   A glossary (flat, master, vignetting, ring, rotation, diffuser, light, raw...) is written in
   area 1 and used by every later area.
5. **A comment found false** is translated as it stands, then corrected in a commit of its own
   with its evidence and listed at wrap-up. Code that seems wrong is reported, never changed.
6. **Nothing else changes:** no requirement, no test logic, no golden, no README, no version.
   `CHANGELOG.md`: `Internal` lines for the documentation file and the backfill (updated per area).
7. **`/validate`** runs in areas 1 and 2 (they hold `Metrics.cs`, `Stats.cs`, `Analysis/`,
   `Report/`), though the token proof already shows no formula moved; if the masters under
   `E:\Photografie\Astro\Flats and Darks` are missing (SirilCleanup moves them), the session says
   so and does not invent figures.

## Each area's session

- Setup: `git worktree add .claude/worktrees/<Task-name> -b claude/<Task-name> main` from
  `E:\Photografie\Code`; edits by absolute path in the worktree; commits with the hook's `== OK`.
- Steps: count the area; read its notes and `known-issues.md`; per file, dependency order,
  translate every comment and complete every type and method; checks; commit (one or two per
  area); false-comment commit; `/validate` (areas 1-2); `/wrap-up` with this plan's "What
  deviated" section for the area; propose the merge.
- At each wrap-up I give the prompt for the next area, to paste into a session opened in
  `E:\Photografie\Code\FlatCheck`.
- Area 1, this session: commits (1) this plan to `docs/plans/2026-10-24-document-every-type-and-method-and-translate-the-comments.md`;
  (2) the documentation file + ADR 0059; (3) ADR 0060 + `CLAUDE.md` "Languages"; (4) the Core
  root files; (5) `Metrics.cs`, `Thresholds.cs`, `LensProfiles/`; (6) false comments; wrap-up.

## Verification, every area

- The gate green at each commit (hook `== OK`, test counts as on `main`).
- `doccount count` on the area: 0 types and 0 methods to touch.
- `doccount tokens` against an export of `main`: every changed `.cs` token-identical (string
  literals included). XAML and csproj (area 5): the diff read line by line, only comments moved.
- `doccount facts`: every number, ID and identifier of the old comments found in the new ones.
- `doccount romanian`: no line left, or only names and quoted output, each listed.
- `doccount width`: no comment line over 100 columns; added lines ASCII only.
- `scripts/check-design.ps1 -Since main`: each note listed read against the change.
- The whole diff read back before each commit.

## What deviated

Each area adds its own section at its wrap-up.

### Area 1, the core's base (2026-10-24, `claude/Document-the-core-base`)

- **The documentation file found 34 errors, not the handful the pre-check expected,** in 15
  files of all five projects: 20 parameters left untagged in 9 members that tag others
  (CS1573), 3 blocks with a bare `<dir>`, `<versiune>` or `<nume simplu>` (7 errors, CS1570),
  3 `<paramref>` to a local or a tuple element (CS1734), 2 `cref`s to a renamed or another type's
  member (CS1574) and 2 to an overloaded one (CS0419). The plan said to stop and report; put to
  the owner, the answer was "fix the 34 now": the smallest change each, the missing tags in
  English beside the Romanian ones (ADR 0060's exception), in the documentation-file commit.
  Files of areas 2-5 already touched that way: `ExposureAnalyzer.cs`, `HistEqAnalyzer.cs`,
  `SmoothScaleAdvisor.cs`, `StructureReport.cs`, `LightSeriesRun.cs`, `SplitRun.cs`,
  `StructureRun.cs`, `FlatCheckCli.cs`, `SirilInstall.cs`, `SplitPlan.cs`,
  `SyntheticTabViewModel.cs` and the tests `RawReaderTests.cs`, `StatsTests.cs`,
  `SirilInstallTests.cs`. A publish of both `kit.json` entries ships no `.xml`.
- **Four comments corrected, in a commit of their own:**
  1. `IFrameReader.cs`, header: `ImageLoader` names both readers in its list
     (`ImageLoader.cs`, `Readers`); it does not "know none of them by name".
  2. `Version.cs`, header and `Copyright`: `<Version>` and `<Copyright>` live in
     `Directory.Build.props` (its own header says so, and `VersionTests` checks it), not in
     `FlatCheck.csproj`.
  3. `FitsReader.LoadWithHeader`: the keys read from the dictionary are not all numeric;
     `BAYERPAT`, `ROWORDER`, `INSTRUME` and `DATE-OBS` are strings (none holds a slash).
     `known-issues.md`, point 1, said the same and was corrected with it.
  4. `FitsReader.HStr`: used for `BAYERPAT`, `ROWORDER` and `INSTRUME` too, not only for
     `DATE-OBS`.
- **The glossary** below was written at this area's end rather than its start.
- **Measured on 2026-10-24:**
  - The counter on area 1's 24 files: on `main` 31 types and 170 methods to touch; on the
    branch 0 and 0. The whole code has 133 types and 664 methods left for areas 2-5.
  - `tokens`: all 121 code files and 78 test files identical to `main`'s code, 35 of the code
    files with changed comments or layout.
  - `facts`: no number, ID or identifier of the old comments missing, except two judged:
    `MapResidual` (an `<inheritdoc cref>` replaced by its own text) and `PackageReference`
    (now "PackageReferences").
  - `romanian`: no line left in the 24 files, but one quoted output
    (`LensIdentification.cs`, "focala din FOCALLEN").
  - `width`: no comment line over 100 columns in the 24 files; every added line ASCII.
  - `/validate`: the 40 cases ran on the real masters, all present; 300 figures, every one
    identical to the recorded value.
  - The gate: `== OK` at each of the six commits, 951 + 331 tests.

### Area 2, the analyzers and reports (2026-10-24, `claude/Document-the-analyzers-and-reports`)

- **No documentation-file error and no code change**: the 22 files built clean under the gate
  at the first try.
- **Five comments corrected, in a commit of their own** (four in the commit message's count,
  the fifth found while correcting the fourth, in the same header):
  1. `LightSeriesAnalyzer.cs`, header: the series' files are loaded in `Runs/LightSeriesRun.cs`,
     not in `FlatCheckCli` (it calls `LightAnalyzer.Analyze` and `Combine`; the console calls
     `Runs/` only).
  2. `SmoothAnalyzer.Analyze`, "what was removed": `Prepare` is linear but a ratio is not;
     `Prepare(a) / Prepare(b)` equals `Prepare(a / b)` only where `b` is constant over a block,
     which the smoothed flat nearly is at the analysis block.
  3. `ExposureReport`, the "for the window" section: `ChannelColumn`, its first member, is
     printed by the console (`Write` uses it for the per-channel rows).
  4. `LightSeriesReport.cs`, header: `Runs/LightSeriesRun.cs` writes the header, the rows and
     the summary, for both applications, not "the CLI".
  5. The same header: `Report/` is not "the only place with Romanian text"; the error messages
     are thrown in Romanian where they arise, in `Analysis/` and `Runs/`.
- **One figure first left for the owner, then corrected:** "a thirtieth of the memory" in
  `RatioAnalyzer.Analyze`, `StructureAnalyzer.AnalyzePrepared` and `SmoothAnalyzer.Analyze`,
  and in ADR 0019 ("Averaging at full resolution"). By `Img.Prepare`'s arithmetic the analysis
  raster has (2k)^2 times fewer pixels, k = floor(long side / 2 / 1200): 16 on the D3300's
  6016 x 4016 (3008 -> k = 2), not 30. Asked, the owner did not know and said to find out
  from the code. Measured: all 16 masters under `E:\Photografie\Astro\Flats and Darks` are
  6016 x 4016, and the console prints `dimensiune analiza : 1504 x 1004` on
  `Samyang/ISO400/f2.0-sky`, so 16. The figure dates from the Ratio rewrite (eb13d63,
  2026-09-09); `Prepare` was the same then. The three comments now say a sixteenth, and ADR
  0019 got a dated correction note (the kit's ADR 0024, a wrong figure).
- **A high-effort `/code-review`** of the branch found nine comment findings and no code
  issue; eight fixed in a commit of their own (`VignettingComparer.Analyze`'s gap order, which
  said a null catalog means NoProfiles; the absolute correlation in `Combine`; the return
  shapes of `Levels`, `DepthLightScaleNote` and `Diagnosis`; `AnalyzePrepared`'s second caller,
  the smoothing mode; the full lists in the `ExposureReport` and `RatioReport` headers). The
  ninth is the "thirtieth" figure above, corrected after the owner's answer.
- **`wrap.py`** leaves a single word on the last line of some wrapped `<param>` / `<returns>`
  tags (`/// it.</param>`); readable, left as it is.
- **Measured on 2026-10-24:**
  - The counter on area 2's 22 files: on `main` 38 types and 213 methods to touch; on the
    branch 0 and 0. The whole code has 95 types and 451 methods left for areas 3-5.
  - `tokens`: all 121 code files identical to `main`'s code, 22 with changed comments.
  - `facts`: no number, ID or identifier of the old comments missing, except one judged:
    `SmoothAnalyzer.PositionMedian`, "din 8 in 8" became "one pixel in 8 of the position (one
    in 16 of the sensor)", one 8 instead of two.
  - `romanian`: no line left in the 22 files but two false hits: "care" (English) in
    `ExposureAnalyzer.AnalyzePreview` and the quoted output in `StructureReport.DepthDetail`.
  - `width`: no comment line over 100 columns; every added line ASCII.
  - `check-design.ps1 -Since main`: 14 notes cover the files; none describes a comment, and
    none repeats a corrected claim (ADR 0019 aside, above).
  - `/validate`: the 40 cases ran on the real masters (`FLATCHECK_TESTDATA_LARGE` =
    `E:\Photografie\Astro\Flats and Darks`, all 30 files present and matching the manifest);
    300 figures, every one identical to the recorded value and to area 1's run.
  - The gate: `== OK` at each commit, 951 + 331 tests.

### Area 3, the runs, the console and the session (2026-10-24, `claude/Document-the-runs-console-and-session`)

- **The worktree** was first made under `FlatCheck/.claude/worktrees/` by mistake, removed
  before any edit and made again where the plan says, under
  `E:\Photografie\Code\.claude\worktrees\`.
- **No documentation-file error**: the 35 files built clean under the gate at the first try.
  The documentation commits change no code; the two code changes below came after, at the
  owner's word, each in a commit of its own.
- **Nine comments corrected, in a commit of their own:**
  1. `FlatCheckCli.cs`, header: the dispatch order starts with `--lenses`, `--synthetic` and
     `--against` (the code, and `pipeline.md`, say so), not with `--smooth`.
  2. The same header, the exit codes: they named only `OK`/`SUSPECT`/`PROBLEMA` and `--histeq`;
     now the structure ladder (`PERFECT`/`BUN` 0, `ACCEPTABIL` 2, `REFA` 3), `NEDETERMINAT` 2,
     an input refused 1, the four modes without a verdict, and a pointer to `thresholds.md`.
  3. `AgainstReport.ShapeDetail` / `DepthDetail`: the quoted outputs had " - " where the code
     writes ": ", and 23.0 % for 0.30 stops, where 2^0.30 - 1 is 23.1 %.
  4. `SyntheticRun.Complete(req, f, sources, ...)`: "for the tests and the callers that have
     not identified yet" - nothing calls it, the tests included; it now says so.
  5. `LightSeriesRun.cs`, header: "of the five runs" was 2.0.0's count; `Runs/` holds ten.
  6. `VerdictPalette.cs`, header: "the only exception is NEDETERMINAT" - `IsInconclusive` makes
     `INAPLICABIL` neutral too (`Stats.Severity` gives it 3).
  7. `HistogramCanvas.cs`, header: "the one-dependency rule ... the project's second dependency"
     dates from 2026-09-10 (7158227), before ImageSharp; a charting package would be the third.
  8. `SessionMessages.Explain`, `NotAFlatMaster` and `SirilSession.IsFlatMasterName` cited
     "Cerinta 4" and "Cerinta 6", the window's first brief (0982260, 2026-09-09), not
     `requirements.md`: now R-080 with R-088, and R-082.
  9. `MainWindow.xaml.cs`, header: "only the two things that need a window" - it also opens the
     "Despre" and Siril run windows and asks before closing during a split.
- **Dead code, deleted at the owner's word** ("curata acel overload in plus", 2026-10-24): the
  overload `SyntheticRun.Complete(SyntheticRequest, LoadedFrame, ProfileSources, double?,
  double?, double?, bool)` had no caller, the tests included. The `Complete` that ADR 0030,
  `lens-profiles.md` and `LensIdentification.cs` name is the other overload, which stays.
- **A high-effort `/code-review`** of the branch found 8: one code defect and seven comment or
  plan findings; the owner said to fix all ("Fixeaza tot inclusiv problema de cod").
  - The defect: `SirilInstall.Locate` is documented "does not throw", but `FindConfig` caught
    only `IOException`, so a settings folder the user may not list threw an
    `UnauthorizedAccessException` out of `MainViewModel`'s and `SplitTabViewModel`'s locate
    and out of `SirilScriptRun.For`'s runner. Test first:
    `SirilInstallTests.AnUnlistableSettingsFolderGivesNoConfig_NotAnException` (a deny-list
    ACL on a temp folder) failed with that exception, then passed once `FindConfig(string)`
    caught it. The registry lookup got the same catch, and its `try` moved inside the loop, so
    an unreadable uninstall entry skips itself instead of the rest of its hive; that part has
    no test (the code reads the machine's real Uninstall key). No requirement changed: the
    fix restores the contract the code already stated.
  - The comments: `VerdictPalette` (INAPLICABIL also comes from the Expo tab's not-a-raw),
    `MainWindow` (the other dialogs live in their views or reach a view-model as a delegate),
    `SessionMessages` (the XAML holds the fixed labels), `FlatCheckCli` (the exit-code table
    now lists `--expo`, `--smooth --scale auto` and `--synthetic`; `ExitFor`'s OK/SUSPECT
    ladder is `--light`'s, not an old one), `InstallLocationsFromRegistry` (fixed in code, see
    above) and this section's token claim (below).
- **Measured on 2026-10-24:**
  - The counter on area 3's 35 files: on `main` 52 types and 266 methods to touch; on the
    branch 0 and 0. The whole code has 43 types and 185 methods left for areas 4-5.
  - `tokens`: after the documentation commits, all 121 code files identical to `main`'s code,
    35 with changed comments; after the two code commits, `SyntheticRun.cs` and
    `SirilInstall.cs` differ, by the deleted overload and by the fix.
  - `facts`: no number, ID or identifier of the old comments missing, except four judged:
    "ViewModel" became "view-model" in `MainWindow.xaml.cs`, `ObservableObject.cs`,
    `Converters.cs` and `HistogramCanvas.cs`.
  - `romanian`: no line left in the 35 files but two quoted outputs (`AgainstReport.ShapeDetail`,
    `SessionMessages.SyntheticCropNote`).
  - `width`: no comment line over 100 columns in the 35 files; every added line ASCII.
  - `check-design.ps1 -Since main`: 12 notes cover the files; none describes a comment, and
    none repeats a corrected claim.
  - `/validate`: not run, as assumption 7 says (no formula file in this area, and neither code
    change touches one).
  - The gate: `== OK` at each commit, 951 + 331 tests, 951 + 332 with the new test.

### Area 4, the view-models (2026-10-25, `claude/Document-the-view-models`)

- **No documentation-file error and no code change**: the 22 files built clean under the gate
  at the first try. Overrides of `TabViewModel.Reset` use `<inheritdoc/>`, as assumption 2
  allows for the tab view-model base.
- **Eleven comments corrected, in a commit of their own:**
  1. `TabViewModel.cs`, header: "the six tabs" - there are eight; Split keeps its own running
     state and uses only the result and error part; and Ratio, not only the histogram tab,
     overlaps runs on a quick re-choice.
  2. `MainViewModel.cs`, header and `ProblemTitle`: "six tabs", "four empty panels" - eight.
  3. `MainViewModel.RunChainAsync`: "the Ratio tab usually has two preselected masters" - it
     proposes nothing (R-082) and `ResetTabs` drops its files. (The correction first said "so
     the call finds none"; false when the user picks both files during the chain, as the
     review found - see the review section below.)
  4. `MapScaleViewModel.cs`, header: "used by two tabs, Structure and Ratio" - five: Structure,
     Ratio, Netezire, Sintetic, Split.
  5. `StructureRows.cs`, header: "for both tabs" - Netezire shows the five tests too, before
     and after.
  6. `RatioTabViewModel.cs`, header: "the only tab that does NOT run at start" - only Structure
     and End to end run at opening.
  7. `RatioTabViewModel.Tests`: "the token is the session's, not the run's, so NEITHER cancels
     the other" - each run has its own linked token since the header's fix.
  8. `StructureTabViewModel.cs`, header: the master is `pp_flat_stacked.fit` in `masters/` or
     `process/`, not `process/` only; the corner percentages are composed in `StructureRows`.
  9. `ExposureTabViewModel.Sky`: "Cerinta 8" was the window's first brief (0982260), not
     `requirements.md`; now R-043, the `--sky` bands.
  10. `SmoothTabViewModel.cs`, header: "the other tabs compute when the folder is opened" -
      only Structure and End to end do.
  11. `SyntheticTabViewModel.PrefillAsync`: "once the opening chain has ended, a raw from
      flats/" - the prefill reads the raw's metadata at once, without waiting.
- **Three design-note passages corrected with them**, the same claims: `two-applications.md`
  (the Histogram tab's views are bitmaps from the full-resolution raster, no PNG; Ratio
  overlaps runs too), `lens-profiles.md` (the prefill does not wait for the chain) and
  `code-map.md` (`MapScaleViewModel` is on five tabs).
- **Found and reported** (code, or a requirement; the owner's call, fixed below):
  - `StructureTabViewModel.Pct` has no caller.
  - `MainViewModel.RunChainAsync`'s `Ratio.RecomputeAsync()` seemed to find no files at that
    point (item 3); the review showed it does when the user picks during the chain.
  - R-084 says the Expo tab lists "the raws and FITS of `flats/`"; `SirilSession.RawFlats`
    lists raws only, and its comment and the tab's say why (exact white and black).
  - The window words and computes a few things the rule "Computed only in the core" puts in
    the core: `ExposureTabViewModel.AdviceText` (the stops, and "Sub 60 %", "Sub ~30 %" as
    literals for `Th.ExpoSkyTooHigh` and `Th.ExpoTooLow`), its `Caption` ("98 % din white"),
    `DescribeChannels`' percentages, `LightTabViewModel`'s refusal text and `DescribeReasons`,
    and `StructureRows`' corner percentages.
- **Measured on 2026-10-25:**
  - The counter on area 4's 22 files: on `main` 25 types and 144 methods to touch; on the
    branch 0 and 0. The whole code has 18 types and 41 methods left, all in `Views/` (area 5).
  - `tokens`: all 22 files identical to `main`'s code, 22 with changed comments.
  - `facts`: no number, ID or identifier of the old comments missing, except two judged:
    "ViewModel" became "view-model" in five files, and the "8" of "Cerinta 8" (item 9).
  - `romanian`: no line left but two quoted outputs (`SirilRunViewModel.CommandCount`,
    `SmoothTabViewModel.AdviceRows`).
  - `width`: no comment line over 100 columns; every added line ASCII.
  - `check-design.ps1 -Since main`: four notes cover the files (`code-map.md`,
    `two-applications.md`, `lens-profiles.md`, `modes-synthetic.md`); three corrected above,
    `modes-synthetic.md` holds.
  - `/validate`: not run, as assumption 7 says (no formula file in this area).
  - The gate: `== OK` at each commit, 951 + 332 tests.
- **The four findings fixed at the owner's word** ("Fix all", 2026-10-25), a code change
  beyond the plan's "comments only", in a commit of its own:
  - `StructureTabViewModel.Pct` deleted; the chain's `Ratio.RecomputeAsync()` removed
    (`RatioWaitsForTwoExplicitChoices` already pins that Ratio stays empty on opening).
  - R-084 reworded to the behaviour `TheListComesFromFlatsAndHoldsOnlyCameraRaws` pins: camera
    raws only; the requirement had said "raws and FITS" since the kit adoption (7fb607c).
  - The window's wording and figures moved to `Report/` unchanged: `ExposureReport`
    (`VerdictWord`, `Advice`, `ChannelsSummary`, `ChannelOrderNote`, `HistogramCaption`,
    `PreviewChannelNote`, `ScreenGap`), `LightReport` (`FieldText`, `FieldWarning`,
    `TooWideText`), `LightSeriesReport` (`CorrelationValue`, `ReasonsText`), `StructureReport`
    (`CornerShares`, `VignetteNote`, `CentroidNote`). The same literals sat in the console's
    text too (`Sub 60%`, `Sub ~30%`, `>=98% white`) and in `ExposureAnalyzer` (`0.98 * white`,
    four times): both now read `Th.ExpoSkyTooHigh`, `Th.ExpoTooLow` and the new
    `Th.ExpoClipWhite` = 0.98, whose row is in `thresholds.md`. Three tests pin the moved
    texts to `Th` and to the console's ratios.
  - Measured: every console golden, the 43 parity goldens included, unchanged; `/validate`,
    40 cases on the real masters, 300 figures, all 300 identical to the recorded value; the
    gate `== OK`, 954 + 332 tests.
- **A high-effort `/code-review`** of the branch found 9; the owner said to fix all nine
  ("Fix all nine review findings, including giving the Ratio tab the waitIdle seam"):
  1. The removed chain call was the only thing that recomputed a Ratio pick made during the
     chain after it; the pick itself ran beside Structure and the series. Test first:
     `R082_APickDuringTheOpeningChainWaitsForIt` failed (the run completed with the chain still
     open), then passed once `RatioTabViewModel.Attach` took `waitIdle` and `RecomputeAsync`
     awaited it, as Netezire, Sintetic and Split do. R-082 says so.
  2. The claim "never had files at that point" corrected in the CHANGELOG, this plan and
     `MainViewModel`'s comments.
  3. `ExposureReport.VerdictLines` is the one verdict tree: the console prints its head after
     "VERDICT:" and its lines indented, the window the head and the lines as one paragraph.
     The window's advice now reads in the console's words; the not-a-raw advice stays the
     window's own (its way out is the application, not `flatcheck flat.NEF --expo`).
  4. `LightReport.MarginalLines` / `DegeneracyLines` and `LightSeriesReport.ReasonLines` /
     `SameFlatLines` hold the lines both applications print; the window joins them. The
     console's "nu pot verifica ... vezi --fov" and the window's unknown-field note stay
     separate, being different advice.
  5. `StructureReport.CornerSharePct` / `CornerOverCentrePct` compute the two ratios once.
  6. `TabViewModel`'s header: Split uses the progress properties too.
  7. The headers of `LightReport.cs`, `LightSeriesReport.cs` and `StructureReport.cs` name
     their window pieces.
  8. `Th.ExpoClipWhite`'s comment moved above it, within 100 columns.
  9. Core tests for the moved functions: the window's advice equals the console's verdict
     block on six levels, the channel lines, the order note, the gap, the preview note, the
     reasons and the correlation, the centroid note.
  - Measured: the console goldens and the 43 parity goldens unchanged; `/validate`, 40
    cases, 300 figures, all 300 identical to the recorded value; the gate `== OK`, 964 + 333
    tests.

## Glossary: one English word per Romanian term, in every area

| Romanian | English | Where, and the identifiers that fix it |
|---|---|---|
| flat, master | flat, master | the input; `pp_flat_stacked.fit` |
| vignetare; adancime | vignetting; depth | `VignettingModel`, the depth verdict in stops |
| inel; sector; arc | ring; sector; arc | `RadialModel`, `RingFit`, `RingSectors` |
| dala, dale | tile | `TileMedians`, `TileSize`, `MaxTileDeviation` |
| pata, pete | spot | `LocalSpots`, `LocalSpotWarn` |
| colt; centru; varf | corner; centre; vertex | `CornerStats`, `FitCenter` |
| inclinare; reziduu; harta | tilt; residual; map | `tiltPct`, `ResidualVerdict`, the residual map |
| sa (a cerului) | saddle | the even 2t term |
| difuzor; textura; netezire | diffuser; texture; smoothing | `FineTexture`, `SmoothAnalyzer` |
| rotire la 180 | 180-degree rotation | `Rotate180`, `--ratio` |
| prag; banda; scara | threshold; band; scale | `Th`, the residual scale, `MapRange` |
| cadru; raster de analiza | frame; analysis raster | `Img.Prepare` |
| previzualizare; miniatura | preview; thumbnail | `EmbeddedPreview`, `PreviewReader` |
| focala; diafragma; obiectiv; montura; aparat | focal length; aperture; lens; mount; camera | `LensProfile`, `LensChoice` |
| crop de calibrare | calibration crop | `LensChoice.Calibration` |

- Quoted output and every string literal stay Romanian.
- British spelling as the docs use it (centre, colour, behaviour, normalise, symmetrise);
  "analyzer" as the types write it.

## The prompt for area 2

To paste into a session opened in `E:\Photografie\Code\FlatCheck`, once area 1 is merged:
"Area 2 of the documentation backfill, `Analysis/` and `Report/`: follow
`docs/plans/2026-10-24-document-every-type-and-method-and-translate-the-comments.md` on `main`,
'Each area's session', on branch `claude/Document-the-analyzers-and-reports`. The plan is
approved. The scratch tool's source is in
`C:\Users\User\AppData\Local\Temp\claude\E--Photografie-Code-FlatCheck\0e1af765-b386-4d2c-8f34-b2966ed6f718\scratchpad\doccount`
(and `wrap.py` beside it): copy them into yours and build the tool."

## The prompt for area 3

To paste into a session opened in `E:\Photografie\Code\FlatCheck`, once area 2 is merged:
"Area 3 of the documentation backfill, `Runs/`, `FlatCheck/` and the Wpf `Session/`,
`Interop/`, `Mvvm/`, `Presentation/`, `App` and `MainWindow`: follow
`docs/plans/2026-10-24-document-every-type-and-method-and-translate-the-comments.md` on `main`,
'Each area's session', on branch `claude/Document-the-runs-console-and-session`. The plan is
approved. The scratch tool's source is in
`C:\Users\User\AppData\Local\Temp\claude\E--Photografie-Code-FlatCheck\374a5ba5-2bc6-4317-9b11-7357b78b4fb2\scratchpad\doccount`
(and `wrap.py` beside it): copy them into yours and build the tool."

## The prompt for area 4

To paste into a session opened in `E:\Photografie\Code\FlatCheck`, once area 3 is merged:
"Area 4 of the documentation backfill, the Wpf `ViewModels/`: follow
`docs/plans/2026-10-24-document-every-type-and-method-and-translate-the-comments.md` on `main`,
'Each area's session', on branch `claude/Document-the-view-models`. The plan is approved. The
scratch tool's source is in
`C:\Users\User\AppData\Local\Temp\claude\E--Photografie-Code-FlatCheck\d30c94e5-1cf8-4aa9-a885-57d7c43d0ce9\scratchpad\doccount`
(and `wrap.py` and `chk.sh` beside it): copy them into yours and build the tool."

## The prompt for area 5

To paste into a session opened in `E:\Photografie\Code\FlatCheck`, once area 4 is merged:
"Area 5 of the documentation backfill, the Wpf `Views/`, every XAML comment, the tests'
comments and the csproj comments, and the rule's exception closed: follow
`docs/plans/2026-10-24-document-every-type-and-method-and-translate-the-comments.md` on `main`,
'Each area's session', on branch `claude/Document-the-views-and-translate-tests`. The plan is
approved. The scratch tool's source is in
`C:\Users\User\AppData\Local\Temp\claude\E--Photografie-Code-FlatCheck\2af746cc-6be7-4e2a-a1a6-2b71e33b1f07\scratchpad\doccount`
(and `wrap.py` and `chk.sh` beside it; `chk.sh` names this session's scratchpad and worktree in
its first lines): copy them into yours and build the tool."
