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

// HelpTests.cs - the Help button and F1 open the manual at the section of what is on screen, in
// the main window and in the four viewing windows (R-186, ADR 0102). The browser is never
// started: each window's OpenAddress is replaced by a stand-in that keeps the address. The
// addresses are written out, as the wiki's headings make them.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AstroPhotoAnalyzer.Tests;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("fereastra")]
public class HelpTests
{
    const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/AstroPhotoAnalyzer";

    readonly StaFixture _sta;

    public HelpTests(StaFixture sta) => _sta = sta;

    /// <summary>
    /// Each state of the main window and each tab has its section of the manual. The busy
    /// overlay is over everything; the tabs are drawn over the error card when both show (a
    /// field refused while a result is on screen, CR-040); the card alone opens the table of its
    /// messages; everything else is the opening of an image.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, null, Page + "#opening-an-image")]
    [InlineData(true, false, false, null, Page + "#opening-an-image")]
    [InlineData(true, false, false, ManualSection.Export, Page + "#opening-an-image")]
    [InlineData(true, true, false, ManualSection.Tones, Page + "#opening-an-image")]
    [InlineData(false, false, true, null, Page + "#when-something-goes-wrong")]
    [InlineData(false, false, true, ManualSection.Export, Page + "#when-something-goes-wrong")]
    [InlineData(false, true, true, ManualSection.Colour, Page + "#colour")]
    [InlineData(false, true, false, ManualSection.Report, Page + "#report")]
    [InlineData(false, true, false, ManualSection.Tones, Page + "#tones")]
    [InlineData(false, true, false, ManualSection.Export, Page + "#export")]
    [InlineData(false, true, false, ManualSection.Resolution, Page + "#resolution")]
    [InlineData(false, true, false, ManualSection.Colour, Page + "#colour")]
    [InlineData(false, true, false, ManualSection.CurvesEditing, Page + "#curves-editing")]
    [InlineData(false, true, false, ManualSection.ColoursForPaper, Page + "#colours-for-paper")]
    [InlineData(false, true, false, ManualSection.SoftProof, Page + "#soft-proof")]
    [InlineData(false, true, false, ManualSection.CropWithAspectRatio, Page + "#crop-with-aspect-ratio")]
    public void R186_EachStateAndTabHasItsSectionOfTheManual(bool isBusy, bool hasResult, bool hasError,
                                                            ManualSection? tab, string expected)
    {
        Assert.Equal(expected, Manual.Url(Manual.SectionFor(isBusy, hasResult, hasError, tab)));
    }

    /// <summary>The nine tabs, in their order, carry their nine sections.</summary>
    [Fact]
    public void R186_TheNineTabsCarryTheirSections()
    {
        _sta.Invoke(() =>
        {
            var w = new MainWindow();
            Assert.Equal(new object[]
            {
                ManualSection.Report, ManualSection.Tones, ManualSection.Export, ManualSection.Resolution,
                ManualSection.Colour, ManualSection.CurvesEditing, ManualSection.ColoursForPaper,
                ManualSection.SoftProof, ManualSection.CropWithAspectRatio,
            }, w.Tabs.Items.Cast<TabItem>().Select(t => t.Tag).ToArray());
        });
    }

