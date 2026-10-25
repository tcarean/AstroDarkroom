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

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// The window's language (R-101): the start-up reads <c>--lang</c>, never takes it for the
/// folder, and an English window shows no Romanian word, from the XAML, the view-models or the
/// core. The assembly runs in Romanian (<see cref="RomanianByDefault"/>, serial), so each test
/// here puts the default back when it ends.
/// </summary>
[Collection("fereastra")]
public class WindowLanguageTests
{
    readonly StaFixture _sta;

    public WindowLanguageTests(StaFixture sta) => _sta = sta;

    [Theory]
    [InlineData(new[] { "D:\\sesiune", "--lang", "ro" }, "ro", null)]
    [InlineData(new[] { "--lang=en", "D:\\sesiune" }, "en", null)]
    [InlineData(new[] { "D:\\sesiune" }, "en", null)]
    [InlineData(new[] { "D:\\sesiune", "--lang", "fr" }, "en", "fr")]
    [InlineData(new[] { "--lang" }, "en", "")]
    public void R101_TheStartupSetsTheLanguageAndReturnsAValueItDidNotUnderstand(string[] args, string expected, string? bad)
    {
        UiLanguage saved = Ui.Default;
        try
        {
            string unknown = App.ApplyCommandLine(args);

            Assert.Equal(expected == "ro" ? UiLanguage.Romanian : UiLanguage.English, Ui.Default);
            Assert.Equal(bad, unknown);
        }
        finally
        {
            Ui.Default = saved;
        }
    }

    [Fact]
    public void R101_TheMessageForAValueNotUnderstoodIsEnglish()
        => Assert.Equal("--lang expects en or ro, got 'fr'", LanguageArgument.BadValueMessage("fr"));

    /// <summary>A --lang and its value are never the session folder, even when the value is
    /// a folder that exists.</summary>
    [Fact]
    public void R101_TheLanguageIsNeverTakenForTheFolder()
    {
        using var ws = new FlatCheck.Tests.TempWorkspace();
        string other = Path.Combine(ws.Dir, "sesiune");
        Directory.CreateDirectory(other);

        Assert.Null(App.FolderFromArgs(new[] { "--lang", ws.Dir }));
        Assert.Equal(other, App.FolderFromArgs(new[] { "--lang", ws.Dir, other }));
        Assert.Equal(other, App.FolderFromArgs(new[] { other, "--lang=ro" }));
    }

