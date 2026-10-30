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

// Copied from DarksCheck (which copied LightsCheck); a fix goes there first (ADR 0007). The
// link's one filtered catch writes a Debug line, as LightsCheck's does since its log (ADR 0009).

using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using NLog;

namespace CopyWcs.Wpf;

/// <summary>
/// The About window: the icon, the version, the copyright and the GPLv3 notice, all from
/// <see cref="AboutViewModel"/> (R-035).
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>The About window's line in the diagnostic log: a link that would not open (R-043).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    /// <summary>
    /// The window over a new <see cref="AboutViewModel"/>, with the icon's largest frame as its
    /// logo and the dark title bar once it has a handle.
    /// </summary>
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
        Logo.Source = AppIcon.Largest();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
    }

    /// <summary>
    /// Opens the link in the system browser; UseShellExecute is what makes a URL open on .NET Core.
    /// A refused start leaves the link on screen to be copied by hand, and a Debug line in the
    /// diagnostic log with the reason.
    /// </summary>
    /// <param name="sender">The hyperlink.</param>
    /// <param name="e">The navigation request; marked handled.</param>
    private void OpenLink(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // OriginalString, never AbsoluteUri again: a relative link throws there, which is
            // what landed here, and a value the log reads must never throw (ADR 0009).
            Log.Debug(ex, "the link " + e.Uri.OriginalString + " could not be opened");
        }
    }

    /// <summary>The close button: closes the window.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
