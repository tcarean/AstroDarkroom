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

// Converters.cs - the translations between what the view-models hold and what the XAML
// wants.
//
// The important one is the first: the residual map arrives here as PNG bytes, exactly those
// the console would have written to disk, and becomes an image WITHOUT touching the disk. So
// there is no temp file to clean up, no extra failure path, and what is seen on screen is
// guaranteed to be the same thing as what is saved.

using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace FlatCheck.Wpf
{
    /// <summary>PNG bytes to a frozen image, in memory; one way only.</summary>
    internal sealed class PngBytesToImageConverter : IValueConverter
    {
        /// <summary>Decodes PNG bytes into an image.</summary>
        /// <param name="value">The PNG bytes, or anything else.</param>
        /// <param name="targetType">The binding target's type; ignored.</param>
        /// <param name="parameter">The converter parameter; ignored.</param>
        /// <param name="culture">The binding culture; ignored.</param>
        /// <returns>A frozen <see cref="BitmapImage"/>, or null when the value is not a
        /// non-empty byte array.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not byte[] png || png.Length == 0) return null;

            var img = new BitmapImage();
            img.BeginInit();
            // CacheOption OnLoad, so the stream can be closed at once - otherwise BitmapImage
            // keeps it open and the bytes stay alive as long as the image lives.
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = new MemoryStream(png);
            img.EndInit();
            img.Freeze();
            return img;
        }

        /// <summary>Not supported: the binding is one way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Never; throws <see cref="NotSupportedException"/>.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>True to Visible, anything else to Collapsed; one way only.</summary>
    internal sealed class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>Shows the element for true.</summary>
        /// <param name="value">The bound value.</param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns><see cref="Visibility.Visible"/> for true, otherwise
        /// <see cref="Visibility.Collapsed"/>.</returns>
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Not supported: the binding is one way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Never; throws <see cref="NotSupportedException"/>.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>True to Collapsed, anything else to Visible; one way only.</summary>
    internal sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        /// <summary>Hides the element for true.</summary>
        /// <param name="value">The bound value.</param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns><see cref="Visibility.Collapsed"/> for true, otherwise
        /// <see cref="Visibility.Visible"/>.</returns>
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>Not supported: the binding is one way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Never; throws <see cref="NotSupportedException"/>.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>Bool inverted. For IsEnabled, where Visibility would make no sense.</summary>
    internal sealed class InverseBoolConverter : IValueConverter
    {
        /// <summary>Inverts a bool.</summary>
        /// <param name="value">The bound value.</param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns>False for true, true for anything else (null included).</returns>
        public object Convert(object value, Type t, object p, CultureInfo c) => value is not true;

        /// <summary>Not supported: the binding is one way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Never; throws <see cref="NotSupportedException"/>.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>Null or an empty string to Collapsed, anything else to Visible; one way
    /// only.</summary>
    internal sealed class NotNullToVisibilityConverter : IValueConverter
    {
        /// <summary>Shows the element when there is something to show.</summary>
        /// <param name="value">The bound value.</param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns><see cref="Visibility.Collapsed"/> for null or "", otherwise
        /// <see cref="Visibility.Visible"/>.</returns>
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value == null || (value is string s && s.Length == 0)
                ? Visibility.Collapsed
                : Visibility.Visible;

        /// <summary>Not supported: the binding is one way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Never; throws <see cref="NotSupportedException"/>.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Only the file name. The lists hold the full paths - they are what is handed on to the
    /// analyzer - but the full path would break the width of a combo, and anyway all the files
    /// of a list sit in the same folder.
    /// </summary>
    internal sealed class FileNameConverter : IValueConverter
    {
        /// <summary>Cuts a path to its file name.</summary>
        /// <param name="value">The bound value, normally a full path.</param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns>The file name for a non-empty string, otherwise the value
        /// unchanged.</returns>
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is string s && s.Length > 0 ? Path.GetFileName(s) : value;

        /// <summary>Not supported: the binding is one way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Never; throws <see cref="NotSupportedException"/>.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// The colour of a verdict. It goes through VerdictPalette, which deduces it from
    /// Stats.Severity - so a new verdict from the core is coloured right without touching the
    /// XAML.
    /// </summary>
    internal sealed class VerdictToBrushConverter : IValueConverter
    {
        /// <summary>A verdict word to its brush.</summary>
        /// <param name="value">The verdict, a string; anything else counts as null.</param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns>The frozen brush <see cref="VerdictPalette.BrushFor"/> gives.</returns>
        public object Convert(object value, Type t, object p, CultureInfo c)
            => VerdictPalette.BrushFor(value as string);

        /// <summary>Not supported: the binding is one way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Never; throws <see cref="NotSupportedException"/>.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }
}
