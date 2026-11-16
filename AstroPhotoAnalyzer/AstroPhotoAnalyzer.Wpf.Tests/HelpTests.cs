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

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// overlay covers the right side; the tabs are drawn over the error card when both show (a
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
    /// show, also with the focus in a field of the parameter card or on a button of the top bar.
    /// The key goes through the preview pass and WPF's own translation of a key into a command,
    /// so a key handler or an input binding that took F1 away would fail here.
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
            TextBox field = LogicalDescendants(w).OfType<TextBox>().First();
            Button openImage = LogicalDescendants((DependencyObject)w.FindName("TopButtons")).OfType<Button>().First();

            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#opening-an-image", opened.Last());
            Assert.True(PressF1(field));
            Assert.True(PressF1(openImage));
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
            Assert.True(PressF1(field));
            Assert.Equal(Page + "#export", opened.Last());

            w.Tabs.SelectedIndex = 8;
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#crop-with-aspect-ratio", opened.Last());
            Assert.Equal(8, opened.Count);

            Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Message == "Help opened: " + Page + "#export");
            await vm.CurvesComputation;
            await vm.ProofComputation;
            await vm.GamutFitComputation;
        });

    /// <summary>
    /// F1 in each viewing window opens its own section: the map's, the preview's and the curve
    /// window's headings, and the soft proof's for its full screen, which the Soft proof section
    /// explains, its keys G, O and I passing F1 by. Each press writes its address to the log.
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

            var proof = new SoftProofWindow { OpenAddress = opened.Add, DataContext = new SoftProofTabViewModel() };
            Assert.True(PressF1((UIElement)proof.Content));
            Assert.Equal(Page + "#soft-proof", opened.Last());

            Assert.Equal(4, opened.Count);
            Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Message == "Help opened: " + Page + "#the-curve-window");
            foreach (Window window in new Window[] { map, preview, curve, proof }) window.Close();
        });
    }

    /// <summary>
    /// F1 held down opens the manual once, in the main window and in each viewing window: the
    /// key's repeats are taken before WPF turns them into the Help command, which would open one
    /// browser tab per repeat (the branch's first review, 2026-11-16). The next press opens it
    /// again once the key was let go, or once the window lost the focus: the browser coming to
    /// the front can take the key's release, and WPF, which calls a key-down a repeat when the
    /// last key-down it saw was the same key, then calls that next press a repeat (the second
    /// review, 2026-11-16, read in WPF's KeyboardDevice and run through its input manager).
    /// </summary>
    [Fact]
    public void R186_HoldingF1OpensTheManualOnce()
    {
        _sta.Invoke(() =>
        {
            var opened = new List<string>();
            var main = new MainWindow { OpenAddress = opened.Add };
            var map = new MapWindow { OpenAddress = opened.Add };
            var preview = new PreviewWindow { OpenAddress = opened.Add };
            var curve = new CurveWindow { OpenAddress = opened.Add };
            var proof = new SoftProofWindow { OpenAddress = opened.Add, DataContext = new SoftProofTabViewModel() };

            foreach ((Window window, UIElement target) in new (Window, UIElement)[]
                     {
                         (main, main.Tabs), (map, (UIElement)map.Content), (preview, (UIElement)preview.Content),
                         (curve, (UIElement)curve.Content), (proof, (UIElement)proof.Content),
                     })
            {
                int before = opened.Count;
                Assert.True(PressF1(target, hold: true));
                for (int i = 0; i < 3; i++) Assert.True(PressF1(target, repeat: true, hold: true));
                Assert.Equal(before + 1, opened.Count);

                ReleaseF1(target);
                Assert.True(PressF1(target, hold: true));
                Assert.True(PressF1(target, repeat: true, hold: true));
                Assert.Equal(before + 2, opened.Count);

                // the browser came to the front and took the key's release: the next press,
                // which WPF calls a repeat, still opens the manual
                typeof(Window).GetMethod("OnDeactivated", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [EventArgs.Empty]);
                Assert.True(PressF1(target, repeat: true));
                Assert.Equal(before + 3, opened.Count);
            }
            foreach (Window window in new Window[] { main, map, preview, curve, proof }) window.Close();
        });
    }

    /// <summary>
    /// The ICC profile's line in the top bar never runs under the buttons (R-155): a name too
    /// long for the room left is shortened, "Scoate" right after it and 20 px short of the first
    /// button, at the window's minimum width and wider; a name that fits keeps "Scoate" 10 px
    /// after its end. The Help button made the room smaller, which brought the old overrun to
    /// wider windows (measured 2026-11-16, ADR 0102).
    /// </summary>
    [Theory]
    [InlineData("Canson Infinity Baryta Photographique II 310 - Epson SC-P9500 PK", 1000, true)]
    [InlineData("Canson Infinity Baryta Photographique II 310 - Epson SC-P9500 PK", 1300, true)]
    [InlineData("Zeta baryta", 1000, true)]
    [InlineData("Zeta baryta", 1920, false)]
    public Task R155_TheProfileLineIsShortenedBeforeTheButtonsAtAnyWidth(string name, int width, bool shortened)
        => _sta.InvokeAsync(async () =>
        {
            using var ws = new TempWorkspace();
            string file = ws.WriteFile("paper.icc", IccPrinterTestData.V2Lut16(name, 20));
            var w = new MainWindow();
            var vm = (MainViewModel)w.DataContext;
            await vm.ChooseProfile(new IccProfileRow(IccPaperBlack.Read(file)));
            var content = (UIElement)w.Content;
            w.Content = null;
            var host = new Border { Child = content, DataContext = w.DataContext, Background = Brushes.Black };
            foreach (CommandBinding b in w.CommandBindings) host.CommandBindings.Add(b);
            WindowSmokeTests.Render(host, width, 600, $"profile-line-{width}-{name.Length}");

            var text = (TextBlock)w.FindName("ProfileText");
            var clear = (Button)w.FindName("ClearProfileButton");
            var buttons = (FrameworkElement)w.FindName("TopButtons");
            double textLeft = text.TranslatePoint(new Point(0, 0), host).X;
            double clearLeft = clear.TranslatePoint(new Point(0, 0), host).X;
            double clearRight = clearLeft + clear.ActualWidth;
            double buttonsLeft = buttons.TranslatePoint(new Point(0, 0), host).X;

            Assert.True(clear.ActualWidth > 0 && text.ActualWidth > 0);
            Assert.True(clearRight <= buttonsLeft - 20 + 0.5, $"Scoate ends at {clearRight:F1}, the buttons start at {buttonsLeft:F1}");
            Assert.Equal(textLeft + text.ActualWidth + 10, clearLeft, 0.5);
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double whole = text.DesiredSize.Width;
            if (shortened) Assert.True(whole > text.ActualWidth + 0.5, $"the line is {text.ActualWidth:F1} px, whole {whole:F1}");
            else Assert.Equal(whole, text.ActualWidth, 0.5);   // no wider than its text: Scoate right after it
            w.Close();
        });

    /// <summary>
    /// The top bar's Help button shows a drawn icon and the word, with its tooltip, between "Save
    /// the report..." and About, and its click opens the manual; About opens from its own click,
    /// and its tooltip names no key. The bar fits the window's minimum width.
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
            var opened = new List<string>();
            var w = new MainWindow { OpenAddress = opened.Add };
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

            // a click, through the button's own handler, opens the manual (no image yet: the
            // opening's section)
            typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(help, null);
            Assert.Equal(new[] { Page + "#opening-an-image" }, opened);
        });
    }

    /// <summary>
    /// Presses F1 on <paramref name="target"/> as the keyboard would with the focus there: the
    /// preview pass from the window down, then, unless a handler took it, the key-down that WPF
    /// translates into the Help command, then the key let go. With <paramref name="hold"/> the key
    /// stays down; with <paramref name="repeat"/> the key-down is one WPF calls a repeat
    /// (KeyEventArgs.IsRepeat, set through its internal setter, which the input system uses).
    /// </summary>
    /// <returns>Whether something handled the key-down.</returns>
    static bool PressF1(UIElement target, bool repeat = false, bool hold = false)
    {
        bool handled = KeyDownF1(target, repeat);
        if (!hold) ReleaseF1(target);
        return handled;
    }

    /// <summary>The key-down of F1 on <paramref name="target"/>, as <see cref="PressF1"/>
    /// describes it, the key left down.</summary>
    /// <returns>Whether something handled it.</returns>
    static bool KeyDownF1(UIElement target, bool repeat)
    {
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, new KeySource(), 0, Key.F1) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        if (repeat) typeof(KeyEventArgs).GetMethod("SetRepeat", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(e, [true]);
        Assert.Equal(repeat, e.IsRepeat);
        target.RaiseEvent(e);
        if (e.Handled) return true;
        e.RoutedEvent = Keyboard.KeyDownEvent;
        target.RaiseEvent(e);
        return e.Handled;
    }

    /// <summary>Lets F1 go on <paramref name="target"/>: the preview key-up and the key-up, as
    /// the keyboard sends them.</summary>
    static void ReleaseF1(UIElement target)
    {
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, new KeySource(), 0, Key.F1) { RoutedEvent = Keyboard.PreviewKeyUpEvent };
        target.RaiseEvent(e);
        if (e.Handled) return;
        e.RoutedEvent = Keyboard.KeyUpEvent;
        target.RaiseEvent(e);
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

    /// <summary>Every element under <paramref name="node"/> in the logical tree, depth first: the
    /// window's controls before it is laid out, when the visual tree is not built yet.</summary>
    static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject node)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(node))
        {
            if (child is not DependencyObject d) continue;
            yield return d;
            foreach (var x in LogicalDescendants(d)) yield return x;
        }
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
