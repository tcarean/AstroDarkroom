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

using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CopyWcs.Tests;
using NLog;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The Help button and F1 (R-039, ADR 0016): the manual's three addresses, the section of what
/// the outcome card shows, F1 pressed through the window's own key handler (a held key's repeats
/// too) and the button clicked, and the guard that hands the shell only a web address. The
/// browser never starts: the window's <c>OpenAddress</c> is replaced and the address kept.
/// The button's place, icon and words are rendered in <see cref="WindowSmokeTests"/>.
/// </summary>
[Collection("window")]
public class HelpTests
{
    private const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/CopyWcs";

    private readonly StaFixture _sta;

    public HelpTests(StaFixture sta) => _sta = sta;

    /// <summary>The anchors GitHub makes from the page's three headings, and which the card calls for.</summary>
    [Fact]
    public void R039_TheManualOpensAtTheSectionOfWhatTheCardShows()
    {
        Assert.Equal(Page, Manual.Page);
        Assert.Equal(Page + "#the-window", Manual.Url(ManualSection.TheWindow));
        Assert.Equal(Page + "#the-result", Manual.Url(ManualSection.TheResult));
        Assert.Equal(Page + "#when-something-goes-wrong", Manual.Url(ManualSection.WhenSomethingGoesWrong));

        // no outcome yet; a result; a refusal; a warning on a copy that went on
        Assert.Equal(ManualSection.TheWindow, Manual.SectionFor(hasOutcome: false, hasError: false, hasWarnings: false));
        Assert.Equal(ManualSection.TheResult, Manual.SectionFor(hasOutcome: true, hasError: false, hasWarnings: false));
        Assert.Equal(ManualSection.WhenSomethingGoesWrong, Manual.SectionFor(hasOutcome: true, hasError: true, hasWarnings: false));
        Assert.Equal(ManualSection.WhenSomethingGoesWrong, Manual.SectionFor(hasOutcome: true, hasError: false, hasWarnings: true));
        Assert.Equal(ManualSection.WhenSomethingGoesWrong, Manual.SectionFor(hasOutcome: true, hasError: true, hasWarnings: true));
    }

    /// <summary>
    /// F1, pressed on the window and on a box inside it, and the Help button open the section of
    /// the card on screen: the window before any run, the result after a preview, the problems
    /// after a refusal and after a forced preview with its warning (R-039).
    /// </summary>
    [Fact]
    public Task R039_F1AndHelpOpenTheSectionOfTheCardOnScreen()
        => _sta.InvokeAsync(async () =>
        {
            using WindowFixture same = new();
            using WindowFixture wider = new(destWidth: 200);
            List<string> opened = [];
            MainViewModel vm = new() { Source = same.Source, Dest = same.Dest };
            MainWindow w = new(vm) { OpenAddress = opened.Add };
            UIElement inside = FirstTextBox(w);

            Assert.True(PressF1(inside));

            await vm.Preview();
            Assert.True(vm.HasCards && !vm.HasError && !vm.HasWarnings);
            Click(w.HelpButton);

            vm.Dest = wider.Dest;
            await vm.Preview();
            Assert.True(vm.HasError);
            Assert.True(PressF1(w));

            vm.Force = true;
            await vm.Preview();
            Assert.True(vm.HasWarnings && !vm.HasError);
            Click(w.HelpButton);

            Assert.Equal(
                [Page + "#the-window", Page + "#the-result", Page + "#when-something-goes-wrong", Page + "#when-something-goes-wrong"],
                opened);
        });

    /// <summary>
    /// A held F1 opens the manual once: its repeats, some thirty a second, would each open a
    /// browser tab. The repeats go through the window's key handler as the keyboard's would, and
    /// are taken (handled), so no command behind them runs either.
    /// </summary>
    [Fact]
    public void R039_AHeldF1OpensTheManualOnce()
        => _sta.Invoke(() =>
        {
            List<string> opened = [];
            MainWindow w = new() { OpenAddress = opened.Add };

            Assert.True(PressF1(w));
            Assert.True(PressF1(w, isRepeat: true));
            Assert.True(PressF1(w, isRepeat: true));

            Assert.Equal([Page + "#the-window"], opened);
        });

