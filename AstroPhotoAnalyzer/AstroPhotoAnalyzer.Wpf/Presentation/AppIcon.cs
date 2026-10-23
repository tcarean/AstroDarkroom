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

// AppIcon.cs - the application's icon, at the largest size in the file. An <Image> given the
// .ico directly takes the frame it finds first - at 72 px on screen that can be the 16 px one,
// scaled up. "Despre" wants the 256 px frame, scaled down.

using System;
using System.Linq;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>The icon both executables share, read from this assembly's resources.</summary>
    internal static class AppIcon
    {
        internal static readonly Uri Source =
            new Uri("pack://application:,,,/AstroPhotoAnalyzer.Wpf;component/Icon/AstroPhotoAnalyzer.ico");

        /// <summary>Decodes the .ico and picks its widest frame.</summary>
        /// <returns>
        /// The widest frame, frozen when it can be; 256 px in the icon the build ships.
        /// </returns>
        internal static BitmapSource Largest()
        {
            var decoder = BitmapDecoder.Create(Source, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            BitmapFrame frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
            if (frame.CanFreeze) frame.Freeze();
            return frame;
        }
    }
}
