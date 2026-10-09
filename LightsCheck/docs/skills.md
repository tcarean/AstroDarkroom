# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (the kit's
ADR 0018). What LightsCheck needs on top of them is here, one section per skill; each skill
reads this file before its first step, and each section says at which step it applies.
Values (the solution, the folders, the publish list, what the auditor may also read) are in
`kit.json`, not here.

## /new-feature

- At step 1: a console requirement also states the exit code (`docs/requirements.md` has the
  console contract block).
- At step 2: the console and the core are tested in `tests/LightsCheck.Tests`, the window in
  `tests/LightsCheck.Wpf.Tests` (a tab with a result is rendered in `WindowSmokeTests`); a
  console mode also gets a golden scenario in `GoldenOutputTests` (the rule "Golden files"). A figure
  measured on a real session goes to `docs/validation.json` as the skill says, with the session
  named relative to `LIGHTSCHECK_TESTDATA_LARGE`.
- At step 3: measure on the real sessions before you design (`docs/validation.md` lists them;
  `docs/validation/2026-10-06-veil/` holds the hand analysis the tool reproduces).
- At step 5: `LightsCheck.Core` first (`Analysis/` decides numbers, `Report/` decides words,
  `Runs/` chains them), then the console and the window, which only call `Runs/`. Nothing is
  computed outside the core (the rule "Computed only in the core").
- At step 6: numeric code is `Imaging/Stats.cs`, `Analysis/` and `Report/` (the rule "Measured
  on the sessions"). The golden files are regenerated with `LIGHTSCHECK_UPDATE_GOLDEN=1` and
  `--filter GoldenOutputTests`.
- At step 8: `README.md` is in Romanian and names every console option.

## /wrap-up

- At step 2: numeric code is `src/LightsCheck.Core/Imaging/Stats.cs`, `Analysis/` and
  `Report/` (the rule "Measured on the sessions").

## /validate

- At step 1: the real sessions are under `E:\Photografie\Astro`, the folder
  `LIGHTSCHECK_TESTDATA_LARGE` names; `docs/validation.json` names each session relative to it.
  The cases read whole sessions of raws and take minutes (ADR 0004).
- At step 3: the table's second column is the session, not a fixture.

## /release

- At step 5: the console is published as the single copyable `lightscheck.exe` under
  `console/` and the window as `LightsCheck.Wpf.exe` under `wpf/`, both with the libraw DLLs
  embedded; the tag is `lightscheck-vX.Y.Z`.

## /mutate

- At step 1: pass a filter on this project (`scripts/mutate.ps1 -Mutate "**/File.cs"`).

## /docs-health

(none; `audit.alsoRead` in `kit.json` lets the auditor read `THIRD-PARTY-NOTICES.md` and the
golden texts under `tests/LightsCheck.Tests/Golden/`, the console's expected output, not code)
