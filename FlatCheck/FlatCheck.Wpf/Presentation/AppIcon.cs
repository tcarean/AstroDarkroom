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

// AppIcon.cs - icoana aplicatiei, la marimea cea mai mare din fisier.
//
// Un .ico are opt cadre desenate separat (vezi Icon/make_icon.py), si un <Image> care
// primeste direct fisierul ia cadrul pe care il gaseste primul - la 64 px pe ecran asta
// poate fi cel de 16, scalat, si iese neclar. Fereastra "Despre" vrea cadrul de 256,
// micsorat, deci il alege explicit, cu acelasi decodor pe care il foloseste IconTests.

using System;
using System.Linq;
using System.Windows.Media.Imaging;

namespace FlatCheck.Wpf
{
    internal static class AppIcon
    {
        /// <summary>Aceeasi resursa ca Icon= din MainWindow.xaml, si acelasi fisier ca al consolei.</summary>
        internal static readonly Uri Source =
            new Uri("pack://application:,,,/FlatCheck.Wpf;component/Icon/flatcheck.ico");

        /// <summary>Cadrul cu cei mai multi pixeli, inghetat, ca sa poata fi impartit intre fire.</summary>
        internal static BitmapSource Largest()
        {
            var decoder = BitmapDecoder.Create(Source, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            BitmapFrame frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
            if (frame.CanFreeze) frame.Freeze();
            return frame;
        }
    }
}
