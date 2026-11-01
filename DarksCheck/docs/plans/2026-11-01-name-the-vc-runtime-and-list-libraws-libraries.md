# Name the Visual C++ runtime when LibRaw cannot load; About lists LibRaw's libraries and fits its screen

Plan for the task branch `claude/Fix-DarksCheck-raw-message-and-About`, worktree
`.claude/worktrees/Fix-DarksCheck-raw-message-and-About`, from `main` at 157b73d8. Written on
2026-11-01; approved by the owner on 2026-11-01 as written, every question answered as
recommended; this file is the first commit of the branch. The branch was planned as
`claude/Name-the-VC-runtime-and-LibRaw-libraries`, a name FlatCheck's session took first for its
own task. The version is not changed.

## Context

Two changes left over from the Astro Darkroom suite work (AstroDarkroom ADRs 0014 and 0016).

1. LibRaw's DLLs import Microsoft's Visual C++ v14 runtime, which neither DarksCheck's release
   nor the suite ships. When it is missing, DarksCheck's message says that the native DLLs must
   sit beside `darkscheck.exe` and to publish with `IncludeNativeLibrariesForSelfExtract`: advice
   for a developer, wrong for a user.
2. About lists three components, while its lead says "two". It must list what the program
   ships, as the suite's `THIRD-PARTY-NOTICES` does: LibRaw, and apart from it the libraries
   inside its DLLs.

### Measured on 2026-11-01 (scratch copy of `main`, nothing in the repository touched)

The task's facts all hold:

- **Imports.** `raw_r.dll` imports `MSVCP140`, `VCOMP140`, `VCRUNTIME140` and `VCRUNTIME140_1`.
  `jpeg8.dll`, `lcms2.dll` and `zlib1.dll` import `VCRUNTIME140`. The rest are the Universal CRT
  and Windows.
- **The exception.** With `raw_r.dll` removed, and with a copy of it whose `VCRUNTIME140_1.dll`
  import was renamed to a DLL that does not exist (a missing runtime), the exception is the same:
  `Unable to load DLL 'libraw.dll' or one of its dependencies ... (0x8007007E)`.
  - The log keeps the whole exception, but never says which DLL failed.
  - The run's `ERROR:` line repeats the message three times (`FrameStack.FirstErrors`).
- **The own release (`kit.json`)** writes the exe, `.pdb` and `.xml`, and nothing else.
  - The console's single file bundles LibRaw's 4 DLLs and extracts exactly those.
  - The window's bundles them plus WPF's `vcruntime140_cor3.dll`.
  - Neither bundle carries any of the four runtime DLLs.
- **The suite** publishes the window flat, LibRaw's DLLs beside `DarksCheck.Wpf.exe`, with
  `THIRD-PARTY-NOTICES.txt` and `LICENSE` at the root.
- **The DLLs can be lost** only by a custom publish without the flag, with the exe copied alone.
  That gives the same outer message, inner text `Dll was not found.`
- **The probe's cost**, in a fresh .NET 8 process: 1.5-6 ms per runtime DLL found, 4-7 ms per
  miss.
- **About's height** grows from about 710 DIP of content at its 584 DIP client width (≈750 with
  the frame) to about 990 (≈1030) with six rows and the new lead. A 125 % screen's work area is
  about 825 DIP, so About fits there today and would not after this change.

### The owner's answers (2026-11-01)

- **The message:** no developer hint.
- **The log:** a line at the first failure saying which runtime DLLs Windows cannot load.
- **About's list:** six components; no row for .NET.
- **About's height:** fit it here, with APA's design.

### Decided here (the process allows it)

- **The pointer to the licence texts.** A wording that holds in both places exists, so no
  question: the suite's `THIRD-PARTY-NOTICES.txt` beside the program, and the suite's `.md` in
  the source repository, with the repository's address.
- **The loader's text leaves the user's message.** It names `libraw.dll`. The log keeps it.
- **The run's `ERROR:` line** still quotes three frames' errors, as today. The new message is
  shorter than the old one: about 190 characters against 330.

## The changes

### 1. The message (R-110; R-008 points to it)

- **Where.** `src/DarksCheck.Core/Imaging/RawReader.cs`, `Missing`. Only its `T(...)` pair and
  the comments change: the two exceptions of the rule "Copied files stay copies" (ADRs 0019,
  0021). Its code and its catch list stay LightsCheck's. The wording is for LightsCheck to take
  back.
- **The wording** follows the launcher's (`AstroDarkroom.Core/VcRuntime.Warning`), naming no
  folder and no executable, so it holds in both places:
  - EN: `cannot load LibRaw: Microsoft's Visual C++ runtime is likely missing or too old, so
    camera raw files cannot be read until its latest version is installed:
    https://aka.ms/vc14/vc_redist.x64.exe`
  - RO: `nu pot incarca LibRaw: runtime-ul Microsoft Visual C++ probabil lipseste sau este prea
    vechi, deci nu pot citi fisiere raw de aparat pana nu instalezi ultima lui versiune:
    https://aka.ms/vc14/vc_redist.x64.exe`
