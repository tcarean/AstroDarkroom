# Name the Visual C++ runtime when LibRaw cannot load, and list LibRaw's libraries in About

## Context

Two changes are left over from the Astro Darkroom suite work (AstroDarkroom ADR 0016, R-017, R-018).
Both apply wherever FlatCheck runs: its own release (`kit.json`: the console as a single file
with the natives inside, the window as a folder) and the suite's flat folder (the window only).

1. **The message when LibRaw cannot load.** `RawReader.Missing` tells the user to put the native
   DLLs beside `flatcheck.exe` and to publish with `IncludeNativeLibrariesForSelfExtract`. That
   is a developer's advice, and it is wrong in every shipped form, which already has the DLLs.
   The real cause on a user's PC is Microsoft's Visual C++ v14 runtime, missing or too old.
2. **About's third-party list.** It must list what the tool ships, as the suite's
   `THIRD-PARTY-NOTICES.txt` does. It must also point to the licence texts truthfully from both
   places the tool runs, and name ImageSharp's licence as Apache-2.0 alone (the Split License
   asks documentation to name only the licence granted).

**Measured on 2026-11-01 (read-only):**

- **The DLLs' imports**, read from the PE import tables of `Sdcb.LibRaw.runtime.win64` 0.21.1:
  - `raw_r.dll` imports `MSVCP140`, `VCOMP140`, `VCRUNTIME140` and `VCRUNTIME140_1`, plus
    `jpeg8`, `lcms2`, `zlib1`, the UCRT, `KERNEL32` and `WS2_32`.
  - `jpeg8.dll`, `lcms2.dll` and `zlib1.dll` import `VCRUNTIME140`.
- **The DLLs' versions.** Their version strings are libjpeg-turbo 2.1.3, zlib 1.2.11 and
  LibRaw 0.21.1-Release.
- **The resolver.** `Sdcb.LibRaw` 0.21.1.7 holds `raw_r.dll`, then `libraw.dll`, plus the Linux
  and macOS names. So the exception names `libraw.dll` both when `raw_r.dll` is missing and
  when the runtime is: the exception cannot tell the two apart.
- **Where the DLLs sit.**
  - The suite publishes `FlatCheck.Wpf` only (`AstroDarkroom/kit.json`). Its flat folder has
    `raw_r`, `jpeg8`, `lcms2` and `zlib1` beside the exe, and no Visual C++ runtime, only WPF's
    `vcruntime140_cor3.dll` (`artifacts/0.9.0`).
  - The own console release carries the DLLs inside the exe.
- **What the own release ships.** `release.ps1` publishes `console/`, `wpf/` and
  `release.json`. It ships no notices file and no LICENSE. The suite folder has
  `THIRD-PARTY-NOTICES.txt` at its root, with every licence's whole text. FlatCheck's own
  `THIRD-PARTY-NOTICES.md` holds the compatibility arguments only. GitHub's `main` is 47 commits
  behind the local `main`, so the suite's full-text file is not online yet.
- **The window shows inner exceptions.** `TabViewModel.Describe` appends every inner exception's
  message that the text does not already contain. Keeping the `DllNotFoundException` as the inner
  exception would put "libraw.dll" back on the window's screen.
- **About today.** It already lists LibRaw (a small mismatch with the task text, which asked to
  add it). The suite's file also names FlatCheck among the programs that ship ".NET 8 and WPF".
- **About's height.** The window has no scroll (`Width=600`, `SizeToContent=Height`, a
  `StackPanel`). APA's About reached 862 DIP and needed the launcher's fit (APA ADR 0070).

**The owner's answers (2026-11-01), all four recommended options:**

1. **Developer hint.** On Windows the message names the runtime alone: missing or too old, the
   likely cause, with Microsoft's link. On other systems, which have no Visual C++ runtime, the
   advice about the native files stays.
2. **Log probe.** The log also says which of the four runtime DLLs Windows cannot load, as the
   launcher's `NativeProbe` does, and in which folder `raw_r.dll` was found.
3. **The pointer to the licence texts** names both places: `THIRD-PARTY-NOTICES.txt` beside the
   program in the Astro Darkroom folder, and the suite file's GitHub address, as a link.
