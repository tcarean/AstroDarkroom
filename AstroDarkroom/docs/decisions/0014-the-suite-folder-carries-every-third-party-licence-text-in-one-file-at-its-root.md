# 0014. The suite folder carries every third-party licence text, in one file at its root

Date: 2026-10-31
Status: accepted 2026-10-31
Supersedes: 0013 in part (the notices file kept beside the source for the launcher's components)

## Context

ADR 0013 kept `THIRD-PARTY-NOTICES.md` beside the source, as every tool does with its own
(FlatCheck ADR 0040, CopyWcs ADR 0009, ImageVisualCompare ADR 0043), since a file of one name
with different bytes in each tool would collide in the flat suite folder. So the 1.0.0 zip
carried the suite's GPL `LICENSE` and lensfun's note, and no text of any licence of the code of
others it ships; ImageVisualCompare's ADR 0043 records that this falls short of BSD-3-Clause,
whose binary condition asks the notice, the conditions and the disclaimer to go with the
distribution, and MIT, Apache-2.0 and LGPL-2.1 ask for their texts too.

Measured on 2026-10-31: every file of the 1.0.0 suite folder is one of the two runtime packs'
(179 `Microsoft.NETCore.App`, 59 `Microsoft.WindowsDesktop.App`, 8.0.33), one of seven packages',
a program's own, `LICENSE`, `release.json` or `lensfun\`. The windows' lock files resolve `NLog`
and `NLog.Targets.AtomicFile` 6.2.2, `Sdcb.LibRaw` 0.21.1.7, `Sdcb.LibRaw.runtime.win64` and
`.linux64` 0.21.1 (the second puts no file in the Windows folder), `SixLabors.ImageSharp` 3.1.11
and `Wacton.Unicolour` 8.0.0. The LibRaw package's four DLLs are four projects: `raw_r.dll`
reports LibRaw `0.21.1-Release`, `jpeg8.dll` libjpeg-turbo 2.1.3, `lcms2.dll` 2120 through
`cmsGetEncodedCMMversion` (Little CMS 2.12), `zlib1.dll` zlib 1.2.11. The WindowsDesktop pack's
`LICENSE` has the NETCore pack's words, its last line end aside, and the pack has no notices file;
WPF's repository keeps one at the tag of each runtime version.

On 2026-10-31 the owner chose, of the options put to them with a recommendation each: one file
in the suite, at its root, from the launcher's publish, the tools unchanged; everything the zip
holds, the native libraries and the .NET runtime with WPF included, the .NET texts taken as they
are from the runtime pack; the missing texts downloaded from the projects at the versions
shipped; and, for LibRaw's source, a link (not the recommended written offer, nor the source
beside the zip).

Reading the texts brought three facts the questions did not have. LibRaw's `COPYRIGHT` names code
of others inside it: the DCB demosaicing and FBDD denoising, under a 3-clause BSD licence whose
text is in `src/demosaic/dcb_demosaic.cpp`, and which `raw_r.dll` holds (it exports `dcb` and
`fbdd`); pieces of the Adobe DNG SDK 1.4, under MIT by its words, with no text of that licence
in LibRaw's source; and dcraw.c's unrestricted code. The X3F tools it also names are not compiled
in (`x3f_thumb_loader`, declared only under `USE_X3FTOOLS`, is absent). ImageSharp's
`THIRD-PARTY-NOTICES.TXT`, at the commit 3.1.11 was built from, names SharpZipLib (MIT), Chromium
(BSD-3) and corefx (MIT) code inside it, with neither copyright lines nor texts. And Sdcb.LibRaw's
`LICENSE.txt`, at the commit its package names, is the MIT template with `[year] [fullname]`
unfilled; the package names Zhou Jie.

The review of the branch brought one more. The four DLLs were compiled by vcpkg, not from
Sdcb.LibRaw's repository: their debug paths name vcpkg's build folders (`buildtrees/libraw` and
the like), and the package records no vcpkg commit. `raw_r.dll` was linked on 2023-05-31; the
only `libraw` port that built LibRaw 0.21.1 before that day is the one of vcpkg's commit
`2e3b958` (2023-02-16), which compiles LibRaw 0.21.1 with the CMake files of LibRaw-cmake at
`6e26c9e`, after two patches of its own to them; and the DLL's imports, ten functions of
`jpeg8.dll` and ten of OpenMP's `VCOMP140.DLL`, show it was installed with the port's two
features that are off by default, as `libraw[dng-lossy,openmp]:x64-windows`. LGPL-2.1 counts the
scripts that control compilation as part of a library's source, so a link to LibRaw's tag alone
does not give them.
Put to the owner again the same day, with the source archive beside the zip as the
recommendation, the owner chose exact links only.

## Decision

- **One file, the suite's.** `THIRD-PARTY-NOTICES.md` has a section for each component written
  by others that a program of the suite folder ships: each package the windows' lock files
  resolve, each of the four native libraries of the LibRaw package, the lensfun database, the
  Lynds catalogue, and .NET 8 with WPF. A section opens with a list (`Packages:` with the versions
  resolved, `Files:`, `Programs:`, `Licence:`, `Project:`), then the role, why the licence is
  compatible with the GPLv3, the copyright notices and the licence's whole text, quoted word for
  word from the project's own file at the version shipped. The code of others that a component's
  own notices name is covered in its section with its own notice and licence text, one level
  down: the DCB/FBDD and Adobe pieces in LibRaw's, the SharpZipLib, Chromium and corefx pieces in
  ImageSharp's, the texts taken from the file that names them or, for code adapted from a project
  still moving, from that project's licence file at a named commit; the Adobe pieces, whose
  licence LibRaw names without a text, get MIT's usual text under the copyright line LibRaw
  gives. ImageSharp's `Licence:` line names Apache-2.0 alone, the licence the Six Labors Split
  License grants open-source software, since it asks documentation to name the granted licence
  only.
