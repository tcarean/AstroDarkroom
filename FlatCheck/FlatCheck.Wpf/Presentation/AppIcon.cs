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

// AppIcon.cs - the application's icon, at the largest size in the file.
//
// An .ico has eight frames drawn separately (see Icon/make_icon.py), and an <Image> given the
// file directly takes the frame it finds first - at 64 px on screen that may be the 16 one,
// scaled, and it comes out blurred. The "Despre" window wants the 256 frame, scaled down, so it
// picks it explicitly, with the same decoder IconTests uses.

using System;
using System.Linq;
using System.Windows.Media.Imaging;

namespace FlatCheck.Wpf
{
    /// <summary>The application's icon resource, and its largest frame for the "Despre"
    /// window.</summary>
    internal static class AppIcon
    {
        /// <summary>The same resource as Icon= in MainWindow.xaml, and the same file as the
        /// console's.</summary>
        internal static readonly Uri Source =
            new Uri("pack://application:,,,/FlatCheck.Wpf;component/Icon/flatcheck.ico");

        /// <summary>Decodes the icon and picks its frame with the most pixels.</summary>
        /// <returns>The widest frame, frozen, so it can be shared between threads.</returns>
        internal static BitmapSource Largest()
        {
            var decoder = BitmapDecoder.Create(Source, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            BitmapFrame frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
            if (frame.CanFreeze) frame.Freeze();
            return frame;
        }
    }
}
