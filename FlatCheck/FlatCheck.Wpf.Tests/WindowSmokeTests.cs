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
// WHY IT EXISTS. The other tests check view-models, which are ordinary C#. The XAML is not
// checked that way: a resource with a wrong name, a missing converter or a binding to a
// property that does not exist get past the compiler and show only at run time - and some,
// at LOADING, that is, every time.
//
// The test was written after the window crashed at start, always, with "Cannot find
// non-neutral culture related to 'en-us'": the project had inherited <InvariantGlobalization>
// from the console application, where it is right, and WPF cannot bind anything without
// culture data. The build passed, all hundred tests passed, and the application did not
// start. Now it would fail here.
//
// What is checked: the window is built, laid out, rendered into a bitmap, and produces no
// binding error on the way; for the Structure tab's message without Adobe (R-113), also that it
// is on screen in its red above the rows or the gap, hidden with Adobe, and that its link leaves
// the tab; for "Despre" (R-095), that its link to the notices is there, showing its address,
// under the lead and above the list. What is not checked: how it looks.

using FlatCheck.Tests;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace FlatCheck.Wpf.Tests;

[Collection("window")]
public class WindowSmokeTests
{
    readonly StaFixture _sta;

    public WindowSmokeTests(StaFixture sta) => _sta = sta;

    /// <summary>
    /// Catches the binding errors, which otherwise drain silently into the debugger's Output
    /// window and are never seen from a test.
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
    /// Renders an element into a bitmap - it forces the whole chain of templates.
    ///
    /// With FLATCHECK_DUMP_SCREENS=&lt;folder&gt; it also saves a PNG. It is not a comparison
    /// with a reference and must not become one: how a window looks is the eye's business.
    /// It is only how you look at it without starting the application - for example when the
    /// Windows session is locked and a screenshot would catch the lock screen.
    /// </summary>
    internal static void Render(FrameworkElement el, int w = 1360, int h = 800, string? name = null)
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

    // ------------------------------------------------------------------ the tests

    /// <summary>The case that crashed: the window did not even get to show.</summary>
    [Fact]
    public void TheMainWindowLoadsAndRenders()
        => _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();
            Render(new MainWindow(), 1360, 820);
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

    /// <summary>
    /// The "Despre" window: new XAML, a new template in the theme, an icon resource read
    /// through the decoder and links bound to properties - all things that compile with the
    /// wrong name and fail only when shown.
    /// </summary>
    [Fact]
    public Task TheAboutWindowLoadsAndRenders()
        => _sta.InvokeAsync(async () =>
        {
            using var errors = new BindingErrors();
            var about = new AboutWindow(SystemParameters.WorkArea);
            // A window never shown resolves its bindings and styles in a later dispatcher pass;
            // measured before it, the content comes out short (397 DIP instead of 880 on
            // 2026-11-01).
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ContextIdle);

            // The CONTENT is rendered, not the window: a Window not shown renders
            // transparent (that is how "window" below comes out too), and the root panel
            // inherits the DataContext and goes through the same bindings. At its own height,
            // so that the render shows the whole window and no scroll bar (R-125).
            var content = (FrameworkElement)about.Content;
            content.Measure(new Size(600, double.PositiveInfinity));
            Render(content, 600, (int)Math.Ceiling(content.DesiredSize.Height), "about");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);

