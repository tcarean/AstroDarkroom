# 0026. LibRaw's load failure names the Visual C++ runtime in LightsCheck's words, and the reader logs which of its DLLs Windows cannot load

Date: 2026-11-01
Status: accepted 2026-11-02

## Context

LibRaw's DLLs, from `Sdcb.LibRaw.runtime.win64` 0.21.1, import Microsoft's Visual C++ v14
runtime (x64): `raw_r.dll` imports `vcruntime140.dll`, `vcruntime140_1.dll`, `msvcp140.dll` and
`vcomp140.dll`, and `jpeg8.dll`, `lcms2.dll` and `zlib1.dll` import `vcruntime140.dll`. Their
import tables were read on 2026-11-01, as AstroDarkroom's ADR 0016 read them the day before. The
2015 and 2017 runtimes lack `vcruntime140_1.dll`, which Visual Studio 2019 added.

Neither DarksCheck's release nor the Astro Darkroom folder ships that runtime. Read from their
manifests on 2026-11-01, the single files of `kit.json` bundle LibRaw's four DLLs, and the
window's also bundles WPF's own `vcruntime140_cor3.dll`: none of the four. Without the runtime,
no raw could be read. The error then said that "the native DLLs (raw_r.dll and its neighbours)
must sit beside darkscheck.exe" and to "publish with IncludeNativeLibrariesForSelfExtract or copy
the whole folder". That is advice for a developer. It is wrong for a user who has the DLLs and
lacks the runtime, and wrong where the DLLs are inside the single file.

The loader's own text, which the error quoted, never names the runtime. `Sdcb.LibRaw`'s resolver
tries `raw_r.dll` and then `NativeLibrary.Load("libraw.dll")`. Measured on a copy of the console
without `raw_r.dll`, and on one whose `raw_r.dll` imports a DLL that does not exist, as a missing
runtime would: both gave `Unable to load DLL 'libraw.dll' or one of its dependencies ...
(0x8007007E)`. A single file copied without its DLLs gave only `Dll was not found.`

On 2026-11-01 the owner chose no hint for developers in the message, and a log line at the first
failure saying which of the runtime's DLLs Windows cannot load.

DarksCheck's reader is a copy of LightsCheck's, and its code changes there first (the rule
"Copied files stay copies"). This task's first version therefore wrote the probe outside the
copy: `VcRuntime`, in `Logging/`, called from the two places that log a frame's failure, its line
giving each DLL's version and path. During the task's third review, on 2026-11-01, LightsCheck
merged its own answer to the same request (its ADR 0030): the check inside its reader, and a
message with a note on FITS frames. On 2026-11-02 the owner chose to take LightsCheck's code and
its words.

## Decision

- **LightsCheck's hunk, taken word for word.** `RawReader.Missing(inner)` calls
  `Missing(inner, Runtime)`. Its pair is LightsCheck's, English and Romanian, and the code
  appends Microsoft's link to it (R-110):
  - English: `cannot load libraw: Microsoft's Visual C++ runtime is likely missing or too old, so
    camera raw files cannot be read until its latest version is installed (FITS frames are not
    affected): https://aka.ms/vc14/vc_redist.x64.exe`
  - Romanian: `nu pot incarca libraw: probabil runtime-ul Microsoft Visual C++ lipseste sau este
    prea vechi, deci nu pot citi fisiere raw de aparat pana nu instalezi ultima lui versiune
    (cadrele FITS nu sunt afectate): https://aka.ms/vc14/vc_redist.x64.exe`
- **What the message leaves out.** It names no executable, folder, DLL or publish option, so it
  holds for the single file and the suite's folder alike. The loader's exception stays the inner
  one, which the log writes with the frame's line.
- **The runtime check, once per process.** `RuntimeCheck` asks Windows to load the four DLLs by
  name (`NativeLibrary.TryLoad`, each released at once) at the first failure, and the reader
  writes one Warn line. The line names the DLLs that do not load, or says that the four load, so
  none is missing. It claims no more, since a runtime too old for LibRaw's DLLs may still have the
  four. Later failures ask nothing.
- **A hunk, not a whole refresh.** The code is LightsCheck's, word for word; the comments are
  DarksCheck's (ADR 0019), as with FlatCheck's hunks in ADRs 0020 and 0023. LightsCheck's other
  later hunks of the reader stay out, as `docs/design/imaging.md` says.

## Alternatives rejected

- **DarksCheck's own probe outside the copy** (this task's first version). The reader would
  differ from its source on purpose. The next refresh would bring LightsCheck's check beside it:
  two runtime lines per process, and the link twice in every message.
- **DarksCheck's own wording**, the launcher's without the FITS note. Two tools of one suite
  would word the same failure two ways.
- **A sentence for whoever built DarksCheck.** It would appear in every raw's row and three times
  in the run's error line, for a case only a hand-made publish reaches.
- **The loader's text in brackets, as before.** It names `libraw.dll` whatever is missing.
- **The check at every failure.** LightsCheck counted 174 failures for one session of its own,
  87 raws failing twice each, and the launcher measured 12 to 26 ms for each check (LightsCheck's
  ADR 0030, AstroDarkroom's ADR 0016): seconds per run for the same answer each time.
- **Shipping the runtime.** AstroDarkroom's ADR 0016 rejected it for the suite, and the same
  holds here: the Visual Studio licence's terms for distributors, and Microsoft's advice against
  copies beside a program.

## Consequences

- **`LibRawLoadTests`** holds LightsCheck's five tests of the hunk, under DarksCheck's names:
  - the words in both languages and the inner exception;
  - the one check per process and its line, for missing DLLs and for none missing;
  - the four names read from the import tables of the DLLs the build brings, at every run.

  It also runs a copy of the console without `raw_r.dll`, out of process: every row carries the
  message, the run's error too, exit 1, one runtime line per run in the log.
- **A later refresh of the reader from LightsCheck** keeps this hunk as it is.
- **FlatCheck** words the message and logs the runtime its own way, in its own task.
