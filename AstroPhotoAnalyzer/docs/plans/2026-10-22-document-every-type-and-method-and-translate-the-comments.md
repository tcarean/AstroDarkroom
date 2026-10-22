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

## What deviated

(Filled in by each area at its wrap-up.)
