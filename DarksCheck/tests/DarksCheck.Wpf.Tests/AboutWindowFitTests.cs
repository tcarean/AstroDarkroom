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
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DarksCheck.Tests;
using NLog;

namespace DarksCheck.Wpf.Tests;

/// <summary>
/// About fits the work area of its screen (R-121, ADR 0027), AstroPhotoAnalyzer's tests of its
/// ADR 0070 taken with its design: no taller than the area, its cards scrolling when their content
/// is taller, the Close button in view, and as before where it fits. No test here shows a window:
/// the content is laid out at the height a capped window leaves it, as WindowSmokeTests renders it;
/// the keys are raised as events through a stand-in presentation source, the fit through the
/// window's own OnContentRendered; the arithmetic and Windows' answer are tested apart.
/// </summary>
[Collection("window")]
public class AboutWindowFitTests
{
    /// <summary>The six keys the cards scroll by while they can.</summary>
    private static readonly Key[] ScrollKeys = [Key.Down, Key.Up, Key.PageDown, Key.PageUp, Key.End, Key.Home];

    private readonly StaFixture _sta;

    public AboutWindowFitTests(StaFixture sta) => _sta = sta;

    /// <summary>
    /// Where the window's top goes so that it stays inside a work area: the launcher's cases
    /// (AstroDarkroom's R-016), on this window's arithmetic.
    /// </summary>
    [Theory]
    [InlineData(100.0, 600.0, 0.0, 800.0, 100.0)]          // it fits where it opens: unchanged
    [InlineData(300.0, 600.0, 0.0, 800.0, 200.0)]          // past the bottom: up until its bottom is the area's
    [InlineData(-60.0, 600.0, 0.0, 800.0, 0.0)]            // past the top: down to the area's top
    [InlineData(-60.0, 990.0, 0.0, 800.0, 0.0)]            // taller than the area: at its top (the window is cut to the area's height)
    [InlineData(-60.0, 800.0, 0.0, 800.0, 0.0)]            // exactly as tall as the area: at its top
    [InlineData(500.0, 600.0, 40.0, 800.0, 240.0)]         // an area that does not start at 0, a taskbar above it
    [InlineData(100.0, 600.0, -1080.0, 1040.0, -640.0)]    // a screen above the primary, at negative coordinates
    public void R121_TheWindowsTopKeepsItInsideTheWorkArea(double top, double height, double areaTop, double areaHeight, double expected)
        => Assert.Equal(expected, ScreenArea.TopInside(top, height, areaTop, areaHeight));

    /// <summary>
    /// On a work area lower than the window's content, the window is capped at the area's height.
    /// Laid out at a height under its own, as a capped window leaves it, the content keeps the
    /// Close button inside that height and its cards scroll; End and Home pressed on the window
    /// move them to either end, and Enter is left to the Close button.
    /// </summary>
    [Fact]
    public Task R121_OnALowWorkAreaTheCardsScrollAndCloseStaysInView()
        => _sta.InvokeAsync(async () =>
        {
            AboutWindow about = new(new Rect(0, 0, 1280, 680));
            // A window never shown resolves its bindings in a later dispatcher pass; laid out
            // before it, the content would measure short.
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            FrameworkElement content = (FrameworkElement)about.Content;

            // 560 DIP, well under the content's own height: a window capped at 680 leaves its
            // content 680 less its title bar and frame, about 640 DIP at 100 %.
            WindowSmokeTests.Render(content, 600, 560, "about-low-work-area");

            Assert.Equal(680, about.MaxHeight);
            Rect close = Bounds(about.CloseButton, content);
            Assert.True(close.Top >= 0 && close.Bottom <= 560, FormattableString.Invariant($"the Close button lies at {close.Top:0}..{close.Bottom:0} DIP of 560"));
            Assert.True(about.Cards.ScrollableHeight > 0, "the cards do not scroll");

            Assert.True(PressKey(about, Key.End));
            await Settle(content);
            Assert.Equal(about.Cards.ScrollableHeight, about.Cards.VerticalOffset);
            Assert.True(PressKey(about, Key.Home));
            await Settle(content);
            Assert.Equal(0, about.Cards.VerticalOffset);
            Assert.False(PressKey(about, Key.Enter));
            about.Close();
        });

