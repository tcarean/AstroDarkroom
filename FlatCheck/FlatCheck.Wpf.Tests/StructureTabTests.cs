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
/// Tabul de structura. Cel mai important test din fisier e cel care compara raportul
/// salvat cu iesirea consolei, caracter cu caracter: exact aia e cerinta, iar daca
/// fereastra ar incepe sa-si compuna singura textul, s-ar rupe tacut.
/// </summary>
public class StructureTabTests
{
    static async Task<StructureTabViewModel> Run(SessionFixture f)
    {
        var vm = new StructureTabViewModel();
        await vm.RunAsync(f.Session, CancellationToken.None);
        return vm;
    }

    /// <summary>Masterul din masters/ (Siril 1.4) se analizeaza la fel, si sursa spune de unde a venit.</summary>
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
    /// Doua mastere in folder: se masoara cel mai nou si se spune, cu ambele cai, fiindca
    /// pot veni din doua seturi de flat-uri diferite si omul trebuie sa stie pe care il vede.
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
    /// CERINTA 5, literal: fisierul salvat trebuie sa contina EXACT iesirea consolei.
    /// Se compara cu textul produs de acelasi strat pe care il foloseste flatcheck.exe -
    /// deci daca cineva ar reformata ceva doar pentru fereastra, testul cade.
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

