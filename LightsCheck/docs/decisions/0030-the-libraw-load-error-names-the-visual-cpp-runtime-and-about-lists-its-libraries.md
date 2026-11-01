# 0030. The libraw load error names the Visual C++ runtime, and About lists the libraries in libraw's DLLs

Date: 2026-11-01
Status: accepted 2026-11-01

## Context

LibRaw's DLLs, from `Sdcb.LibRaw.runtime.win64` 0.21.1, need Microsoft's Visual C++ v14 runtime
for x64, as read from their import tables on 2026-11-01. `raw_r.dll` imports `VCRUNTIME140.dll`,
`VCRUNTIME140_1.dll`, `MSVCP140.dll` and OpenMP's `VCOMP140.DLL`. `jpeg8.dll`, `lcms2.dll` and
`zlib1.dll` import `VCRUNTIME140.dll`. Neither form of the tool ships that runtime:

- The own release is the two single-file executables of `release.ps1`. Their bundles hold the
  four LibRaw DLLs, which the program extracts under `%TEMP%\.net\lightscheck\<bundle id>\` at
  start, and WPF's renamed `vcruntime140_cor3.dll`.
- The suite folder holds the four DLLs beside `LightsCheck.Wpf.exe`.

Without the runtime, or with one too old to have all four DLLs (the 2015 and 2017 packages lack
`vcruntime140_1.dll`, which Visual Studio 2019 added), raw files cannot be read, while FITS still
can. The suite's launcher has checked for the runtime since its ADR 0016, which also measured why
the tools' error misleads. Sdcb.LibRaw's resolver tries `raw_r.dll`, then `libraw.dll`, so the
exception names `libraw.dll`. `RawReader.Missing` then told the user that the DLLs must sit beside
`lightscheck.exe` and to publish with `IncludeNativeLibrariesForSelfExtract`. That advice is for a
developer, and false in both shipped forms.

The About window listed Sdcb.LibRaw, LibRaw and NLog under a lead that said "two components" and
sent the reader to `THIRD-PARTY-NOTICES.md`. That file holds no licence text but NLog's, and
travels with neither form. The suite's `THIRD-PARTY-NOTICES.txt` (AstroDarkroom's ADR 0014,
R-017) gives every licence whole. For LightsCheck it names Sdcb.LibRaw, LibRaw, libjpeg-turbo
(`jpeg8.dll`), Little CMS (`lcms2.dll`), zlib (`zlib1.dll`), NLog, and .NET 8 with WPF.

The owner made three choices on 2026-11-01, each put with the measurements and a recommendation,
and took the recommendation each time:

- no hint for a developer in the message;
- the log says which runtime DLLs do not load, asked once per process;
- no row for .NET.

## Decision

- **The message.** It names the runtime, missing or too old, as the likely cause, and ends with
  Microsoft's permalink to its latest x64 package, `https://aka.ms/vc14/vc_redist.x64.exe`. Its
  wording follows the launcher's warning. It is a `T` pair, the Romanian without diacritics. It
  says that FITS frames are not affected, and names no executable, folder, DLL or publish option.
  The loader's exception stays the inner one, and the log writes it with the frame's line as
  before (R-074).
- **The runtime check, once per process.** The first failure asks Windows to load
  `vcruntime140.dll`, `vcruntime140_1.dll`, `msvcp140.dll` and `vcomp140.dll` by name, in the
  tool's own process, and writes one Warn line. The line names the DLLs that do not load, or says
  that the four load, so the runtime is not the cause (`RawReader.RuntimeCheck`, R-071). Later
  failures ask nothing.
- **Six rows in About.** Sdcb.LibRaw, LibRaw, libjpeg-turbo, Little CMS, zlib and NLog: the
  suite file's list for LightsCheck without .NET and WPF, which the launcher's About leaves out
  too (its R-015).
  - Each native library's role names its DLL.
  - libjpeg-turbo's says that the software is based in part on the work of the Independent JPEG
    Group, as the IJG licence asks of a binary's documentation.
  - The three new rows claim `Sdcb.LibRaw.runtime.win64`, so a package may now be claimed by
    several components, each naming one of its native DLLs (R-010).
- **The lead in the core, counting in words.** `ThirdParty.Lead` replaces
  `WindowTexts.AboutThirdPartyLead`, beside the list it counts, as the launcher keeps its own.
  It says "six" / "sase", and a test holds the word to the list's length. It says where the whole
  texts are: `THIRD-PARTY-NOTICES.txt` beside the program in the Astro Darkroom suite folder, and,
  in the source, `AstroDarkroom/THIRD-PARTY-NOTICES.md`. Both are true from either form, since
  the own release is built from the repository that holds the second (R-075).

## Alternatives rejected

- **A short hint for a build of one's own** ("check that raw_r.dll ... came with the program").
  Both shipped forms carry the DLLs, so the hint helps only a hand-made build, and its developer
  has the log.
- **The loader's own words in parentheses, as before.** They are English in both languages and
  name `libraw.dll`, which points away from the runtime.
- **The check at every failure.** A missing libraw fails each frame of a series twice, the
  measurement then the metadata: 174 times on the Veil session. A DLL that does not load is looked
  for along the whole PATH, 12 to 26 ms for the four on this PC (the launcher's measurement), so
  2 to 4.5 s per run, for the same answer.
- **The check plus where raw_r.dll was loaded from and which of LibRaw's DLLs are there.** Offered
  as an option; the owner kept the runtime line alone.
- **A row for .NET 8 and WPF.** The launcher's About lists no platform either, and the runtime's
  own notices travel with it.
- **A count composed in code.** A number word in two languages needs a table in the core for a
  sentence that changes when a package does; a literal word held by a test turns red at that
  moment, which the stale "two" shows was missing.
- **Shipping the notices with the own release.** The own release is built from the source, which
  holds the texts; making `release.ps1` publish a text file is a decision of its own, and nothing
  here needs it.

## Consequences

- The four DLL names are tied to LibRaw 0.21.1's build. `RawReaderTests` reads the import tables
  of the DLLs the build brings for Windows, and fails when they import another runtime DLL.
  `ThirdPartyTests` reads the core's restore, and fails when a package brings a native DLL that no
  row names.
- The check asks by bare name, so Windows searches from the program's folder, while `raw_r.dll`'s
  imports are searched from `raw_r.dll`'s folder, since .NET loads it by full path with
  `LOAD_WITH_ALTERED_SEARCH_PATH`; the rest of the order is the same. In the suite folder the two
  folders are one. In the single-file release `raw_r.dll`'s folder is the extraction folder, so
  the answers differ only for a runtime DLL copied into one of the two folders.
- A runtime that has the four DLLs but is older than the one LibRaw was built with passes the
  check, as it passes the launcher's.
- The check runs once per process: a window left open while the user installs the runtime writes
  no second line.
- The message speaks of Windows' runtime. LightsCheck ships for Windows only; a console built from
  the source on another system would show the same message, a case no shipped form meets.
- About grows by three rows. AstroDarkroom's ADR 0016 says the three tools keep their old message
  until their follow-ups; for LightsCheck that sentence is outdated once this is merged, and it
  is AstroDarkroom's to amend.