- **The loader's exception** stays the inner one.

### 2. The log line (part of R-110)

- **A new file, `src/DarksCheck.Core/Logging/VcRuntime.cs`** (DarksCheck's own). It holds:
  - `DownloadUrl` and `Files`, the four DLLs in the launcher's order;
  - `IsLoadFailure(e)`: RawReader's `InvalidOperationException` whose inner exception is one of
    the four loader exceptions its catch list takes;
  - `Probe()`: `NativeLibrary.TryLoad` of each DLL, freed at once, with the loaded module's path
    and file version read from the process's module list;
  - a pure `Line(...)` that words the result;
  - `LogOnce(e)`, guarded per process with `Interlocked`. Its testable overload takes the probe
    and the guard.
- **The line.** At Warn: `Visual C++ runtime: missing vcruntime140_1.dll; found vcruntime140.dll
  14.x (path), ...; camera raw files cannot be read`. At Info when all four load, the cause then
  being elsewhere. It is written once per process, at the first LibRaw load failure.
- **The hooks**, one call each, at the two seams that log a frame's failure:
  `FrameInfo.Read(int, string)` and `FrameStack.LogFailed`. `LightsAnalyzer` goes through the
  latter. The copied reader is not touched.

### 3. About's list (R-120; R-010 amended)

- **`src/DarksCheck.Core/ThirdParty.cs`.** Three new components after LibRaw, each role naming
  its DLL:
  - libjpeg-turbo (`jpeg8.dll`): IJG, BSD-3-Clause and zlib. The licence line quotes "this
    software is based in part on the work of the Independent JPEG Group", which the IJG licence
    asks of an executable's documentation; the own single file has no other. Copyright lines as
    the suite's file quotes them.
  - Little CMS (`lcms2.dll`): MIT, Marti Maria Saguer.
  - zlib (`zlib1.dll`): zlib, Gailly and Adler.
  - LibRaw's role names `raw_r.dll`.
  - Each new component's `Packages` is `Sdcb.LibRaw.runtime.win64`.
- **R-010.** A package is claimed by one component, except `Sdcb.LibRaw.runtime.win64`, whose
  four DLLs are four components. This is a requirement change, made first, so its test changes
  with it; it is not a test edited to pass.
- **`src/DarksCheck.Wpf/ViewModels/WindowTexts.cs`, `AboutThirdPartyLead`.**
  - The count comes from the list, in words, through a small `CountWord`, as FlatCheck does.
    "two" becomes "six".
  - The pointer: "Their licence texts, whole, and why each is compatible are in
    THIRD-PARTY-NOTICES.txt, beside the program in the Astro Darkroom folder, and in
    AstroDarkroom/THIRD-PARTY-NOTICES.md in the source repository,
    https://github.com/tcarean/AstroDarkroom", and the Romanian.
  - `AboutViewModel` (a LightsCheck copy) is unchanged.
- **`THIRD-PARTY-NOTICES.md` (DarksCheck's).**
  - New sections for libjpeg-turbo, Little CMS and zlib (role, licence, compatibility, version,
    project, the IJG sentence).
  - LibRaw's section names `raw_r.dll` alone and the runtime it needs.
  - The intro points to the suite's file for the whole texts.

### 4. About fits its screen (R-121): APA's design (its ADR 0070) taken as a hunk

- **`src/DarksCheck.Wpf/Presentation/ScreenArea.cs`** (new, DarksCheck's own):
  - `WorkAreaOf(window)`, as the launcher's (`LibraryImport` of `MonitorFromWindow` and
    `GetMonitorInfoW`, as `DarkTitleBar` does), falling back to `SystemParameters.WorkArea` with
    a Debug line;
  - `TopInside`, APA's arithmetic.
- **`Views/AboutWindow.xaml`**: the cards in a `ScrollViewer` named `Cards` (transparent, not
  focusable), the Close button in a row of its own. Margins chosen so that, where the window
  fits, the layout is as before.
- **`Views/AboutWindow.xaml.cs`**:
  - the constructor is `AboutWindow(Rect workArea)` and sets `MaxHeight`;
  - at `ContentRendered`, `FitInto` keeps the top inside the area and writes the Debug line
    `About shown WxH at (x,y); work area ...; cards scroll: yes|no`;
  - at `PreviewKeyDown`, `Scroll(key)` handles Up, Down, Page Up, Page Down, Home and End only
    while the cards can scroll.
- **`MainWindow.xaml.cs`** passes `ScreenArea.WorkAreaOf(this)`.
- **The three tests that construct About** pass a large `Rect`.
- **Headers:** the window's files say "copied from LightsCheck, with APA's fit as its own hunk
  (ADR 0027)".

### 5. Documents

- **`docs/requirements.md`.** R-008's last clause and R-010 are amended. New rows: R-110 under
  "Reading frames (the block continues at R-110)", and R-120 and R-121 under "The window (the
  block continues at R-120)". All start as `draft`, then become `implemented`.
- **ADR 0026, the message and the probe.** Rejected: a builder's sentence, the loader's text, no
  probe, a probe at every start, the probe inside the copied reader, shipping the runtime
  (AstroDarkroom ADR 0016).
- **ADR 0027, About.**
  - It records: six rows (.NET left out as the platform), the DLL in each role, the IJG
    sentence, the count from the list, the pointer, the notices sections, and the fit taken from
    APA as a hunk.
  - It supersedes ADR 0009 in part, the About window's "LightsCheck's to improve first", with
    0009's Status line to match.
  - ADR 0025's "ThirdPartyTests counts three" gets a dated note (the kit's ADR 0024).
  - Its table: the heights measured at work areas of 1040, 825, 728 and 690 DIP.
