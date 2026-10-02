# 2026-10-02 Adopt the ClaudeCodeStarterKit process

Status: **approved** by the owner on 2026-10-02 ("Aprob planul, trei commit-uri, mută versiunea
în Directory.Build.props"), with two instructions, both of which confirm the body below:

- **Three commits**, one per layer, as the body proposes.
- **The version moves to `Directory.Build.props`**, as ADR 0004 proposes.

Written on 2026-10-02 in the main checkout (`main` at 9dd0b79; this project's last commit is
0982260 of 2026-09-09) for an implementing session that is **not** the one that wrote it.
Everything the implementing session needs is in this file or in a file this one names; it does
not need the conversation that produced it.

The implementing session fills "What deviated" at its wrap-up. The file was written in the main
checkout and moved into the task's worktree before the first step.

## Goal

Bring ImageVisualCompare onto the process the kit at `E:\Photografie\Code\ClaudeCodeStarterKit`
defines (`GHID.md`, "Migrarea unui proiect existent"), in the three layers the guide describes,
exactly as FlatCheck was migrated on 2026-09-25 and AstroPhotoAnalyzer on 2026-10-02, and bring
the documentation to the state it would have had if the process had been used from the start:
`CLAUDE.md` holds only the project's rules; the behaviour is in `docs/requirements.md` with IDs;
the decisions are in `docs/decisions/`; the explanations are in `docs/design/`; the figures on a
real pair of images are in `docs/validation.json`, read by a test. **Nothing written today is
deleted: it is moved.**

The difference from the two earlier migrations: here the document to split is `CLAUDE.md`
itself (247 lines), as in FlatCheck, but it is already well organised: a file map
("Structure"), eight invariants that each record a decision and the defect behind it, four
topical sections ("No imaging dependency", "Memory, measured", "Testing", "Versioning",
"Licensing") and a "Committing" rule the kit supersedes. There is no `docs/` folder at all, no
test data, and the comments are already English in 36 of the 39 `.cs` files. This is the
smallest of the three migrations: one session.

## Read first (the implementing session, before any edit)

1. `../ClaudeCodeStarterKit/.claude/rules/kit/process.md` - the rules the project follows from
   the first commit. Every rule is cited by its name in quotes, never by a number.
2. `../ClaudeCodeStarterKit/README.md` - "Ce e unde" and "Ce e impus mecanic".
3. `../ClaudeCodeStarterKit/GHID.md`, section "Migrarea unui proiect existent" (Romanian).
4. `../AstroPhotoAnalyzer/CLAUDE.md` - the template for the new `CLAUDE.md` (the same shape
   here; Appendix A is already written in that shape).
5. `../AstroPhotoAnalyzer/docs/plans/2026-10-02-adopt-the-starter-kit.md`, whole, with its
   "What deviated" and the docs-health report at the end: the same migration, the same day, one
   project over. Its ADRs `../AstroPhotoAnalyzer/docs/decisions/0001` to `0006` are the models
   for ADRs 0001 to 0007 here.
6. `../AstroPhotoAnalyzer/docs/design/README.md` (the index and the Covers rule), one note such
   as `../AstroPhotoAnalyzer/docs/design/thresholds.md` (the header boilerplate of a moved
   note), and the header of `../ClaudeCodeStarterKit/scripts/check-design.ps1` (the exact
   Covers grammar the gate parses).
7. `../AstroPhotoAnalyzer/docs/requirements.md` (the header and the row format),
   `../AstroPhotoAnalyzer/docs/validation.md` and `validation.json`,
   `../AstroPhotoAnalyzer/AstroPhotoAnalyzer.Tests/RepoInvariantTests.cs`, `ValidationTests.cs`,
   `TestData.cs`, `../AstroPhotoAnalyzer/docs/skills.md`, `../AstroPhotoAnalyzer/kit.json`.
8. `../ClaudeCodeStarterKit/scripts/kit-config.ps1` (every `kit.json` key),
   `check-decisions.ps1` header (the ADR header format the gate checks), `check-testdata.ps1`
   header (the manifest), `release.ps1` header (it reads `<Version>` from
   `Directory.Build.props`, which decides ADR 0004).
9. This project's `CLAUDE.md`, `README.md`, `Directory.Build.props`, the three `.csproj` files,
   and every `.cs` file under `src/` and `tests/` (about 2900 lines without the licence
   headers; the requirement rows are written from them, and the Covers sentences name them).

## Measured before planning (2026-10-02, main at 9dd0b79)

| | ImageVisualCompare |
| --- | --- |
| build Release | **0 warnings**; `Directory.Build.props` already sets `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `Nullable`, `LangVersion latest`, `PlatformTarget x64` |
| tests | 142 cases (102 test methods, 55 `InlineData` rows), green, 0.4 s; one test project, `net9.0-windows` with `UseWPF` |
| `dotnet format --verify-no-changes`, no `.editorconfig` | 9 whitespace findings, all in `src/ImageVisualCompare.Wpf/AssemblyInfo.cs` (the Visual Studio template's `ThemeInfo` attribute with its aligned comments) |
| the same with the kit's full `.editorconfig` | 12 WHITESPACE, 6 IDE0005 (unused usings), **38 IDE0011** (braces on one-line `if`s, from `csharp_prefer_braces`), 3382 ENDOFLINE; measure again in layer 1 with the `.editorconfig` of Appendix D.4 and record the counts in ADR 0003 |
| line endings | 47 of the 53 tracked files are CRLF on disk and LF in the index (`git ls-files --eol`); the root `.gitattributes` says `* text=auto eol=lf`, so git shows no difference, and a format pass with `end_of_line = lf` rewrites them on disk without any git diff |
| `CLAUDE.md` | 247 lines: "Structure" (a file map), "Invariants" (eight, each a decision with the defect that taught it), "No imaging dependency", "Memory, measured", "Testing", "Versioning", "Licensing", "Committing" |
| `docs/` | none; no ADRs, no requirements with IDs, no changelog, no validation read by tests |
| `README.md` | 152 lines, Romanian with diacritics, the user documentation; its own callout says it is the only place for it |
| version | `<Version>1.1.1</Version>` in `src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj`, with `<Copyright>` and `<PackageLicenseExpression>`; **not** in `Directory.Build.props`, where the kit's `release.ps1` reads it. History: 1.0.0 (c6b676d, 2026-09-08), 1.1.0 (861f5c1, 2026-09-09, padding), 1.1.1 (f4b91ee, 2026-09-09, dark title bars and the message dialog) |
| packages | four test packages with versions in the test csproj (`coverlet.collector` 6.0.2, `Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2); **none** in Core or Wpf; no `Directory.Packages.props`, no lock files, no `global.json` |
| comment language | English in 36 of 39 `.cs` files and in every `.xaml`; Romanian without diacritics in `src/ImageVisualCompare.Wpf/Version.cs`, `tests/ImageVisualCompare.Tests/VersionTests.cs`, `tests/ImageVisualCompare.Tests/LicenseHeaderTests.cs` (doc comments and two assertion messages) and in the two comments of the Wpf csproj; `Directory.Build.props` comments are English; every user-visible string (dialog titles, messages, the status bar) is English |
| citations of `CLAUDE.md` in code | none (`grep -rn CLAUDE.md src tests` finds nothing); the only cross-reference is `Version.cs` naming the csproj as the source of truth |
| git hooks | installed at the root of the `photo` repository (`.githooks/`, `core.hooksPath` absolute); they pick up this project as soon as `scripts/verify.ps1` exists |
| stray files | `.vs/` only, ignored by the root `.gitignore`; no `.github/`, no project `.gitignore` |
| test data | none in the repository: `TempImages` writes PNG and TIFF fixtures in six pixel formats at run time. The 5794 x 3637 pair the "Memory, measured" table was measured on was not found under `E:\Photografie` (depth 3); its figures stay as history |
| a real pair for validation | `E:\Photografie\Claude\de_printat.png` (5938 x 3888, Rgb48, 103 387 067 bytes) and `E:\Photografie\Claude\de_printat_PRINT_preview_16bit.tif` (5938 x 3888, Rgb48, 130 166 332 bytes): the same frame before and after the print-preview stretch, which is exactly the use this tool was built for. A third file, `de_printat_50x35_TIFF8.tif` (5554 x 3888, Bgr24, 20 099 182 bytes), makes a mismatched pair with the first: padding and a bit-depth difference on real files. SHA-256 of the three in Appendix D.7 |
| Claude memory | no note about this project; nothing to fold in |

## Decisions (each an ADR, accepted with the plan)

The migration's own, ADR 0001 to 0007:

1. **0001 Adopt the kit's process; `CLAUDE.md` splits into requirements, decisions and design
   notes.** The analog of AstroPhotoAnalyzer's 0001. The eight invariants and the five topical
   sections become retroactive ADRs (0008 to 0026) and design notes in `docs/design/` (moved
   unchanged); "Structure" becomes `docs/design/code-map.md`; the behaviour the README and the
   code describe becomes `docs/requirements.md` with IDs; `CLAUDE.md` is cut to the rules, the
   commands and the pointers (Appendix A). The section "Committing" ("Do not commit ... hand
   over a commit message ... a body that explains why") is superseded by the kit's rule
   "Commits and merges": the assistant commits on its `claude/` branch, one line, no body; the
   ADR quotes the old text in its Context. The rule "User documentation lives in `README.md`"
   stays a `CLAUDE.md` rule (ADR 0025).
2. **0002 The layout stays `src/` and `tests/`; the kit's values for this project are in
   `kit.json`.** The layout already matches the kit's default. Values (Appendix B): the
   solution, test filter `Category!=Validation`, code folder `src/`, test folder `tests/`, test
   data under `tests/testdata/` with `IVC_TESTDATA_LARGE` as the override nobody sets (ADR
   0005), Stryker from `tests/ImageVisualCompare.Tests/`, one publish target (the WPF
   application, self-contained win-x64, into `artifacts/<version>/` itself), the auditor may
   also read `README.md` (the user documentation, which states behaviour and is not code).
3. **0003 Gate strictness: what is on stays on; `dotnet format` adds whitespace and `IDE0005`;
   the kit's code-style rules stay off.** `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`
   and `Nullable` are already on in `Directory.Build.props` and the build has 0 warnings, so
   nothing is relaxed. The `.editorconfig` of Appendix D.4 holds whitespace rules and `IDE0005`;
   the kit's `csharp_prefer_braces` and the other style rules are not taken (38 findings on
   2026-10-02; a style pass is a task of its own). One `dotnet format` pass: the counts go in
   the ADR. Reproducibility from the kit in full: `global.json` (SDK 9.0.318),
   `Directory.Packages.props` with the four test packages, `packages.lock.json` per project,
   `Deterministic`. The rule "zero NuGet dependencies outside the test project" is unchanged and
   becomes the project rule "No imaging dependency" (ADR 0008).
4. **0004 `<Version>`, `<Copyright>` and `<PackageLicenseExpression>` move from the Wpf csproj
   to `Directory.Build.props`.** The kit's `scripts/release.ps1` reads and writes `<Version>` in
   `Directory.Build.props` and nothing else; a script is never edited in a project (the kit's
   rule "Shared folders"). So the three elements move, with their comments (translated, ADR
   0006), into the first `PropertyGroup` of `Directory.Build.props` (Appendix D.3); the csproj
   keeps only the project reference, the output type, the framework, `UseWPF` and
   `InternalsVisibleTo`. `VersionTests` follows: `AppCsproj()` becomes `BuildProps()` reading
   `Directory.Build.props`; `TheVersionComesFromTheCsproj` becomes
   `TheVersionComesFromTheBuildProps`, `TheCopyrightComesFromTheCsproj` becomes
   `TheCopyrightComesFromTheBuildProps`, `TheCsprojDeclaresTheLicence` becomes
   `TheBuildPropsDeclareTheLicence`; the assertions are the same. `Version.cs`'s header comment
   names the new file. The version number itself (1.1.1) does not change: the owner bumps it
   (the kit's rule "Version and year are the owner's"). This is the one change under `src/` and
   `tests/` in layer 1 besides the format pass, and the 142 tests plus `VersionTests` prove it.
   Alternative rejected: asking the kit for a configurable version file; `Directory.Build.props`
   is where the kit, FlatCheck and AstroPhotoAnalyzer all keep it, and this project's reason for
   the csproj (one application, one assembly that matters) holds no less there.
5. **0005 Validation on a real pair runs outside the gate, on git-ignored copies in
   `tests/testdata/large/`.** The three files of the table above are copied by the implementing
   session into `tests/testdata/large/` (git-ignored) of the worktree and of the main checkout;
   `tests/testdata/manifest.json` lists them with their SHA-256 (Appendix D.7);
   `ValidationTests` carry `Category=Validation` and are kept out of the gate by `testFilter`,
   because the files exist on one machine (a gate that passes without the data and fails with
   it is not one gate; FlatCheck ADR 0004, AstroPhotoAnalyzer ADR 0004). `IVC_TESTDATA_LARGE`
   is the override `kit-config.ps1` requires a name for; nobody sets it. Two cases: the matched
   pair and the mismatched one (section 2.4). The memory figures are guarded with a wide
   tolerance (60 MB) because they depend on the machine; the decode time is recorded and not
   guarded.
6. **0006 English everywhere, as the kit's rule "Languages" says; the three Romanian-commented
   files are translated in this task.** Unlike FlatCheck and AstroPhotoAnalyzer, where every
   comment was Romanian and translating would have meant a diff over the whole code base, here
   36 of 39 source files, every XAML file and every user-visible string are already English. The
   three exceptions (`Version.cs`, `VersionTests.cs`, `LicenseHeaderTests.cs`: doc comments, two
   header comments and two assertion messages) and the two csproj comments (which move to
   `Directory.Build.props` under ADR 0004 anyway) are translated to English in layer 1,
   comments and messages only, no code; the `Notice` array in `LicenseHeaderTests` is not a
   comment and is not touched. `README.md` stays Romanian with diacritics, as the kit's rule
   allows for READMEs. So no `CLAUDE.md` rule varies the kit's; this ADR records the choice and
   the translation.
7. **0007 The changelog starts at 1.1.1; the git hooks are not copied; root CI is deferred;
   nothing is deleted.** `CHANGELOG.md` (Appendix D.5) opens with `[Unreleased]` and one section
   `[1.1.1] - 2026-09-09` summarising what the ten commits of 2026-09-08 and 2026-09-09 built,
   with the two version steps named; earlier detail is in `git log`. The git hooks are the
   root's (the kit's ADR 0016). CI at the root of `photo` is a repository change, left for a
   task of its own (FlatCheck ADR 0007, AstroPhotoAnalyzer ADR 0006). There is no stray file to
   delete.

