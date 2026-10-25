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

using System.Windows.Media.Imaging;

namespace LightsCheck.Wpf;

/// <summary>The application's icon, for the windows that draw it themselves (About).</summary>
internal static class AppIcon
{
    /// <summary>
    /// The same resource as Icon= in MainWindow.xaml, and the same file as the console's.
    /// </summary>
    internal static readonly Uri Source = new("pack://application:,,,/LightsCheck.Wpf;component/Icon/lightscheck.ico");

    /// <summary>
    /// The frame with the most pixels, frozen so it can be shared between threads.
    /// </summary>
    /// <returns>The icon's widest frame.</returns>
    internal static BitmapSource Largest()
    {
        BitmapDecoder decoder = BitmapDecoder.Create(Source, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        if (frame.CanFreeze)
        {
            frame.Freeze();
        }

        return frame;
    }
}
