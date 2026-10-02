# 0004. Validation on the reference image runs outside the gate, on a git-ignored copy

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

The tool was built to reproduce, then automate, measurements made by hand on one image in the
conversation that started it: `de_printat.png`, the owner's Milky Way core, 5938 x 3888, 16-bit
RGB PNG, 103 387 067 bytes, kept in `E:\Photografie\Claude`. The kit keeps acceptance figures in
`docs/validation.json`, read by `ValidationTests`, and lists every test data file in a manifest
with its SHA-256; files too large for git are "large", found in `testdata/large/` or in the
folder an environment variable names. The analysis of the whole image takes about 11 seconds.
The plan proposed `APA_TESTDATA_LARGE`, set for the user, naming `E:\Photografie\Claude`, as
FlatCheck's `FLATCHECK_TESTDATA_LARGE` names its masters (its ADR 0004). On 2026-10-02 the owner
answered: no variable; it is one file, copy it into `AstroPhotoAnalyzer.Tests/testdata/large/`.

## Decision

- `ValidationTests` carry the xUnit trait `Category=Validation`. `scripts/verify.ps1` runs the
  tests with `kit.json`'s filter `Category!=Validation`; `/validate` and `/release` run the cases,
  and so does anyone with `--filter "Category=Validation"`.
- The reference image is a copy, made by hand, in `AstroPhotoAnalyzer.Tests/testdata/large/`,
  which the project's `.gitignore` excludes. `AstroPhotoAnalyzer.Tests/testdata/manifest.json`
  lists it with its SHA-256; `scripts/check-testdata.ps1`, the gate's first step after the sync
  check, verifies the hash and only warns when the file is absent; a validation case whose file
  is absent returns early with a message.
- `APA_TESTDATA_LARGE` stays in `kit.json` only because the kit's `kit-config.ps1` requires a
  name for the override; nobody sets it.
- `docs/validation.json` records, per case, the image, the parameters and the expected figures
  with a tolerance; `docs/validation.md` says where each figure came from, beside the numbers
  made by hand in 2026-09.

## Alternatives rejected

- **The environment variable.** It suits FlatCheck's thirty files, 1.4 GB, which live in their
  session folders; here it would be a machine-wide setting for one file. The copy needs no
  setting, works the same from every shell and every tool that runs the tests, and is the
  owner's choice.
- **Validation in the gate.** The file exists on one machine: a gate that passes without it and
  fails with it is not one gate. And every commit would pay the analyses on top of the tests.
- **Commit the image.** 103 MB in the history of the `photo` repository for good; sample images
  never enter the repository.
- **The figures as literals in the tests.** Figures measured on real data live in a document,
  with a date and a tolerance, so a change is a visible diff in `docs/`, not an assert edited in
  passing (the kit's rule "No test edited to pass").

## Consequences

- A fresh clone, another machine or a new worktree has no copy: the image is copied into its
  `testdata/large/` by hand (the test data README says so); until then `/validate` reports the
  cases "not measured" and the gate prints a yellow warning.
- The manifest guards the copy, not the original: if the owner re-exports `de_printat.png`, the
  copy and the recorded figures stay as they were, and replacing the copy is a deliberate change,
  with `check-testdata.ps1 -Update` and the cases re-measured.
- A formula change can pass the gate and move a validation figure; `/wrap-up` and `/release` ask
  for `/validate`, and a drift is reported, never corrected silently.