Retroactive, ADR 0008 to 0026, written from the sources named (each says "Retroactive:
recorded on 2026-10-02 from <source>; the decision itself dates from <commit or phase>" in its
Context, and ends with a `Source:` line). The sources are this project's `CLAUDE.md` as it is
today (section names below are its headings), the README, and the doc comments of the files
named:

| ADR | Title (short statement of the decision) | Sources |
| --- | --- | --- |
| 0008 | WIC decodes, through `IImageLoader`, the one seam, typed on `BitmapSource` and kept in the Wpf assembly; no imaging NuGet package (SkiaSharp, ImageSharp and Magick.NET compared and rejected) | CLAUDE.md "No imaging dependency", first paragraph (".NET 9 ... zero NuGet dependencies"), "Structure" (`Imaging/IImageLoader.cs`); `IImageLoader.cs` and `WicImageLoader.cs` summaries |
| 0009 | Core has no UI framework reference; `PointD`, `SizeD`, `RectD` are declared there so it needs no `WindowsBase`; the controls do no arithmetic of their own: every number on screen comes from a Core record (`RenderPlan`, `DividerLayout`, `MinimapLayout`, `ScrollBarState`) that has a test | "Structure" (the Core list and "The last four exist so that the controls do no arithmetic"); `PointD.cs`, `Geometry.cs` summaries |
| 0010 | One `TransformGroup` instance on both images and both backdrops, never two transforms configured alike | Invariant "One transform object, not two"; `CompareView.xaml.cs` constructor comment |
| 0011 | The viewport counts in physical pixels, WPF in device-independent pixels; the conversion happens in exactly two places (the input handlers and `Sync`); 100 % means one image pixel per physical screen pixel; the divider is the exception and stays in DIPs | Invariant "Physical pixels below, device-independent pixels above"; `CompareView.xaml.cs` summary; `RenderPlanner.For` comment; README "„100%" înseamnă un pixel de imagine peste un pixel fizic de ecran" |
| 0012 | The image layers are `Canvas`, not `Grid`, and each `Image` is sized explicitly to its own pixel count, never left at WPF's DPI-derived natural size | Invariants "The image layers are `Canvas`, not `Grid`" and "Images are sized explicitly in pixels"; `CompareView.xaml` comment on `AfterLayer`; `SetPair` comment |
| 0013 | The clip goes on the layer, never on the image; both layers draw over the same opaque backdrop (same brush, same size, same transform), verified with 50 %-alpha red over blue | Invariants "The clip goes on the layer" and "Both layers need the same opaque backdrop"; `CompareView.xaml` comments on the two backdrops; `UpdateOverlay` comment |
| 0014 | Padding is layout, never pixels: the canvas is the larger extent on each axis, both images anchored top-left, the backdrop fills the gap, no bitmap is copied; the user is asked before any decode, and declining is an answer, not an error | Invariant "Padding is layout, never pixels"; `ImagePairValidator.cs` (the `PadPlan` and `Inspect` comments); `ImagePairLoadResult` comment; `MainWindow.OpenPairAsync` comment; README "Dacă dimensiunile în pixeli diferă" |
| 0015 | Both headers are probed before either decode; the two decodes run one after the other, never in parallel; every expected file failure (`IOException`, `UnauthorizedAccessException`, `NotSupportedException`, `FileFormatException`, `ArgumentException`, `OutOfMemoryException`) becomes a message, never a crash | "Structure" (`Imaging/ImagePairLoader.cs`); `ImagePairLoader.LoadAsync` comments; `IsExpectedLoadFailure` comment |
| 0016 | WIC pixels are unmanaged, so a dropped pair is reclaimed by a forced collection with a finaliser pass, not by large object heap compaction; the outgoing pair is released before the incoming one is shown; the memory readout is sampled on a 2 s timer, not on view changes | "Memory, measured" (the two paragraphs after the table); `MemoryHousekeeping.ReleaseImageBuffers` comment; `MainWindow.ShowPair` and the `_memoryTimer` comment |
| 0017 | `InvariantGlobalization` is never set: WPF's font cache constructs `CultureInfo("en")` and the process dies during text layout | Invariant "Never set `InvariantGlobalization`"; `Directory.Build.props` comment |
| 0018 | Dark whatever the system theme: the application's own `MessageDialog` instead of `MessageBox`, and every `Window` asks DWM for the dark caption in its constructor (the exact chrome colour on Windows 11, the generic dark on Windows 10); the common file dialogs are the one exception and follow the system theme | Invariant "No `MessageBox`, and every window opts into a dark caption"; `MessageDialog.xaml.cs` and `DarkTitleBar.cs` summaries; "Structure" (`Services/FileDialogService.cs`); README's third paragraph |
| 0019 | The scrollbars float over the image, never in the layout, so their appearing cannot change the fit scale | Invariant "Scrollbars float over the image"; `CompareView.xaml` comment on the scrollbars |
| 0020 | The zoom model: the floor is the fit scale, or 100 % for an image smaller than the viewport; the ceiling is 1600 %; the wheel zooms continuously by 1.15 anchored at the cursor (Ctrl+wheel the same), Shift+wheel pans 140 physical pixels; the buttons step a ladder of stops that searches rather than indexes; nearest-neighbour resampling from 400 % | README "Controale" and the two bold paragraphs after it; `Viewport.MinScale`, `MaxScale`, `ZoomTo` comments; `ZoomLadder` summary; `RenderPlanner.PixelatedThreshold` comment; `CompareView.OnMouseWheel` comment and constants |
| 0021 | The divider is a fraction of the viewport width (screen space, the line belongs to the window); the handle is 26 x 56 DIPs, the line can be grabbed within 8 DIPs anywhere along it, and the divider wins over panning | `DividerModel.cs` summary and constants; `CompareView.OnMouseLeftButtonDown` comment |
| 0022 | The source format is reported, never branched on: WIC is asked with `PreservePixelFormat` so the status bar says what the file is, everything is converted to Bgra32, and an unrecognised format falls through to its bits per pixel instead of being refused | "Structure" (`ImageInfo.cs`, `WicImageLoader.cs` "Classify"); `ImageInfo` summary; `WicImageLoader.Load` and `Classify` comments; README "Ce fișiere îi dai" |
| 0023 | One source of truth for the version, read back from `AssemblyInformationalVersion` with the `+revision` suffix trimmed, no literal in code; shown in the title, the status bar and About; no `--version` flag; the owner bumps it (the kit's ADR 0029) | "Versioning"; `Version.cs` header and `Read` comments; `VersionTests` summary. The file the version lives in is `Directory.Build.props` from today (ADR 0004); the ADR says so |
| 0024 | GPLv3-or-later, `LICENSE` verbatim: the notice on every `.cs` file (not `.xaml`), kept alive by `LicenseHeaderTests` with an explicit project list; the copyright on screen for the whole run, docked right; the About dialog carries the §5(d) warranty text as a property so a test reads it without a window; `<Copyright>` is the one source, with a literal fallback | "Licensing"; `MainWindow.xaml` comment on `StatusLegal`; `VersionInfo.About` comment; `LicenseHeaderTests` summary |
| 0025 | User documentation lives in `README.md`, in Romanian, and only there; comments and `CLAUDE.md` do not restate it | CLAUDE.md line "User documentation lives in `README.md`. Do not restate it here or in comments."; README callout "Documentația de utilizator stă aici" |
| 0026 | Fixtures are generated at run time (`TempImages`, six pixel formats plus TIFF) so the real codecs are exercised without binaries in the repository; the WPF layer is untested by design and is verified by launching the application and looking, because both real defects lived there | "Testing" (all three paragraphs); `TempImages` summary |

If, while writing, two of these turn out to be one decision, merge them and renumber before
the commit (numbers are assigned once only after they are on `main`); if a source holds a
decision not listed here, add it at the end and say so under "What deviated". Twenty-six is
the expected count, not a target.

## Ground rules for the implementing session

- **Where.** Open the session in `E:\Photografie\Code\ImageVisualCompare` (the project folder
  of the main checkout, never the git root, never the desktop app's worktree option: the kit's
  ADR 0027). Before anything, `git worktree list` and `git status --short .`: another session on
  the same task, or a dirty tree, is a stop. Then make the task's worktree and branch in one
  command, from the project folder:

  ```bash
  git worktree add .claude/worktrees/Adopt-the-starter-kit-process -b claude/Adopt-the-starter-kit-process main
  ```

  Every edit, build, test and commit happens under
  `E:\Photografie\Code\ImageVisualCompare\.claude\worktrees\Adopt-the-starter-kit-process\ImageVisualCompare\`
  (the worktree holds the whole `photo` repository; `../ClaudeCodeStarterKit` inside it is the
  kit at the same `main`). Copy this plan there first, to
  `docs/plans/2026-10-02-adopt-the-starter-kit.md`. The root `.gitignore` already ignores
  `**/.claude/worktrees/`.
- **The git hooks are live from the first commit that adds `scripts/verify.ps1`:** the root
  `pre-commit` runs `scripts/verify.ps1` in this project (expect under a minute: the suite is
  0.4 s, restore and build dominate), refuses a session's commit on a `claude/` branch in the
  main checkout (hence the worktree), and `commit-msg` refuses a message that is not one line
  of at most 100 characters. The Claude Code hooks of `.claude/settings.json` are loaded at
  session start, so they are not live in the implementing session; the git hooks gate the
  commits anyway.
- **Never edit** `scripts/`, `.claude/skills/`, `.claude/agents/` or `.claude/rules/kit/` in
  this project, nor the process keys of `.claude/settings.json`: they are the kit's, copied. If
  a script cannot do what this project needs, that is a kit change, a separate task; say so.
- **Nothing is deleted, it is moved.** Every paragraph of today's `CLAUDE.md` has a destination
  in the tables of section 2.3 and 2.6. Do not improve the prose while moving it; do not
  de-duplicate between a requirement row, a design note and an ADR in this task (FlatCheck did
  that two days later, its ADR 0051; it is a task of its own). Pointers are the only new
  sentences in moved text.
- **Code changes in this task are exactly these**, and nothing else under `src/` or `tests/`:
  the `dotnet format` pass; the move of three elements out of the Wpf csproj (ADR 0004) and the
  matching edits to `VersionTests` and the `Version.cs` header comment; the translation of
  comments and assertion messages in three files (ADR 0006); the three new test files of layer
  3 and the `R###_` renames. Anything else is a checkpoint.
- **Never bump the version, never roll the year, never commit on `main`.** The branch's commits
  are the session's (the kit's ADR 0021): one line, English, at most 100 characters, saying
  why; no body, no trailers. The merge is proposed and asked at the end.
- **Languages.** English in everything, `README.md` excepted (Romanian with diacritics). A
  new source file (`RepoInvariantTests.cs`, `ValidationTests.cs`, `TestData.cs`) starts with the
  GPL notice: `LicenseHeaderTests` scans the test project and fails otherwise; copy the sixteen
  lines from any existing file.
- **Editing long files.** Do not rewrite a 200-line file through a heredoc: a Python or
  PowerShell edit script written to the scratchpad first, or the Edit tool, keeps `\n` and
  backticks intact. `CLAUDE.md` is replaced whole (Appendix A) only after every section has been
  copied into its note or ADR.
- **Subagents.** The retroactive ADRs (19) and the design notes (6) are independent writing jobs
  from fixed sources; AstroPhotoAnalyzer used subagents for its 18 ADRs. Give each subagent this
  plan's table rows, the ADR template, one finished ADR of `../AstroPhotoAnalyzer/docs/decisions/`
  as the model, and the exact source text; review every file before it is committed.
  Requirement rows are better written by the main session, in one pass, so the IDs stay
  consistent with the tests renamed in layer 3.
- **Commits.** Three on the branch, one per layer, each with the gate green (the owner may say
  otherwise at approval). Proposed messages, each under 100 characters:
  1. `Adopt the kit's gate: synced scripts, kit.json, changelog, lock files, the version in Build.props`
  2. `Split CLAUDE.md into requirements, decisions and design notes; CLAUDE.md keeps the rules`
  3. `Tie requirements to tests: RepoInvariantTests, ValidationTests on the de_printat pair, R IDs`
- **Checkpoints that need the owner** (stop and ask; everything else is decided here): the
  plan's approval (which accepts the 26 ADRs); one of the three files of Appendix D.7 missing
  from `E:\Photografie\Claude\` or with another hash; a `dotnet format` change that is not
  whitespace, a removed using or a line ending; a `CLAUDE.md` paragraph with no destination in
  the mapping tables; a validation figure that contradicts the "Memory, measured" table by more
  than its own tolerance says (report, do not adjust); the `/docs-health` report (relayed,
  nothing fixed); the merge.

## Layer 1: the gate (about an hour)

Every path below is relative to the worktree's `ImageVisualCompare/` folder.

1. **Bootstrap the shared folders.** Copy `../ClaudeCodeStarterKit/scripts/` to `scripts/`
   without the kit-only files (`rename.ps1`, `install-hooks.ps1`, `hooks/pre-commit`,
   `hooks/pre-merge-commit`, `hooks/commit-msg`); keep `hooks/claude-commit-gate.ps1`,
   `hooks/claude-track-edits.ps1`, `hooks/claude-stop-gate.ps1`. Write `kit.json` (Appendix B,
   verbatim). Then run, in the project:

   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/sync.ps1
   ```

   It mirrors `scripts/` (kit-only files are neither copied nor deleted), copies
   `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` and, since the project has no
   `.claude/settings.json`, the kit's copy of it. Then `scripts/sync.ps1 -Check` must exit 0.
