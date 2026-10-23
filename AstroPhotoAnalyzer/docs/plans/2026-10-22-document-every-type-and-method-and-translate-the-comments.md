# Document every type and method, and translate the code's comments to English

## Context

On 2026-10-21 the kit's rule "Documentation comments" (the kit's ADR 0035) started asking for
an XML documentation comment on every type and method in the code: a `<summary>`, a `<param>`
for each parameter, a `<typeparam>` for each type parameter, a `<returns>` for a value. Code
written before the rule gets its comments in a task of its own. In AstroPhotoAnalyzer the rule
"Languages" adds that a C# file's Romanian comments are translated when the file gets its
documentation comments (ADR 0005's Status: Romanian comments in the C# files superseded by the
kit's ADR 0035). The owner asked: "Backfill the XML documentation comments in
AstroPhotoAnalyzer's code folders ... about 126 types and 393 methods to touch. Plan first:
measure, split by area, one area per session with verify after each. Translate each C# file's
Romanian comments to English in the same pass ... Propose whether to turn on
GenerateDocumentationFile with CS1591 muted."

The outcome: every type and method in `AstroPhotoAnalyzer.Core/`, `AstroPhotoAnalyzer/` and
`AstroPhotoAnalyzer.Wpf/` documented, every comment in those C# files in English, no code
changed, the gate green after each area. MindfulnessReminder's and ImageVisualCompare's backfills
(their `docs/plans/2026-10-21-document-every-type-and-method.md`) are the pattern; this plan says
where APA differs: five times the size, a translation on top, and five sessions instead of one.

## Facts checked (2026-10-22, `main` at 2f96fc4)

`main` moved to 3326fe4 (ImageVisualCompare's backfill) before area 1's branch was cut from it;
nothing under `AstroPhotoAnalyzer/` changed, so every figure below holds at the branch's base.

- **The gaps,** measured with the kit task's scratch counter (`doccount`, a Roslyn syntax walk
  over the folders `kit.json` lists, skipping `bin/`, `obj/`, `*.g.cs`), ImageVisualCompare's
  build. In the code folders, 71 files and 11,109 lines:
  - types: 158, of which 121 have no comment and 5 lack the `<param>` tags of a positional
    record (`ThirdPartyComponent`, `DpiLadderRow`, `LstarLadderRow`, `CurvePoint`,
    `DarkNebula`);
  - methods: 396, of which 233 have no comment and 160 lack tags (155 a `<param>`, 138 the
    `<returns>`); 287 return something, 3 of them a plain `Task` (`CurvesTab.ComputeAsync`,
    `MainViewModel.OpenAsync`, `RunAsync`); 30 constructors; 14 overrides;
  - **126 types and 393 methods to touch**, the owner's figures;
  - 5 generic methods need a `<typeparam>` the counter does not check: `DustAnalyzer.PatchLstars`,
    `JpegAnalyzer.TiffBytes`, `RasterOps.LstarPlane`, `ObservableObject.Set`,
    `CurveCanvas.Register`;
  - properties, indexers and events: 520, of which 328 have no comment (not required).
- **The comments are Romanian, all of them.** Each of the 71 files carries Romanian comments:
  1,906 non-blank comment lines after the licence header (1,522 with a Romanian function word,
  1 English; the rest are short continuations). They are file-header paragraphs on why the code
  is the way it is, doc comments (515 `<summary>`, 1 `<param>`, 46 `<paramref>`, 1 `<see cref>`;
  no `<returns>`, `<inheritdoc/>`, `<exception>`, `<remarks>` or `<c>`), `//` comments, and
  trailing comments on code lines (`const int Half = 12;   // fereastra 25 = 2 * 12 + 1`). They
  cite ADRs and requirements 141 times. 215 comment lines run past 100 columns, up to 256.
- **Out of scope by the request's words:** the tests (54 files, 35 of them with 246 Romanian
  comment lines next to English ones; the rule exempts tests from documentation comments), the
  XAML comments (16 files, about 60 comments; ADR 0005 keeps them Romanian, its supersession
  names the C# files only), `Icon/make_icon.py`, and the csproj and `Directory.Build.props`
  comments (ADR 0005).
- **No test reads a code comment.** `LicenseHeaderTests` reads the GPL header (unchanged),
  `RepoInvariantTests` the test names, `VersionTests` and `ThirdPartyTests` the props and the
  core's csproj.
- **What the compiler would say with the documentation file on,** pre-checked by a scan of the
  670 doc blocks in the five projects (the compiler's own count comes in area 1):
  - 3 blocks are malformed XML (CS1570): `Report/Recommendation.cs:28` (`Ok < Atentie <
    Problema`), `Report/ReportWriter.cs:160` (`"<= 0.05"`) and
    `AstroPhotoAnalyzer.Tests/PrintStretchSimulatorTests.cs:106` (`<= 0.05`);
  - no `<param>` or `<paramref>` names a missing parameter (CS1572, CS1734), no member tags only
    some parameters (CS1573), no comment sits on something that is not a member (CS1587);
  - the 9 `cref`s all resolve by reading (CS1574, CS0419).
- **The documentation file is off today.** `Directory.Build.props` (Romanian comments) sets no
  `GenerateDocumentationFile`; the gate builds with `-warnaserror` and runs
  `dotnet format --verify-no-changes`; `.editorconfig` holds whitespace and IDE0005;
  `EnforceCodeStyleInBuild` is off (ADR 0003). The kit, DarksCheck, LightsCheck,
  ImageVisualCompare and MindfulnessReminder generate the file with CS1591 muted;
  MindfulnessReminder also sets `PublishDocumentationFiles` false. The SDK (9.0.319) publishes
  each project's XML file and its references' unless that property is false, so the release's
  `console/` folder would hold two `.xml` files beside the single-file exe.
- **Contracts to inherit from:** the project's interfaces `IFrameReader` (`FitsReader`,
  `ImageSharpReader`) and `ISrgbLinearizer` (`ExactLinearizer`, `LiftTable`); its base classes
  `ResultTabViewModel` (seven tabs) and `ObservableObject`. Framework contracts: six
  `IValueConverter`s (`Presentation/Converters.cs`, parameters named `value`, `t`, `p`, `c`),
  `RelayCommand : ICommand`, WPF overrides. The core is `internal` throughout; the window's types
  are public.
- **Six view classes sit on one line each** with their constructor
  (`Views/ResultViews.xaml.cs:27-32`); their constructors can get a comment only if the lines
  are split.
- **A parked branch overlaps:** `claude/Recommend-the-lift-that-puts-the-dust-on-paper-black`
  (5708045, cut from 9dd0b79) changes 57 files, `Options.cs`, `AnalyzerCli.cs`, `MainWindow.xaml.cs`
  and five view-model files among them. Every file this task translates conflicts with it.
- **`/validate`:** 14 cases, about 2.5 min, on images the worktree must hold a copy of
  (`docs/skills.md`); `/wrap-up` step 2 calls `Input/`, `Analysis/`, `Report/`, `Parameters.cs`
  and `Thresholds.cs` numeric code.
- **Branches:** no branch or worktree for this task.

## The documentation file: proposed, and the owner's answer (2026-10-22)

Put to the owner with four options; the owner chose the recommended one, "Yes, first, all
projects". Set aside: after the last area (the areas would go unchecked while written), the code
projects only (a second rule beside the kit's props; the test comments never checked), and
leaving it off (the counter sees a missing tag, never a wrong one).

- **What it buys:** with the file generated and the gate's `-warnaserror`, the compiler fails a
  malformed comment (CS1570, the likeliest slip of a translation: a bare `<` or `&`), a `<param>`
  or `<paramref>` naming no parameter (CS1572, CS1734), a parameter left untagged beside tagged
  ones (CS1573), a comment on something that is not a member (CS1587), and a `cref` that does not
  resolve or is ambiguous (CS1574, CS0419). The counter sees a tag missing; the compiler sees a
  tag wrong.
- **The change,** the first code commit of area 1, before any comment is written:
  - `Directory.Build.props`, for all five projects: `GenerateDocumentationFile` true,
    `NoWarn` CS1591, `PublishDocumentationFiles` false, with a comment in Romanian (ADR 0005
    keeps that file's comments Romanian): CS1591 would ask for public members only, properties
    included, and the core is internal; nothing reads the XML at run time, so no publish ships
    it.
  - The three malformed comments escaped (`&lt;`) and nothing else: the two in `Report/` stay
    Romanian until area 3 translates them; the test's is a comment, no assertion changes.
  - ADR 0038, "The build writes the XML documentation file so the compiler checks the comments;
    CS1591 stays muted and the files are not published": a value of this project's (the kit's
    ADR 0035 describes the checks wherever the file is generated; APA did not generate it).
  - `CLAUDE.md`, "Style": one sentence naming the checks and ADR 0038.
  - Its `Internal` line in `CHANGELOG.md`.
  - `dependencies.md` covers `Directory.Build.props` for its NuGet audit setting; it is read
    against the change and is expected to stay as it is.
- **Checked in that commit:** the gate, whose build is now the compiler's own count; a
  `dotnet publish` of both `kit.json` entries into the scratchpad, listing no `.xml`; the gate's
  time against `main`'s. **If anything beyond the three escapes fires, the session stops and
  reports what fired** (a format finding included).

## The areas: one per session, in this order

| # | Area | Files | Lines | Types | Methods | Comment lines | Notes to read |
|---|---|---|---|---|---|---|---|
| 1 | `Core/Input/` | 15 | 2,112 | 16 | 99 | 402 | `input.md`, `dependencies.md` |
| 2 | `Core/Analysis/` | 16 | 3,222 | 41 | 83 | 573 | `measurements.md`, `dependencies.md` |
| 3 | `Core/Report/`, `Core/Runs/`, `ThirdParty.cs`, `Version.cs` | 10 | 1,517 | 15 | 76 | 209 | `recommendations-and-report.md`, `pipeline.md`, `dependencies.md` |
| 4 | `Thresholds.cs`, `Parameters.cs`, `AstroPhotoAnalyzer/` | 5 | 1,139 | 6 | 15 | 438 | `thresholds.md`, `console.md` |
| 5 | `AstroPhotoAnalyzer.Wpf/` | 25 | 3,119 | 48 | 120 | 284 | `window.md`, `console.md` |
| | **Total** | **71** | **11,109** | **126** | **393** | **1,906** | |

- **Why this order:** the layers of `CLAUDE.md`'s "Layout", bottom up. Input holds the two
  interfaces and the types everything else names (`Raster`, `Stats`, `ColorMath`), so later
  comments can point at settled contracts. Each area is the Covers of one or two design notes,
  read before writing, plus the ADRs `CLAUDE.md` lists under "Read before touching" for its files.
  `code-map.md` covers every code file and `known-limitations.md` ten files of areas 1 and 2, so
  `check-design.ps1` lists them too; both are read against each area's change.
- **Size:** areas 2 and 5 are the largest (about 120-170 members and 300-600 lines each); 3
  and 4 are lighter, 4 mostly translation (`Thresholds.cs` alone is 332 comment lines).
- **Per file** each session lists its gaps with the counter at its start; the per-member list
  is not repeated here.

## Assumptions (the kit's rule "Two readings": decided, stated)

1. **Scope:** the 126 types, 393 methods and 5 generic methods above, and every comment in the
   71 C# files of the code folders, property and field comments included (translated; none
   added, as the rule allows).
2. **Tags:**
   - `<summary>` always; a `<param>` for each parameter, a positional record's on the type; a
     `<typeparam>` for each type parameter; a `<returns>` for each value, none for the three
     plain `Task`s.
   - No new `<exception>` or `<remarks>`: none exists. A throw a caller must plan for, other than
     an argument guard, is said in the summary; an argument guard is said as the parameter's
     range.
3. **`<inheritdoc/>` only where a contract of this project states it all,** once its own
   comment is complete: the `IFrameReader` and `ISrgbLinearizer` implementations and the
   `ResultTabViewModel` overrides, each checked. Own text for every framework override or
   implementation (the converters, `RelayCommand`, WPF's `On*`, `App.OnStartup`), every
   constructor, and an implementation that adds to its contract (`LiftTable.Linear`'s table).
4. **What a comment says:** the contract, in the voice of the file.
   - **Every number names its unit or scale,** where a reader of a signature needs it most:
     a level 0..1 (display-referred) or 0..65535, L*, Dmax, pixels of the image or of the
     screen, cm, dpi, degrees, a fraction or a percent; and what NaN or null means where the
     code returns or accepts it on purpose (`Nullable` is off in the code projects).
   - A threshold is named, not quoted: `<see cref="Thresholds.StarMinCount"/>`, so a comment
     cannot drift from `Thresholds.cs`.
   - English; `null`, `true`, `false` as plain words or `<c>`; ADR and requirement IDs kept
     where the comment has them and allowed in new ones, as the code already does; Romanian
     output quoted as the user sees it (`PROBLEMA`, "Nivelul fundalului"; ADR 0005,
     Consequences); an overloaded name in a `cref` gets its parameter list (CS0419).
   - Every new or translated comment line fits in 100 columns; a long one-line summary takes
     the block form.
5. **Event handlers** tag `sender` and `e`: what raises it, and whether it is read.
6. **A translation keeps the meaning, not the words:** every number, identifier, ID, quote and
   CAPITALISED emphasis stays; paragraphs stay where they are, file headers included. A
   sentence that already says what a parameter means or what comes back moves into its tag; a
   summary that says only why gets a first sentence saying what. Moving "how" paragraphs into
   the design notes, or trimming them, is not this task.
7. **A comment found false while translating** is translated as it stands in the area's main
   commit, then corrected in a commit of its own, each with its evidence (the code line, the
   ADR or the test), and listed at wrap-up. Code that seems wrong is reported, never changed.
8. **Nothing else changes:** no requirement, no test beyond the one escaped comment, no README,
   no golden file, no version bump. `CHANGELOG.md` gets two `Internal` lines in area 1, the
   documentation file and the backfill; each later area updates the second to name what is done.
9. **One branch per area** (the owner's answer, 2026-10-22), cut from `main` after the previous
   area is merged: each session is a task (the kit's rules "One task per session" and
   "Branches"), each review is one area, and area N+1 starts from area N's "What deviated".
   Set aside: one branch for all five, one merge of about 3,000 changed lines.
10. **`/validate` runs in areas 1 to 4,** as `/wrap-up` step 2 asks for those folders, though the
    token proof below already shows no formula moved; area 5 touches no numeric code.

## Each area's session

- **Opened in `E:\Photografie\Code\AstroPhotoAnalyzer`,** from a prompt I give at each wrap-up
  (a chip would open at the checkout root). Area 1 runs in this session.
- **Setup:** `git worktree add .claude/worktrees/<Task-name> -b claude/<Task-name> main`;
  `<wt>` = `...\.claude\worktrees\<Task-name>\AstroPhotoAnalyzer`. Edits on absolute paths there;
  commits `git -C <wt> commit -q -m "..."`, expecting the hook's `== OK`. Branch names:
  1. `claude/Document-and-translate-the-input-code`
  2. `claude/Document-and-translate-the-analyzers`
  3. `claude/Document-and-translate-the-report-and-the-runs`
  4. `claude/Document-and-translate-the-thresholds-parameters-and-console`
  5. `claude/Document-and-translate-the-window`
- **The scratch tool,** `doccount`, built from ImageVisualCompare's copy in this session's
  scratchpad
  (`C:\Users\User\AppData\Local\Temp\claude\E--Photografie-Code-AstroPhotoAnalyzer\12a227d5-dffd-4e69-8cd2-8ea6561095f8\scratchpad\doccount`)
  and copied into each later session's: the counter extended with `<typeparam>` on methods and
  plain `Task` exempt from `<returns>`; `romanian`, which lists the comment lines that still hold
  a Romanian function word outside quotes and file names; `tokens`, which compares each changed
  file's token stream with `main`'s (an export of `main` into the scratchpad), comments and
  whitespace excluded, directives and disabled code included; and `width`, the comment lines
  over 100 columns.
- **Steps:** count the area; read its notes, ADRs and `known-limitations.md`; per file, in
  dependency order (interfaces and the types others name first): translate every comment and
  complete every type and method; then the checks below; commit; correct the false comments in
  a commit of their own; `/validate` (areas 1 to 4, the images copied into the worktree's
  `testdata/large/` first); `/wrap-up` (the CHANGELOG line, this plan's "What deviated" for the
  area); propose the merge and suggest `/code-review main...<branch>`.
- **If the gate goes red for a reason that predates this task, the session stops and reports.**
  So does a session whose context runs short: it stops after a commit, and the next session
  continues on the same branch from this plan.

## Commits

Each message one line, under 100 characters, saying why.

- **Area 1,** in this session, `claude/Document-and-translate-the-input-code`:
  1. this plan, saved as `<wt>/docs/plans/2026-10-22-document-every-type-and-method-and-translate-the-comments.md`;
  2. the documentation file (above);
  3. the contracts the rest name: `IFrameReader.cs`, `LiftTable.cs` (`ISrgbLinearizer`),
     `Raster.cs`, `Stats.cs`, `ColorMath.cs`, `RasterOps.cs`: 6 files, 5 types, 43 methods, 189
     comment lines;
  4. the readers, the writer and the sky: `FitsReader.cs`, `ImageSharpReader.cs`,
     `ImageLoader.cs`, `FitsWriter.cs`, `IccPaperBlack.cs`, `IccProfileInfo.cs`, `Wcs.cs`,
     `Astrometry.cs`, `DarkNebulaCatalogue.cs`: 9 files, 11 types, 56 methods, 213 lines;
  5. the false comments found, if any;
  6. wrap-up: the CHANGELOG lines and "What deviated".
- **Areas 2 to 5:** the area's files in one or two commits, the false comments, the wrap-up.
- **The prompt for the next area,** given at each wrap-up, to paste into a session opened in
  `E:\Photografie\Code\AstroPhotoAnalyzer`: "Area N of the documentation backfill, <area>:
  follow `docs/plans/2026-10-22-document-every-type-and-method-and-translate-the-comments.md`
  on `main`, 'Each area's session', on branch `claude/<name>`. The plan is approved. The scratch
  tool's source is in `<this session's scratchpad>\doccount`: copy it into yours and build it."

## Verification, in every area

- **The gate** green at each commit (the hook's `== OK`, the test count as on `main`).
- **The counter** on the area's files: 0 types and 0 methods undocumented or incomplete.
- **No code changed:** `tokens` finds every changed `.cs` file token-identical to `main`, string
  literals (the Romanian UI text) included.
- **Every comment in English:** `romanian` finds no line in the area, or only names and quoted
  output, each listed.
- **No comment line over 100 columns** in the area's files.
- **`scripts/check-design.ps1 -Since main`:** each note listed is read against the change; none
  should change.
- **`/validate`** (areas 1 to 4): no drift, the table pasted.
- **The whole diff read back** before each commit: each Romanian line against its translation.

## Then

- After area 5: the counter on the whole code shows 0 and 0, and `romanian` shows no line in
  the 71 files; `documentation-comments-rule.md` in memory marks APA done.
- **Left for later, not this task:** the tests' Romanian comments, the XAML comments (ADR 0005),
  and the parked branch, which is the owner's call.

## Folded in after approval: the planning review

A planning agent read the draft against the code while it was put to the owner; its report came
after the approval. What it changes, for every area:

- **Facts it added:** no file in the five projects has a preprocessor directive; the only
  caller-info attribute in the code is `[CallerMemberName]` (`ObservableObject.cs`); there are no
  operators, explicit interface implementations, delegates, indexers or static constructors;
  one `/* */` comment exists (`FitsWriter.cs:68`); the code has at least nine local functions
  (`CurveAdvisor`, `TonalAnalyzer`, `PrintSizeAnalyzer`, `PrintStretchSimulator`, `CurveCanvas`,
  `MapViewModel`, `ResultTabs`), which the rule exempts and the counter does not count.
- **Rules it added:**
  - A local function keeps `//`: `///` on one is CS1587.
  - A `cref` is written fully qualified rather than by adding a `using`, which would change
    the code the token proof compares.
  - A doc block goes above a member's attributes (`[DllImport]` in `DarkTitleBar.cs`).
  - `ImageSharpReader` has a nested `FileFacts` class, which captures a `cref` meant for
    `Analysis/FileFacts.cs` from inside that file.
- **Checks it added,** run with the others in every area:
  - `facts`, a mode of the scratch tool: every number, ADR and requirement ID and identifier of
    a file's old comments is found again in its new ones, the misses listed and judged (a
    Romanian `20.000` is an English `20,000`);
  - every added line is ASCII, as all 71 files are today;
  - the branch changes only the area's `.cs` files, the plan and `CHANGELOG.md` (area 1 also
    the documentation file's set);
  - an added directive would show in `tokens`, which compares directives too.
- **The parked branch** is to be rewritten on `main`, never merged or cherry-picked (its plan,
  `docs/plans/2026-10-02-detect-dust-and-sky-from-a-plate-solved-fits-and-a-dark-nebula-catalogue.md`),
  so it constrains nothing here.
- **Set aside:** doing area 4 first, in the planning session, as the lightest; the approved
  order was already under way, and the glossary below gives the later areas the vocabulary
  `Thresholds.cs` would have.
- **For area 5:** once the code is English, the rule "Languages" in `CLAUDE.md` still describes
  files "not yet translated"; what it says then about the tests' Romanian comments (29 of 54
  files) is the owner's call, put at area 5's wrap-up.

## Glossary: one English word per Romanian term, in every area

| Romanian | English | Where, and the identifiers that fix it |
|---|---|---|
| fundal | background | its level (`BackgroundMedian`, "Nivelul fundalului") and its colour (`BackgroundResult`, "Culoarea fundalului"); ADR 0013 |
| cer | sky | the class and the modelled sky (`SkyModel*`); ADR 0025, ADR 0033; never "background" |
| praf | dust | `Dust*` |
| nor, nori; nori luminosi | cloud, clouds; bright clouds | `DustCloud*`; the classes of ADR 0026 |
| banda | band | the Milky Way band, `BandGalacticLatitudeDeg` |
| varfuri | peaks | the fifth class |
| dala, dale | patch | the dust, sky, class and map code (`DustPatchPx`, `PatchClass`, `PatchLstars`) |
| dala, dale | tile | the noise, plateau and quantization code (`NoiseTile`, `PlateauTiles`, `PctTilesBelowBanding`) |
| negrul hartiei; hartie; mediu | paper black; paper; medium | `PaperBlackLstar`, `--medium` |
| prag; tinta; tinta exacta | threshold; target; exact target | `Thresholds`, R-043 |
| scara (the simulation's); treapta | ladder; step | the lifts of ADR 0025 and ADR 0035 |
| ridica, ridicare; liniarizare | lift; linearisation | `LiftTable`, ADR 0035 |
| esantion; subesantion; nivel; plan; rand; cadru | sample; subsample; level; plane; row; frame | `Raster`, `RasterOps` |
| stea; colt; centru | star; corner; centre | `StarAnalyzer`, `CornerToCenterRatio` |
| raport; recomandare; harta; simulare | report; recommendation; map; simulation | `Report/`, `MapViewModel` |

- Quoted output and every string literal stay Romanian.
- British spelling where the docs use it (colour, centre, normalise, linearise); "analyzer" and
  "quantization" as the types and the docs write them.

## What deviated

Each area adds its own section at its wrap-up.

### Area 1, `Core/Input/` (2026-10-22, `claude/Document-and-translate-the-input-code`)

- **It ran in the planning session, as planned,** in seven commits instead of six: the planning
  review, which came after the approval, got a commit of its own.
- **The extended counter counts 392 methods, not 393.** The kit task's counter flagged the plain
  `Task` methods as missing a `<returns>` and checked no `<typeparam>` on a method; with both
  fixed, the code holds 126 types and 392 methods to touch. Area 1's 16 and 99 are unchanged;
  area 5's row is the one that drops, to 119 (`MainViewModel.RunAsync`, a plain `Task`).
- **The width rule:** at first it was held for comment-only lines, leaving `FitsWriter.Save`'s
  161-column `catch` with its `/* */` comment. The review showed that moving the comment changes
  no token, so it now sits on its own `//` line and every comment line fits in 100 columns.
- **The documentation file, on as proposed:** the gate's build found the three malformed comments
  and nothing else. A broken summary (CS1570) and a `<param>` naming no parameter (CS1572) were
  each put into `Recommendation.cs` and failed the core's build under `-warnaserror`, then taken
  out. All five projects write their XML file; a publish of both `kit.json` entries ships none
  (`console/`: the exe and two `.pdb`; `wpf/`: 247 files). The two failing builds ran on the core
  only; the review's suggestion to repeat them on the window and a test project was not taken,
  since the five XML files show the setting reaches every project and the checks are the
  compiler's.
- **`<inheritdoc/>` on three members:** `ExactLinearizer.Linear`, `FitsReader.Handles` and
  `ImageSharpReader.Handles`. The two `Load` methods (each adds its format's specifics) and
  `LiftTable.Linear` (its table) have their own text, as Assumption 3 says.
- **Eight comments corrected, in a commit of their own, each against its line:**
  1. `IFrameReader.cs`, header: `ImageLoader` names both readers in its list
     (`ImageLoader.cs:37`); it does not "know none of them by name".
  2. `Raster.cs`, header: the reader attaches its `SourceInfo` as `Raster.Source`, which four
     analyzers read, and some analyzers take `Parameters` too; not "a Raster and nothing else".
  3. `ImageLoader.cs`, header: the same fact, said of "where the pixels came from".
  4. `SourceInfo.BitDepth`: 64 on a FITS of doubles, 1, 2 or 4 on a PNG, the TIFF's own on a
     TIFF; not "8, 16 or 32".
  5. `RasterOps.cs`, header: `Subsample` at a stride of 1 or less returns its input.
  6. `FitsReader.ReadData`: signed integers stored without BZERO can be clipped too.
  7. `IccPaperBlack.Printable`: it keeps digits and punctuation; not "drops the bytes that are
     not letters". (The first correction said it drops non-ASCII bytes, which is false too: the
     review found they show as '?'; corrected again, see "Code review".)
  8. `Astrometry.cs`, header: it also gives the galactic latitude of the band (ADR 0026).

  At wrap-up `input.md` (facts 5 and 8) and `code-map.md` (fact 8) were brought in line.
- **The Romanian scan leaves three lines, judged:** "et al." in `Astrometry.cs` (Latin) and the
  quoted output of `IccPaperBlack.Describe` (two lines).
- **`Report/`'s two escaped comments** (`Verdict`, `ReportWriter.TargetText`) changed one
  character each and stay Romanian until area 3, as planned; `TargetText` gets its tags then.
- **Measured on 2026-10-22:**

  | Commit | What | Gate | Tests |
  |---|---|---|---|
  | 20e16e6 | the plan | `== OK in 43s` | 351 + 54 |
  | 7d187c9 | the documentation file (ADR 0038) | `== OK in 53s` | 351 + 54 |
  | 56fd088 | the contracts: 6 files | `== OK in 43s` | 351 + 54 |
  | 961d6b6 | the planning review, folded into this plan | `== OK in 43s` | 351 + 54 |
  | 14996be | the readers, the writer and the sky: 9 files | `== OK in 41s` | 351 + 54 |
  | ae54364 | the eight false comments | `== OK in 42s` | 351 + 54 |
  | (none) | wrap-up step 1 | `== OK in 47s` | 351 + 54 |

  - **The counter on `Core/Input/`:** on `main`, 16 types (15 undocumented, 1 missing a
    `<param>`) and 99 methods (46 undocumented, 53 incomplete); on the branch 0 and 0. The whole
    code has 110 types and 293 methods left for areas 2 to 5.
  - **No code changed:** `tokens` finds the code of all 71 files identical to `main`'s, 17 of
    them with changed comments or layout. `Core/Input/`: 15 files, +968/-339.
  - **No fact lost:** `facts` finds every number, ID and identifier of the old comments in the
    new ones, in the 15 files.
  - **English:** on `main`, 324 of the comment lines of `Core/Input/` held a Romanian function
    word, in all 15 files; on the branch 3, judged above. (This first said "of 519": the scratch
    tool then also counted the declaration line after each doc block. Fixed, it counts 433:
    the 402 non-blank lines of the table, 30 bare `//` lines and one trailing comment.)
  - **Widths:** 27 comment lines of `Core/Input/` ran past 100 columns on `main`; none now.
  - **ASCII:** no added line holds a character outside ASCII.
  - **`/validate`:** the fourteen cases pass, 1237 figures identical to the recorded ones.
  - **`check-design.ps1 -Since main`** listed six notes: `input.md` and `code-map.md` changed
    (above); `dependencies.md`, `extending.md`, `known-limitations.md` and
    `recommendations-and-report.md` were read against the change and hold.

### Code review of area 1 (2026-10-22)

`/code-review main...claude/Document-and-translate-the-input-code` ran at maximum effort: ten
finder angles, eight verifiers (each candidate checked against the code, many with a probe run
on the branch's build), and a final sweep. About 60 candidates; about 15 refuted; 15 findings
reported. The owner had every comment-only finding fixed and the code defects kept as questions.

- **Comments made true about the code,** in `Core/Input/`:
  - two of the eight corrections were themselves wrong: `Printable` shows a non-ASCII byte as
    '?' (the ASCII decoding's replacement), and the `Raster.cs` and `ImageLoader.cs` headers
    swapped one false generalisation for another (`GamutAnalyzer.Build` takes a path;
    `CurveRun` checks `SourceInfo.Format == "FITS"`; a `--wcs` file goes straight to
    `FitsReader`);
  - over-promising new docs: `ImageSharpReader.Load` and `IFrameReader.Load` (the user message
    covers only what Identify catches; "row 0 at the top" ignores EXIF orientation and a FITS
    without ROWORDER), `FitsReader`'s class summary (BITPIX 64 refused; the first HDU with
    NAXIS 2 or more is taken, XTENSION unchecked), `ReadHeader` (a header cut inside its first
    block reads as the end of the file), `ReadData` (the half-scale reading of signed integers
    without BZERO), `HInt`/`HDbl` (`NumberStyles.Any`: no FITS D exponent, lenient commas),
    `FitsWriter.Save` and `FromFits` (the temporary file and the tail cut short),
    `IccPaperBlack.Read`'s `path`, `SourceInfo.Format` and `PlanesInFile`;
  - claims translated as they stood and found false: ImageSharp's Identify "reads only the
    header", "THE ONLY ... for reading" (JpegAnalyzer decodes), "the only code that knows a
    format", the `Gamma` interpolation bound and "exactly on a level", `PowerLut`'s 65537th
    entry, the ulp "7.6e-6 at L* 32..64" (also in `input.md`, and ADR 0035 with a dated
    Corrected note), Otsu's floor with ties, `WithRowsFlipped`'s borrow use, "does not look for
    stars", the catalogue's '#' lines, RA "[0, 360)", the "exact inverse" of `LinearToSrgb`,
    `SeededRandom`'s platform promise for `NextGaussian`;
  - the three quotes of Romanian output translated against the Glossary are Romanian again
    ("asa cum e", "fara profil", "99% sub negrul hartiei"); "linear luminance" of one channel is
    now "linear value"; the four `Stats` tags "The values." say how NaN is handled.
- **Docs:** ADR 0038 names CS1587 and replaces the gate-time line, which read as a cost the
  data do not show; `CLAUDE.md` and the CHANGELOG name the ambiguous `cref` and the
  non-member `///`; the CHANGELOG's count says what it counts; this plan's baselines above.
- **The scratch tool** counted the declaration line after each doc block as a comment line,
  which made `facts` weaker (an identifier dropped from a comment could be "found" in the
  declaration) and gave the 519. Fixed; area 1 re-checked with it: 0 facts lost except the four
  figures corrected on purpose (`7.6e-6`, `32`, `3`, `1.0`).
- **Code defects, questions for the owner** (all on `main` before this branch; none changed
  here, as Assumption 7 says):
  1. `IccProfileInfo.ReadTextTag` reads the `mluc` record size at `off + 16` instead of
     `off + 12`. A v4 profile re-saved by lcms2 (Siril's library) has 00 00 00 00 there, so its
     name is lost, and the report skips "Profilul nu e sRGB" for, e.g., an Adobe RGB v4 profile.
  2. `ImageSharpReader.Load` wraps only Identify's two exception types: damage the decode meets,
     a TIFF variant ImageSharp does not decode, or a cut TIFF that trips Identify's own index
     check reaches the user in English; a TIFF missing part of a strip is analysed with the
     missing rows as black, with no note.
  3. `FitsReader.HInt`/`HDbl` cannot read a FITS D exponent: `BZERO = 3.2768D+04` reads as 0 and
     the image is refused as linear; `CRVAL1` in D gives a wrong WCS silently.
  4. No EXIF/TIFF orientation is applied: an oriented JPEG, TIFF or PNG is measured as stored,
     with its corner names swapped.
  5. A signed 16-bit FITS without BZERO is analysed at half level with no note (Orion stored
     that way: background 0.151 -> 0.076, 276 stars -> 4). R-011's formula implies it and R-145
     knows it on the save side; refuse, note, rescale, or record it in `known-limitations.md`?
  6. `FitsReader` takes the first HDU with NAXIS 2 or more: a first HDU with 4 axes or 2 planes
     is refused even when a valid image follows, a BINTABLE is read as an image, and BITPIX 64
     is refused.
  7. A FITS whose header is cut inside its first block is reported as "niciun HDU cu imagine
     gasit" instead of "header incomplet".
  8. `FitsWriter.Save`: a delete refused with access denied replaces the real error (which the
     window then shows); a source whose tail is cut short after the image is copied without a
     refusal, against R-145's "reports the reason".
  9. Otsu's `minAbove` floor counts ranks, so ties at the cut leave fewer bright patches than
     `DustAnalyzer` relies on (posterised or flat data only).
- **Process questions for the owner:**
  - `/validate` in areas 2 to 4 re-measures figures the token proof shows cannot move, at about
    2.5 min and a 772 MB copy each; the rule "Formula changes re-validate" names the folders, so
    skipping it needs your word.
  - The scratch tool exists only in this session's `%TEMP%` scratchpad; a copy in the repository
    (under `docs/plans/`, or in the kit) would keep areas 2 to 5 from depending on that folder.
- **Left as they are:** `ReportWriter.TargetText` stays without tags until area 3 (its file is
  still Romanian); lists of a type's members in headers and `code-map.md` are summaries.

### Area 2, `Core/Analysis/` (2026-10-23, `claude/Document-and-translate-the-analyzers`)

- **It ran in a session of its own, from area 1's prompt,** in four commits: the seven files of
  the tonal, dust, simulation and curve chain, which the others name; the other nine; the false
  comments; the wrap-up. The scratch tool came from area 1's scratchpad, its `CommentLines` fix
  included, copied into this session's and rebuilt.
- **Area 1's review, applied before writing:**
  - quoted Romanian output stays Romanian: "asa cum e", "negru pur", "pana la X cm", "Punct de
    la", "Pastreaza master-ul de 16 biti", "sub nivelul 32", the class names and the report's
    sentences;
  - "pentru om" is "for the user", as in area 1; "the owner" only where ADR 0029's owner is meant;
  - a patch's L\* on mono is of its linear value, not of a luminance;
  - the NaN, empty and null cases are said where the code returns them, and every guard as the
    parameter's range;
  - every claim a translation carried about a figure, a bound or an edge case was checked against
    its code line, ten of them with a probe on the branch's build: a scratch project named
    `AstroPhotoAnalyzer.Tests`, so that the core's `InternalsVisibleTo` admits it (probes P1 to
    P10 below).
- **28 comments corrected, in a commit of their own (204b07a), each against its line or a probe;**
  26 facts, two of them said twice:
  1. `TonalAnalyzer.Analyze`: "sub nivelul 32" is a sample under 32/255, on 16 bits the levels
     under 8224 (and 8224 itself on a TIFF or PNG, by float rounding), not the script's high byte
     under 32, which stopped at 8191 (P1).
  2. `TonalAnalyzer.LstarShares`: the median's sort would cost about as much as the rest of a
     grid row, not more (P5: 86 ms against 111 on a colour subsample the size of the
     plate-solved FITS's).
  3. `DustAnalyzer.cs`, header: on the band path a patch outside the band `BrightCloudLstar` over
     the modelled sky is band, not sky (`Classify`).
  4. The header and `DustSource.DustClass`: the dust class decides at or under the darkest sky,
     not only darker (`DecideDustSource` compares with `<=`).
  5. `DustMethod.Band`: the band path also needs the frame's span of galactic latitude (ADR 0037).
  6. `DustCloud.Patches`: only the patches inside the disc's bounding box in pixels (P4).
  7. `DustMap.SkySigma`: the robust sigma of every patch's residual, not of the sky patches';
     `BrightThreshold` is infinite when the nebula's tail holds fewer than two patches, not
     whenever no patch is bright nebula (tied tail values leave a finite threshold and none
     above it).
  8. `DustAnalyzer.SubsampleGrid`: a subsample with one side under 8 samples has patches as wide
     as that side, not a single patch (P9: 5 x 20 samples give 4 patches).
  9. `PrintStretchSimulator.cs`, header: the recommended row also has the sky at the visibility
     threshold (ADR 0029, `PrintSafe`).
  10. The same header: 114 cloud rows on the plate-solved FITS (V-004), not 116 on the reference
      image (116 was ADR 0025's measurement with astropy; `de_printat.png` has no WCS).
  11. `SimulationRow.Explicit`: a given target on the grid marks that grid row, not only an
      extra row (P6).
  12. `SimulationResult.ClassFractions`: the quoted bright-nebula text ends in ", pragul Otsu".
  13. `PrintPreview.ReductionStride` and `Render`: the long side is at most `PreviewMaxSide`, not
      under it (P8: 3200 px gives 1600).
  14. `ToneCurve.cs`, header: the table gives the spline at a 16-bit level to float precision,
      not exactly (P2: 65 056 of the 65 536 levels equal, the rest within 8.9e-8; 1.5e-7 on a
      curve of slope 3; on floats 9.5e-8 and 8.3e-7, under the 1e-6 the header gives).
  15. `CurvePoint`: the dust's point keeps its label when it is the origin (`Curves`).
  16. `CurveAdvisor.cs`, header: the peaks' anchor is the median of their class, not its upper
      part (R-141).
  17. `CurveAdvisor.ClassLevels`: the same, and NaN only for an empty band, bright or peak class
      (an empty dust class gives the darkest sky, an empty sky class all the patches).
  18. `CurveAdvisor.ShadowChartLimit`: the curve's fourth point is the bright clouds' when the
      dust's point is the origin (P7).
  19. `GamutFigure.PctByClass`: NaN only on colour; on mono every class reads 0 (P10).
  20. `CurvePurpose`: the purpose also decides the gamut figure (`CurveRun`).
  21. `CurveEvidence.GamutBefore`: null too with a profile that cannot be used (`CurveRun`).
  22. `BitDepthResult.Applicable`: true only on a 16-bit integer source, so false also on a
      32-bit integer FITS and without a Source.
  23. `CropAnalyzer.Analyze`: an odd cut leaves the right (or bottom) side one pixel more.
  24. `StarAnalyzer.cs`, header: the high threshold is on the peak, not on the amplitude; two
      differences from the script, not one (the candidate threshold, ADR 0016).
  25. `StarAnalyzer.IsWindowMax`: both pixels of a flat two-pixel peak pass, so that star is
      measured twice (P3).
  26. `JpegAnalyzer.cs`, header: samples, not pixels; the 3 % was ImageMagick's, this encoder's
      is 1.2 % at q100 (ADR 0019).

  At wrap-up `measurements.md` was brought in line with facts 13, 14 and 23.
- **The probes,** on the branch's build, 2026-10-23: P1 the 16-bit levels under `DarkLevelA`;
  P2 `MapSample` against `Map` on every 16-bit level and on 5 million floats, on V-008's
  `Puternica` and on a curve of slope 3; P3 a star centred between two pixels (two stars
  measured, FWHM 3.135 each); P4 `FindClouds` against every patch of a synthetic WCS like the
  plate-solved FITS's (5837 x 3867, 41.7"/px, no rotation: 408 clouds either way, LDN 621 with
  372 of its 379 patches and LDN 649 with 169 of 170, the rows of 9 patches or more unchanged at
  119); P5 the time of a grid row's parts; P6 a given target of 0.19; P7 the shadow chart's end
  with the dust at 0.05 and at 0 (0.375, then 0.625 where R-147 wants 0.375); P8 the reduction
  step at 1600, 3200 and 3201 px; P9 a grid on 5 x 20 samples; P10 the gamut figure on mono and
  on colour with empty classes.
- **Left as they are:** R-032 says the crop cuts "equally from both sides" (requirements are not
  this task's; true to a pixel); `window.md` says the map's image is "under `PreviewMaxSide`"
  (area 5's note: "at most", fact 13); the Romanian scan leaves 11 lines, all quoted output in
  `PrintStretchSimulator.cs` (the report's sentences, quoted across line breaks).
- **Code defects, questions for the owner** (all on `main` before this branch; none changed
  here, as Assumption 7 says):
  1. `CurveAdvisor.ShadowChartLimit` takes `Points[3]`; when the dust's anchor is 0 its point
     replaces the (0, 0) end, so the shadow chart ends at 1.25 x the bright clouds' input instead
     of the band's (P7: 0.625 against 0.375), against R-147.
  2. `StarAnalyzer` allows ties for the window's maximum, so a star whose two brightest pixels
     are equal is measured twice (P3), in the count and in the FWHM statistics; rare on 16 bits,
     likelier on 8.
  3. `DustAnalyzer.FindClouds` bounds its search by the disc's radius at the central plate scale;
     far from the tangent point the TAN projection stretches a disc (radially by 1 / cos^2 of
     the distance), so a large cloud near the edge loses patches that are inside its disc (P4:
     7 of LDN 621's 379, 1 of LDN 649's 170; no row appears or disappears).
  4. `CurveAdvisor.Measure` takes the subsample as it is, `sub`, and never reads it.
  5. `TonalAnalyzer.Analyze` compares floats with 32/255f, so on a 16-bit TIFF or PNG the level
     8224, exactly the 8-bit level 32, counts as under 32, and on a FITS it does not (P1);
     negligible on the figures.
- **Process:** area 1's two questions had no answer when this session started, so `/validate`
  ran, as the rule "Formula changes re-validate" asks (the seven files copied, 772 MB), and the
  scratch tool still lives in the scratchpads: area 3 copies it, with `checks.sh`, `wrapdoc.py`,
  `drift.py` and the probe project, from this session's scratchpad
  (`...\E--Photografie-Code-AstroPhotoAnalyzer\a214bf4e-2a6a-45f9-8451-f5225048091c\scratchpad`).
- **Measured on 2026-10-23:**

  | Commit | What | Gate | Tests |
  |---|---|---|---|
  | c1233ff | the tonal, dust, simulation and curve analyzers: 7 files | `== OK in 38s` | 351 + 54 |
  | b32054e | the other nine analyzers | `== OK in 38s` | 351 + 54 |
  | 204b07a | the 28 false comments | `== OK in 43s` | 351 + 54 |

  - **The counter on `Core/Analysis/`:** on `main`, 41 types (38 undocumented, 3 positional
    records missing their `<param>`s) and 83 methods (34 undocumented, 49 incomplete); on the
    branch 0 and 0. The whole code has 69 types and 210 methods left for areas 3 to 5.
  - **No code changed:** `tokens` finds the code of all 71 files identical to `main`'s, the 16 of
    `Core/Analysis/` with changed comments or layout: +1547/-499 before the wrap-up.
  - **No fact lost:** `facts` finds every number, ID and identifier of the old comments in the
    new ones, except "116" (fact 10, on purpose), "20.000" (now "20,000"), one of the two "0.01"
    of "din 0.01 in 0.01" ("in steps of 0.01"), "0033" (on `main` a line break split "ADR 0033")
    and "DarkLevelB" (now `Thresholds.DarkLevelB` in a `cref`).
  - **English:** on `main`, 482 of the 602 comment lines of `Core/Analysis/` held a Romanian
    function word, in all 16 files; on the branch 11 of 1648, judged above.
  - **Widths:** 79 comment lines of `Core/Analysis/` ran past 100 columns on `main`; none now.
  - **ASCII:** no added line holds a character outside ASCII.
  - **`/validate`:** the fourteen cases pass, 1237 figures identical to the recorded ones.
  - **`check-design.ps1 -Since main`** listed four notes: `measurements.md` changed (above);
    `code-map.md`, `dependencies.md` and `known-limitations.md` were read against the change and
    hold.

### Code review of area 2 (2026-10-23)

`/code-review main...claude/Document-and-translate-the-analyzers` ran at maximum effort: eleven
finder angles, six verifiers (most candidates checked with a probe on the branch's build or on
the reference images), and a final sweep. 48 distinct candidates, 12 refuted, 38 confirmed (36
plus 2 from the sweep); 15 reported. The owner had every comment-only finding fixed, the lower
ones included, and the code defects kept as questions. The probes are in the scratchpad's
`review\` folder.

- **Comments made true about the code,** in `Core/Analysis/`:
  - two of area 2's own corrections were wrong or short: the "sub nivelul N" comment (fact 1),
    since an 8-bit TIFF, PNG or JPEG is read as n x 257 x (1 / 65535) in float, which counts the
    level N itself too; and the `ToneCurve` header's float bound (fact 14), exceeded within a
    step of the corner where a curve clamped at 1 meets the clamp (1.6e-6 on V-014's Maxima, up
    to 7.8e-6 on synthetic anchors);
  - new docs that over-promised: `TonalResult`'s summary (the background level is on 0..1, not
    in levels), `SigmaLsb`'s return (the / sqrt 2 x 255), `PrintSizeAnalyzer.Analyze`'s fwhmPx
    (NaN under `StarMinCount` stars), `Crossing`'s return (a branch no input reaches),
    `CurveAnchors`, `Gray` and `Problem` (only the three upper classes can be NaN), `Map` and
    `Slope` with NaN, `DpiLadder`'s return (now names `ReferenceDpi`), `Simulate`'s target
    range, `Explicit` and `Primary` (the 6-decimal rounding), the exact targets' "null" (a given
    target over 0.60 extends the ladder), `SimulationResult.Clouds` (empty without a WCS);
  - claims translated as they stood and found false: `DmaxToPassAsIs` "passes as it is" (the
    dust only), `Curves` "a spline that would not be monotone" (the clamped table is judged),
    the curve evidence "re-measured" (the noise gain and noise after are predicted), the gamut
    figure "good to tenths of a percent", `FindClouds` "whose disc holds a patch centre",
    `ClassifyBySky`'s tail fraction (by rank) and its sigma-0 sentence (positive residuals are
    still split by Otsu), `BackgroundAnalyzer`'s "the two give the same thing" (real data ties),
    the crop's "Null when nothing is cut", `QuantizationAnalyzer`'s "the difference between the
    two", `PercentileFromHistogram`'s count (one sample more at 99.9 % of a whole n), "pixels"
    for samples in the `TonalAnalyzer` and `BitDepthAnalyzer` headers;
  - quoted output back in Romanian ("sub negru", "sub L* 15"; "PROBLEMA", "ATENTIE"; the class
    names quoted); the star's "background" is its base, as R-070 and the file say; a property
    comment added to `AlreadyPrintSafe` (the dust alone, not print-safe).
- **Docs:** `measurements.md` (the B4 heading names the sky too; the gamut grid's figures
  re-measured, the first record's 0.01 % / 0.11 % now 0.16 % / 0.72 %, with the per-class
  undercount on the FITS; the `<=` note), ADR 0014 a dated Corrected note ("real data has no
  ties"), the CHANGELOG's Curbe line (no histogram path since ADR 0033; the peaks from the
  median), this plan's "754 MB" (772 MB).
- **Code defects, questions for the owner** (all on `main` before this branch; none changed
  here, as Assumption 7 says), most severe first:
  1. `DmaxToPassAsIs` tests the dust only, and the report says "trece asa cum e de la Dmax X":
     false whenever the sky is under L* 15 (de_printat.png: 2.33, yet ATENTIE at 2.4;
     `Golden/dark-sky.txt` prints it inside a PROBLEMA section). R-043 and README.md say the
     same. Reword, or give the Dmax at which both pass (none when the sky is the cause)?
  2. Every Maxima seen (V-009, V-011, V-014) has a spline that rises over 1 and is kept: the
     table maps 21 to 41 % of the input range to white. R-140 and R-142 say a spline that is not
     monotone is never suggested; the code and `measurements.md` judge the clamped table.
     Reject the overshoot (no Maxima on those images), cap the scale, or reword R-140/R-142?
  3. On an 8-bit TIFF, PNG or JPEG the dark-level counts include the level itself (+2 points
     "sub nivelul 32" on the reference image's 8-bit version; a flat level-32 file reads 100 %).
     This re-scopes area 2's defect 5, which said 16-bit and negligible. Compare levels, not
     floats?
  4. `Keep16BitMaster` reads `MissingFrom8` alone: an 8-bit file saved as 16 bits gets both
     "Fisierul e pe 8 biti deghizat in 16" and "Pastreaza master-ul de 16 biti". R-082 and R-096
     word the rule differently. Use the difference, or skip the advice on an upscale?
  5. The crop is decided on the ratio (1e-6), not on pixels: 4961 x 3508 at 297:210 prints an
     Attention that cuts 0 px; 3508 x 2480 cuts 1 px and says 0 per side (R-032's letter).
  6. A target background that rounds to 0 or 1 at 6 decimals, or NaN, or exactly 1 from the
     window (whose field accepts (0, 1]), ends the run with an error and no report.
  7. A given target over 0.60 extends the ladder, so the exact targets are interpolated across
     the gap (0.615 printed where the 0.01 ladder crosses at 0.638); R-043 wants "> 0.60".
  8. A given target with a 7th decimal loses its "(dat)" mark and its Preview role (R-023).
  9. The gamut grid undercounts by 1.5 to 6 points per class (above); a finer grid, an exact
     round trip on the pixels near the edge, or the figure labelled as a lower bound?
  10. `FindClouds` can skip whole clouds, not only outer patches: over 312 pointings of a frame
      like the plate-solved FITS, 48 clouds skipped, 9 of them with a row; this extends area 2's
      defect 3, whose "no row appears or disappears" held at one pointing only (R-044).
  11. `<=` on the background percentile moves the cast verdict on 8-bit data (+2.0 against
      +1.0 levels on the reference image's 8-bit version); ADR 0014 is now corrected.
  12. `Thresholds.StarWindow` is read by no code (`StarAnalyzer` has `Half = 12`), against ADR
      0012 and R-070.
  13. `AlreadyPrintSafe` and validation.json's `simulation.alreadyPrintSafe` mean the dust
      alone, true on frames whose verdict is ATENTIE; rename, or drop the figure?
  14. Area 2's defect 2 (a two-pixel flat peak measured twice): R-070 and ADR 0016 require "ties
      allowed", so the fix removes duplicate maxima, or amends both.
  15. Efficiency, all with identical results measured: an early exit from the ladder once every
      series has crossed (1.4 s on de_printat.png, 0.9 s on the FITS); selection instead of full
      sorts for the medians and percentiles (0.7 to 1 s per run); one pass for a grid row's
      patch L*s and L* shares (0.2 to 0.5 s); a 256-entry table in `Stress` (0.2 s);
      `ClassLevels` recomputing the "before" patch L*s that `AsIsDust` holds (0.19 s per
      Calculeaza), its `paperBlackLstar` changing nothing; `MapSample` repeating
      `RasterOps.Interpolate`.
- **Handed on, not changed here:** areas 3 and 4 find two stale counts of the same fact as fact
  10: `Thresholds.cs:217` "Pe imaginea de referinta: 116 din 406 nori" (the build gives 114 rows
  of 397 clouds on the plate-solved FITS) and `ReportWriter.cs:155`'s example "(406 nori, 4078
  dale)", ADR 0025's superseded definition; area 5 `window.md`'s "under PreviewMaxSide". R-032,
  R-043, R-070, R-082/R-096, R-140/R-142, R-147's "two ends unlabelled", README.md:73-75 and
  CLAUDE.md:165 ("the anchors are the upper part of the classes") are the owner's text.
- **Refuted, for the record:** the sky's one-decimal rounding against the table (unreachable for
  a median of float patch L*s), the gamut's "~28 microseconds" (the 17^3 method reproduces it),
  the preview share's wording, the scare quotes, `ShadowChartLimit` (known defect 1), the
  "floor" wording, the hand-written inverse in `DmaxToPassAsIs`.

### Area 3, `Core/Report/`, `Core/Runs/`, `ThirdParty.cs`, `Version.cs` (2026-10-23, `claude/Document-and-translate-the-report-and-the-runs`)

- **It ran in a session of its own, from area 2's prompt,** in three commits: the ten files in
  one (1,517 lines in the plan's table, 1,546 on `main` at 1b80408), the false comments, the
  wrap-up. The scratch tool, `checks.sh`, `wrapdoc.py`, `drift.py` and the probe project came
  from area 2's scratchpad, rebuilt in this session's; `checks.sh` takes the area's four
  prefixes.
- **The worktree's name, 46 characters, is over the ~41 that area 2's memory gave as safe**
  (the plan fixes the branch name): the longest path of the monorepo, a DarksCheck ADR, came to
  257 of Windows's 260 and the checkout was complete. Area 4's name,
  `Document-and-translate-the-thresholds-parameters-and-console` (60 characters), would pass
  260 (271); its worktree needs a shorter folder name than the branch's, which the kit's rule
  "Worktrees" asks to match, or a shorter branch name: the owner's call when area 4 starts.
- **Areas 1 and 2's reviews, applied before writing:** the quoted Romanian output stays as the
  user sees it ("asa cum e", "fiindca", "PROBLEMA", the report's example lines); "omul" is "the
  user", and "the owner" only in `Version.cs`, where ADR 0029's owner increments the version;
  every NaN, null and empty case is said where the code returns or accepts it; a throw a caller
  plans for (`BorrowWcs`, the two `Execute`s, `CurveExport.Save`, the `Recommendation`
  constructor) is in the summary or the parameter's range, no `<remarks>`; each claim checked
  against its code line or caller, one with a probe on the branch's build (P1 below).
- **The counter's methods: 78, not 76.** `main` moved since the plan's table (area 2's review
  fixes); the area had 18 types (14 undocumented, `ThirdPartyComponent` missing its `<param>`s:
  15 to touch) and 78 methods (48 undocumented, 30 incomplete).
- **Three comments corrected, in a commit of their own (08681ed):**
  1. `ReportWriter.DustDefinition`: the example was ADR 0025's superseded line ("10 % din dalele
     norilor catalogati (406 nori, 4078 dale); cerul = mediana dalelor din afara norilor"),
     handed on by area 2; it is now the line `Golden/dark-sky.txt` prints.
  2. `Recommendations.Background`: the branch where the dust reaches black on the grid but the
     sky reaches L\* 15 on no row is not "only on a grid the user gave": P1 reaches it on the
     default grid, with a mono frame whose left 45 % is a sky at 0.08 and the rest a nebula at
     0.25 ("Nicio tinta pana la 0.20 nu aduce cerul la L* 15; ar trebui 0.341"), and on six of
     the 36 frames it tried (P1 is `probe3` in this session's scratchpad).
  3. `Fmt.cs`, header: not every number in the report goes through `Fmt`; the integers (the
     counts, `DarkLevelA`, `DustPatchPx`, the shadow-lift levels, the seed) are interpolated as
     they are, which holds because both applications run in the invariant culture.

  At wrap-up `recommendations-and-report.md` was brought in line with fact 3.
- **Left as they are:** `CurveExport.Save` says the operating system refuses any other spelling
  of the input, "a link" among them; a symbolic link would more likely be replaced as a link,
  leaving the input untouched, but no probe ran (creating one needs a privilege), so the
  sentence stands as translated. `Thresholds.cs:217`'s "116 din 406 nori" is area 4's, as area 2
  handed it on. The Romanian scan leaves 5 lines, all quoted output in `ReportWriter.cs` (the
  `DustDefinition` and `DustDmaxText` examples).
- **Code defects, questions for the owner:** none found in this area.
- **Measured on 2026-10-23:**

  | Commit | What | Gate | Tests |
  |---|---|---|---|
  | 51fe855 | the ten files | `== OK in 66s` | 380 + 58 |
  | 08681ed | the three false comments | `== OK in 58s` | 380 + 58 |

  - **The counter on the area:** on `main`, 15 types and 78 methods to touch; on the branch 0
    and 0. The whole code has 54 types and 138 methods left for areas 4 and 5.
  - **No code changed:** `tokens` finds the code of all 71 files identical to `main`'s, the 10 of
    the area with changed comments or layout: +756/-186.
  - **No fact lost:** `facts` finds every number, ID and identifier of the old comments in the
    new ones, except the stale example's "10", "406", "4078", "3.6" and "2.4" (fact 1, on
    purpose) and "DustCloudReportRows" (now `Thresholds.DustCloudReportRows` in a `cref`).
  - **English:** on `main`, 174 of the 239 comment lines of the area held a Romanian function
    word, in all 10 files; on the branch 5 of 803, judged above.
  - **Widths:** 16 comment lines of the area ran past 100 columns on `main`; none now.
  - **ASCII:** no added line holds a character outside ASCII.
  - **`/validate`:** the fourteen cases pass; of the 1242 figures 1240 are identical to the
    recorded ones and two differ by 0.0001, inside the 0.01 tolerance: V-011's
    `curves.Maxima.lstar.clouds` (67.1502 recorded, 67.1503 measured) and V-014's
    `curves.Maxima.lstar.peaks` (77.9226, 77.9227). The code is token-identical to `main`, and a
    second run gave the same figures, so this branch did not move them. Both were recorded at
    6bad089, before three later commits to the core on `main`; a0dcb7d, which changed the
    spline's tolerance, is the likely cause, not checked.
  - **`check-design.ps1 -Since main`** listed five notes: `recommendations-and-report.md`
    changed (above); `code-map.md`, `dependencies.md`, `extending.md` and `pipeline.md` were read
    against the change and hold.

### Review of area 3 (2026-10-23)

A fresh subagent read the branch's diff against the code, its callers and the requirements, one
probe on the build (`Fmt.S1` around zero). It found no meaning lost in the translation and seven
claims too strong, each checked against its line and fixed in one commit:

- `CurveTexts`'s summary: the window writes some sentences of its own (`CurvesView.xaml:128`,
  the curve window's title and headings); the class holds those of diagnosis and evidence.
- `Fmt.cs`'s header (fact 3 as first corrected) read as if every integer were interpolated; many
  go through `Fmt.Int` and `Fmt.Dim`. `recommendations-and-report.md` listed `Int` and `Dim`
  among the numbers "with decimals" and left out `Level` and `Dmax`.
- `ShadowHeading`'s limit is not within 0..1: 1.25 x the band's input, over 1 when that input is
  over 0.8.
- `TargetText`'s null also comes with a background at 0 or 1 (`SimulationResult.ExactTarget`).
- `Recommendations.Size` without a distance says the wall chooses only when the stars are points.
- This plan: area 4's branch name has 60 characters, and the path would be 271, not 61 and 272.

Refuted: "screen pixels" for `CurveRun.Execute`'s size (the window passes `DpiScale`d device
pixels, `ResultViews.xaml.cs:46`), and about 25 other claims the review listed as checked. Left
as it is: `CurveExport.Save`'s "a link", which the review too could not settle without a probe
(a hard link is refused, as the input's handle has no delete sharing; a symbolic link is likely
replaced as a link, the input untouched).

### Area 4, `Thresholds.cs`, `Parameters.cs`, `AstroPhotoAnalyzer/` (2026-10-23, `claude/Translate-the-thresholds-and-console`)

- **The branch and the worktree are named `claude/Translate-the-thresholds-and-console`,** not
  the plan's `claude/Document-and-translate-the-thresholds-parameters-and-console`: the owner
  chose the name on 2026-10-23, since the plan's would push the worktree past Windows's
  260-character path limit (area 3's "What deviated"). The worktree's folder matches the branch,
  as the kit's rule "Worktrees" asks.
- **It ran in a session of its own, from area 3's prompt,** in three commits: the five files, the
  false comments, the wrap-up. The scratch tool, `checks.sh`, `wrapdoc.py`, `drift.py` and the
  two probe projects came from area 3's scratchpad, rebuilt in this session's; `checks.sh`
  takes the area's three prefixes.
- **Areas 1 to 3's reviews, applied before writing:** the quoted Romanian output stays as the
  user sees it ("Atentie", "fiindca", "(explicit)", "EROARE: ", the class and strength names);
  "omul" is "the user", "proprietarul" "the owner" (ADR 0026, 0033, 0034's choices); the star's
  "fundal" is its base, the median of the window's border, as `StarAnalyzer` says; every claim
  of a threshold's comment was checked against the code that reads the threshold (a list of
  every use, 105 constants) and its figures against `docs/validation.json`; every NaN, null and
  throw is said where the code returns or accepts it.
- **The counter's figures:** the area had 6 types and 17 methods to touch, not the table's 15
  (the difference was not traced: `main` moved since, and the counter was extended).
  `Thresholds` gets a type summary; the constants that had no comment (`DarkLevelB`,
  `NoiseTileSize`, ...) share the one above them and get none, as the rule allows.
- **Thirteen comments corrected, in a commit of their own (1f1f4e4, whose message says twelve),
  each against its line or a figure:**
  1. `UpscaledFraction`: a fraction of samples, every plane's, not of pixels
     (`BitDepthAnalyzer`, `Recommendations.cs:412`).
  2. `DarkLevelA`: the dark counts are of samples of every channel whose 8-bit level is under the
     threshold (`TonalAnalyzer.Level8Under`, ADR 0043), not "dark pixels".
  3. `ScreenBackgroundLo`: de_printat.png has no WCS, so no catalogued dust (V-001: 0 clouds);
     "reaches black at 0.19" is now the plate-solved FITS at Dmax 2.0, exact target 0.184, row
     0.19 (V-004).
  4. `DustCloudMinPatches`: "116 of 406 clouds" on the reference image, handed on by area 2, is
     now 114 of the 397 clouds with a patch in the frame, on the plate-solved FITS (V-004; 406 was
     the astropy measurement, `validation.md`).
  5. `PlateauTileMaxRangeLevels`: up to 120 tiles; on the reference image 20 pass in
     `PlateauMaxAttempts` tries and give 2.68 px (V-001), not "120 tiles ... 2.6 px".
  6. `JpegErrorCountLevels`: samples, not pixels (`JpegAnalyzer`'s per-sample `Add`).
  7. `ShadowLiftLevelLo`: the gaps are counted strictly between the two levels, from the first
     level used to the last (`QuantizationAnalyzer.Count`).
  8. `MissingLevelsWarn`: `Keep16BitMaster` compares the levels missing on the 8-bit path beyond
     those missing on the 16-bit one, not the missing levels alone.
  9. `CornerColourSpreadRatio`: the spread must exceed the fraction of the mean absolute
     departure (`>`), not reach it.
  10. `CurveShadowChartFactor`: the chart ends at the curve's fourth point, the band's, or the
      bright clouds' when the dust's point is the origin (`CurveAdvisor.ShadowChartLimit`).
  11. `CurveGamutGrid`: the pixels near the threshold take the exact round trip (ADR 0042), not
      all "by interpolation".
  12. `Parameters.DmaxProfile`: not "for the report only": the Tonuri ladder's paper-black row
      reads it, and `CurveRun` reopens `DmaxProfilePath` only while it is set.
  13. `AnalyzerCli.cs`, header: exit code 1 also covers a folder, an invalid parameter
      combination, an unusable ICC profile and a `-o` file not written (`Run`, `console.md`).
- **Left as they are:** the historical figures of `DustPatchPx` ("the parked branch"),
  `BandGalacticLatitudeDeg`, `DmaxSensitivityStep`, `NaturalDistanceTolerance` and the curve
  constants, measured on an earlier build and dated or attributed as such. `thresholds.md` and
  `console.md` say none of the thirteen facts; they hold.
- **Code defects, questions for the owner** (on `main` before this branch; none changed here, as
  Assumption 7 says):
  1. The console's number flags accept "NaN", and `--distance`, `--sizes`, `--passepartout`,
     `--frame` and `--aspect` "Infinity" too: `TryNum` reads them (the invariant culture), and
     only `--target-bg`'s check refuses NaN. Probed on 2026-10-23 with the branch's console on
     de_printat.png: `--dmax NaN` prints "Dmax n/a, negrul hartiei L* n/a" and exact targets
     "<= 0.05 la Dmax n/a", `--distance NaN` "Distanta de privire: n/a m", `--passepartout NaN`
     outer sizes "n/a x n/a", `--aspect NaN:1` "ramane 5938 x 1 px", `--sizes NaN` a size row of
     "n/a"; each run exits 2, with no error. Refuse NaN and infinity in `TryNum` (one place, a
     console message), or also in the core's `Validate`, so the window's fields share it (area
     5 to check `ParameterFields`)?
- **Measured on 2026-10-23:**

  | Commit | What | Gate | Tests |
  |---|---|---|---|
  | 5efd860 | the five files | `== OK in 62s` | 380 + 58 |
  | 1f1f4e4 | the thirteen false comments | `== OK in 63s` | 380 + 58 |

  - **The counter on the area:** on `main`, 6 types and 17 methods to touch (10 undocumented, 7
    incomplete); on the branch 0 and 0. The whole code has 48 types and 121 methods left for
    area 5 (the plan's table said 48 and 120; `main` moved).
  - **No code changed:** `tokens` finds the code of all 71 files identical to `main`'s, the 5 of
    the area with changed comments or layout: +606/-350.
  - **No fact lost:** `facts` finds every number, ID and identifier of the old comments in the
    new ones, except "116", "406" and "2.6" (facts 4 and 5, on purpose), "35 937" (now
    "35,937") and one of the two "0.01" of "din 0.01 in 0.01" ("in steps of 0.01").
  - **English:** on `main`, 338 of the 475 comment lines of the area held a Romanian function
    word, in all 5 files; on the branch 0 of 731.
  - **Widths:** 45 comment lines of the area ran past 100 columns on `main`; none now.
  - **ASCII:** no added line holds a character outside ASCII.
  - **`/validate`:** the fourteen cases pass; of the 1242 figures 1240 are identical to the
    recorded ones, and the same two as in area 3 differ by 0.0001, inside the 0.01 tolerance
    (V-011's `curves.Maxima.lstar.clouds`, V-014's `curves.Maxima.lstar.peaks`).
  - **`check-design.ps1 -Since main`** listed four notes: `code-map.md`, `console.md`,
    `extending.md` and `thresholds.md`, read against the change; all hold.

### Area 5, `AstroPhotoAnalyzer.Wpf/` (2026-10-24, `claude/Document-and-translate-the-window`)

- **It ran in a session of its own, from area 4's prompt,** in three commits: the 25 files, the
  false comments with the `window.md` fix the owner asked for, the wrap-up. The scratch tool,
  `checks.sh`, `wrapdoc.py`, `drift.py` and the two probe projects came from area 4's
  scratchpad, rebuilt in this session's; `checks.sh` takes the area's one prefix. A third probe,
  `nanprobe` (a console project named `AstroPhotoAnalyzer.Wpf.Tests`, so that the window's
  `InternalsVisibleTo` admits it), answers area 4's code question for the window, below.
- **Areas 1 to 4's reviews, applied before writing:** the quoted Romanian output stays as the
  user sees it ("asa cum e", "se calculeaza", "sfert", the button labels "Analizeaza",
  "Calculeaza", "Foloseste", the window titles); "omul" is "the user"; every NaN, null and throw
  is said where the code returns or accepts it; a claim about framework behaviour was checked
  against the API (a null `PropertyChangedEventArgs` name means every property; `Progress<T>`
  captures the synchronization context at construction; the DWM attribute numbers); every other
  claim against its code line or its XAML (the maximised windows, the map label bound to `Name`,
  the F1 command, the `TextBoxBase` test of the arrow keys).
- **The counter's figures:** 48 types and 121 methods to touch (95 undocumented, 26 incomplete),
  as area 4 said; `main` at 8718852 holds 159 types and 419 methods in all, against the plan's
  158 and 392, the reviews' fixes having added members.
- **`<inheritdoc/>` on 12 members:** the `Populate` and `Clear` overrides of the six tabs in
  `ResultTabs.cs`, whose base says it all (fills the rows and sets `HasResult`; empties them).
  Own text on the Curbe tab's two (it keeps the result and fills nothing; it returns the purpose
  to print), on the WPF overrides (`OnStartup`, the two `OnRender`), the six converters' twelve
  methods, `RelayCommand` and every constructor, as Assumption 3 says.
- **The six one-line views** of `ResultViews.xaml.cs` are split into blocks, so that each
  constructor has its comment; `tokens` finds the code identical.
- **Four comments corrected, in a commit of their own (00ab833), each against its line:**
  1. `MainViewModel.cs`, header: seven tabs, not six (the Curbe tab, R-146, was added after it).
  2. `ResultTabs.cs`, header: the tabs phrase no sentence of diagnosis; the descriptions, guides
     and checks are their own explanation, every number interpolated (ADR 0020, the rule
     "Explanation is not diagnosis"), which the header, written before the explanation pass,
     denied.
  3. `RasterBitmaps.Whole`: the step brings the long side to at most `maxSide`, not under it
     (3200 px at 1600 gives 1600); area 2's fact 13 again, on the window's copy of the formula.
  4. `CloudLayer.Caption`: the example lacked the "(57 dale)" tail `MapViewModel` appends, and
     the map's label shows `Name` alone (`MapWindow.xaml:115`), not the caption.

  With them, `window.md`'s "under `PreviewMaxSide`" is "at most", as area 2 handed on and the
  owner asked.
- **Three omissions completed in the translation itself,** not in a commit of their own, since
  each adds to a true sentence rather than replacing a false one: `SimulationRows` names the
  footer beside the header; the Tonuri `Guide` names `Parameters` among the sources of its
  figures (`Parameters.DmaxOf`, as `window.md` says); the `IccPickerViewModel` header's "reads
  no profile" is rendered "parses no profile", since the view-model asks the core to read
  (`IccPaperBlack.Installed`, `IccPaperBlack.Read`).
- **Left as they are:** the `CurveCanvas` header's "the output axis goes up to the largest value
  drawn" (the axis ends at `XMax` or above; `OnRender`'s own comment says it exactly); the
  `MainViewModel` header's "a few seconds on 23 MP (the JPEG is half of them)", a figure not
  re-measured; `RelayCommand`'s "see FlatCheck for why". The Romanian scan leaves one line in the
  area, the quoted "se calculeaza" of `CurvesTab.cs` split across a line break, and 20 in the
  whole code, all quoted output or "et al.", judged in areas 1 to 3 and here.
- **Code question for the owner, area 4's question 1 answered for the window** (probed on
  2026-10-24 with `TryBuild` on each field, `nanprobe` in this session's scratchpad): the
  window accepts "NaN" in Dmax, the distance, the widths, the passepartout and the frame
  (`OptionalNumber`'s `v <= lo` and `v > hi` are both false on NaN, `SizesOf`'s `v <= 0` too)
  and builds the parameters with NaN in them; "Infinity" is refused by the five range-checked
  fields (`> hi`) but accepted by the widths (`[Infinity]`) and by the aspect (`Infinity:1`
  gives a ratio of infinity, `1:Infinity` 0, `Infinity:Infinity` NaN); "-Infinity" is refused
  everywhere; the target background refuses all three with the core's message (R-023, ADR
  0040). So the window matches the console, and a check in the core's `Parameters.Validate`
  (or in `TargetBackgroundError`'s manner, per parameter) would cover both applications at
  once, where a fix in `TryNum` and `ParseNumber` would be two. Recommendation: the core.
- **Process question for the owner,** put here as the planning review asked: once this branch
  is merged no code file carries a Romanian comment, and `CLAUDE.md`'s rule "Languages" still
  says "most of them still carry Romanian ones: a file's comments are translated when it gets
  its documentation comments ... until then a comment added to it is Romanian". Options:
  (a) reword the rule now, on this branch at the owner's word: the C# comments are English (the
  kit's ADR 0035), the tests' Romanian comments (29 of 54 files, about 246 lines) stay until a
  task of their own, and the csproj and `Directory.Build.props` comments stay Romanian (ADR
  0005); (b) leave it until the tests are translated. Recommendation: (a), with a dated note
  on ADR 0005's Status; the tests' translation is a task of its own, not this one.
- **Measured on 2026-10-24:**

  | Commit | What | Gate | Tests |
  |---|---|---|---|
  | aa9639f | the 25 files | `== OK in 40s` | 380 + 58 |
  | 00ab833 | the four false comments and `window.md` | `== OK in 47s` | 380 + 58 |

  - **The counter on the area:** on `main`, 48 types and 121 methods to touch; on the branch 0
    and 0. **The whole code shows 0 and 0:** 159 types, 419 methods, 12 of them `<inheritdoc/>`,
    as "Then" below asked.
  - **No code changed:** `tokens` finds the code of all 71 files identical to `main`'s, the 25 of
    the area with changed comments or layout: +1093/-266 in the first commit.
  - **No fact lost:** `facts` finds every number, ID and identifier of the old comments in the
    new ones, except "ViewModel" in six files (now "view-model") and "15" (now
    `DustCloudReportRows`, the threshold it was).
  - **English:** on `main`, 251 of the 309 comment lines of the area held a Romanian function
    word, in all 25 files; on the branch 1 of 1104, judged above.
  - **Widths:** 52 comment lines of the area ran past 100 columns on `main`; none now.
  - **ASCII:** no added line holds a character outside ASCII.
  - **`/validate`:** not run; the area touches no numeric code (Assumption 10), and `tokens`
    shows the core identical to `main`'s.
  - **`check-design.ps1 -Since main`** listed four notes: `window.md` changed (the fix above);
    `code-map.md`, `console.md` and `extending.md` were read against the change and hold.
