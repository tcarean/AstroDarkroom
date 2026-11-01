# LightsCheck: the libraw load message names the Visual C++ runtime; About lists what the tool ships

Approved by the owner on 2026-11-01 as written, after their three answers below. Written for an
implementer who has this file, `CLAUDE.md`, the repository and the AstroDarkroom checkout beside it.

Branch `claude/Fix-LightsCheck-raw-message-and-About` (the project in the name: the same request
runs in DarksCheck's and FlatCheck's sessions, and branch names are repo-wide), worktree
`LightsCheck/.claude/worktrees/Fix-LightsCheck-raw-message-and-About`, from `main` (c5be60cd).
This plan is saved as `docs/plans/2026-11-01-fix-lightscheck-raw-message-and-about.md`.

## Context

Two changes left over from the Astro Darkroom suite work (AstroDarkroom ADRs 0014 and 0016,
R-017 and R-018):

1. When libraw cannot load, `RawReader.Missing` tells the user that the native DLLs must sit
   beside `lightscheck.exe` and to publish with `IncludeNativeLibrariesForSelfExtract`. That is
   developer advice. It is wrong for a user who has the DLLs but lacks Microsoft's Visual C++ v14
   runtime, which the DLLs import and which neither form of the tool ships. It also appends the
   loader's words, which name `libraw.dll`.
2. About lists three components. It must list what the tool ships, as the suite's
   `THIRD-PARTY-NOTICES.txt` does: LibRaw and, apart from it, the libraries inside its DLLs.
   Its pointer to the licence texts must be true from the suite folder and from the own release.

### Facts checked on 2026-11-01 (all of the brief's facts match)

- `Sdcb.LibRaw.runtime.win64` 0.21.1 holds `raw_r.dll`, `jpeg8.dll`, `lcms2.dll` and
  `zlib1.dll`, all x64. Read from their import tables:
  - `raw_r.dll` imports `VCRUNTIME140`, `VCRUNTIME140_1`, `MSVCP140` and `VCOMP140`.
  - The other three import `VCRUNTIME140`.
  - Besides these, the four import only the UCRT `api-ms-win-crt-*` DLLs, `KERNEL32` and
    `WS2_32`, all part of Windows.
- `Sdcb.LibRaw` 0.21.1.7 holds `LibRawNativeLoader`, `SetDllImportResolver`, `TryLoad`, and the
  literals `raw_r.dll` and `libraw.dll`.
- The own release (the 1.0.0 single-file exes, parsed bundle manifests) bundles the four LibRaw
  DLLs and extracts them at run time to `%TEMP%\.net\lightscheck\<id>\`, not beside the exe.
  The window's bundle also has WPF's renamed `vcruntime140_cor3.dll`. Neither exe has any of
  the four runtime DLLs.
- The suite folder (`AstroDarkroom/artifacts/0.9.0`) has LibRaw's four DLLs beside
  `LightsCheck.Wpf.exe`, plus `vcruntime140_cor3.dll`, and none of the four runtime DLLs.
- The own release is `artifacts\<v>\console\` and `wpf\` (exe, pdb, xml) plus `release.json`,
  with no licence text. The README says it is built from the source, and the console is not in
  the suite.
- A missing libraw fails every frame of a series run twice: the measurement, then its metadata.
  On the Veil session that is 174 failures.
- About's intro paragraph says "two components, both compatible", but lists three. It went
  stale when NLog was added (2026-10-30).

### The owner's answers (2026-11-01)

- **No developer hint.** The message names the runtime and Microsoft's link only. The loader's
  words stay in the log, with the exception.
- **The runtime probe in the log, once per process.** At the first libraw failure the reader
  asks Windows for the four runtime DLLs by name, as the launcher's `NativeProbe` does, and
  writes one Warn line.
- **No .NET row in About.** Six rows, as the launcher's About (R-015) leaves out the runtime it
  ships.

## Wording

The message is an `InvalidOperationException`. The loader's exception is its inner one and is
logged as today. The message ends with the link, as the launcher's warning does:

- EN: `cannot load libraw: Microsoft's Visual C++ runtime is likely missing or too old, so camera raw files cannot be read until its latest version is installed (FITS frames are not affected): https://aka.ms/vc14/vc_redist.x64.exe`
- RO: `nu pot incarca libraw: probabil runtime-ul Microsoft Visual C++ lipseste sau este prea vechi, deci nu pot citi fisiere raw de aparat pana nu instalezi ultima lui versiune (cadrele FITS nu sunt afectate): https://aka.ms/vc14/vc_redist.x64.exe`