4. **.NET 8 and WPF are not listed**, as in the launcher's About. The lead says "seven
   components" and the database.

## Branch and worktree

- From `FlatCheck/`: `git worktree add .claude/worktrees/Name-the-VC-runtime-and-LibRaw-libraries -b claude/Name-the-VC-runtime-and-LibRaw-libraries main`
  (40 characters).
- This plan is saved there as `docs/plans/2026-11-01-name-the-vc-runtime-and-libraws-libraries.md`.
- Every build and test runs in the worktree, never in the main checkout.
- Commits use the worktree's literal path, never a shell variable (the commit hook needs it).

## Step 0: measure About's height first

- In the worktree, a temporary scratch test (never committed) measures the About content at the
  client width (584 DIP), in both languages, without showing the window. It adds the frame and
  title bar (39 DIP at 100 %, APA ADR 0070).
- The measurement is taken on `main`'s rows and again with the eight rows.
- It is compared against the work areas from AstroDarkroom ADR 0013 and APA ADR 0070:
  - 1040 DIP: a 1080-line screen at 100 %;
  - about 825 DIP: the same at 125 %;
  - about 728 DIP: a 1366x768 screen at 100 %;
  - about 690 DIP: a 1080-line screen at 150 %.
- If the three rows push the window past an area it fits in today, **stop and ask the owner**
  whether to bring the launcher's fit (R-016's design, as APA's ADR 0070) into this branch.

## 1. Requirements first: `docs/requirements.md`

- **R-004.** The last sentence becomes: when LibRaw cannot be loaded, a FITS run is unaffected
  and a raw run exits `1` with R-122's message.
- **R-122 (new, implemented): the message when LibRaw cannot be loaded.** Any of
  `DllNotFoundException`, a `FileNotFoundException` for another file, `BadImageFormatException`
  or `TypeInitializationException` is turned into an `InvalidOperationException`. It is worded
  in the run's language (`T` pairs, Romanian without diacritics) and carries no inner exception.
  - **On Windows:**
    `cannot load LibRaw: most likely Microsoft's Visual C++ runtime is missing or too old, so
    FlatCheck cannot read camera raw files until its latest version is installed (the FITS modes
    work without it): https://aka.ms/vc14/vc_redist.x64.exe`. This is the launcher's
    `VcRuntime.Warning` phrase, with the link last.
    The Romanian:
    `nu pot incarca LibRaw: cel mai probabil runtime-ul Microsoft Visual C++ lipseste sau este
    prea vechi, deci FlatCheck nu poate citi fisiere raw de aparat pana nu instalezi ultima lui
    versiune (modurile pe FITS merg si fara el): https://aka.ms/vc14/vc_redist.x64.exe`.
  - **Elsewhere:** LibRaw's native libraries exist for Windows x64 and Linux x64 only, and must
    sit beside the program or inside it (`IncludeNativeLibrariesForSelfExtract`).
  - **Neither form names `libraw.dll`.** The exception goes to the log (R-110).
- **R-095 (amended).**
  - The list adds libjpeg-turbo (`jpeg8.dll`), Little CMS (`lcms2.dll`) and zlib (`zlib1.dll`),
    each with its licence, copyright and project.
  - The libjpeg-turbo row carries the IJG sentence its licence asks documentation to state.
  - ImageSharp's licence is `Apache-2.0` alone.
  - Each package of the core is claimed by at least one component. A package claimed by several
    components is divided by native file: each DLL of the win-x64 runtime is named by exactly
    one component.
  - The lead counts the linked components, seven today, plus the database.
  - The lead points to `THIRD-PARTY-NOTICES.txt` beside the program in the Astro Darkroom
    folder, and to that file's GitHub address, shown as a link.
- **R-110 (amended).** When LibRaw cannot be loaded, `RawReader` writes one Warn line with the
  exception. On Windows the line also says where `raw_r.dll` was found among the folders .NET
  searches for native libraries (or that it was in none), then the launcher's runtime clause:
  `Visual C++ runtime: missing vcomp140.dll`, or `... vcruntime140.dll, vcruntime140_1.dll,
  msvcp140.dll, vcomp140.dll load`.

