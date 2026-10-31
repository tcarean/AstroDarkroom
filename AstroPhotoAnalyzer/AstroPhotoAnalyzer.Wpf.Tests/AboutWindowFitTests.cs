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

// AboutWindowFitTests.cs - "Despre" fits the work area of its screen (R-158, ADR 0070): no taller
// than the area, its cards scrolling when their content is taller, the Close button in view. No
// test here shows a window: the content is laid out at the height a capped window leaves it, as
// WindowSmokeTests renders it, and the window's arithmetic and Windows' answer are tested apart.

using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using AstroPhotoAnalyzer.Tests;
using NLog;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("fereastra")]
public class AboutWindowFitTests
{
    readonly StaFixture _sta;

    public AboutWindowFitTests(StaFixture sta) => _sta = sta;

    /// <summary>
    /// Where the window's top goes so that it stays inside a work area: the launcher's cases
    /// (AstroDarkroom's R-016), on this window's arithmetic.
    /// </summary>
    [Theory]
    [InlineData(100.0, 600.0, 0.0, 800.0, 100.0)]   // it fits where it opens: unchanged
    [InlineData(300.0, 600.0, 0.0, 800.0, 200.0)]   // past the bottom: up until its bottom is the area's (R-158's example)
    [InlineData(-60.0, 600.0, 0.0, 800.0, 0.0)]     // past the top: down to the area's top
    [InlineData(-60.0, 862.0, 0.0, 800.0, 0.0)]     // taller than the area: at its top (the window is cut to the area's height)
    [InlineData(-60.0, 800.0, 0.0, 800.0, 0.0)]     // exactly as tall as the area: at its top
    [InlineData(500.0, 600.0, 40.0, 800.0, 240.0)]  // an area that does not start at 0, a taskbar above it
    [InlineData(100.0, 600.0, -1080.0, 1040.0, -640.0)]   // a screen above the primary, at negative coordinates
    public void R158_TheWindowsTopKeepsItInsideTheWorkArea(double top, double height, double areaTop, double areaHeight, double expected)
        => Assert.Equal(expected, ScreenArea.TopInside(top, height, areaTop, areaHeight));

    /// <summary>
    /// On a work area lower than the window's content, the window is capped at the area's height.
    /// Laid out at a height under its own, as a capped window leaves it, the content keeps the
    /// Close button inside that height, its cards scroll, and the keys move them to either end.
    /// </summary>
    [Fact]
    public Task R158_OnALowWorkAreaTheCardsScrollAndCloseStaysInView()
        => _sta.InvokeAsync(async () =>
        {
            AboutWindow about = new(new Rect(0, 0, 1280, 680));
            // A window never shown resolves its bindings in a later dispatcher pass; laid out
            // before it, the content would measure short.
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            var content = (FrameworkElement)about.Content;

            // 560 DIP, under the content's 808 to 823: a window capped at 680 leaves its content
            // 680 less the title bar and the frame, 641 DIP at 100 % on the machine measured.
            WindowSmokeTests.Render(content, 600, 560, "despre-ecran-mic");

            Assert.Equal(680, about.MaxHeight);
            Rect close = about.CloseButton.TransformToAncestor(content).TransformBounds(new Rect(about.CloseButton.RenderSize));
            Assert.True(close.Top >= 0 && close.Bottom <= 560,
                FormattableString.Invariant($"the Close button lies at {close.Top:0}..{close.Bottom:0} DIP of 560"));
            Assert.True(about.Cards.ScrollableHeight > 0, "the cards do not scroll");

            Assert.True(about.Scroll(Key.End));
            await Settle(content);
            Assert.Equal(about.Cards.ScrollableHeight, about.Cards.VerticalOffset);
            Assert.True(about.Scroll(Key.Home));
            await Settle(content);
            Assert.Equal(0, about.Cards.VerticalOffset);
            Assert.False(about.Scroll(Key.Enter));
            about.Close();
        });

    /// <summary>
    /// On a work area where the window fits, nothing scrolls: the content laid out at its own
    /// height leaves the cards nothing to scroll, and the Close button sits under them, as before.
    /// </summary>
    [Fact]
    public Task R158_WhereTheWindowFitsNothingScrolls()
        => _sta.InvokeAsync(async () =>
        {
            AboutWindow about = new(new Rect(0, 0, 1920, 1040));
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            var content = (FrameworkElement)about.Content;
            content.Measure(new Size(600, double.PositiveInfinity));
            int natural = (int)Math.Ceiling(content.DesiredSize.Height);

            WindowSmokeTests.Render(content, 600, natural);

            Assert.Equal(1040, about.MaxHeight);
            Assert.Equal(0, about.Cards.ScrollableHeight);
            Rect close = about.CloseButton.TransformToAncestor(content).TransformBounds(new Rect(about.CloseButton.RenderSize));
            Assert.True(close.Bottom <= natural, FormattableString.Invariant($"the Close button ends at {close.Bottom:0} DIP of {natural}"));
            about.Close();
        });

    /// <summary>
    /// The work area is the one of the window's own screen: a hidden window given a handle on the
    /// primary screen gets from Windows the same area WPF knows for that screen, and a window
    /// without a handle gets it through the fallback.
    /// </summary>
    [Fact]
    public void R158_TheWorkAreaIsTheOneOfTheWindowsScreen()
        => _sta.Invoke(() =>
        {
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
        });

    /// <summary>
    /// Once laid out, the window writes its size and place, its screen's work area and whether
    /// its cards scroll to the diagnostic log, at Debug (R-171).
    /// </summary>
    [Fact]
    public Task R171_TheAboutWindowLogsItsSizeTheWorkAreaAndWhetherItsCardsScroll()
        => _sta.InvokeAsync(async () =>
        {
            Rect area = new(0, 0, 1280, 680);
            AboutWindow about = new(area);
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            WindowSmokeTests.Render((FrameworkElement)about.Content, 600, 560);
            using LogCapture capture = LogCapture.Start();

            about.FitInto(area);

            LogCapture.Captured line = Assert.Single(capture.Events);
            Assert.Equal(LogLevel.Debug, line.Level);
            Assert.Equal("Window", line.Logger);
            Assert.StartsWith("About shown ", line.Message, StringComparison.Ordinal);
            Assert.EndsWith("; work area 1280x680 at (0,0); cards scroll: yes", line.Message, StringComparison.Ordinal);
            about.Close();
        });

    /// <summary>
    /// Lets the scroll viewer carry out a queued scroll: it does so in its next layout, which the
    /// dispatcher runs before it is idle.
    /// </summary>
    static async Task Settle(UIElement root)
    {
        for (int i = 0; i < 3; i++)
        {
            root.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        }
    }
}