- **`README.md`.**
  - "Getting it": the Visual C++ v14 runtime is needed for raws only, most computers have it,
    the message and the link.
  - "Building": how LibRaw's DLLs travel, inside the single file or beside the exe. This is where
    the developer hint now lives.
  - The About line, and "Licence": where the whole texts are.
- **Design notes.**
  - `imaging.md`: the message.
  - `logging.md`: the `VcRuntime` line, About's line and `ScreenArea`'s line.
  - `window.md`: About's list, lead and fit, and `ScreenArea` (Presentation's second file not
    copied).
  - The index: the window row names R-120 and R-121.
- **Old advice in comments:** the Core csproj comment and `Directory.Packages.props` (the
  runtime they need), and RawReader's header comment.
- **`CHANGELOG.md` [Unreleased]:** Changed (the message and log line; About's list and pointer;
  the fit) and Fixed (the lead said two of three).

## Tests (written before the code, seen red)

- **R-110, in `DarksCheck.Tests`:**
  - **The message, both languages.** RawReader's private `Missing` is called through reflection
    with a `DllNotFoundException`. The test checks the runtime's name, "missing or too old" /
    "lipseste sau este prea vechi", the link, the inner exception kept, no `libraw.dll`, no
    `IncludeNativeLibrariesForSelfExtract`, no `darkscheck.exe`, and ASCII Romanian.
  - **End to end, out of process.** The console's files are copied from the test's output folder
    without `raw_r.dll`, then `darkscheck.exe` is run on a temporary session of three `.NEF`
    darks, with `--inventory` and with a full run. Expected:
    - every row carries the new message;
    - the full run ends in `ERROR: no dark could be decoded: ...` with exit 1;
    - its log holds exactly one `Visual C++ runtime:` line, at Info, since this PC has the
      runtime.
  - **The probe's line.** `IsLoadFailure` is true for `Missing`'s exception and false for
    RawReader's other refusals. `Line` words the found and missing cases. `LogOnce` writes once
    for two failures and never for another error. A fake probe and a local guard are used.
- **R-010 (`ThirdPartyTests`).** Every linked package is claimed. Only the win64 runtime is
  shared, by exactly LibRaw, libjpeg-turbo, Little CMS and zlib. The count is 3 → 6.
- **R-120:**
  - The names equal the sections of `../AstroDarkroom/THIRD-PARTY-NOTICES.md` whose `Programs:`
    line names DarksCheck, but ".NET 8 and WPF".
  - Each DLL in the build output's `runtimes/win-x64/native` is named by exactly one
    component's role, in both languages.
  - The lead holds the count word of the list and both places, in both languages (`AboutTests`).
- **R-121 (`AboutWindowFitTests`, ported from APA's):**
  - `TopInside`'s cases;
  - on a low work area the cards scroll and Close stays in view, End and Home move them, Enter
    stays Close's;
  - where the window fits, nothing changes and no key is taken;
  - the work area is the window's own screen's;
  - the fit and its Debug line at `ContentRendered`.
- **Unchanged and must stay green:** the goldens (no raw and no `--help` change), the rendering
  and language tests of every tab and of About.

## Verification

- The gate in the worktree. A `dotnet build DarksCheck.sln -c Release --no-restore
  --no-incremental -warnaserror` comes first, so no warning hides.
- The end-to-end test's output read by eye, and the About renders dumped with
  `DARKSCHECK_DUMP_SCREENS` and looked at, in English and Romanian.
- The heights at the four work areas: the window shown off the screen, not activated, inside a
  test process. The figures go into ADR 0027 and R-121's example.
- No `/validate`: no formula or threshold changes.
- No live start of the real window unless the owner says so.
- `scripts/check-design.ps1 -Since main`.
- AstroDarkroom's R-017 is unaffected: no package changes.

## Wrap-up, reviews, merge

- `/wrap-up`: verify, CHANGELOG, requirements `implemented`, the plan saved, the whole diff read
  back, and a commit on the branch.
- A subagent review of `git diff main...HEAD`, briefed with the intent and the rules, not my
  conclusions. I verify each finding and fix the real ones in one commit.
- A second subagent review of that commit alone.
- Before proposing the merge, I check `main`'s latest ADR number (0026 and 0027 are free today)
  and say that a patch bump seems warranted; the version is the owner's.

## What deviated

Filled at wrap-up.
