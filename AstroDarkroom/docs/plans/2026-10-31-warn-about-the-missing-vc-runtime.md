# 2026-10-31 Name the Visual C++ runtime LibRaw needs, and warn when it is missing

## Goal

`raw_r.dll` (LibRaw, from `Sdcb.LibRaw.runtime.win64` 0.21.1, compiled by vcpkg) imports
`VCRUNTIME140.dll`, `VCRUNTIME140_1.dll`, `MSVCP140.dll` and `VCOMP140.DLL`, the Visual C++
2015-2022 runtime for x64; `jpeg8.dll`, `lcms2.dll` and `zlib1.dll` import `VCRUNTIME140.dll`
too. The suite folder ships none of them (only WPF's renamed `vcruntime140_cor3.dll`), and no
document names the runtime as a prerequisite.

Measured on 2026-10-31 with a scratch loader (`LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR`, so `System32`'s
copies cannot stand in): with LibRaw's four DLLs alone in a folder, all four fail with error 126
("The specified module could not be found"); with the four runtime DLLs beside them, all load
and `libraw_version()` is `0.21.1-Release`. On a computer without the runtime, DarksCheck,
FlatCheck and LightsCheck therefore cannot read camera raw files (their FITS modes still work),
and each says "cannot load libraw ... The native DLLs (raw_r.dll and its neighbours) must sit
beside flatcheck.exe ... publish with IncludeNativeLibrariesForSelfExtract", which misleads: the
DLLs are there, and the runtime is not named. The launcher, ImageVisualCompare, CopyWcs and
AstroPhotoAnalyzer do not need it. Not measured on a fresh Windows: this PC is Windows 10 Home
(no Windows Sandbox) with the runtime 14.51.36247 installed.

.NET 8 loads a NuGet package's native library by its full path with
`LOAD_WITH_ALTERED_SEARCH_PATH` (`nativelibrary.cpp`, release/8.0), so `raw_r.dll`'s dependencies
are looked for in the suite folder, then in Windows' standard order (System32, Windows, the
current folder, PATH): the same places `NativeLibrary.TryLoad("vcruntime140.dll")` searches from
a program in the suite folder.

