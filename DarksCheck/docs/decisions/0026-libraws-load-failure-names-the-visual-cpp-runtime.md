# 0026. LibRaw's load failure names the Visual C++ runtime, and the log says which of its DLLs Windows loads

Date: 2026-11-01
Status: accepted 2026-11-01

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

The loader's exception, which the message quoted, names `libraw.dll` whatever is missing, since
`Sdcb.LibRaw`'s resolver tries `raw_r.dll` and then `NativeLibrary.Load("libraw.dll")`. Measured on
a copy of the console without `raw_r.dll`, and on one whose `raw_r.dll` imports a DLL that does
not exist, as a missing runtime would: both gave `Unable to load DLL 'libraw.dll' or one of its
dependencies: The specified module could not be found. (0x8007007E)`. The log kept that
exception and never said which DLL Windows refused.

Where the DLLs themselves can go missing was measured too:

- The single file extracts its four DLLs at start.
- The suite keeps them beside the window, with .NET.
- Only a custom publish without the flag, its exe copied alone, loses them. That gives the same
  outer message, with the inner `Dll was not found.`

On 2026-11-01 the owner chose two things, each as recommended:

- no hint for developers in the message;
- a log line at the first failure, saying which of the runtime's DLLs Windows cannot load.

## Decision

- **The message.** When LibRaw cannot be loaded, every raw's error words the launcher's warning
  (AstroDarkroom's `VcRuntime.Warning`) as the likely cause (R-110):
  - English: `cannot load LibRaw: Microsoft's Visual C++ runtime is likely missing or too old, so
    camera raw files cannot be read until its latest version is installed:
    https://aka.ms/vc14/vc_redist.x64.exe`
  - Romanian: `nu pot incarca LibRaw: runtime-ul Microsoft Visual C++ probabil lipseste sau este
    prea vechi, deci nu pot citi fisiere raw de aparat pana nu instalezi ultima lui versiune:
    https://aka.ms/vc14/vc_redist.x64.exe`
- **What the message leaves out.** It names no folder and no executable, so it holds for the
  single file and the suite's folder alike. It gives no advice on publishing; the README's
  "Building" says how the DLLs travel. The loader's exception stays the inner one, for the log.
  Its text no longer goes into the message.
- **Only the pair changes in the copied reader.** `RawReader.Missing` keeps LightsCheck's code
  and catch list. Its `T(...)` pair and the comments are DarksCheck's (ADRs 0019 and 0021), for
  LightsCheck to take back.
- **The log line.** The first time in a process that a frame's error is that failure,
  `VcRuntime` asks Windows for the four DLLs and writes one line. `VcRuntime` is DarksCheck's
  own code, in `Logging/`.
  - It loads each by name (`NativeLibrary.TryLoad`) and releases each one at once.
  - The line is at Warn, naming those that do not load, or at Info when the four load, the cause
    then being elsewhere. Each DLL found is given with its file version and its path, from the
    process's module list.
  - A probe that throws writes its reason at Warn instead.
- **Where it is called.** The two seams that log a frame's failure call it: `FrameInfo.Read`
  and `FrameStack.LogFailed`, through which the lights' loop goes too. It recognises
  RawReader's error by its inner exception, one of the four types RawReader's catch list takes.
- **Its cost, measured** in a fresh .NET 8 process: 1.5 to 6 ms for each DLL found, 4 to 7 ms
  for each miss. It is paid only after a failure, and once.

## Alternatives rejected

- **A sentence for whoever built DarksCheck.** It would appear in every raw's row and three
  times in the run's error line, for a case that only a custom publish reaches.
- **The loader's text in brackets, as before.** It names `libraw.dll` whatever is missing.
- **No probe.** A bug report would need a question back to learn which runtime the computer has.
- **A probe at every start, as the launcher's.** It would cost every run, FITS runs and `--help`
  included, for a line that matters only when a raw fails.
- **The probe inside RawReader, or the message chosen from its answer.** That is code in a copied
  file, whose fixes go to LightsCheck first (the rule "Copied files stay copies").
- **Shipping the runtime.** AstroDarkroom's ADR 0016 rejected it for the suite, and the same
  holds here: the Visual Studio licence's terms for distributors, and Microsoft's advice against
  copies beside a program.

## Consequences

- **`LibRawLoadTests`** pins:
  - the message in both languages, word for word, through RawReader's own `Missing`;
  - the probe's line and its once-per-process guard;
  - a copy of the console without `raw_r.dll`, run out of process: every row carries the
    message, the run's error too, exit 1, one runtime line in each process's log.
- **The probe's search** is Windows' standard one, from the executable's folder. LibRaw's DLLs
  look in their own folder first, which for the single file is the extraction folder. Neither
  holds a copy of the runtime, so the two answers agree unless a user puts one beside the
  executable.
- **The link** is written in both messages here, and in AstroDarkroom's `VcRuntime.DownloadUrl`.
- **LightsCheck and FlatCheck** have the same old message in their own readers; each mends it in
  its own task.
