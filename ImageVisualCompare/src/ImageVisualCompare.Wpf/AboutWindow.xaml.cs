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

// AboutWindow.xaml.cs - the About window, FlatCheck's in this tool's colours (ADR 0045).
//
// It exists because GPLv3 section 5(d) asks an interactive program to show its copyright notice,
// the absence of warranty and where the licence is, and because the components written by others
// ask for their notices beside ours (R-075). All its text comes through AboutViewModel; here
// remain the window's own things: the logo, the dark caption, the links, and Close.

using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using ImageVisualCompare.Wpf.Interop;
using ImageVisualCompare.Wpf.Logging;
using NLog;

namespace ImageVisualCompare.Wpf;

/// <summary>
/// The modal About window: the icon, the name, the version, the copyright, the GPLv3 notice and
/// the components written by others, all bound to <see cref="AboutViewModel"/> (R-072, R-075).
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>The window's one log line: a link the system would not open.</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    /// <summary>The executable's icon, the same file, as the application's WPF resource.</summary>
    private static readonly Uri IconSource = new("pack://application:,,,/ImageVisualCompare.Wpf;component/Assets/ImageVisualCompare.ico");

    /// <summary>
    /// Builds the window over a new <see cref="AboutViewModel"/>, in the interface's language of
    /// the moment, with the icon's largest frame as its logo and the dark caption.
    /// </summary>
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
        Logo.Source = LargestIconFrame();

        // Before the window is shown, so the caption is never white for a frame (ADR 0018).
        DarkTitleBar.Apply(this);
    }

    /// <summary>
    /// Decodes the icon and picks its frame with the most pixels. An <c>Image</c> given the file
    /// takes the first frame it finds, which may be the 16-pixel one, scaled up and blurred; the
    /// logo wants the 256-pixel frame, scaled down.
    /// </summary>
    /// <returns>The widest frame, frozen.</returns>
    private static BitmapSource LargestIconFrame()
    {
        BitmapDecoder decoder = BitmapDecoder.Create(IconSource, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        frame.Freeze();
        return frame;
    }

    /// <summary>
    /// Opens a link of the window, the licence's or a component's, in the system's browser.
    /// Outside a navigation window a <c>Hyperlink</c> only raises this routed event, caught on the
    /// window's root. <c>UseShellExecute</c> is what opens a URL on .NET; a system with no
    /// browser for it throws, which only writes a line in the log. The process the shell may
    /// return is not watched, so it is disposed at once.
    /// </summary>
    /// <param name="sender">The window, where the link's event arrives.</param>
    /// <param name="e">The navigation; marked handled.</param>
    private void OpenLink(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // The address as written: AbsoluteUri throws again for a relative one, the very case
            // that lands here with an InvalidOperationException.
            Log.Debug(ex, "the link " + e.Uri.OriginalString + " could not be opened");
        }
    }

    /// <summary>Close's handler, also what Enter and Esc press: closes the window.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