## 2. Tests, written to fail first

- **`FlatCheck.Tests`:**
  - **`R122_*`.**
    - The message in English and Romanian on Windows: the runtime words, the link last, no
      `libraw.dll`, no `IncludeNativeLibrariesForSelfExtract`, no diacritics.
    - The message elsewhere: no "Visual C++", the advice about the native files.
    - `CannotLoad(cause)` returns an exception with no inner exception, whose message is the
      user's text.
    - `LogCapture` holds one Warn line from `RawReader` with the cause's message and, on
      Windows, the runtime clause.
  - **`VcRuntime`.** `Missing` keeps the order of `Files` and asks the loader once per file, as
    the launcher's tests do. A fake loader covers the "missing" branch, which this PC (runtime
    installed) cannot reach for real.
  - **The log line's pure composer.** Cases: missing / all load, `raw_r.dll` found / in none of
    the folders, not Windows.
  - **`ThirdPartyTests`.**
    - `R095_EveryPackageTheCoreLinksIsListed` gets the new rule.
    - New `R095_`: the DLLs in the test output's `runtimes/win-x64/native/` are exactly the
      components' native files, each named once.
    - ImageSharp's licence is `Apache-2.0`.
    - The libjpeg-turbo row holds the IJG sentence.
    - The component count goes from 5 to 8.
    - `R095_EveryComponentHasAnEntryInTheNoticesFile` now covers the three new headings.
- **`FlatCheck.Wpf.Tests`:**
  - **`AboutTests`.**
    - The lead says "links seven components" and "leaga sapte componente".
    - The lead names `THIRD-PARTY-NOTICES.txt` in both languages.
    - The view-model exposes the notices link.
  - **`TabViewModel.Describe`** of the new exception holds no `libraw.dll`.
- These tests change because R-095 and R-110 change: requirement first, then test.

## 3. Code

- **`FlatCheck.Core/VcRuntime.cs` (new).** It mirrors the launcher's `VcRuntime`
  (`AstroDarkroom/src/AstroDarkroom.Core/VcRuntime.cs`), which is not shared across projects:
  - `DownloadUrl`;
  - `Files`, the four names, taken from the import tables;
  - `Missing(Func<string,bool> canLoad)`;
  - `CanLoad(string file)`, which calls `NativeLibrary.TryLoad` by name and frees the handle at
    once.
- **`FlatCheck.Core/RawReader.cs`.**
  - `Missing` becomes `CannotLoad(Exception cause)`. It probes only on Windows and only on this
    failure path: the four runtime DLLs, and the folder holding `raw_r.dll` among
    `AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES")`, checked in a scratch single-file
    publish.
  - It logs Warn with the cause and returns the `InvalidOperationException` of
    `CannotLoadMessage(bool windows)`.
  - The header comment and the four catch sites are updated. No field of a libraw type is added
    (`HoldsNoLibrawTypedFields...`).
- **`FlatCheck.Core/ThirdParty.cs`.**
  - An `init` property `NativeFiles` (empty by default), set to `raw_r.dll` on LibRaw.
  - LibRaw's role becomes "(`raw_r.dll`)".
  - Three new rows, with copyright and URL from the suite's file:
    - libjpeg-turbo: IJG, BSD-3-Clause and zlib; "Copyright (C) 1991-2020 Thomas G. Lane, Guido
      Vollbeding; (C) 2009-2022 D. R. Commander; (C) 2015 Viktor Szathmáry", followed by "This
      software is based in part on the work of the Independent JPEG Group." The copyright field
      is never translated. The URL is https://libjpeg-turbo.org/.
    - Little CMS: MIT; Marti Maria Saguer 1998-2020; https://www.littlecms.com/.
    - zlib: zlib; Jean-loup Gailly and Mark Adler 1995-2017; https://zlib.net/.
  - ImageSharp's licence becomes `Apache-2.0`.
  - A `NoticesUrl` constant: `https://github.com/tcarean/AstroDarkroom/blob/main/AstroDarkroom/THIRD-PARTY-NOTICES.md`.
  - The header comment is updated.
