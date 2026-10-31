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

// AboutWindow.xaml.cs - the "Despre" window: GPLv3 section 5(d) asks an interactive program to
// show its notice. Only the dark title bar, the opening of links and the window's fit to its
// screen remain here (R-158, ADR 0070).

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The "Despre" window (R-119), over an <see cref="AboutViewModel"/>, fitted to the work area
    /// of its screen (R-158).
    /// </summary>
    public partial class AboutWindow : Window
    {
        /// <summary>
        /// Loads the XAML, binds a new <see cref="AboutViewModel"/>, puts the largest icon frame
        /// in the logo and asks for the dark title bar once there is a handle. The window is no
        /// taller than <paramref name="workArea"/>, is kept inside it once shown, and its cards
        /// scroll from the keyboard too (R-158, ADR 0070).
        /// </summary>
        /// <param name="workArea">
        /// The work area of the screen the window opens on, in device-independent pixels: the
        /// main window's, from <see cref="ScreenArea.WorkAreaOf"/>.
        /// </param>
        public AboutWindow(Rect workArea)
        {
            InitializeComponent();
            DataContext = new AboutViewModel();
            Logo.Source = AppIcon.Largest();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            MaxHeight = workArea.Height;
            // Not at Loaded: a window sized to its content still has its first height then (436
            // DIP measured here, for 862), so the guard and the log line would read a wrong one.
            ContentRendered += (_, _) => FitInto(workArea);
            PreviewKeyDown += ScrollCards;
        }

        /// <summary>
        /// The diagnostic log: a link that could not be opened, the window's size and place once
        /// shown (R-171, ADR 0069).
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

        /// <summary>
        /// Opens a hyperlink in the system's browser, through the shell; a refusal by the shell
        /// or a missing handler is swallowed, and written to the diagnostic log at Debug.
        /// </summary>
        /// <param name="sender">The hyperlink; not read.</param>
        /// <param name="e">The navigation; its URI is opened and it is marked handled.</param>
        void OpenLink(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // OriginalString, not AbsoluteUri: a relative link throws there, which is what this
                // catch took, and would throw again out of the handler and close the window (ADR
                // 0069, after DarksCheck's review).
                Log.Debug(ex, "the link " + e.Uri.OriginalString + " could not be opened in a browser");
            }
        }

        /// <summary>
        /// Keeps the shown window inside its screen's work area (R-158), and writes its size and
        /// place, the area and whether its cards scroll to the diagnostic log at Debug. Called once
        /// its content is first rendered, when its size is final. WPF has placed it there already
        /// (a window centred on its owner is clamped into the owner screen's work area); the move
        /// holds that place as a guard, so the requirement does not rest on WPF's placement alone,
        /// as in the launcher (AstroDarkroom ADR 0013).
        /// </summary>
        /// <param name="workArea">The work area of the window's screen, in device-independent pixels.</param>
        internal void FitInto(Rect workArea)
        {
            Top = ScreenArea.TopInside(Top, ActualHeight, workArea.Top, workArea.Height);
            if (Log.IsDebugEnabled)
                Log.Debug("About shown " + LogText.Num(ActualWidth) + "x" + LogText.Num(ActualHeight)
                    + " at (" + LogText.Num(Left) + "," + LogText.Num(Top) + "); work area "
                    + LogText.Num(workArea.Width) + "x" + LogText.Num(workArea.Height)
                    + " at (" + LogText.Num(workArea.Left) + "," + LogText.Num(workArea.Top) + "); cards scroll: "
                    + LogText.Yes(Cards.ScrollableHeight > 0));
        }

        /// <summary>
        /// Scrolls the cards by a key (R-158): Up and Down by a line, Page Up and Page Down by a
        /// page, Home and End to either end. The scroll viewer carries the move out at its next
        /// layout.
        /// </summary>
        /// <param name="key">The key pressed.</param>
        /// <returns>True when the key is one of the six and the cards were asked to scroll.</returns>
        internal bool Scroll(Key key)
        {
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
        /// The window's keys for the cards: the focus stays on the Close button or on a link,
        /// neither of which scrolls, so the keys a scroll viewer answers are passed to it here.
        /// Enter and Esc go on to the Close button as before.
        /// </summary>
        /// <param name="sender">The window; not read.</param>
        /// <param name="e">The key; marked handled when it scrolled the cards.</param>
        void ScrollCards(object sender, KeyEventArgs e)
        {
            if (Scroll(e.Key)) e.Handled = true;
        }

        /// <summary>The close button.</summary>
        /// <param name="sender">The button; not read.</param>
        /// <param name="e">The click; not read.</param>
        void Close(object sender, RoutedEventArgs e) => Close();
    }
}
