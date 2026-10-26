# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (the kit's
ADR 0018). What AstroDarkroom needs on top of them is here, one section per skill; each skill
reads this file before its first step, and each section says at which step it applies.
Values (the solution, the folders, the publish list, what the auditor may also read) are in
`kit.json`, not here.

## /new-feature

- At step 1: a new app in the suite is one `release.publish` entry in `kit.json` (its window
  project, a sibling project; never its console, ADR 0002), one `AppCatalogue` line (folder,
  Romanian title and description, the exe name the component's project file builds) and a
  row in R-001; `SuiteTests` then checks the three agree and that the component is on .NET 8
  with the suite's package versions. Nothing else changes: the window lists whatever the
  catalogue holds.
- At step 2: the core is tested in `tests/AstroDarkroom.Tests` with a temp folder of empty
  files named like the executables (`TempWorkspace`); the window has no tests (ADR 0001) and
  is checked by starting it from a published suite folder.

## /wrap-up

(none)

## /validate

(none; the launcher has no measured figures, `docs/validation.json` has no case)

## /release

- At step 2: this release is the suite's. Read each component's `CHANGELOG.md`
  (`../DarksCheck`, `../FlatCheck`, `../LightsCheck`, `../ImageVisualCompare`,
  `../AstroPhotoAnalyzer`) and list the ones whose `[Unreleased]` has content: their entry in
  `release.json` will say `unreleased: true` (the launcher does not show it, ADR 0007). Say so
  to the owner, who may release them first (each with its own `/release`) or
  bundle them as they are; the recommendation is to bundle, since `release.json` records each
  component's commit.
- At step 5: the publish is one flat folder, `artifacts/<version>/`, with the launcher and
  every component's window beside each other and one copy of the desktop runtime; a file two
  publishes write with different bytes stops the release (the kit's ADR 0036). The consoles
  are not bundled: their base runtime differs from the desktop one in three files (ADR 0002).
  `release.json` and the zip `AstroDarkroom-<version>-win-x64.zip` with its `.sha256` land in
  `artifacts/`. The tag is `astrodarkroom-vX.Y.Z`.
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