2. **`.claude/settings.json`**: replace its `$comment` with the project's (Appendix D.1). Add no
   `allow` entries: the project's commands are covered by the kit's list. `sync.ps1 -Check` must
   still exit 0 (the comment is the project's).
3. **Reproducibility files and the version's move (ADR 0003, ADR 0004).**
   - `global.json`: Appendix D.2 (`9.0.318`, `latestPatch`; `dotnet --version` prints 9.0.318
     on this machine).
   - `Directory.Build.props`: replace with Appendix D.3. It gains `<Version>1.1.1</Version>`,
     `<Copyright>`, `<PackageLicenseExpression>` (moved from the csproj, comments in English)
     and the second `PropertyGroup` (`Deterministic`, `ContinuousIntegrationBuild`,
     `ManagePackageVersionsCentrally`, `RestorePackagesWithLockFile`, `RestoreLockedMode`,
     `SatelliteResourceLanguages`); it keeps every property and comment it has today.
   - `src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj`: remove the `<Version>`,
     `<Copyright>` and `<PackageLicenseExpression>` elements and their two comments; what
     remains is the project reference, `OutputType`, `TargetFramework`, `UseWPF` and the
     `InternalsVisibleTo` item.
   - `Directory.Packages.props`: new, Appendix D.6; then remove the `Version="..."` attribute
     from the four `PackageReference` lines in
     `tests/ImageVisualCompare.Tests/ImageVisualCompare.Tests.csproj` (central package
     management refuses a version in a csproj).
   - `dotnet restore ImageVisualCompare.sln` writes a `packages.lock.json` in each of the three
     project folders (Core and Wpf get one with no packages); all three are committed.
   - `.editorconfig`: Appendix D.4. Note the difference from AstroPhotoAnalyzer's: `xaml` is
     **not** in the two-space group, because this project's XAML is four-space indented and the
     editor would re-indent it; `dotnet format` does not touch XAML either way.
   - `.gitattributes`: Appendix D.8 (the kit's plus `*.tif` and `*.tiff` binary).
   - `.gitignore`: Appendix D.9.
   - `.config/dotnet-tools.json`: the kit's verbatim (Stryker 4.16.0).
     `tests/ImageVisualCompare.Tests/stryker-config.json`: the kit's with
     `"project": "ImageVisualCompare.Core.csproj"`.
4. **`VersionTests` and `Version.cs` follow the move (ADR 0004) and the three files turn
   English (ADR 0006).**
   - `tests/ImageVisualCompare.Tests/VersionTests.cs`: `AppCsproj()` becomes `BuildProps()`
     reading `Path.Combine(RepoDir(), "Directory.Build.props")`; the three test names change as
     ADR 0004 lists; the two assertion messages become `"Directory.Build.props has no <Version>"`
     and `"Directory.Build.props has no <Copyright>"`; the class and method doc comments are
     translated to English, same meaning, no new claims.
   - `src/ImageVisualCompare.Wpf/Version.cs`: the header comment names `Directory.Build.props`
     as the single source and is English; the three doc comments are translated.
   - `tests/ImageVisualCompare.Tests/LicenseHeaderTests.cs`: the class doc comment, the
     `Notice` summary, the `bin/obj` comment and the `TheScanSeesAllThreeProjects` summary are
     translated; the two assertion messages become `"Files whose first lines are not the licence
     notice:\n  "` and `"too few files scanned: "`. The `Notice` array stays byte for byte.
   - Nothing else in these files changes. `dotnet test` must still report 142 passed.
5. **`CHANGELOG.md`**: Appendix D.5.
6. **Test data (ADR 0005).** Create `tests/testdata/` with `README.md` (Appendix D.10) and
   `manifest.json` (Appendix D.7). Create `tests/testdata/large/` and copy the three files into
   it from `E:\Photografie\Claude\` (about 254 MB; `Copy-Item`, the originals are not touched),
   then run

   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1
   ```

   and expect three `ok` lines: the hashes in Appendix D.7 were computed on 2026-10-02. A `HASH`
   or `MISSING` line is a checkpoint. Make the same copy into the main checkout's
   `E:\Photografie\Code\ImageVisualCompare\tests\testdata\large\` so `/validate` works there
   after the merge (the folder shows as untracked until the merge brings `.gitignore`).
7. **Format pass.** `dotnet format ImageVisualCompare.sln`, then `git diff --stat` and read the
   diff: whitespace (the `AssemblyInfo.cs` attribute), removed usings (about six) and nothing
   else. The on-disk CRLF to LF rewrite of 47 files does not appear in the diff (the index is LF
   already). Record the file count and the kinds for ADR 0003. Anything else is a checkpoint.
8. **Gate.** `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`. Steps 0
   to 7 green: `kit files` (sync -Check), `test data manifest` (three `ok`), `decisions`
   (yellow: no folder yet), `design notes` (passes without `docs/design/`), restore, format,
   build with `-warnaserror`, 142 tests. Paste the last ten lines in the session. Also
   `scripts/release.ps1 -Version 1.1.2 -WhatIf`: it must get past the `<Version>` check and
   refuse on the branch (a task branch), which proves the move of ADR 0004 and the publish
   list of `kit.json` parse.
9. **ADRs 0001 to 0007** go into this commit (AstroPhotoAnalyzer did the same: `kit.json`,
   `.editorconfig`, `Directory.Build.props` and the CHANGELOG cite them). Copy
   `../ClaudeCodeStarterKit/docs/decisions/0000-template.md` first. Header format, letter for
   letter (`check-decisions.ps1` is a gate step from this commit on): line 1 `# NNNN. <title>`,
   line 2 blank, line 3 `Date: 2026-10-02`, line 4 `Status: accepted 2026-10-02`, line 5 blank;
   no `Supersedes` line. Sections `## Context`, `## Decision`, `## Alternatives rejected`,
   `## Consequences`; exactly one `## Decision` per file. Numbers cited as `ADR 0009` mean this
   project's; the kit's are always `the kit's ADR NNNN`. Run `scripts/verify.ps1` again: step 2
   now checks seven headers.
10. **Commit 1** on the branch (the git pre-commit hook runs verify again; the commit-msg hook
    checks the line). Report the hash.

## Layer 2: the documentation (one long session, no code)

### 2.1 `docs/requirements.md`

Format: AstroPhotoAnalyzer's exactly (the header with the rules for entries, then one table
per area, columns `ID | Status | Requirement`). Rules of this file:

- IDs in blocks of ten, one block per area, assigned once; gaps are deliberate.
- Status `accepted` for every row (the behaviour is agreed and in the code); layer 3 moves the
  rows whose tests get an `R###_` name to `implemented`. No row is `draft`.
- A row states observable behaviour: inputs, what is on screen or in the record, the constants
  **by their name in the code and their value on 2026-10-02**, and one concrete example with
  numbers. The examples are the README's (its status bar line, its controls table) and the real
  pair's (5938 x 3888, 16-bit RGB). No adjectives.
- A row says *what*; the design note of the area keeps *how* and *why*. The README paragraph
  that describes the same behaviour is the user's version and stays where it is.
- Constants are quoted as `` `Viewport.MaxScale` (16) ``.

The blocks and the rows (write them in this order; the titles below are the content each row
must carry, not its text; a row may be split in two when it would exceed about ten lines, and
two may be merged when one example covers both; keep the block):

**Opening a pair (R-001 to R-009)**, from README "Cum o pornești", "Ce fișiere îi dai",
`App.xaml.cs`, `FileDialogService.cs`, `ImagePairLoader.cs`, `MainWindow.xaml.cs`:
- R-001 no arguments: two open dialogs in turn, titles `Select image A  —  shown left of the
  divider` and `Select image B  —  shown right of the divider`, filter `Images (*.png;*.tif;*.tiff)`
  first; the second dialog opens in the first file's folder; cancelling the first prompt closes
  the application when no pair is loaded, cancelling later keeps the current pair.
- R-002 two existing paths on the command line skip the dialogs, A first; used once (`Ctrl+O`
  later goes through the dialogs); fewer than two existing paths fall through to the dialogs,
  not to an error.
- R-003 both headers are probed before any pixel is decoded; a pair whose pixel dimensions
  differ is put to the user with the exact message of `ImagePairValidator.DescribeMismatch`
  (quote it, with the example `A: de_printat.png — 5938 × 3888`, `B: de_printat_50x35_TIFF8.tif
  — 5554 × 3888`, `Both can be padded with black to 5938 × 3888,`); yes loads, no returns to the
  dialogs with no error shown; without a user to ask (no delegate) a mismatched pair is refused
  with that message.
- R-004 the canvas is the larger extent on each axis (`PadPlan`), both images anchored
  top-left, the gap is the backdrop; no bitmap is copied; example: 5938 x 3888 and 5554 x 3888
  give a 5938 x 3888 canvas with `RequiresPadding` true.
- R-005 a missing, locked, truncated, unreadable or too-large file becomes the message `Could
  not read <file name>.` followed by the exception's message, shown in the dialog titled
  `Cannot compare these files`; never a crash.
- R-006 progress: `ImageLoadProgress` 0 with A's file name, 0.5 with B's, 1 with null; the
  overlay says `Opening…`, `Decoding <file>…`, `Almost there…`; the two decodes run one after
  the other.
- R-007 a matching pair loads to an `ImagePair` whose `PixelWidth`/`PixelHeight` are the pair's
  and whose `DisplayBytes` is the sum of both Bgra32 footprints (184 695 552 for the real pair).
- R-008 opening a new pair releases the old one first (`Compare.Clear()`,
  `ReleaseImageBuffers()`), so two pairs are never resident; the window title becomes `<A>  vs
  <B>  —  Image Visual Compare 1.1.1` and the toolbar shows `<A>   vs   <B>`.

**Formats (R-010 to R-019)**, from README "Ce fișiere îi dai", `WicImageLoader.cs`,
`ImageInfo.cs`, `ImagePairValidator.cs`:
- R-010 any PNG or TIFF WIC decodes, 8 or 16 bits per channel, gray, gray+alpha, RGB, RGBA,
  indexed; every image is converted to Bgra32 and frozen; the file is not locked after the load;
  loading off the calling thread gives the same result.
- R-011 the format reported is the file's own (`PreservePixelFormat`), as `Classify` maps it:
  `Rgb48` is `16-bit RGB`, `Bgr24`/`Rgb24` `8-bit RGB`, `Gray16` `16-bit gray`, `Indexed8`
  `8-bit indexed`, `Rgba64` `16-bit RGBA`; an unknown format is `<bits per pixel>-bit`;
  `Probe` reports exactly what `Load` does. Example: a Siril "32-bit" export is a 48 bpp PNG
  reported `16-bit RGB`.
- R-012 bit depth and channel layout may differ between A and B; only the pixel dimensions are
  compared.
- R-013 `ImageInfo.DisplayBytes` is width x height x 4 (92 347 776 for 5938 x 3888);
  `Dimensions` is `5938 × 3888` (with the multiplication sign); `FileName` is the name without
  the folder.

**Zoom and pan (R-020 to R-029)**, from README "Controale" and the two bold paragraphs,
`Viewport.cs`, `ZoomLadder.cs`, `RenderPlan.cs`, `CompareView.xaml.cs`:
- R-020 the zoom floor is the fit scale (`Viewport.MinScale`), or 100 % when the image is
  smaller than the viewport; the ceiling is `Viewport.MaxScale` (16, that is 1600 %); a request
  outside is clamped.
- R-021 `Fit` (`Ctrl+0`, the button, and after every load) shows the whole image centred on the
  axis with spare room; an image smaller than the viewport is centred.
- R-022 `100%` (`Ctrl+1`, the button) is one image pixel per **physical** screen pixel: the
  render scale is `Viewport.Scale / deviceScale` (`RenderPlanner.For`), so on a 150 % display a
  2000 px image at 100 % is 1333 DIPs wide; a non-positive device scale is rejected.
- R-023 the wheel zooms by `WheelZoomFactor` 1.15 per notch, anchored at the cursor (the
  content point under the pointer stays under it); `Ctrl`+wheel does the same; `Shift`+wheel
  pans by `WheelPanPixels` 140 physical pixels.
- R-024 `+`/`−` (keys and buttons) step to the next or previous stop of `ZoomLadder.Steps`
  (quote the nineteen values 0.02 ... 16.0), anchored at the viewport centre; an off-ladder scale
  steps to its neighbour; the ends saturate.
- R-025 at `RenderPlanner.PixelatedThreshold` (4.0, that is 400 %) and above the bitmaps are
  resampled nearest-neighbour, below it `HighQuality`.
- R-026 dragging outside the divider pans; the offset is clamped so the image never leaves the
  viewport (`MaxOffsetX/Y`); the scrollbars and the minimap set the offset with the same clamp;
  `CentreOn` puts a content point in the middle.
- R-027 a window resize holds the content point at the viewport centre still; growing the
  window raises the fit scale and pulls a zoom at the floor up with it.
- R-028 the pixel under the cursor (`ContentPointInsideAt`) is the image pixel, or null on the
  margin around the image, at the far edge (x equal to the width) and without content.
- R-029 without content zoom and pan are no-ops; negative content or viewport sizes are
  rejected; screen to content and back round-trips.

**The divider (R-030 to R-039)**, from README "Controale", `DividerModel.cs`,
`DividerLayout.cs`, `CompareView.xaml.cs`:
- R-030 the divider is a fraction of the viewport width, 0.5 at start and after every new pair,
  clamped to 0..1; it stays put on screen while the image zooms and pans under it.
- R-031 `←`/`→` move it by 1 DIP, `Shift+←`/`Shift+→` by 10.
- R-032 a press within `LineGrabTolerance` (8 DIPs) of the line anywhere along its height, or
  inside the handle (`HandleWidth` 26 x `HandleHeight` 56, centred on the line), starts a
  divider drag with the `SizeWE` cursor; the divider wins over panning; the handle the layout
  draws is the handle the hit test accepts.
- R-033 image A is clipped to the left of the line (`ClipRect` from 0 to `LineX`), the handle
  is centred on the line and on the height; the layout is well formed at 0 and 1.

**Minimap and scrollbars (R-040 to R-049)**, from README "Controale", `MinimapGeometry.cs`,
`ScrollBarState.cs`, `Geometry.cs`, `MinimapView.xaml.cs`, `CompareView.xaml.cs`:
- R-040 the minimap, bottom-left, shows a thumbnail of the shared canvas (not of image A
  alone) fitted into `MaxThumbnailWidth` 190 x `MaxThumbnailHeight` 150 with the aspect ratio
  kept (`AspectFit.Fit`; degenerate inputs give an empty box); the thumbnail bitmap is rendered
  once per pair, at most 380 x 300.
- R-041 the viewport rectangle on the thumbnail is the visible region scaled, clamped to the
  thumbnail, never smaller than 2 px a side, and hidden when the whole image is on screen;
  without content nothing is drawn.
- R-042 a click or drag in the minimap centres the view on the content point under it.
- R-043 a scrollbar appears only on an axis the image overflows; its range is 0 to the maximum
  offset, its thumb the visible extent, its value the current offset clamped; the bars float
  over the image and never change the viewport size.

**Status bar and readouts (R-050 to R-059)**, from README "Ce îți arată bara de stare",
`MainWindow.xaml.cs`, `MemoryHousekeeping.cs`:
- R-050 the pair line is `<W> × <H>  ·  A <format>  ·  B <format>`; when the pair was padded,
  `<canvas> padded  ·  A <dims>  ·  B <dims>  ·  A <format>  ·  B <format>` (quote the README's
  two examples).
- R-051 the cursor readout is `<x>, <y>` as integers and `A <R> <G> <B>` / `B <R> <G> <B>`, each
  channel right-aligned in three characters, read from the Bgra32 pixel; `—` where that image
  does not reach (past its own width or height); empty when the pointer is off the image; only
  whole-pixel changes update it.
- R-052 the memory readout is `managed <N> MB, process <N> MB` (`MemorySnapshot.Describe`,
  whole megabytes), refreshed every 2 s while a pair is shown.
- R-053 the zoom readout, in the toolbar and in the badge at the top-right of the surface, is
  the scale in percent with no decimals (`100%`).
- R-054 the hint `wheel to zoom · drag the line to move it · drag to pan · ←/→ nudge · Ctrl+O
  open` takes the width left over and trims with an ellipsis; the version and copyright are
  docked right and never trimmed.

**Window and chrome (R-060 to R-069)**, from README's third paragraph and "Controale",
`MainWindow.xaml(.cs)`, `MessageDialog.xaml.cs`, `DarkTitleBar.cs`, `FileDialogService.cs`:
- R-060 the window starts maximised, titled `Image Visual Compare 1.1.1` until a pair loads;
  the comparison surface takes keyboard focus after a load and on any click on it.
- R-061 every window is dark whatever the system theme: the chrome colour is `#1B1F26`, and
  the caption is asked from DWM in the constructor (attribute 20 from build 19041, 19 from
  17763; on Windows 11, build 22000, the caption and border take the exact chrome colour as
  COLORREF `0x00261F1B`; earlier builds get the generic dark caption). The Windows open-file
  dialogs are the exception and follow the system theme.
- R-062 messages use the application's dialog, never `MessageBox`: `Show` has one `OK` button;
  `Confirm` has `Yes` and `No`, and `Escape` or the close button answer no.
- R-063 the keyboard shortcuts of the README's table, exactly (`+`/`−`, `Ctrl+0`, `Ctrl+1`,
  `←`/`→`, `Shift+←`/`Shift+→`, `Ctrl+O`); `Ctrl+O` and the `Open…` button open another pair;
  `About` shows `About Image Visual Compare 1.1.1` with `VersionInfo.About`.

