# 0004. The input is a Siril session folder; the validation runs on whole sessions outside the gate

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

The owner first asked for "a folder with the raws" and, on 2026-10-09, for consistency with
FlatCheck, chose the Siril session folder as the input: `lights/` and `biases/` are found in
it, and the biases give the read noise without typing it. The real sessions are gigabytes of
raws (the Veil session: 87 NEF, 1.9 GB) that cannot live in git, and a decode of all of them
takes minutes.

## Decision

The console and the window take one session folder. The lights are the frames of `lights/`
when it exists (an empty `lights/` is an error, never a fallback: the session root holds
`result.fit` and the other stacked results, which must not pass for lights), or of the folder
itself when there is no `lights/` (a folder of raws handed over without the Siril layout still
works); the biases are `biases/`, optional. A folder's frames
are its camera raws when there are any, otherwise its FITS files, never both (a folder with
both holds converted copies). The validation cases (`docs/validation.json`) name a session
relative to the folder `LIGHTSCHECK_TESTDATA_LARGE` points at, run with
`Category=Validation` outside the gate (`kit.json`'s `testFilter`), and return early with a
message when the session is absent; every frame the gate's tests read is generated in the
test (`SessionFixture`). The manifest lists no large file: the sessions are folders of raws
the owner keeps, not fixtures.

## Alternatives rejected

- **Only the raws folder.** Loses the biases, and differs from FlatCheck for no gain.
- **A session fixture of real raws in `tests/testdata/large/` with hashes.** 1.9 GB per session
  to hash on every `check-testdata.ps1` run; the session folder is the unit, and its path in
  `validation.json` is enough to find it.
- **Mixing raws and FITS in one folder.** A converted copy beside its original would be counted
  twice.

## Consequences

`SessionFolder` is the one place that knows the layout. A test that needs frames builds them
(`SessionFixture`, `FitsTestData`). Re-measuring is `/validate` with the variable set to
`E:\Photografie\Astro`.