The log line, Warn, once per process:

- `libraw cannot be loaded; Visual C++ runtime: missing [vcruntime140_1.dll, vcomp140.dll]`
- or `libraw cannot be loaded; Visual C++ runtime: [vcruntime140.dll, vcruntime140_1.dll, msvcp140.dll, vcomp140.dll] load, so it is not the cause`

About's intro paragraph moves to the core as `ThirdParty.Lead`, beside the list it counts, as
the launcher's `ThirdParty.Lead`:

- EN: `The tool ships six components written by others, all compatible with the GPLv3. The FITS reader, the PNG encoder, every calculation and every threshold are written by hand. Each licence's whole text, and why it is compatible, is in THIRD-PARTY-NOTICES.txt beside the program in the Astro Darkroom suite folder, and in the source in AstroDarkroom/THIRD-PARTY-NOTICES.md.`
- RO: `Unealta livreaza sase componente scrise de altii, toate compatibile cu GPLv3. Cititorul FITS, encoderul PNG, toate calculele si toate pragurile sunt scrise de mana. Textul intreg al fiecarei licente, si motivul pentru care e compatibila, sunt in THIRD-PARTY-NOTICES.txt, langa program in folderul suitei Astro Darkroom, si in sursa, in AstroDarkroom/THIRD-PARTY-NOTICES.md.`

The pointer is true from both places: the suite folder has the file beside the program, and the
own release is built from the repository that holds `AstroDarkroom/THIRD-PARTY-NOTICES.md`.

The rows, in order. The copyright lines are the ones the suite file quotes; each new role names
its DLL.

| Name | Licence | Copyright | Url |
| --- | --- | --- | --- |
| Sdcb.LibRaw | MIT | unchanged | unchanged |
| LibRaw | LGPL-2.1, unchanged | unchanged | unchanged |
| libjpeg-turbo | IJG, BSD-3-Clause and zlib | `Copyright (C)2009-2022 D. R. Commander, (C)2015 Viktor Szathmáry; (C) 1991-2020 Thomas G. Lane, Guido Vollbeding` | `https://libjpeg-turbo.org/` |
| Little CMS | MIT | `Copyright (c) 1998-2020 Marti Maria Saguer` | `https://www.littlecms.com/` |
| zlib | zlib | `Copyright (C) 1995-2017 Jean-loup Gailly and Mark Adler` | `https://zlib.net/` |
| NLog | BSD-3-Clause | unchanged | unchanged |

- LibRaw's role now names `raw_r.dll` and says that it calls the next three.
- libjpeg-turbo's role carries the acknowledgement the IJG licence asks of a binary
  distribution's documentation: "this software is based in part on the work of the Independent
  JPEG Group" (RO: "programul se bazeaza in parte pe munca Independent JPEG Group").
- The three new rows claim `Sdcb.LibRaw.runtime.win64`. The Linux package's files are not
  shipped: both forms of the tool are win-x64.

## Steps

### 1. Worktree, branch, plan

```
git worktree add .claude/worktrees/Fix-LightsCheck-raw-message-and-About -b claude/Fix-LightsCheck-raw-message-and-About main
```

Run it from `LightsCheck/`, after `git branch --list 'claude/*'`. Then save this plan.

### 2. Requirements (`docs/requirements.md`)

- **R-074, new, under "Reading frames".** When libraw cannot be loaded, reading a raw throws
  `InvalidOperationException` with the message above. It is in the run's language, the Romanian
  without diacritics, and ends with the link. Libraw cannot be loaded when the reader catches a
  `DllNotFoundException`, a `FileNotFoundException` for another file than the raw, a
  `BadImageFormatException` or a `TypeInitializationException`.
  - The message names no executable, folder or publish option, so it holds in the single-file
    release and in the suite folder alike. The loader's exception is the inner one.
  - Once per process, at the first such failure, the log gets the Warn line above: the runtime
    DLLs Windows cannot load, asked by name in the tool's own process.
  - The four DLLs are the imports of `Sdcb.LibRaw.runtime.win64` 0.21.1's DLLs.