    /// <summary>Antetul e primul lucru din fisier: un raport lipit spune singur ce l-a produs.</summary>
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
    /// Pe chipul "auto", ce se vede pe ecran si ce se salveaza pe disc sunt aceiasi octeti.
    ///
    /// Testul cerea asta NECONDITIONAT pana la comutatorul de scara, si atunci era corect:
    /// fereastra afisa mereu encodarea automata. Acum implicitul e +/-10 %, deci pe ecran
    /// e alta encodare a ACELUIASI reziduu - o alegere de privit, nu o a doua masuratoare.
    /// Fisierul ramane pe auto (vezi TheSavedMapStaysOnTheAutoScale), deci garantia se
    /// pastreaza exact acolo unde mai are sens: cand omul cere chiar scara fisierului.
    /// </summary>
    [Fact]
    public async Task OnTheAutoScaleTheDisplayedMapIsTheSameFileThatWasSaved()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);
        Cap(vm, null).IsSelected = true;

        Assert.Equal(File.ReadAllBytes(f.Session.ResidualPngPath), vm.Map);
    }

    // ------------------------------------------------------------------ randurile

    /// <summary>
    /// Cele cinci teste apar toate, in ordinea din consola, si fiecare cu verdictul lui.
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
    /// Pe un master float scris de Siril (valorile impartite la 65535) eticheta de
    /// niveluri spunea "median 0 ADU / max 0 ADU", ca si cum fisierul ar fi gol.
    /// Formatarea vine acum din nucleu si e cea din raport.
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
    /// Atentionarea de praf: apare cand exista o pata compacta, cu textul din nucleu, si
    /// lipseste pe un master curat. Nu e verdict - verdictul ramane cel al testelor.
    /// </summary>
    [Fact]
    public async Task ADustShadowShowsTheLocalSpotWarningFromTheCore()
    {
        // fixture-ul scrie 128x128 -> raster de 64x64, dale fine de 2 px: o umbra de
        // 8 px raza pe senzor e 4 px pe raster, cat sa umple o dala fara sa umple si
        // vecinatatea de 7x7 din care se scade mediana
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
        Assert.Contains(vm.LocalSpotWarning.Split('\n')[0], vm.Report);   // acelasi text ca in raport
    }

    [Fact]
    public async Task ACleanMasterShowsNoLocalSpotWarning()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Empty(vm.LocalSpotWarning);
    }

    /// <summary>
    /// Fiecare rand de test isi spune ce masoara. Asimetria azimutala si inclinarea au
    /// stat cu o cifra, un verdict si nicio descriere, fiindca notele se scriau inline
    /// aici si nimeni nu numara cate lipsesc. Acum vin toate din nucleu, iar testul asta
    /// cade daca mai apare un rand fara nota.
    /// </summary>
    [Fact]
    public async Task EveryTestRowCarriesItsOwnDescription()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.All(vm.Tests, t => Assert.True(t.HasNote, "rand fara descriere: " + t.Label));
    }

    /// <summary>Descrierile vin din nucleu, nu sunt rescrise in fereastra.</summary>
    [Fact]
    public async Task TheDescriptionsAreTheOnesTheCoreDefines()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(StructureReport.TestNote(nameof(StructureResult.AzimuthalMaxPct)), vm.Tests[1].Note);
        Assert.Equal(StructureReport.TestNote(nameof(StructureResult.ResidualPtpPct)), vm.Tests[2].Note);
        Assert.Equal(StructureReport.TestNote(nameof(StructureResult.TiltPct)), vm.Tests[3].Note);
        Assert.Equal(StructureReport.TestNote(nameof(StructureResult.MaxTilePct)), vm.Tests[4].Note);
        // coltul isi adauga cele patru procente pe un al doilea rand
        Assert.StartsWith(StructureReport.TestNote(nameof(StructureResult.CornerSpreadPct)), vm.Tests[0].Note);
        Assert.Contains("stanga-sus", vm.Tests[0].Note);
    }

    /// <summary>Reziduul e cel care conteaza cel mai mult, si se vede ca atare.</summary>
    [Fact]
    public async Task OnlyTheResidualRowIsMarkedAsTheKeyOne()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        var key = Assert.Single(vm.Tests.Where(t => t.IsKey));
        Assert.Contains("reziduu", key.Label);
    }

    /// <summary>
    /// Randurile informative NU poarta verdict. Offsetul centrului de iluminare a fost
    /// un test pana in 1.2.0 si nu mai e - nu e scale-invariant, iar peste un prag nici
    /// macar monoton - deci o pastila colorata langa el ar reintroduce exact greseala.
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
    /// Scara vine din StructureReport.Scale(), deci din aceleasi praguri ca raportul, si
    /// banda in care a cazut verdictul e marcata.
    /// </summary>
    [Fact]
    public async Task TheScaleHasFourBandsAndMarksTheCurrentOne()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(4, vm.Scale.Count);
        Assert.Equal(new[] { "PERFECT", "BUN", "ACCEPTABIL", "REFA" },
                     vm.Scale.Select(s => s.Band).ToArray());

        // Titlul din XAML numara TESTELE, nu benzile, si a ramas "PATRU" o versiune
        // intreaga dupa ce testele au ajuns cinci - vazut abia intr-o randare.
        Assert.Equal(5, vm.Tests.Count);

        var current = Assert.Single(vm.Scale.Where(s => s.IsCurrent));
        Assert.Equal(vm.Verdict, current.Band);
    }

    /// <summary>Sfatul vine din nucleu, nu e rescris in fereastra.</summary>
    [Fact]
    public async Task TheAdviceIsTheOneTheReportWouldPrint()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(StructureReport.Advice(vm.Verdict), vm.Advice);
    }

    // ------------------------------------------------------------------ esecuri

    /// <summary>
    /// Un fisier care nu e FITS: analiza pica, dar tabul ramane intreg si spune de ce.
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

    /// <summary>Golirea sterge tot, ca sa nu ramana cifre din folderul precedent.</summary>
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

    // ------------------------------------------ vignetarea fata de profilul obiectivului

    static LoadedFrame RawFrame() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 20, FNumber = 2.8, Camera = "NIKON D3300",
        NikonLensId = 139, LensMinFocalMm = 11, LensMaxFocalMm = 20, LensMaxAperture = 2.8,
    };

    /// <summary>The tab with hand-made profile sources and a fabricated raw in place of the file in flats/.</summary>
    static async Task<(StructureTabViewModel Vm, ProfileSources Sources)> RunWithProfiles(SessionFixture f, TempWorkspace ws)
    {
        var sources = LensProfileTestData.Sources(ws);
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

    /// <summary>The fixture master is deeper than the model: shape PERFECT, depth worse, and the depth carries the verdict and the advice.</summary>
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

    // -------------------------------------------------------- scara hartii

    static MapScaleOption Cap(StructureTabViewModel vm, double? capPct)
        => vm.MapScale.Options.Single(o => o.Label == RunReport.MapScaleLabel(capPct));

    /// <summary>
    /// Implicit e banda in care se asteapta sa traiasca un flat normal, nu "auto": doua
    /// sesiuni trebuie sa se poata compara dintr-o privire, fara sa apesi nimic.
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
    /// Fisierul de pe disc NU urmeaza chipul bifat. Consola scrie encodarea automata, si
    /// cele doua aplicatii trebuie sa scrie acelasi fisier pentru acelasi master -
    /// chipurile sunt un ajutor de privit, nu o setare de iesire.
    /// </summary>
    [Fact]
    public async Task TheSavedMapStaysOnTheAutoScale()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var session = f.Session;

        var vm = await Run(f);
        Cap(vm, Th.ResidPerfect).IsSelected = true;      // si dupa ce s-a schimbat scara

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
    /// Pe "auto" legenda spune capetele CHIAR calculate - tocmai ele nu se puteau vedea
    /// pe ecran inainte, iar ele fac ca rosu plin sa insemne altceva de la o imagine la alta.
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
    /// Alegerea de scara e o preferinta de privit, nu un rezultat: trece peste schimbarea
    /// folderului. Harta si rasterul, nu.
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

    /// <summary>Encodarea e determinista, si a doua bifare o ia din cache.</summary>
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

    /// <summary>Un singur chip bifat, si asta se vede in ViewModel, nu doar in grupul de butoane.</summary>
    [Fact]
    public async Task ExactlyOneScaleIsSelectedAtATime()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = await Run(f);

        Cap(vm, null).IsSelected = true;

        Assert.Single(vm.MapScale.Options.Where(o => o.IsSelected));
        Assert.Equal("auto", vm.MapScale.Selected.Label);
    }

    // -------------------------------------------------------- ghidul hartii

    /// <summary>
    /// Ghidul e documentatie, nu rezultat: exista si inainte de orice rulare, si nu se
    /// schimba dupa. Daca ar depinde de un rezultat, ar fi un diagnostic - si tocmai asta
    /// nu e, fiindca unealta nu clasifica forma hartii.
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

    /// <summary>Pliat implicit, si starea trece peste schimbarea folderului ca si scara.</summary>
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
