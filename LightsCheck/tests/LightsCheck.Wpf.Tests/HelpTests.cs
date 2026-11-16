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
using LightsCheck.Core;
using LightsCheck.Tests;
using NLog;

namespace LightsCheck.Wpf.Tests;

/// <summary>
/// The Help button and F1 open the manual at the section of what is on screen (R-091, ADR 0060),
/// as FlatCheck's do (its R-141). The browser is never started: the window's OpenAddress is
/// replaced by a stand-in that keeps the address.
/// </summary>
[Collection("window")]
public class HelpTests
{
    private const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/LightsCheck";

    private readonly StaFixture _sta;

    public HelpTests(StaFixture sta) => _sta = sta;

    /// <summary>
    /// Each state of the window and each tab has its section of the manual; the addresses are
    /// written out, as the wiki's headings make them. Every screen shown instead of the tabs (no
    /// result, or the wait screen over everything) opens "Opening a session".
    /// </summary>
    [Theory]
    [InlineData(false, false, null, Page + "#opening-a-session")]
    [InlineData(false, false, ManualSection.Quality, Page + "#opening-a-session")]
    [InlineData(false, true, null, Page + "#opening-a-session")]
    [InlineData(true, true, ManualSection.Frames, Page + "#opening-a-session")]
    [InlineData(true, false, null, Page + "#opening-a-session")]
    [InlineData(true, false, ManualSection.Summary, Page + "#summary")]
    [InlineData(true, false, ManualSection.Frames, Page + "#frames")]
    [InlineData(true, false, ManualSection.Dithering, Page + "#dithering")]
    [InlineData(true, false, ManualSection.Quality, Page + "#quality")]
    [InlineData(true, false, ManualSection.Rejected, Page + "#rejected")]
    [InlineData(true, false, ManualSection.Report, Page + "#report")]
    public void R091_EachStateAndTabHasItsSectionOfTheManual(bool hasResult, bool isRunning, ManualSection? tab, string expected)
        => Assert.Equal(expected, Manual.Url(Manual.SectionFor(hasResult, isRunning, tab)));

    /// <summary>The six tabs, in their order, carry their six sections.</summary>
    [Fact]
    public void R091_TheSixTabsCarryTheirSections()
        => _sta.Invoke(() =>
        {
            MainWindow w = new();
            Assert.Equal(
                new object[]
                {
                    ManualSection.Summary, ManualSection.Frames, ManualSection.Dithering,
                    ManualSection.Quality, ManualSection.Rejected, ManualSection.Report,
                },
                w.Tabs.Items.Cast<TabItem>().Select(t => t.Tag).ToArray());
        });

    /// <summary>
    /// F1, pressed on an element inside the window, opens the manual at the section of what is on
    /// screen and logs the address: the opening's before a folder, on the problem card, after a
    /// stopped run and during a run; the selected tab's once the tabs show. The key goes through
    /// WPF's own translation of a key into a command, so an input binding that took F1 away would
    /// fail here.
    /// </summary>
    [Fact]
    public async Task R091_HelpAndF1OpenTheManualAtTheSectionOnScreen()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace ws = new();
        using LogCapture capture = LogCapture.Start();

