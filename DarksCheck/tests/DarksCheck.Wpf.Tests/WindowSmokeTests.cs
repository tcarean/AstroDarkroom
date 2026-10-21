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
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DarksCheck.Tests;

namespace DarksCheck.Wpf.Tests;

/// <summary>
/// The window really loads and really draws. The view-model tests check C#; XAML is checked
/// only at run time: a misnamed resource, a missing converter or a binding to a property that
/// does not exist pass the compiler and show when the window loads. Each test builds the
/// element, lays it out, renders it to a bitmap and asserts that no binding error was traced.
/// With DARKSCHECK_DUMP_SCREENS=&lt;folder&gt; each render is also saved as a PNG, to look at
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

    private static void Render(FrameworkElement el, int w = 1360, int h = 820, string? name = null)
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
        string? dir = Environment.GetEnvironmentVariable("DARKSCHECK_DUMP_SCREENS");
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
    public async Task R071_EveryColumnHeaderOfTheTablesHasAHint()
    {
        await _sta.InvokeAsync(() =>
        {
            MainViewModel vm = new();
            foreach (FrameworkElement view in new FrameworkElement[] { new FramesView { DataContext = vm }, new LightsView { DataContext = vm } })
            {
                Render(view);
                Style head = (Style)view.FindResource("Head"), leftHead = (Style)view.FindResource("LeftHead");
                // the header TextBlocks: the two header styles, with a text (the table note's header is empty without a result)
                List<TextBlock> headers = Descendants(view).OfType<TextBlock>().Where(t => (t.Style == head || t.Style == leftHead) && !string.IsNullOrEmpty(t.Text)).ToList();
                Assert.True(headers.Count >= 10, $"{view.GetType().Name}: {headers.Count} headers");
                Assert.All(headers, t => Assert.False(string.IsNullOrWhiteSpace(t.ToolTip as string), $"{view.GetType().Name}: column '{t.Text}' has no hint"));
            }

            return Task.CompletedTask;
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject d)
    {
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(d, i);
            yield return child;
            foreach (DependencyObject grandchild in Descendants(child))
            {
                yield return grandchild;
            }
        }
    }

    [Fact]
    public void TheAboutWindowLoadsAndRenders()
        => _sta.Invoke(() =>
        {
            using BindingErrors errors = new();
            AboutWindow about = new();

            // The content, not the window: an unshown Window renders transparent, while the
            // root panel inherits the DataContext and goes through the same bindings.
            Render((FrameworkElement)about.Content, 600, 760, "about");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);

            BitmapSource logo = Assert.IsAssignableFrom<BitmapSource>(about.Logo.Source);
            Assert.Equal(256, logo.PixelWidth);
        });

    /// <summary>
    /// Every tab with a real result in it (R-071): an empty tab would render with half its
    /// templates wrong; only with data are all the bindings touched. The session has biases,
    /// lights and a partner, so every card and every chart has something to draw.
    /// </summary>
    [Fact]
    public async Task R071_EveryTabRendersWithRealResultsAndNoBindingErrors()
    {
        using TestSettings.Own _ = new();
        using SessionFixture f = WindowFixture.Session();
        using SessionFixture p = WindowFixture.Partner();
        MainViewModel vm = new() { Partner = p.Root };

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);
            Assert.True(vm.HasResult);
            Assert.NotNull(vm.Result!.Partner);

            using BindingErrors errors = new();
            Render((FrameworkElement)new MainWindow(vm).Content, name: "window");
            Render(new SummaryView { DataContext = vm }, h: 1800, name: "tab-summary");
            Render(new FramesView { DataContext = vm }, name: "tab-frames");
            Render(new MapsView { DataContext = vm }, name: "tab-maps");
            Render(new HotPixelsView { DataContext = vm }, name: "tab-hot");
            Render(new LightsView { DataContext = vm }, h: 1000, name: "tab-lights");
            Render(new IndexView { DataContext = vm }, name: "tab-index");
            Render(new ScaleView { DataContext = vm }, h: 1400, name: "tab-scale");

            // the emphasis draws a row's bold parts as bold runs (R-071, ADR 0016)
            TextBlock block = new();
            List<string> boldParts = ["1.43 ADU"];
            Emphasis.SetBold(block, boldParts);
            Emphasis.SetText(block, "30 cadre -> 1.43 ADU zgomot");
            // the rebuild is deferred to the end of the binding pass, so the two properties cost one rebuild
            block.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            Assert.Equal(3, block.Inlines.Count);
            Assert.Equal(FontWeights.Bold, ((System.Windows.Documents.Run)block.Inlines.ElementAt(1)).FontWeight);
            Assert.Equal("1.43 ADU", ((System.Windows.Documents.Run)block.Inlines.ElementAt(1)).Text);

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
    }

    /// <summary>A library without biases or lights: the maps say why, the lights tab has one row (R-071).</summary>
    [Fact]
    public async Task R071_ALibraryRendersWithTheEmptyMapsAndTheLightsNote()
    {
        using TestSettings.Own _ = new();
        using SessionFixture f = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice }, inSubfolder: false);
        MainViewModel vm = new();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);
            Assert.True(vm.HasResult);

            using BindingErrors errors = new();
            Render(new MapsView { DataContext = vm }, name: "tab-maps-library");
            Render(new LightsView { DataContext = vm }, name: "tab-lights-library");
            Render(new IndexView { DataContext = vm }, name: "tab-index-library");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
    }

    /// <summary>The problem card, when the folder cannot be analysed (R-070).</summary>
    [Fact]
    public async Task R070_TheProblemCardRendersForAFolderThatCannotBeAnalysed()
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

    /// <summary>The chart alone, with gaps (NaN), guides and a reference series: the drawing code has its own branches.</summary>
    [Fact]
    public void TheChartRendersWithGapsGuidesAndAReference()
        => _sta.Invoke(() =>
        {
            List<ChartPoint> pts = [new(1, 2.0, "1", VerdictPalette.Good), new(2, double.NaN, "2", VerdictPalette.Neutral), new(3, 3.5, "3", VerdictPalette.Bad, "tip")];
            List<ChartPoint> reference = [new(1, 2.2, "1", VerdictPalette.Neutral), new(3, 3.0, "3", VerdictPalette.Neutral)];
            List<GuideLine> guides = [new(3.0, "limit", VerdictPalette.Warn)];
            Render(new SeriesChart { Points = pts, Guides = guides, Reference = reference, Title = "ADU" }, 600, 200, "chart-line");
            Render(new SeriesChart { Points = pts, Guides = guides, Bars = true, Minimum = 0 }, 600, 200, "chart-bars");
            Render(new SeriesChart(), 600, 200);
            SeriesChart chart = new() { Points = pts };
            Render(chart, 600, 200);
            Assert.NotNull(chart.PointAt(44 + 2));
        });
}
