# 0004. Validation on the real masters runs outside the commit gate, on files that stay where they are

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

The kit keeps acceptance figures in `docs/validation.json`, read by `ValidationTests` inside the
gate, and its sample fixtures are a few hundred bytes. FlatCheck's acceptance figures were
measured on real masters: fifteen `pp_flat_stacked.fit` of 96 MB each under
`E:\Photografie\Astro\Flats and Darks`, plus the first raw of each session's `flats/` (18-21 MB).
One structure run on a 24 MP master takes about 3 s and several hundred MB; the smooth mode 4-5 s;
the profile comparison parses the Adobe files of the camera's mount on the machine. A validation
pass over the library is a minute or more, on data that exists on one machine and cannot enter
git.

## Decision

`ValidationTests` carry the xUnit trait `Category=Validation`. `scripts/verify.ps1` runs the
tests with `--filter "Category!=Validation"`; `/validate` and `/release` run the validation
cases, and so does anyone with `--filter "Category=Validation"`. The masters and raws stay in
their session folders: `FLATCHECK_TESTDATA_LARGE` names that root, `FlatCheck.Tests/testdata/manifest.json`
lists every file relative to it with its SHA-256 (30 files, hashed on 2026-09-25 in 14 s),
`scripts/check-testdata.ps1` verifies the hashes as the gate's first step and only warns when a
file is absent, and a validation test whose file is absent returns early with a message.
`docs/validation.json` records, per case, the master, the parameters and the expected figures
with a tolerance; `docs/validation.md` says where each figure came from.

## Alternatives rejected

- **Validation in the gate, like the kit.** A minute or more per commit on top of the 3 minutes
  the tests already cost, for figures that change only when a formula changes; and a gate that
  passes on a machine without the data but fails on the owner's is not one gate.
- **Copy the masters into `testdata/large/`.** 1.4 GB duplicated for no reason; the session
  folders are the masters' home and the README names them.
- **Record the figures in the tests as literals.** The guide's rule: figures measured on real
  data live in a document, with a date and a tolerance, so a change is a visible diff in
  `docs/`, not an assert edited in passing (rule 3).

## Consequences

A formula change can pass the gate and move a validation figure; `/wrap-up` and `/release` ask
for `/validate`, and the drift is reported, never corrected silently. A re-stacked master fails
`check-testdata.ps1` until the manifest is updated on purpose with `-Update` and the cases
re-measured. The figures recorded on 2026-09-25 are the current build's output on each master,
with the earlier hand measurements quoted beside them in `docs/validation.md` where they exist.
