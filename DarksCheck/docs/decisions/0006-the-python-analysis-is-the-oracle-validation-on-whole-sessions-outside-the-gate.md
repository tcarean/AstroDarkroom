# 0006. The Python analysis of the Smida session is the numeric oracle; the validation runs on whole sessions outside the gate

Date: 2026-10-11
Status: accepted 2026-10-11

## Context

The nine checks were first run on 2026-10-11 with Python scripts on the Smida session (20
darks, 30 biases, 32 lights, 1.9 GB of raws) and on the library of 2026-10-10 (30 darks), and
their figures (433 pixels above 20 ADU, the 0.83 to 1.08 warming, the 8.9 ratio, and so on)
are in `docs/validation/2026-08-12-smida/results.json`, `results_supplementary.json` and the
CSV files. The sessions cannot live in git and a full read of one takes minutes; the gate must
stay under two minutes. LightsCheck's ADR 0004 solved the same problem.

## Decision

The figures of the Python analysis are the oracle: every validation case in
`docs/validation.json` records what the C# measures on a real session and is checked against
the Python figure with a tolerance stated in `docs/validation.md`, kind by kind. The cases name
a session relative to the folder `DARKSCHECK_TESTDATA_LARGE` points at
(`E:\Photografie\Astro`, which holds both the Smida session under `Milkyway-Core/` and the
library under `Flats and Darks/Darks/`), run with `Category=Validation` outside the gate
(`kit.json`'s `testFilter`), and return early with a message when the session is absent.
Every frame the gate's tests read is generated in the test (`SessionFixture`, `FitsTestData`,
`SyntheticFrame`); the manifest lists no large file. A figure changes only after a
re-measurement, with a dated line in `docs/validation.md`.

## Alternatives rejected

- **Hashing the sessions in the test-data manifest.** Gigabytes to hash on every
  `check-testdata.ps1` run; the folder path in `validation.json` is enough to find them.
- **Running the Python scripts in the gate.** A Python environment with rawpy on every
  machine and minutes per run, for figures that do not change.
- **Small real raws as fixtures.** A raw cut to a few kilobytes is no longer a raw libraw can
  open; the synthetic FITS frames carry the same cards and known answers.

## Consequences

`ValidationTests` reads `validation.json`; `/validate` re-measures with the variable set.
Each plan step adds its cases (V-001 and V-002 for the inventory, the checks from step 2).
