# Test data

AstroPhotoAnalyzer has two kinds of test data and one manifest.

- **Generated or hand-built in the test.** Every raster the unit and golden tests use is
  written by `SyntheticRasters` and `Scenes` (a dark sky with every defect of the reference
  image, a clean sky, a mono sky); the 16-bit TIFF and PNG fixtures are hand-built by
  `TiffTestData` and `PngTestData`, because ImageSharp 3.1 cannot encode them, and that is
  the more honest test (`docs/design/dependencies.md`); FITS files by `FitsTestData`, ICC
  profiles by `IccTestData`, 8-bit files by `ImageFiles`. The golden texts under
  `AstroPhotoAnalyzer.Tests/Golden/` are compared byte for byte and re-approved only with
  `APA_UPDATE_GOLDEN=1`, as the rule "Golden files" says. So `fixtures/` is empty.
- **Large, outside git.** The reference images the acceptance figures in
  `docs/validation.json` were measured on (`de_printat.png`, a Milky Way core, 5938 x 3888,
  16-bit RGB PNG, 103 MB, and nine FITS frames) and the lab's two ICC profiles, twelve files,
  1.4 GB. They live outside the repository, in the owner's folder for sets kept for the tests
  alone, `E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer` (ADR 0088), the same folder
  for the main checkout and for every worktree, so no task copies them. `manifest.json`, here,
  lists each with `"location": "large"`, its SHA-256, where it came from and what it
  demonstrates. `ValidationTests` and `StarPairTests` reach them through `TestData.Large(name)`,
  which looks in that folder, or in the one `APA_TESTDATA_LARGE` names when it is set, and
  return early with a message when a file is absent (xUnit 2 has no dynamic skip). They carry
  the trait `Category=Validation` and are not part of `scripts/verify.ps1`; `/validate` runs
  them. `scripts/check-testdata.ps1`, the kit's, looks only in the folder `APA_TESTDATA_LARGE`
  names (or in a `large/` beside this file, which no longer exists): the gate, which does not
  set it, lists the twelve as absent, in yellow, and `/validate` sets it so that the hashes are
  checked.

On a new machine or a fresh clone, copy the twelve files into the same folder, or set
`APA_TESTDATA_LARGE` to wherever they are. After a file is replaced on purpose, from
PowerShell:

```powershell
$env:APA_TESTDATA_LARGE = 'E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer'
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
```

Then re-measure its cases (`/validate`) and record the change in `docs/validation.md`.
