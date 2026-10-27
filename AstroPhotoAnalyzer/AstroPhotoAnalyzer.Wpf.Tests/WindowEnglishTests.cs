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

// WindowEnglishTests.cs - the window in English (ADR 0048): every tab and every window of a
// real result is built in an English scope, laid out, and every text it shows - a TextBlock's
// text, a control's content, header and tooltip, each window's title, every busy message of the
// analysis, a parameter error - is searched for a Romanian word. A text left Romanian shows
// here, as the console's do in the English goldens.
//
// APA_DUMP_SCREENS=<folder> saves the renders as PNG, with "-en" in their names.

using AstroPhotoAnalyzer.Tests;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("fereastra")]
public class WindowEnglishTests
{
    readonly StaFixture _sta;

    public WindowEnglishTests(StaFixture sta) => _sta = sta;

    /// <summary>Lays the element out, renders it, and returns every text it shows.</summary>
    static string TextsOf(FrameworkElement el, string name, int w = 1400, int h = 860)
    {
        el.Measure(new Size(w, h));
        el.Arrange(new Rect(0, 0, w, h));
        el.UpdateLayout();

        string? dir = Environment.GetEnvironmentVariable("APA_DUMP_SCREENS");
        if (!string.IsNullOrEmpty(dir))
        {
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(el);
            Directory.CreateDirectory(dir);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var fs = File.Create(Path.Combine(dir, name + "-en.png"));
            enc.Save(fs);
        }

        var texts = new List<string>();
        Collect(el, texts);
        return string.Join("\n", texts);
    }

    static void Collect(DependencyObject d, List<string> texts)
    {
        switch (d)
        {
            case TextBlock tb:
                texts.Add(tb.Text);
                break;
            case HeaderedContentControl hc when hc.Header is string header:
                texts.Add(header);
                break;
        }
        if (d is ContentControl cc && cc.Content is string content) texts.Add(content);
        if (d is FrameworkElement fe && fe.ToolTip is string tip) texts.Add(tip);
        if (d is FrameworkElement el && el.ToolTip is ToolTip { Content: string tipText }) texts.Add(tipText);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            Collect(VisualTreeHelper.GetChild(d, i), texts);
    }

