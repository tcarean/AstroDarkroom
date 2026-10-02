# Testing

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §11. **Covers:** the
> shared fixtures `AstroPhotoAnalyzer.Tests/SyntheticRasters.cs`,
> `AstroPhotoAnalyzer.Tests/Scenes.cs`, `AstroPhotoAnalyzer.Tests/FitsTestData.cs`,
> `AstroPhotoAnalyzer.Tests/TiffTestData.cs`, `AstroPhotoAnalyzer.Tests/PngTestData.cs`,
> `AstroPhotoAnalyzer.Tests/IccTestData.cs`, `AstroPhotoAnalyzer.Tests/ImageFiles.cs`,
> `AstroPhotoAnalyzer.Tests/TempWorkspace.cs`, the project file
> `AstroPhotoAnalyzer.Wpf.Tests/AstroPhotoAnalyzer.Wpf.Tests.csproj` that borrows five of
> them, and the window's harness `AstroPhotoAnalyzer.Wpf.Tests/StaFixture.cs` and
> `AstroPhotoAnalyzer.Wpf.Tests/WindowSmokeTests.cs`. A change to the code this describes
> means this note is updated in the same task, or deleted with the decision it explains.

## Testing

| kind | where | what |
|---|---|---|
| synthetic, known answer | `AstroPhotoAnalyzer.Tests/*AnalyzerTests.cs` | Gaussian stars of known FWHM, injected noise sigma, ramps, casts, corners, upscales; each analyzer recovers the truth within a stated tolerance |
| fixtures | `SyntheticRasters`, `Scenes` (DarkSky with every defect of the reference image, CleanSky, MonoSky), `FitsTestData`, `TiffTestData` (hand-built 16-bit RGB TIFF with ICC), `PngTestData` (hand-built 16-bit PNG with iCCP, own CRC-32), `IccTestData` (v2/v4 profiles), `ImageFiles` (ImageSharp-encoded 8-bit files), `TempWorkspace` | the 16-bit TIFF/PNG fixtures are hand-built because ImageSharp 3.1 cannot encode them |
| golden | `GoldenOutputTests`, `Golden/*.txt` | the console's whole output on the scenes, version banner and temp path scrubbed; `APA_UPDATE_GOLDEN=1` re-approves |
| console | `CliTests`, `OptionsTests` | exit codes, `-o`, parsing, parameter mapping |
| recommendations | `RecommendationsTests` | every flag the reference image raised, on the dark scene |
| plumbing | `LicenseHeaderTests` (every source file starts with the notice; explicit project list), `VersionTests` (version/copyright from props, banner on line 1), `ThirdPartyTests` (csproj <-> `ThirdParty.Components` <-> notices headings) | |
| window | `Wpf.Tests/WindowSmokeTests` (renders the window's *content*, About, every tab with a real result, the error card; fails on any binding error; `APA_DUMP_SCREENS=<dir>` saves PNGs), `CultureTests` (report identical under ro-RO), `ParameterFieldsTests`, `ViewModelTests` | one STA thread with a message loop per process (`StaFixture`) |
| acceptance | `docs/validation.md` | the tool on the reference image against the hand-made numbers |

```bash
dotnet test AstroPhotoAnalyzer.sln
```
