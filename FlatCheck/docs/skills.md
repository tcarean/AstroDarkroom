# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (kit ADR
0018, FlatCheck ADR 0049). What FlatCheck needs on top of them is here, one section per skill;
each skill reads this file before its first step, and each section says at which step it
applies. Values (the solution, the folders, the publish list, what the auditor may also read)
are in `kit.json`, not here.

## /new-feature

- At step 1: a console requirement also states the exit code (`docs/requirements.md` has the
  contract block).
- At step 2: the console and the core are tested in `FlatCheck.Tests`, the window in
  `FlatCheck.Wpf.Tests`. A console mode also gets a golden scenario in `GoldenOutputTests` and,
  if it opens a numeric branch the parity set does not reach, a `ParityGoldenTests` scenario
  (`docs/design/process.md`, "Adding a golden scenario" and "Adding a parity golden scenario").
  A figure measured on a real master goes to `docs/validation.json` as the skill says.
- At step 3: measure on the real masters before you design. On this project a design chosen
  without a measurement has been wrong three times (`docs/design/README.md` lists the notes;
  `docs/validation.md` lists the masters).
- At step 5: `FlatCheck.Core` first (`Analysis/` decides numbers, `Report/` decides words,
  `Runs/` chains them), then the console and the window, which only call `Runs/`. Nothing is
  computed outside the core (the rule "Computed only in the core").
- At step 6: numeric code is `Metrics.cs`, `Stats.cs`, `Analysis/` and `Report/` (the rule
  "Formula changes re-validate"). The golden files, the parity ones included, are re-approved
  with `FLATCHECK_UPDATE_GOLDEN=1` only after the owner has read the diff and said yes.
- At step 8: `README.md` is in Romanian; the "Opțiuni" section lists every console flag
  (`docs/design/process.md`, "The README Opțiuni section").

## /wrap-up

- At step 2: numeric code is `FlatCheck.Core/Metrics.cs`, `Stats.cs`, `Analysis/` and `Report/`
  (the rule "Formula changes re-validate"); a `Golden/parity_*.txt` that changed is a finding
  like a moved figure, and a re-approval there is a C#-only decision (ADR 0047).

## /validate

- At step 1: the real masters are under `E:\Photografie\Astro\Flats and Darks`, the folder
  `FLATCHECK_TESTDATA_LARGE` names; `FlatCheck.Tests/testdata/manifest.json` lists them with
  their hashes. The validation tests read 96 MB files and take minutes (ADR 0004).
- At step 3: the table's second column is the master, not a fixture.

## /release

- At step 3: ask for `/validate` whether or not a formula moved; a release is the moment a
  stale figure on the real masters gets noticed.
- At step 5: the console is published as the single copyable `flatcheck.exe` under `console/`
  and the window under `wpf/`, both with `lensfun/` beside them; the tag is `flatcheck-vX.Y.Z`.
- At step 7: the README's build section names the publish commands; `artifacts/` is what they
  produce.

## /mutate

- At step 1: always pass a filter on this project. With about 940 core tests per mutant, a
  whole-core run takes hours; `scripts/mutate.ps1 -Mutate "**/Metrics.cs"` is the shape.

## /docs-health

(none; `audit.alsoRead` in `kit.json` lets the auditor read `THIRD-PARTY-NOTICES.md` and the
golden texts under `FlatCheck.Tests/Golden/`, the console's expected output, not code)