Options put to the owner on 2026-10-31: name the runtime as a prerequisite and have the launcher
check for it (recommended); the prerequisite text only; ship the four DLLs from Visual Studio's
redist folder (allowed by the redistribution list, but the Visual Studio licence's section 4 asks
"external end users to agree to terms that protect the Distributable Code at least as much as
this agreement", Microsoft advises against local copies "for servicing reasons", and the release
would need Visual Studio's C++ workload on the build machine). The owner chose the prerequisite
and the launcher's check.

## Steps

Branch `claude/Warn-about-the-missing-VC-runtime`, worktree
`.claude/worktrees/Warn-about-the-missing-VC-runtime`. Requirements first, then tests, then code.

1. **Requirements** (`docs/requirements.md`):
   - **R-009**: `Prerequisites.Details` has four paragraphs; the third names Microsoft's Visual
     C++ 2015-2022 runtime (x64), which most computers have, needed by DarksCheck, FlatCheck and
     LightsCheck to read camera raw files, says the launcher tells when it is missing, and gives
     `https://aka.ms/vc14/vc_redist.x64.exe` (Microsoft's permalink for the latest x64 package).
     The summary line stays as it is.
   - **R-018** (new): at start the launcher asks Windows to load `vcruntime140.dll`,
     `vcruntime140_1.dll`, `msvcp140.dll` and `vcomp140.dll`, the way a program in the suite
     folder finds them (`VcRuntime.Missing`, given the window's loader). When one does not load,
     the main window shows under the prerequisites line, in the warning colour and the current
     language, that DarksCheck, FlatCheck and LightsCheck cannot read camera raw files until the
     runtime is installed, followed by the link, which opens in the browser; when all four load,
     the line is not shown.
   - **R-012**: the log's list gains the check's outcome (Info when the four load, Warn naming
     those that do not) and the link opened, or its exception.
2. **Tests first**: `LanguageTests.R009_...` with four paragraphs and the runtime's facts; a new
   `VcRuntimeTests` with `R018_` tests of `Missing` (a fake loader), of the warning's two texts and
   of the log line.
3. **Core**: `src/AstroDarkroom.Core/VcRuntime.cs` (`Files`, `DownloadUrl`, `Missing`, `Warning`);
   the new paragraph in `Prerequisites.Details`; `LogText.VcRuntime`.
4. **Window**: `NativeProbe.CanLoad` (`NativeLibrary.TryLoad`, then `Free`); `App.OnStartup` runs
   the check once, logs it and passes the result to `MainWindow`, which shows a `RuntimeWarning`
   line under `PrerequisitesLine` (`WarnBr`, wrapping, collapsed unless something is missing): a
   `Run` from `VcRuntime.Warning` and a `Hyperlink` to `VcRuntime.DownloadUrl` that opens the
   browser (`UseShellExecute`, the URI's `OriginalString`), logged.
5. **Docs**: ADR 0016; `launcher.md`; `logging.md`; `README.md`; the root `README.md`;
   `CHANGELOG.md`; `CLAUDE.md`.
6. **Wrap-up**: `/wrap-up`, a subagent review, the merge proposal.

## Verification

1. `scripts/verify.ps1` green, after a `--no-incremental -warnaserror` build.
2. Teeth: `Missing` that ignores its loader, or a warning without the three tools' names, turns
   the matching test red; restored after.
3. The main window rendered offscreen in English and Romanian with `vcomp140.dll` missing, and
   with nothing missing (no line); "Despre" with its fourth paragraph, its height measured
   against today's 931 / 946 DIP.
4. The real probe on this PC: the launcher started from a published suite folder when the owner
   is away from the PC; its log says the four DLLs load and the window shows no warning.

Follow-ups, to join each tool's About task: DarksCheck, FlatCheck and LightsCheck's "cannot load
libraw" message names the Visual C++ runtime and its link instead of advice about publishing.

## What deviated

- **The link's failure path.** Beyond the plan, a browser that will not start leaves a message
  box with the address (in both languages) and an Error line, as `OpenApp` does for an app;
  R-018 says so.
- **Measured for the ADR:** the four loads take 15.2 to 17.2 ms in a fresh process (three runs),
  most of it `vcruntime140_1.dll`.
- **Verification.** Gate green (108 tests); a `Missing` that ignores its loader and a warning
  without the three tools' names each turned an R-018 test red; the suite published from the
  worktree (370 files, no collision); the main window rendered offscreen: 704 DIP of content
  without the warning, 740 with it (the line wraps to two, 30 DIP), the same in both languages;
  "Despre" 997 DIP with its fourth paragraph (931 / 946 before), under a 1080p screen's 1040 DIP
  work area. The live start of the launcher from the published folder waited: the owner was at
  the PC. The probe's own call was measured in a scratch process (all four DLLs load here).
- **The review's findings, taken** (a subagent review of 2fab20f7, which found the four DLLs
  and the search order right). ADR 0016 supersedes in part ADR 0007's "the launcher checks none
  of it". While the warning shows, each activation of the window asks again, so a user back
  from the installer no longer sees a stale warning. The probe makes the suite folder the
  current folder, as the tools have it, so a launcher started elsewhere cannot find a copy the
  tools would not. A new `SuiteTests` check holds the windows that resolve
  `Sdcb.LibRaw.runtime.win64` to 0.21.1, whose imports `VcRuntime.Files` lists, and the warning
  and the log line to naming exactly those windows. The runtime is named v14, Microsoft's name
  for it (Windows lists "v14 Redistributable", or "2015-2022" when older), and the warning says
  "missing or too old", since an older runtime can lack one of the four (the review: the 2015
  and 2017 packages have no `vcruntime140_1.dll`; not checked here). ADR 0016 names
  Sdcb.LibRaw's resolver and says the probe in the real executable is checked by hand only.
  Nits: About's comment, the README's "one line under the title", a blank line in the
  CHANGELOG. Not taken: each DLL's path and version in the launcher's log (ADR 0016's
  alternatives).
- **Verification after the review.** Gate green, 109 tests. The new suite check turned red when
  DarksCheck's lock file said 0.21.2, and when LightsCheck's no longer listed the package
  (restored after). With "or too old" the warning wrapped to three lines and broke the link in
  two; "from this computer" was dropped, and it is two lines again (30 DIP, the link whole) in
  both languages: the main window's content 740 DIP with it, 704 without. "Despre" is 983 / 998
  DIP of content (Romanian / English), 1012 / 1027 with the frame, under 1040. The live start
  ran with the PC locked and idle: the launcher, published from the worktree, logged `App Visual
  C++ runtime: vcruntime140.dll, vcruntime140_1.dll, msvcp140.dll, vcomp140.dll load`, its
  window had no `RuntimeWarning` (UI Automation), and it closed with exit code 0. The link and
  the activation check cannot be seen on this PC, which has the runtime.