**Version and licence (R-070 to R-079)**, from CLAUDE.md "Versioning" and "Licensing",
`Version.cs`, `VersionTests.cs`, `LicenseHeaderTests.cs`:
- R-070 the version is `<Version>` of `Directory.Build.props` (1.1.1 on 2026-10-02), read from
  `AssemblyInformationalVersion`, three parts, no `+revision`; `VersionInfo.Banner` is `Image
  Visual Compare 1.1.1`; there is no `--version` flag.
- R-071 the copyright is `<Copyright>` of `Directory.Build.props`, `Copyright (C) 2026 Tudor
  Carean`, shown in the status bar as `Image Visual Compare 1.1.1 · Copyright (C) 2026 Tudor
  Carean` for the whole run.
- R-072 the About text starts with the banner and the copyright, then `This program comes with
  ABSOLUTELY NO WARRANTY.`, the GPL v3-or-later sentence, and the pointer to `LICENSE` and
  `https://www.gnu.org/licenses/gpl-3.0.html` (quote the nine lines).
- R-073 every `.cs` file of the three projects starts with the sixteen-line notice (SPDX line,
  copyright, the three GNU paragraphs, `//` prefix); `.xaml` files do not carry it; `LICENSE`
  is the verbatim GPL v3 text; `<PackageLicenseExpression>` is `GPL-3.0-or-later`.

**Memory (R-080 to R-089)**, from README "Memorie", CLAUDE.md "Memory, measured",
`MemoryHousekeeping.cs`:
- R-080 the figures of the 5794 x 3637 16-bit pair (173 MB private after a headless load, 12 MB
  after release, 283 MB loaded and rendered at fit, a plateau near 570 MB private and 300 MB
  working set after several zoom levels, bounded) and the real pair's figures of
  `docs/validation.json` V-001; decoding two 108 MB files takes about 2.7 s.
- R-081 `ReleaseImageBuffers` is a forced full collection, a finaliser pass and a second
  collection, no large object heap compaction; it runs after the old pair is dropped and before
  the new one is shown.

About 45 rows. The file's header says that `CLAUDE.md` was split on 2026-10-02 (ADR 0001),
that the README is the user's version of the same behaviour (ADR 0025), and that `accepted`
is the state of the rows whose tests do not carry an ID yet, as AstroPhotoAnalyzer's does.

### 2.2 `docs/decisions/`

ADRs 0001 to 0007 exist since commit 1. Write 0008 to 0026 from the table above (subagents,
reviewed), with the same header format. A retroactive ADR's Context starts with "Retroactive:
recorded on 2026-10-02 from <file and section>; the decision itself dates from <commit>" (the
commits: a6e5f10 the base, 801bda5 the refactoring and tests, c6b676d licence and versioning,
861f5c1 padding, f4b91ee dark title bars and the message dialog, all of 2026-09-08/09) and the
file ends with `Source: <files and sections>`. Consequences name the tests that guard the
decision (`ViewportTests`, `RenderPlanTests`, `DividerModelTests`, `DividerLayoutTests`,
`ImagePairValidatorTests`, `ImagePairLoaderTests`, `WicImageLoaderTests`, `ImageInfoTests`,
`VersionTests`, `LicenseHeaderTests`) or say "none by test; launch the application" where the
decision lives in the WPF layer (0010, 0012, 0013, 0017, 0018, 0019). Where a decision has an
alternative the invariant text names (a `Grid`, a clip on the image, two transforms, LOH
compaction, `MessageBox`, an imaging package), that is the "Alternatives rejected" section;
the defect that taught it is the Context.

### 2.3 `docs/design/`

The index `README.md` first (AstroPhotoAnalyzer's text adapted: the first paragraph says the
notes were `CLAUDE.md`'s sections until 2026-10-02, ADR 0001, moved unchanged; the Covers rule;
the table with three columns `Area | Note | Read before editing`; the closing paragraph pointing
at `../decisions/`, `../requirements.md`, `../validation.md`). Every note starts with the
quote-block boilerplate AstroPhotoAnalyzer's notes use:

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "<section>". Where a paragraph
> became a decision record, its number is given beside it. **Covers:** `path`, `path`, ... . A
> change to the code this describes means this note is updated in the same task, or deleted
> with the decision it explains.

The Covers sentence: paths in backticks, relative to the project root or to `src/` or `tests/`
(`check-design.ps1` accepts both: `src/ImageVisualCompare.Core/Viewport.cs` and
`ImageVisualCompare.Core/Viewport.cs` both resolve); a folder only when the note describes its
files; no "e.g." and no other full stop before its end (the sentence ends at the first `. `);
a class or method name is allowed but is not a path. `check-design.ps1` fails on a path git
does not have, on a note missing from the index and on a note without the sentence.

| Note | Moves in, unchanged | Covers |
| --- | --- | --- |
| `code-map.md` | "Structure" whole (the Core list with its closing paragraph, the Wpf list, the tests line) | `src/ImageVisualCompare.Core/`, `src/ImageVisualCompare.Wpf/`, `tests/ImageVisualCompare.Tests/` (one line per file; the note describes every file of the three folders) |
| `rendering.md` | Invariants "One transform object, not two", "Physical pixels below, device-independent pixels above", "The image layers are `Canvas`, not `Grid`", "The clip goes on the layer, never on the image", "Both layers need the same opaque backdrop", "Images are sized explicitly in pixels", "Scrollbars float over the image" (ADRs 0010 to 0013, 0019 beside them) | `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml`, `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs`, `src/ImageVisualCompare.Wpf/Controls/MinimapView.xaml.cs`, `src/ImageVisualCompare.Core/Viewport.cs`, `src/ImageVisualCompare.Core/RenderPlan.cs`, `src/ImageVisualCompare.Core/DividerModel.cs`, `src/ImageVisualCompare.Core/DividerLayout.cs`, `src/ImageVisualCompare.Core/MinimapGeometry.cs`, `src/ImageVisualCompare.Core/ScrollBarState.cs` |
| `imaging.md` | "No imaging dependency" whole; "Memory, measured" whole (the table and the two paragraphs); Invariant "Padding is layout, never pixels" (ADRs 0008, 0014, 0015, 0016 beside them) | `src/ImageVisualCompare.Wpf/Imaging/IImageLoader.cs`, `src/ImageVisualCompare.Wpf/Imaging/WicImageLoader.cs`, `src/ImageVisualCompare.Wpf/Imaging/ImagePairLoader.cs`, `src/ImageVisualCompare.Wpf/Imaging/MemoryHousekeeping.cs`, `src/ImageVisualCompare.Core/ImagePairValidator.cs`, `src/ImageVisualCompare.Core/ImageInfo.cs` |
| `shell.md` | Invariants "Never set `InvariantGlobalization`" and "No `MessageBox`, and every window opts into a dark caption" (ADRs 0017, 0018 beside them); the "Structure" bullets for `MainWindow`, `MessageDialog`, `App.xaml.cs`, `Services/FileDialogService.cs`, `Interop/DarkTitleBar.cs`, `Theme.xaml` are repeated here by pointer to `code-map.md` | `src/ImageVisualCompare.Wpf/MainWindow.xaml`, `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs`, `src/ImageVisualCompare.Wpf/MessageDialog.xaml`, `src/ImageVisualCompare.Wpf/MessageDialog.xaml.cs`, `src/ImageVisualCompare.Wpf/Interop/DarkTitleBar.cs`, `src/ImageVisualCompare.Wpf/Services/FileDialogService.cs`, `src/ImageVisualCompare.Wpf/App.xaml.cs`, `src/ImageVisualCompare.Wpf/Theme.xaml`, `Directory.Build.props` |
| `versioning-and-licence.md` | "Versioning" whole and "Licensing" whole (ADRs 0023, 0024 beside them; the sentence "The single source of truth is `<Version>` in `src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj`" gets a bracketed note "(`Directory.Build.props` since 2026-10-02, ADR 0004)" and is otherwise unchanged) | `src/ImageVisualCompare.Wpf/Version.cs`, `Directory.Build.props`, `tests/ImageVisualCompare.Tests/VersionTests.cs`, `tests/ImageVisualCompare.Tests/LicenseHeaderTests.cs`, `LICENSE` |
| `testing.md` | "Testing" whole (the count "142 cases" gets "(on 2026-10-02; `RepoInvariantTests` and `ValidationTests` were added the same day)" beside it) | `tests/ImageVisualCompare.Tests/TempImages.cs`, `tests/ImageVisualCompare.Tests/WicImageLoaderTests.cs`, `tests/ImageVisualCompare.Tests/ImagePairLoaderTests.cs`, and the untested files it names: `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs`, `src/ImageVisualCompare.Wpf/MessageDialog.xaml.cs`, `src/ImageVisualCompare.Wpf/Interop/DarkTitleBar.cs`, `src/ImageVisualCompare.Wpf/Services/FileDialogService.cs`, `src/ImageVisualCompare.Wpf/Imaging/MemoryHousekeeping.cs` |

The first paragraph of `CLAUDE.md` is the new `CLAUDE.md`'s first paragraph. The second
(".NET 9, Windows, x64, zero NuGet dependencies") is the rule "No imaging dependency" and ADR
0008. The third ("User documentation lives in `README.md`") is the rule of the same name and
ADR 0025. "Committing" goes to ADR 0001's Context as the superseded text. Nothing else of
`CLAUDE.md` exists today, so after the six notes and the ADRs the walk of section 2.6 should
find every paragraph placed.

"Read before touching" rows for the index (the ones most likely to catch a careless edit):
`rendering.md` for anything in `Controls/` (ADRs 0010 to 0013: the shared transform, the two
pixel spaces, `Canvas`, the clip and the backdrop); `imaging.md` before adding a package or
touching the loader (ADRs 0008, 0014, 0015, 0016); `shell.md` before `Directory.Build.props`
(ADR 0017) or any new `Window` (ADR 0018); `testing.md` before claiming a WPF change works
(ADR 0026: launch it).

### 2.4 `docs/validation.md` and `docs/validation.json`

`validation.md`, under AstroPhotoAnalyzer's structure: the head paragraph (what the file is,
where the files are, the git-ignored copies, the manifest, `Category=Validation`, ADR 0005);
"How a case runs" (one kind, `pair`; layer 3, step 3 says what it measures); "Cases" (V-001 and
V-002 below); "The figures of 2026-09-08" holding the "Memory, measured" table of `CLAUDE.md`
with its two paragraphs as a quotation (the pair they were measured on is not available; the
figures are history, not a case); "The figures of 2026-10-02" filled in layer 3 from the
`MEASURED` lines; "History" with one line, 2026-10-02.

`validation.json`, `$comment` as AstroPhotoAnalyzer's adapted, two cases, `expected` left
`null` in layer 2 and filled in layer 3:

| ID | kind | before | after | what it guards |
| --- | --- | --- | --- | --- |
| V-001 | pair | `de_printat.png` | `de_printat_PRINT_preview_16bit.tif` | a matched 16-bit pair, PNG and TIFF: what the probe reports for both (5938 x 3888, 16, `Rgb`, `16-bit RGB`, 92 347 776 display bytes, the file sizes), no padding, a successful load with both bitmaps Bgra32 and frozen, 184 695 552 display bytes, the private memory the load adds and what the release gives back |
| V-002 | pair | `de_printat.png` | `de_printat_50x35_TIFF8.tif` | a mismatched pair, 16-bit against 8-bit: the probe of the second (5554 x 3888, 8, `Rgb`, `8-bit RGB`, 86 375 808), a 5938 x 3888 canvas with `RequiresPadding` true and `Dimensions` `5938 × 3888`, a successful load when the question is answered yes, 178 723 584 display bytes, the memory figures |

Field names are in layer 3, step 3; integers, booleans and strings are compared exactly, the
memory figures (megabytes, doubles) within the case's `tolerance` of 60; `decode.seconds` is
measured and printed but never put in `expected`.

### 2.5 `CLAUDE.md`

