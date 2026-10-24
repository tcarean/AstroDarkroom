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