- **R-075, new, under "Repository and legal".**
  - About lists the six components, in the order above, each with its role, licence, copyright
    line and address: what the suite's `THIRD-PARTY-NOTICES.txt` names for LightsCheck, without
    .NET and WPF.
  - `ThirdParty.Lead` counts them in words, "six" or "sase", and points to the licence texts as
    above.
  - libjpeg-turbo's row credits the Independent JPEG Group in both languages.
- **R-010, rewritten.**
  - Every linked package is claimed, and every claimed package is linked.
  - Each native DLL the build brings for Windows (`runtimes/win-x64/native`) is named, in both
    languages, in the role of exactly one component: `raw_r.dll` LibRaw, `jpeg8.dll`
    libjpeg-turbo, `lcms2.dll` Little CMS, `zlib1.dll` zlib. These four claim
    `Sdcb.LibRaw.runtime.win64`.
  - Any other package is claimed by exactly one component. The notices heading and LibRaw's LGPL
    stay as they are.
- **R-007.** "A raw run exits 1 with a message naming what is missing" becomes "with the message
  of R-074".
- **R-071.** It adds the runtime line of R-074 to what the log holds.

### 3. Tests, written first and seen failing

`tests/LightsCheck.Tests/RawReaderTests.cs`:

- `R074_TheLoadFailureNamesTheVisualCppRuntimeAndItsLink_InBothLanguages` checks the following:
  - the exact EN and RO texts, through `Ui.Use`;
  - the message ends with the URL;
  - the inner exception is kept;
  - no `IncludeNativeLibrariesForSelfExtract`, `lightscheck.exe` or `libraw.dll` in the message;
  - the Romanian is ASCII.
- `R074_TheRuntimeIsAskedForOnce_AndTheLogNamesTheDllsThatDoNotLoad` uses a `RuntimeCheck` with a
  fake loader and a `LogCapture`. Two failures give one Warn line naming the missing DLLs, and
  each DLL is asked once.
- `R074_WhenTheFourLoad_TheLogSaysTheRuntimeIsNotTheCause`.
- `R074_TheRuntimeFilesAreWhatLibrawsDllsImport` reads the import tables of the DLLs in the
  test's `runtimes/win-x64/native` with `System.Reflection.PortableExecutable.PEReader`. The
  `vcruntime*`, `msvcp*` and `vcomp*` names it finds must equal `RawReader.RuntimeFiles`, ignoring
  case, so a new LibRaw build turns this red until the list is read again. It runs on Linux too.

`tests/LightsCheck.Tests/ThirdPartyTests.cs`:

- `R010_EveryPackageTheCoreLinksIsListedExactlyOnce` becomes
  `R010_EveryLinkedPackageIsClaimed_AndOnlyTheNativeRuntimeOnesByMoreThanOne`. This is a
  requirement change, reported at wrap-up.
- `R010_EachNativeLibraryOfTheWindowsBuildIsNamedByExactlyOneComponent_InBothLanguages` checks
  the test's `runtimes/win-x64/native`. The folder must exist and hold at least one DLL, so the
  test cannot pass vacuously.
- `EveryComponentNamesItsLicenceAndUrl` changes its count from 3 to 6, tied to R-075.
- `R075_TheComponentsAreTheSixTheToolShips_InOrder`.
- `R075_TheLeadCountsTheComponentsInWordsAndPointsToTheLicenceTexts_InBothLanguages` uses a
  number-word table: one to ten, and "una" to "zece" in Romanian. Adding a component without
  updating the count turns it red, which today's "two" shows was needed.
- `R075_LibjpegTurbosRowCreditsTheIndependentJpegGroup_InBothLanguages`.

`tests/LightsCheck.Wpf.Tests/AboutTests.cs`: `EveryHeadingAndLeadIsWritten` asserts
`vm.ThirdPartyLead == ThirdParty.Lead`, replacing the old `Contains("THIRD-PARTY-NOTICES.md")`.

### 4. Code