Replace it with Appendix A only when every row of the table in 2.3 is done. Target 130 to 150
lines; the hard limit is 200 (`RepoInvariantTests.ClaudeMd_StaysShort`). What leaves it and
where it goes: "Structure" -> Layout (short) and `code-map.md`; the eight "Invariants" -> the
project's Rules (one line each, citing the ADR) and `rendering.md`, `imaging.md`, `shell.md`;
"No imaging dependency" -> the rule "No imaging dependency", ADR 0008 and `imaging.md`;
"Memory, measured" -> `imaging.md` and `validation.md`; "Testing" -> the rule "Run the
application when the WPF layer changes", ADR 0026 and `testing.md`; "Versioning" -> ADR 0023,
the kit's rule "Version and year are the owner's" and `versioning-and-licence.md`; "Licensing"
-> the rule "Licence headers", ADR 0024 and the same note; "Committing" -> the kit's rule
"Commits and merges" (superseded; quoted in ADR 0001).

### 2.6 The walk

When every row of the two mapping tables above is done, open today's `CLAUDE.md` (from `git
show main:ImageVisualCompare/CLAUDE.md` in the worktree) and tick each heading and each
paragraph against its destination; a paragraph with no destination is a checkpoint, not a
deletion. Then write Appendix A over `CLAUDE.md`.

### 2.7 `docs/skills.md`

Appendix C, verbatim.

### 2.8 Pointers that cite the old places

`grep -rn -i "CLAUDE.md" src tests README.md` found nothing on 2026-10-02 besides the README's
own callout, which stays. `Version.cs`'s header is already repointed in layer 1 (ADR 0004).
`README.md`: add the section "### Dezvoltare: gate-ul, documentația și Claude Code" from
Appendix D.11 before "## Licență", and in "Compilare" add one sentence after the two commands:
"Gate-ul complet, cel pe care îl rulează hook-ul `pre-commit`, e `scripts/verify.ps1` (vezi
mai jos)." Nothing else in the README changes: it is the user documentation and the tool's
behaviour did not.

### 2.9 Gate and commit 2

`scripts/verify.ps1` green: step 2 `decisions` now checks 26 headers, step 3 `design notes`
checks the index and every Covers path. Also run `scripts/check-design.ps1 -Since main` in the
worktree once: it lists `versioning-and-licence.md` and `shell.md` (their Covers name
`Directory.Build.props`, `Version.cs`, `VersionTests.cs` and `LicenseHeaderTests.cs`, which
layer 1 changed) and `testing.md` only if a file it names changed; read each listed note
against the diff (the move of ADR 0004 and the translations) and confirm the text still holds.
Commit 2.

## Layer 3: requirements tied to tests (about an hour)

1. **`tests/ImageVisualCompare.Tests/TestData.cs`**: AstroPhotoAnalyzer's with the names
   changed: `RepoDir()` from `[CallerFilePath]` (three `GetDirectoryName` calls: the file is
   `tests/ImageVisualCompare.Tests/TestData.cs`), `LargeDir()` from `IVC_TESTDATA_LARGE` or
   `tests/testdata/large/`, `Large(name)` null when absent. GPL header; English summary.
2. **`tests/ImageVisualCompare.Tests/RepoInvariantTests.cs`**: AstroPhotoAnalyzer's, with
   `TestProjects` = `{ Path.Combine("tests", "ImageVisualCompare.Tests") }` (one project). Keeps:
   the three requirement/test links, `RequirementIds_AreUnique`, `DesignNotes_HaveTheirIndex`,
   `ClaudeMd_StaysShort` (200). `EveryValidationCase_NamesAManifestFile` reads the case keys
   `before` and `after` (both must be manifest names) instead of `image`; the manifest is
   `tests/testdata/manifest.json`. The `TestMethod` regex matches `public void R###_` and
   `public async Task R###_`, which covers the three `async Task` tests of
   `ImagePairLoaderTests` and `WicImageLoaderTests`.
3. **`tests/ImageVisualCompare.Tests/ValidationTests.cs`**: trait `Category=Validation` on the
   class; `[Theory] [MemberData(nameof(Cases))]` over the IDs of `docs/validation.json`; one
   kind, `pair`. A case:
   - resolves `before` and `after` through `TestData.Large`; either absent prints
     `NOT MEASURED <id>: <name> is absent under <dir>` and returns;
   - `var loader = new WicImageLoader();` `Probe` both, `ImagePairValidator.Inspect` the two
     infos; figures `before.width`, `before.height`, `before.bitsPerChannel`, `before.channels`
     (the enum name as a string), `before.format` (`FormatDescription`), `before.displayBytes`,
     `before.fileBytes` (`new FileInfo(path).Length`), the same seven for `after.`, then
     `pad.width`, `pad.height`, `pad.requiresPadding`, `pad.dimensions`;
   - `MemoryHousekeeping.ReleaseImageBuffers()`, `baseline = MemoryHousekeeping.Snapshot().PrivateBytes`;
     a `Stopwatch`; `await new ImagePairLoader(loader).LoadAsync(before, after, null,
     _ => Task.FromResult(true))`; figures `decode.seconds` (two decimals), `load.success`,
     `load.displayBytes`, `load.beforeIsBgra32` and `load.afterIsBgra32`
     (`Bitmap.Format == PixelFormats.Bgra32`), `load.frozen` (both `IsFrozen`),
     `memory.privateDeltaMb` = (private bytes now minus baseline) / 1 048 576 rounded to one
     decimal;
   - drop every reference to the pair (`result = null`), `ReleaseImageBuffers()`,
     `memory.privateAfterReleaseDeltaMb` = (private bytes now minus baseline) / 1 048 576, one
     decimal;
   - prints `MEASURED <id> <json>` to the test output and appends it to the file named by
     `IVC_VALIDATION_DUMP` when set; with `expected` null prints `UNMEASURED` and returns;
     otherwise compares each `expected` field as AstroPhotoAnalyzer's does (integers, booleans
     and strings exactly, doubles within `tolerance`) and fails with the full drift list.
   `AstroPhotoAnalyzer.Tests/ValidationTests.cs` is the model; its `Figures(AnalysisResult)`
   is replaced by the list above.
4. **Measure.** `dotnet test ImageVisualCompare.sln -c Release --filter "Category=Validation"`
   (two runs, a few seconds each plus the decodes). Paste the two `MEASURED` lines in the
   session, copy them into `validation.json` as `expected` **without** `decode.seconds`, fill
   "The figures of 2026-10-02" in `validation.md` (a small table: case, decode seconds, private
   MB added by the load, private MB left after the release, against the 173 MB and 12 MB of the
   2026-09-08 table), and run the filter again: two passes. A memory figure that differs from
   the old table by more than the two pairs' size difference explains (the real pair's two
   Bgra32 buffers are 176 MB against 161 MB) is a finding to report, not to adjust.
5. **Rename the tests** to `R###_...` (the method name only; nothing else cites them: no
   doc, no ADR, no comment names a test method on 2026-10-02, and the ADRs of layer 2 name test
   classes, not methods). The mapping (test -> ID; when a test pins two rows, the first):

   | File | Test | ID |
   | --- | --- | --- |
   | `ImagePairLoaderTests` | `LoadsAMatchingPair` | R-007 |
   | | `WithNoOneToAskAMismatchedPairIsStillRefused`, `AMismatchedPairIsRejectedBeforeAnyPixelsAreDecoded`, `DecliningThePaddingCostsNothingAndIsNotAnError`, `TheQuestionCarriesBothFilesAndTheResultingCanvas`, `AMatchingPairIsNeverQuestioned` | R-003 |
   | | `AcceptingThePaddingGivesACanvasBigEnoughForBothImages` | R-004 |
   | | `AMissingFileBecomesAMessageNamingIt`, `ACorruptFileBecomesAMessageRatherThanACrash` | R-005 |
   | | `ReportsProgressAsEachImageStarts`, `ImagesAreDecodedOneAfterTheOtherSoThePeakStaysLow` | R-006 |
   | `ImagePairValidatorTests` | `MatchingDimensionsNeedNoPadding`, `TheCanvasIsTheLargerExtentOnEachAxis`, `TheCanvasIsBigEnoughForEitherImageWhicheverWayRoundTheyAreGiven` | R-004 |
   | | `TheQuestionNamesBothFilesBothResolutionsAndTheResult`, `APadRequestCarriesItsOwnWording` | R-003 |
   | | `BitDepthAndChannelLayoutDoNotHaveToMatch` | R-012 |
   | `WicImageLoaderTests` | `Loads16BitRgbPng_TheFormatSirilProduces`, `ReadsTiffToo`, `EveryFormatIsNormalisedToFrozenBgra32`, `LoadingLeavesTheFileUnlocked`, `LoadAsync_ProducesTheSameResultOffTheCallingThread` | R-010 |
   | | `ReportsWhateverFormatTheFileTurnsOutToHave`, `Probe_ReportsTheSameThingAsAFullLoad` | R-011 |
   | `ImageInfoTests` | `FormatDescription_ReportsWhatWasLoaded` | R-011 |
   | | `FileName_StripsTheDirectory`, `DisplayBytes_CountsTheBgra32Footprint` | R-013 |
   | `ViewportTests` | `ZoomTo_ClampsAtTheCeiling`, `ZoomTo_WillNotShrinkAnImageBelowTheFitScale`, `ForAnImageSmallerThanTheViewport_TheFloorIsActualSize` | R-020 |
   | | `ZoomToFit_ShowsTheWholeImage`, `ZoomToFit_CentresTheImageOnTheAxisWithSpareRoom`, `AnImageSmallerThanTheViewportIsCentred` | R-021 |
   | | `ZoomToActualSize_GivesOneScreenPixelPerImagePixel` | R-022 |
   | | `ZoomTo_HoldsTheAnchoredContentPointUnderTheCursor` | R-023 |
   | | `PanBy_MovesTheImageWithTheDrag`, `PanBy_StopsAtTheImageEdges`, `MaxOffsets_DescribeTheScrollableRange`, `SetOffset_IsClampedLikeEveryOtherMovement`, `CentreOn_PutsTheRequestedPointInTheMiddle` | R-026 |
   | | `ResizingTheWindowHoldsTheCentreOfTheViewStill`, `GrowingTheWindowRaisesTheFitScaleAndPullsTheZoomUpWithIt` | R-027 |
   | | `ContentPointInsideAt_ReturnsTheImagePixelUnderTheCursor`, `ContentPointInsideAt_ReturnsNullOnTheEmptyMarginAroundTheImage`, `ContentPointInsideAt_ExcludesTheFarEdgeBecauseThereIsNoPixelThere`, `ContentPointInsideAt_WithoutContentIsNull` | R-028 |
   | | `WithoutContent_ZoomAndPanAreNoOps`, `SetContentSize_RejectsNegativeDimensions`, `ScreenAndContentSpaceRoundTrip` | R-029 |
   | `ZoomLadderTests` | all five | R-024 |
   | `RenderPlanTests` | `TheTranslationIsTheNegatedOffset`, `TheRenderScaleDividesOutTheDisplayScaling`, `OnAScaledDisplayOneHundredPercentStillMeansOneImagePixelPerScreenPixel`, `ANonPositiveDeviceScaleIsRejected` | R-022 |
   | | `DeepZoomSwitchesToShowingThePixelsThemselves` | R-025 |
   | `DividerModelTests` | `StartsCentred`, `Position_IsClampedToTheViewport`, `MoveTo_ConvertsScreenPositionToAFraction`, `MoveTo_BeyondTheEdgeClampsInsteadOfEscaping`, `Reset_ReturnsToTheCentre` | R-030 |
   | | `NudgeBy_ShiftsByAScreenAmount` | R-031 |
   | | `HitTest_GrabsTheLineAnywhereAlongItsLength`, `HitTest_IgnoresPressesThatShouldPanInstead`, `HitTest_AcceptsTheWiderHandleTargetAtTheCentre` | R-032 |
   | `DividerLayoutTests` | `TheHandleTheLayoutDrawsIsTheHandleTheHitTestAccepts` | R-032 |
   | | `TheClipCoversEverythingLeftOfTheLine`, `TheHandleIsCentredOnTheLineAndOnTheHeight`, `TheLayoutStaysWellFormedAtTheExtremes` | R-033 |
   | `GeometryTests` | all six (`AspectFit` and `RectD`) | R-040 |
   | `MinimapGeometryTests` | `TheRectangleIsTheVisibleRegionScaledOntoTheThumbnail`, `TheTwoAxesAreScaledIndependently`, `AtFitTheRectangleCoversTheThumbnailAndIsHidden`, `TheRectangleIsClampedWhereTheViewportOverhangsTheImage`, `DeepZoomStillLeavesAVisibleRectangle`, `WithoutContentThereIsNothingToDraw` | R-041 |
   | | `AClickMapsToTheContentPointUnderIt`, `CentringOnAClickPutsThatPointInTheMiddleOfTheView` | R-042 |
   | `ScrollBarStateTests` | all four | R-043 |
   | `VersionTests` | `VersionIsAThreePartNumber`, `TheVersionComesFromTheBuildProps`, `TheVersionIsNotTheFourPartOrSourceTaggedForm`, `TheBannerIsTheNameAndTheVersion` | R-070 |
   | | `TheCopyrightComesFromTheBuildProps`, `TheCopyrightNamesTheAuthorAndTheYear` | R-071 |
   | | `AboutStartsWithTheBannerAndTheCopyright`, `AboutCarriesTheWarrantyDisclaimerAndPointsAtTheLicence` | R-072 |
   | | `TheBuildPropsDeclareTheLicence` | R-073 |
   | `LicenseHeaderTests` | all three | R-073 |

   Every test method gets a name (the suite is small and every test pins a row); `TempImages`
   and the `Dispose` methods are not tests. The rows with no test (R-001, R-002, R-008, R-050
   to R-054, R-060 to R-063, R-080, R-081: the WPF layer and the figures) stay `accepted`.
6. **Statuses**: every row with an `R###_` test moves to `implemented`; `RepoInvariantTests`
   fails on either direction being off. Expect about 30 `implemented` rows of about 45.
7. **Gate.** `scripts/verify.ps1` green (now with `RepoInvariantTests`; the two validation
   cases stay out through `testFilter`; `LicenseHeaderTests` sees the three new files). Commit 3.

## Close

1. `/docs-health` once, through `general-purpose` with the text of
   `.claude/agents/docs-auditor.md` as the prompt (the agent type registers at the next
   session start). Relay the report in full; fix nothing; its gaps are the next tasks, listed
   at the end of this file as AstroPhotoAnalyzer's plan does.
