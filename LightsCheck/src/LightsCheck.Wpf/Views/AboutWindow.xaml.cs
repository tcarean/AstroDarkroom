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

using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using NLog;

namespace LightsCheck.Wpf;

/// <summary>
/// The "Despre" window: version, copyright, the GPLv3 notice and the third-party list.
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>
    /// The window over a new <see cref="AboutViewModel"/>, with the icon's largest frame as its
    /// logo and the dark title bar asked for once it has a handle.
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
            // window (ADR 0029).
            Log.Debug(ex, "the link " + e.Uri.OriginalString + " could not be opened in a browser");
        }
    }

    /// <summary>The diagnostic log: a link that could not be opened (R-071).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    /// <summary>The "Inchide" button: closes the window.</summary>
    /// <param name="sender">The button clicked.</param>
    /// <param name="e">The click's arguments, not used.</param>
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
