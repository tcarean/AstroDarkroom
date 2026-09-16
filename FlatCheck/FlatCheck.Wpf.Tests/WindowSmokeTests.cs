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

// WindowSmokeTests.cs - fereastra chiar se incarca si chiar se deseneaza.
//
// DE CE EXISTA. Restul testelor verifica ViewModel-uri, care sunt C# obisnuit. XAML-ul nu
// se verifica la fel: o resursa cu nume gresit, un convertor lipsa sau o legare catre o
// proprietate care nu exista trec de compilator si se vad abia la rulare - iar unele, la
// INCARCARE, adica de fiecare data.
//
// Testul a fost scris dupa ce fereastra a crapat la pornire, mereu, cu "Cannot find
// non-neutral culture related to 'en-us'": proiectul mostenise <InvariantGlobalization>
// de la aplicatia de consola, unde e corect, iar WPF nu poate lega nimic fara date de
// cultura. Compilarea trecea, toate cele o suta de teste treceau, si aplicatia nu pornea.
// Acum ar pica aici.
//
// Ce se verifica: fereastra se construieste, se aseaza, se deseneaza intr-un bitmap, si
// nu produce nicio eroare de legare pe drum. Ce nu se verifica: cum arata.

using FlatCheck.Tests;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlatCheck.Wpf.Tests;

[Collection("fereastra")]
public class WindowSmokeTests
{
    readonly StaFixture _sta;

    public WindowSmokeTests(StaFixture sta) => _sta = sta;

    /// <summary>
    /// Prinde erorile de legare, care altfel se scurg tacut in fereastra de Output a
    /// depanatorului si nu se vad niciodata dintr-un test.
    /// </summary>
    sealed class BindingErrors : IDisposable
    {
        readonly StringBuilder _sb = new();
        readonly TraceListener _listener;
        readonly SourceLevels _before;

        internal BindingErrors()
        {
            PresentationTraceSources.Refresh();
            _before = PresentationTraceSources.DataBindingSource.Switch.Level;
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            _listener = new TextWriterTraceListener(new StringWriter(_sb));
            PresentationTraceSources.DataBindingSource.Listeners.Add(_listener);
        }

        internal string Text { get { _listener.Flush(); return _sb.ToString(); } }

        public void Dispose()
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(_listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = _before;
            _listener.Dispose();
        }
    }

    /// <summary>
    /// Deseneaza un element intr-un bitmap - forteaza tot lantul de sabloane.
    ///
    /// Cu FLATCHECK_DUMP_SCREENS=&lt;folder&gt; salveaza si un PNG. Nu e o comparatie cu o
    /// referinta si nici nu trebuie sa devina una: cum arata o fereastra e treaba
    /// ochiului. E doar cum te uiti la ea fara sa pornesti aplicatia - de exemplu cand
    /// sesiunea Windows e blocata si o captura de ecran ar prinde ecranul de blocare.
    /// </summary>
    static void Render(FrameworkElement el, int w = 1360, int h = 800, string? name = null)
    {
        el.Measure(new Size(w, h));
        el.Arrange(new Rect(0, 0, w, h));
        el.UpdateLayout();

        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(el);

        Assert.Equal(w, bmp.PixelWidth);
        Dump(bmp, name);
    }

    static void Dump(RenderTargetBitmap bmp, string? name)
    {
        string? dir = Environment.GetEnvironmentVariable("FLATCHECK_DUMP_SCREENS");
        if (string.IsNullOrEmpty(dir) || name == null) return;

        Directory.CreateDirectory(dir);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(Path.Combine(dir, name + ".png"));
        enc.Save(fs);
    }

    // ------------------------------------------------------------------ testele

    /// <summary>Cazul care a crapat: fereastra nici nu ajungea sa se afiseze.</summary>
    [Fact]
    public void TheMainWindowLoadsAndRenders()
        => _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();
            Render(new MainWindow(), 1360, 820);
            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

    /// <summary>
    /// Fereastra "Despre": XAML nou, un sablon nou in tema, o resursa de icoana citita
    /// prin decodor si link-uri legate la proprietati - toate lucruri care compileaza cu
    /// numele gresit si cad abia la afisare.
    /// </summary>
    [Fact]
    public void TheAboutWindowLoadsAndRenders()
        => _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();
            var about = new AboutWindow();