2. `/wrap-up`: verify, `CHANGELOG.md` already has its `Internal` line (Appendix D.5; adjust the
   counts to what was done), requirements statuses, the 26 ADRs, this plan's "What deviated",
   README checked, `check-design.ps1 -Since main`. Then propose the merge: `git merge --no-ff
   claude/Adopt-the-starter-kit-process` in the main checkout, and ask who runs it. After the
   merge, `git branch -d` and `git worktree remove`.

## Verification

- `scripts/verify.ps1` green after each layer; the last ten lines pasted each time.
- `scripts/sync.ps1 -Check` exit 0; `scripts/check-decisions.ps1` and `check-design.ps1` exit 0
  on their own.
- `dotnet test ImageVisualCompare.sln -c Release --filter "Category=Validation"`: two cases pass
  against the recorded figures; the `MEASURED` lines are in the session.
- `wc -l CLAUDE.md` under 200; `RepoInvariantTests` green (the counts: `implemented` rows and
  `R###` IDs in the sources agree both ways).
- `git diff main...HEAD --stat -- src tests`: the format pass, the Wpf csproj (three elements
  gone), the test csproj (four `Version` attributes gone), `Version.cs` (comments),
  `VersionTests.cs` (names, one path, comments), `LicenseHeaderTests.cs` (comments, two
  messages), the three new test files, the renamed test methods, three `packages.lock.json`;
  nothing else.
- `scripts/release.ps1 -Version 1.1.2 -WhatIf` passes the version check and refuses on the
  branch (proving `Directory.Build.props` and the publish list parse).
- The application launched once from the worktree (`dotnet run --project
  src/ImageVisualCompare.Wpf -- tests/testdata/large/de_printat.png
  tests/testdata/large/de_printat_PRINT_preview_16bit.tif`) and a screenshot looked at: the
  status bar reads `5938 × 3888  ·  A 16-bit RGB  ·  B 16-bit RGB` and the version `1.1.1`
  (ADR 0026: nothing in this task touched the WPF layer's code, but the version's move touched
  what it displays).

## What deviated

Implemented on 2026-10-02 in one session, on `claude/Adopt-the-starter-kit-process`, in three
commits, one per layer, each with the gate green.

- **Ten invariants, not eight.** The former `CLAUDE.md` lists ten under "Invariants"; this plan
  said eight in its Goal, its measurements, decision 1 and Appendix D.11. The mapping tables
  already placed all ten (seven in `rendering.md`, one in `imaging.md`, two in `shell.md`), so
  only the count was wrong; ADR 0001 and the README say ten.
- **The build writes the documentation file.** With `EnforceCodeStyleInBuild` on, IDE0005 at
  warning stopped the build on `EnableGenerateDocumentationFile`: Roslyn checks unused usings at
  build only when the XML documentation file is generated. `Directory.Build.props` now sets
  `GenerateDocumentationFile` and mutes CS1591, as the kit does, and CS1573, which the
  documentation file raised six times in `ImagePairLoader.cs`, where a comment documents the one
  parameter that needs it. ADR 0003 records it.
- **The format pass touched 4 files**, not the measured "about six" usings: one space in
  `AssemblyInfo.cs` and three unused usings. The measurement had counted each diagnostic twice.
  The CRLF files of the main checkout did not matter: a fresh worktree is LF on disk.
- **ADR 0003 has the analyzer count the plan lacked**: 48 warnings at `AnalysisLevel`
  latest-recommended, 37 of them CA1707 for the underscores in test names.
- **`release.ps1 -Version 1.1.2 -WhatIf` refuses a linked worktree before it reads the
  version**, so it proved only that `kit.json` parses. Its two regexes, run directly, read
  `<Version>` 1.1.1 from exactly one place in `Directory.Build.props` and the floor 1.1.1 from
  `CHANGELOG.md`.
- **ADRs 0001 to 0007 were written by this session, 0008 to 0026 by three subagents** from the
  table, the sources and the commit each decision came from (found with `git log -S`), then
  reviewed. ADR 0001 quotes the superseded section "Committing" whole, since it is that
  section's destination.
- **The CHANGELOG line grew with each commit**, so each commit's tree describes itself; the
  final text is Appendix D.5 with the counts filled in.
- **The design notes' Covers sentences name only what each note describes** (the kit's ADR
  0032), which differs from the table in section 2.3: `rendering.md` leaves out
  `MinimapView.xaml.cs`, `DividerLayout.cs`, `MinimapGeometry.cs` and `ScrollBarState.cs`, which
  its invariants do not describe; `imaging.md` leaves out `ImagePairLoader.cs` and adds
  `CompareView.xaml.cs` and `MainWindow.xaml.cs` for the padding paragraph; `shell.md` leaves out
  `MainWindow.xaml`, `MessageDialog.xaml`, `FileDialogService.cs` and `App.xaml.cs`;
  `versioning-and-licence.md` adds `MainWindow.xaml` for the status bar; `code-map.md` names the
  two code folders and the test csproj, since the moved map has no line for `Version.cs`,
  `AssemblyInfo.cs` or the test files. The pointers to decisions are "See ADR NNNN." sentences
  after the paragraphs; the four sentences that named the csproj as the home of the version or
  the copyright carry "(`Directory.Build.props` since 2026-10-02, ADR 0004)". Two phrases of
  `rendering.md` that the code had outgrown before today carry a pointer the same way: "the
  backdrop", singular since 861f5c1 added the second (ADR 0010), and the "exactly two places" of
  the conversion, which `UpdateViewportSize` and `ZoomAtCentre` also do (ADR 0011). A script cut
  the notes out of the original `CLAUDE.md` and confirmed every other line verbatim.
- **`CLAUDE.md`'s rule "Physical pixels below, device-independent pixels above" says the
  conversion happens only in `CompareView`**, on the way in and in `Sync`, instead of Appendix
  A's "in exactly two places": the subagent that wrote ADR 0011 found it in two more places of
  the same control.
- **The README's development section sits at the end of "Compilare"**, right below the new
  sentence that points to it: placed just before "Licență", its `###` heading would have fallen
  under "Ce nu face (încă)".
- **`CLAUDE.md` says the gate takes about 20 s**, as measured, not "under a minute", and its
  style section names the documentation file. It is 140 lines.
- **`ValidationTests` is synchronous.** The load runs in a helper that is not inlined and returns
  no reference to the pair, waiting on `ImagePairLoader.LoadAsync` (which awaits with
  `ConfigureAwait(false)` throughout, so the wait cannot deadlock). Awaited in the test method,
  the pair could stay reachable through the async state machine and the reading after the
  release would measure nothing.
- **Validation figures, measured twice**: the load adds 180.2 and 181 MB on V-001, 169 MB both
  times on V-002, and leaves 2.4 or 3.1 MB and -2 or -2.2 MB after the release; the decode takes
  2.74 to 2.82 s and 1.91 to 2.04 s. `validation.json` holds the first run; xUnit ran V-002
  first both times. They agree with the 2026-09-08 table (about two Bgra32 buffers, all given
  back), so there was no finding to report.
- **102 tests renamed for 31 rows; 14 rows stay `accepted`**: the WPF layer, untested by design
  (R-001, R-002, R-008, R-050 to R-054, R-060 to R-063), and the memory figures (R-080, R-081).
  The header of `docs/requirements.md` says so. `GeometryTests.cs` holds two classes,
  `AspectFitTests` and `RectDTests`, not one called `GeometryTests`; the renames went by file and
  method, so the table above still applied. ADR 0004 now names its three renamed tests by their
  `R###_` names. `RepoInvariantTests` has seven tests: the kit's fixture-manifest test has no
  fixtures to check here, and FlatCheck's process-start test has nothing to guard.
- **Findings, left for tasks of their own** (the ADRs state what the code does, and say so):
  - `scripts/mutate.ps1` gives Stryker no test filter, so a mutation run will also run the two
    validation cases wherever they cover a mutant, seconds each with the real pair present. Not
    measured: no mutation run was made. A kit question, since every adopter has its validation
    cases in its test project;
  - the summary of `Viewport.cs` says screen space is device-independent pixels; the code and
    ADR 0011 say physical;
  - the README and the comment in `MainWindow.ShowPair` say two pairs are never in memory at
    once; while a new pair decodes, the current one is still held and on screen (R-008,
    ADR 0016);
  - the comment in `ImagePairLoader.LoadAsync` calls the transient decode buffer the largest
    allocation, while the 2026-09-08 measurement found that WIC streams the conversion with no
    transient spike (ADR 0015);
  - the comment in `DarkTitleBar.ApplyTo` says the colour call "simply fails" on Windows 10; the
    code makes it only from build 22000 (ADR 0018);
  - the summary of `MemorySnapshot.PrivateBytes` says "private working set"; the value is
    `PrivateMemorySize64`, private bytes (R-052);
  - the README says the arrow keys move the divider by 1 or 10 px; they move it by 1 or 10 DIPs,
    the same only at 100 % display scaling (R-031, ADR 0011); its padded status bar example has
    one space around each `·`, the code two (R-050); it says nearest-neighbour starts "peste
    400%", the code at 400 % itself (R-025, ADR 0020);
  - About says the full licence "is in the LICENSE file next to this program", and nothing puts
    it there: no csproj item copies `LICENSE` to the build or the publish output. This one is a
    defect against GPLv3 §5(d), not wording, and the most useful of the tasks to follow;
  - the former `CLAUDE.md` said all four licensing points "are pinned by tests"; the copyright
    docked right has no test, and `LicenseHeaderTests` checks four markers of `LICENSE`, not its
    exact text (ADR 0024);
  - the summary of `TempImages` says it writes "every format the loader claims to handle"; the
    tests use six PNG formats and one TIFF, and `Classify`'s fall-through for an unknown format
    has no test (ADR 0022); `ColorChannels.GrayAlpha` has a description and a test, and
    `Classify` never returns it.
- **The audit corrected three statements of this task's own text**: the `$comment` of
  `docs/validation.json` said the tolerance applies to "every numeric field that is not an
  integer", while V-002's memory figures are written as integers and compared within it (the
  measured kind decides); `docs/skills.md` named the worktree's `testdata/large/` for
  `tests/testdata/large/`; R-041 gave the minimap rectangle's floor in pixels, where the code
  works in the minimap's DIPs. Everything else the audit found is below, for the owner to pick.
- **The commit of layer 3 was amended once**, after the audit, to carry its report and these
  three corrections, so the branch keeps the three commits the owner asked for.

## The docs-health report of 2026-10-02

Run once at the end, through `general-purpose` with the text of `.claude/agents/docs-auditor.md`
(the agent type registers at the next session start). Its list of files opened holds nothing
under `src/`; the audit is valid. The small change it planned from the docs alone: the A/B toggle
the README lists under "Ce nu face (încă)", a key that switches the surface between image A
alone, image B alone and the split view.

Verdict: **mostly**. From the docs alone a fresh model can follow the process and change Core
with a test; a change in the WPF layer, such as the toggle, depends on how `CompareView` and
`MainWindow` work and talk to each other, which no design note describes, and the auditor had to
piece it together from ADRs 0009 to 0014 and 0019 to 0021 and still guess the control's members.

Gaps (what is missing, where it should go, effort):

1. How the WPF layer works: `CompareView`'s handlers, `Viewport` and `DividerModel`, then
   `Sync`, `UpdateOverlay`, `UpdateScrollBars` and the minimap; the control's public members;
   how the zoom percentage (R-053) and the cursor pixel (R-051) reach `MainWindow`; `MainWindow`'s
   open, confirm, decode and retry loop and how it builds the status bar. Today these are
   fragments in ADRs that change only in the kit's ADR 0024's three ways.
   `docs/design/rendering.md` and `shell.md`, with Covers naming those files. A session.
2. `implemented` rows with parts no test guards: R-023 (the wheel factor and pan step), R-031
   (the nudge sizes), R-003 and R-005 (dialog titles, the dialogs reopening), R-006 (the overlay
   texts), R-032 (the cursors), R-040 (the thumbnail from A, 380 x 300), R-073 ("verbatim",
   where `LicenseHeaderTests` checks four markers). Split each into a Core row and a window row.
   `docs/requirements.md`. An hour.
3. The notes moved unchanged are stale or correct themselves in brackets: `testing.md`'s "142
   cases", the csproj sentences of `versioning-and-licence.md`, "exactly two places" in
   `rendering.md`; `imaging.md` never names `ImagePairLoader` although the index sends
   `Imaging/` there; `code-map.md` has no line for `Version.cs`, `AssemblyInfo.cs` or a test
   file, and the types the rows name (`ImagePair`, `ImageLoadProgress`, `ImagePairLoadResult`,
   `MemorySnapshot`, `MinimapLayout`, `ScrollBarStates`) are mapped to no file. `docs/design/`.
   An hour, a session with the de-duplication against ADRs that `docs/design/README.md` defers.
4. Covers sentences wider than the kit's ADR 0032: `code-map.md` covers
   `src/ImageVisualCompare.Wpf/` and skips files in it; `testing.md` covers five `src/` files it
   only says are untested. Minutes.
5. The test infrastructure is described only in this plan: the patterns `RepoInvariantTests`
   reads rows and tests with, `ClaudeMd_StaysShort`'s 200 lines, what `DesignNotes_HaveTheirIndex`
   checks (and whether the kit's ADR 0031 retires it now that `check-design.ps1` checks the
   index), why `ValidationTests` is synchronous. `docs/design/testing.md`, with Covers naming
   `RepoInvariantTests.cs`, `ValidationTests.cs` and `TestData.cs`. An hour.
6. A home for the open findings, which live only under "What deviated" above: `draft` rows (for
   example `LICENSE` beside the executable) and a pointer from `CLAUDE.md`. Minutes.
7. Rows without a numeric example: R-027, R-028, R-029, R-033, R-041, R-042; R-010 gives no
   size; the offsets of R-026 and R-043 are content pixels, and every example is at 100 %, where
   the units coincide. `docs/requirements.md`. Minutes.
8. The toolbar's `Navigation` (R-060) is a label, and nothing says so; the README's "Controale"
   does not list it. R-060, `README.md`. Minutes.
9. Choices stated without a recorded reason: the minimap thumbnail drawn from image A only
   (R-040, which matters for any A/B mode), the shortcuts working only while the surface has the
   focus and `Ctrl+O` window-wide (R-063), x64 only, the window starting maximised (the README
   gives the reason). `docs/decisions/0027` on. An hour.
10. Kit rules restated instead of named: `CLAUDE.md` "Layout" names the shared folders by the
    kit's ADR numbers instead of its rule "Shared folders"; the rule "User documentation lives in
    `README.md`" overlaps the kit's rule "Languages" without naming it. Minutes.
