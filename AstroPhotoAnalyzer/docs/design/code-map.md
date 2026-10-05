# File map

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §16; the lines that
> named the old documents now name the new ones. What each folder owns is `CLAUDE.md`,
> "Layout". **Covers:** `AstroPhotoAnalyzer.Core/`, `AstroPhotoAnalyzer/` and
> `AstroPhotoAnalyzer.Wpf/`, one line per file; the tests are listed by pointer only. A change
> to the code this describes means this note is updated in the same task, or deleted with the
> decision it explains.

## File map

```
Directory.Build.props                  version, copyright
AstroPhotoAnalyzer.sln
LICENSE, THIRD-PARTY-NOTICES.md, CLAUDE.md, README.md, CHANGELOG.md
kit.json, global.json, Directory.Packages.props   the kit's process (CLAUDE.md, "Layout")
Icon/make_icon.py, Icon/AstroPhotoAnalyzer.ico
docs/requirements.md                   the behaviour, with IDs
docs/decisions/                        the ADRs
docs/design/                           these notes; README.md is the index
docs/validation.md, validation.json    the figures on the reference image
docs/plans/                            approved plans, the implementation plan of 2026-09-20 first

AstroPhotoAnalyzer.Core/
  Input/Raster.cs                      Raster, SourceInfo
  Input/RasterOps.cs                   subsample, crop, gamma, levels, L* plane, linear guard
  Input/ColorMath.cs                   sRGB, luminance, L*, paper black
  Input/Stats.cs                       percentiles (numpy), SeededRandom
  Input/IFrameReader.cs, FitsReader.cs, ImageSharpReader.cs, IccProfileInfo.cs, IccPaperBlack.cs (the Dmax of a printer profile, R-028), ImageLoader.cs
  Input/Wcs.cs                         the TAN WCS of a plate-solved FITS, pixel <-> sky (R-018)
  Input/Astrometry.cs                  unit vectors, separation, B1950 -> J2000
  Input/DarkNebulaCatalogue.cs, ldn.txt  the Lynds catalogue (embedded resource) and its reader
  Analysis/FileFacts.cs, BitDepthAnalyzer.cs, CropAnalyzer.cs, TonalAnalyzer.cs,
           DustAnalyzer.cs (the dust and the sky on patches, with or without the catalogue),
           PrintStretchSimulator.cs (the grid, the ladder, the recommendation, the cloud rows),
           PrintPreview.cs (a row as an image, for the preview window),
           NoiseAnalyzer.cs, BackgroundAnalyzer.cs,
           StarAnalyzer.cs, PrintSizeAnalyzer.cs, QuantizationAnalyzer.cs, JpegAnalyzer.cs
  Report/Recommendation.cs             Verdict, Topic, Recommendation, VerdictText
  Report/Recommendations.cs            the rules
  Report/ReportWriter.cs               the text
  Report/Fmt.cs                        invariant number formatting
  Report/RunReport.cs                  banner, warranty
  Runs/AnalysisRun.cs                  the chain, AnalysisResult, RefusedImageException
  Parameters.cs, Thresholds.cs, ThirdParty.cs, Version.cs

AstroPhotoAnalyzer/                    Program.cs, Options.cs, AnalyzerCli.cs

AstroPhotoAnalyzer.Wpf/
  App.xaml(.cs), MainWindow.xaml(.cs), Theme/Dark.xaml
  Interop/DarkTitleBar.cs, Mvvm/ObservableObject.cs, Mvvm/RelayCommand.cs
  Presentation/AppIcon.cs, BarsCanvas.cs, Converters.cs, RasterBitmaps.cs, VerdictPalette.cs
  ViewModels/MainViewModel.cs, ParameterFields.cs, ResultTabs.cs, Rows.cs, AboutViewModel.cs,
             MapViewModel.cs (the map window, R-130), PreviewWindowViewModel.cs (the preview window, R-131),
             IccPickerViewModel.cs (the profile window, R-132)
  Views/ReportView, TonesView, ExportView, ResolutionView, BackgroundView, PreviewView (.xaml),
        ResultViews.xaml.cs, AboutWindow.xaml(.cs), BusyOverlay.xaml(.cs),
        MapWindow.xaml, PreviewWindow.xaml, OverlayWindows.xaml.cs, IccPickerWindow.xaml(.cs)

AstroPhotoAnalyzer.Tests/              see testing.md; Golden/*.txt; testdata/ (manifest, large/)
AstroPhotoAnalyzer.Wpf.Tests/          StaFixture, WindowSmokeTests, CultureTests, ParameterFieldsTests, ViewModelTests, OverlayWindowTests, IccPickerTests
```