    /// <summary>
    /// F1, pressed on an element inside the main window, opens the manual at the section of what
    /// is on screen and logs the address: the opening's before an image and while the analysis
    /// runs, the table of the card's messages on the card, the selected tab's once the tabs
    /// show. The key goes through the preview pass and WPF's own translation of a key into a
    /// command, so a key handler or an input binding that took F1 away would fail here.
    /// </summary>
    [Fact]
    public Task R186_HelpAndF1OpenTheManualAtTheSectionOnScreen()
        => _sta.InvokeAsync(async () =>
        {
            using var ws = new TempWorkspace();
            string tif = WindowSmokeTests.DarkTiff(ws);
            using LogCapture capture = LogCapture.Start();
            var opened = new List<string>();
            var w = new MainWindow { OpenAddress = opened.Add };
            var vm = (MainViewModel)w.DataContext;

            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#opening-an-image", opened.Last());

            await vm.OpenAsync(ws.PathTo("nu-exista.tif"));
            Assert.True(vm.HasError);
            Assert.False(vm.HasResult);
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#when-something-goes-wrong", opened.Last());

            Task run = vm.OpenAsync(tif);
            Assert.True(vm.IsBusy);
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#opening-an-image", opened.Last());
            await run;
            Assert.True(vm.HasResult, vm.Error);

            w.Tabs.SelectedIndex = 2;
            Assert.True(PressF1((UIElement)((TabItem)w.Tabs.SelectedItem).Content));
            Assert.Equal(Page + "#export", opened.Last());

            w.Tabs.SelectedIndex = 8;
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#crop-with-aspect-ratio", opened.Last());
            Assert.Equal(5, opened.Count);

            Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Message == "Help opened: " + Page + "#export");
            await vm.CurvesComputation;
            await vm.ProofComputation;
            await vm.GamutFitComputation;
        });

    /// <summary>
    /// F1 in each viewing window opens its own section: the map's, the preview's and the curve
    /// window's headings, and the soft proof's for its full screen, which the Soft proof section
    /// explains. Each press writes its address to the log.
    /// </summary>
    [Fact]
    public void R186_F1InTheViewingWindowsOpensTheirSections()
    {
        _sta.Invoke(() =>
        {
            using LogCapture capture = LogCapture.Start();
            var opened = new List<string>();

            var map = new MapWindow { OpenAddress = opened.Add };
            Assert.True(PressF1((UIElement)map.Content));
            Assert.Equal(Page + "#the-dust--sky-map-window", opened.Last());

            var preview = new PreviewWindow { OpenAddress = opened.Add };
            Assert.True(PressF1((UIElement)preview.Content));
            Assert.Equal(Page + "#the-preview-window", opened.Last());

            var curve = new CurveWindow { OpenAddress = opened.Add };
            Assert.True(PressF1((UIElement)curve.Content));
            Assert.Equal(Page + "#the-curve-window", opened.Last());

            var proof = new SoftProofWindow { OpenAddress = opened.Add };
            Assert.True(PressF1((UIElement)proof.Content));
            Assert.Equal(Page + "#soft-proof", opened.Last());

            Assert.Equal(4, opened.Count);
            Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Message == "Help opened: " + Page + "#the-curve-window");
            foreach (Window window in new Window[] { map, preview, curve, proof }) window.Close();
        });
    }

    /// <summary>
    /// The top bar's Help button runs the Help command and shows a drawn icon and the word, with
    /// its tooltip, between "Save the report..." and About; About opens from its own click, and
    /// its tooltip names no key. The bar fits the window's minimum width.
    /// </summary>
    [Theory]
    [InlineData(true, "Help", "The manual, at the section of this tab (F1)", "About", "Version, licence, components", "Save the report...")]
    [InlineData(false, "Ajutor", "Manualul, la sectiunea tabului curent (F1)", "Despre", "Versiune, licenta, componente", "Salveaza raportul...")]
    public void R186_TheHelpButtonShowsAnIconAndTheWord(bool english, string word, string tip, string about, string aboutTip, string save)
    {
        _sta.Invoke(() =>
        {
            using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);

            // a window never shown renders white, its template unapplied: its content is moved
            // into a plain host with the window's data and commands, at the window's minimum width
            var w = new MainWindow();
            var content = (UIElement)w.Content;
            w.Content = null;
            var host = new Border { Child = content, DataContext = w.DataContext, Background = Brushes.Black };
            foreach (CommandBinding b in w.CommandBindings) host.CommandBindings.Add(b);
            WindowSmokeTests.Render(host, 1000, 600, english ? "en-help-top-bar" : "ro-help-top-bar");

            var buttons = Descendants(host).OfType<Button>().ToList();
            Button help = Assert.Single(buttons, b => b.Command == ApplicationCommands.Help);
            Assert.True(help.IsEnabled);
            Assert.Equal(tip, help.ToolTip);
            Assert.Contains(Descendants(help).OfType<TextBlock>(), t => t.Text == word);
            Image icon = Assert.Single(Descendants(help).OfType<Image>());
            Assert.IsType<DrawingImage>(icon.Source);
            Assert.True(icon.ActualWidth > 0);

            Button aboutButton = Assert.Single(buttons, b => b.Content as string == about);
            Assert.Null(aboutButton.Command);
            Assert.Equal(aboutTip, aboutButton.ToolTip);

            // the bar fits the window's minimum width: About, the rightmost, ends at the bar's
            // 20 px right padding, not past it
            Assert.True(aboutButton.TranslatePoint(new Point(aboutButton.ActualWidth, 0), host).X < 1000 - 20 + 0.5);

            // Help sits between "Save the report..." and About, in the same row
            var row = (Panel)VisualTreeHelper.GetParent(help);
            int at = row.Children.IndexOf(help);
            Assert.Same(aboutButton, row.Children[at + 1]);
            Assert.Equal(save, ((Button)row.Children[at - 1]).Content);
        });
    }

    /// <summary>
    /// Presses F1 on <paramref name="target"/> as the keyboard would with the focus there: the
    /// preview pass from the window down, then, unless a handler took it, the key-down that WPF
    /// translates into the Help command.
    /// </summary>
    /// <returns>Whether something handled the key.</returns>
    static bool PressF1(UIElement target)
    {
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, new KeySource(), 0, Key.F1) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(e);
        if (e.Handled) return true;
        e.RoutedEvent = Keyboard.KeyDownEvent;
        target.RaiseEvent(e);
        return e.Handled;
    }

    /// <summary>
    /// The presentation source a key event names: a window never shown has none, and nothing
    /// here is drawn through it (as in AboutWindowFitTests).
    /// </summary>
    sealed class KeySource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;

        public override bool IsDisposed => false;

        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    /// <summary>Every element under <paramref name="node"/> in the visual tree, depth first.</summary>
    static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(node, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }
}
