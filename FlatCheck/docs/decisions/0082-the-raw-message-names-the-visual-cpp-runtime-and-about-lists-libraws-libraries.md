# 0082. When LibRaw cannot load, the message names the Visual C++ runtime and the log probes it; About lists LibRaw's three libraries and where every licence's whole text is

Date: 2026-11-01
Status: accepted 2026-11-01
Supersedes: 0009 in part (the message naming what is missing)

## Context

Two changes were left over from the Astro Darkroom suite: AstroDarkroom's ADR 0014 and ADR 0016,
and its R-017 and R-018.

**The message.** LibRaw's DLLs, from `Sdcb.LibRaw.runtime.win64` 0.21.1, import Microsoft's
Visual C++ v14 runtime for x64. This was read from their import tables on 2026-11-01:

- `raw_r.dll` imports `MSVCP140`, `VCOMP140`, `VCRUNTIME140` and `VCRUNTIME140_1`;
- `jpeg8.dll`, `lcms2.dll` and `zlib1.dll` import `VCRUNTIME140`.

No place the tool runs ships that runtime:

- FlatCheck's own release (`kit.json`): the console as a single file with the DLLs inside, and
  the window as a folder with them beside it;
- the Astro Darkroom folder, which carries the window only, in a flat folder
  (`artifacts/0.9.0`).

The runtimes of 2015 and 2017 lack `vcruntime140_1.dll`, which Visual Studio 2019 added.

`Sdcb.LibRaw` 0.21.1.7's resolver, `LibRawNativeLoader`, holds `raw_r.dll` and then `libraw.dll`
(its strings, read the same day). So the `DllNotFoundException` that reaches `RawReader` names no
cause. Measured on 2026-11-01, on publishes in the scratch folder with `raw_r.dll` removed, and
with a copy whose import `VCOMP140.DLL` was renamed:

