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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DarksCheck.Core;
using DarksCheck.Tests;

namespace DarksCheck.Wpf.Tests;

/// <summary>
/// The Help button and F1 open the manual at the section of what is on screen (R-125, ADR 0061),
/// FlatCheck's HelpTests (its R-141) over DarksCheck's states and tabs. The browser is never
/// started: the window's OpenAddress is replaced by a stand-in that keeps the address.
/// </summary>
[Collection("window")]
public class HelpTests
{
    private const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/DarksCheck";

    private readonly StaFixture _sta;

    public HelpTests(StaFixture sta) => _sta = sta;

    /// <summary>
    /// Each state of the window and each tab has its section of the manual; the addresses are
    /// written out, as the wiki's headings make them. The wait screen covers everything, the
    /// problem card goes to the table of what to do (the owner's answer), and a window with no
    /// result showing (no folder yet, a stopped run) goes to how a folder is opened.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, null, Page + "#opening-a-folder")]
    [InlineData(false, false, false, ManualSection.Summary, Page + "#opening-a-folder")]
    [InlineData(false, false, true, null, Page + "#opening-a-folder")]
    [InlineData(false, true, true, ManualSection.Maps, Page + "#opening-a-folder")]
    [InlineData(true, false, false, null, Page + "#when-something-goes-wrong")]
    [InlineData(true, false, false, ManualSection.Frames, Page + "#when-something-goes-wrong")]
    [InlineData(false, true, false, null, Page + "#opening-a-folder")]
    [InlineData(false, true, false, ManualSection.Summary, Page + "#summary")]
    [InlineData(false, true, false, ManualSection.Frames, Page + "#frames")]
    [InlineData(false, true, false, ManualSection.Maps, Page + "#maps")]
    [InlineData(false, true, false, ManualSection.HotPixels, Page + "#hot-pixels")]
    [InlineData(false, true, false, ManualSection.MatchWithLights, Page + "#match-with-lights")]
    [InlineData(false, true, false, ManualSection.Index, Page + "#index")]
    [InlineData(false, true, false, ManualSection.AduScaleExplained, Page + "#adu-scale-explained")]
    [InlineData(false, true, false, ManualSection.Report, Page + "#report")]
    public void R125_EachStateAndTabHasItsSectionOfTheManual(bool hasProblem, bool hasResult, bool isRunning,
                                                            ManualSection? tab, string expected)
        => Assert.Equal(expected, Manual.Url(Manual.SectionFor(hasProblem, hasResult, isRunning, tab)));

    /// <summary>The eight tabs, in their order, carry their eight sections.</summary>
    [Fact]
    public void R125_TheEightTabsCarryTheirSections()
        => _sta.Invoke(() =>
        {
            using TestSettings.Own _ = new();
            MainWindow w = new();
            Assert.Equal(
                new object[]
                {
                    ManualSection.Summary, ManualSection.Frames, ManualSection.Maps, ManualSection.HotPixels,
                    ManualSection.MatchWithLights, ManualSection.Index, ManualSection.AduScaleExplained, ManualSection.Report,
                },
                w.Tabs.Items.Cast<TabItem>().Select(t => t.Tag).ToArray());
        });