            // Se deseneaza CONTINUTUL, nu fereastra: un Window neafisat se randeaza
            // transparent (asa iese si "fereastra" de mai jos), iar panoul radacina
            // mosteneste DataContext-ul si trece prin aceleasi legari.
            Render((FrameworkElement)about.Content, 600, 720, "despre");
            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);

            // icoana e cadrul mare, nu cel de 16 px scalat
            var logo = Assert.IsAssignableFrom<BitmapSource>(about.Logo.Source);
            Assert.Equal(256, logo.PixelWidth);
        });

    /// <summary>
    /// Cele patru taburi, cu rezultate reale in ele. Un tab gol s-ar desena si cu jumatate
    /// din sabloane gresite - abia cu date se ating toate legarile.
    /// </summary>
    [Fact]
    public async Task EveryTabRendersWithRealResultsAndNoBindingErrors()
    {
        using var f = new SessionFixture()
            .WithFlatMaster()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit")
            .WithLightSeries(6)
            .WithRawFlats("DSC_0101.NEF", "DSC_0102.NEF");

        var vm = new MainViewModel();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);

            using var errors = new BindingErrors();

            // Tabul Ratio porneste gol - randat asa, fiindca aia e starea pe care o vede
            // omul cand deschide fereastra, si are propriile legari (documentatia).
            Render(new RatioView { DataContext = vm.Ratio }, name: "tab-ratio-gol");

            // ...si abia apoi cu rezultat, dupa cele doua alegeri explicite.
            vm.Ratio.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
            vm.Ratio.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));
            await vm.Ratio.RecomputeAsync();

            Render(new MainWindow { DataContext = vm }, 1360, 820, "fereastra");
            Render(new StructureView { DataContext = vm.Structure }, name: "tab-structura");
            Render(new RatioView { DataContext = vm.Ratio }, name: "tab-ratio");
            Render(new LightView { DataContext = vm.Light }, name: "tab-end-to-end");
            Render(new ExposureView { DataContext = vm.Exposure }, name: "tab-expunere");
            Render(new HistEqView { DataContext = vm.HistEq }, name: "tab-histeq-gol");
            // Netezirea porneste goala, cu ghidul deschis si butonul activ - starea pe
            // care o vede omul; rezultatul ei se randeaza in testul dedicat.
            Render(new SmoothView { DataContext = vm.Smooth }, name: "tab-netezire-gol");

            // Ghidul hartii, desfasurat. Pliat, legarile lui din interiorul cardului nu
            // sunt niciodata evaluate - si tocmai o legare care cade abia la afisare e
            // ce exista suita asta sa prinda.
            vm.Structure.MapGuide.IsExpanded = true;
            vm.Structure.HowToRead.IsExpanded = true;
            vm.Ratio.MapGuide.IsExpanded = true;
            Render(new StructureView { DataContext = vm.Structure }, name: "tab-structura-ghid");
            Render(new RatioView { DataContext = vm.Ratio }, name: "tab-ratio-ghid");

            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

        // si chiar au avut ce desena
        Assert.True(vm.Structure.HasResult);
        Assert.True(vm.Light.HasResult);
        Assert.True(vm.Ratio.HasResult);
        Assert.Equal(2, vm.Exposure.Files.Count);
        Assert.Equal(3, vm.HistEq.Files.Count);
        Assert.True(vm.Smooth.CanRun);
        Assert.False(vm.Smooth.HasResult);
    }

    /// <summary>
    /// Tabul de netezire CU rezultat - ambele harti, atentionarea, cele doua baterii -
    /// si in starea de refuz (cub de culoare). Rezultatul vine prin Populate, fara
    /// fisier scris, ca la tabul de histograma.
    /// </summary>
    [Fact]
    public async Task TheSmoothTabRendersAResultAndARefusal()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var blotched = TestImages.From(256, 256, (x, y) =>
            FitsTestData.Radial(256, 256)(x, y)
            * (1 + 0.10 * Math.Exp(-((x - 60) * (x - 60) + (y - 180) * (y - 180)) / (2 * 10.0 * 10.0))));
        var result = SmoothAnalyzer.Analyze(blotched, 12);

        _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();

            var vm = new SmoothTabViewModel(_ => true);
            vm.Attach(f.Session, CancellationToken.None);
            vm.Populate(result);
            Render(new SmoothView { DataContext = vm }, h: 1800, name: "tab-netezire");

            vm.ShowAfterMap = true;
            vm.Guide.IsExpanded = false;
            Render(new SmoothView { DataContext = vm }, name: "tab-netezire-dupa");

            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

        Assert.True(result.RemovedFlagged);

        // refuzul, prin rularea reala pe un cub
        using var g = new SessionFixture().WithProcess();
        File.WriteAllBytes(Path.Combine(g.ProcessDir, SirilSession.FlatMasterName),
            FitsTestData.BuildFitsCube(32, 32, 3, (x, y, p) => 1000 + p));
        var refused = new SmoothTabViewModel(_ => true);
        refused.Attach(g.Session, CancellationToken.None);
        await refused.RunAsync();
        Assert.True(refused.HasError);

        _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();
            Render(new SmoothView { DataContext = refused }, name: "tab-netezire-refuz");
            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });
    }

    /// <summary>
    /// Tabul de histograma CU rezultat: chipurile de vedere, poza, nota de master, ghidul
    /// desfasurat, apoi o vedere pe canal si un rezultat mono (fara chipuri). Rezultatul
    /// vine direct din analizor, ca la previzualizarea de pe tabul de expunere.
    /// </summary>
    [Fact]
    public void TheHistEqTabRendersAResult()
        => _sta.Invoke(() =>
        {
            // Un mozaic pe un camp radial, nu FlatCfa: acela e constant pe canal si se
            // egalizeaza intr-un alb uniform, ceea ce e corect si nu se vede nimic din el.
            var cfa = HistEqAnalyzer.Analyze(
                FlatCheck.Tests.TestImages.From(128, 128, FlatCheck.Tests.FitsTestData.WithBayerMosaic(
                    FlatCheck.Tests.FitsTestData.Radial(128, 128, center: 34000))),
                CfaPattern.Parse("RGGB", "BAYERPAT"));
            var vm = new HistEqTabViewModel();
            vm.Populate(cfa, isMaster: true);

            using var errors = new BindingErrors();
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq");

            vm.Guide.IsExpanded = true;
            Render(new HistEqView { DataContext = vm }, h: 1600, name: "tab-histeq-ghid");

            vm.View.Options.First(o => o.Label == "G1").IsSelected = true;
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq-canal");

            // zoom-ul: 100 % si un factor mare, cu controlul de deplasare in joc
            vm.Zoom.Actual.Execute(null);
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq-100");
            vm.Zoom.Manual(3);
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq-zoom");
            Assert.Equal(3, vm.Zoom.EffectiveZoom, 9);

            var mono = HistEqAnalyzer.Analyze(
                FlatCheck.Tests.TestImages.From(128, 128, FlatCheck.Tests.FitsTestData.Radial(128, 128)), null);
            vm.Populate(mono);
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq-mono");

            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
            Assert.True(vm.HasResult);
            Assert.False(vm.View.IsVisible);
            Assert.Equal("", vm.MasterNote);
            // fisier nou: inapoi la potrivit, si controlul a scris factorul efectiv sub 1
            Assert.True(vm.Zoom.IsFit);
            Assert.InRange(vm.Zoom.EffectiveZoom, 0.01, 1.0);
        });

    /// <summary>
    /// Ecranul de problema are propriile legari, si e chiar ecranul pe care il vede cineva
    /// care deschide aplicatia pe un folder unde n-a rulat inca scriptul din Siril - adica
    /// primul ecran al primei utilizari gresite.
    /// </summary>
    [Fact]
    public async Task TheProblemScreenRenders()
    {
        using var f = new SessionFixture().WithProcess();
        var vm = new MainViewModel();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);

            using var errors = new BindingErrors();
            Render(new MainWindow { DataContext = vm }, 1360, 820);
            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

        Assert.True(vm.HasProblem);
    }

    /// <summary>
    /// Ecranul de problema cu blocul de rulare in toate starile lui: se poate apasa (cu
    /// avertismentul ca Siril e deschis), blocat de un folder lipsa, blocat fara Siril.
    /// Instalarea e falsa, ca desenul sa nu depinda de masina.
    /// </summary>
    [Fact]
    public async Task TheProblemScreenRendersTheRunBlock()
    {
        using var f = new SessionFixture().WithProcess();
        using var ws = new TempWorkspace();
        string script = Path.Combine(ws.Dir, "OSC_Preprocessing_WithoutDark.ssf");
        File.WriteAllText(script, SirilInstallTests.WithoutDark);
        string cli = Path.Combine(ws.Dir, "siril-cli.exe");
        File.WriteAllText(cli, "x");
        foreach (string d in new[] { "biases", "flats" })
        {
            Directory.CreateDirectory(Path.Combine(f.Root, d));
            File.WriteAllText(Path.Combine(f.Root, d, "a.NEF"), "x");
        }

        var install = new SirilInstall(cli, new[] { script }, Path.Combine(ws.Dir, "config.ini"));
        var vm = new MainViewModel(() => install, () => true);

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);

            using var errors = new BindingErrors();
            // continutul, nu fereastra: un Window neafisat se randeaza transparent
            Render((FrameworkElement)new MainWindow { DataContext = vm }.Content, 1360, 820, "problema-blocat");

            Directory.CreateDirectory(Path.Combine(f.Root, "lights"));
            File.WriteAllText(Path.Combine(f.Root, "lights", "a.NEF"), "x");
            vm.Preprocess.Refresh();
            Render((FrameworkElement)new MainWindow { DataContext = vm }.Content, 1360, 820, "problema-ruleaza");

            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

        Assert.True(vm.Preprocess.CanRun);
        Assert.True(vm.Preprocess.HasWarning);
    }

    /// <summary>
    /// Fereastra de rulare: in timpul rularii (bara, comanda curenta, log), apoi reusita,
    /// apoi esuata. Liniile sunt cele inregistrate de la siril-cli, printr-un runner fals.
    /// </summary>
    [Fact]
    public async Task TheRunWindowRendersRunningAndFinished()
        => await _sta.InvokeAsync(async () =>
        {
            using var errors = new BindingErrors();

            var running = new SirilRunViewModel(@"E:\sesiune", @"C:\scripturi\OSC_Preprocessing_WithoutDark.ssf",
                                                SirilInstallTests.WithoutDark) { Warning = SessionMessages.SirilGuiRunning() };
            var gate = new TaskCompletionSource<int>();
            Task run = running.RunAsync((onLine, ct) =>
            {
                onLine("log: Welcome to siril 1.4.4 for windows (x86_64 ucrt64) - CLI");
                onLine("log: Running command: requires");
                onLine("log: Running command: cd");
                onLine("log: Running command: convert");
                onLine("progress: Converting files, 0.00%");
                onLine("\x1b[A\x1b[2KTprogress: 33.33%");
                return gate.Task;
            });
            var window = new SirilRunWindow(running, (_, _) => Task.FromResult(0));
            Render((FrameworkElement)window.Content, 900, 600, "siril-ruleaza");
            Assert.True(running.IsRunning);

            gate.SetResult(0);
            await run;
            Render((FrameworkElement)window.Content, 900, 600, "siril-reusit");
            Assert.True(running.IsSuccess);

            var failed = new SirilRunViewModel(@"E:\sesiune", @"C:\scripturi\OSC_Preprocessing_WithoutDark.ssf",
                                               SirilInstallTests.WithoutDark);
            await failed.RunAsync((onLine, ct) =>
            {
                onLine("log: Running command: requires");
                onLine("log: Running command: cd");
                return Task.FromResult(1);
            });
            var failedWindow = new SirilRunWindow(failed, (_, _) => Task.FromResult(1));
            Render((FrameworkElement)failedWindow.Content, 900, 600, "siril-esuat");
            Assert.True(failed.IsFailure);

            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

    /// <summary>
    /// Refuzul gardei de camp: alt drum prin sabloane - fara harta, fara panoul de cifre,
    /// cu pastila neutra - deci merita desenat separat.
    /// </summary>
    [Fact]
    public async Task TheRefusedEndToEndTabRenders()
    {
        using var f = new SessionFixture()
            .WithFlatMaster()
            .WithLightSeries(6, SessionFixture.WideFieldCards);

        var vm = new MainViewModel();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);

            using var errors = new BindingErrors();
            Render(new LightView { DataContext = vm.Light });
            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

        Assert.True(vm.Light.Refused);
    }

    /// <summary>
    /// Tabul de expunere CU rezultat: alt drum prin sabloane decat cel gol - panoul de
    /// niveluri, lista canalelor CFA, verdictul, si cele patru desene de histograma.
    ///
    /// Ghidul se randeaza si desfasurat, din acelasi motiv ca la ghidul hartii: pliat,
    /// legarile lui din interiorul cardului nu sunt niciodata evaluate.
    /// </summary>
    [Fact]
    public async Task TheExposureTabRendersAResult()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");
        var vm = new ExposureTabViewModel();

        await _sta.InvokeAsync(async () =>
        {
            vm.Attach(f.Session, CancellationToken.None);
            vm.Selected = path;
            await vm.RecomputeAsync();

            using var errors = new BindingErrors();
            Render(new ExposureView { DataContext = vm }, name: "tab-expunere-rezultat");

            vm.HistogramGuide.IsExpanded = true;
            Render(new ExposureView { DataContext = vm }, h: 1400, name: "tab-expunere-ghid");

            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

        Assert.True(vm.HasResult);
        Assert.Equal(4, vm.Histograms.Count);
    }

    /// <summary>
    /// Tabul cu previzualizarea aparatului: comutatorul aprins, vederea pe previzualizare
    /// (randuri fara benzi), tabelul raw / ecran si nota de sub el. Alt drum prin sabloane
    /// decat cel cu comutatorul stins, pe care il acopera celelalte doua teste de mai sus.
    /// Previzualizarea e un JPEG facut in test, deci nu e nevoie de niciun raw.
    /// </summary>
    [Fact]
    public void TheExposureTabRendersThePreviewView()
        => _sta.Invoke(() =>
        {
            PreviewImage preview = PreviewReader.Decode(FlatCheck.Tests.TestImages.JpegOf(160, 120, (x, y) =>
            {
                double d = Math.Sqrt((x - 80) * (x - 80) + (y - 60) * (y - 60)) / 100.0;
                byte v = (byte)(230 - 60 * d);
                return ((byte)(v - 40), v, (byte)Math.Min(255, v + 15));
            }));
            var r = ExposureAnalyzer.Analyze(FlatCheck.Tests.TestImages.FlatCfa(128, 128, 550), 1000, null, false,
                                             null, CfaPattern.Parse("RGGB", "BAYERPAT"), preview);
            var vm = new ExposureTabViewModel();
            vm.Populate(r);

            using var errors = new BindingErrors();
            Render(new ExposureView { DataContext = vm }, h: 1400, name: "tab-expunere-comparatie");

            vm.Source.Preview.IsSelected = true;
            Render(new ExposureView { DataContext = vm }, h: 1400, name: "tab-expunere-ecran");

            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
            Assert.True(vm.Source.IsPreviewSelected);
            Assert.Equal(3, vm.Histograms.Count);
        });

    /// <summary>
    /// Un cadru fara mozaic ia alt drum: un singur rand de histograma, fara culoare si
    /// fara pozitie, plus explicatia in locul verdictului.
    /// </summary>
    [Fact]
    public async Task TheExposureTabRendersAMonoResult()
    {
        using var f = new SessionFixture().WithFlatMaster().WithFlats();
        string path = Path.Combine(f.FlatsDir, "debayerizat.fit");
        File.WriteAllBytes(path, FlatCheck.Tests.FitsTestData.BuildFits(
            128, 128, FlatCheck.Tests.FitsTestData.Radial(128, 128, center: 34000)));
        var vm = new ExposureTabViewModel();

        await _sta.InvokeAsync(async () =>
        {
            vm.Attach(f.Session, CancellationToken.None);
            vm.Selected = path;
            await vm.RecomputeAsync();

            using var errors = new BindingErrors();
            Render(new ExposureView { DataContext = vm }, name: "tab-expunere-mono");
            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

        Assert.Single(vm.Histograms);
        Assert.False(vm.IsRawFlat);
    }

    /// <summary>
    /// Tabul de expunere inainte de orice alegere: are lista, dar niciun rezultat. E
    /// starea in care il gaseste omul cand comuta pe el, deci trebuie sa se desene.
    ///
    /// Acopera si desenul de histograma cu TOATE proprietatile pe valoarea implicita -
    /// curbe null, praguri NaN - fiindca elementul exista in sablon inainte sa existe
    /// vreo cifra. E singurul test care trece pe drumul ala.
    /// </summary>
    [Fact]
    public async Task TheExposureTabRendersBeforeAnythingIsPicked()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF");
        var vm = new MainViewModel();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);

            using var errors = new BindingErrors();
            Render(new ExposureView { DataContext = vm.Exposure });
            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

        Assert.False(vm.Exposure.HasResult);
    }
}
