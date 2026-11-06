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
  Input/LiftTable.cs                   the two linearisers: exact, or lifted with a gamma through one 16-bit table (ADR 0035)
  Input/ColorMath.cs                   sRGB, luminance, L*, L*a*b*, paper black
  Input/Stats.cs                       percentiles (numpy), robust sigma, Otsu, SeededRandom
  Input/IFrameReader.cs, FitsReader.cs, ImageSharpReader.cs, IccProfileInfo.cs, IccPaperBlack.cs (the Dmax of a printer profile, R-028), ImageLoader.cs,
        FitsWriter.cs (the saved FITS with a curve applied, R-145), AtomicFile.cs (a file written whole or not at all, R-005, R-145),
        TiffWriter.cs (the saved 16-bit TIFF of the paper fit, R-177), IccProfiles.cs (the sRGB profile it embeds when the input has none)
  Input/Wcs.cs                         the TAN WCS of a plate-solved FITS, pixel <-> sky (R-018)
  Input/Astrometry.cs                  unit vectors, separation, B1950 -> J2000, galactic latitude
  Input/DarkNebulaCatalogue.cs, ldn.txt  the Lynds catalogue (embedded resource) and its reader
  Analysis/FileFacts.cs, BitDepthAnalyzer.cs, CropAnalyzer.cs, TonalAnalyzer.cs,
           DustAnalyzer.cs (the five classes of the patches, by the band or by the modelled sky, the one measurement of the dust and the sky, the catalogue's clouds),
           PrintStretchSimulator.cs (the grid, the ladder, the recommendation, the cloud rows),
           PrintPreview.cs (a row as an image, for the preview window),
           ToneCurve.cs, CurveAdvisor.cs (the suggested curves: spline, anchors, recipe, evidence; R-140 to R-143),
           GamutAnalyzer.cs (the share outside the paper's gamut, R-144),
           GamutFit.cs (the paper fit: the table of the largest chroma over L* and hue, the knee, the recipe; R-175, R-176),
           SoftProof.cs (the soft proof: the profile's tables per intent, the display step; R-160 to R-162),
           NoiseAnalyzer.cs, BackgroundAnalyzer.cs,
           StarAnalyzer.cs, PrintSizeAnalyzer.cs, QuantizationAnalyzer.cs, JpegAnalyzer.cs
  Report/Recommendation.cs             Verdict, Topic, Recommendation, VerdictText
  Report/Recommendations.cs            the rules
  Report/ReportWriter.cs               the text
  Report/Fmt.cs                        invariant number formatting; the ** marks of a because (ADR 0063)
  Report/RunReport.cs                  banner, warranty
  Report/CurveTexts.cs                 the Editare Curbe tab's sentences and the HISTORY text
  Report/ProofTexts.cs                 the Soft proof tab's sentences
  Report/GamutFitTexts.cs              the Culori pentru hartie tab's sentences, the HISTORY and description texts
  Runs/AnalysisRun.cs                  the chain, AnalysisResult, RefusedImageException
  Runs/CurveRun.cs                     the second run, after the analysis: CurveSet, CurveExport
  Runs/ProofRun.cs                     the third run, after the analysis: ProofSet (ADR 0066)
  Runs/GamutFitRun.cs                  the fourth run, after the analysis: GamutFitSet, GamutFitExport (ADR 0082)
  Logging/LogSetup.cs                  the diagnostic log's configuration, its first and last lines (ADR 0069)
  Logging/LogText.cs                   how a value is written in the log; a result record's figures
  Logging/RunClock.cs                  a run's start and done lines, with the time of each stage
  Parameters.cs, Thresholds.cs, ThirdParty.cs, Version.cs
  Language.cs                          the interface language: Tr.Pick, the --lang parser (ADR 0048)

AstroPhotoAnalyzer/                    Program.cs, Options.cs, AnalyzerCli.cs

AstroPhotoAnalyzer.Wpf/
  App.xaml(.cs), MainWindow.xaml(.cs), Theme/Dark.xaml
  Loggers.cs                           the names of the window's loggers in the diagnostic log
  Interop/DarkTitleBar.cs, Interop/ScreenArea.cs, Mvvm/ObservableObject.cs, Mvvm/RelayCommand.cs
  Presentation/AppIcon.cs, BarsCanvas.cs, CurveCanvas.cs, Converters.cs, RasterBitmaps.cs, VerdictPalette.cs,
               TExtension.cs (a XAML text in both languages), Emphasis.cs (a because's ** marks in bold, R-112),
               AccessibleTabControl.cs (the window's tabs, their content in the UI Automation tree, R-157),
               ZoomPanViewer.cs (the soft proof's viewer: wheel zoom, drag pan, double-click fit, R-163)
  ViewModels/MainViewModel.cs, ParameterFields.cs, ResultTabs.cs, Rows.cs, AboutViewModel.cs,
             MapViewModel.cs (the map window, R-130), PreviewWindowViewModel.cs (the preview window, R-131),
             IccPickerViewModel.cs (the profile window, R-132),
             WindowSettings.cs (the remembered settings file, R-156),
             CurvesTab.cs (the Editare Curbe tab and the curve window, R-146, R-147),
             SoftProofTab.cs (the Soft proof tab and its window, R-163),
             GamutFitTab.cs (the Culori pentru hartie tab, R-178)
  Views/ReportView, TonesView, ExportView, ResolutionView, BackgroundView, CurvesView, GamutFitView, SoftProofView,
        SoftProofControls (.xaml),
        ResultViews.xaml.cs, AboutWindow.xaml(.cs), BusyOverlay.xaml(.cs),
        MapWindow.xaml, PreviewWindow.xaml, CurveWindow.xaml, SoftProofWindow.xaml, OverlayWindows.xaml.cs,
        IccPickerWindow.xaml(.cs)

AstroPhotoAnalyzer.Tests/              see testing.md; Golden/*.txt and *.en.txt; testdata/ (manifest, large/);
                                       RunsInRomanian, LanguageTests, RomanianWords (language.md)
AstroPhotoAnalyzer.Wpf.Tests/          StaFixture, WindowSmokeTests, CultureTests, ParameterFieldsTests, ViewModelTests, OverlayWindowTests, IccPickerTests,
                                       ProfileBarTests (the bar's profile and the settings, R-155, R-156),
                                       RunsInRomanian, AppLanguageTests, WindowEnglishTests,
                                       EmphasisTests (the bold marks, R-112), ExportTabTests (the Export tab's facts, R-114)
```
