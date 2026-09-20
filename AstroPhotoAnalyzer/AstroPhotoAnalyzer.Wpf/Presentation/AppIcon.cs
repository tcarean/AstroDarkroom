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

// AppIcon.cs - icoana aplicatiei, la marimea cea mai mare din fisier. Un <Image> care
// primeste direct .ico-ul ia cadrul pe care il gaseste primul - la 72 px pe ecran poate
// fi cel de 16, scalat. "Despre" vrea cadrul de 256, micsorat.

using System;
using System.Linq;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    internal static class AppIcon
    {
        internal static readonly Uri Source =
            new Uri("pack://application:,,,/AstroPhotoAnalyzer.Wpf;component/Icon/AstroPhotoAnalyzer.ico");

        internal static BitmapSource Largest()
        {
            var decoder = BitmapDecoder.Create(Source, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            BitmapFrame frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
            if (frame.CanFreeze) frame.Freeze();
            return frame;
        }
    }
}
