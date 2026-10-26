// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using FlatCheck.Tests;

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// The structure tab. The most important test in the file is the one that compares the
/// saved report with the console's output, character for character: that is exactly the
/// requirement, and if the window started composing the text by itself, it would break
/// silently.
/// </summary>
public class StructureTabTests
{
    static async Task<StructureTabViewModel> Run(SessionFixture f)
    {
        var vm = new StructureTabViewModel();
        await vm.RunAsync(f.Session, CancellationToken.None);
        return vm;
    }

    /// <summary>The master in masters/ (Siril 1.4) is analysed the same way, and the source
    /// says where it came from.</summary>
    [Fact]
    public async Task ItAnalysesAMasterKeptInMasters()
    {
        using var f = new SessionFixture().WithFlatMasterInMasters();

        var vm = await Run(f);

        Assert.True(vm.HasResult);
        Assert.Equal(Path.Combine(f.MastersDir, SirilSession.FlatMasterName), vm.Source);
        Assert.False(vm.HasSourceWarning);
    }

    /// <summary>
    /// Two masters in the folder: the newer is measured and that is said, with both paths,
    /// because they can come from two different sets of flats and the user must know which
    /// one they see.
    /// </summary>
    [Fact]
    public async Task WithTwoMastersTheNewerIsAnalysedAndAWarningNamesBoth()
    {
        using var f = new SessionFixture().WithFlatMaster().WithFlatMasterInMasters();
        string inProcess = Path.Combine(f.ProcessDir, SirilSession.FlatMasterName);
        string inMasters = Path.Combine(f.MastersDir, SirilSession.FlatMasterName);
        File.SetLastWriteTimeUtc(inMasters, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(inProcess, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var vm = await Run(f);

        Assert.Equal(inProcess, vm.Source);
        Assert.True(vm.HasSourceWarning);
        Assert.Contains(inProcess, vm.SourceWarning);
        Assert.Contains(inMasters, vm.SourceWarning);
        Assert.Contains("DOUA", vm.SourceWarning);

        vm.Reset();
        Assert.False(vm.HasSourceWarning);
    }

    [Fact]
    public async Task ItAnalysesTheConventionalFlatMaster()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.True(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.Equal(f.Session.FlatMasterPath, vm.Source);
        Assert.NotEmpty(vm.Verdict);
    }

    /// <summary>
    /// R-081 and R-090, literally: the saved file must hold EXACTLY the console's output. It
    /// is compared with the text produced by the same layer flatcheck.exe uses - so if
    /// someone reformatted something for the window only, the test fails.
    /// </summary>
    [Fact]
    public async Task TheSavedReportIsCharacterForCharacterTheConsoleOutput()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var session = f.Session;

        var vm = await Run(f);

        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        StructureReport.Write(
            StructureAnalyzer.Analyze(ImageLoader.LoadImage(session.FlatMasterPath)),
            session.FlatMasterPath, expected);
        RunReport.WriteMapSaved(session.ResidualPngPath, expected);

        Assert.Equal(expected.ToString(), File.ReadAllText(session.ResultTextPath));
        Assert.Equal(expected.ToString(), vm.Report);
    }

    /// <summary>The header is the first thing in the file: a pasted report says by itself
    /// what produced it.</summary>
    [Fact]
    public async Task TheSavedReportStartsWithTheVersionAndCopyright()
    {
        using var f = new SessionFixture().WithFlatMaster();

        await Run(f);

        var lines = File.ReadAllLines(f.Session.ResultTextPath);
        Assert.Equal(VersionInfo.Banner, lines[0]);
        Assert.Equal(VersionInfo.Copyright, lines[1]);
    }

    [Fact]
    public async Task TheReportAndTheMapLandInTheChosenFolder()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var session = f.Session;

        var vm = await Run(f);

        Assert.True(File.Exists(session.ResultTextPath));
        Assert.True(File.Exists(session.ResidualPngPath));
        Assert.Equal(session.ResultTextPath, vm.SavedTo);
        Assert.Empty(vm.SaveError);
    }

