# ImageVisualCompare

WPF before/after slider for two variants of the same astro processing: image A left of a
draggable vertical line, image B right of it, both at the same zoom and pan, with the RGB of
the pixel under the cursor read from both. Built to see a difference of one or two levels in a
dark field, where the eye cannot. One WPF application over a pure core library, .NET 8,
Windows, x64, **no imaging package**: decoding is WIC, the codec WPF uses anyway (ADR 0008),
and the project's own reader for FITS, which WIC does not read (ADR 0029). The application's
only packages are NLog's two, for the diagnostic log beside the executable (ADR 0043); the core
has none. Everything on screen is dark, whatever the system theme (ADR 0018).

## Commands

- Gate (what pre-commit, pre-merge-commit and the Claude commit hook run, about 20 s):
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build ImageVisualCompare.sln -c Release --nologo` (Windows only: WPF)
- Test: `dotnet test ImageVisualCompare.sln -c Release --nologo --filter "Category!=Validation"`
- One test: `dotnet test ImageVisualCompare.sln --filter "FullyQualifiedName~R020"`
- Format: `dotnet format ImageVisualCompare.sln` (check only: `--verify-no-changes`)
- Run: `dotnet run --project src/ImageVisualCompare.Wpf -- [--lang=ro] [A.png B.png]`; without
  the two paths the file dialogs ask (R-001, R-002), without `--lang` the interface is English
  (R-100); the log is `bin/<config>/net8.0-windows/log/ImageVisualCompare.Wpf.log`, its level
  the environment variable `IMAGEVISUALCOMPARE_LOG` (`trace` adds the FITS cards, R-122)
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

- `src/ImageVisualCompare.Core/` - `net8.0`, no UI framework reference: zoom and pan
  (`Viewport.cs`, `ZoomLadder.cs`), the divider (`DividerModel.cs`), the canvas of a pair
  (`ImagePairValidator.cs`), what a file turned out to be (`ImageInfo.cs`), the FITS reader
  (`FitsHeader.cs`, `FitsScale.cs`, `FitsRowDecoder.cs`), the display modes (`DisplayMode.cs`,
  `DisplayRenderer.cs`, `SampleHistogram.cs`, `LevelLookup.cs`, `RainbowTable.cs`; ADR 0040), a float TIFF's samples shown as a
  FITS's (`FloatSamples.cs`), and the layout
  records the controls assign without arithmetic of their own (`RenderPlan.cs`,
  `DividerLayout.cs`, `MinimapGeometry.cs`, `ScrollBarState.cs`, `Geometry.cs`, `PointD.cs`),
  and every text the application shows, in English and Romanian, with the command line that
  chooses between them (`UiText.cs`, `StartupArguments.cs`).
  All of it unit tested.
- `src/ImageVisualCompare.Wpf/` - `net8.0-windows`, the application: `Program.cs` (the entry
  point, which starts the log), `Logging/` (the diagnostic log), `Imaging/` (the decoder
  seam, WIC and the FITS loader behind it, the pair loader, memory housekeeping), `Controls/` (the comparison
  surface, the minimap), `MainWindow`, `MessageDialog`, `AboutWindow` (FlatCheck's, ADR 0045),
  `Interop/DarkTitleBar.cs`,
  `Services/FileDialogService.cs`, `Theme.xaml`, `Version.cs` and `ThirdParty.cs` (what About
  says), `Assets/` (the icon and the script that draws it). `THIRD-PARTY-NOTICES.md` holds the
  licences' text of the components About lists and stays beside the source; no build ships it
  (R-075).
- `tests/ImageVisualCompare.Tests/` - `net8.0-windows` with `UseWPF`: Core, the imaging layer
  on the real WIC codecs (fixtures generated at run time by `TempImages` and `FitsFixture`), a
  defect of a window or a control on one STA thread, never shown (`WpfThread`, ADR 0032; the
  `App` start WPF runs on that thread does show its own window, ADR 0043), the log of every
  test captured at `Trace` (`LogCapture`), `RepoInvariantTests`, `ValidationTests` (trait
  `Category=Validation`), `LicenseHeaderTests`, `VersionTests`, `ThirdPartyTests`,
  `AboutWindowTests`.
  `tests/testdata/manifest.json` hashes the real pair, which stays outside git.
- `Directory.Build.props` - `<Version>` and `<Copyright>`, the single place for both (ADR
  0004), and the strictness every project builds with.
- `kit.json` - what the kit's scripts, skills and agent need to know about this project.
  `docs/skills.md` - this project's additions to the kit's skills. `scripts/`,
  `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` and the process keys of
  `.claude/settings.json` are the kit's, the same here (ADR 0002; the kit's ADRs 0017, 0018,
  0028).
- `docs/requirements.md` - the behaviour, with IDs. `docs/decisions/` - ADRs. `docs/validation.md`
  + `.json` - figures on the real pair. `docs/design/` - the explanations, per area, read on
  demand. `docs/plans/` - approved plans. `README.md` - the user documentation, English (ADR 0041).
- The process rules are the kit's, in `.claude/rules/kit/process.md`; this project's own are in
  this file. The reasons behind the process are in `GHID.md` in the kit folder
  `ClaudeCodeStarterKit` beside this project in the `photo` repository (for people, not loaded).
- This folder lives inside the `photo` monorepo (`E:\Photografie\Code`); the git root is the
  parent. Sample images never enter the repository: the root `.gitignore` covers `*.tiff` but
  not `*.tif`, `*.png` or `*.fit`, so keep them out by hand.

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are ImageVisualCompare's own; one that extends
or varies a kit rule names it.

- **No imaging dependency** (extends the kit's rule "Packages"). Core references no NuGet
  package, and the application only `NLog` and `NLog.Targets.AtomicFile`, for the log (ADR
  0043); WIC decodes, and `IImageLoader` is the one seam a replacement
  decoder would fill. SkiaSharp, ImageSharp and Magick.NET were compared and rejected; read
  `docs/design/imaging.md` before proposing one again (ADR 0008). FITS, which WIC has no codec
  for, is read by the project's own code in Core, behind the same seam (ADR 0029). A package
  the application references gets an entry in `ThirdParty.cs`, which About shows, and a heading
  in `THIRD-PARTY-NOTICES.md` (R-075).
- **The log is English, starts in `Program.Main`, and never stops the tool.** Every log line is
  composed through `LogText`, never through `UiText`; a message the user was shown is logged
  as shown. Only `Program.Main` calls `LogSetup.Apply` and `Close`, each in a `try`: the WPF
  test thread builds an `App`, and WPF runs its start there. Every logger is a `static readonly
  Logger Log` with a name of `Loggers`, but `LogSetup`'s, a property fetched on use; nothing in
  Core logs (ADR 0043).
- **A FITS, and a float TIFF, is shown as stored.** No stretch, and the 8-bit level is the one
  WIC gives the same value in a 16-bit PNG; `FitsScale` is the only place a FITS value or a
  float sample becomes a level, never WIC's float conversion (ADR 0029, ADR 0035).
- **Core has no UI.** `ImageVisualCompare.Core` references no UI framework; its own `PointD`,
  `SizeD` and `RectD` exist so it never needs `WindowsBase`. The controls do no arithmetic: a
  number on screen comes from a Core record and has its test there (ADR 0009).
- **One transform object, not two.** The same `TransformGroup` instance on both images and both
  backdrops; the synchronisation is structural (ADR 0010).
- **Physical pixels below, device-independent pixels above.** `Viewport` counts physical
  pixels; the conversion happens only in `CompareView`: on the way in, wherever it hands
  `Viewport` a position or a size, and in `Sync` on the way out. The divider stays in DIPs
  (ADR 0011).
- **The image layers are `Canvas`, sized in pixels, clipped on the layer, over one backdrop.**
  Never a `Grid`, never the natural size, never a clip on the `Image`, never a layer without
  the shared opaque backdrop (ADR 0012, ADR 0013).
- **Padding is layout, never pixels.** A mismatched pair is compared on the larger canvas,
  both anchored top-left, the backdrop fills the gap, and the user is asked before any decode
  (ADR 0014).
- **Formats are reported, not branched on.** Everything is Bgra32 after the load, and nothing
  after it reads the format; the status bar says what the file was (ADR 0022). The one branch
  is in `WicImageLoader`: float samples go to `FloatSamples`, not to WIC's conversion (ADR 0035).
- **Two interface languages** (varies the kit's rule "Languages"). Every text the application
  shows is a member of `UiText` in Core, its English and Romanian wordings side by side, never a
  literal where it is shown; `--lang` chooses the table once at start-up, English by default.
  Romanian is allowed in those Romanian wordings, in the tests that assert them, and where a
  comment or a document quotes them; nowhere else. No `CultureInfo` is set, so numbers read the same in both (ADR 0038).
- **Never set `InvariantGlobalization`.** WPF's font cache dies on it during text layout, and no
  unit test can catch it (ADR 0017).
- **No `MessageBox`; every `Window` calls `DarkTitleBar.Apply` in its constructor.** The
  application's own `MessageDialog` says and asks; the file dialogs are the one thing that
  follows the system theme (ADR 0018).
- **Run the application when the WPF layer changes.** Both real defects lived in the untested
  layer and were found on a screenshot; a green suite says little about `Controls/`,
  `MainWindow`, `Program.cs` or `Directory.Build.props`, whose few tests reproduce single
  defects in windows they never show (ADR 0026, ADR 0032; the `App` start WPF runs on the test
  thread does show its own, ADR 0043).
- **Licence headers.** Every `.cs` file starts with the GPL notice, `.xaml` files do not;
  `LicenseHeaderTests` fails otherwise, and its project list is explicit, so adding a project
  is a decision (ADR 0024).
- **User documentation lives in `README.md`, and only there.** Code comments explain why the
  code is the way it is; the README explains how to use the tool, in English, quoting the
  English interface (ADR 0041); `docs/` explains what it does and how, for whoever maintains it
  (ADR 0025).

## Workflow

The kit's workflow in `.claude/rules/kit/process.md` applies. ImageVisualCompare adds:

- `/wrap-up` checks `README.md`, the user documentation, and launches the application when a
  file under `src/ImageVisualCompare.Wpf/` changed (the rule "Run the application").
- A release is tagged `imagevisualcompare-vX.Y.Z`, the name `scripts/release.ps1` prints; it
  publishes the application self-contained for win-x64 into `artifacts/X.Y.Z/`.
- The main checkout of the kit's rule "Worktrees" is the shared checkout `E:\Photografie\Code`.
  A worktree has no copy of the real pair; a task that runs `/validate` copies the files that
  `docs/skills.md` lists for it into the worktree's `tests/testdata/large/` first (ADR 0005).

## Read before touching

`docs/design/README.md` maps every area to its note. The ones most likely to catch a careless
edit:

- Anything in `Controls/`: `docs/design/rendering.md` and ADR 0010 to ADR 0013, ADR 0019 (the
  shared transform, the two pixel spaces, `Canvas`, the clip, the backdrop, the floating bars).
- Anything in `Imaging/`, or a package: `docs/design/imaging.md` and ADR 0008, ADR 0014 to
  ADR 0016 (probe first, decode sequentially, release through the finaliser pass).
- `Directory.Build.props`, a new `Window`, the About window: `docs/design/shell.md` and ADR 0017,
  ADR 0018, ADR 0045.
- A text on screen, `App.xaml.cs`, the command line: `docs/design/language.md` and ADR 0038.
- `Program.cs`, `Logging/`, a log line: `docs/design/logging.md` and ADR 0043.
- `Version.cs`, `ThirdParty.cs`, `LicenseHeaderTests`, `ThirdPartyTests`:
  `docs/design/versioning-and-licence.md`, ADR 0023, ADR 0024, ADR 0043.
- A file you have not opened before: `docs/design/code-map.md`.

## Style

- `.editorconfig` holds whitespace rules and `IDE0005`; `dotnet format` enforces them, and so
  does the build, which writes the XML documentation file for that reason (CS1591 muted).
  Warnings are errors, `EnforceCodeStyleInBuild` and `Nullable` are on, in
  `Directory.Build.props` (ADR 0003). The kit's code-style rules (braces, `var`) are not
  enabled: 38 findings on 2026-10-02; a style pass is a task of its own.
- Fix, do not suppress, unless the suppression carries a comment with the reason.