11. Which `CLAUDE.md` wins in a worktree: from `ImageVisualCompare/.claude/worktrees/...` Claude
    Code loaded both the worktree's `CLAUDE.md` and the main checkout's, which until the merge is
    the old one ("Do not commit", the version in the Wpf csproj). `CLAUDE.md` "Workflow" names
    only `E:\Photografie\Code`. Minutes here; a session in the kit, since every adopter's
    worktrees live under its own folder.
12. "Overlay" names two things: `CompareView.UpdateOverlay`'s divider overlay and R-006's
    loading overlay over the whole window, whose file no document gives. `code-map.md`. Minutes.

Top three, in the auditor's words: no design note for how the WPF layer works; `implemented`
rows with unguarded parts, where a dialog title or the wheel factor can change with the gate
green; the moved notes, whose every fact has to be checked against a row and an ADR before it
can be trusted.

What is good: the rows can be tested as written (exact strings, constants with their values,
worked numbers that check out: R-021's 28 px, R-024's 152 % to 200 % and 150 %, R-007's 184 695 552
bytes), all 31 `implemented` IDs appear in the test list and no test carries an unknown one; the
ADRs are honest about their guards and record the stale comments instead of hiding them;
`CLAUDE.md` is short, cites an ADR per rule and routes each area to its note.

None of the gaps is closed in this task; each is a task of its own (`/docs-health` step 4).

---

## Appendix A: the new `CLAUDE.md`

The text below is the target; the implementing session adjusts a path, a count or a timing
that turned out otherwise, and nothing else.

````markdown
# ImageVisualCompare

WPF before/after slider for two variants of the same astro processing: image A left of a
draggable vertical line, image B right of it, both at the same zoom and pan, with the RGB of
the pixel under the cursor read from both. Built to see a difference of one or two levels in a
dark field, where the eye cannot. One WPF application over a pure core library, .NET 9,
Windows, x64, **no NuGet package** outside the test project: decoding is WIC, the codec WPF
uses anyway (ADR 0008). Everything on screen is dark, whatever the system theme (ADR 0018).

## Commands

