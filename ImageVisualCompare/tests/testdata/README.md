# Test data

ImageVisualCompare has one kind of test data and one manifest.

- **Generated in the test.** Every image the unit tests decode is written by `TempImages` in
  the test project: PNG and TIFF in six pixel formats, random pixels, a corrupt file. So there
  is no `fixtures/` folder: a generator that fits in eighty lines beats binary files in the
  repository, and it keeps the formats under test visible in code (ADR 0026).
- **Large, outside git.** The three images `manifest.json` lists with `"location": "large"`: the
  owner's Milky Way core as a 16-bit PNG, the same frame as a 16-bit TIFF after the
  print-preview stretch, and the same frame cropped and exported as 8-bit TIFF. They are the
  real pairs the figures in `docs/validation.json` are measured on: a matched pair and a
  mismatched one. Each is a copy, made by hand, in `tests/testdata/large/`, which git ignores
  (ADR 0005); `IVC_TESTDATA_LARGE` may name another folder, as `scripts/check-testdata.ps1`
  allows, and nobody sets it. The script reports a missing one as a warning locally and as an
  error in CI only when `requiredInCi` is true. `ValidationTests` reach them through
  `TestData.Large(name)` and return early with a message when one is absent (xUnit 2 has no
  dynamic skip). They carry the trait `Category=Validation` and are not part of
  `scripts/verify.ps1`; `/validate` runs them.

After a file is replaced on purpose:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
```

Then re-measure its cases (`/validate`) and record the change in `docs/validation.md`.
