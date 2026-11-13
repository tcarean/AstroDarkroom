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
using System.Windows.Input;
using System.Windows.Media;

namespace FlatCheck.Wpf.Tests;

[Collection("window")]
public class HelpTests
{
    const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck";

    readonly StaFixture _sta;

    public HelpTests(StaFixture sta) => _sta = sta;

    /// <summary>
    /// Each state of the window and each tab has its section of the manual; the addresses are
    /// written out, as the wiki's headings make them.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, null, Page + "#opening-a-session")]
    [InlineData(true, false, true, null, Page + "#opening-a-session")]
    [InlineData(true, false, true, ManualSection.Split, Page + "#opening-a-session")]
    [InlineData(true, true, false, null, Page + "#the-session-folder")]
    [InlineData(true, true, false, ManualSection.Exposure, Page + "#the-session-folder")]
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
    /// The Help command, which F1 raises, opens the manual at the section of what is on
    /// screen and logs the address: the opening's before a folder, the session folder's on the
    /// problem card, the selected tab's once the tabs show.
    /// </summary>
    [Fact]
    public async Task R141_HelpAndF1OpenTheManualAtTheSectionOnScreen()
    {
        Assert.Contains(ApplicationCommands.Help.InputGestures.OfType<KeyGesture>(), g => g.Key == Key.F1 && g.Modifiers == ModifierKeys.None);

        using var good = new SessionFixture().WithFlatMaster();
        using var bad = new SessionFixture().WithProcess();
        using var capture = LogCapture.Start();

        await _sta.InvokeAsync(async () =>
        {
            var opened = new List<string>();
            var w = new MainWindow { OpenAddress = opened.Add };

            ApplicationCommands.Help.Execute(null, w);
            Assert.Equal(Page + "#opening-a-session", opened.Last());

            await w.ViewModel.OpenAsync(bad.Root);
            Assert.True(w.ViewModel.HasProblem);
            ApplicationCommands.Help.Execute(null, w);
            Assert.Equal(Page + "#the-session-folder", opened.Last());

            await w.ViewModel.OpenAsync(good.Root);
            Assert.False(w.ViewModel.HasProblem || w.ViewModel.IsRunning);
            w.Tabs.SelectedIndex = 2;
            ApplicationCommands.Help.Execute(null, w);
            Assert.Equal(Page + "#exposure", opened.Last());

            w.Tabs.SelectedIndex = 5;
            ApplicationCommands.Help.Execute(null, w);
            Assert.Equal(Page + "#split", opened.Last());
            Assert.Equal(4, opened.Count);
        });

        Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Message == "Help opened: " + Page + "#exposure");
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
            Assert.True(help.TranslatePoint(new Point(help.ActualWidth, 0), host).X < 960);
            Assert.Equal(tip, help.ToolTip);
            Assert.Contains(Descendants(help).OfType<TextBlock>(), t => t.Text == word);
            Image icon = Assert.Single(Descendants(help).OfType<Image>());
            Assert.IsType<DrawingImage>(icon.Source);
            Assert.True(icon.ActualWidth > 0);

            Button aboutButton = Assert.Single(buttons, b => b.Content as string == about);
            Assert.Null(aboutButton.Command);
            Assert.Equal(aboutTip, aboutButton.ToolTip);

            // Help sits between the folder button and About, in the same row
            var row = (Panel)VisualTreeHelper.GetParent(help);
            int at = row.Children.IndexOf(help);
            Assert.Same(aboutButton, row.Children[at + 1]);
            Assert.Equal(Ui.T("Choose folder...", "Alege folder..."), ((Button)row.Children[at - 1]).Content);
        });
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