- **A folder publish** (the window's, alone or in the Astro Darkroom folder): "Unable to load DLL
  'libraw.dll' or one of its dependencies: The specified module could not be found.
  (0x8007007E)", the same text whether `raw_r.dll` or its import is missing.
- **The single-file console**, self-extracting as `kit.json` publishes it: "Dll was not found."

The task that asked for this change took the first text for both.

ADR 0009 recorded a message "naming what is missing". It told the user:

- that the native DLLs must sit beside `flatcheck.exe`;
- to publish with `IncludeNativeLibrariesForSelfExtract`;
- the loader's own message, quoted.

That is a developer's advice, wrong in every release, since every release has the DLLs. Also,
the window's error panel (`TabViewModel.Describe`) appends every inner exception's message the
text does not already contain.

**The list.** The Astro Darkroom folder's `THIRD-PARTY-NOTICES.txt` names FlatCheck among the
programs that ship each of these:

- NLog, Sdcb.LibRaw and ImageSharp;
- LibRaw, libjpeg-turbo, Little CMS and zlib;
- the lensfun database;
- ".NET 8 and WPF".

About's list had three faults:

- **LibRaw's libraries.** It showed LibRaw as "the native libraries", one row, and not the
  three libraries `raw_r.dll` calls, each under its own licence.
- **The pointer.** Its lead sent the reader to `THIRD-PARTY-NOTICES.md` "beside the source".
  That file is beside neither the suite folder's program nor the own release's. The own release
  ships no notices file and no LICENSE: `release.ps1` publishes `console/`, `wpf/` and
  `release.json`.
- **ImageSharp's licence.** Its row named the Split License. Once the Split License grants the
  Apache License 2.0, it asks all documentation to name the granted licence only.

**The options**, put to the owner on 2026-11-01, each with a recommendation. The owner took the
four recommended:

1. **A developer's hint in the message.**
   - None on Windows (taken).
   - The hint after the runtime's sentence.
   - The probe choosing between the two sentences.
2. **The log naming which runtime DLLs do not load**, as the launcher's `NativeProbe` does.
   - Yes, with the folder `raw_r.dll` was found in (taken).
   - Yes, with each DLL's path and version.
   - No.
3. **The pointer to the licence texts.**
   - Both places: the file beside the program in the Astro Darkroom folder, and its address in
     the repository (taken).
   - The own release shipping the suite's file.
   - The address alone.
4. **".NET 8 and WPF" in the list.**
   - No, as in the launcher's About, which lists NLog alone though the launcher ships .NET
     too (taken).
   - Yes.

## Decision

- **The message (R-122).** `RawReader.CannotLoad` turns the four loader exceptions it catches into
  an `InvalidOperationException` with no inner exception.
  - On Windows its message names Microsoft's Visual C++ runtime, missing or too old, as the
    likely cause.
    - It uses the launcher's words (`VcRuntime.Warning`) and puts Microsoft's link,
      `https://aka.ms/vc14/vc_redist.x64.exe`, last, so that nothing of the message follows the
      address a user copies.
    - In full: "cannot load LibRaw: most likely Microsoft's Visual C++ runtime is missing or too
      old, so FlatCheck cannot read camera raw files until its latest version is installed (the
      FITS modes work without it): https://aka.ms/vc14/vc_redist.x64.exe".
    - It gives no developer hint. The README's build section keeps the publishing advice.
  - Elsewhere, where there is no Visual C++ runtime, it keeps the advice about the native files:
    they exist for Windows x64 and Linux x64 only, and must sit beside the program or inside it.
  - Both texts are in the run's language, the Romanian without diacritics.
- **The log (R-110).** `RawReader` writes the loader's exception on a Warn line of its own, so
  nothing is lost by leaving it out of the refusal. On Windows the line adds two answers:
  - the first of the folders .NET searches before Windows' own search
    (`NATIVE_DLL_SEARCH_DIRECTORIES`) that holds `raw_r.dll`, or the list of them when none does;
  - which of the runtime's four DLLs `NativeLibrary.TryLoad` does not load by name, in the
    launcher's words (`Visual C++ runtime: missing vcomp140.dll`).

  The probe is `VcRuntime` in the core, the launcher's list and probe. It runs on this path only,
  after LibRaw failed.
- **The list (R-095).**
  - **Three new components**, after LibRaw (`raw_r.dll`), with the copyright lines of the
    suite's notices:
    - libjpeg-turbo (`jpeg8.dll`; IJG, BSD-3-Clause and zlib). Its copyright line ends with the
      sentence the IJG licence asks of a binary's documentation, "This software is based in part
      on the work of the Independent JPEG Group.", untranslated like every copyright line.
    - Little CMS (`lcms2.dll`; MIT).
    - zlib (`zlib1.dll`; zlib).
  - **The count.** The lead counts seven components and the database.
  - **The packages.** Each of the four claims `Sdcb.LibRaw.runtime.win64` and names its DLL
    (`NativeFiles`). A package may be claimed by several components when they divide its DLLs.
    Every DLL of its `runtimes/win-x64/native` is named by exactly one component
    (`ThirdPartyTests`).
  - **ImageSharp's licence** reads `Apache-2.0` in the list, in the README and on the notices'
    licence line. The notices keep the argument for the grant.
  - **The pointer.** The lead says each licence's whole text, and why it is compatible, is in
    `THIRD-PARTY-NOTICES.txt` beside the program in the Astro Darkroom folder, and online. The
    online copy is a link under the lead: the suite's notices in the source repository,
    `https://github.com/tcarean/AstroDarkroom/blob/main/AstroDarkroom/THIRD-PARTY-NOTICES.md`
    (`ThirdParty.NoticesUrl`).
  - **".NET 8 and WPF"** is not listed.

## Alternatives rejected

- **The developer's hint after the runtime's sentence.** Every release has the DLLs. A user
  cannot act on advice about publishing, and the message would grow by a sentence on every
  computer without the runtime.
- **The probe choosing the sentence.** The message would depend on a probe that is itself an
  approximation: it searches from the program's folder, not from `raw_r.dll`'s, and it passes a
  runtime that has the four DLLs but is too old. The log line gives the same facts to whoever
  reads a bug report.
- **Each runtime DLL's path and version in the log.** It would show a runtime older than LibRaw's
  build tools, the one case the probe lets through, but it needs the process's module list read
  on a path no user has reported yet. The four names and `raw_r.dll`'s folder are enough to tell
  apart the two causes the message names.
- **Keeping the loader's exception as the inner one.** The window, a folder publish, would show
  its `libraw.dll` under the message again, through `TabViewModel.Describe`, which every tab
  uses.
- **The own release shipping the suite's notices file**, through a property `kit.json` would
  pass. FlatCheck's build would read a file of AstroDarkroom's folder, and the file speaks of the
  suite. The link reaches the same text from anywhere.
- **The link alone.** In the Astro Darkroom folder, the file beside the program is the copy that
  needs no network.
- **".NET 8 and WPF" as a row.** The list is the code the tool links. The launcher's About, the
  suite's own, lists NLog alone though it ships .NET too.

## Consequences

- **Where the message shows.** The window's error panel and the console's error line give the
  message as it is, ending with the link. The Structure report ("the raw beside the master could
  not be read (...), so the lens is not known") and the Synthetic tab's prefill note ("... could not
  be read (...); type the aperture by hand") quote it inside a sentence of their own, so a
  parenthesis and a few words follow the link there.
  - In the window's error panel the address is plain text, which cannot be selected, so it has
    to be typed there.
  - The console's line, and the report's text box, can be copied.
- **Checked end to end on 2026-11-01**, with the publishes above and a file named `x.NEF`:
  - the console printed the message in both languages and exited 1;
  - the log line named `raw_r.dll`'s folder: the program's folder in a folder publish, the
    extraction folder (`extract\flatcheck\<hash>`) in the self-extracting single file, and
    "none of" the program's folder when the DLL was removed.
- **On a computer with the runtime**, nothing changes and nothing is probed.
- **On a computer without it**, the log line says what is missing. This PC has the runtime, so
  the probe's "missing" branch is tested through a stand-in loader only. A fresh Windows was not
  tried.
- **A runtime that is too old.** A runtime with the four DLLs but older than LibRaw's build tools
  passes the probe. The line then says the four load, and the message still names the runtime,
  "too old".
- **Guards against a new LibRaw build.**
  - `LibRawLoadTests` reads the import tables of the four DLLs in the test output, so a LibRaw
    build that imports another part of the runtime fails before the list of four goes stale.
  - `ThirdPartyTests` fails on a fifth DLL in the package, until it is a component.
- **The link points at the repository's `main`.**
  - It shows the full notices once `main` is pushed. On 2026-11-01 GitHub's `main` was 47
    commits behind and held an older file.
  - It follows the file as it changes, not the copy a given release shipped.
- **AstroDarkroom's ADR 0016** says the three tools keep their old message until their
  follow-ups. FlatCheck's follow-up is this one.
- **The window's height.** The list has eight rows, and the window grew past a 1080-line screen
  at 100 %. ADR 0083 answers that.