    /// <summary>
    /// On a work area where the window fits, it is as before: the content laid out at its own
    /// height shows no scroll bar, the Close button keeps its gaps of 16 DIP above and 20 below,
    /// and the window takes no key, so the arrows still move the focus between its links.
    /// </summary>
    [Fact]
    public Task R121_WhereTheWindowFitsNothingChanges()
        => _sta.InvokeAsync(async () =>
        {
            AboutWindow about = new(new Rect(0, 0, 1920, 1040));
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            FrameworkElement content = (FrameworkElement)about.Content;
            content.Measure(new Size(600, double.PositiveInfinity));
            int natural = (int)Math.Ceiling(content.DesiredSize.Height);

            WindowSmokeTests.Render(content, 600, natural);

            Assert.Equal(1040, about.MaxHeight);
            Assert.True(natural < 1040, FormattableString.Invariant($"the content is {natural} DIP high"));
            Assert.Equal(Visibility.Collapsed, about.Cards.ComputedVerticalScrollBarVisibility);
            Rect cards = Bounds(about.Cards, content);
            Rect close = Bounds(about.CloseButton, content);
            Assert.Equal(16, close.Top - cards.Bottom, 3);
            Assert.Equal(20, content.ActualHeight - close.Bottom, 3);
            foreach (Key key in ScrollKeys)
            {
                Assert.False(PressKey(about, key), key + " was taken where the cards fit");
            }

            about.Close();
        });

    /// <summary>
    /// The work area is the one of the window's own screen: a hidden window given a handle on the
    /// primary screen gets from Windows the area WPF knows for that screen, and a window without a
    /// handle gets it through the fallback. On a machine with one screen the two answers are
    /// equal, so the area alone cannot tell them apart: the fallback writes a Debug line, which a
    /// window with a handle must not cause.
    /// </summary>
    [Fact]
    public void R121_TheWorkAreaIsTheOneOfTheWindowsScreen()
        => _sta.Invoke(() =>
        {
            using LogCapture capture = LogCapture.Start();
            Rect primary = SystemParameters.WorkArea;
            Window hidden = new()
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = primary.Left + 100,
                Top = primary.Top + 100,
                Width = 200,
                Height = 100,
                ShowInTaskbar = false,
                ShowActivated = false,
            };
            Assert.Equal(primary, ScreenArea.WorkAreaOf(hidden));

            new WindowInteropHelper(hidden).EnsureHandle();
            try
            {
                Assert.Equal(primary, ScreenArea.WorkAreaOf(hidden));
            }
            finally
            {
                hidden.Close();
            }

            Assert.DoesNotContain(capture.Events, e => e.Message.Contains("could not be read", StringComparison.Ordinal));
        });

    /// <summary>
    /// When its content is first rendered, the window keeps its top inside the work area and
    /// writes its size and place, the area and whether its cards scroll to the diagnostic log,
    /// at Debug. A window never shown is 0 by 0 DIP, so a top of 900 in an area 680 DIP high
    /// comes back to 680.
    /// </summary>
    [Fact]
    public Task R121_WhenItsContentIsRenderedTheAboutWindowFitsAndLogsItsSizePlaceAndArea()
        => _sta.InvokeAsync(async () =>
        {
            AboutWindow about = new(new Rect(0, 0, 1280, 680)) { Left = 100, Top = 900 };
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            WindowSmokeTests.Render((FrameworkElement)about.Content, 600, 560);
            using LogCapture capture = LogCapture.Start();

            typeof(Window).GetMethod("OnContentRendered", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(about, [EventArgs.Empty]);

            Assert.Equal(680, about.Top);
            LogCapture.Captured line = Assert.Single(capture.Events);
            Assert.Equal(LogLevel.Debug, line.Level);
            Assert.Equal("Window", line.Logger);
            Assert.Equal("About shown 0x0 at (100,680); work area 1280x680 at (0,0); cards scroll: yes", line.Message);
            about.Close();
        });

    /// <summary>
    /// Presses a key on a window never shown: a <c>PreviewKeyDown</c> raised on it, as the input
    /// system raises it before the focused element's <c>KeyDown</c>.
    /// </summary>
    /// <returns>Whether a handler of the window took the key.</returns>
    private static bool PressKey(Window window, Key key)
    {
        KeyEventArgs e = new(Keyboard.PrimaryDevice, new KeySource(), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        window.RaiseEvent(e);
        return e.Handled;
    }

    /// <summary>An element's bounds in the coordinates of one of its ancestors.</summary>
    private static Rect Bounds(FrameworkElement element, Visual ancestor)
        => element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    /// <summary>
    /// Lets the scroll viewer carry out a queued scroll: it does so in its next layout, which the
    /// dispatcher runs before it is idle.
    /// </summary>
    private static async Task Settle(UIElement root)
    {
        for (int i = 0; i < 3; i++)
        {
            root.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        }
    }

    /// <summary>
    /// The presentation source a key event names: a window never shown has none, and nothing
    /// here is drawn through it.
    /// </summary>
    private sealed class KeySource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;

        public override bool IsDisposed => false;

        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