    // Words that are Romanian and never English, as whole words.
    static readonly Regex Romanian = new(
        @"\b(si|sau|nu|din|cu|pe|de|la|fara|mai|este|stopuri|cadre|cadrul|harta|obiectiv|obiectivul|"
        + @"netezit|netezire|sintetic|fisier|fisierul|folderul|alege|masterul|eroare|lipseste|scara|"
        + @"colt|centru|reziduu|vignetare|expunere|structura|teste|atentie|verdictul|praf|ales|niciunul|"
        + @"despre|inchide|opreste|potrivit|ruleaza|parti|tabul|cerut|preumplut|nimic|pentru|aparat|"
        + @"canal|canalul|mediana|scrie|salvat|salveaza|netezeste|propune|ramas|scos|inainte|dupa)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Every tab with real results, with its guides open, and the problem screen, all built
    /// and rendered in English: no text on screen holds a Romanian word.
    /// </summary>
    [Fact]
    public async Task R101_AnEnglishWindowShowsNoRomanianWord()
    {
        UiLanguage saved = Ui.Default;
        Ui.Default = UiLanguage.English;
        try
        {
            using var f = new SessionFixture()
                .WithFlatMaster()
                .WithSmoothedMaster()
                .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit")
                .WithLightSeries(6)
                .WithRawFlats("DSC_0101.NEF", "DSC_0102.NEF");
            using var broken = new SessionFixture().WithProcess();

            var vm = new MainViewModel();
            var problem = new MainViewModel();
            var texts = new List<string>();

            await _sta.InvokeAsync(async () =>
            {
                await vm.OpenAsync(f.Root);
                vm.Ratio.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
                vm.Ratio.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));
                await vm.Ratio.RecomputeAsync();
                await vm.Synthetic.Pending;
                await problem.OpenAsync(broken.Root);

                vm.Structure.MapGuide.IsExpanded = true;
                vm.Structure.HowToRead.IsExpanded = true;
                vm.Ratio.MapGuide.IsExpanded = true;
                vm.Exposure.HistogramGuide.IsExpanded = true;
                vm.HistEq.Guide.IsExpanded = true;
                vm.Smooth.Guide.IsExpanded = true;
                vm.Synthetic.Guide.IsExpanded = true;

                foreach (var (name, view) in new (string, FrameworkElement)[]
                {
                    ("en-window", new MainWindow { DataContext = vm }),
                    ("en-problem", new MainWindow { DataContext = problem }),
                    ("en-structure", new StructureView { DataContext = vm.Structure }),
                    ("en-ratio", new RatioView { DataContext = vm.Ratio }),
                    ("en-end-to-end", new LightView { DataContext = vm.Light }),
                    ("en-exposure", new ExposureView { DataContext = vm.Exposure }),
                    ("en-histogram", new HistEqView { DataContext = vm.HistEq }),
                    ("en-smoothing", new SmoothView { DataContext = vm.Smooth }),
                    ("en-synthetic", new SyntheticView { DataContext = vm.Synthetic }),
                    ("en-split", new SplitView { DataContext = vm.Split }),
                })
                {
                    Render(view, name);
                    Collect(view, texts);
                }
            });

            Assert.True(vm.Structure.HasResult && vm.Ratio.HasResult && vm.Light.HasResult);
            Assert.True(problem.HasProblem);
            foreach (string tab in new[] { "Structure", "Exposure", "Smoothing", "Synthetic" })
                Assert.Contains(tab, texts);
            var romanian = texts.Where(t => Romanian.IsMatch(WithoutPaths(t))).Distinct().ToList();
            Assert.True(romanian.Count == 0, "Romanian on an English screen:\n" + string.Join("\n---\n", romanian));
        }
        finally
        {
            Ui.Default = saved;
        }
    }

    /// <summary>
    /// The states the test above leaves empty, filled, in English: an exposure result with its
    /// histograms and guide, an equalised master with its guide and views, a smoothing result
    /// with its scale proposal, a split table, an error card, and the About window.
    /// </summary>
    [Fact]
    public void R101_EnglishResultsGuidesAndCardsShowNoRomanianWord()
    {
        UiLanguage saved = Ui.Default;
        Ui.Default = UiLanguage.English;
        try
        {
            using var f = new SessionFixture()
                .WithFlatMaster()
                .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
            SplitResult split = SplitRun.Analyze(f.Session.FlatMasterPath,
                new[] { f.MasterPath("pp_flat_stacked_A.fit"), f.MasterPath("pp_flat_stacked_B.fit") },
                null, new DateTime(2026, 9, 28, 10, 31, 0), 10);
            var texts = new List<string>();

            _sta.Invoke(() =>
            {
                var mosaic = FlatCheck.Tests.TestImages.From(128, 128, FlatCheck.Tests.FitsTestData.WithBayerMosaic(
                    FlatCheck.Tests.FitsTestData.Radial(128, 128, center: 34000)));

                var exposure = new ExposureTabViewModel();
                exposure.Populate(ExposureAnalyzer.Analyze(mosaic, 65535, null, false, null, CfaPattern.Parse("RGGB", "BAYERPAT")));
                exposure.HistogramGuide.IsExpanded = true;

                var histeq = new HistEqTabViewModel();
                histeq.Populate(HistEqAnalyzer.Analyze(mosaic, CfaPattern.Parse("RGGB", "BAYERPAT")), HistEqKind.Smoothed);
                histeq.Guide.IsExpanded = true;

                var smooth = new SmoothTabViewModel(_ => true);
                smooth.Attach(f.Session, CancellationToken.None);
                smooth.Populate(SmoothAnalyzer.Analyze(
                    FlatCheck.Tests.TestImages.From(256, 256, FlatCheck.Tests.FitsTestData.Radial(256, 256)), 12));
                smooth.PopulateAdvice(SmoothScaleAdvisor.Decide(0.69, SmoothScaleAdvisor.Ladder.Select(px => new SmoothScaleTrial
                {
                    ScalePx = px,
                    TextureAfterPct = 0.02,
                    TextureGone = true,
                    RemovedPtpPct = 3,
                }).ToList()));

                var splitVm = new SplitTabViewModel();
                splitVm.Populate(split);

                var ratio = new RatioTabViewModel();
                ratio.Choose(isA: true, path: @"C:\somewhere\picture.jpg");

                foreach (var (name, view) in new (string, FrameworkElement)[]
                {
                    ("en-exposure-result", new ExposureView { DataContext = exposure }),
                    ("en-histogram-result", new HistEqView { DataContext = histeq }),
                    ("en-smoothing-result", new SmoothView { DataContext = smooth }),
                    ("en-split-result", new SplitView { DataContext = splitVm }),
                    ("en-ratio-error", new RatioView { DataContext = ratio }),
                    ("en-about", new AboutWindow()),
                })
                {
                    Render(view, name);
                    Collect(view, texts);
                }

                Assert.True(exposure.HasResult && histeq.HasResult && smooth.HasResult && splitVm.HasResult);
                Assert.True(ratio.HasError);
            });

            Assert.Contains("Mosaic", texts);
            var romanian = texts.Where(t => Romanian.IsMatch(WithoutPaths(t))).Distinct().ToList();
            Assert.True(romanian.Count == 0, "Romanian on an English screen:\n" + string.Join("\n---\n", romanian));
        }
        finally
        {
            Ui.Default = saved;
        }
    }

    /// <summary>
    /// Files with the former Romanian names are not the smoothed or the synthetic master any
    /// more (R-101, the owner's decision): beside the master only the English names count.
    /// </summary>
    [Fact]
    public void R101_FilesWithTheFormerRomanianNamesAreNotRecognised()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string dir = Path.GetDirectoryName(f.Session.FlatMasterPath)!;
        File.WriteAllText(Path.Combine(dir, "pp_flat_stacked_netezit.fit"), "x");
        File.WriteAllText(Path.Combine(dir, "pp_flat_stacked_sintetic.fit"), "x");

        Assert.Equal(Path.Combine(dir, "pp_flat_stacked_smoothed.fit"), SirilSession.SmoothedPathBeside(f.Session.FlatMasterPath));
        Assert.Equal(Path.Combine(dir, "pp_flat_stacked_synthetic.fit"), SirilSession.SyntheticPathBeside(f.Session.FlatMasterPath));
        Assert.DoesNotContain(f.Session.HistEqCandidates(), c => c.Path.Contains("_netezit"));
    }

    /// <summary>The same screen in Romanian still speaks Romanian: the check above is not
    /// passing because nothing is shown.</summary>
    [Fact]
    public async Task R101_ARomanianWindowStillSpeaksRomanian()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0101.NEF");
        var vm = new MainViewModel();
        var texts = new List<string>();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);
            var view = new MainWindow { DataContext = vm };
            Render(view);
            Collect(view, texts);
        });

        Assert.Contains("Structura", texts);
        Assert.Contains(texts, t => Romanian.IsMatch(t));
    }

    /// <summary>Lays an element out so that every template is applied. A window that is never
    /// shown builds no visual tree of its own, so its content is laid out instead (it keeps the
    /// window's DataContext through the logical tree).</summary>
    /// <param name="el">The element; a window is laid out through its content.</param>
    /// <param name="name">With FLATCHECK_DUMP_SCREENS=&lt;folder&gt;, the PNG the render is saved
    /// as, for the eye (as in WindowSmokeTests); null for none.</param>
    static void Render(FrameworkElement el, string? name = null)
    {
        if (el is Window w) el = (FrameworkElement)w.Content;
        el.Measure(new Size(1360, 2400));
        el.Arrange(new Rect(0, 0, 1360, 2400));
        el.UpdateLayout();

        string? dir = Environment.GetEnvironmentVariable("FLATCHECK_DUMP_SCREENS");
        if (string.IsNullOrEmpty(dir) || name == null) return;
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(1360, 2400, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(el);
        Directory.CreateDirectory(dir);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
        using var fs = File.Create(Path.Combine(dir, name + ".png"));
        enc.Save(fs);
    }

    /// <summary>Every text the element shows: text blocks, string contents and headers, and
    /// the tooltips, through the visual tree; a window's through its content.</summary>
    static void Collect(DependencyObject node, List<string> into)
    {
        if (node is Window w)
        {
            if (w.Title is { Length: > 0 } title) into.Add(title);
            node = (DependencyObject)w.Content;
        }
        switch (node)
        {
            case TextBlock tb when !string.IsNullOrWhiteSpace(tb.Text): into.Add(tb.Text); break;
            case HeaderedContentControl h when h.Header is string s: into.Add(s); break;
        }
        if (node is ContentControl { Content: string c }) into.Add(c);
        if (node is FrameworkElement { ToolTip: string tip }) into.Add(tip);
        if (node is TabControl tabs)
            foreach (var item in tabs.Items.OfType<TabItem>())
                if (item.Header is string header) into.Add(header);

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            Collect(VisualTreeHelper.GetChild(node, i), into);
    }

    /// <summary>A text without its file system paths, whose folder names are the fixtures'
    /// and the machine's.</summary>
    static string WithoutPaths(string text) => Regex.Replace(text, @"[A-Za-z]:\\\S*|\S*[\\/]\S*", "");
}