- **The About window.**
  - `SessionMessages.AboutThirdPartyLead` gets the new pointer sentence, in both languages.
  - `AboutViewModel.NoticesUrl` exposes the link.
  - `AboutWindow.xaml` shows a `Hyperlink` line under the lead, as the licence card does.
- Every new or edited type and method gets its XML documentation comment.

## 4. Documentation, as the process asks

- **`README.md`.**
  - A bullet under "What FlatCheck works with": the Visual C++ v14 runtime (x64), the names
    Windows lists it under, and the link.
  - The build section's sentence "a raw is refused with a message naming what is missing" is
    reworded: the message names the runtime, and the log says which DLL.
  - The third-party table gets the three rows, "seven components" and the IJG sentence.
    ImageSharp's licence reads `Apache-2.0`, and its pin note moves out of the licence column.
  - The About section mentions the pointer.
- **`THIRD-PARTY-NOTICES.md`.**
  - Sections for libjpeg-turbo, Little CMS and zlib: role, licence and GPLv3 compatibility
    (the FSF lists IJG, BSD-3, zlib and MIT as compatible), plus the IJG sentence.
  - LibRaw's role becomes `raw_r.dll`.
  - ImageSharp's licence line reads Apache-2.0, granted by the Split License.
  - The intro says where the whole texts are.
  - "How an entry is added" covers a new DLL in the runtime package.
- **`FlatCheck.Core.csproj`.** The comment on the natives names the runtime.
- **`docs/design/dependencies.md`.** It gains the loading failure's mechanics: the resolver's
  two names, why the user sees no inner exception, the probe and its search. `VcRuntime.cs` is
  added to `Covers:`.
- **`docs/design/logging.md`.** The `RawReader` row gains the Warn line.
- **`docs/decisions/0082-...`.** The four choices, the measurements and the alternatives
  rejected. "Supersedes: 0009 in part" (the "naming what is missing" message and its publish
  advice), with ADR 0009's status line amended. ADR 0010 is checked for a licence wording to
  supersede.
- **`CHANGELOG.md` [Unreleased].**
  - Changed: the message, About's list, ImageSharp's licence, the pointer.
  - Added: the log probe.
- The version is not touched.

## 5. Verification

- `dotnet build FlatCheck.sln -c Release --no-incremental -warnaserror --no-restore`, then the
  gate (`scripts/verify.ps1`), green.
- **Scratch, in the scratchpad.** A plain single-file console publish (no self-extract) and a
  real raw:
  1. With `raw_r.dll` removed, run it: the new message, exit 1, and a log line saying
     `raw_r.dll` is in no folder and the four runtime DLLs load.
  2. With a copy of `raw_r.dll` whose import `VCOMP140.DLL` is renamed: the same message. This
     shows the case the exception cannot tell apart.
  3. A self-extracting publish confirms that the search folders hold the extraction folder.
- **Limit.** The probe's "missing" branch on a PC without the runtime is covered only by the
  fake loader. This PC has the runtime.
- **Renders.** `FLATCHECK_DUMP_SCREENS` renders the About window, in English and Romanian, at
  its measured height, and the PNGs are read. The window is not launched live.
- No golden should move. If one does, that is a finding. No formula changes, so no `/validate`.
- **Wrap-up.** `/wrap-up`, a subagent review of `main...HEAD`, then a second one on the fix
  commit. Before proposing the merge, check `main`'s latest ADR number.

## Addendum: About fits its screen (the owner's answer after step 0)

**Step 0 measured on 2026-11-01**, in a scratch test since removed. The About content was laid out
at 584 DIP, after one dispatcher pass at `ContextIdle` so that the bindings had resolved.

- Today's content is 880 DIP, a window of about 919 DIP with the frame. It fits a 1080-line
  screen at 100 % (1040 DIP) and already overflows at 125 % (about 825), on 1366x768 (about
  728) and at 150 % (about 690).
- With the three rows and the new pointer, the content is 1161 DIP in English and 1176 in
  Romanian, a window of about 1200 and 1215 DIP: it no longer fits 1040.

The owner chose the launcher's fit in this branch, as AstroPhotoAnalyzer's ADR 0070 brought it
there. It goes in a commit of its own, with its requirement, tests and ADR:

