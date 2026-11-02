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

        // The table's column header counts the TESTS, not the bands; the title did until
        // R-126, and it stayed "PATRU" for a whole version after the tests had become five -
        // seen only in a render.
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
        Assert.Contains("profilul cel mai apropiat", vm.ProfileSummary);
        Assert.Equal(4, vm.Scale.Count);                               // the depth, in stops, in the one scale (R-126)
        Assert.True(vm.ScaleHasDepth);
        Assert.Equal(vm.ProfileRows.Single(r => r.IsNearest).Verdict, Assert.Single(vm.Scale, s => s.IsDepthCurrent).Band);
        Assert.Contains("Nu e un prag nou", vm.ScaleDepthNote);
        Assert.All(vm.ProfileRows, r => Assert.NotEmpty(r.Note));
        Assert.Contains("simetrica in jurul centrului", vm.ProfileNote);
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
    /// R-115: a lensfun/ folder the user may not list does not take the Structure run down with
    /// it: the five tests and the profile card still come, with the Adobe rows the raw's lens ID
    /// finds and no red paragraph. Before, the shared catalog's load threw from the folder's
    /// enumeration and the tab showed "The analysis failed" whenever flats/ held a raw
    /// (2026-10-30).
    /// </summary>
    [Fact]
    public async Task R115_AnUnlistableLensfunFolderDoesNotFailTheStructureRun()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        string dir = LensProfileTestData.ProfilesDir(ws);
        string lensfun = Path.Combine(dir, "lensfun");
        using var deny = UnlistableFolder.Deny(lensfun);
        var vm = new StructureTabViewModel(new ProfileCatalog(() => ProfileLocator.Locate(new[] { dir }, lensfun)),
                                           path => RawReader.IsRawPath(path) ? RawFrame() : ImageLoader.LoadInfo(path));

        await vm.RunAsync(f.Session, CancellationToken.None);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.False(vm.HasError);
        Assert.True(vm.HasProfileRows);
        Assert.Equal("", vm.AdobeMissing);
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

    // ------------------------------------------ without Adobe's profiles (R-113)

    /// <summary>The tab on <paramref name="sources"/>, with the fabricated raw in flats/.</summary>
    static async Task<StructureTabViewModel> RunOn(SessionFixture f, ProfileSources sources)
    {
        var vm = new StructureTabViewModel(new ProfileCatalog(() => sources), path => RawReader.IsRawPath(path) ? RawFrame() : ImageLoader.LoadInfo(path));
        await vm.RunAsync(f.Session, CancellationToken.None);
        return vm;
    }

    /// <summary>
    /// On a computer without Adobe software the comparison runs on lensfun alone; the card then
    /// says the Adobe profiles were not found, what to install, the link and the folders
    /// searched, all in the core's words, while the saved report stays the console's.
    /// </summary>
    [Fact]
    public async Task R113_WithoutAdobeFilesTheCardSaysSoAndHowToGetThem()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, withAdobe: false);

        var vm = await RunOn(f, sources);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Equal(0, sources.AdobeFiles);
        Assert.StartsWith("Profilele de obiectiv Adobe nu au fost gasite", vm.AdobeMissing);
        Assert.Equal(StructureReport.AdobeMissing(sources), vm.AdobeMissing);
        Assert.Equal(StructureReport.AdobeInstall(), vm.AdobeInstall);
        Assert.Equal(StructureReport.AdobeDngConverterUrl, vm.AdobeLink);
        Assert.Equal(StructureReport.AdobeSearched(sources), vm.AdobeSearched);
        Assert.Contains(sources.AdobeDirectories[0], vm.AdobeSearched);
        Assert.True(vm.HasProfileRows);                                // lensfun alone still compares
        Assert.All(vm.ProfileRows, r => Assert.Equal("lensfun", r.Source));

        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        StructureRun.Execute(f.Session.FlatMasterPath, expected,
            new StructureQuery { Sources = sources, RawPath = f.Session.ReferenceRawPath, LoadInfo = _ => RawFrame() });
        RunReport.WriteMapSaved(f.Session.ResidualPngPath, expected);
        Assert.Equal(expected.ToString(), File.ReadAllText(f.Session.ResultTextPath));
        Assert.DoesNotContain("DNG Converter", vm.Report);             // the window's card only
        Assert.DoesNotContain(vm.AdobeMissing, vm.Report);
        Assert.DoesNotContain(vm.AdobeSearched, vm.Report);
    }

    /// <summary>With one Adobe file found there is nothing to say about Adobe.</summary>
    [Fact]
    public async Task R113_WithAdobeFilesTheCardSaysNothingAboutThem()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, sources) = await RunWithProfiles(f, ws);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.True(sources.AdobeFiles > 0);
        Assert.Empty(vm.AdobeMissing);
        Assert.Empty(vm.AdobeSearched);
    }

    /// <summary>
    /// Without a raw in flats/ the tab's run searches no profile and does not wait for the
    /// catalog (in the window the Sintetic tab's prefill loads it anyway), so it says nothing
    /// about Adobe; the card says why it cannot compare.
    /// </summary>
    [Fact]
    public async Task R113_WithoutARawTheRunSearchesNothingSoNothingIsSaidAboutAdobe()
    {
        using var f = new SessionFixture().WithFlatMaster();
        using var ws = new TempWorkspace();
        int searches = 0;
        var vm = new StructureTabViewModel(new ProfileCatalog(() => { searches++; return LensProfileTestData.Sources(ws, withAdobe: false); }), ImageLoader.LoadInfo);

        await vm.RunAsync(f.Session, CancellationToken.None);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Equal(0, searches);
        Assert.Empty(vm.AdobeMissing);
        Assert.Empty(vm.AdobeSearched);
        Assert.Contains("niciun raw langa master", vm.ProfileGap);
    }

    /// <summary>
    /// In the window the catalog is already loaded when the tab runs without a raw (the Sintetic
    /// tab's prefill loads it at every opening): the card still says nothing about Adobe, since
    /// the owner chose no message without a raw (ADR 0073).
    /// </summary>
    [Fact]
    public async Task R113_WithoutARawTheCardSaysNothingEvenWithTheCatalogLoaded()
    {
        using var f = new SessionFixture().WithFlatMaster();
        using var ws = new TempWorkspace();
        var catalog = new ProfileCatalog(() => LensProfileTestData.Sources(ws, withAdobe: false));
        Assert.Equal(0, (await catalog.EnsureAsync()).AdobeFiles);   // as the Sintetic tab's prefill leaves it
        var vm = new StructureTabViewModel(catalog, ImageLoader.LoadInfo);

        await vm.RunAsync(f.Session, CancellationToken.None);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Empty(vm.AdobeMissing);
        Assert.Empty(vm.AdobeSearched);
        Assert.Contains("niciun raw langa master", vm.ProfileGap);
    }

    /// <summary>
    /// With no profile at all, the card says Adobe's were not found and, in its gap, that there
    /// is no profile on this machine; the red paragraph does not claim lensfun is searched, since
    /// the gap says its folder is missing too. Where the block sits is the render test's.
    /// </summary>
    [Fact]
    public async Task R113_WithNoProfileAtAllTheCardSaysSoAndTheGapToo()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var none = ProfileLocator.Locate(new[] { Path.Combine(ws.Dir, "no-adobe") }, Path.Combine(ws.Dir, "no-lensfun"));

        var vm = await RunOn(f, none);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Equal(StructureReport.AdobeMissing(none), vm.AdobeMissing);
        Assert.NotEmpty(vm.AdobeMissing);
        Assert.DoesNotContain("lensfun", vm.AdobeMissing);
        Assert.False(vm.HasProfileRows);
        Assert.Contains("niciun profil de obiectiv pe masina", vm.ProfileGap);
    }

    [Fact]
    public async Task R113_ResetClearsTheAdobeMessage()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var vm = await RunOn(f, LensProfileTestData.Sources(ws, withAdobe: false));
        Assert.NotEmpty(vm.AdobeMissing);

        vm.Reset();

        Assert.Empty(vm.AdobeMissing);
        Assert.Empty(vm.AdobeSearched);
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

    // ------------------------------------------------- the "no filters" box (R-118)

    /// <summary>The console's plain report for the fixture master, banner and map line
    /// included: what the saved file must hold whenever nothing is compared.</summary>
    static string PlainReport(SessionFixture f)
    {
        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        StructureRun.Execute(f.Session.FlatMasterPath, expected);
        RunReport.WriteMapSaved(f.Session.ResidualPngPath, expected);
        return expected.ToString();
    }

    /// <summary>The console's report with --raw for the fixture master and the fabricated
    /// raw: what the saved file must hold whenever the comparison is on.</summary>
    static string ComparedReport(SessionFixture f, ProfileSources sources)
    {
        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        StructureRun.Execute(f.Session.FlatMasterPath, expected,
            new StructureQuery { Sources = sources, RawPath = f.Session.ReferenceRawPath, LoadInfo = _ => RawFrame() });
        RunReport.WriteMapSaved(f.Session.ResidualPngPath, expected);
        return expected.ToString();
    }

    /// <summary>The worse of the five tests' verdicts, the shape's verdict.</summary>
    static string ShapeVerdict(StructureTabViewModel vm)
        => vm.Tests.Select(t => t.Verdict).OrderByDescending(Stats.Severity).First();

    /// <summary>
    /// R-118: the box is on by default; off before the run, the run is the plain console one
    /// even with a raw and a catalog - no profile row, no gap, no Adobe paragraph, the five
    /// tests' verdict, the master's own depth and the paragraph saying why - and the saved
    /// file is the plain report, byte for byte.
    /// </summary>
    [Fact]
    public async Task R118_UncheckedTheRunIsThePlainOneWithTheMastersDepthAndTheNote()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, withAdobe: false);
        var vm = new StructureTabViewModel(new ProfileCatalog(() => sources), path => RawReader.IsRawPath(path) ? RawFrame() : ImageLoader.LoadInfo(path));
        Assert.True(vm.NoFilters);

        vm.NoFilters = false;
        await vm.RunAsync(f.Session, CancellationToken.None);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.False(vm.HasProfileRows);
        Assert.Empty(vm.ProfileGap);
        Assert.Equal("", vm.AdobeMissing);
        Assert.Empty(vm.VerdictDetail);
        Assert.Equal(ShapeVerdict(vm), vm.Verdict);
        Assert.Equal(StructureReport.Advice(vm.Verdict), vm.Advice);
        Assert.Contains("stopuri", vm.ProfileMaster);
        Assert.NotEmpty(vm.FilteredNote);
        Assert.Contains("filtru", vm.FilteredNote);
        Assert.DoesNotContain(StructureReport.ProfilesHeading, vm.Report);
        Assert.Equal(PlainReport(f), File.ReadAllText(f.Session.ResultTextPath));
    }

    /// <summary>
    /// R-118: unchecking the box with a compared result on screen redoes the comparison and
    /// the verdict - every row goes, the five tests' verdict stands, the file is saved again as
    /// the plain report - and the master is not reloaded: the map stays the same bytes.
    /// </summary>
    [Fact]
    public async Task R118_UncheckingItWithAResultOnScreenDropsTheRowsAndTheDepthFromTheVerdict()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, _) = await RunWithProfiles(f, ws);
        Assert.True(vm.HasProfileRows);
        string shape = ShapeVerdict(vm);
        Assert.NotEqual(shape, vm.Verdict);                            // the depth carried it
        byte[] map = vm.Map;

        vm.NoFilters = false;
        await vm.Pending;

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.False(vm.HasProfileRows);
        Assert.Empty(vm.ProfileGap);
        Assert.Equal(shape, vm.Verdict);
        Assert.Empty(vm.VerdictDetail);
        Assert.Contains("stopuri", vm.ProfileMaster);
        Assert.NotEmpty(vm.FilteredNote);
        Assert.Same(map, vm.Map);
        Assert.Equal(PlainReport(f), File.ReadAllText(f.Session.ResultTextPath));
        Assert.Equal(PlainReport(f), vm.Report);
    }

    /// <summary>
    /// R-118: checking the box with a plain result on screen builds the table and counts the
    /// depth in the verdict, and the file is saved again as the console's --raw report.
    /// </summary>
    [Fact]
    public async Task R118_CheckingItWithAResultOnScreenComparesAndCountsTheDepth()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var vm = new StructureTabViewModel(new ProfileCatalog(() => sources), path => RawReader.IsRawPath(path) ? RawFrame() : ImageLoader.LoadInfo(path));
        vm.NoFilters = false;
        await vm.RunAsync(f.Session, CancellationToken.None);
        Assert.False(vm.HasProfileRows);
        string shape = vm.Verdict;
        byte[] map = vm.Map;

        vm.NoFilters = true;
        await vm.Pending;

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Equal(2, vm.ProfileRows.Count);
        Assert.Equal(vm.ProfileRows.Single(r => r.IsNearest).Verdict, vm.Verdict);
        Assert.NotEqual(shape, vm.Verdict);
        Assert.Contains("adancimea", vm.VerdictDetail);
        Assert.Empty(vm.FilteredNote);
        Assert.Same(map, vm.Map);
        Assert.Equal(ComparedReport(f, sources), File.ReadAllText(f.Session.ResultTextPath));
    }

    /// <summary>R-118: the box is a setting of the folder, not a result: Reset leaves it as
    /// it was, and the next folder's file sets it.</summary>
    [Fact]
    public async Task R118_ResetLeavesTheBoxAndClearsTheNote()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = new StructureTabViewModel();
        vm.NoFilters = false;
        await vm.RunAsync(f.Session, CancellationToken.None);
        Assert.NotEmpty(vm.FilteredNote);

        vm.Reset();

        Assert.False(vm.NoFilters);
        Assert.Empty(vm.FilteredNote);
        Assert.Empty(vm.ProfileMaster);
    }

    /// <summary>R-118: a flip shows the waiting screen at once, before anything is awaited, so
    /// the box (bound to the busy flag) is locked for the whole recompare.</summary>
    [Fact]
    public async Task R118_AFlipShowsTheBusyScreenAtOnce()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = new StructureTabViewModel();
        await vm.RunAsync(f.Session, CancellationToken.None);
        Assert.False(vm.IsBusy);

        vm.NoFilters = false;

        Assert.True(vm.IsBusy);
        await vm.Pending;
        Assert.False(vm.IsBusy);
        Assert.NotEmpty(vm.FilteredNote);
    }

    /// <summary>
    /// R-118 (the review of 2026-10-30, finding 1): a folder closed while a recompare runs -
    /// Reset, as another folder's opening does - leaves nothing of it on screen and rewrites no
    /// file: the result returns to a tab whose session is gone and is dropped.
    /// </summary>
    [Fact]
    public async Task R118_AFolderClosedDuringARecompareLeavesNothingOnScreen()
    {
        using var f = new SessionFixture().WithFlatMaster();
        using var hold = new ManualResetEventSlim(false);
        bool block = false;
        var vm = new StructureTabViewModel(null, path => { if (block) hold.Wait(); return ImageLoader.LoadInfo(path); });
        await vm.RunAsync(f.Session, CancellationToken.None);
        string report = File.ReadAllText(f.Session.ResultTextPath);
        DateTime written = File.GetLastWriteTimeUtc(f.Session.ResultTextPath);

        block = true;
        vm.NoFilters = false;                 // the recompare blocks in the worker on the master's header
        Task pending = vm.Pending;
        vm.Reset();                           // the folder is closed meanwhile
        hold.Set();
        await pending;

        Assert.False(vm.HasResult);
        Assert.Empty(vm.Verdict);
        Assert.Empty(vm.Report);
        Assert.Empty(vm.FilteredNote);
        Assert.Equal(report, File.ReadAllText(f.Session.ResultTextPath));
        Assert.Equal(written, File.GetLastWriteTimeUtc(f.Session.ResultTextPath));
    }

    // ------------------------------------------------ the lens picker on a list by range (R-121)

    /// <summary>The tab on the catalog with the two 135 f/2 lenses and the range raw in place
    /// of the file in flats/, run once.</summary>
    static async Task<(StructureTabViewModel Vm, ProfileSources Sources)> RunWithRangePair(SessionFixture f, TempWorkspace ws, SessionSettings? settings = null, string camera = "NIKON D3300")
    {
        var sources = LensProfileTestData.Sources(ws, rangePair: true);
        var vm = new StructureTabViewModel(new ProfileCatalog(() => sources), path => RawReader.IsRawPath(path) ? LensProfileTestData.Range135Raw(camera) : ImageLoader.LoadInfo(path));
        await vm.RunAsync(f.Session, CancellationToken.None, settings);
        return (vm, sources);
    }

    /// <summary>The console's report for the fixture master, the range raw and a chosen lens
    /// (null: none chosen).</summary>
    static string RangeReport(SessionFixture f, ProfileSources sources, string? lensKey)
    {
        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        StructureRun.Execute(f.Session.FlatMasterPath, expected,
            new StructureQuery { Sources = sources, RawPath = f.Session.ReferenceRawPath, LoadInfo = _ => LensProfileTestData.Range135Raw(), LensKey = lensKey });
        RunReport.WriteMapSaved(f.Session.ResidualPngPath, expected);
        return expected.ToString();
    }

    /// <summary>
    /// R-121: a raw that gives only a range, with two lenses of that range, offers the picker:
    /// the prompt, the none entry selected, one entry per candidate in the rows' order; both
    /// rows are on the card and the saved report is the console's for the same raw.
    /// </summary>
    [Fact]
    public async Task R121_ARawThatGivesOnlyARangeOffersThePickerWithEveryCandidate()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, sources) = await RunWithRangePair(f, ws);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Equal(2, vm.ProfileRows.Count);
        Assert.True(vm.OffersLensChoice);
        Assert.NotEmpty(vm.LensChoicePrompt);
        Assert.Equal(3, vm.LensOptions.Count);
        Assert.Null(vm.LensOptions[0].Key);
        Assert.NotEmpty(vm.LensOptions[0].Label);
        Assert.Equal(LensProfileTestData.KeyOf(sources, LensProfileTestData.Nikkor135Dc), vm.LensOptions[1].Key);
        Assert.Equal(LensProfileTestData.KeyOf(sources, LensProfileTestData.Samyang135), vm.LensOptions[2].Key);
        Assert.Contains(LensProfileTestData.Samyang135, vm.LensOptions[2].Label);
        Assert.Same(vm.LensOptions[0], vm.SelectedLens);
        Assert.Equal(RangeReport(f, sources, null), File.ReadAllText(f.Session.ResultTextPath));
    }

    /// <summary>R-121: a list the lens ID decided offers no picker.</summary>
    [Fact]
    public async Task R121_AListFoundByTheLensIdOffersNoPicker()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, _) = await RunWithProfiles(f, ws);

        Assert.True(vm.HasProfileRows);
        Assert.False(vm.OffersLensChoice);
        Assert.Empty(vm.LensOptions);
        Assert.Empty(vm.LensChoicePrompt);
        Assert.Null(vm.SelectedLens);
    }

    /// <summary>
    /// R-121: picking a lens redoes the comparison on the kept result - that lens's row alone,
    /// the comparison's verdict its profile's, the report saved as the console's for the same
    /// query, the map the same bytes - and the picker keeps every candidate; picking the none
    /// entry again brings both rows back.
    /// </summary>
    [Fact]
    public async Task R121_PickingALensNarrowsTheComparisonAndSavesTheReport()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, sources) = await RunWithRangePair(f, ws);
        string key = LensProfileTestData.KeyOf(sources, LensProfileTestData.Samyang135);
        byte[] map = vm.Map;

        vm.SelectedLens = vm.LensOptions.Single(o => o.Key == key);
        await vm.Pending;

        Assert.True(vm.HasResult, vm.ErrorText);
        LensProfileRow row = Assert.Single(vm.ProfileRows);
        Assert.Equal(LensProfileTestData.Samyang135, row.Lens);
        Assert.True(row.IsNearest);
        Assert.Equal(3, vm.LensOptions.Count);
        Assert.Equal(key, vm.SelectedLens.Key);
        Assert.Contains(LensProfileTestData.Nikkor135Dc, vm.ProfileSummary);   // the lens left out, named
        Assert.Same(map, vm.Map);
        Assert.Equal(RangeReport(f, sources, key), vm.Report);
        Assert.Equal(RangeReport(f, sources, key), File.ReadAllText(f.Session.ResultTextPath));

        vm.SelectedLens = vm.LensOptions[0];
        await vm.Pending;

        Assert.Equal(2, vm.ProfileRows.Count);
        Assert.Same(vm.LensOptions[0], vm.SelectedLens);
        Assert.Equal(RangeReport(f, sources, null), File.ReadAllText(f.Session.ResultTextPath));
    }

    /// <summary>
    /// R-121: the folder's remembered key is applied at the run, before anything is on screen;
    /// a key that matches no candidate is ignored - every row, the none entry selected.
    /// </summary>
    [Fact]
    public async Task R121_ARememberedLensIsAppliedAtTheRunAndAKeyAmongNoCandidateIsIgnored()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, rangePair: true);
        string key = LensProfileTestData.KeyOf(sources, LensProfileTestData.Samyang135);
        var remembered = SessionSettings.None;
        remembered.Lens = key;

        var (vm, _) = await RunWithRangePair(f, ws, remembered);

        Assert.True(vm.HasResult, vm.ErrorText);
        LensProfileRow row = Assert.Single(vm.ProfileRows);
        Assert.Equal(LensProfileTestData.Samyang135, row.Lens);
        Assert.Equal(key, vm.SelectedLens.Key);

        var stale = SessionSettings.None;
        stale.Lens = "Adobe|NIKON CORPORATION|Nobody 135mm f/2|NIKON D3";
        var (again, _) = await RunWithRangePair(f, ws, stale);

        Assert.Equal(2, again.ProfileRows.Count);
        Assert.Same(again.LensOptions[0], again.SelectedLens);
    }

    /// <summary>R-121: Reset empties the picker with the rest of the card.</summary>
    [Fact]
    public async Task R121_ResetEmptiesThePicker()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, _) = await RunWithRangePair(f, ws);
        Assert.True(vm.OffersLensChoice);

        vm.Reset();

        Assert.False(vm.OffersLensChoice);
        Assert.Empty(vm.LensOptions);
        Assert.Empty(vm.LensChoicePrompt);
        Assert.Null(vm.SelectedLens);
    }

    /// <summary>
    /// R-121: a chosen lens whose profile makes no row (a lensfun lens on a body the lensfun
    /// table does not know: no crop) leaves the card on the gap, but the picker stays, on that
    /// lens, so the choice can be undone from the screen; picking the none entry brings the
    /// Adobe row back.
    /// </summary>
    [Fact]
    public async Task R121_AChosenLensThatComparesNothingKeepsThePickerSoTheChoiceCanBeUndone()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, sources) = await RunWithRangePair(f, ws, camera: "NIKON D5");
        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.True(vm.OffersLensChoice);
        LensProfileRow adobe = Assert.Single(vm.ProfileRows);           // the lensfun lens has no crop on this body
        Assert.Equal(LensProfileTestData.Nikkor135Dc, adobe.Lens);
        string key = LensProfileTestData.KeyOf(sources, LensProfileTestData.Samyang135);

        vm.SelectedLens = vm.LensOptions.Single(o => o.Key == key);
        await vm.Pending;

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.False(vm.HasProfileRows);
        Assert.NotEmpty(vm.ProfileGap);
        Assert.True(vm.OffersLensChoice);
        Assert.Equal(key, vm.SelectedLens.Key);

        vm.SelectedLens = vm.LensOptions[0];
        await vm.Pending;

        Assert.Equal(LensProfileTestData.Nikkor135Dc, Assert.Single(vm.ProfileRows).Lens);
        Assert.Empty(vm.ProfileGap);
    }

    /// <summary>
    /// R-121: the null the ComboBox pushes when the list is replaced is not a pick: nothing is
    /// written, nothing recompared, and the selection stays.
    /// </summary>
    [Fact]
    public async Task R121_ANullFromTheBindingIsNotAPick()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var settings = SessionSettings.Read(f.Session.SettingsPath);
        var (vm, _) = await RunWithRangePair(f, ws, settings);
        LensOption before = vm.SelectedLens;
        string report = File.ReadAllText(f.Session.ResultTextPath);
        DateTime written = File.GetLastWriteTimeUtc(f.Session.ResultTextPath);

        vm.SelectedLens = null;
        await vm.Pending;

        Assert.Same(before, vm.SelectedLens);
        Assert.False(File.Exists(settings.FilePath));
        Assert.Equal(report, File.ReadAllText(f.Session.ResultTextPath));
        Assert.Equal(written, File.GetLastWriteTimeUtc(f.Session.ResultTextPath));
    }

    /// <summary>
    /// R-121: a gap nothing was chosen for (the range raw without an aperture) offers no
    /// picker: no pick could cure it, so the prompt must not promise a verdict.
    /// </summary>
    [Fact]
    public async Task R121_AGapNothingWasChosenForOffersNoPicker()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, rangePair: true);
        LoadedFrame noFStop = LensProfileTestData.Range135Raw();
        noFStop.FNumber = null;
        var vm = new StructureTabViewModel(new ProfileCatalog(() => sources), path => RawReader.IsRawPath(path) ? noFStop : ImageLoader.LoadInfo(path));

        await vm.RunAsync(f.Session, CancellationToken.None);

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.False(vm.HasProfileRows);
        Assert.NotEmpty(vm.ProfileGap);
        Assert.False(vm.OffersLensChoice);
        Assert.Empty(vm.LensChoicePrompt);
        Assert.Null(vm.SelectedLens);
    }

    // ------------------------------------------------------ the one verdict scale (R-126)

    /// <summary>
    /// R-126: with the vignetting compared, the five tests' scale and the depth's are one table:
    /// every band carries its interval in percent, the same band in stops (the core's
    /// DepthScaleRows, not a list of the window's) and the console's meaning. The five tests'
    /// verdict marks a percent cell and the nearest profile's verdict a stops cell - on this
    /// fixture in two different rows, which is why a row cannot be what is marked. The sentence
    /// that the stops are the same scale sits under the table; the card keeps only what the End
    /// to end test would read.
    /// </summary>
    [Fact]
    public async Task R126_WithTheVignettingComparedOneScaleCarriesTheStopsAndMarksEachVerdictsCell()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, _) = await RunWithProfiles(f, ws);
        Assert.True(vm.HasProfileRows, vm.ErrorText);

        var percent = StructureReport.Scale();
        var stops = StructureReport.DepthScaleRows();
        Assert.True(vm.ScaleHasDepth);
        Assert.Equal(new[] { "PERFECT", "BUN", "ACCEPTABIL", "REFA" }, vm.Scale.Select(s => s.Band).ToArray());
        Assert.Equal(percent.Select(p => p.Band), stops.Select(d => d.Band));
        Assert.Equal(percent.Select(p => p.Range), vm.Scale.Select(s => s.Range));
        Assert.Equal(stops.Select(d => d.Range), vm.Scale.Select(s => s.DepthRange));
        Assert.Equal(percent.Select(p => p.Meaning), vm.Scale.Select(s => s.Meaning));
        Assert.All(vm.Scale, s => Assert.True(s.HasDepth));

        string shape = ShapeVerdict(vm);
        string depth = vm.ProfileRows.Single(r => r.IsNearest).Verdict;
        Assert.NotEqual(shape, depth);                                 // two verdicts, two rows
        Assert.Equal(shape, Assert.Single(vm.Scale, s => s.IsCurrent).Band);
        Assert.Equal(depth, Assert.Single(vm.Scale, s => s.IsDepthCurrent).Band);

        Assert.Equal(StructureReport.DepthScaleNote().Replace('\n', ' '), vm.ScaleDepthNote);
        Assert.Contains("Testul End-to-end", vm.ProfileLightNote);
        Assert.DoesNotContain("Nu e un prag nou", vm.ProfileLightNote);
    }

    /// <summary>
    /// R-126: with nothing compared - no raw in flats/, or the box unticked for a filter - the
    /// table is the five tests' alone: no stops in any row, no stops cell marked, no sentence
    /// about stops; the percent cell of the five tests' verdict is still marked.
    /// </summary>
    [Fact]
    public async Task R126_WithNothingComparedTheScaleHasThePercentColumnAlone()
    {
        using var plain = new SessionFixture().WithFlatMaster();
        using var filtered = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        // The window's own case: a catalog, and no raw in flats/ to name the lens.
        var noRaw = new StructureTabViewModel(new ProfileCatalog(() => sources), path => RawReader.IsRawPath(path) ? RawFrame() : ImageLoader.LoadInfo(path));
        await noRaw.RunAsync(plain.Session, CancellationToken.None);
        var withFilter = new StructureTabViewModel(new ProfileCatalog(() => sources), path => RawReader.IsRawPath(path) ? RawFrame() : ImageLoader.LoadInfo(path));
        withFilter.NoFilters = false;
        await withFilter.RunAsync(filtered.Session, CancellationToken.None);

        foreach (StructureTabViewModel vm in new[] { noRaw, withFilter })
        {
            Assert.True(vm.HasResult, vm.ErrorText);
            Assert.False(vm.ScaleHasDepth);
            Assert.Equal(4, vm.Scale.Count);
            Assert.All(vm.Scale, s => Assert.False(s.HasDepth));
            Assert.DoesNotContain(vm.Scale, s => s.IsDepthCurrent);
            Assert.Equal(ShapeVerdict(vm), Assert.Single(vm.Scale, s => s.IsCurrent).Band);
            Assert.Empty(vm.ScaleDepthNote);
            Assert.Empty(vm.ProfileLightNote);
        }
    }

    /// <summary>
    /// R-126: the box redraws the table with the comparison. Unticked, the stops column and its
    /// sentence go and only the five tests' cell stays marked; ticked again, they come back with
    /// the depth's cell: the recompare rebuilds the scale, it does not keep the last one.
    /// </summary>
    [Fact]
    public async Task R126_TheBoxTakesTheStopsColumnAwayAndBringsItBack()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, _) = await RunWithProfiles(f, ws);
        string depth = vm.ProfileRows.Single(r => r.IsNearest).Verdict;
        Assert.True(vm.ScaleHasDepth);

        vm.NoFilters = false;
        await vm.Pending;

        Assert.False(vm.ScaleHasDepth);
        Assert.All(vm.Scale, s => Assert.False(s.HasDepth));
        Assert.DoesNotContain(vm.Scale, s => s.IsDepthCurrent);
        Assert.Empty(vm.ScaleDepthNote);
        Assert.Equal(ShapeVerdict(vm), Assert.Single(vm.Scale, s => s.IsCurrent).Band);

        vm.NoFilters = true;
        await vm.Pending;

        Assert.True(vm.ScaleHasDepth);
        Assert.Equal(depth, Assert.Single(vm.Scale, s => s.IsDepthCurrent).Band);
        Assert.NotEmpty(vm.ScaleDepthNote);
    }

    /// <summary>R-126: Reset leaves no stops column and no sentence about stops for the next
    /// folder.</summary>
    [Fact]
    public async Task R126_ResetClearsTheStopsColumn()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var (vm, _) = await RunWithProfiles(f, ws);
        Assert.True(vm.ScaleHasDepth);

        vm.Reset();

        Assert.False(vm.ScaleHasDepth);
        Assert.Empty(vm.Scale);
        Assert.Empty(vm.ScaleDepthNote);
        Assert.Empty(vm.ProfileLightNote);
    }
}
