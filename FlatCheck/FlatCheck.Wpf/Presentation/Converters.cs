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

// Converters.cs - traducerile dintre ce tin ViewModel-urile si ce vrea XAML-ul.
//
// Cel important e primul: harta reziduala ajunge aici ca octeti PNG, exact aia pe care
// i-ar fi scris consola pe disc, si se transforma in imagine FARA sa atinga discul. Asa
// nu exista un fisier temporar de curatat, nu exista o cale de esec in plus, si ce se
// vede pe ecran e garantat acelasi lucru cu ce se salveaza.

using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace FlatCheck.Wpf
{
    internal sealed class PngBytesToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not byte[] png || png.Length == 0) return null;

            var img = new BitmapImage();
            img.BeginInit();
            // CacheOption OnLoad, ca stream-ul sa poata fi inchis imediat - altfel
            // BitmapImage il tine deschis si octetii raman in viata cat traieste imaginea.
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = new MemoryStream(png);
            img.EndInit();
            img.Freeze();
            return img;
        }

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    internal sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    internal sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>Bool inversat. Pentru IsEnabled, unde Visibility n-ar avea sens.</summary>
    internal sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is not true;

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    internal sealed class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value == null || (value is string s && s.Length == 0)
                ? Visibility.Collapsed
                : Visibility.Visible;

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Doar numele fisierului. Listele tin caile intregi - ele sunt ce se da mai departe
    /// analizorului - dar calea intreaga ar rupe latimea unui combo, si oricum toate
    /// fisierele dintr-o lista stau in acelasi folder.
    /// </summary>
    internal sealed class FileNameConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is string s && s.Length > 0 ? Path.GetFileName(s) : value;

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Culoarea unui verdict. Trece prin VerdictPalette, care o deduce din Stats.Severity
    /// - deci un verdict nou din nucleu se coloreaza corect fara sa se atinga XAML-ul.
    /// </summary>
    internal sealed class VerdictToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => VerdictPalette.BrushFor(value as string);

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }
}
