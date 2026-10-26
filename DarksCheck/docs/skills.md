# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (the kit's
ADR 0018). What DarksCheck needs on top of them is here, one section per skill; each skill
reads this file before its first step, and each section says at which step it applies.
Values (the solution, the folders, the publish list, what the auditor may also read) are in
`kit.json`, not here.

## /new-feature

- At step 1: a console requirement also states the exit code (`docs/requirements.md` has the
  console contract block).
- At step 2: the console and the core are tested in `tests/DarksCheck.Tests`, the window in
  `tests/DarksCheck.Wpf.Tests` (a tab with a result is rendered in `WindowSmokeTests`;
  `DARKSCHECK_DUMP_SCREENS=<folder>` saves a PNG per screen); a console mode also gets a
  golden scenario in `GoldenOutputTests` (the rule "Golden files"). A figure
  measured on a real session goes to `docs/validation.json` as the skill says, with the
  session named relative to `DARKSCHECK_TESTDATA_LARGE`.
- At step 3: measure on the real sessions before you design (`docs/validation.md` lists them;
  `docs/validation/2026-08-12-smida/` holds the hand analysis the tool reproduces).
- At step 5: `DarksCheck.Core` first (`Analysis/` decides numbers, `Thresholds.cs` holds the
  constants, `Report/` decides words and rows, `Runs/` chains them), then the console and the
  window, which only call `Runs/` and bind the report's rows. Nothing is computed outside the
  core (the rule "Computed only in the core").
- At step 6: numeric code is `Imaging/Stats.cs`, `Analysis/`, `Thresholds.cs` and `Report/`
  (the rule "Measured on the sessions"). The golden files are regenerated with
  `DARKSCHECK_UPDATE_GOLDEN=1` and `--filter GoldenOutputTests`.
- At step 8: `README.md` is in English and names every console option.

## /wrap-up

- At step 2: numeric code is `src/DarksCheck.Core/Imaging/Stats.cs`, `Analysis/`,
  `Thresholds.cs` and `Report/` (the rule "Measured on the sessions").

## /validate

- At step 1: the real sessions are under `E:\Photografie\Astro`, the folder
  `DARKSCHECK_TESTDATA_LARGE` names; `docs/validation.json` names each session relative to it
  (the library of darks sits under `Flats and Darks/`). The cases read whole folders of raws
  (ADR 0006).
- At step 3: the table's second column is the session, not a fixture.

## /release

- The tag is `darkscheck-vX.Y.Z`; the publish list in `kit.json` names the console
  (`darkscheck.exe` under `console/`) and the window (`DarksCheck.Wpf.exe` under `wpf/`), each a
  single file with the libraw DLLs embedded.

## /docs-health

- The auditor may also read `THIRD-PARTY-NOTICES.md`, the golden texts and the Smida report
  (`kit.json`, `audit.alsoRead`).

## /mutate

- The core's numeric files are the candidates: `Analysis/` and `Imaging/Stats.cs`.
