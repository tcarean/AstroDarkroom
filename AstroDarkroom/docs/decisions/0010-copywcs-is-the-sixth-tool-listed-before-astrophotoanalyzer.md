# 0010. CopyWcs is the sixth tool of the suite, listed between ImageVisualCompare and AstroPhotoAnalyzer

Date: 2026-10-27
Status: accepted 2026-10-27; the description of CopyWcs superseded by 0017
Supersedes: 0002 in part (the count of five windows)

## Context

CopyWcs (`WCS_Copy`, a kit project with a WPF window since its ADR 0007) copies the plate solve
of a Siril-solved FITS into a processed copy of the same stack: the WCS a stretch or an export
drops, and the one AstroPhotoAnalyzer's catalogue of dark nebulae needs. It is on .NET 8, its
window takes `--lang` in the one-token form the launcher passes (R-008), and its README is in
English. On 2026-10-27 the owner asked for it in the launcher, with the language passed like the
other tools'. ADR 0002 counted five windows, and `CLAUDE.md` already said a new app in the suite
is one `kit.json` entry, one catalogue line and the `SuiteTests` run; the place of the new row
was the one choice left.

## Decision

CopyWcs is the sixth component of the suite: the entry
`../WCS_Copy/src/CopyWcs.Wpf/CopyWcs.Wpf.csproj` in `release.publish` and the catalogue line
`WCS_Copy` / `CopyWcs` / `CopyWcs.Wpf.exe`, described as "Copies the plate solve of one FITS
into another" / "Copiaza plate solve-ul unui FITS in altul", after the tagline of its own
About window ("from one FITS" there). It stands after ImageVisualCompare and before
AstroPhotoAnalyzer: in the order of a
session it runs on the processed image, once the processing is chosen, and before the finished
image is checked by the tool that reads the WCS it restores. Its console, `copywcs.exe`, stays
in its own release (ADR 0002). Everything else of ADR 0002 stands; only the count changes.

## Alternatives rejected

- **Last, after AstroPhotoAnalyzer.** The final check uses the solve CopyWcs restores, so the
  restore comes first in the workflow the rows follow (R-001).
- **Before ImageVisualCompare.** The comparison picks between two processings; the solve is put
  back into the one that was picked.
- **Not in the suite, in its own release only.** The owner asked for it in the launcher, and the
  tool that makes a plate-solved FITS out of a processed image belongs beside the tool that
  needs one.

## Consequences

The window has six rows, which `SizeToContent="Height"` fits. The About window lists the
components under the catalogue's titles: `release.json` names each by its folder, and this
is the first whose folder, `WCS_Copy`, is not its name. R-001 lists six apps; `README.md`,
the front page and `CLAUDE.md` say six; `docs/skills.md` names `../WCS_Copy` among the
CHANGELOGs `/release` reads; the prerequisites texts (R-009) name CopyWcs among the tools that
open the files exported from Siril. `SuiteTests` reads CopyWcs's lock files with the other
components', so a package it resolves to another version than a sibling is a red gate, fixed in
the component.

Measured on 2026-10-27 in the task's worktree, `scripts/release.ps1 -Version 1.0.0 -PublishOnly`
with the projects already built: CopyWcs's window is the fifth publish, 9 files of its own and
243 already there with the same bytes, no collision; the folder has 365 files and 160.5 MB
(356 files with five tools, ADR 0002), the zip 65.9 MB, and the publish took 77 seconds.

```
merged   248 file(s) copied, 0 already there with the same bytes, into ...\artifacts\1.0.0
merged   14 file(s) copied, 238 already there with the same bytes, into ...\artifacts\1.0.0
merged   67 file(s) copied, 243 already there with the same bytes, into ...\artifacts\1.0.0
merged   9 file(s) copied, 243 already there with the same bytes, into ...\artifacts\1.0.0
merged   9 file(s) copied, 239 already there with the same bytes, into ...\artifacts\1.0.0
merged   9 file(s) copied, 239 already there with the same bytes, into ...\artifacts\1.0.0
merged   8 file(s) copied, 239 already there with the same bytes, into ...\artifacts\1.0.0
component WCS_Copy 1.0.0 (cfa54cc, unreleased changes)
published ...\artifacts\1.0.0 (365 files, 160.5 MB)
zipped    ...\artifacts\AstroDarkroom-1.0.0-win-x64.zip (65.9 MB), SHA-256 in AstroDarkroom-1.0.0-win-x64.zip.sha256
```