- **R-123 (new).** "Despre" fits the work area of the screen the main window is on:
  - never taller than that area, opening inside it;
  - its cards scroll when they are taller, with the wheel and with Up, Down, Page Up, Page Down,
    Home and End, the keys only while the cards can scroll;
  - Close stays in view in a row of its own, and Enter and Esc close the window as before.
- **R-110 (amended).** One Debug line under `Window` once About is shown: its size and place,
  the work area, and whether its cards scroll.
- **`FlatCheck.Wpf/Interop/ScreenArea.cs`**, as APA's: `WorkAreaOf(Window)`, with
  `MonitorFromWindow` and `GetMonitorInfoW` through `DllImport` and the primary screen's area
  as the fallback, and `TopInside(top, height, areaTop, areaHeight)`.
- **The About window.** The constructor becomes `AboutWindow(Rect workArea)`. It sets
  `MaxHeight` and, at `ContentRendered`, `FitInto` and the Debug line. `PreviewKeyDown` scrolls
  the cards. In the XAML, the cards go in a `ScrollViewer` named `Cards` and Close in its own
  row, with the same margins where the window fits.
- **The main window** passes `ScreenArea.WorkAreaOf(this)`.
- **Tests.**
  - New `AboutWindowFitTests`: `R123_` for the arithmetic, the low area with its keys, the area
    where it fits, and Windows' answer; `R110_` for the Debug line through `OnContentRendered`.
  - `WindowSmokeTests` and `WindowLanguageTests` pass `SystemParameters.WorkArea`. The smoke
    render shows the whole window.
- **ADR 0083.** It records the fit, its measured table (the content's natural heights against
  the four areas, computed and not shown) and the differences from APA's, if any.
- `docs/design/` covers `ScreenArea.cs` and the About window's fit.

## Not in this task

- **AstroDarkroom is not touched.** The last paragraph of its ADR 0016 ("keep their 'cannot load
  libraw' message ...") goes stale once this merges. The owner is told at the end.
- **The Linux runtime package** carries `libgomp.so.1` (GPL-3.0 with the GCC exception),
  `libjpeg.so.8` and `liblcms2.so` for a Linux build, which neither Windows form ships. That is
  noted for the owner, not acted on.

## What deviated (at wrap-up, 2026-11-01)

- **The About fit came into this branch.** It is the addendum above, after step 0's measurement
  and the owner's answer.
- **A fact of the task held only in part.** It said the caught exception names `libraw.dll`.
  Measured end to end in scratch publishes, that holds for a folder publish, the window's, alone
  or in the Astro Darkroom folder. The single-file console's exception says only "Dll was not
  found.". The owner chose to go on with the docs stating both texts (R-122, ADR 0082,
  `dependencies.md`, the CHANGELOG). The design did not change.
- **The real About is a little shorter than step 0's estimate:** 1146 DIP of content in English
  and 1161 in Romanian (step 0's stand-in rows gave 1161 and 1176), because the link under the
  lead is set at 11 px. ADR 0083's table uses the real figures.
- **Two small additions to the code list.**
  - `LogText.VcRuntime` holds the runtime clause in the launcher's words.
  - The window's R-122 check sits in `ExposureTabTests`, the tab that reads raws.
- **Two smaller test changes.**
  - `WindowSmokeTests` renders About after a dispatcher pass, at its own height. Without the
    pass, the content measured 397 DIP instead of 880.
  - `WindowSmokeTests.Render` became internal, for `AboutWindowFitTests`.
- **The README's "Logs" paragraph** names the new log line too.
- **An unrelated flaky test.**
  - `HistEqTabTests.SwitchingTheViewChangesThePictureWithoutRerunning` failed in two of nine
    full test runs, the gate's included. It passed in every run of the window tests alone and in
    800 runs of its class under load.
  - The likely race, read from the code (the failing assertion's message was not captured: the
    five runs with a TRX log all passed): setting `Selected` starts a run (`Pending`), then the
    test starts a second with `RecomputeAsync()` and asserts `Pending.IsCompleted`. The first
    run checks its cancelled token only after `HistEqRun.Execute` returns, so it can still be
    running.
  - The test and the tab are unchanged on this branch. Reported to the owner, not edited.
