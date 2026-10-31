# 0016. The launcher names the Visual C++ runtime as a prerequisite and warns when it is missing

Date: 2026-10-31
Status: accepted 2026-10-31
Supersedes: 0007 in part (the launcher checking none of the prerequisites and never looking outside its folder)

## Context

LibRaw's DLLs in the suite folder, from `Sdcb.LibRaw.runtime.win64` 0.21.1 (compiled by vcpkg,
ADR 0014), need Microsoft's Visual C++ v14 runtime for x64: `raw_r.dll` imports
`VCRUNTIME140.dll`, `VCRUNTIME140_1.dll`, `MSVCP140.dll` and OpenMP's `VCOMP140.DLL`, and
`jpeg8.dll`, `lcms2.dll` and `zlib1.dll` import `VCRUNTIME140.dll`. The suite folder ships none
of them; WPF's runtime pack carries its own renamed copy, `vcruntime140_cor3.dll`, which nothing
else loads. No document named the runtime as a prerequisite. Microsoft calls it the "Visual C++
v14 Redistributable", for programs built with Visual Studio 2017 to 2026, and asks for one "at
least as recent as the build tools used to build your app" (its page of the latest supported
downloads, read on 2026-10-31); Windows lists this PC's as "Microsoft Visual C++ v14
Redistributable (x64) - 14.51.36247", and older installations as "2015-2022", "2015-2019",
"2017" or "2015". The last two came before Visual Studio 2019, which added `vcruntime140_1.dll`
(OpenJDK's JDK-8242507).

Measured on 2026-10-31 with a scratch loader that keeps `System32`'s copies out
(`LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR`): LibRaw's four DLLs alone in a folder all fail with error
126, "The specified module could not be found"; with the runtime's four DLLs beside them, all
load and `libraw_version()` is `0.21.1-Release`. On a computer without the runtime, DarksCheck,
FlatCheck and LightsCheck cannot read camera raw files (their FITS modes still work), and their
message, "cannot load libraw ... The native DLLs (raw_r.dll and its neighbours) must sit beside
flatcheck.exe ... publish with IncludeNativeLibrariesForSelfExtract", points at a developer's
publish, not at the runtime. The launcher, ImageVisualCompare, CopyWcs and AstroPhotoAnalyzer do
not need it. A fresh Windows could not be measured: this PC is Windows 10 Home, without Windows
Sandbox, with the runtime 14.51.36247 installed.

The tools reach `raw_r.dll` through Sdcb.LibRaw's own resolver, `LibRawNativeLoader`, which asks
`NativeLibrary.TryLoad` for `raw_r.dll` and, failing that, `NativeLibrary.Load` for `libraw.dll`.
.NET 8 finds the first in the suite folder and loads it by its full path with
`LOAD_WITH_ALTERED_SEARCH_PATH` (`src/coreclr/vm/nativelibrary.cpp`, release/8.0), so `raw_r.dll`'s
dependencies are looked for in its own folder, the suite folder, then in Windows' standard order,
whose current folder is the suite folder for a tool the launcher starts. `NativeLibrary.TryLoad`,
given a bare file name by a program in the suite folder whose current folder is the suite folder,
searches the same places.

The options, put to the owner on 2026-10-31 with a recommendation: name the runtime as a
prerequisite and have the launcher check for it; the prerequisite text only; ship the runtime's
four DLLs. The owner chose the first.

## Decision

- **A prerequisite.** `Prerequisites.Details` gains a third paragraph, before the optional Adobe
  profiles: Microsoft's Visual C++ v14 runtime (x64), which most computers have, needed by
  DarksCheck, FlatCheck and LightsCheck to read camera raw files, with Microsoft's permalink for
  the latest supported x64 package, `https://aka.ms/vc14/vc_redist.x64.exe` (R-009). The READMEs
  say the same, with the names Windows lists the runtime under.
- **A check at start.** `App.OnStartup` asks Windows to load the four DLLs by name
  (`NativeProbe.MissingRuntime`: `NativeLibrary.TryLoad` with the suite folder as the current
  folder, each DLL released at once), `VcRuntime.Missing` in the core keeping the list and its
  order, and writes one log line, Info when the four load, Warn naming those that do not
  (R-012). When one does not load, the runtime is missing or too old to have all four, and the
  main window shows a line under the prerequisites line, in the warning colour: the three tools
  cannot read camera raw files until the runtime's latest version is installed, then
  Microsoft's link, which opens in the browser through the shell; a browser that will not start
  leaves a message box with the address (R-018).
- **Again while the line shows.** Each activation of the main window asks again, so the line
  goes when the user comes back from installing the runtime; an answer that changed is logged.
- **Tied to the LibRaw build.** The four are the imports of the DLLs of
  `Sdcb.LibRaw.runtime.win64` 0.21.1, and the search the probe copies is the one the resolver
  of `Sdcb.LibRaw` 0.21.1.7 makes .NET use. `SuiteTests` holds every window that resolves
  either package to that version, and the warning, the log line and the prerequisite paragraph
  to naming exactly those windows, so a new LibRaw has its DLLs and its resolver read again
  before it ships.
- **Nothing of Microsoft's in the zip**, and the three tools unchanged here.

## Alternatives rejected

- **Ship the four DLLs, from Visual Studio's `VC\redist` folder (14.44.35211).** The
  redistribution list allows them, unmodified, with a program, to licensed Visual Studio users;
  but section 4 of the Visual Studio 2022 Community licence asks distributors to "require
  distributors and external end users to agree to terms that protect the Distributable Code at
  least as much as this agreement", which a zip opened without accepting any licence does not;
  Microsoft advises against copies beside the program "for servicing reasons" (Windows Update
  does not reach them); and every release would need Visual Studio's C++ workload on the
  building machine.
- **The prerequisite text alone.** The user would learn of the runtime only when a tool refuses a
  raw file, and then from that tool's misleading message.
- **The tools explaining it, without the launcher.** The launcher is where the suite says what it
  needs (R-009, ADR 0007); the tools' message is mended in each tool, as a follow-up.
- **A P/Invoke of `LoadLibraryExW` in the window.** `NativeLibrary.TryLoad` already asks Windows'
  standard search for a bare name, with no new unsafe code.
- **The check at start only.** A user who installed the runtime from the link would come back to
  a warning that it is still missing, until the launcher is started again.
- **The current folder left as the launcher found it.** A launcher started from a folder holding a
  copy of the runtime would find it there, where the tools, started in the suite folder, do not
  look.
- **Each DLL's path and version in the launcher's log.** A list of the process's modules read at
  each check, for a mismatch the current folder above closes; when a tool fails to load LibRaw,
  where Windows looked belongs in that tool's own log, with its follow-up.

## Consequences

The check costs about 16 ms at start when the four load (three fresh processes on this PC: 15.2
to 17.2 ms, most of it `vcruntime140_1.dll`); on a computer with the runtime nothing is asked
after the start. A DLL that does not load is looked for along the whole search, PATH included,
at start and at each activation of the window while the warning shows: four misses took 25 to
26 ms the first time and 12 ms when repeated, on this PC with 55 folders on PATH (three fresh
processes, measured by the review). The time grows with PATH, and a network folder on PATH that
does not answer would hold the window as long as it would hold a tool loading LibRaw. On a
computer without the runtime the main window is a line or two taller; "Despre" has a fourth
paragraph. A runtime that has the four DLLs but is older than the build tools of LibRaw's
(14.36) passes the check, though Microsoft asks for one at least as recent; whether LibRaw then
loads was not measured. The check is right as long as the tools load LibRaw as they do today,
through `LibRawNativeLoader` and .NET 8, by full path with the standard search for its
dependencies; a new version of either package, which `SuiteTests` stops, or a tool that loaded
LibRaw otherwise, needs the check revisited.

The window has no tests (ADR 0001): `VcRuntime` is tested with a fake loader and the warning line
was checked by rendering the window offscreen with a missing DLL; the probe inside
`AstroDarkroom.exe` is checked by hand only, by starting the launcher from a published suite
folder and reading its log, and on a computer with the runtime the line, its link and its going
away cannot be seen that way. DarksCheck, FlatCheck and LightsCheck keep their "cannot load
libraw" message, whose exception names `libraw.dll`, the resolver's last try, until their
follow-up tasks name the runtime and its link there.
