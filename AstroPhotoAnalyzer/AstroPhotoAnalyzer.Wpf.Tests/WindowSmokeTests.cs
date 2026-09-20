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
// XAML-ul nu se verifica la compilare: o resursa cu nume gresit, un convertor lipsa, o
// legare catre o proprietate inexistenta trec de compilator si se vad abia la rulare -
// unele la INCARCARE. Testele de aici construiesc fereastra, o aseaza, o deseneaza
// intr-un bitmap si asculta erorile de legare. Ce nu se verifica: cum arata.
//
// APA_DUMP_SCREENS=<folder> salveaza randarile ca PNG - ca sa te uiti la fereastra fara
// s-o pornesti. Nu e o comparatie cu o referinta.

using AstroPhotoAnalyzer.Tests;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("fereastra")]
public class WindowSmokeTests
{
    readonly StaFixture _sta;

    public WindowSmokeTests(StaFixture sta) => _sta = sta;

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

    static void Render(FrameworkElement el, int w = 1400, int h = 860, string? name = null)
    {
        el.Measure(new Size(w, h));
        el.Arrange(new Rect(0, 0, w, h));
        el.UpdateLayout();

        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(el);
        Assert.Equal(w, bmp.PixelWidth);

        string? dir = Environment.GetEnvironmentVariable("APA_DUMP_SCREENS");
        if (string.IsNullOrEmpty(dir) || name == null) return;
        Directory.CreateDirectory(dir);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(Path.Combine(dir, name + ".png"));
        enc.Save(fs);
    }

    static string DarkTiff(TempWorkspace ws)
    {
        Raster r = Scenes.DarkSky();
        return ws.WriteFile("cer-intunecat.tif", TiffTestData.Build16Rgb(r.W, r.H,
            (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V2("sRGB test")));
    }

    /// <summary>
    /// Se deseneaza CONTINUTUL ferestrei, nu fereastra: un Window neafisat se randeaza
    /// transparent (PNG alb), iar panoul radacina mosteneste DataContext-ul si trece prin
    /// aceleasi legari.
    /// </summary>
    static FrameworkElement ContentOf(MainWindow w) => (FrameworkElement)w.Content;

    /// <summary>Cazul din FlatCheck care a crapat la pornire: fereastra goala, fara rezultat.</summary>
    [Fact]
    public void TheMainWindowLoadsAndRenders()
        => _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();
            Render(ContentOf(new MainWindow()), name: "fereastra-goala");
            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

    [Fact]
    public void TheAboutWindowLoadsAndRenders()
        => _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();
            var about = new AboutWindow();
            Render((FrameworkElement)about.Content, 600, 720, "despre");
            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);

            var logo = Assert.IsAssignableFrom<BitmapSource>(about.Logo.Source);
            Assert.Equal(256, logo.PixelWidth);
        });

    /// <summary>
    /// Toate taburile, cu un rezultat real. Un tab gol s-ar desena si cu jumatate din
    /// sabloane gresite - abia cu date se ating toate legarile.
    /// </summary>
    [Fact]
    public async Task EveryTabRendersWithARealResultAndNoBindingErrors()
    {
        using var ws = new TempWorkspace();
        string tif = DarkTiff(ws);
        var vm = new MainViewModel();
        vm.Parameters.Aspect = "50:35";
        vm.Parameters.Distance = "1.5";

        await _sta.InvokeAsync(async () =>
        {
            using var errors = new BindingErrors();

            await vm.OpenAsync(tif);
            Assert.True(vm.HasResult, vm.Error);

            Render(ContentOf(new MainWindow { DataContext = vm }), name: "fereastra");
            Render(new ReportView { DataContext = vm.Report }, name: "tab-raport");
            vm.Report.ShowText = true;
            Render(new ReportView { DataContext = vm.Report }, h: 2200, name: "tab-raport-text");
            Render(new TonesView { DataContext = vm.Tones }, name: "tab-tonuri");
            Render(new ExportView { DataContext = vm.Export }, name: "tab-export");
            Render(new ResolutionView { DataContext = vm.Resolution }, name: "tab-rezolutie");
            Render(new BackgroundView { DataContext = vm.Background }, name: "tab-fundal");
            Render(new PreviewView { DataContext = vm.Preview }, h: 2400, name: "tab-preview");

            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });

        Assert.Equal("PROBLEMA", vm.Report.VerdictLabel);
        Assert.Equal(6, vm.Report.Sections.Count);
        Assert.NotEmpty(vm.Preview.Tiles);
        Assert.Contains(vm.Preview.Tiles, t => t.Title.StartsWith("Simulare print"));
        Assert.Contains(vm.Preview.Tiles, t => t.Title.StartsWith("Decupaj"));
    }

    /// <summary>Cardul de eroare, cu textul refuzului - starea pe care o vede omul pe un FITS liniar.</summary>
    [Fact]
    public async Task TheErrorCardRendersARefusal()
    {
        using var ws = new TempWorkspace();
        string bad = ws.WriteFile("gol.tif", new byte[16]);
        var vm = new MainViewModel();

        await _sta.InvokeAsync(async () =>
        {
            using var errors = new BindingErrors();
            await vm.OpenAsync(bad);
            Assert.True(vm.HasError);
            Render(ContentOf(new MainWindow { DataContext = vm }), name: "fereastra-eroare");
            Assert.True(errors.Text.Length == 0, "erori de legare:\n" + errors.Text);
        });
    }
    /// <summary>
    /// Tooltipul de ajutor, cu stilul temei si textul real. Un tooltip e un popup, nu se
    /// vede in randarea ferestrei - de asta se randeaza controlul direct: latimea trebuie
    /// sa fie cea din tema (nu un rand de un metru), iar textul destul de inalt pentru
    /// paragrafele lui. APA_DUMP_SCREENS il salveaza ca "tooltip-distanta.png".
    /// </summary>
    [Fact]
    public void TheHelpTooltipRendersAtTheThemeWidth()
        => _sta.Invoke(() =>
        {
            var tip = new ToolTip { Content = new ParameterFields().DistanceHelp };
            tip.Measure(new Size(1000, 1000));
            Size s = tip.DesiredSize;

            Assert.InRange(s.Width, 300, 420 + 2 * 8 + 2);   // MaxWidth + padding + bordura
            Assert.True(s.Height > 12 * 17, "tooltipul e prea scund pentru patru paragrafe: " + s.Height);
            Render(tip, (int)Math.Ceiling(s.Width), (int)Math.Ceiling(s.Height), "tooltip-distanta");
        });
}
