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

// Copied from LightsCheck, its fit to the screen included (its ADR 0031; here ADR 0027);
// a fix goes there first (ADR 0009).

using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using DarksCheck.Core;
using NLog;

namespace DarksCheck.Wpf;

/// <summary>
/// The "About" window: version, copyright, the GPLv3 notice and the third-party list, fitted to
/// the work area of its screen (R-121).
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>
    /// The window over a new <see cref="AboutViewModel"/>, with the icon's largest frame as its
    /// logo and the dark title bar asked for once it has a handle. It is no taller than
    /// <paramref name="workArea"/>, is kept inside it once shown, and its cards scroll from the
    /// keyboard too (R-121, ADR 0027).
    /// </summary>
    /// <param name="workArea">
    /// The work area of the screen the window opens on, in device-independent pixels: the main
    /// window's, from <see cref="ScreenArea.WorkAreaOf"/>.
    /// </param>
    public AboutWindow(Rect workArea)
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
        Logo.Source = AppIcon.Largest();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        MaxHeight = workArea.Height;
        // Not at Loaded: a window sized to its content still has its first height then
        // (AstroPhotoAnalyzer measured 436 DIP for a window of 862), so the guard and the log
        // line would read a wrong one (its ADR 0070).
        ContentRendered += (_, _) => FitInto(workArea);
        PreviewKeyDown += ScrollCards;
    }

    /// <summary>
    /// Keeps the shown window inside its screen's work area (R-121), and writes its size and
    /// place, the area and whether its cards scroll to the diagnostic log at Debug. Called once
    /// its content is first rendered, when its size is final. WPF has placed it there already (a
    /// window centred on its owner is clamped into the owner screen's work area); the move holds
    /// that place as a guard, so the requirement does not rest on WPF's placement alone, as in
    /// the launcher (AstroDarkroom's ADR 0013).
    /// </summary>
    /// <param name="workArea">The work area of the window's screen, in device-independent pixels.</param>
    internal void FitInto(Rect workArea)
    {
        Top = ScreenArea.TopInside(Top, ActualHeight, workArea.Top, workArea.Height);
        if (Log.IsDebugEnabled)
        {
            Log.Debug("About shown " + LogText.Num(ActualWidth) + "x" + LogText.Num(ActualHeight)
                + " at (" + LogText.Num(Left) + "," + LogText.Num(Top) + "); work area "
                + LogText.Num(workArea.Width) + "x" + LogText.Num(workArea.Height)
                + " at (" + LogText.Num(workArea.Left) + "," + LogText.Num(workArea.Top) + "); cards scroll: "
                + LogText.Yes(Cards.ScrollableHeight > 0));
        }
    }

    /// <summary>
    /// Scrolls the cards by a key while they can scroll (R-121): Up and Down by a line, Page Up
    /// and Page Down by a page, Home and End to either end. Where the cards fit no key is taken,
    /// so the arrows still move the focus between the links and the Close button, as before the
    /// fit. The scroll viewer carries the move out at its next layout.
    /// </summary>
    /// <param name="key">The key pressed.</param>
    /// <returns>True when the cards can scroll and the key is one of the six: they were asked to scroll.</returns>
    internal bool Scroll(Key key)
    {
        if (Cards.ScrollableHeight <= 0)
        {
            return false;
        }

        switch (key)
        {
            case Key.Down: Cards.LineDown(); return true;
            case Key.Up: Cards.LineUp(); return true;
            case Key.PageDown: Cards.PageDown(); return true;
            case Key.PageUp: Cards.PageUp(); return true;
            case Key.End: Cards.ScrollToBottom(); return true;
            case Key.Home: Cards.ScrollToTop(); return true;
            default: return false;
        }
    }

    /// <summary>
    /// The window's keys for the cards: the focus stays on the Close button or on a link, neither
    /// of which scrolls, so while the cards can scroll the keys a scroll viewer answers are passed
    /// to it here. Any other key, and every key where the cards fit, goes on as before: Enter and
    /// Esc to the Close button, the arrows to the focus.
    /// </summary>
    /// <param name="sender">The window; not used.</param>
    /// <param name="e">The key; marked handled when it scrolled the cards.</param>
    private void ScrollCards(object sender, KeyEventArgs e)
    {
        if (Scroll(e.Key))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Opens the link in the system browser; UseShellExecute is what makes a URL open on .NET Core.
    /// A link that cannot be opened (no browser starts, or the link is relative and has no
    /// absolute address) is a Debug line in the diagnostic log with the reason, and the window stays.
    /// </summary>
    /// <param name="sender">This window, on which the handler is set; not used.</param>
    /// <param name="e">The navigation request; marked handled, its URI opened when it can be.</param>
    private void OpenLink(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // OriginalString, not AbsoluteUri: a relative link throws there, which is what this
            // catch took, and would throw again out of the handler; the log never stops the
            // window (ADR 0025).
            Log.Debug(ex, "the link " + e.Uri.OriginalString + " could not be opened in a browser");
        }
    }

    /// <summary>The diagnostic log: a link that could not be opened, the window's size and place once shown (R-101).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    /// <summary>The "Close" button: closes the window.</summary>
    /// <param name="sender">The button clicked.</param>
    /// <param name="e">The click's arguments, not used.</param>
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