    /// <summary>
    /// F1 no longer reaches About: the window binds no Help command, so the key's default gesture
    /// runs nothing, and the About button opens its window on its own click (R-035, R-039).
    /// </summary>
    [Fact]
    public void R039_F1IsNoLongerTheAboutWindowsKey()
        => _sta.Invoke(() =>
        {
            MainWindow w = new();
            Assert.DoesNotContain(w.CommandBindings.Cast<System.Windows.Input.CommandBinding>(), b => b.Command == ApplicationCommands.Help);
            Assert.Null(w.AboutButton.Command);
            Assert.Null(w.HelpButton.Command);
        });

    /// <summary>
    /// Only an absolute http or https address reaches the shell: any other text would be resolved
    /// against the working folder and the PATH, and a <c>file:</c> one would open a file. One
    /// that is not is a Debug line, and nothing starts (R-039).
    /// </summary>
    [Fact]
    public void R039_OnlyAWebAddressReachesTheShell()
    {
        Assert.True(ExternalLink.IsWebAddress(Page + "#the-result", out Uri? uri));
        Assert.Equal(Page + "#the-result", uri!.AbsoluteUri);
        Assert.True(ExternalLink.IsWebAddress("http://example.org/", out _));
        Assert.False(ExternalLink.IsWebAddress("notepad.exe", out _));
        Assert.False(ExternalLink.IsWebAddress("THIRD-PARTY-NOTICES.md", out _));
        Assert.False(ExternalLink.IsWebAddress("file:///C:/Windows/notepad.exe", out _));
        Assert.False(ExternalLink.IsWebAddress("mailto:someone@example.org", out _));
        Assert.False(ExternalLink.IsWebAddress("", out _));

        using LogCapture capture = LogCapture.Start();
        ExternalLink.Open("notepad.exe");

        LogCapture.Captured line = Assert.Single(capture.Events, e => e.Logger == "Window");
        Assert.Equal(LogLevel.Debug, line.Level);
        Assert.Equal("the link notepad.exe is not a web address, so it was not opened", line.Message);
    }

    /// <summary>The first text box of the window's tree, an element inside the window that takes keys.</summary>
    private static System.Windows.Controls.TextBox FirstTextBox(MainWindow w)
    {
        var content = (FrameworkElement)w.Content;
        content.Measure(new Size(720, 560));
        content.Arrange(new Rect(0, 0, 720, 560));
        content.UpdateLayout();
        return Descendants(content).OfType<System.Windows.Controls.TextBox>().First();
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

    /// <summary>A click of a button, as the mouse's would raise it.</summary>
    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    /// <summary>
    /// F1 pressed on an element, through the preview event the keyboard raises first, so the
    /// window's own key handler reads it wherever the focus is. After ImageVisualCompare's test.
    /// </summary>
    /// <param name="target">The element the key goes to: the window or one inside it.</param>
    /// <param name="isRepeat">True for a repeat of the key held down.</param>
    /// <returns>Whether a handler took the key.</returns>
    internal static bool PressF1(UIElement target, bool isRepeat = false)
    {
        KeyEventArgs e = new(Keyboard.PrimaryDevice, new KeySource(), 0, Key.F1) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        if (isRepeat)
        {
            // WPF marks a repeat from the keyboard's state, through a member of its own; the test
            // marks it the same way.
            typeof(KeyEventArgs)
                .GetMethod("SetRepeat", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(e, [true]);
        }

        target.RaiseEvent(e);
        return e.Handled;
    }

    /// <summary>The presentation source a key event names: a window never shown has none, and
    /// nothing is drawn through this one.</summary>
    private sealed class KeySource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;

        public override bool IsDisposed => false;

        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
