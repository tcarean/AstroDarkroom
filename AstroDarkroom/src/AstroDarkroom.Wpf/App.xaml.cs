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
using System.Windows.Media.Imaging;
using AstroDarkroom.Core;

namespace AstroDarkroom.Wpf;

/// <summary>The application: it opens the main window on the folder the executable runs from.</summary>
public partial class App : Application
{
    /// <summary>The icon, the same resource as Icon= in the windows' XAML and the executable's own.</summary>
    internal static readonly Uri IconSource = new("pack://application:,,,/AstroDarkroom;component/Icon/astrodarkroom.ico");

    /// <summary>
    /// Opens the main window on the suite folder, the launcher's own base directory (R-002).
    /// </summary>
    /// <param name="e">The start-up arguments; not read, the launcher takes none.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainWindow window = new(new SuiteFolder(AppContext.BaseDirectory));
        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// The icon's frame with the most pixels, for the About box, where WPF scales it down; the
    /// first frame is not reliably the largest.
    /// </summary>
    /// <returns>The widest frame, frozen.</returns>
    internal static BitmapSource LargestIconFrame()
    {
        BitmapDecoder decoder = BitmapDecoder.Create(IconSource, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        if (frame.CanFreeze)
        {
            frame.Freeze();
        }

        return frame;
    }
}
