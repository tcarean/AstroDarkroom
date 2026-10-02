# 0005. Validation on a real pair runs outside the gate, on git-ignored copies in tests/testdata/large/

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Until 2026-10-02 the only figures measured on real files were the "Memory, measured" table of
`CLAUDE.md`, taken by hand on a 5794 x 3637 16-bit pair of about 108 MB each; that pair was not
found under `E:\Photografie` on 2026-10-02. The kit keeps measured figures in
`docs/validation.json`, read by `ValidationTests`, and lists every test data file in a manifest
with its SHA-256; files too large for git are "large", found in `testdata/large/` or in the
folder an environment variable names. Three exports of one frame are in
`E:\Photografie\Claude` on this machine: `de_printat.png` (5938 x 3888, Rgb48, 103 387 067
bytes), `de_printat_PRINT_preview_16bit.tif` (the same frame after the print-preview stretch,
5938 x 3888, Rgb48, 130 166 332 bytes) and `de_printat_50x35_TIFF8.tif` (cropped to 50:35, 5554
x 3888, Bgr24, 20 099 182 bytes). The first two are a before and after of one processing, which
is the use the tool was built for; the first and the third differ in size and bit depth.
AstroPhotoAnalyzer chose copies in `testdata/large/` over a variable the same day (its
ADR 0004).

## Decision

- `ValidationTests` carry the xUnit trait `Category=Validation`. `scripts/verify.ps1` runs the
  tests with `kit.json`'s filter `Category!=Validation`; `/validate` and `/release` run the
  cases, and so does anyone with `--filter "Category=Validation"`.
- The three files are copies, made by hand, in `tests/testdata/large/`, which the project's
  `.gitignore` excludes. `tests/testdata/manifest.json` lists them with their SHA-256;
  `scripts/check-testdata.ps1` verifies the hashes and only warns when a file is absent; a case
  whose file is absent returns early with a message.
- `IVC_TESTDATA_LARGE` is in `kit.json` only because `kit-config.ps1` requires a name for the
  override; nobody sets it.
- Two cases: V-001, the matched pair (the PNG against the 16-bit TIFF); V-002, the mismatched
  one (the PNG against the 8-bit TIFF, padded). Each guards what the probe reports, the canvas,
  the load, and the private memory the load adds and the release gives back. The memory figures
  are guarded within 60 MB, because they depend on the machine and on the process; the decode
  time is printed and never guarded.
- The 2026-09-08 memory table stays, as history, in `docs/validation.md`.

## Alternatives rejected

- **An environment variable naming `E:\Photografie\Claude`.** A machine-wide setting for three
  files; the copies need no setting and work the same from every shell and tool.
- **Validation in the gate.** The files exist on one machine: a gate that passes without them
  and fails with them is not one gate.
- **Commit the images.** 254 MB in the history of the `photo` repository for good; sample images
  never enter the repository.
- **A tight tolerance on memory.** Private bytes move with the collector, the runtime and what
  else ran in the test host; a tolerance smaller than that fails on noise. What is guarded is
  that the pair costs about two Bgra32 buffers and that the release gives them back.
- **A full-size synthetic pair from `TempImages`.** It tests the code, not the codecs on real
  exports; the unit tests already cover the formats at small sizes.

## Consequences

- A fresh clone, another machine or a new worktree has no copies: the files are copied into its
  `tests/testdata/large/` by hand (the test data README says so); until then `/validate` reports
  the cases "not measured" and the gate prints a yellow warning.
- The manifest guards the copies, not the originals: replacing a copy is a deliberate change,
  with `check-testdata.ps1 -Update` and the cases re-measured.
- A change to the loader or the release path can move a memory figure; `/wrap-up` asks for
  `/validate` when numeric code changed (`docs/skills.md` names the folders), and a drift is
  reported, never corrected silently.
