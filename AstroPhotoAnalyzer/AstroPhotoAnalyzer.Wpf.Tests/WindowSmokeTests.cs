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

// WindowSmokeTests.cs - the window really loads and really renders.
//
// XAML is not checked at compile time: a resource with a wrong name, a missing converter, a
// binding to a property that does not exist pass the compiler and show only at run time -
// some at LOADING. The tests here build the window, lay it out, render it into a bitmap and
// listen for the binding errors. What is not checked: how it looks.
//
// APA_DUMP_SCREENS=<folder> saves the renders as PNG - to look at the window without
// starting it. It is not a comparison against a reference.

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
    /// The window's CONTENT is rendered, not the window: a Window never shown renders
    /// transparent (a white PNG), while the root panel inherits the DataContext and goes
    /// through the same bindings.
    /// </summary>
    static FrameworkElement ContentOf(MainWindow w) => (FrameworkElement)w.Content;

    /// <summary>The FlatCheck case that crashed at startup: the empty window, no result.</summary>
    [Fact]
    public void R110_TheMainWindowLoadsAndRenders()
        => _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();
            var window = new MainWindow();
            Render(ContentOf(window), name: "fereastra-goala");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
            // The save's outcome has its place under the buttons (R-110, R-111).
            Assert.NotNull(window.FindName("SaveMessageText"));
            Assert.NotNull(window.FindName("SaveErrorText"));
        });

    /// <summary>The profile window with a list of its own: three printer profiles, one of them CMYK with its note.</summary>
    [Fact]
    public void R132_TheProfileWindowLoadsAndRenders()
        => _sta.Invoke(() =>
        {
            using var ws = new TempWorkspace();
            ws.WriteFile("baryta.icc", IccPrinterTestData.V2Lut16("Zeta baryta", 20));
            ws.WriteFile("canvas.icm", IccPrinterTestData.V4LutAToB("Alfa canvas", 30));
            ws.WriteFile("tipar.icc", IccPrinterTestData.V2Lut16("Tipar", 8, space: "CMYK"));
            using var errors = new BindingErrors();

            var picker = new IccPickerWindow { DataContext = new IccPickerViewModel(ws.Dir) };
            Render((FrameworkElement)picker.Content, 720, 520, "profil-icc");
            Render((FrameworkElement)new IccPickerWindow { DataContext = new IccPickerViewModel(ws.PathTo("gol")) }.Content, 720, 520, "profil-icc-gol");

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

    [Fact]
    public void R119_TheAboutWindowLoadsAndRenders()
        => _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();
            var about = new AboutWindow();
            Render((FrameworkElement)about.Content, 600, 720, "despre");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);

            var logo = Assert.IsAssignableFrom<BitmapSource>(about.Logo.Source);
            Assert.Equal(256, logo.PixelWidth);
        });

    /// <summary>
    /// Every tab, with a real result. An empty tab would render even with half its
    /// templates wrong - only with data are all the bindings touched.
    /// </summary>
    [Fact]
    public async Task R110_EveryTabRendersWithARealResultAndNoBindingErrors()
    {
        using var ws = new TempWorkspace();
        string tif = DarkTiff(ws);
        var vm = new MainViewModel();
        vm.Parameters.Aspect = "50:35";
        vm.Parameters.Distance = "1.5";
        vm.Parameters.Dmax = "2.0";   // the paper the dark scene was built for (Scenes.BuiltForDmax)

        await _sta.InvokeAsync(async () =>
        {
            using var errors = new BindingErrors();

            await vm.OpenAsync(tif);
            Assert.True(vm.HasResult, vm.Error);

            Render(ContentOf(new MainWindow { DataContext = vm }), name: "fereastra");
            Render(new ReportView { DataContext = vm.Report }, name: "tab-raport");
            vm.Report.ShowText = true;
            Render(new ReportView { DataContext = vm.Report }, h: 2200, name: "tab-raport-text");
            Render(new TonesView { DataContext = vm.Tones }, h: 2000, name: "tab-tonuri");
            Render(new ExportView { DataContext = vm.Export }, name: "tab-export");
            Render(new ResolutionView { DataContext = vm.Resolution }, name: "tab-rezolutie");
            Render(new BackgroundView { DataContext = vm.Background }, name: "tab-fundal");
            Render(new PreviewView { DataContext = vm.Preview }, h: 2400, name: "tab-preview");
            Render(new CurvesView { DataContext = vm.Curves }, name: "tab-curbe-gol");
            // R-146, R-147: the gallery on a suggestion, and its curve window.
            vm.Curves.Compute(1300, 600);
            Assert.True(vm.Curves.HasGallery, vm.Curves.Message);
            vm.Curves.Next.Execute(null);
            Render(new CurvesView { DataContext = vm.Curves }, name: "tab-curbe");
            CurveWindowViewModel? curve = null;
            vm.Curves.CurveRequested += c => curve = c;
            vm.Curves.OpenCurve.Execute(null);
            Render((FrameworkElement)new CurveWindow { DataContext = curve }.Content, 1280, 760, "fereastra-curba");

            // R-130, R-131: the two windows of the Tonuri tab, built from the same result.
            MapViewModel map = vm.Tones.BuildMap();
            Render((FrameworkElement)new MapWindow { DataContext = map }.Content, name: "harta-praf-cer");
            Assert.False(map.HasWcs);   // a TIFF: no WCS, the labels box is disabled
            PreviewWindowViewModel preview = vm.Tones.BuildPreview(AnalysisRun.Execute(tif, Scenes.OnBuiltForPaper()).Simulation.Rows[3]);
            Render((FrameworkElement)new PreviewWindow { DataContext = preview }.Content, name: "preview-rand");

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

        Assert.Equal("PROBLEMA", vm.Report.VerdictLabel);
        Assert.Equal(6, vm.Report.Sections.Count);
        Assert.NotEmpty(vm.Preview.Tiles);
        Assert.Contains(vm.Preview.Tiles, t => t.Title.StartsWith("Simulare print"));
        Assert.Contains(vm.Preview.Tiles, t => t.Title.StartsWith("Decupaj"));
    }

    /// <summary>
    /// The error card, with the refusal's text - the state the user sees on a linear FITS.
    /// </summary>
    [Fact]
    public async Task R015_TheErrorCardRendersARefusal()
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
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
    }
    /// <summary>
    /// The help tooltip, with the theme's style and the real text. A tooltip is a popup, it
    /// does not show in the window's render - that is why the control is rendered directly:
    /// the width must be the theme's (not a one-metre row), and the text tall enough for
    /// its paragraphs. APA_DUMP_SCREENS saves it as "tooltip-distanta.png".
    /// </summary>
    [Fact]
    public void R026_TheHelpTooltipRendersAtTheThemeWidth()
        => _sta.Invoke(() =>
        {
            var tip = new ToolTip { Content = new ParameterFields().DistanceHelp };
            tip.Measure(new Size(1000, 1000));
            Size s = tip.DesiredSize;

            Assert.InRange(s.Width, 300, 420 + 2 * 8 + 2);   // MaxWidth + padding + border
            Assert.True(s.Height > 12 * 17, "the tooltip is too short for four paragraphs: " + s.Height);
            Render(tip, (int)Math.Ceiling(s.Width), (int)Math.Ceiling(s.Height), "tooltip-distanta");
        });
}
