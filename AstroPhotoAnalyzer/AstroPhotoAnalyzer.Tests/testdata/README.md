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

## Kept inputs of recorded measurements

The same folder holds the files that measurements recorded in the decisions were made on and
that no test reads (ADR 0110): they are copies of files in the owner's catalogue, which is
reorganised, kept so that a measurement can be made again on the very file. They are not in
`manifest.json`, so `check-testdata.ps1` does not look at them; a file moves into the manifest
when a validation case is made from it. Copied on 2026-11-17, each hash equal to its
original's:

| file | bytes | SHA-256 | what it is, and what rests on it |
| --- | --- | --- | --- |
| `denoised_syqon_studio_in_max_0.75_stretch0.17-log4_Gentle_lowdust_sat0.25_signed.tif` | 135 467 874 | `2352d21a041b60ecf7dfc50aaa544a8854fa5ac7edbf5be1675f768156459a54` | the Smida Milky Way TIFF of 2026-11-16, sRGB, 5837 x 3867: the image of ADR 0109 and of R-187's example |
| `starless_denoised_syqon_studio_in_max_0.75_stretch0.12-logd4_Gentle_sat0.25.tif` | 135 467 896 | `c63022bb29aaef2c60abb4d0a2a37eff0767cf23ba421fd95d901f13e669f43b` | the Smida TIFF of ADR 0081 (the gamut rule scored against Lightroom's warning) and of the "Smida TIFF, WhiteWall baryta" rows of ADR 0083 and `docs/design/paper-fit.md` |
| `WhiteWall_ICC_Inkjet_Hahnemuehle_Photo_Rag_Baryta.icc` | 2 584 156 | `ae2bc3c2784737b9953106936d987fbcd8f8e025af27e876312f32d748e0ff7a` | WhiteWall's printer profile for Hahnemuhle Photo Rag Baryta (v4.3, paper black L\* 3.38): ADR 0081, ADR 0083, ADR 0109 |
| `HFA_EpsSC-P900_PK_PhotoRagBaryta.icc` | 3 000 452 | `5d9de97a77458700d410c9c8580de7b68f3b4fdf8a07f685a52a71222bbeb0ce` | Hahnemuhle's own profile for Photo Rag Baryta on the Epson SC-P900: ADR 0109 |
| `lightroom-content-credentials-2026-11-17\denoised_syqon_studio_in_max_0.75_stretch0.15_logd4.16_Gentle_lowdust_signed_Soft.tif` | 135 564 803 | `ada526e955f79e56c39b6e87fe1a1978d905331bc17224aac933c446bedf6353` | the owner's export of 2026-11-17 from Lightroom Classic 16.0, 5837 x 3867, 16-bit RGB, with the Content Credentials in a second directory that holds no image: the file the reader refused, and the measurement of ADR 0111 and of the figures R-012 gives for it |

`lightroom-captures-2026-11-17\` holds the owner's ten captures of Lightroom Classic's soft
proofing with the destination gamut warning on (relative, simulate paper and ink), behind the
table of ADR 0109. In the nine captures of 1920 x 1040 the image fills x 401 to 1518, y 128 to
868; the first is a narrower one, beside the tool. Each name gives the profile and the file
shown: `fit-to-4` is the file the tool saved before R-187, `fit-to-3.5` and `fit-to-3.0` the
trial files of that day (the built code writes the `fit-to-3.5` file sample for sample), `02`
and `04` the owner's test with a luminance-range mask on the dark tones, `07` a second capture
of the WhiteWall proof copy, `10` the file fitted for WhiteWall proofed through the P900
profile.