`src/LightsCheck.Core/Imaging/RawReader.cs` is "adapted", so it is LightsCheck's to change. It
keeps FlatCheck's style and `#nullable disable`, and no field of a libraw type. It gains:

- `RuntimeUrl`, a const, and `RuntimeFiles`, the four DLL names.
- `MissingRuntime(Func<string, bool> canLoad)`, which keeps the order of `RuntimeFiles`.
- `RuntimeLine(IReadOnlyList<string> missing)`, which uses `LogText.List`.
- A nested `RuntimeCheck`: a loader and an `Interlocked` flag, with `FirstLine()` returning the
  line once and null after.
- A static instance `Runtime = new RuntimeCheck(CanLoad)`.
- `CanLoad(file)`: `NativeLibrary.TryLoad` by bare name, then `Free`.
- `Missing(inner)`, which becomes `Missing(inner, Runtime)`: it writes the first line at Warn,
  then returns the new message.

The catch blocks are unchanged. The header comment says where the DLLs travel (beside the exe,
or inside the single file and extracted) and what they need. XML docs go on every member added
or edited.

Other files:

- `src/LightsCheck.Core/ThirdParty.cs`: the three components, LibRaw's role, `Lead`, and the class
  summary (the new R-010 rule, where the whole texts are).
- `src/LightsCheck.Wpf/ViewModels/AboutViewModel.cs`: `ThirdPartyLead => ThirdParty.Lead`.
- `src/LightsCheck.Wpf/ViewModels/WindowTexts.cs`: remove `AboutThirdPartyLead`.
- `src/LightsCheck.Core/LightsCheck.Core.csproj` comment: the DLLs travel with the exe and need
  the runtime, which nothing here ships; a raw run names it (R-074).
- `Directory.Packages.props` comment on the runtime packages: a new version has its DLLs'
  imports and About's list read again (the R-074 and R-010 tests).

### 5. Docs

- `THIRD-PARTY-NOTICES.md`, kept in Romanian:
  - sections `libjpeg-turbo`, `Little CMS` and `zlib`, each with role, version as the suite
    measured it, licence, compatibility and project, and for libjpeg-turbo the IJG sentence;
  - LibRaw's section says `raw_r.dll` is LibRaw and the other three have their own sections,
    and names the runtime they need;
  - the intro points to the suite file for the whole texts.
- `README.md`:
  - "Getting it" gets a paragraph on the runtime: the names Windows lists it under, too old
    before VS 2019, not shipped, FITS still read, and the English message quoted with its link.
  - "Building" notes that raw files need the runtime.
  - "Logs" mentions the runtime line.
  - "Licence" lists libjpeg-turbo, Little CMS and zlib and points to `THIRD-PARTY-NOTICES.txt`
    and `../AstroDarkroom/THIRD-PARTY-NOTICES.md`, with the IJG sentence.
- Design notes:
  - `imaging.md`: the load failure, the message, the once-per-process probe, and where it
    searches against where `raw_r.dll`'s imports are searched (the %TEMP% extraction folder of
    the single file).
  - `logging.md`: `RawReader`'s row in the seams table.
  - `window.md`: About's lead is the core's.
  - `language.md`: glossary rows for "runtime-ul Microsoft Visual C++" and "fisiere raw de
    aparat".
- `docs/decisions/0030-the-libraw-load-error-names-the-visual-cpp-runtime-and-about-lists-its-libraries.md`
  - Context: the facts above and AstroDarkroom ADR 0016.
  - Decision: the message, the probe once per process, the six rows, the lead in the core with
    its count in words, the pointer.
  - Alternatives rejected: the developer hint, the loader's words, a probe on every failure
    (2 to 4.5 s on Veil), the probe plus folder listing, a .NET row, a count built in code, and
    the own release shipping the notices.
  - Consequences: tied to 0.21.1 by tests. The probe searches the exe's folder where
    `raw_r.dll`'s imports search the extraction folder, which differs only for a runtime copy
    in one of them.
  - Check main's latest ADR number before the merge.
- `CHANGELOG.md`, under [Unreleased] "Changed": the message and the log line (R-074, ADR 0030),
  and About's six components, count and pointer (R-075).

## Verification