    /// <summary>
    /// A window's content texts and its title. A bound title is resolved only when the
    /// dispatcher gets to it, so the dispatcher runs its queue first.
    /// </summary>
    static async Task<string> TextsOfAsync(Window w, string name, int width = 1400, int height = 860)
    {
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ContextIdle);
        return w.Title + Environment.NewLine + TextsOf((FrameworkElement)w.Content, name, width, height);
    }

    static void AssertEnglish(string where, string texts)
    {
        List<string> found = RomanianWords.In(texts);
        Assert.True(found.Count == 0, $"{where} shows Romanian words: {string.Join(", ", found)}");
    }

    static string DarkTiff(TempWorkspace ws)
    {
        Raster r = Scenes.DarkSky();
        return ws.WriteFile("cer-intunecat.tif", TiffTestData.Build16Rgb(r.W, r.H,
            (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V2("sRGB test")));
    }

    [Fact]
    public async Task R153_EveryWindowTextIsEnglishWithoutLang()
    {
        using var ws = new TempWorkspace();
        string tif = DarkTiff(ws);
        ws.WriteFile("baryta.icc", IccPrinterTestData.V2Lut16("Zeta baryta", 20));
        ws.WriteFile("tipar.icc", IccPrinterTestData.V2Lut16("Tipar", 8, space: "CMYK"));

        await _sta.InvokeAsync(async () =>
        {
            using IDisposable english = Tr.Use(UiLanguage.English);
            AssertEnglish("the empty window", await TextsOfAsync(new MainWindow(), "fereastra-goala"));
            // The bar with a remembered profile (R-155, R-156), readable and not.
            string settingsPath = ws.PathTo("settings.json");
            File.WriteAllText(settingsPath, "{\"iccProfile\": " + System.Text.Json.JsonSerializer.Serialize(ws.PathTo("baryta.icc")) + "}");
            AssertEnglish("the window with a profile", await TextsOfAsync(new MainWindow(WindowSettings.Read(settingsPath)), "fereastra-profil"));
            File.WriteAllText(settingsPath, "{\"iccProfile\": " + System.Text.Json.JsonSerializer.Serialize(ws.PathTo("absent.icc")) + "}");
            AssertEnglish("the window with a missing profile", await TextsOfAsync(new MainWindow(WindowSettings.Read(settingsPath)), "fereastra-profil-lipsa"));
            var about = new AboutWindow();
            string aboutTexts = await TextsOfAsync(about, "despre", 600, 720);
            Assert.Equal("About AstroPhotoAnalyzer", about.Title);
            AssertEnglish("the About window", aboutTexts);
            AssertEnglish("the profile window", await TextsOfAsync(new IccPickerWindow { DataContext = new IccPickerViewModel(ws.Dir) }, "profil-icc", 720, 520));

            var vm = new MainViewModel();
            vm.Parameters.Distance = "abc";
            Assert.Null(vm.Parameters.TryBuild(out string fieldError));
            AssertEnglish("a parameter error", fieldError);

            vm.Parameters.Aspect = "50:35";
            vm.Parameters.Distance = "1.5";
            vm.Parameters.Dmax = "2.0";
            var busy = new List<string>();
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.BusyMessage) && vm.BusyMessage != null) busy.Add(vm.BusyMessage); };
            await vm.OpenAsync(tif);
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            Assert.True(vm.HasResult, vm.Error);
            Assert.Equal("PROBLEM", vm.Report.VerdictLabel);
            Assert.Contains("reading", busy);
            AssertEnglish("the busy messages", string.Join(Environment.NewLine, busy));

            AssertEnglish("the window", await TextsOfAsync(new MainWindow { DataContext = vm }, "fereastra"));
            vm.Report.ShowText = true;
            AssertEnglish("Report", TextsOf(new ReportView { DataContext = vm.Report }, "tab-raport", h: 2200));
            AssertEnglish("Tones", TextsOf(new TonesView { DataContext = vm.Tones }, "tab-tonuri", h: 2000));
            AssertEnglish("Export", TextsOf(new ExportView { DataContext = vm.Export }, "tab-export"));
            AssertEnglish("Resolution", TextsOf(new ResolutionView { DataContext = vm.Resolution }, "tab-rezolutie"));
            AssertEnglish("Colour", TextsOf(new BackgroundView { DataContext = vm.Background }, "tab-fundal"));
            AssertEnglish("Preview", TextsOf(new PreviewView { DataContext = vm.Preview }, "tab-preview", h: 2400));
            await vm.CurvesComputation;   // R-146: the gallery comes with the analysis
            Assert.True(vm.Curves.HasGallery, vm.Curves.Message);
            vm.Curves.Next.Execute(null);
            AssertEnglish("Curves", TextsOf(new CurvesView { DataContext = vm.Curves }, "tab-curbe"));
            CurveWindowViewModel? curve = null;
            vm.Curves.CurveRequested += c => curve = c;
            vm.Curves.OpenCurve.Execute(null);
            AssertEnglish("the curve window", await TextsOfAsync(new CurveWindow { DataContext = curve }, "fereastra-curba", 1280, 760));
            var mapWindow = new MapWindow { DataContext = vm.Tones.BuildMap() };
            string mapTexts = await TextsOfAsync(mapWindow, "harta-praf-cer");
            Assert.StartsWith("Dust / sky map", mapWindow.Title);
            AssertEnglish("the map", mapTexts);
            PreviewWindowViewModel preview = vm.Tones.BuildPreview(AnalysisRun.Execute(tif, Scenes.OnBuiltForPaper()).Simulation.Rows[3]);
            AssertEnglish("the preview window", await TextsOfAsync(new PreviewWindow { DataContext = preview }, "preview-rand"));

            var fields = new ParameterFields();
            AssertEnglish("the help texts", string.Join("\n", fields.DistanceHelp, fields.AspectHelp, fields.TargetBgHelp,
                fields.WcsHelp, fields.PassepartoutHelp, fields.FrameHelp));
        });
    }
}
