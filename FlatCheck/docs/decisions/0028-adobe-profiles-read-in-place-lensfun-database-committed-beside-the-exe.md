# 0028. Adobe profiles are read in place and never copied; the lensfun database is committed whole and shipped beside the exe

Date: 2026-09-25
Status: accepted 2026-09-25; the two Adobe folders superseded by 0066 (Lightroom's own folders are read too, each relative name once)

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-synthetic.md, docs/design/lens-profiles.md
and docs/design/code-map.md (and the memory note flatcheck-synthetic-flat); the decision itself
dates from 2026-09-17, when the synthetic flat was built. The mode needs the lens's published
vignetting profile, and two public sources exist: Adobe LCP files, which Lightroom / Camera Raw /
DNG Converter install on the machine (3531 `.lcp` files, 455 MB on the author's machine), and the
lensfun `data/db` (59 XML files, 5.19 MB, 0.58 MB gzipped, CC BY-SA 3.0). Both are read by our own
code over `System.Xml.Linq`, no new package. Parsing every Adobe file takes 36 s; listing them
from file names plus lensfun takes 2.2 s (`--lenses`, measured 2026-09-17).

## Decision

The owner's decision of 2026-09-17, recorded so nobody re-opens it: Adobe profiles are read from
`C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0\**` and `%APPDATA%\Adobe\CameraRaw\LensProfiles`
only, never copied into the repo or the output, and they are deliberately not a third-party
component (the notices file says so under its own heading). The whole lensfun `data/db` is
committed verbatim under `lensfun/` (with `LENSFUN-VERSION.txt`, `LICENSE.txt` and the dev-time
`update-lensfun.py`), not embedded in the assembly, copied next to the exe by both application
csproj files, and updated by replacing the files. lensfun is parsed up front; Adobe files are
stubs built from the `<Camera> (<Lens>) - RAW.lcp` file name (`AdobeProfileFile.FromName`) and a
file is parsed only when its lens is chosen or a Nikon lens ID is looked up, and then only the
files of the camera's mount, in parallel.

## Alternatives rejected

- **Embedding the lensfun database in the assembly.** Turned down by the owner: "whole
  database, not embedded, folder beside the exe"; lens and camera entries share files, so a
  partial copy would not work either, and updating is replacing files.
- **Copying the Adobe profiles.** Never considered acceptable: they are Adobe's, read from where
  Adobe put them; `--profiles <dir>` replaces the two locations for the tests (`LensProfileTestData`).
- **Parsing every Adobe file at start.** 36 s against 2 s for the list from names; the parse is
  paid per chosen lens instead.

## Consequences

- The "single copyable binary" is now "the exe, plus the `lensfun` folder if you want the lensfun
  half of the list". Without the folder every mode runs; `--lenses` and the window's sources
  panel say which folder was looked at. The Adobe half needs nothing shipped.
- `lensfun/` is not git-ignored: a fresh clone must build. The lensfun database is the one data
  component in `ThirdParty.cs` (`IsData`, no package), displayed in "Despre" because CC BY-SA asks
  for attribution; `EveryComponentNamesItsLicenceAndUrl` allows exactly one such component.
- Aliases of unparsed Adobe stubs are not searchable; the name from the file is. Adobe stubs
  carry no calibration label, because the SFF is in the file, not its name.
- `SyntheticReport.AdobeWhere` / `LensfunWhere` are the one wording for where profiles come from,
  printed by the console and shown by the window's sources panel (collapsed by default, the
  owner's decision).
- Guards: the reader tests on hand-written XML, the catalog tests, `LicenseHeaderTests` (which
  scans `lensfun/update-lensfun.py`), and `ThirdPartyTests` for the data-component entry.
- Source notes: docs/design/modes-synthetic.md, docs/design/lens-profiles.md (sections 1, 5, 10),
  docs/design/code-map.md, docs/design/dependencies.md; memory note flatcheck-synthetic-flat.