1. The new tests run red on the old code (`--filter "FullyQualifiedName~R074|FullyQualifiedName~R075|FullyQualifiedName~R010"`), then green after the code.
2. `dotnet build LightsCheck.sln -c Release --no-incremental -warnaserror --no-restore`, then
   `scripts/verify.ps1` green: format, build, all tests and `check-design`. The golden files
   should not change, since the scenarios are FITS; any changed line is a finding.
3. A real process: copy the console's build output to the scratchpad and delete `raw_r.dll`.
   Run it on a scratch folder holding a dummy `.NEF`, with `--lang en` and `--lang ro`. Expect:
   - the R-074 message after `ERROR:` / `EROARE:`, with exit 1;
   - one log line "... [4 DLLs] load, so it is not the cause" (the runtime is installed here);
   - the inner exception in the log.
4. About rendered offscreen in both languages, as `WindowLanguageTests` does, with the dumped
   PNGs viewed and the height measured. If it is taller than about 820 DIP (a 1080p screen at
   125 % minus the taskbar), report it. A fit with scrolling cards, as in APA's ADR 0070 and the
   launcher's R-016, is the owner's call, not done unasked.
5. `/wrap-up` (gate, CHANGELOG, docs, commit). Then a subagent review of `git diff main...HEAD`,
   a second review of the fix commit only, and the merge proposed (`--no-ff`), each step asked.

## Not in this task

- AstroDarkroom is untouched. Its ADR 0016's last paragraph (the tools keep their old message
  until their follow-ups) goes stale for LightsCheck once this merges. That note is for the
  owner.
- The own release ships no licence text at all, not even `LICENSE`. The pointer holds through
  the source; shipping texts with it would be a decision of its own.
- No version bump: 1.1.0 waits for the owner's `/release`.

## What deviated

- **Test names.** The two R-010 tests are
  `R010_EveryLinkedPackageIsClaimed_AndOnlyANativePackageByMoreThanOneComponent` and
  `R010_EachNativeLibraryForWindowsIsNamedByExactlyOneComponent_ThatClaimsItsPackage_InBothLanguages`.
  `EveryHeadingAndLeadIsWritten` lost its `THIRD-PARTY-NOTICES.md` line, and the equality with
  `ThirdParty.Lead` is a test of its own, `R075_TheLeadAboveTheListIsTheCores`.
- **Where the native libraries are read.** The R-010 test reads them from the core's restore
  (`src/LightsCheck.Core/obj/project.assets.json`, the `win-x64` native assets of each package),
  not from the test's bin folder. That gives each DLL's package too, so the test checks that the
  component naming it claims the package that brings it. The R-074 import test reads the bin
  folder's DLLs, as planned.
- **The red phase.** Stubs kept the old behaviour behind the new signatures until the tests had
  run. Nine tests failed for the planned reasons:
  - the old message;
  - no runtime line;
  - an empty `RuntimeFiles` against the four names read from the import tables;
  - `jpeg8.dll` named by no row;
  - the old list and lead.
  The stubs were then replaced.
- **About's height** is the one finding.
  - Measured offscreen at the window's client width: the content is 978 DIP in both languages
    (950 to 965 at 600 px). The window, with its 31 DIP of title bar and frame, is about
    1,010 DIP.
  - The three new rows add 253 DIP. Before them the window was about 740 DIP.
  - This is above the plan's 820 DIP mark. It fits this PC's 1040-DIP work area (1920 x 1080 at
    100 %), but not a 1080p screen at 125 %, where the Close button falls off the screen; Esc
    and Enter still close the window.
  - As planned, it is reported to the owner and no fit is made here.
- **Verification 3 was done as planned.** A copy of the console's build without `raw_r.dll`,
  run on two dummy `.NEF` lights, gave:
  - the R-074 message after `ERROR:` and `EROARE:`, with exit 1;
  - the same message in both inventory rows;
  - one `WARN` line per process, `RawReader libraw cannot be loaded; Visual C++ runtime:
    [vcruntime140.dll, vcruntime140_1.dll, msvcp140.dll, vcomp140.dll] load, so it is not the
    cause` (this PC has the runtime);
  - the inner `DllNotFoundException`, naming `libraw.dll`, in the log.
