# Test data

AstroPhotoAnalyzer has two kinds of test data and one manifest.

- **Generated or hand-built in the test.** Every raster the unit and golden tests use is
  written by `SyntheticRasters` and `Scenes` (a dark sky with every defect of the reference
  image, a clean sky, a mono sky); the 16-bit TIFF and PNG fixtures are hand-built by
  `TiffTestData` and `PngTestData`, because ImageSharp 3.1 cannot encode them, and that is
  the more honest test (`docs/design/dependencies.md`); FITS files by `FitsTestData`, ICC
  profiles by `IccTestData`, 8-bit files by `ImageFiles`. The golden texts under
  `AstroPhotoAnalyzer.Tests/Golden/` are compared byte for byte and re-approved only with
  `APA_UPDATE_GOLDEN=1` after the owner has read the diff. So `fixtures/` is empty.
- **Large, outside git.** The reference image `de_printat.png` (a Milky Way core, 5938 x 3888,
  16-bit RGB PNG, 103 MB) that the acceptance figures in `docs/validation.json` were measured
  on. It is a copy, made by hand, of the owner's file in `E:\Photografie\Claude`, kept in
  `large/` beside this file, which git ignores (ADR 0004). `manifest.json` lists it with
  `"location": "large"`, its SHA-256 and what it demonstrates. `APA_TESTDATA_LARGE` could
  point the scripts and the tests at another folder instead; nobody sets it.
  `scripts/check-testdata.ps1` reports the image absent as a warning locally and as an error
  in CI only when `requiredInCi` is true. `ValidationTests` reach it through
  `TestData.Large(name)` and return early with a message when it is absent (xUnit 2 has no
  dynamic skip). They carry the trait `Category=Validation` and are not part of
  `scripts/verify.ps1`; `/validate` runs them.

On a new machine or a fresh clone, copy the image into `large/` again. After the image is
replaced on purpose:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
```

Then re-measure its cases (`/validate`) and record the change in `docs/validation.md`.
