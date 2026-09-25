# Test data

FlatCheck has two kinds of test data and one manifest.

- **Generated in the test.** Every FITS the unit and golden tests use is written by
  `FitsTestData` (synthetic vignetting, gradients, blotches, CFA mosaics) and every camera
  preview by `TestImages.JpegOf`. A generator beats a stored file whenever the generator is
  simpler than the file is large, and a synthetic with known truth is a better fixture than a
  real master, because the right answer is known exactly. The golden texts under
  `FlatCheck.Tests/Golden/` are compared byte for byte and re-approved only with
  `FLATCHECK_UPDATE_GOLDEN=1` after the owner has read the diff. So `fixtures/` is empty.
- **Large, outside git.** The real masters and raws under `E:\Photografie\Astro\Flats and Darks`
  that the acceptance figures in `docs/validation.json` were measured on. `manifest.json` lists
  them with `"location": "large"`, a path relative to the folder named by
  `FLATCHECK_TESTDATA_LARGE` (default `FlatCheck.Tests/testdata/large/`, git-ignored), their
  SHA-256, and what each demonstrates. `scripts/check-testdata.ps1` reports an absent large
  file as a warning locally and as an error in CI only when `requiredInCi` is true.
  `ValidationTests` reach them through `TestData.Large(name)` and return early with a message
  when the file is absent (xUnit 2 has no dynamic skip). They carry the trait
  `Category=Validation` and are not part of `scripts/verify.ps1`; `/validate` runs them.

After a master is re-stacked on purpose:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
```

Then re-measure its cases (`/validate`) and record the change in `docs/validation.md`.
