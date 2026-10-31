# 2026-10-31 Ship the third-party licence texts in the suite

## Goal

The suite zip carries the suite's GPL `LICENSE` and the lensfun note, and no text of any licence of
the code written by others that it ships. Every window shows, in "Despre", a component's name,
licence identifier, copyright line and address; the whole texts stay in each project's
`THIRD-PARTY-NOTICES.md`, which no build publishes (FlatCheck ADR 0040, CopyWcs ADR 0009,
ImageVisualCompare ADR 0043, ADR 0013 here). ImageVisualCompare's ADR 0043 records that this falls
short of BSD-3-Clause, which asks the notice, the conditions and the disclaimer to go with a binary
distribution; MIT and Apache-2.0 ask the licence's text to go with it too, and LGPL-2.1 a copy of
the licence. The owner asked on 2026-10-31 to resolve it.

What the zip holds, measured on the 1.0.0 suite folder and the NuGet cache:

| Code | DLLs in the suite | Licence | Text on disk |
| --- | --- | --- | --- |
| NLog, NLog.Targets.AtomicFile 6.2.2 | `NLog.dll`, `NLog.Targets.AtomicFile.dll` | BSD-3-Clause | FlatCheck's verified copy |
| Sdcb.LibRaw 0.21.1.7 | `Sdcb.LibRaw.dll` | MIT | no (not in the package) |
| LibRaw 0.21.1 (Sdcb.LibRaw.runtime.win64) | `raw_r.dll` | LGPL-2.1 chosen of LGPL-2.1 / CDDL-1.0 | the LGPL text in Git for Windows; LibRaw's own COPYRIGHT no |
| libjpeg-turbo 2.1.3 (same package) | `jpeg8.dll` | IJG + BSD-3-Clause + zlib | no |
| Little CMS 2.x (same package) | `lcms2.dll` | MIT | no; version not in the DLL's resources |
| zlib 1.2.11 (same package) | `zlib1.dll` | zlib | Git's copy, later years |
| SixLabors.ImageSharp 3.1.11 | `SixLabors.ImageSharp.dll` | Six Labors Split License, Apache-2.0 grant | package `LICENSE`; Apache-2.0 text in Git's OpenSSL |
| Wacton.Unicolour 8.0.0 | `Wacton.Unicolour.dll` | MIT | package `LICENSE` |
| lensfun database | `lensfun\` | CC BY-SA 3.0 | `lensfun\LICENSE.txt` ships already |
| Lynds' Catalogue of Dark Nebulae | inside AstroPhotoAnalyzer.Core.dll | citation | APA's notices |
| .NET 8 runtime and WPF, self-contained | the runtime's DLLs | MIT, plus the runtime's own third-party notices | `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` in the runtime packs |

The owner's answers of 2026-10-31, each the recommendation but the last:
1. **Where:** one file in the suite, `THIRD-PARTY-NOTICES.txt` at the suite folder's root, published
   by the launcher. The tools do not change.
2. **What:** everything the zip holds, the three native libraries of the LibRaw package and the .NET
   runtime with WPF included; the .NET files taken as they are from the runtime packs the suite is
   built with.
3. **Downloads:** allowed, for the licence texts missing on disk, from the projects' own
   repositories at the versions the zip holds.
4. **LibRaw's source:** a link to the source of the exact version, nothing more (the recommendation
   was a written offer with the link; the source beside the zip was the third option).

## Steps

Branch `claude/Ship-the-licence-texts-in-the-suite`, worktree
`.claude/worktrees/Ship-the-licence-texts-in-the-suite`. Requirements first, then tests, then code.

1. **The texts.** Download into a new empty scratch folder, read as text only, nothing run:
   libjpeg-turbo 2.1.3 `LICENSE.md` and `README.ijg`; Little CMS `COPYING` at the version `lcms2.dll`
   reports through `cmsGetEncodedCMMversion` (read once, before the download); Sdcb.LibRaw
   `LICENSE.txt` at the tag of 0.21.1.7; LibRaw 0.21.1 `COPYRIGHT` and `LICENSE.LGPL`; zlib 1.2.11's
   licence from its `zlib.h` header. The Apache-2.0 text from Git for Windows' OpenSSL copy after a
   word-for-word check of its opening and closing lines; the ImageSharp and Unicolour texts from
   their packages.
2. **R-017** in `docs/requirements.md`: the suite folder carries `THIRD-PARTY-NOTICES.txt` at its
   root, from the launcher's publish, one section per component written by others that any window
   ships (every package of the windows' lock files, the four native libraries of the LibRaw package,
   the lensfun database, the Lynds catalogue, the .NET runtime with WPF), each with its copyright
   notice, its licence's whole text and the windows that ship it; LibRaw's section links the source
   of the exact version; the .NET section names `DOTNET-LICENSE.txt` and
   `DOTNET-THIRD-PARTY-NOTICES.txt`, copied unchanged from the runtime pack the suite is built with.
   R-015's pointer: "Despre"'s lead names `THIRD-PARTY-NOTICES.txt` beside the program.
3. **Tests** in `SuiteTests`, written first: every package resolved in a shipped lock file of a
   component's window or of the launcher is named on a section's `Packages:` line; each native DLL
   of the LibRaw package has a section; each section holds its licence's text (BSD conditions and
   disclaimer, MIT's permission and warranty sentences, the LGPL-2.1 and Apache-2.0 titles and last
   terms, libjpeg-turbo's IJG sentence, zlib's three conditions); LibRaw's section links the 0.21.1
   source; the launcher's window project publishes the file as `THIRD-PARTY-NOTICES.txt` and copies
   the two runtime files.
4. **The file.** `THIRD-PARTY-NOTICES.md` rewritten as the suite's: an opening, then one section per
   row of the table, each with its role, the windows that ship it, its packages or DLLs, its licence,
   its compatibility, its copyright notice and its whole text. LibRaw's section states LGPL-2.1 §6 as
   it applies here and links the 0.21.1 source and Sdcb.LibRaw's build.
5. **The publish** (`src/AstroDarkroom.Wpf/AstroDarkroom.Wpf.csproj`): the file linked as
   `THIRD-PARTY-NOTICES.txt`, copied like `LICENSE`; a target adding the NETCore runtime pack's
   `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` as `DOTNET-LICENSE.txt` and
   `DOTNET-THIRD-PARTY-NOTICES.txt`, found through the resolved runtime pack, failing the publish
   when they are missing; the WindowsDesktop pack's `LICENSE` shipped too only if its words differ.
6. **"Despre":** the lead names `THIRD-PARTY-NOTICES.txt` beside the program, in both languages.
7. **Docs:** ADR 0014, superseding ADR 0013 in part, ADR 0013's Status completed; `launcher.md`;
   `README.md`; the root `README.md`; `CHANGELOG.md`; `CLAUDE.md`; `docs/skills.md`.
8. **Wrap-up:** `/wrap-up`, a subagent review of the branch, the merge proposal.

## Verification

1. `scripts/verify.ps1` green in the worktree.
2. Teeth: removing a package's name from its section, a native DLL's section, or the BSD disclaimer
   turns the matching test red; restored after.
3. `scripts/release.ps1 -Version 1.0.0 -PublishOnly`: the suite folder has the three files at its
   root, the two .NET ones equal byte for byte to the runtime pack's files of the runtime version in
   the windows' `runtimeconfig.json`, no collision; the zip holds them.
4. "Despre" rendered offscreen shows the new lead in both languages.

Follow-ups, proposed after the merge, one task per tool: DarksCheck, FlatCheck and LightsCheck point
"Despre" at the shipped copy for LibRaw's licence and list libjpeg-turbo, Little CMS and zlib apart
from LibRaw (LightsCheck's lead also counts two components for three); ImageVisualCompare, CopyWcs
and AstroPhotoAnalyzer point "Despre" at the suite's file.

## What deviated

- **One level deeper than the table.** Reading the texts showed code of others inside two
  components: LibRaw's `COPYRIGHT` names DCB/FBDD (3-clause BSD, in `raw_r.dll`, which exports
  `dcb` and `fbdd`), Adobe DNG SDK pieces (MIT) and dcraw.c (no restricted code), and the X3F tools
  (not compiled in: no `x3f_thumb_loader`); ImageSharp's `THIRD-PARTY-NOTICES.TXT` names
  SharpZipLib (MIT), Chromium (BSD-3) and corefx (MIT) without copyright lines or texts. Under
  the owner's "everything in the zip", their sections carry those notices and texts too (ADR
  0014). Assumption stated, not asked: the BSD and MIT licences ask for their text in a binary
  distribution, so "everything" reads as including them.
- **Texts beyond the plan's list.** ImageSharp's `THIRD-PARTY-NOTICES.TXT` at `f4a2684` was
  downloaded like the others; the DCB/FBDD header of LibRaw 0.21.1, Chromium's `LICENSE` at
  `d522fb7` and SharpZipLib's `LICENSE.txt` at `ff2d7c3` were read from their repositories
  without saving them, typed into the file, and compared with the sources word for word. Every
  quoted text of the file was compared so (16, all equal), the Adobe one with the MIT text it
  takes, since LibRaw ships none for those pieces.
- **Sdcb.LibRaw's licence file** is the MIT template with `[year] [fullname]` unfilled; it is
  shipped as it is, the package's holder (Zhou Jie) named beside it.
- **The tests.** The checks that read only this project went to `ThirdPartyTests`, the one that
  reads the siblings' lock files to `SuiteTests`, with a `Notices` helper; beyond the plan, that
  test also checks the version on each `Packages:` line and each section's `Programs:` line, and
  the lead and the published name moved into the core (`ThirdParty.Lead`, `NoticesFile`), so a
  test pins them and compares the name with the project's `Link`.
- **Format.** Each section opens with a `Packages:` / `Files:` / `Programs:` / `Licence:` /
  `Project:` list, which the tests parse; the texts are quoted with `> ` as before, tabs expanded
  and trailing spaces dropped; `<br>` was taken out of libjpeg-turbo's BSD copyright line.
- **Verification.** The gate green (102 tests); six mutations each turned the matching test red
  (a package name, a version, a program, a native library's section, the BSD disclaimer, and the
  publish's runtime pack, which stopped the publish with its error); `release.ps1 -Version 1.0.0
  -PublishOnly` published 370 files with no collision, its only new files the three at the root,
  the two .NET ones equal to the 8.0.33 pack's that all seven `.runtimeconfig.json` name, the zip
  holding them; "Despre" rendered offscreen at 931 / 946 DIP as before, the lead on one line in
  both languages. The live start of the launcher from the published folder waited, the owner
  being at the PC.
- **The review** (a subagent, nine findings). The one that mattered: the four DLLs of the LibRaw
  package were compiled by vcpkg (their debug paths), not from Sdcb.LibRaw's repository, and
  LGPL-2.1 counts build scripts as source. Measured: `raw_r.dll` linked 2023-05-31, so vcpkg's
  `libraw` port of commit `2e3b958` (LibRaw 0.21.1 with LibRaw-cmake `6e26c9e` and two patches).
  It contradicted the premise of the owner's link choice, so it went back to them: of a source
  archive beside the zip (recommended), exact links, or a written offer, they chose exact links.
  Taken as well: a test that every section has its licence text checked; a test that asks
  MSBuild for the real publish list (`-getItem`, about a second), which turned red when the
  target was hooked to a target that never runs, while an item stripped of
  `CopyToPublishDirectory` stays copied (MSBuild lists it as `Always`), so its green was right;
  the Adobe text's lead-in; Apache-2.0 alone on ImageSharp's licence line, as its Split License
  asks; WPF's own notices linked; "nothing has to be installed" narrowed to .NET, since
  `raw_r.dll` needs the Visual C++ runtime the folder lacks; continued list lines joined by
  `Notices.Field`; stale summaries in `SuiteTests`, `CLAUDE.md` and `docs/skills.md`.
- **The second review** (of the fix commit only; two risks, six nits). Taken: the install the
  DLL came from, `libraw[dng-lossy,openmp]:x64-windows`, both features off by default (measured:
  `raw_r.dll` imports ten functions of `jpeg8.dll` by ordinal and ten of `VCOMP140.DLL`); a stop
  in `/release` while a LibRaw tool's About window still names `THIRD-PARTY-NOTICES.md`; the
  README's "(nothing to install)" narrowed to .NET, as the root README has it; ImageSharp's
  licence naming in FlatCheck's and AstroPhotoAnalyzer's About windows added to the per-tool
  follow-ups; a test that the WPF link names the WindowsDesktop pack the publish takes (red with
  `v8.0.32`); the publish-list query's switches checked against `kit.json` and its wording made
  exact (it leaves out the referenced projects' outputs); at least one phrase per checked
  section. Left: the plan's own step 4, which still says "Sdcb.LibRaw's build", records what was
  approved, and this section what changed.