- Gate (what pre-commit, pre-merge-commit and the Claude commit hook run, under a minute):
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build ImageVisualCompare.sln -c Release --nologo` (Windows only: WPF)
- Test: `dotnet test ImageVisualCompare.sln -c Release --nologo --filter "Category!=Validation"`
- One test: `dotnet test ImageVisualCompare.sln --filter "FullyQualifiedName~R020"`
- Format: `dotnet format ImageVisualCompare.sln` (check only: `--verify-no-changes`)
- Run: `dotnet run --project src/ImageVisualCompare.Wpf -- [A.png B.png]`; without the two
  paths the file dialogs ask (R-001, R-002)
- Figures on the real pair (seconds per case, not in the gate): `/validate`, or
  `dotnet test ImageVisualCompare.sln -c Release --filter "Category=Validation"`; the files are
  git-ignored copies in `tests/testdata/large/` (ADR 0005)
- Test data hashes after an intentional change: `scripts/check-testdata.ps1 -Update`
- Design notes a branch touched (the index and Covers paths are a gate step):
  `scripts/check-design.ps1 -Since main`
- Mutation score of Core (minutes, not in the gate): `scripts/mutate.ps1 [-Mutate "**/Viewport.cs"]`
- Release (verify, version in `Directory.Build.props`, CHANGELOG, publish; no commit, no tag):
  `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`
- Take the kit's shared folders and settings keys after a change there: `scripts/sync.ps1`
  (`-Check` only compares)

## Layout

- `src/ImageVisualCompare.Core/` - `net9.0`, no UI framework reference: zoom and pan
  (`Viewport.cs`, `ZoomLadder.cs`), the divider (`DividerModel.cs`), the canvas of a pair
  (`ImagePairValidator.cs`), what a file turned out to be (`ImageInfo.cs`), and the layout
  records the controls assign without arithmetic of their own (`RenderPlan.cs`,
  `DividerLayout.cs`, `MinimapGeometry.cs`, `ScrollBarState.cs`, `Geometry.cs`, `PointD.cs`).
  All of it unit tested.
- `src/ImageVisualCompare.Wpf/` - `net9.0-windows`, the application: `Imaging/` (the decoder
  seam and WIC behind it, the pair loader, memory housekeeping), `Controls/` (the comparison
  surface, the minimap), `MainWindow`, `MessageDialog`, `Interop/DarkTitleBar.cs`,
  `Services/FileDialogService.cs`, `Theme.xaml`, `Version.cs`.
- `tests/ImageVisualCompare.Tests/` - `net9.0-windows` with `UseWPF`: Core, the imaging layer
  on the real WIC codecs (fixtures generated at run time by `TempImages`),
  `RepoInvariantTests`, `ValidationTests` (trait `Category=Validation`), `LicenseHeaderTests`,
  `VersionTests`. `tests/testdata/manifest.json` hashes the real pair, which stays outside git.
- `Directory.Build.props` - `<Version>` and `<Copyright>`, the single place for both (ADR
  0004), and the strictness every project builds with.
- `kit.json` - what the kit's scripts, skills and agent need to know about this project.
  `docs/skills.md` - this project's additions to the kit's skills. `scripts/`,
  `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` and the process keys of
  `.claude/settings.json` are the kit's, the same here (ADR 0002; the kit's ADRs 0017, 0018,
  0028).
- `docs/requirements.md` - the behaviour, with IDs. `docs/decisions/` - ADRs. `docs/validation.md`
  + `.json` - figures on the real pair. `docs/design/` - the explanations, per area, read on
  demand. `docs/plans/` - approved plans. `README.md` - the user documentation, Romanian.
- The process rules are the kit's, in `.claude/rules/kit/process.md`; this project's own are in
  this file. The reasons behind the process are in `GHID.md` in the kit folder
  `ClaudeCodeStarterKit` beside this project in the `photo` repository (for people, not loaded).
- This folder lives inside the `photo` monorepo (`E:\Photografie\Code`); the git root is the
  parent. Sample images never enter the repository: the root `.gitignore` covers `*.tiff` but
  not `*.tif` or `*.png`, so keep them out by hand.

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are ImageVisualCompare's own; one that extends
or varies a kit rule names it.

- **No imaging dependency** (extends the kit's rule "Packages"). Core and the application
  reference no NuGet package; WIC decodes, and `IImageLoader` is the one seam a replacement
  decoder would fill. SkiaSharp, ImageSharp and Magick.NET were compared and rejected; read
  `docs/design/imaging.md` before proposing one again (ADR 0008).
- **Core has no UI.** `ImageVisualCompare.Core` references no UI framework; its own `PointD`,
  `SizeD` and `RectD` exist so it never needs `WindowsBase`. The controls do no arithmetic: a
  number on screen comes from a Core record and has its test there (ADR 0009).
- **One transform object, not two.** The same `TransformGroup` instance on both images and both
  backdrops; the synchronisation is structural (ADR 0010).
- **Physical pixels below, device-independent pixels above.** `Viewport` counts physical
  pixels; the conversion happens in exactly two places, `CompareView`'s input handlers and
  `Sync`. The divider stays in DIPs (ADR 0011).
- **The image layers are `Canvas`, sized in pixels, clipped on the layer, over one backdrop.**
  Never a `Grid`, never the natural size, never a clip on the `Image`, never a layer without
  the shared opaque backdrop (ADR 0012, ADR 0013).
- **Padding is layout, never pixels.** A mismatched pair is compared on the larger canvas,
  both anchored top-left, the backdrop fills the gap, and the user is asked before any decode
  (ADR 0014).
- **Formats are reported, never branched on.** Everything is Bgra32 after the load; the status
  bar says what the file was (ADR 0022).
- **Never set `InvariantGlobalization`.** WPF's font cache dies on it during text layout, and no
  unit test can catch it (ADR 0017).
- **No `MessageBox`; every `Window` calls `DarkTitleBar.Apply` in its constructor.** The
  application's own `MessageDialog` says and asks; the file dialogs are the one thing that
  follows the system theme (ADR 0018).
- **Run the application when the WPF layer changes.** Both real defects lived in the untested
  layer and were found on a screenshot; a green suite says nothing about `Controls/`,
  `MainWindow` or `Directory.Build.props` (ADR 0026).
- **Licence headers.** Every `.cs` file starts with the GPL notice, `.xaml` files do not;
  `LicenseHeaderTests` fails otherwise, and its project list is explicit, so adding a project
  is a decision (ADR 0024).
- **User documentation lives in `README.md`, and only there.** Code comments explain why the
  code is the way it is; the README explains how to use the tool, in Romanian; `docs/` explains
  what it does and how, for whoever maintains it (ADR 0025).

## Workflow

The kit's workflow in `.claude/rules/kit/process.md` applies. ImageVisualCompare adds:

- `/wrap-up` checks `README.md`, the user documentation, and launches the application when a
  file under `src/ImageVisualCompare.Wpf/` changed (the rule "Run the application").
- A release is tagged `imagevisualcompare-vX.Y.Z`, the name `scripts/release.ps1` prints; it
  publishes the application self-contained for win-x64 into `artifacts/X.Y.Z/`.
- The main checkout of the kit's rule "Worktrees" is the shared checkout `E:\Photografie\Code`.
  A worktree has no copy of the real pair; a task that runs `/validate` copies the three files
  into the worktree's `tests/testdata/large/` first (ADR 0005).

## Read before touching

`docs/design/README.md` maps every area to its note. The ones most likely to catch a careless
edit:

- Anything in `Controls/`: `docs/design/rendering.md` and ADR 0010 to ADR 0013, ADR 0019 (the
  shared transform, the two pixel spaces, `Canvas`, the clip, the backdrop, the floating bars).
- Anything in `Imaging/`, or a package: `docs/design/imaging.md` and ADR 0008, ADR 0014 to
  ADR 0016 (probe first, decode sequentially, release through the finaliser pass).
- `Directory.Build.props`, a new `Window`: `docs/design/shell.md` and ADR 0017, ADR 0018.
- `Version.cs`, `LicenseHeaderTests`: `docs/design/versioning-and-licence.md`, ADR 0023, ADR
  0024.
- A file you have not opened before: `docs/design/code-map.md`.

## Style

- `.editorconfig` holds whitespace rules and `IDE0005`; `dotnet format` enforces them. Warnings
  are errors, `EnforceCodeStyleInBuild` and `Nullable` are on, in `Directory.Build.props` (ADR
  0003). The kit's code-style rules (braces, `var`) are not enabled: 38 findings on 2026-10-02;
  a style pass is a task of its own.
- Fix, do not suppress, unless the suppression carries a comment with the reason.
````

## Appendix B: `kit.json`

```json
{
  "$comment": "What the shared scripts, skills and agent need to know about ImageVisualCompare. With docs/skills.md, the only file edited here to use them; scripts/, .claude/skills/, .claude/agents/ and .claude/rules/kit/ are byte-identical with ../ClaudeCodeStarterKit's (scripts/sync.ps1 keeps them so; the kit's ADRs 0017, 0018 and 0028; ADR 0002). The layout is the kit's default, src/ and tests/. The validation cases on the real pair stay out of the gate, and the files are git-ignored copies in tests/testdata/large/; IVC_TESTDATA_LARGE is the override kit-config.ps1 requires a name for, and nobody sets it (ADR 0005). See scripts/kit-config.ps1 for every key.",
  "solution": "ImageVisualCompare.sln",
  "testFilter": "Category!=Validation",
  "codeFolders": ["src/"],
  "codeFiles": [],
  "testFolders": ["tests/"],
  "testData": {
    "folder": "tests/testdata/",
    "largeEnv": "IVC_TESTDATA_LARGE"
  },
  "mutate": {
    "testProject": "tests/ImageVisualCompare.Tests/"
  },
  "release": {
    "publish": [
      {
        "project": "src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj",
        "output": "",
        "args": ["-r", "win-x64", "--self-contained"]
      }
    ]
  },
  "audit": {
    "alsoRead": ["README.md"]
  },
  "kit": "../ClaudeCodeStarterKit"
}
```

## Appendix C: `docs/skills.md`

````markdown
# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (the kit's
ADR 0018; ADR 0002). What ImageVisualCompare needs on top of them is here, one section per
skill; each skill reads this file before its first step, and each section says at which step it
applies. Values (the solution, the folders, the publish list, what the auditor may also read)
are in `kit.json`, not here.

## /new-feature

- At step 1: a requirement about the surface names the input (wheel, key, drag, button) and
  the unit it is measured in (physical pixels, DIPs or image pixels: the rule "Physical pixels
  below, device-independent pixels above"); one about a file names what the status bar shows.
- At step 2: every test lives in `tests/ImageVisualCompare.Tests`. A number the surface puts on
  screen gets its test on the Core record that computes it (`RenderPlan`, `DividerLayout`,
  `MinimapLayout`, `ScrollBarState`), not on the control (the rule "Core has no UI"). A format
  or a loader change gets a fixture from `TempImages`, never a binary file. A figure measured
  on the real pair is a field in a case of `docs/validation.json`.
- At step 5: `ImageVisualCompare.Core` first, then the control that assigns the record. A
  change in `Controls/` or `MainWindow` is not done until the application was launched on the
  real pair and a screenshot looked at (the rule "Run the application"; ADR 0026).
- At step 6: numeric code is `src/ImageVisualCompare.Core/` and `src/ImageVisualCompare.Wpf/Imaging/`.
- At step 8: `README.md` is in Romanian, with diacritics; its "Controale" table lists every
  input and its "Ce îți arată bara de stare" section every readout.

## /wrap-up

- At step 2: numeric code is `src/ImageVisualCompare.Core/` and
  `src/ImageVisualCompare.Wpf/Imaging/` (a memory figure that moved is a finding).
- At step 9: a change under `src/ImageVisualCompare.Wpf/` also launches the application once,
  on the real pair, before the commit (the rule "Run the application").

## /validate

- At step 1: the files are `de_printat.png`, `de_printat_PRINT_preview_16bit.tif` and
  `de_printat_50x35_TIFF8.tif`, git-ignored copies in `tests/testdata/large/` of the owner's
  files in `E:\Photografie\Claude` (ADR 0005); `tests/testdata/manifest.json` holds their
  hashes. A worktree has no copy: copy the three files into the worktree's `testdata/large/`
  first, or every case prints `NOT MEASURED`. A case probes and decodes a 23 MP pair, a few
  seconds.
- At step 3: the table's second column is the case's two files, not a fixture; `decode.seconds`
  is printed and never guarded; the memory figures are megabytes with a 60 MB tolerance, so a
  drift there is a machine before it is a regression.

## /release

- At step 5: the application is published self-contained for win-x64 into `artifacts/X.Y.Z/`
  itself; the tag is `imagevisualcompare-vX.Y.Z`.
- At step 7: the README's "Compilare" section names the build and test commands; `artifacts/`
  is what a release produces.

## /mutate

- At step 1: Core is small (eleven files, 142 tests on 2026-10-02), so a whole-core run is
  reasonable; `scripts/mutate.ps1 -Mutate "**/Viewport.cs"` is the shape for one file. No run
  has been measured yet; write the first one's time and score here.

## /docs-health

(none; `audit.alsoRead` in `kit.json` lets the auditor read `README.md`, the user
documentation, which states behaviour and is not code)
````

## Appendix D: the small files

### D.1 `.claude/settings.json`, the `$comment`

AstroPhotoAnalyzer's comment with "AstroPhotoAnalyzer's" replaced by "ImageVisualCompare's"
(read it in `../AstroPhotoAnalyzer/.claude/settings.json`; it ends with "(ADR 0002; the kit's
ADRs 0017, 0018 and 0028)", which holds here too). The rest of the file is the kit's and is
not edited.

### D.2 `global.json`

```json
{
  "sdk": {
    "version": "9.0.318",
    "rollForward": "latestPatch"
  }
}
```

### D.3 `Directory.Build.props`

```xml
<Project>

  <!-- Shared by the three projects. The single place for the version, the copyright and the
       strictness. Do not move <Version> or <Copyright> back into a csproj: VersionTests reads
       this file, and scripts/release.ps1 writes <Version> here (ADR 0004). -->
  <PropertyGroup>
    <!-- The owner bumps the version by hand, per feature or fix (ADR 0023; the kit's ADR 0029).
         MSBuild carries it into AssemblyInformationalVersion, where VersionInfo reads it. -->
    <Version>1.1.1</Version>

    <!-- <Copyright> reaches AssemblyCopyrightAttribute, where VersionInfo.Copyright reads it for
         the status bar. The program is GPLv3-or-later; the full text is in LICENSE at the
         project root (ADR 0024). -->
    <Copyright>Copyright (C) 2026 Tudor Carean</Copyright>
    <PackageLicenseExpression>GPL-3.0-or-later</PackageLicenseExpression>

    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <!-- A 21 MP pair needs ~300 MB at peak; a 32-bit process cannot host that. -->
    <PlatformTarget>x64</PlatformTarget>
    <!-- Do not set InvariantGlobalization here. WPF's font cache builds a CultureInfo("en")
         while working out typography availability, which throws in invariant mode and takes
         the whole application down the moment certain text is laid out (ADR 0017). -->
  </PropertyGroup>

  <!-- Reproducibility (ADR 0003): the same sources, the same bytes; every package version once,
       in Directory.Packages.props; the full graph in packages.lock.json, which CI restores in
       locked mode. -->
  <PropertyGroup>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <RestoreLockedMode Condition="'$(CI)' == 'true'">true</RestoreLockedMode>
    <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
  </PropertyGroup>

</Project>
```

### D.4 `.editorconfig`

```ini
# Whitespace rules, enforced by `dotnet format --verify-no-changes` in scripts/verify.ps1.
# EnforceCodeStyleInBuild, Nullable and warnings-as-errors are on in Directory.Build.props; the
# kit's code-style rules (braces, var) are deliberately not enabled here (ADR 0003): 38
# findings on 2026-10-02, and a style pass over the code base is a task of its own.
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

# XAML is not in this group: this project's XAML is four-space indented.
[*.{json,yml,yaml,props,targets,csproj,sln}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false

[*.{ps1,sh}]
end_of_line = lf

[*.cs]
# unnecessary using
dotnet_diagnostic.IDE0005.severity = warning
```

### D.5 `CHANGELOG.md`

```markdown
# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Internal

- The project follows the ClaudeCodeStarterKit process (ADR 0001 to 0007): `scripts/verify.ps1`
  is the gate (the kit's scripts, skills, agent and rules synced, `kit.json`, the test data
  manifest, `dotnet format`, warnings as errors, the tests), package versions live once in
  `Directory.Packages.props` with lock files, and `<Version>` and `<Copyright>` moved from the
  Wpf csproj to `Directory.Build.props`, where the kit's release script reads them. `CLAUDE.md`
  was split into `docs/requirements.md` (about 45 rows with IDs), `docs/decisions/` (26 ADRs,
  19 of them retroactive) and `docs/design/` (6 notes, moved unchanged); `CLAUDE.md` holds only
  the project's rules. `RepoInvariantTests` ties `implemented` rows to tests named `R###_...`,
  and `ValidationTests` checks two cases on a real 23 MP pair outside the gate. The comments of
  three files turned English. No behaviour changed; one `dotnet format` pass over N files.

## [1.1.1] - 2026-09-09

The state of the tool when the changelog was started, built on 2026-09-08 and 2026-09-09 in
ten commits:

- 1.0.0 (2026-09-08): the slider over two identically sized images, one shared transform, zoom
  anchored at the cursor with a ladder of stops, the divider with its handle and arrow-key
  nudges, the minimap, floating scrollbars, the pixel readout from both images, WIC decoding of
  any PNG or TIFF with the source format reported, the memory readout and the release of the
  outgoing pair; the GPL notice on every source file, the version and copyright in the status
  bar, the About dialog.
- 1.1.0 (2026-09-09): a pair of different pixel dimensions is compared on the larger canvas,
  both anchored top-left, after the user agrees; nothing is decoded before the answer.
- 1.1.1 (2026-09-09): dark title bars through DWM on every window, and the application's own
  message dialog in place of `MessageBox`.

No earlier version was released; the ten commits are in `git log`.
```

### D.6 `Directory.Packages.props`

```xml
<Project>
  <!-- Central Package Management: every package version lives here, once (ADR 0003). The rule
       "No imaging dependency" (CLAUDE.md, ADR 0008): the core and the application reference no
       package at all; the test project has the four below and nothing else. No new package
       without asking (the kit's rule "Packages"), and every package gets a "why" here. -->
  <ItemGroup>
    <!-- Test host and runner (xUnit 2: no dynamic skip, which is why an absent large file returns
         early with a message instead of skipping). -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>
</Project>
```

### D.7 `tests/testdata/manifest.json`

The hashes were computed on 2026-10-02 with `Get-FileHash -Algorithm SHA256` on the files in
`E:\Photografie\Claude\`; `check-testdata.ps1` must print `ok` for all three after the copy.

```json
{
  "$comment": "Every test data file outside git: three images under tests/testdata/large/, git-ignored, copied by hand from E:/Photografie/Claude (ADR 0005); IVC_TESTDATA_LARGE may name another folder, nobody sets it. Paths are relative to that folder. scripts/check-testdata.ps1 verifies presence and SHA-256 (a missing large file is a warning locally); -Update rewrites this file with PowerShell JSON layout. The cases in docs/validation.json name these files. ImageVisualCompare has no repo fixtures: TempImages writes them at run time.",
  "files": [
    {
      "name": "de_printat.png",
      "location": "large",
      "requiredInCi": false,
      "sha256": "490774dc90d723c831dff4a0b5e36e51b3579f602a1483b5b13560c29b21e9d4",
      "source": "the owner's finished Milky Way core, 5938 x 3888, 16-bit RGB PNG (Rgb48), exported from Siril 2026-08-30; image A of both cases",
      "demonstrates": "docs/validation.json V-001, V-002: a 16-bit PNG probed and decoded; R-010, R-011, R-013"
    },
    {
      "name": "de_printat_PRINT_preview_16bit.tif",
      "location": "large",
      "requiredInCi": false,
      "sha256": "aaaba06c8fb6dd6200329715f0bf38ff57a23aa4afb4774690c86e82b696ad01",
      "source": "the same frame after the print-preview stretch, 5938 x 3888, 16-bit RGB TIFF (Rgb48), 2026-09-20; image B of V-001",
      "demonstrates": "docs/validation.json V-001: a matched pair, PNG against TIFF, no padding; R-007"
    },
    {
      "name": "de_printat_50x35_TIFF8.tif",
      "location": "large",
      "requiredInCi": false,
      "sha256": "a92f02df49b142be6747042e9628ff2e71dbe388db2a100daa8e17edba3d673e",
      "source": "the same frame cropped to 50:35 and exported as 8-bit TIFF (Bgr24), 5554 x 3888, 2026-09-19; image B of V-002",
      "demonstrates": "docs/validation.json V-002: a mismatched pair, 16-bit against 8-bit, padded to 5938 x 3888; R-003, R-004, R-012"
    }
  ]
}
```

### D.8 `.gitattributes`

```gitattributes
# Normalise line endings in the repo. The sources are LF and .editorconfig keeps them so
# (ADR 0003; the photo repository's root .gitattributes says the same, the kit's ADR 0014).
* text=auto eol=lf
*.sh text eol=lf
*.png binary
*.ico binary
*.tif binary
*.tiff binary
```

### D.9 `.gitignore`

```gitignore
# In addition to the repository root's .gitignore (bin/, obj/, .vs/, **/.claude/worktrees/).
.verify-stamp
artifacts/
TestResults/
StrykerOutput/
# The real pair, copied here by hand; it never enters the repository (ADR 0005).
tests/testdata/large/
.claude/settings.local.json
```

### D.10 `tests/testdata/README.md`

```markdown
# Test data

ImageVisualCompare has one kind of test data and one manifest.

- **Generated in the test.** Every image the unit tests decode is written by `TempImages` in
  the test project: PNG and TIFF in six pixel formats, random pixels, a corrupt file. So there
  is no `fixtures/` folder: a generator that fits in eighty lines beats binary files in the
  repository, and it keeps the formats under test visible in code (ADR 0026).
- **Large, outside git.** The three images `manifest.json` lists with `"location": "large"`: the
  owner's Milky Way core as a 16-bit PNG, the same frame as a 16-bit TIFF after the
  print-preview stretch, and the same frame cropped and exported as 8-bit TIFF. They are the
  real pairs the figures in `docs/validation.json` are measured on: a matched pair and a
  mismatched one. Each is a copy, made by hand, in `tests/testdata/large/`, which git ignores
  (ADR 0005); `IVC_TESTDATA_LARGE` may name another folder, as `scripts/check-testdata.ps1`
  allows, and nobody sets it. The script reports a missing one as a warning locally and as an
  error in CI only when `requiredInCi` is true. `ValidationTests` reach them through
  `TestData.Large(name)` and return early with a message when one is absent (xUnit 2 has no
  dynamic skip). They carry the trait `Category=Validation` and are not part of
  `scripts/verify.ps1`; `/validate` runs them.

After a file is replaced on purpose:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
```

Then re-measure its cases (`/validate`) and record the change in `docs/validation.md`.
```

### D.11 `README.md`, the new section (Romanian, with diacritics, before "## Licență")

```markdown
### Dezvoltare: gate-ul, documentația și Claude Code

Din 2026-10-02 proiectul urmează procesul din `ClaudeCodeStarterKit` (folderul de lângă acesta
în repo-ul `photo`; ghidul cu motivația fiecărei piese e `GHID.md` acolo), la fel ca FlatCheck
și AstroPhotoAnalyzer. Pe scurt, ce e impus mecanic:

- **`scripts/verify.ps1` e gate-ul**: scripturile identice cu ale kit-ului, manifestul datelor
  de test, antetele ADR-urilor, notele de design (fiecare în index, cu fișierele ei numite în
  propoziția `Covers:`), restore, `dotnet format` (doar verifică), build cu avertismentele ca
  erori, toate testele în afară de cele de validare. Sub un minut. Îl rulează hook-ul git
  `pre-commit` de la rădăcina repo-ului la orice commit care atinge proiectul, hook-ul
  `pre-merge-commit` la orice merge fără conflicte, hook-ul Claude Code înainte de orice
  `git commit` al modelului, și `scripts/release.ps1`. Fără el verde nu intră niciun commit.

  ```bash
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
  ```

- **Mesajul de commit** e o singură linie, în engleză, de cel mult 100 de caractere.
- **`scripts/`, `.claude/skills/`, `.claude/agents/` și `.claude/rules/kit/` sunt ale
  kit-ului**, byte cu byte, și nu se editează aici; ce e specific proiectului ca valori e în
  `kit.json`, pașii pe care îi adaugă la skill-uri sunt în `docs/skills.md`.
- **Comportamentul e în `docs/requirements.md`**, cu ID-uri `R-NNN` purtate în numele testelor;
  `RepoInvariantTests` cere ca fiecare cerință `implemented` să aibă un test cu ID-ul ei și
  invers. Vechiul `CLAUDE.md` a fost împărțit pe 2026-10-02 în cerințe, decizii și note de
  design; nimic nu s-a pierdut, s-a mutat.
- **Deciziile sunt în `docs/decisions/`**, un ADR per decizie, cu alternativele respinse; cele
  luate înainte de 2026-10-02 (cele opt invariante din vechiul `CLAUDE.md`, printre altele) au
  fost scrise retroactiv.
- **Cifrele pe o pereche reală** (două exporturi de 23 MP ale aceleiași imagini, care nu sunt în
  repo) sunt în `docs/validation.json`, citit de `ValidationTests` (`--filter
  "Category=Validation"`, nu în gate); copiile stau în `tests/testdata/large/`, ignorat de git,
  iar `tests/testdata/manifest.json` le ține hash-urile. `docs/validation.md` spune de unde vine
  fiecare cifră, lângă măsurătorile de memorie din 2026-09-08.
- **Explicațiile din spatele codului** sunt în `docs/design/`, cu indexul în
  `docs/design/README.md`; `CLAUDE.md` ține doar regulile proiectului, cele de proces sunt ale
  kit-ului, în `.claude/rules/kit/process.md`.
- **Versiunea** o crește omul, în `Directory.Build.props` (mutată acolo din csproj pe
  2026-10-02). Un release se face pe `main`, cu `scripts/release.ps1 -Version X.Y.Z`, care
  rulează gate-ul, închide secțiunea din CHANGELOG și publică aplicația în `artifacts/X.Y.Z/`.
  Commit-ul și tag-ul rămân ai omului.
- În Claude Code: `/new-feature`, `/wrap-up`, `/validate`, `/docs-health`, `/mutate`, `/release`.
```
