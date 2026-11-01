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

// AboutWindow.xaml.cs - the "Despre" window.
//
// It exists because GPLv3 section 5(d) asks an interactive program to show its copyright
// notice, the absence of warranty and the pointer to the licence - the console gives them
// at --help, the window had nowhere to - and because LibRaw's LGPL asks for the library's
// notice beside ours. All the text comes through AboutViewModel; here remain only the window
// things: the dark title bar, opening the links in the browser, and the window's fit to its
// screen (R-125, ADR 0086), the design of the Astro Darkroom launcher and of AstroPhotoAnalyzer.

using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using NLog;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The modal "Despre" window: the icon, the version, the copyright, the GPLv3 notice and the
    /// third-party components, all bound to <see cref="AboutViewModel"/>, fitted to the work area
    /// of its screen (R-125).
    /// </summary>
    public partial class AboutWindow : Window
    {
        /// <summary>
        /// Builds the window over a new <see cref="AboutViewModel"/>, with the largest icon and
        /// the dark title bar. The window is no taller than <paramref name="workArea"/>, is kept
        /// inside it once shown, and its cards scroll from the keyboard too (R-125, ADR 0086).
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
            // Not at Loaded: a window sized to its content may still have its first height then
            // (AstroPhotoAnalyzer measured 436 DIP there for a window of 862), so the guard and the
            // log line could read a wrong one.
            ContentRendered += (_, _) => FitInto(workArea);
            PreviewKeyDown += ScrollCards;
        }

        /// <summary>The diagnostic log: the window's size and place once shown (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

        /// <summary>Opens a link of the window, the licence's or a component's, in the system's
        /// browser, through <see cref="ExternalLink.Open"/>.</summary>
        /// <param name="sender">The window, where the link's event arrives.</param>
        /// <param name="e">The navigation; marked handled.</param>
        void OpenLink(object sender, RequestNavigateEventArgs e) => ExternalLink.Open(e);

        /// <summary>
        /// Keeps the shown window inside its screen's work area (R-125), and writes its size and
        /// place, the area and whether its cards scroll to the diagnostic log at Debug (R-110).
        /// Called once its content is first rendered, when its size is final. WPF has placed it
        /// there already (a window centred on its owner is clamped into the owner screen's work
        /// area); the move holds that place as a guard, so the requirement does not rest on WPF's
        /// placement alone, as in the launcher (AstroDarkroom ADR 0013).
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
        /// Scrolls the cards by a key while they can scroll (R-125): Up and Down by a line, Page
        /// Up and Page Down by a page, Home and End to either end. Where the cards fit no key is
        /// taken, so the arrows still move the focus between the links and the Close button, as
        /// before the fit. The scroll viewer carries the move out at its next layout.
        /// </summary>
        /// <param name="key">The key pressed.</param>
        /// <returns>True when the cards can scroll and the key is one of the six: they were asked to scroll.</returns>
        internal bool Scroll(Key key)
        {
            if (Cards.ScrollableHeight <= 0) return false;
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
        /// neither of which scrolls, so while the cards can scroll the keys a scroll viewer
        /// answers are passed to it here. Any other key, and every key where the cards fit, goes
        /// on as before: Enter and Esc to the Close button, the arrows to the focus.
        /// </summary>
        /// <param name="sender">The window; not read.</param>
        /// <param name="e">The key; marked handled when it scrolled the cards.</param>
        void ScrollCards(object sender, KeyEventArgs e)
        {
            if (Scroll(e.Key)) e.Handled = true;
        }

        /// <summary>The "Inchide" button's handler: closes the window.</summary>
        /// <param name="sender">The button.</param>
        /// <param name="e">The click.</param>
        void Close(object sender, RoutedEventArgs e) => Close();
    }
}