            // the icon is the large frame, not the 16 px one scaled
            var logo = Assert.IsAssignableFrom<BitmapSource>(about.Logo.Source);
            Assert.Equal(256, logo.PixelWidth);
        });

    /// <summary>
    /// Where each licence's whole text is online is a link in the window, under the lead that
    /// says so and above the components (R-095): the own release carries no notices file, so
    /// without this link its reader would have no copy to go to. The view-model's address alone
    /// would pass with the link gone from the XAML.
    /// </summary>
    [Fact]
    public Task R095_TheAboutWindowLinksTheNoticesUnderTheLead()
        => _sta.InvokeAsync(async () =>
        {
            var about = new AboutWindow(SystemParameters.WorkArea);
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ContextIdle);
            var content = (FrameworkElement)about.Content;
            content.Measure(new Size(600, double.PositiveInfinity));
            Render(content, 600, (int)Math.Ceiling(content.DesiredSize.Height));
            var vm = (AboutViewModel)about.DataContext;

            Hyperlink link = Assert.Single(Descendants(content).OfType<TextBlock>().SelectMany(t => t.Inlines.OfType<Hyperlink>()),
                                           h => h.NavigateUri.AbsoluteUri == ThirdParty.NoticesUrl);
            Assert.Equal(ThirdParty.NoticesUrl, string.Concat(link.Inlines.OfType<Run>().Select(r => r.Text)));
            var holder = (TextBlock)link.Parent;
            TextBlock lead = Assert.Single(Descendants(content).OfType<TextBlock>(), t => t.Text == vm.ThirdPartyLead);
            // A row's name is a Run bound in the row template, which TextBlock.Text does not hold.
            TextBlock firstRow = Descendants(content).OfType<TextBlock>().First(t => t.Inlines.OfType<Run>().Any(r => r.Text == vm.Components[0].Name));
            Assert.True(holder.ActualHeight > 0);
            Assert.True(lead.TranslatePoint(new Point(), content).Y < holder.TranslatePoint(new Point(), content).Y);
            Assert.True(holder.TranslatePoint(new Point(), content).Y < firstRow.TranslatePoint(new Point(), content).Y);
            about.Close();
        });

    /// <summary>
    /// The window and its six tabs: Structura and End to end with real results in
    /// them, the others in their opening state (their results are rendered by the tests
    /// below). An empty tab would render even with half of its templates wrong - only with
    /// data are all the bindings reached.
    /// </summary>
    [Fact]
    public async Task EveryTabRendersWithRealResultsAndNoBindingErrors()
    {
        using var f = new SessionFixture()
            .WithFlatMaster()
            .WithLightSeries(6)
            .WithRawFlats("DSC_0101.NEF", "DSC_0102.NEF");

        var vm = new MainViewModel();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);

            using var errors = new BindingErrors();

            Render(new MainWindow { DataContext = vm }, 1360, 820, "window");
            Render(new StructureView { DataContext = vm.Structure }, name: "tab-structure");
            Render(new LightView { DataContext = vm.Light }, name: "tab-end-to-end");
            Render(new ExposureView { DataContext = vm.Exposure }, name: "tab-exposure");
            Render(new HistEqView { DataContext = vm.HistEq }, name: "tab-histeq-empty");
            await vm.Synthetic.Pending;
            Render(new SyntheticView { DataContext = vm.Synthetic }, name: "tab-synthetic-empty");
            // Split starts empty, with the frames counted, the chips and the script - the
            // state at opening; its result is rendered in the dedicated test.
            Render(new SplitView { DataContext = vm.Split }, name: "tab-split-empty");

            // The map guide, expanded. Collapsed, its bindings inside the card are never
            // evaluated - and a binding that fails only when shown is exactly what this
            // suite exists to catch.
            vm.Structure.MapGuide.IsExpanded = true;
            vm.Structure.HowToRead.IsExpanded = true;
            Render(new StructureView { DataContext = vm.Structure }, name: "tab-structure-guide");

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

        // and they really had something to render
        Assert.True(vm.Structure.HasResult);
        Assert.True(vm.Light.HasResult);
        Assert.Equal(2, vm.Exposure.Files.Count);
        Assert.Equal(3, vm.HistEq.Files.Count);    // master, two frames
    }

    /// <summary>
    /// The Split tab with a result: the header with a variable number of columns, the cells
    /// with pills, the maps side by side. The result comes through Populate, from SplitRun on
    /// generated masters, without Siril.
    /// </summary>
    [Fact]
    public void TheSplitTabRendersTheTableAndTheMaps()
    {
        using var f = new SessionFixture()
            .WithFlatMaster()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        SplitResult r = SplitRun.Analyze(f.Session.FlatMasterPath,
            new[] { f.MasterPath("pp_flat_stacked_A.fit"), f.MasterPath("pp_flat_stacked_B.fit") },
            null, new DateTime(2026, 9, 28, 10, 31, 0), 10);

        _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();
            var vm = new SplitTabViewModel();
            vm.Populate(r);
            Assert.True(vm.HasResult);
            Assert.Equal(3, vm.Maps.Count);
            Render(new SplitView { DataContext = vm }, name: "tab-split");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
    }

    /// <summary>
    /// The structure tab with the vignetting comparison against the lens profiles - the rows
    /// per profile, the pills, the verdict's detail - and in the state where it could not
    /// compare. The result comes through Populate, on hand-made profile sources.
    /// </summary>
    [Fact]
    public void TheStructureTabRendersTheProfileComparisonAndTheGap()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var r = StructureAnalyzer.Analyze(TestImages.From(256, 256, FitsTestData.Radial(256, 256)));
        var master = new LoadedFrame { Header = new Dictionary<string, string> { ["SIMPLE"] = "T" }, FocalMm = 20, Camera = "Nikon D3300" };
        var raw = new LoadedFrame
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
        StructureOutcome compared = StructureRun.Compare(r, master, new StructureQuery { Sources = sources, RawPath = "x.NEF", LoadInfo = _ => raw });
        StructureOutcome gap = StructureRun.Compare(r, master, new StructureQuery { Sources = sources });

        _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();

            var vm = new StructureTabViewModel();
            vm.Populate(compared);
            Render(new StructureView { DataContext = vm }, h: 1800, name: "tab-structure-profile");
            Assert.True(vm.HasProfileRows);

            vm.Populate(new StructureOutcome(r, null, null, null), gap);
            Render(new StructureView { DataContext = vm }, h: 1800, name: "tab-structure-without-raw");
            Assert.False(vm.HasProfileRows);
            Assert.NotEmpty(vm.ProfileGap);

            // R-118: the "no filters" box unchecked - the master's own depth, the paragraph
            // saying why nothing is compared, no row and no gap.
            vm.NoFilters = false;
            vm.Populate(new StructureOutcome(r, null, null, null));
            Render(new StructureView { DataContext = vm }, h: 1800, name: "tab-structure-filter");
            Assert.False(vm.HasProfileRows);
            Assert.Empty(vm.ProfileGap);
            Assert.NotEmpty(vm.ProfileMaster);
            Assert.NotEmpty(vm.FilteredNote);

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
    }

    /// <summary>
    /// R-126: the one scale on screen. Compared, every interval in stops is drawn once - in the
    /// table under the card, below the card's last line, not in the card - on the same line as
    /// its percent and to its right, under a column heading; the cells marked are exactly the two
    /// verdicts'; the sentence on the stops is drawn once, under the table; the paragraph on why
    /// the residual matters stands under the five tests, above the vignetting card, not under the
    /// scale. With nothing compared the stops heading and every stops cell take no width, and
    /// the percent cell keeps its mark. The result comes through Populate, on hand-made profile
    /// sources.
    /// </summary>
    [Fact]
    public void R126_TheOneScaleDrawsTheStopsBesideThePercentOnlyWhenCompared()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var r = StructureAnalyzer.Analyze(TestImages.From(256, 256, FitsTestData.Radial(256, 256)));
        var master = new LoadedFrame { Header = new Dictionary<string, string> { ["SIMPLE"] = "T" }, FocalMm = 20, Camera = "Nikon D3300" };
        var raw = new LoadedFrame
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
        StructureOutcome compared = StructureRun.Compare(r, master, new StructureQuery { Sources = sources, RawPath = "x.NEF", LoadInfo = _ => raw });

        _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();

            var vm = new StructureTabViewModel();
            vm.Populate(compared);
            var view = new StructureView { DataContext = vm };
            Render(view, h: 1800);
            List<TextBlock> texts = Descendants(view).OfType<TextBlock>().ToList();

            TextBlock heading = Assert.Single(texts, t => t.Text == "adancimea fata de profil");
            TextBlock percentHeading = Assert.Single(texts, t => t.Text == "cele cinci teste");
            Assert.True(heading.ActualWidth > 0);
            TextBlock cardLast = Assert.Single(texts, t => t.Text == vm.ProfileNote);   // the card's last line
            double cardBottom = cardLast.TranslatePoint(new Point(), view).Y + cardLast.ActualHeight;
            Assert.Contains(vm.Scale, s => s.IsCurrent);
            Assert.Contains(vm.Scale, s => s.IsDepthCurrent);
            double lastRow = 0;
            foreach (ScaleRow row in vm.Scale)
            {
                TextBlock percent = Assert.Single(texts, t => t.Text == row.Range);
                TextBlock stops = Assert.Single(texts, t => t.Text == row.DepthRange);   // once: the card has no scale
                Point p = percent.TranslatePoint(new Point(), view), s = stops.TranslatePoint(new Point(), view);
                Assert.True(s.X > p.X, row.Band);
                Assert.True(Math.Abs(s.Y - p.Y) < 1, row.Band);
                Assert.True(s.Y > cardBottom, row.Band);
                Assert.True(Math.Abs(heading.TranslatePoint(new Point(), view).X - s.X) < 1, row.Band);
                Assert.True(Math.Abs(percentHeading.TranslatePoint(new Point(), view).X - p.X) < 1, row.Band);
                Assert.Equal(row.IsCurrent, Marked(percent));
                Assert.Equal(row.IsDepthCurrent, Marked(stops));
                lastRow = Math.Max(lastRow, s.Y);
            }
            TextBlock stopsNote = Assert.Single(texts, t => t.Text == vm.ScaleDepthNote);   // once: not in the card
            Assert.True(stopsNote.TranslatePoint(new Point(), view).Y > lastRow);

            // Why the residual matters: under the five tests' rows, before the vignetting card.
            double why = Assert.Single(texts, t => t.Text == vm.WhyResidual).TranslatePoint(new Point(), view).Y;
            double lastTest = texts.Where(t => vm.Tests.Any(x => x.Label == t.Text)).Max(t => t.TranslatePoint(new Point(), view).Y);
            double profiles = Assert.Single(texts, t => t.Text == StructureReport.ProfilesHeading).TranslatePoint(new Point(), view).Y;
            Assert.True(why > lastTest && why < profiles, $"paragraph at {why}, last test at {lastTest}, profiles card at {profiles}");

            vm.Populate(new StructureOutcome(r, null, null, null));
            Render(view, h: 1800);
            texts = Descendants(view).OfType<TextBlock>().ToList();

            Assert.Equal(0, Assert.Single(texts, t => t.Text == "adancimea fata de profil").ActualWidth);
            foreach (ScaleRow row in vm.Scale)
            {
                TextBlock percent = Assert.Single(texts, t => t.Text == row.Range);
                Assert.Equal(row.IsCurrent, Marked(percent));
                // The stops cell beside it takes no width: no empty padded cell is left behind.
                var line = (Grid)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(percent));
                Border stopsCell = Assert.Single(line.Children.OfType<Border>(), b => Grid.GetColumn(b) == 2);
                Assert.Equal(0, stopsCell.ActualWidth);
            }

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
    }

    /// <summary>Whether a cell of the verdict scale is drawn marked: its text sits in a border
    /// with a visible background.</summary>
    static bool Marked(TextBlock cell)
        => VisualTreeHelper.GetParent(cell) is Border b && b.Background is SolidColorBrush s && s.Color.A > 0;

    /// <summary>
    /// R-121: the structure tab on a raw that gives only a range - the prompt and the picker
    /// over the rows - first with no choice (both rows), then with the Samyang chosen (its row
    /// alone, the picker on it). The result comes through Populate, on hand-made sources.
    /// </summary>
    [Fact]
    public void R121_TheStructureTabRendersTheLensPicker()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, rangePair: true);
        var r = StructureAnalyzer.Analyze(TestImages.From(256, 256, FitsTestData.Radial(256, 256)));
        var master = new LoadedFrame { Header = new Dictionary<string, string> { ["SIMPLE"] = "T" }, FocalMm = 135, Camera = "Nikon D3300" };
        LoadedFrame raw = LensProfileTestData.Range135Raw();
        string key = LensProfileTestData.KeyOf(sources, LensProfileTestData.Samyang135);
        StructureOutcome open = StructureRun.Compare(r, master, new StructureQuery { Sources = sources, RawPath = "x.NEF", LoadInfo = _ => raw });
        StructureOutcome chosen = StructureRun.Compare(r, master, new StructureQuery { Sources = sources, RawPath = "x.NEF", LoadInfo = _ => raw, LensKey = key });

        _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();

            var vm = new StructureTabViewModel();
            vm.Populate(open);
            Render(new StructureView { DataContext = vm }, h: 1800, name: "tab-structure-lens-open");
            Assert.True(vm.OffersLensChoice);
            Assert.Equal(2, vm.ProfileRows.Count);
            Assert.Null(vm.SelectedLens.Key);

            vm.Populate(chosen);
            Render(new StructureView { DataContext = vm }, h: 1800, name: "tab-structure-lens-chosen");
            Assert.True(vm.OffersLensChoice);
            Assert.Single(vm.ProfileRows);
            Assert.Equal(key, vm.SelectedLens.Key);

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
    }

    /// <summary>
    /// The opening panel (R-120): the window while the opening runs, held at the catalog so
    /// the structure and the prefill rows say "running" and the series row "waiting"; the tabs
    /// are collapsed meanwhile and the panel is up. Then, once everything has ended, the panel
    /// is gone and the tabs are back.
    /// </summary>
    [Fact]
    public async Task R120_TheOpeningPanelRendersWhileTheOpeningRuns()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0101.NEF");
        using var ws = new TempWorkspace();
        using var hold = new ManualResetEventSlim(false);
        var sources = LensProfileTestData.Sources(ws);
        var raw = new LoadedFrame { Header = new Dictionary<string, string>(), FocalMm = 20, FNumber = 2.8, Camera = "NIKON D3300", LensId = 139, LensMinFocalMm = 11, LensMaxFocalMm = 20, LensMaxAperture = 2.8 };
        var vm = new MainViewModel(null, null, null,
            locateProfiles: () => { hold.Wait(); return sources; },
            loadInfo: path => RawReader.IsRawPath(path) ? raw : ImageLoader.LoadInfo(path));

        Task open = vm.OpenAsync(f.Root);   // waits at the catalog
        Assert.True(vm.IsRunning);
        Assert.True(vm.OpeningSteps[0].IsRunning);

        _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();
            // The window's content, as the problem-screen render does: a Window itself draws
            // nothing into a bitmap.
            var window = new MainWindow { DataContext = vm };
            Render((FrameworkElement)window.Content, 1360, 820, "opening");
            Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("Opening")).Visibility);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("Tabs")).Visibility);
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

        hold.Set();
        await open;
        Assert.False(vm.IsRunning);

        _sta.Invoke(() =>
        {
            var window = new MainWindow { DataContext = vm };
            Render((FrameworkElement)window.Content, 1360, 820);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("Opening")).Visibility);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("Tabs")).Visibility);
        });
    }

    /// <summary>
    /// The structure tab on a computer without Adobe software (R-113): the red paragraph, the
    /// instructions, the link to the DNG Converter and the folders searched, above the lensfun
    /// rows, and above the gap when there is no profile at all; with Adobe's files the block is
    /// collapsed, instructions and link included. The result comes through Populate, with the
    /// sources the run searched.
    /// </summary>
    [Fact]
    public void R113_TheAdobeBlockRendersAboveTheRowsOrTheGapAndHidesWithAdobe()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, withAdobe: false);
        var none = ProfileLocator.Locate(new[] { Path.Combine(ws.Dir, "no-adobe") }, Path.Combine(ws.Dir, "no-lensfun"));
        var withAdobe = LensProfileTestData.Sources(ws);
        var r = StructureAnalyzer.Analyze(TestImages.From(256, 256, FitsTestData.Radial(256, 256)));
        var master = new LoadedFrame { Header = new Dictionary<string, string> { ["SIMPLE"] = "T" }, FocalMm = 20, Camera = "Nikon D3300" };
        var raw = new LoadedFrame
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
        StructureOutcome compared = StructureRun.Compare(r, master, new StructureQuery { Sources = sources, RawPath = "x.NEF", LoadInfo = _ => raw });
        StructureOutcome noProfile = StructureRun.Compare(r, master, new StructureQuery { Sources = none, RawPath = "x.NEF", LoadInfo = _ => raw });
        StructureOutcome adobe = StructureRun.Compare(r, master, new StructureQuery { Sources = withAdobe, RawPath = "x.NEF", LoadInfo = _ => raw });

        _sta.Invoke(() =>
        {
            using var errors = new BindingErrors();

            var vm = new StructureTabViewModel();
            vm.Populate(compared, null, sources);
            var view = new StructureView { DataContext = vm };
            Render(view, h: 1800, name: "tab-structure-without-adobe");
            Assert.NotEmpty(vm.AdobeMissing);
            Assert.True(vm.HasProfileRows);

            // On screen, in the window's red for a message, above the first profile row.
            TextBlock red = Assert.Single(Descendants(view).OfType<TextBlock>(), t => t.Text == vm.AdobeMissing);
            Assert.Equal(Visibility.Visible, ((UIElement)VisualTreeHelper.GetParent(red)).Visibility);
            Assert.True(red.ActualHeight > 0);
            Assert.Equal(Color.FromRgb(0xF0, 0x85, 0x7E), ((SolidColorBrush)red.Foreground).Color);
            TextBlock firstRow = Descendants(view).OfType<TextBlock>().First(t => t.Text == vm.ProfileRows[0].Lens);
            Assert.True(red.TranslatePoint(new Point(), view).Y < firstRow.TranslatePoint(new Point(), view).Y);

            // No profile at all: above the gap that says so.
            vm.Populate(noProfile, null, none);
            Render(view, h: 1800, name: "tab-structure-without-profiles");
            Assert.NotEmpty(vm.ProfileGap);
            red = Assert.Single(Descendants(view).OfType<TextBlock>(), t => t.Text == vm.AdobeMissing);
            TextBlock gap = Assert.Single(Descendants(view).OfType<TextBlock>(), t => t.Text == vm.ProfileGap);
            Assert.True(red.ActualHeight > 0);
            Assert.True(red.TranslatePoint(new Point(), view).Y < gap.TranslatePoint(new Point(), view).Y);

            // With Adobe's files the whole block is collapsed, the constant instructions too.
            vm.Populate(adobe, null, withAdobe);
            Render(view, h: 1800);
            Assert.Empty(vm.AdobeMissing);
            TextBlock install = Assert.Single(Descendants(view).OfType<TextBlock>(), t => t.Text == vm.AdobeInstall);
            Assert.Equal(Visibility.Collapsed, ((UIElement)VisualTreeHelper.GetParent(install)).Visibility);

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
    }

    /// <summary>
    /// The card's link carries Adobe's DNG Converter page, and its RequestNavigate leaves the
    /// tab unhandled, so it reaches the window around it (R-113). A stand-in root catches it
    /// here: MainWindow's own handler would open the browser.
    /// </summary>
    [Fact]
    public void R113_TheCardsLinkCarriesAdobesPageOutOfTheTab()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, withAdobe: false);
        var r = StructureAnalyzer.Analyze(TestImages.From(256, 256, FitsTestData.Radial(256, 256)));
        StructureOutcome plain = new(r, null, null, null);

        _sta.Invoke(() =>
        {
            var vm = new StructureTabViewModel();
            vm.Populate(plain, null, sources);
            var view = new StructureView { DataContext = vm };
            var root = new Border { Child = view };
            Uri? reached = null;
            root.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler((_, e) => { reached = e.Uri; e.Handled = true; }));
            Render(root, h: 1800);

            Hyperlink link = Assert.Single(Descendants(view).OfType<TextBlock>().SelectMany(t => t.Inlines.OfType<Hyperlink>()));
            Assert.Equal(StructureReport.AdobeDngConverterUrl, link.NavigateUri.AbsoluteUri);
            link.RaiseEvent(new RequestNavigateEventArgs(link.NavigateUri, null) { RoutedEvent = Hyperlink.RequestNavigateEvent, Source = link });
            Assert.Equal(link.NavigateUri, reached);
        });
    }

    /// <summary>
    /// The main window catches the links its tabs raise and opens them through ExternalLink
    /// (R-113). A text check: raising the event in a real window would start the browser.
    /// </summary>
    [Fact]
    public void R113_TheMainWindowOpensTheLinksItsTabsRaise()
    {
        string dir = Path.Combine(RepoDir(), "FlatCheck.Wpf");

        Assert.Contains("Hyperlink.RequestNavigate=\"OpenLink\"", File.ReadAllText(Path.Combine(dir, "MainWindow.xaml")));
        Assert.Contains("RequestNavigateEventArgs e) => ExternalLink.Open(e);", File.ReadAllText(Path.Combine(dir, "MainWindow.xaml.cs")));
    }

    /// <summary>The FlatCheck folder, two levels above this file, as in IconTests.</summary>
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    /// <summary>Every element under <paramref name="node"/> in the visual tree, depth
    /// first.</summary>
    static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(node, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    /// <summary>
    /// The synthetic tab with a written flat and a comparison, from hand-made profiles and an
    /// analyzer result - no file, no Adobe on the machine. Both cards visible, the map on the
    /// right, the sources panel expanded, and no binding error.
    /// </summary>
    [Fact]
    public async Task TheSyntheticTabRendersAResultAndAComparison()
    {
        using var ws = new TempWorkspace();
        using var f = new SessionFixture().WithFlatMaster();
        var sources = LensProfileTestData.Sources(ws, tokinaOnTwoFormats: true);
        var adobe = sources.Catalog.Lenses.First(l => l.Source == ProfileSource.Adobe && l.Mount.StartsWith("NIKON"));
        var profile = sources.Catalog.ProfilesOf(adobe).First(p => p.FocalMm == 20 && Math.Abs(p.FStop - 2.8) < 0.01);
        var built = SyntheticFlatBuilder.Build(profile, 600, 400, 0);
        var against = AgainstAnalyzer.Analyze(built.Flat, built.Flat);

        await _sta.InvokeAsync(async () =>
        {
            await Task.CompletedTask;
            using var errors = new BindingErrors();
            var vm = new SyntheticTabViewModel(_ => true, () => sources);
            // the list attached to a session, short enough for the rows' second line (focals,
            // apertures, distances) - the render is the only thing that looks at that line
            vm.Attach(f.Session, CancellationToken.None);
            await vm.Pending;
            await vm.DetailsPending;
            Assert.All(vm.Lenses, r => Assert.True(r.HasDetails));
            vm.Populate(built, new SyntheticRequest { FocalMm = 20, FocalSource = "FOCALLEN", FStop = 2.8, FStopSource = "--fstop" });
            vm.PopulateCompare(against);
            vm.Guide.IsExpanded = true;
            Render(new SyntheticView { DataContext = vm }, h: 1600, name: "tab-synthetic");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
            Assert.True(vm.HasResult);
            Assert.True(vm.HasCompare);
            Assert.Equal(4, vm.Depth.Count);
            Assert.Equal("PERFECT", vm.CompareVerdict);
            Assert.NotNull(vm.CompareMap);
        });
    }

    /// <summary>
    /// The histogram tab WITH a result: the view chips, the picture, the master note, the
    /// guide expanded, then a per-channel view and a mono result (without chips). The result
    /// comes straight from the analyzer, as for the preview on the exposure tab.
    /// </summary>
    [Fact]
    public void TheHistEqTabRendersAResult()
        => _sta.Invoke(() =>
        {
            // A mosaic on a radial field, not FlatCfa: that one is constant per channel and
            // equalises into a uniform white, which is right and shows nothing of it.
            var cfa = HistEqAnalyzer.Analyze(
                FlatCheck.Tests.TestImages.From(128, 128, FlatCheck.Tests.FitsTestData.WithBayerMosaic(
                    FlatCheck.Tests.FitsTestData.Radial(128, 128, center: 34000))),
                CfaPattern.Parse("RGGB", "BAYERPAT"));
            var vm = new HistEqTabViewModel();
            vm.Populate(cfa, HistEqKind.Master);

            using var errors = new BindingErrors();
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq");

            vm.Guide.IsExpanded = true;
            Render(new HistEqView { DataContext = vm }, h: 1600, name: "tab-histeq-guide");

            vm.View.Options.First(o => o.Label == "G1").IsSelected = true;
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq-channel");

            // the false colours: the chip on, the legend under the picture (R-104)
            vm.View.Rainbow = true;
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq-rainbow");
            Assert.NotEqual("", vm.Legend);
            vm.View.Rainbow = false;

            // the zoom: 100 % and a large factor, with the panning control in play
            vm.Zoom.Actual.Execute(null);
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq-100");
            vm.Zoom.Manual(3);
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq-zoom");
            Assert.Equal(3, vm.Zoom.EffectiveZoom, 9);

            // mono, and as the master: the note under the picture is the master's
            var mono = HistEqAnalyzer.Analyze(
                FlatCheck.Tests.TestImages.From(128, 128, FlatCheck.Tests.FitsTestData.Radial(128, 128)), null);
            vm.Populate(mono, HistEqKind.Master);
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq-mono");
            vm.View.Rainbow = true;                   // the chip alone, without the view row
            Render(new HistEqView { DataContext = vm }, name: "tab-histeq-mono-rainbow");
            vm.View.Rainbow = false;

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
            Assert.True(vm.HasResult);
            Assert.False(vm.View.IsVisible);
            Assert.Equal(HistEqReport.MasterVersusFrame(), vm.MasterNote);
            // a new file: back to fit, and the control wrote the effective factor below 1
            Assert.True(vm.Zoom.IsFit);
            Assert.InRange(vm.Zoom.EffectiveZoom, 0.01, 1.0);
        });

    /// <summary>
    /// The problem screen has its own bindings, and it is the very screen someone sees who
    /// opens the application on a folder where the Siril script has not run yet - that is,
    /// the first screen of the first wrong use.
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
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

        Assert.True(vm.HasProblem);
    }

    /// <summary>
    /// The problem screen with the run block in all its states: it can be pressed (with the
    /// warning that Siril is open), blocked by a missing folder, blocked without Siril. The
    /// installation is fake, so that the render does not depend on the machine.
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
            // the content, not the window: a Window not shown renders transparent
            Render((FrameworkElement)new MainWindow { DataContext = vm }.Content, 1360, 820, "problem-blocked");

            Directory.CreateDirectory(Path.Combine(f.Root, "lights"));
            File.WriteAllText(Path.Combine(f.Root, "lights", "a.NEF"), "x");
            vm.Preprocess.Refresh();
            Render((FrameworkElement)new MainWindow { DataContext = vm }.Content, 1360, 820, "problem-running");

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

        Assert.True(vm.Preprocess.CanRun);
        Assert.True(vm.Preprocess.HasWarning);
    }

    /// <summary>
    /// The run window: during the run (the bar, the current command, the log), then
    /// succeeded, then failed. The lines are the ones recorded from siril-cli, through a fake
    /// runner.
    /// </summary>
    [Fact]
    public async Task TheRunWindowRendersRunningAndFinished()
        => await _sta.InvokeAsync(async () =>
        {
            using var errors = new BindingErrors();

            var running = new SirilRunViewModel(@"E:\sesiune", @"C:\scripturi\OSC_Preprocessing_WithoutDark.ssf",
                                                SirilInstallTests.WithoutDark)
            { Warning = SessionMessages.SirilGuiRunning() };
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
            Render((FrameworkElement)window.Content, 900, 600, "siril-running");
            Assert.True(running.IsRunning);

            gate.SetResult(0);
            await run;
            Render((FrameworkElement)window.Content, 900, 600, "siril-succeeded");
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
            Render((FrameworkElement)failedWindow.Content, 900, 600, "siril-failed");
            Assert.True(failed.IsFailure);

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

    /// <summary>
    /// The field guard's refusal: another path through the templates - no map, no figures
    /// panel, with the neutral pill - so it deserves its own render.
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
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

        Assert.True(vm.Light.Refused);
    }

    /// <summary>
    /// The exposure tab WITH a result: another path through the templates than the empty one
    /// - the levels panel, the list of CFA channels, the verdict, and the four histogram
    /// drawings.
    ///
    /// The guide is rendered expanded too, for the same reason as the map guide: collapsed,
    /// its bindings inside the card are never evaluated.
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
            await vm.Pending;

            using var errors = new BindingErrors();
            Render(new ExposureView { DataContext = vm }, name: "tab-exposure-result");

            vm.HistogramGuide.IsExpanded = true;
            Render(new ExposureView { DataContext = vm }, h: 1400, name: "tab-exposure-guide");

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

        Assert.True(vm.HasResult);
        Assert.Equal(4, vm.Histograms.Count);
    }

    /// <summary>
    /// The tab with the camera's preview: the switch on, the view on the preview (rows
    /// without bands), the raw / screen table and the note under it. Another path through
    /// the templates than the one with the switch off, which the other two tests above
    /// cover. The preview is a JPEG made in the test, so no raw is needed.
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
            Render(new ExposureView { DataContext = vm }, h: 1400, name: "tab-exposure-comparison");

            vm.Source.Preview.IsSelected = true;
            Render(new ExposureView { DataContext = vm }, h: 1400, name: "tab-exposure-screen");

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
            Assert.True(vm.Source.IsPreviewSelected);
            Assert.Equal(3, vm.Histograms.Count);
        });

    /// <summary>
    /// A frame without a mosaic takes another path: a single histogram row, without colour
    /// and without position, plus the explanation in place of the verdict.
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
            await vm.Pending;

            using var errors = new BindingErrors();
            Render(new ExposureView { DataContext = vm }, name: "tab-exposure-mono");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

        Assert.Single(vm.Histograms);
        Assert.False(vm.IsRawFlat);
    }

    /// <summary>
    /// The exposure tab before any choice: it has the list, but no result. It is the state
    /// the user finds it in when switching to it, so it must render.
    ///
    /// It also covers the histogram drawing with ALL its properties at their default value -
    /// null curves, NaN thresholds - because the element exists in the template before any
    /// figure exists. It is the only test that goes down that path.
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
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

        Assert.False(vm.Exposure.HasResult);
    }
}
