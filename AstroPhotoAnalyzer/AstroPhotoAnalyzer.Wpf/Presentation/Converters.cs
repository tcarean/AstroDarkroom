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

using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace AstroPhotoAnalyzer.Wpf
{
    internal sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    internal sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>Vizibil doar cand toate legarile sunt adevarate: eticheta unui nor pe harta (norul e etichetabil SI etichetele sunt bifate).</summary>
    internal sealed class AllTrueToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type t, object p, CultureInfo c)
        {
            foreach (object v in values) if (v is not true) return Visibility.Collapsed;
            return Visibility.Visible;
        }
        public object[] ConvertBack(object value, Type[] t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    internal sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is not true;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    internal sealed class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value == null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    internal sealed class FileNameConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is string s && s.Length > 0 ? Path.GetFileName(s) : value;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>Culoarea unui verdict, dupa eticheta lui ("OK", "ATENTIE", "PROBLEMA").</summary>
    internal sealed class VerdictToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => VerdictPalette.BrushFor(value as string);
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }
}
