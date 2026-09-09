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
    /// Cele patru taburi, cu rezultate reale in ele. Un tab gol s-ar desena si cu jumatate
    /// din sabloane gresite - abia cu date se ating toate legarile.
    /// </summary>
    [Fact]
    public async Task EveryTabRendersWithRealResultsAndNoBindingErrors()
    {
        using var f = new SessionFixture()
            .WithFlatMaster()
            .WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit")
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
            vm.Ratio.Choose(isA: true, path: f.MasterPath("pp_flat_A.fit"));
            vm.Ratio.Choose(isA: false, path: f.MasterPath("pp_flat_B.fit"));
            await vm.Ratio.RecomputeAsync();

            Render(new MainWindow { DataContext = vm }, 1360, 820, "fereastra");
            Render(new StructureView { DataContext = vm.Structure }, name: "tab-structura");
            Render(new RatioView { DataContext = vm.Ratio }, name: "tab-ratio");
            Render(new LightView { DataContext = vm.Light }, name: "tab-end-to-end");
            Render(new ExposureView { DataContext = vm.Exposure }, name: "tab-expunere");

            // Ghidul hartii, desfasurat. Pliat, legarile lui din interiorul cardului nu
            // sunt niciodata evaluate - si tocmai o legare care cade abia la afisare e
            // ce exista suita asta sa prinda.
            vm.Structure.MapGuide.IsExpanded = true;
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
    }

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
    /// niveluri, lista canalelor CFA, verdictul.
    /// </summary>
    [Fact]
    public async Task TheExposureTabRendersAResult()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");
        var vm = new ExposureTabViewModel();

        await _sta.InvokeAsync(async () =>
        {
            vm.Attach(f.Session, CancellationToken.None);
            vm.Selected = path;
            await vm.RecomputeAsync();

            using var errors = new BindingErrors();
            Render(new ExposureView { DataContext = vm }, name: "tab-expunere-rezultat");
            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

        Assert.True(vm.HasResult);
    }

    /// <summary>
    /// Tabul de expunere inainte de orice alegere: are lista, dar niciun rezultat. E
    /// starea in care il gaseste omul cand comuta pe el, deci trebuie sa se desene.
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
