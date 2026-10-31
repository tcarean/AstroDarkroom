# 0016. The launcher names the Visual C++ runtime as a prerequisite and warns when it is missing

Date: 2026-10-31
Status: accepted 2026-10-31

## Context

LibRaw's DLLs in the suite folder, from `Sdcb.LibRaw.runtime.win64` 0.21.1 (compiled by vcpkg,
ADR 0014), need the Visual C++ 2015-2022 runtime for x64: `raw_r.dll` imports
`VCRUNTIME140.dll`, `VCRUNTIME140_1.dll`, `MSVCP140.dll` and OpenMP's `VCOMP140.DLL`, and
`jpeg8.dll`, `lcms2.dll` and `zlib1.dll` import `VCRUNTIME140.dll`. The suite folder ships none
of them; WPF's runtime pack carries its own renamed copy, `vcruntime140_cor3.dll`, which nothing
else loads. No document named the runtime as a prerequisite.

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

.NET 8 loads a NuGet package's native library by its full path with
`LOAD_WITH_ALTERED_SEARCH_PATH` (`src/coreclr/vm/nativelibrary.cpp`, release/8.0), so
`raw_r.dll`'s dependencies are looked for in its own folder, the suite folder, then in Windows'
standard order; `NativeLibrary.TryLoad`, given a bare file name by a program in the suite folder,
searches the same places.

The options, put to the owner on 2026-10-31 with a recommendation: name the runtime as a
prerequisite and have the launcher check for it; the prerequisite text only; ship the runtime's
four DLLs. The owner chose the first.

## Decision

- **A prerequisite.** `Prerequisites.Details` gains a third paragraph, before the optional Adobe
  profiles: Microsoft's Visual C++ 2015-2022 runtime (x64), which most computers have, needed by
  DarksCheck, FlatCheck and LightsCheck to read camera raw files, with Microsoft's permalink for
  the latest supported x64 package, `https://aka.ms/vc14/vc_redist.x64.exe` (R-009). The READMEs
  say the same.
- **A check at start.** `App.OnStartup` asks Windows once to load the four DLLs by name
  (`NativeProbe.CanLoad`: `NativeLibrary.TryLoad`, the DLL released at once), `VcRuntime.Missing`
  in the core keeping the list and its order, and writes one log line, Info when the four load,
  Warn naming those that do not (R-012). When one does not load, the main window shows a line
  under the prerequisites line, in the warning colour: the three tools cannot read camera raw
  files until the runtime is installed, then Microsoft's link, which opens in the browser
  through the shell; a browser that will not start leaves a message box with the address (R-018).
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

## Consequences

The check costs about 16 ms at start (three fresh processes on this PC: 15.2 to 17.2 ms for the
four, most of it `vcruntime140_1.dll`). On a computer without the runtime, the main window is a
line or two taller; "Despre" has a fourth paragraph. The check is right as long as the tools load
LibRaw as .NET 8 does today, by full path with the standard search for its dependencies; a tool
that loaded it otherwise would need the check revisited. The window has no tests (ADR 0001):
`VcRuntime` is tested with a fake loader, the warning line by rendering the window offscreen with
a missing DLL, and the probe itself by starting the launcher from a published suite folder,
whose log says what loaded. DarksCheck, FlatCheck and LightsCheck keep their "cannot load
libraw" message until their follow-up tasks name the runtime and its link there.
