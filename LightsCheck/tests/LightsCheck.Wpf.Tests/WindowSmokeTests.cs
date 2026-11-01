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

using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightsCheck.Tests;

namespace LightsCheck.Wpf.Tests;

/// <summary>
/// The window really loads and really draws. The view-model tests check C#; XAML is checked
/// only at run time: a misnamed resource, a missing converter or a binding to a property that
/// does not exist pass the compiler and show when the window loads. Each test builds the
/// element, lays it out, renders it to a bitmap and asserts that no binding error was traced.
/// With LIGHTSCHECK_DUMP_SCREENS=&lt;folder&gt; each render is also saved as a PNG, to look at
/// the window without starting it; it is not a comparison against a reference.
/// </summary>
[Collection("window")]
public class WindowSmokeTests
{
    private readonly StaFixture _sta;

    public WindowSmokeTests(StaFixture sta) => _sta = sta;

    /// <summary>Catches the binding errors WPF otherwise writes only to the debugger's output.</summary>
    private sealed class BindingErrors : IDisposable
    {
        private readonly StringBuilder _sb = new();
        private readonly TraceListener _listener;
        private readonly SourceLevels _before;

        internal BindingErrors()
        {
            PresentationTraceSources.Refresh();
            _before = PresentationTraceSources.DataBindingSource.Switch.Level;
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            _listener = new TextWriterTraceListener(new StringWriter(_sb));
            PresentationTraceSources.DataBindingSource.Listeners.Add(_listener);
        }

        internal string Text
        {
            get
            {
                _listener.Flush();
                return _sb.ToString();
            }
        }

        public void Dispose()
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(_listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = _before;
            _listener.Dispose();
        }
    }

    internal static void Render(FrameworkElement el, int w = 1360, int h = 820, string? name = null)
    {
        el.Measure(new Size(w, h));
        el.Arrange(new Rect(0, 0, w, h));
        el.UpdateLayout();

        RenderTargetBitmap bmp = new(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(el);

        Assert.Equal(w, bmp.PixelWidth);
        Dump(bmp, name);
    }

    private static void Dump(RenderTargetBitmap bmp, string? name)
    {
        string? dir = Environment.GetEnvironmentVariable("LIGHTSCHECK_DUMP_SCREENS");
        if (string.IsNullOrEmpty(dir) || name is null)
        {
            return;
        }

        Directory.CreateDirectory(dir);
        PngBitmapEncoder enc = new();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using FileStream fs = File.Create(Path.Combine(dir, name + ".png"));
        enc.Save(fs);
    }

    /// <summary>The window before any folder: the empty state, nothing else.</summary>
    [Fact]
    public void TheMainWindowLoadsAndRendersEmpty()
        => _sta.Invoke(() =>
        {
            using BindingErrors errors = new();
            MainWindow w = new();
            Render((FrameworkElement)w.Content, name: "window-empty");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

    /// <summary>
    /// The "Despre" window: its own XAML, a template of the theme, the icon through a decoder
    /// and links bound to properties, all of which compile with a wrong name.
    /// </summary>
    [Fact]
    public void TheAboutWindowLoadsAndRenders()
        => _sta.Invoke(() =>
        {
            using BindingErrors errors = new();
            AboutWindow about = new(SystemParameters.WorkArea);

            // The content, not the window: an unshown Window renders transparent, while the
            // root panel inherits the DataContext and goes through the same bindings. 1000 DIP,
            // the whole content (978 with the six components of R-075), so the render shows no
            // scroll bar (R-076).
            Render((FrameworkElement)about.Content, 600, 1000, "about");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);

            BitmapSource logo = Assert.IsAssignableFrom<BitmapSource>(about.Logo.Source);
            Assert.Equal(256, logo.PixelWidth);
        });

    /// <summary>
    /// Every tab with a real result in it (R-030): an empty tab would render with half its
    /// templates wrong; only with data are all the bindings touched. The frames tab also with
    /// the star cut-outs of a selected frame (R-031).
    /// </summary>
    [Fact]
    public async Task R030_EveryTabRendersWithRealResultsAndNoBindingErrors()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2);
        MainViewModel vm = new();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);
            Assert.True(vm.HasResult);

            using BindingErrors errors = new();
            Render((FrameworkElement)new MainWindow(vm).Content, name: "window");
            Render(new SummaryView { DataContext = vm }, h: 1400, name: "tab-summary");
            Render(new FramesView { DataContext = vm }, name: "tab-frames-none");
            Render(new DitherView { DataContext = vm }, h: 1200, name: "tab-dither");
            Render(new QualityView { DataContext = vm }, h: 1200, name: "tab-quality");
            Render(new RejectedView { DataContext = vm }, name: "tab-rejected");

            vm.SelectedFrame = vm.Frames[2];
            await vm.CropsPending;
            Assert.NotEmpty(vm.Crops);
            Render(new FramesView { DataContext = vm }, name: "tab-frames");

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
    }

    /// <summary>The problem card, when the folder cannot be analysed (R-029).</summary>
    [Fact]
    public async Task R029_TheProblemCardRendersForAFolderThatCannotBeAnalysed()
    {
        using TempWorkspace ws = new();
        MainViewModel vm = new();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(Path.Combine(ws.Dir, "missing"));
            Assert.True(vm.HasProblem);

            using BindingErrors errors = new();
            Render((FrameworkElement)new MainWindow(vm).Content, name: "window-problem");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
    }

    /// <summary>The charts alone, with gaps (NaN) and guides: the drawing code has its own branches.</summary>
    [Fact]
    public void TheChartsRenderWithGapsAndGuides()
        => _sta.Invoke(() =>
        {
            List<ChartPoint> pts = [new(1, 2.0, "1", VerdictPalette.Good), new(2, double.NaN, "2", VerdictPalette.Neutral), new(3, 3.5, "3", VerdictPalette.Bad)];
            List<GuideLine> guides = [new(3.0, "limit", VerdictPalette.Warn)];
            Render(new SeriesChart { Points = pts, Guides = guides, Title = "px" }, 600, 200, "chart-line");
            Render(new SeriesChart { Points = pts, Guides = guides, Bars = true, Minimum = 0 }, 600, 200, "chart-bars");
            Render(new DitherMap { Points = pts, XLabel = "X", YLabel = "Y" }, 400, 400, "chart-map");
            Render(new SeriesChart(), 600, 200);
            Render(new DitherMap(), 400, 400);
        });
}