- **Published as `THIRD-PARTY-NOTICES.txt`** at the suite folder's root by the launcher's window
  project, like `LICENSE`, a name Windows opens with a double click. Its self-contained publish
  adds the `Microsoft.NETCore.App` runtime pack's `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`,
  unchanged, as `DOTNET-LICENSE.txt` and `DOTNET-THIRD-PARTY-NOTICES.txt`, found through the
  runtime pack the publish resolved, and stops with an error when they are missing. The
  WindowsDesktop pack's `LICENSE` is not shipped a second time; WPF's own notices, which its pack
  does not carry, are linked at the tag of the runtime's version, which a test keeps equal to the
  WindowsDesktop pack the publish takes.
- **LibRaw's source by exact links**, the owner's choice: LibRaw is used under the LGPL-2.1, as a
  library of its own that a build with the same interface can replace, and its section links
  what `raw_r.dll` was built from, LibRaw 0.21.1, LibRaw-cmake at `6e26c9e` and vcpkg's `libraw`
  port at `2e3b958`, names the install, `libraw[dng-lossy,openmp]:x64-windows`, and says that the
  package records no vcpkg commit and how the port and its features were found.
- **"Despre" points at the shipped file.** Its lead names `THIRD-PARTY-NOTICES.txt` beside the
  program; the lead and the name are in the core (`ThirdParty.Lead`, `ThirdParty.NoticesFile`),
  where the tests pin them, and the window shows the lead. The card still lists the launcher's
  own components (R-015).
- **The gate binds the file to the suite** (R-017): `SuiteTests` reads the lock file beside each
  window project of `release.publish` and checks that each package is on a section's `Packages:`
  line at the version resolved, that each package named there is resolved, and that each such
  section's `Programs:` line names exactly the windows that resolve its packages; `ThirdPartyTests`
  checks phrases of each section's licence texts, that every section is among those it checks,
  the native libraries' sections, the publish's items in the window project, the publish list
  MSBuild computes for win-x64 with the switches of `kit.json` (`ComputeResolvedFilesToPublishList`),
  the WPF link against the runtime that list takes, and the lead. Before a suite release,
  `/release` stops while a tool that ships LibRaw still points its About window at a notices
  file the folder does not have (`docs/skills.md`).

## Alternatives rejected

- **Each tool's notices file published under a name of its own**: six files, repeating NLog and
  LibRaw, and six changes to the tools; the owner chose one file.
- **The packages only, without the native libraries and .NET**: the four DLLs are four projects
  with their own licences (libjpeg-turbo's asks a sentence in the documentation, BSD the text),
  and .NET's MIT asks its notice to go with the runtime, which the folder carries.
- **The .NET texts copied into the suite's file**: about 70 KB that change with each runtime;
  taken from the pack at publish, they are those of the runtime shipped.
- **The texts from a template or from memory**: the words would not be the projects' own, nor
  their copyright lines; Sdcb.LibRaw's unfilled template and LibRaw's LGPL copy with the FSF's
  old address are shipped as their projects have them.
- **The code of others inside a component left to the component's own notices**: ImageSharp's
  name three pieces with no copyright line or text, and LibRaw's `COPYRIGHT` names DCB/FBDD's
  licence without its text; the BSD and MIT licences ask for their text in a binary distribution.
- **LibRaw's source in an archive beside the zip, with the build scripts** (the recommendation
  after the review), **or a written offer for three years** (the first recommendation): the owner
  chose links, twice.
- **A publish inside the tests, to check the files it writes**: about a minute per run of the
  gate; MSBuild's publish list for win-x64 answers for these files in about a second, copying
  nothing (it leaves out the referenced projects' outputs, which no check here needs).

## Consequences

The suite folder has three more files at its root: `THIRD-PARTY-NOTICES.txt` (about 70 KB),
`DOTNET-LICENSE.txt` and `DOTNET-THIRD-PARTY-NOTICES.txt` (about 1 KB and 69 KB). A component
that adds a package, or updates one, turns this project's gate red until the file covers it at
the new version, as a redrawn icon does (ADR 0011); a new version of the LibRaw runtime package
also means reading the four DLLs' versions and texts again, and finding again, from their link
date, the vcpkg port that built them, which no test can. A runtime package for another system is
named too, since the lock files list it. The publish-list tests run MSBuild from the tests,
about a second once the window is built, and a new SDK patch that moves the 8.0 runtime turns
the WPF link's test red until the link names the new tag. The tools' own About windows still
point at their notices beside their source: pointing them at the suite's file, listing
libjpeg-turbo, Little CMS and zlib apart from LibRaw in the three tools that ship them, and
naming ImageSharp's licence Apache-2.0 in FlatCheck's and AstroPhotoAnalyzer's, as the Split
License asks, is a task in each tool, due before the next suite release, since LGPL-2.1 asks a
program that shows LibRaw's notice to point at the licence's copy (`/release` checks that one). The texts of Chromium and SharpZipLib are those of their licence
files at the commits `d522fb7` and `ff2d7c3` read on 2026-10-31, the projects ImageSharp adapted
code from.