    /// <summary>
    /// On the "auto" chip, what is seen on the screen and what is saved on disk are the same
    /// bytes.
    ///
    /// The test asked for this UNCONDITIONALLY until the scale switch, and then it was
    /// right: the window always showed the automatic encoding. Now the default is +/-10 %,
    /// so the screen shows another encoding of the SAME residual - a viewing choice, not a
    /// second measurement. The file stays on auto (see TheSavedMapStaysOnTheAutoScale), so
    /// the guarantee is kept exactly where it still makes sense: when the user asks for the
    /// file's own scale.
    /// </summary>
    [Fact]
    public async Task OnTheAutoScaleTheDisplayedMapIsTheSameFileThatWasSaved()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);
        Cap(vm, null).IsSelected = true;

        Assert.Equal(File.ReadAllBytes(f.Session.ResidualPngPath), vm.Map);
    }

    // ------------------------------------------------------------------ the rows

    /// <summary>
    /// The five tests all appear, in the console's order, and each with its own verdict.
    /// </summary>
    [Fact]
    public async Task TheFiveDeviationTestsAreShownWithTheirOwnVerdicts()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(5, vm.Tests.Count);
        Assert.All(vm.Tests, t => Assert.True(t.HasVerdict));
        Assert.Contains("colturi", vm.Tests[0].Label);
        Assert.Contains("azimutala", vm.Tests[1].Label);
        Assert.Contains("reziduu", vm.Tests[2].Label);
        Assert.Contains("nclinare", vm.Tests[3].Label);
        Assert.Contains("pata maxima", vm.Tests[4].Label);
    }

    /// <summary>
    /// On a float master written by Siril (the values divided by 65535) the levels label
    /// said "median 0 ADU / max 0 ADU", as if the file were empty. The formatting now comes
    /// from the core and is the report's.
    /// </summary>
    [Fact]
    public async Task OnAFloatMasterTheLevelsAreNotRoundedToZero()
    {
        using var f = new SessionFixture().WithFlatMaster(
            FitsTestData.Scaled(FitsTestData.Radial(256, 256), 1.0 / 65535), bitpix: -32);

        var vm = await Run(f);

        Assert.DoesNotContain(" 0 /", vm.Levels);
        Assert.Contains("float normalizat", vm.Levels);
        Assert.Contains("0.45", vm.Levels);     // 30000 / 65535 = 0.4578 at the centre
    }

    /// <summary>
    /// The dust warning: it appears when there is a compact spot, with the core's text, and
    /// is absent on a clean master. It is not a verdict - the verdict stays the tests'.
    /// </summary>
    [Fact]
    public async Task ADustShadowShowsTheLocalSpotWarningFromTheCore()
    {
        // the fixture writes 128x128 -> a 64x64 raster, fine tiles of 2 px: a shadow of
        // 8 px radius on the sensor is 4 px on the raster, enough to fill a tile without
        // also filling the 7x7 neighbourhood whose median is subtracted
        const int S = 128;
        var radial = FitsTestData.Radial(S, S);
        Func<int, int, double> dust = (x, y) =>
        {
            double dx = x - 0.3 * S, dy = y - 0.3 * S;
            return radial(x, y) * (dx * dx + dy * dy < 8 * 8 ? 0.92 : 1.0);
        };
        using var f = new SessionFixture().WithFlatMaster(dust);

        var vm = await Run(f);

        Assert.StartsWith("ATENTIE", vm.LocalSpotWarning);
        Assert.Contains("biblioteca", vm.LocalSpotWarning);
        Assert.Contains(vm.LocalSpotWarning.Split('\n')[0], vm.Report);   // the same text as in the report
    }

    [Fact]
    public async Task ACleanMasterShowsNoLocalSpotWarning()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Empty(vm.LocalSpotWarning);
    }

    /// <summary>
    /// Every test row says what it measures. The azimuthal asymmetry and the tilt stood with
    /// a figure, a verdict and no description, because the notes were written inline here
    /// and nobody counted how many were missing. Now they all come from the core, and this
    /// test fails if a row without a note appears again.
    /// </summary>
    [Fact]
    public async Task EveryTestRowCarriesItsOwnDescription()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.All(vm.Tests, t => Assert.True(t.HasNote, "rand fara descriere: " + t.Label));
    }

    /// <summary>
    /// The descriptions come from the core, they are not rewritten in the window.
    /// </summary>
    [Fact]
    public async Task TheDescriptionsAreTheOnesTheCoreDefines()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(StructureReport.TestNote(nameof(StructureResult.AzimuthalMaxPct)), vm.Tests[1].Note);
        Assert.Equal(StructureReport.TestNote(nameof(StructureResult.ResidualPtpPct)), vm.Tests[2].Note);
        Assert.Equal(StructureReport.TestNote(nameof(StructureResult.TiltPct)), vm.Tests[3].Note);
        Assert.Equal(StructureReport.TestNote(nameof(StructureResult.MaxTilePct)), vm.Tests[4].Note);
        // the corner adds its four percentages on a second line
        Assert.StartsWith(StructureReport.TestNote(nameof(StructureResult.CornerSpreadPct)), vm.Tests[0].Note);
        Assert.Contains("stanga-sus", vm.Tests[0].Note);
    }

    /// <summary>The residual is what matters most, and it shows as such.</summary>
    [Fact]
    public async Task OnlyTheResidualRowIsMarkedAsTheKeyOne()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        var key = Assert.Single(vm.Tests.Where(t => t.IsKey));
        Assert.Contains("reziduu", key.Label);
    }

    /// <summary>
    /// The informative rows carry NO verdict. The illumination centre's offset was a test
    /// until 1.2.0 and is no longer - it is not scale-invariant, and above a threshold not
    /// even monotonic - so a coloured pill beside it would bring back exactly the mistake.
    /// </summary>
    [Fact]
    public async Task TheInformativeRowsCarryNoVerdict()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(3, vm.Informative.Count);
        Assert.All(vm.Informative, r => Assert.False(r.HasVerdict));
        Assert.Contains(vm.Informative, r => r.Label.Contains("vignetare"));
        Assert.Contains(vm.Informative, r => r.Label.Contains("offset"));
        Assert.Contains(vm.Informative, r => r.Label.Contains("pete locale"));
    }

    /// <summary>
    /// The scale comes from StructureReport.Scale(), so from the same thresholds as the
    /// report, and the band the verdict fell in is marked.
    /// </summary>
    [Fact]
    public async Task TheScaleHasFourBandsAndMarksTheCurrentOne()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(4, vm.Scale.Count);
        Assert.Equal(new[] { "PERFECT", "BUN", "ACCEPTABIL", "REFA" },
                     vm.Scale.Select(s => s.Band).ToArray());

        // The XAML's title counts the TESTS, not the bands, and it stayed "PATRU" for a
        // whole version after the tests had become five - seen only in a render.
        Assert.Equal(5, vm.Tests.Count);

        var current = Assert.Single(vm.Scale.Where(s => s.IsCurrent));
        Assert.Equal(vm.Verdict, current.Band);
    }

    /// <summary>The advice comes from the core, it is not rewritten in the window.</summary>
    [Fact]
    public async Task TheAdviceIsTheOneTheReportWouldPrint()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(StructureReport.Advice(vm.Verdict), vm.Advice);
    }

    // ------------------------------------------------------------------ failures

    /// <summary>
    /// A file that is not FITS: the analysis fails, but the tab stays whole and says why.
    /// </summary>
    [Fact]
    public async Task ACorruptMasterBecomesAnExplanationNotACrash()
    {
        using var f = new SessionFixture().WithProcess();
        File.WriteAllText(Path.Combine(f.ProcessDir, SirilSession.FlatMasterName), "nu e FITS");

        var vm = await Run(f);

        Assert.False(vm.HasResult);
        Assert.True(vm.HasError);
        Assert.NotEmpty(vm.ErrorText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task TheBusyFlagIsClearedWhetherItSucceedsOrFails()
    {
        using var ok = new SessionFixture().WithFlatMaster();
        using var bad = new SessionFixture().WithProcess();
        File.WriteAllText(Path.Combine(bad.ProcessDir, SirilSession.FlatMasterName), "x");

        Assert.False((await Run(ok)).IsBusy);
        Assert.False((await Run(bad)).IsBusy);
    }

    /// <summary>The reset clears everything, so that no figures from the previous folder
    /// remain.</summary>
    [Fact]
    public async Task ResetClearsEverything()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = await Run(f);

        vm.Reset();

        Assert.False(vm.HasResult);
        Assert.Empty(vm.Tests);
        Assert.Empty(vm.Informative);
        Assert.Empty(vm.Scale);
        Assert.Empty(vm.Verdict);
        Assert.Empty(vm.LocalSpotWarning);
        Assert.Empty(vm.Report);
        Assert.Null(vm.Map);
        Assert.Empty(vm.MapLegend);
        Assert.Empty(vm.SavedTo);
    }

    // ------------------------------------------ the vignetting against the lens profile

    static LoadedFrame RawFrame() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 20,
        FNumber = 2.8,
        Camera = "NIKON D3300",
        LensId = 139,
        LensMinFocalMm = 11,
        LensMaxFocalMm = 20,
        LensMaxAperture = 2.8,
    };

    /// <summary>
    /// The tab with hand-made profile sources and a fabricated raw in place of the file in flats/.
    /// </summary>
    static async Task<(StructureTabViewModel Vm, ProfileSources Sources)> RunWithProfiles(SessionFixture f, TempWorkspace ws, bool tokinaOnTwoFormats = false)
    {
        var sources = LensProfileTestData.Sources(ws, tokinaOnTwoFormats);
        var vm = new StructureTabViewModel(new ProfileCatalog(() => sources), path => RawReader.IsRawPath(path) ? RawFrame() : ImageLoader.LoadInfo(path));
        await vm.RunAsync(f.Session, CancellationToken.None);
        return (vm, sources);
    }

    /// <summary>
    /// With a raw beside the master the run is the console's "--raw": the vignetting is
    /// compared with every profile the raw's lens ID points at, the nearest one gives the
    /// depth verdict, the tab's verdict is the worse of shape and depth, and the saved
    /// report is - character for character - what the console prints for the same query.
    /// </summary>
    [Fact]
    public async Task WithARawBesideTheMasterTheVignettingIsComparedWithTheLensProfiles()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, sources) = await RunWithProfiles(f, ws);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.True(vm.HasProfileRows);
        Assert.Equal(2, vm.ProfileRows.Count);                        // the Nikon Adobe file and the lensfun entry
        Assert.Single(vm.ProfileRows, r => r.IsNearest);
        Assert.Contains("da verdictul", vm.ProfileRows.Single(r => r.IsNearest).Note);
        Assert.All(vm.ProfileRows, r => Assert.NotEmpty(r.Verdict));
        Assert.Empty(vm.ProfileGap);
        Assert.Contains("stopuri", vm.ProfileMaster);
        Assert.Contains("apropiat profil", vm.ProfileSummary);
        Assert.Equal(4, vm.ProfileScale.Count);                        // the depth scale, in stops, band marked
        Assert.Single(vm.ProfileScale, s => s.IsCurrent);
        Assert.Equal(vm.ProfileRows.Single(r => r.IsNearest).Verdict, vm.ProfileScale.Single(s => s.IsCurrent).Band);
        Assert.Contains("Nu e un prag nou", vm.ProfileScaleNote);
        Assert.All(vm.ProfileRows, r => Assert.NotEmpty(r.Note));
        Assert.Contains("RADIALA", vm.ProfileNote);
        Assert.Contains("forma", vm.VerdictDetail);
        Assert.Contains("adancimea", vm.VerdictDetail);
        Assert.Equal(5, vm.Tests.Count);                               // the five stay five
        Assert.Contains(StructureReport.ProfilesHeading, vm.Report);
        Assert.Contains("VERDICT: " + vm.Verdict, vm.Report);

        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        StructureRun.Execute(f.Session.FlatMasterPath, expected,
            new StructureQuery { Sources = sources, RawPath = f.Session.ReferenceRawPath, LoadInfo = _ => RawFrame() });
        RunReport.WriteMapSaved(f.Session.ResidualPngPath, expected);
        Assert.Equal(expected.ToString(), File.ReadAllText(f.Session.ResultTextPath));
    }

    /// <summary>
    /// A lensfun lens calibrated on two formats is two rows in the table, with the same name
    /// and the calibration format beside the mount, so the user can tell which one the master
    /// agrees with; an Adobe row shows the mount alone.
    /// </summary>
    [Fact]
    public async Task ALensfunLensCalibratedOnTwoFormatsIsTwoRowsInTheTable()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, _) = await RunWithProfiles(f, ws, tokinaOnTwoFormats: true);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Equal(3, vm.ProfileRows.Count);
        var lensfun = vm.ProfileRows.Where(r => r.Source == "lensfun").ToList();
        Assert.Equal(2, lensfun.Count);
        Assert.Equal(lensfun[0].Lens, lensfun[1].Lens);
        Assert.Contains(lensfun, r => r.Where == "Canon EF / Nikon F AF, calibrat pe APS-C Canon 1.61");
        Assert.Contains(lensfun, r => r.Where == "Canon EF / Nikon F AF, calibrat pe full frame 1");
        Assert.NotEqual(lensfun[0].Depth, lensfun[1].Depth);
        Assert.Equal("NIKON CORPORATION", vm.ProfileRows.Single(r => r.Source == "Adobe").Where);
        Assert.Contains("calibrat pe", vm.Report);
    }

    /// <summary>
    /// The fixture master is deeper than the model: shape PERFECT, depth worse, and the depth
    /// carries the verdict and the advice.
    /// </summary>
    [Fact]
    public async Task WhenTheDepthIsWorseThanTheShapeItCarriesTheVerdictAndTheAdvice()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, _) = await RunWithProfiles(f, ws);

        string shape = vm.Tests.Select(t => t.Verdict).OrderByDescending(Stats.Severity).First();
        string depth = vm.ProfileRows.Single(r => r.IsNearest).Verdict;
        Assert.True(Stats.Severity(depth) > Stats.Severity(shape), $"depth {depth}, shape {shape}");
        Assert.Equal(depth, vm.Verdict);
        Assert.Contains("MAI", vm.Advice);                             // the depth's advice, not the shape's
        Assert.NotEqual(StructureReport.Advice(shape), vm.Advice);
    }

    /// <summary>
    /// Without a raw in flats/ nothing knows the lens: the run is the plain console one (the
    /// existing character-for-character test covers that), and the card says why, in the
    /// core's words.
    /// </summary>
    [Fact]
    public async Task WithoutARawTheCardExplainsWhyItCannotCompare()
    {
        using var f = new SessionFixture().WithFlatMaster();
        using var ws = new TempWorkspace();
        var (vm, _) = await RunWithProfiles(f, ws);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.False(vm.HasProfileRows);
        Assert.StartsWith("Nu se poate compara: ", vm.ProfileGap);
        Assert.Contains("niciun raw langa master", vm.ProfileGap);
        Assert.Empty(vm.VerdictDetail);
        Assert.Equal(StructureReport.Advice(vm.Verdict), vm.Advice);
        Assert.DoesNotContain(StructureReport.ProfilesHeading, vm.Report);   // the report stays the console's plain one
    }

    /// <summary>
    /// A raw in flats/ that carries no lens (a Canon on a telescope: focal length 0, no ID) is
    /// named on the card with what it lacks, never as a missing raw (R-106).
    /// </summary>
    [Fact]
    public async Task ARawThatCarriesNoLensIsNamedOnTheCard()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("IMG_7290.CR2");
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var noLens = new LoadedFrame { Header = new Dictionary<string, string>(), Camera = "Canon EOS 40D" };
        var vm = new StructureTabViewModel(new ProfileCatalog(() => sources), path => RawReader.IsRawPath(path) ? noLens : ImageLoader.LoadInfo(path));

        await vm.RunAsync(f.Session, CancellationToken.None);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.False(vm.HasProfileRows);
        Assert.StartsWith("Nu se poate compara: IMG_7290.CR2 nu spune nimic despre obiectiv", vm.ProfileGap);
        Assert.Contains("telescop", vm.ProfileGap);
        Assert.DoesNotContain("niciun raw langa master", vm.ProfileGap);
    }

    [Fact]
    public async Task ResetClearsTheProfileComparisonToo()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, _) = await RunWithProfiles(f, ws);
        Assert.True(vm.HasProfileRows);

        vm.Reset();

        Assert.False(vm.HasProfileRows);
        Assert.Empty(vm.ProfileRows);
        Assert.Empty(vm.ProfileGap);
        Assert.Empty(vm.ProfileMaster);
        Assert.Empty(vm.VerdictDetail);
    }

    // -------------------------------------------------------- the map scale

    static MapScaleOption Cap(StructureTabViewModel vm, double? capPct)
        => vm.MapScale.Options.Single(o => o.Label == RunReport.MapScaleLabel(capPct));

    /// <summary>
    /// The default is the band a normal flat is expected to live in, not "auto": two
    /// sessions must be comparable at a glance, without pressing anything.
    /// </summary>
    [Fact]
    public async Task TheDefaultScaleIsTheUsableBand()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(RunReport.MapScaleLabel(Th.ResidUsable), vm.MapScale.Selected.Label);
        Assert.Contains($"{Th.ResidUsable:F1} %", vm.MapLegend);
    }

    /// <summary>
    /// The file on disk does NOT follow the ticked chip. The console writes the automatic
    /// encoding, and the two applications must write the same file for the same master -
    /// the chips are a viewing aid, not an output setting.
    /// </summary>
    [Fact]
    public async Task TheSavedMapStaysOnTheAutoScale()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var session = f.Session;

        var vm = await Run(f);
        Cap(vm, Th.ResidPerfect).IsSelected = true;      // even after the scale was changed

        var expected = PngWriter.EncodeResidualMap(
            StructureAnalyzer.Analyze(ImageLoader.LoadImage(session.FlatMasterPath)).Residual);

        Assert.Equal(expected, File.ReadAllBytes(session.ResidualPngPath));
    }

    [Fact]
    public async Task ChoosingAScaleChangesTheMapAndTheLegend()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = await Run(f);

        Cap(vm, null).IsSelected = true;
        byte[] auto = vm.Map;
        string autoLegend = vm.MapLegend;

        Cap(vm, Th.ResidPerfect).IsSelected = true;

        Assert.NotEqual(auto, vm.Map);
        Assert.NotEqual(autoLegend, vm.MapLegend);
        Assert.Contains($"-{Th.ResidPerfect:F1} %", vm.MapLegend);
    }

    /// <summary>
    /// On "auto" the legend gives the ends ACTUALLY computed - precisely those could not be
    /// seen on the screen before, and they make full red mean something else from one image
    /// to another.
    /// </summary>
    [Fact]
    public async Task TheLegendOnAutoShowsTheRealRange()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = await Run(f);
        var residual = StructureAnalyzer.Analyze(
            ImageLoader.LoadImage(f.Session.FlatMasterPath)).Residual;

        Cap(vm, null).IsSelected = true;

        Assert.Equal(RunReport.MapLegend(PngWriter.AutoRange(residual)), vm.MapLegend);
    }

    /// <summary>
    /// The scale choice is a viewing preference, not a result: it survives the change of
    /// folder. The map and the raster do not.
    /// </summary>
    [Fact]
    public async Task TheSelectionSurvivesResetButTheMapDoesNot()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = await Run(f);
        Cap(vm, Th.ResidCheck).IsSelected = true;

        vm.Reset();

        Assert.Equal(RunReport.MapScaleLabel(Th.ResidCheck), vm.MapScale.Selected.Label);
        Assert.Null(vm.Map);
        Assert.Empty(vm.MapLegend);
    }

    /// <summary>
    /// The encoding is deterministic, and the second tick takes it from the cache.
    /// </summary>
    [Fact]
    public async Task ReselectingAScaleReturnsTheSameBytes()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = await Run(f);

        byte[] first = vm.Map;
        Cap(vm, Th.ResidPerfect).IsSelected = true;
        Cap(vm, Th.ResidUsable).IsSelected = true;

        Assert.Equal(first, vm.Map);
    }

    /// <summary>A single chip ticked, and that shows in the view-model, not only in the group
    /// of buttons.</summary>
    [Fact]
    public async Task ExactlyOneScaleIsSelectedAtATime()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = await Run(f);

        Cap(vm, null).IsSelected = true;

        Assert.Single(vm.MapScale.Options.Where(o => o.IsSelected));
        Assert.Equal("auto", vm.MapScale.Selected.Label);
    }

    // -------------------------------------------------------- the map guide

    /// <summary>
    /// The guide is documentation, not a result: it exists before any run too, and does not
    /// change after. If it depended on a result, it would be a diagnosis - and that is
    /// exactly what it is not, because the tool does not classify the map's shape.
    /// </summary>
    [Fact]
    public async Task TheGuideIsAvailableBeforeAndAfterARun()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = new StructureTabViewModel();

        Assert.NotEmpty(vm.MapGuide.Intro);
        Assert.NotEmpty(vm.MapGuide.Shapes);

        string intro = vm.MapGuide.Intro;
        await vm.RunAsync(f.Session, CancellationToken.None);

        Assert.Equal(intro, vm.MapGuide.Intro);
        Assert.NotEmpty(vm.MapGuide.Shapes);
    }

    /// <summary>Collapsed by default, and the state survives the change of folder like the
    /// scale.</summary>
    [Fact]
    public async Task TheGuideStartsCollapsedAndKeepsItsStateAcrossReset()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = await Run(f);

        Assert.False(vm.MapGuide.IsExpanded);

        vm.MapGuide.IsExpanded = true;
        vm.Reset();

        Assert.True(vm.MapGuide.IsExpanded);
    }
}