    /// <summary>
    /// F1, pressed on an element inside the window, opens the manual at the section of what is on
    /// screen and logs the address: the opening's before a folder, the table of what to do on the
    /// problem card, the selected tab's once the tabs show. The key goes through WPF's own
    /// translation of a key into a command, so an input binding that took F1 away would fail here.
    /// </summary>
    [Fact]
    public async Task R125_HelpAndF1OpenTheManualAtTheSectionOnScreen()
    {
        using TestSettings.Own _ = new();
        using SessionFixture good = WindowFixture.Session();
        using TempWorkspace ws = new();

        await _sta.InvokeAsync(async () =>
        {
            using LogCapture capture = LogCapture.Start();
            List<string> opened = [];
            MainViewModel vm = new();
            MainWindow w = new(vm) { OpenAddress = opened.Add };

            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#opening-a-folder", opened.Last());

            await vm.OpenAsync(Path.Combine(ws.Dir, "missing"));
            Assert.True(vm.HasProblem);
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#when-something-goes-wrong", opened.Last());

            await vm.OpenAsync(good.Root);
            Assert.True(vm.HasResult);
            Assert.False(vm.HasProblem || vm.IsRunning);
            w.Tabs.SelectedIndex = 2;
            Assert.True(PressF1((UIElement)((TabItem)w.Tabs.SelectedItem).Content));
            Assert.Equal(Page + "#maps", opened.Last());

            w.Tabs.SelectedIndex = 4;
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#match-with-lights", opened.Last());
            Assert.Equal(4, opened.Count);

            Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Message == "Help opened: " + Page + "#maps");
        });
    }

    /// <summary>
    /// The top bar's Help button runs the Help command and shows a drawn icon and the word, with
    /// its tooltip, just before About; About opens from its own click and its tooltip names no
    /// key; and the whole bar fits the window's minimum width, 1160 px, in the window's text mode,
    /// no taller than at a wider width.
    /// </summary>
    /// <param name="romanian">Whether the window speaks Romanian.</param>
    /// <param name="word">The button's word.</param>
    /// <param name="tip">The button's tooltip.</param>
    /// <param name="about">About's word.</param>
    /// <param name="aboutTip">About's tooltip.</param>
    [Theory]
    [InlineData(false, "Help", "The manual, at the section of this tab (F1)", "About", "Version, licence, components")]
    [InlineData(true, "Ajutor", "Manualul, la sectiunea tabului curent (F1)", "Despre", "Versiune, licenta, componente")]
    public void R125_TheHelpButtonShowsAnIconAndTheWord(bool romanian, string word, string tip, string about, string aboutTip)
        => _sta.Invoke(() =>
        {
            using TestSettings.Own _ = new();
            using IDisposable lang = Ui.Use(romanian ? UiLanguage.Romanian : UiLanguage.English);

            // a window never shown renders white, its template unapplied: its content is moved
            // into a plain host with the window's data, commands and text mode, at the window's
            // minimum width
            MainWindow w = new();
            Assert.Equal(1160, w.MinWidth);
            UIElement content = (UIElement)w.Content;
            w.Content = null;
            Border host = new() { Child = content, DataContext = w.DataContext, Background = Brushes.Black };
            TextOptions.SetTextFormattingMode(host, TextOptions.GetTextFormattingMode(w));
            foreach (CommandBinding b in w.CommandBindings)
            {
                host.CommandBindings.Add(b);
            }

            WindowSmokeTests.Render(host, (int)w.MinWidth, 560, romanian ? "ro-help-top-bar" : "en-help-top-bar");

            List<Button> buttons = Descendants(host).OfType<Button>().ToList();
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
            Assert.True(aboutButton.TranslatePoint(new Point(aboutButton.ActualWidth, 0), host).X < w.MinWidth - 20 + 0.5);

            // and the folder's heading, which wraps in what the other columns leave, stays on at
            // most two lines: at 1100 px it took four and the bar 95 px, at 1020 px one letter a
            // line and the bar 743 px (measured 2026-11-16, ADR 0061)
            Border bar = (Border)((Grid)content).Children[0];
            Assert.True(bar.ActualHeight < 80, "the top bar is " + bar.ActualHeight + " px tall");

            // Help sits between the export button and About, in the same row
            Panel row = (Panel)VisualTreeHelper.GetParent(help);
            int at = row.Children.IndexOf(help);
            Assert.Same(aboutButton, row.Children[at + 1]);
            Assert.Equal(Ui.T("Write the files...", "Scrie fisierele..."), ((Button)row.Children[at - 1]).Content);
        });

    /// <summary>Raises a key-down of F1 on <paramref name="target"/>, as the keyboard would with
    /// the focus there; returns whether something handled it.</summary>
    private static bool PressF1(UIElement target)
    {
        KeyEventArgs e = new(Keyboard.PrimaryDevice, new KeySource(), 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent };
        target.RaiseEvent(e);
        return e.Handled;
    }

    /// <summary>The presentation source a key event names: a window never shown has none, and
    /// nothing here is drawn through it.</summary>
    private sealed class KeySource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;

        public override bool IsDisposed => false;

        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    /// <summary>Every element under <paramref name="node"/> in the visual tree, depth first.</summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(node, i);
            yield return child;
            foreach (DependencyObject d in Descendants(child))
            {
                yield return d;
            }
        }
    }
}