        await _sta.InvokeAsync(async () =>
        {
            List<string> opened = [];
            MainViewModel vm = new();
            MainWindow w = new(vm) { OpenAddress = u => opened.Add(u.AbsoluteUri) };

            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#opening-a-session", opened[^1]);

            await vm.OpenAsync(Path.Combine(ws.Dir, "missing"));
            Assert.True(vm.HasProblem);
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#opening-a-session", opened[^1]);

            Task stopped = vm.OpenAsync(f.Root);
            vm.CancelPending();
            await stopped;
            Assert.True(vm.HasFolder && !vm.HasProblem && !vm.HasResult);
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#opening-a-session", opened[^1]);

            Task run = vm.OpenAsync(f.Root);
            Assert.True(vm.IsRunning);
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#opening-a-session", opened[^1]);
            await run;
            Assert.True(vm.HasResult && !vm.IsRunning);

            w.Tabs.SelectedIndex = 2;
            Assert.True(PressF1((UIElement)((TabItem)w.Tabs.SelectedItem).Content));
            Assert.Equal(Page + "#dithering", opened[^1]);

            w.Tabs.SelectedIndex = 5;
            Assert.True(PressF1(w.Tabs));
            Assert.Equal(Page + "#report", opened[^1]);
            Assert.Equal(6, opened.Count);
        });

        Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Level == LogLevel.Info && e.Message == "Help opened: " + Page + "#dithering");
    }

    /// <summary>
    /// The top bar's Help button runs the Help command and shows a drawn icon and the word, with
    /// its tooltip, just left of About; About opens from its own click, and its tooltip names no
    /// key. At the window's minimum width the bar fits, with Stop shown too, its widest, and keeps
    /// its height.
    /// </summary>
    [Theory]
    [InlineData(false, "Help", "The manual, at the section of this tab (F1)", "About", "Version, licence, components")]
    [InlineData(true, "Ajutor", "Manualul, la sectiunea tabului curent (F1)", "Despre", "Versiune, licenta, componente")]
    public void R091_TheHelpButtonShowsAnIconAndTheWord(bool romanian, string word, string tip, string about, string aboutTip)
        => _sta.Invoke(() =>
        {
            using IDisposable scope = Ui.Use(romanian ? UiLanguage.Romanian : UiLanguage.English);

            // a window never shown renders white, its template unapplied: its content is moved
            // into a plain host with the window's data and commands, at the window's minimum width
            MainWindow w = new();
            Assert.Equal(1040, w.MinWidth);
            int width = (int)w.MinWidth;
            UIElement content = (UIElement)w.Content;
            w.Content = null;
            Border host = new() { Child = content, DataContext = w.DataContext, Background = Brushes.Black };
            foreach (CommandBinding b in w.CommandBindings)
            {
                host.CommandBindings.Add(b);
            }

            WindowSmokeTests.Render(host, width, 560);
            FrameworkElement bar = (FrameworkElement)((Panel)content).Children[0];
            double idleHeight = bar.ActualHeight;

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

            // Help sits between the export button and About, in the same row
            Panel row = (Panel)VisualTreeHelper.GetParent(help);
            int at = row.Children.IndexOf(help);
            Assert.Same(aboutButton, row.Children[at + 1]);
            Assert.Equal(Ui.T("Write the files...", "Scrie fisierele..."), ((Button)row.Children[at - 1]).Content);

            // the widest bar, during a run: Stop shows; About, the rightmost, still ends at the
            // bar's 20 px right padding, not past it, and the folder's column, left with a few
            // pixels, cuts its texts on one line instead of making the bar taller (the owner's
            // answer of 2026-11-16)
            Button stop = row.Children.OfType<Button>().Single(b => b.Content as string == Ui.T("Stop", "Opreste"));
            stop.Visibility = Visibility.Visible;
            WindowSmokeTests.Render(host, width, 560, romanian ? "ro-help-top-bar" : "en-help-top-bar");
            Assert.True(stop.ActualWidth > 0);
            Assert.True(aboutButton.TranslatePoint(new Point(aboutButton.ActualWidth, 0), host).X < width - 20 + 0.5);
            Assert.Equal(idleHeight, bar.ActualHeight);
        });

    /// <summary>Raises a key-down of F1 on <paramref name="target"/>, as the keyboard would with
    /// the focus there; returns whether something handled it.</summary>
    private static bool PressF1(UIElement target)
    {
        KeyEventArgs e = new(Keyboard.PrimaryDevice, new KeySource(), 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent };
        target.RaiseEvent(e);
        return e.Handled;
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

    /// <summary>
    /// The presentation source a key event names: a window never shown has none, and nothing
    /// here is drawn through it (as in AboutWindowFitTests).
    /// </summary>
    private sealed class KeySource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;

        public override bool IsDisposed => false;

        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
