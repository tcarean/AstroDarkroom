# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (the kit's
ADR 0018). What AstroDarkroom needs on top of them is here, one section per skill; each skill
reads this file before its first step, and each section says at which step it applies.
Values (the solution, the folders, the publish list, what the auditor may also read) are in
`kit.json`, not here.

## /new-feature

- At step 1: a new app in the suite is one `release.publish` entry in `kit.json` (its window
  project, a sibling project; never its console, ADR 0002), one `AppCatalogue` line (folder,
  Romanian title and description, the exe name the component's project file builds, the file
  name of its icon), its icon copied into `Icon/` under the component's own file name (R-010,
  ADR 0011) and a row in R-001; `SuiteTests` then checks the three agree, that the copy has
  the component's bytes and that the component is on .NET 8 with the suite's package
  versions. The new app's title goes on the `Programs:` lines of `THIRD-PARTY-NOTICES.md` for the
  packages it ships, and a package new to the suite gets its section (R-017, below). Nothing
  else changes: the window lists whatever the catalogue holds.
- At step 2: the core is tested in `tests/AstroDarkroom.Tests` with a temp folder of empty
  files named like the executables (`TempWorkspace`); the window has no tests (ADR 0001) and
  is checked by starting it from a published suite folder.
- A new log line is English and never goes through `T(...)` (`NoLogLineGoesThroughTheTranslation`);
  a line about the suite takes its words from `LogText` in the core, so a test pins them, and a
  new seam's lines join the table in `docs/design/logging.md` and R-012. A new package is a
  component in `ThirdParty.cs` and a section in `THIRD-PARTY-NOTICES.md` (`ThirdPartyTests`,
  ADR 0013); a package a component also ships takes the component's version (`SuiteTests`).
- A package any window of the suite starts to ship, or ships at a new version, the launcher's or
  a component's, needs its section in `THIRD-PARTY-NOTICES.md` at that version, with the
  windows that ship it and its licence's whole text from the project's own file, read at that
  version (R-017, ADR 0014): `SuiteTests` is red until the `Packages:` and `Programs:` lines
  match the windows' lock files, and `ThirdPartyTests` until the new section's licence phrases
  join `LicenceTexts`. A new version of `Sdcb.LibRaw.runtime.win64` also means reading again what
  its four DLLs report (LibRaw, libjpeg-turbo, Little CMS, zlib), their texts, and from
  `raw_r.dll`'s link date the vcpkg `libraw` port that built it, for LibRaw's source links.

## /wrap-up

- At step 1, after the gate: a change under `src/AstroDarkroom.Wpf/` is checked by starting the
  launcher from a published suite folder (`scripts/release.ps1 -Version X.Y.Z -PublishOnly`),
  only when the owner is not using the PC, and reading `log\AstroDarkroom.log` there: the first
  line, a line for each thing the check did, `exit 0`, and no `ERROR` or `WARN` the check did
  not mean to cause (ADR 0012).

## /validate

(none; the launcher has no measured figures, `docs/validation.json` has no case)

## /release

- At step 2: this release is the suite's. Read each component's `CHANGELOG.md`
  (`../DarksCheck`, `../FlatCheck`, `../LightsCheck`, `../ImageVisualCompare`,
  `../WCS_Copy`, `../AstroPhotoAnalyzer`) and list the ones whose `[Unreleased]` has content:
  their entry in `release.json` will say `unreleased: true` (the launcher does not show it,
  ADR 0007). Say so to the owner, who may release them first (each with its own `/release`) or
  bundle them as they are; the recommendation is to bundle, since `release.json` records each
  component's commit.
- At step 5: the publish is one flat folder, `artifacts/<version>/`, with the launcher and
  every component's window beside each other and one copy of the desktop runtime; a file two
  publishes write with different bytes stops the release (the kit's ADR 0036). The consoles
  are not bundled: their base runtime differs from the desktop one in three files (ADR 0002).
  `release.json` and the zip `AstroDarkroom-<version>-win-x64.zip` with its `.sha256` land in
  `artifacts/`. The tag is `astrodarkroom-vX.Y.Z`. No `log` folder goes into the zip: the
  publish starts no program, and it deletes the folder a test run of the suite left first.
  Check that the folder's root holds `LICENSE`, `THIRD-PARTY-NOTICES.txt`, `DOTNET-LICENSE.txt`
  and `DOTNET-THIRD-PARTY-NOTICES.txt` (R-017, ADR 0014), the last two equal to the files of the
  `Microsoft.NETCore.App` runtime pack whose version the windows' `.runtimeconfig.json` names.
- At step 7: the owner uploads the zip to their Google Drive by hand and shares it as "Anyone
  with the link"; nothing here uploads it or asks for Drive access (ADR 0005). With the link
  the owner gives, propose a new first row of the table under `## Download` in `README.md`,
  and the same row as the only row of the table under `## Download` in the repository's root
  `README.md` (`../README.md`, the Bitbucket front page, ADR 0008), replacing the row there:
  the version, the release date, the link, and the hash from the zip's `.sha256`. Read the hash
  from the file, never retype it. Then propose its commit, `Link the X.Y.Z zip in the READMEs`,
  limited to the two READMEs; the owner commits it on `main`, as the release commit.

## /mutate

(none; the core has no numeric code, the skill applies as it is)

## /docs-health

(none)
