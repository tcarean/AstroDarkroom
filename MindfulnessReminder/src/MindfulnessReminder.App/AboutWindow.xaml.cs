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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MindfulnessReminder.Core;

namespace MindfulnessReminder.App;

/// <summary>
/// About: the version, the copyright and the disclaimer of warranty that GPLv3 section 5(d)
/// asks a program displaying legal notices to display.
///
/// It was a MessageBox until the dark theme arrived. A Win32 dialog cannot be themed, and an
/// About box that opens bright white from a dark tray menu looks like a different program,
/// so it became an ordinary window -- which is also the only way it could carry the app icon.
/// Nothing about the notice itself changed: the text is still <see cref="LicenseInfo.AboutText"/>,
/// composed in Core and merely displayed here.
/// </summary>
public partial class AboutWindow : Window
{
    private const string IconResourceUri = "pack://application:,,,/Assets/app.ico";

    /// <summary>
    /// An About window holding <see cref="LicenseInfo.AboutText"/> and the app icon, not yet
    /// shown.
    /// </summary>
    public AboutWindow()
    {
        InitializeComponent();

        AboutBody.Text = LicenseInfo.AboutText;
        AppIcon.Source = LoadAppIcon();
    }

    /// <summary>
    /// The title bar is not WPF's to paint, so the theme merged in the XAML stops at the
    /// client area and the caption has to be asked for separately. This is the first moment
    /// there is an HWND to ask about, and it is still before the first paint.
    /// </summary>
    /// <param name="e">Passed on to the base class.</param>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        ImmersiveDarkMode.Apply(this);
    }

    /// <summary>
    /// The .ico holds several sizes and the first frame is not reliably the largest. This
    /// window draws it at 48 px, and scaling a 16 px frame up is exactly what makes an
    /// About icon look like a mistake, so the biggest frame is picked and WPF scales down.
    /// The tray does the same thing in the other direction; see TrayIconHost.LoadTrayIcon.
    /// </summary>
    /// <returns>The icon's widest frame.</returns>
    private static ImageSource LoadAppIcon()
    {
        var decoder = BitmapDecoder.Create(
            new Uri(IconResourceUri),
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);

        return decoder.Frames.OrderByDescending(frame => frame.PixelWidth).First();
    }

    /// <summary>Closes the window.</summary>
    /// <param name="sender">The Close button; not read.</param>
    /// <param name="e">The click; not read.</param>
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
