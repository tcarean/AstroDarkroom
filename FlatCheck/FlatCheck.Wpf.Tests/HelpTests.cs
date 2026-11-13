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

// HelpTests.cs - the Help button and F1 open the manual at the section of what is on screen
// (R-141, ADR 0112). The browser is never started: the window's OpenAddress is replaced by a
// stand-in that keeps the address.

using FlatCheck.Tests;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace FlatCheck.Wpf.Tests;

[Collection("window")]
public class HelpTests
{
    const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck";

    readonly StaFixture _sta;

    public HelpTests(StaFixture sta) => _sta = sta;

    /// <summary>
    /// Each state of the window and each tab has its section of the manual; the addresses are
    /// written out, as the wiki's headings make them. Every state without the tabs, the problem
    /// card included, opens "Opening a session" (the owner's answer after the branch review).
    /// </summary>
    [Theory]
    [InlineData(false, false, false, null, Page + "#opening-a-session")]
    [InlineData(true, false, true, null, Page + "#opening-a-session")]
    [InlineData(true, false, true, ManualSection.Split, Page + "#opening-a-session")]
    [InlineData(true, true, false, null, Page + "#opening-a-session")]
    [InlineData(true, true, false, ManualSection.Exposure, Page + "#opening-a-session")]
    [InlineData(true, false, false, ManualSection.Structure, Page + "#structure")]
    [InlineData(true, false, false, ManualSection.EndToEnd, Page + "#end-to-end")]
    [InlineData(true, false, false, ManualSection.Exposure, Page + "#exposure")]
    [InlineData(true, false, false, ManualSection.Histogram, Page + "#histogram")]
    [InlineData(true, false, false, ManualSection.Synthetic, Page + "#synthetic")]
    [InlineData(true, false, false, ManualSection.Split, Page + "#split")]
    public void R141_EachStateAndTabHasItsSectionOfTheManual(bool hasFolder, bool hasProblem, bool isRunning,
                                                            ManualSection? tab, string expected)
    {
        Assert.Equal(expected, Manual.Url(Manual.SectionFor(hasFolder, hasProblem, isRunning, tab)));
    }

    /// <summary>The six tabs, in their order, carry their six sections.</summary>
    [Fact]
    public void R141_TheSixTabsCarryTheirSections()
    {
        _sta.Invoke(() =>
        {
            var w = new MainWindow();
            Assert.Equal(new object[]
            {
                ManualSection.Structure, ManualSection.EndToEnd, ManualSection.Exposure,
                ManualSection.Histogram, ManualSection.Synthetic, ManualSection.Split,
            }, w.Tabs.Items.Cast<TabItem>().Select(t => t.Tag).ToArray());
        });
    }

    /// <summary>
    /// F1, pressed on an element inside the window, opens the manual at the section of what is
    /// on screen and logs the address: the opening's before a folder and on the problem card
    /// (whose explanation is in that section), the selected tab's once the tabs show. The key goes through WPF's own
    /// translation of a key into a command, so an input binding that took F1 away would fail
    /// here.
    /// </summary>
    [Fact]
    public async Task R141_HelpAndF1OpenTheManualAtTheSectionOnScreen()
    {
        using var good = new SessionFixture().WithFlatMaster();
        using var bad = new SessionFixture().WithProcess();
        using var capture = LogCapture.Start();

        await _sta.InvokeAsync(async () =>
        {
            var opened = new List<string>();
            var w = new MainWindow { OpenAddress = opened.Add };

            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#opening-a-session", opened.Last());

            await w.ViewModel.OpenAsync(bad.Root);
            Assert.True(w.ViewModel.HasProblem);
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#opening-a-session", opened.Last());

            await w.ViewModel.OpenAsync(good.Root);
            Assert.False(w.ViewModel.HasProblem || w.ViewModel.IsRunning);
            w.Tabs.SelectedIndex = 2;
            Assert.True(PressF1((UIElement)((TabItem)w.Tabs.SelectedItem).Content));
            Assert.Equal(Page + "#exposure", opened.Last());

            w.Tabs.SelectedIndex = 5;
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#split", opened.Last());
            Assert.Equal(4, opened.Count);
        });

        Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Message == "Help opened: " + Page + "#exposure");
    }

    /// <summary>
    /// A link the shell cannot open is logged and the window goes on: a relative address,
    /// which has no absolute form, included (the review of the branch, 2026-11-13: reading it
    /// threw out of the click handler).
    /// </summary>
    [Fact]
    public void ALinkThatCannotBeOpenedIsLoggedNotThrown()
    {
        using var capture = LogCapture.Start();
        var e = new RequestNavigateEventArgs(new Uri("no-such-flatcheck-page.html", UriKind.Relative), null)
        {
            RoutedEvent = Hyperlink.RequestNavigateEvent,
        };

        ExternalLink.Open(e);

        Assert.True(e.Handled);
        Assert.Contains(capture.Events, ev => ev.Message == "the link no-such-flatcheck-page.html could not be opened in a browser");
    }

    /// <summary>
    /// The top bar's Help button runs the Help command and shows a drawn icon and the word, with
    /// its tooltip; About opens from its own click, and its tooltip names no key.
    /// </summary>
    [Theory]
    [InlineData(false, "Help", "The manual, at the section of this tab (F1)", "About", "Version, licence, components")]
    [InlineData(true, "Ajutor", "Manualul, la sectiunea tabului curent (F1)", "Despre", "Versiune, licenta, componente")]
    public void R141_TheHelpButtonShowsAnIconAndTheWord(bool romanian, string word, string tip, string about, string aboutTip)
    {
        _sta.Invoke(() =>
        {
            using var _ = Ui.Use(romanian ? UiLanguage.Romanian : UiLanguage.English);

            // a window never shown renders white, its template unapplied: its content is moved
            // into a plain host with the window's data and commands, at the window's minimum width
            var w = new MainWindow();
            var content = (UIElement)w.Content;
            w.Content = null;
            var host = new Border { Child = content, DataContext = w.DataContext, Background = Brushes.Black };
            foreach (CommandBinding b in w.CommandBindings) host.CommandBindings.Add(b);
            WindowSmokeTests.Render(host, 960, 560, romanian ? "ro-help-top-bar" : "en-help-top-bar");

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
            Assert.True(aboutButton.TranslatePoint(new Point(aboutButton.ActualWidth, 0), host).X < 960 - 20 + 0.5);

            // Help sits between the folder button and About, in the same row
            var row = (Panel)VisualTreeHelper.GetParent(help);
            int at = row.Children.IndexOf(help);
            Assert.Same(aboutButton, row.Children[at + 1]);
            Assert.Equal(Ui.T("Choose folder...", "Alege folder..."), ((Button)row.Children[at - 1]).Content);
        });
    }

    /// <summary>Raises a key-down of F1 on <paramref name="target"/>, as the keyboard would with
    /// the focus there; returns whether something handled it.</summary>
    static bool PressF1(UIElement target)
    {
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, new KeySource(), 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent };
        target.RaiseEvent(e);
        return e.Handled;
    }

    /// <summary>The presentation source a key event names: a window never shown has none, and
    /// nothing here is drawn through it (as in AboutWindowFitTests).</summary>
    sealed class KeySource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;

        public override bool IsDisposed => false;

        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

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
}
