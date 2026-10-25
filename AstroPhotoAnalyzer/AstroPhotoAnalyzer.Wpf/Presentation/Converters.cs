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

// Converters.cs - the translations between what the view-models hold and what the XAML wants.
// Every binding through them is one-way: ConvertBack is not supported.

using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>True to Visible; anything else, false or null, to Collapsed.</summary>
    internal sealed class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>Converts a bool to a visibility.</summary>
        /// <param name="value">The bound value; only the boxed true is Visible.</param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns>Visibility.Visible or Visibility.Collapsed.</returns>
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Not supported: the binding is one-way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Nothing; always throws NotSupportedException.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>True to Collapsed; anything else, false or null, to Visible.</summary>
    internal sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        /// <summary>Converts a bool to the opposite visibility.</summary>
        /// <param name="value">The bound value; only the boxed true is Collapsed.</param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns>Visibility.Collapsed or Visibility.Visible.</returns>
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>Not supported: the binding is one-way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Nothing; always throws NotSupportedException.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>The opposite bool: true to false, anything else, false or null, to true.</summary>
    internal sealed class InverseBoolConverter : IValueConverter
    {
        /// <summary>Negates a bool.</summary>
        /// <param name="value">The bound value; only the boxed true gives false.</param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns>A boxed bool.</returns>
        public object Convert(object value, Type t, object p, CultureInfo c) => value is not true;

        /// <summary>Not supported: the binding is one-way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Nothing; always throws NotSupportedException.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>Null or an empty string to Collapsed; anything else to Visible.</summary>
    internal sealed class NotNullToVisibilityConverter : IValueConverter
    {
        /// <summary>Converts the presence of a value to a visibility.</summary>
        /// <param name="value">
        /// The bound value; null and "" are Collapsed, a blank string is not.
        /// </param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns>Visibility.Visible or Visibility.Collapsed.</returns>
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value == null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>Not supported: the binding is one-way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Nothing; always throws NotSupportedException.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>A path to its file name, for the top bar; anything else passes through.</summary>
    internal sealed class FileNameConverter : IValueConverter
    {
        /// <summary>Takes the file name of a path.</summary>
        /// <param name="value">The bound value; a non-empty string is treated as a path.</param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns>
        /// The file name of the path, or the value itself when it is not such a string.
        /// </returns>
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is string s && s.Length > 0 ? Path.GetFileName(s) : value;

        /// <summary>Not supported: the binding is one-way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Nothing; always throws NotSupportedException.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>The colour of a verdict, from its label in the interface language ("OK", "CAUTION", "PROBLEM").</summary>
    internal sealed class VerdictToBrushConverter : IValueConverter
    {
        /// <summary>Converts a verdict label to its brush.</summary>
        /// <param name="value">
        /// The label; anything that is not a string counts as null, neutral.
        /// </param>
        /// <param name="t">The target type; ignored.</param>
        /// <param name="p">The converter parameter; ignored.</param>
        /// <param name="c">The culture; ignored.</param>
        /// <returns>The frozen brush of <see cref="VerdictPalette.BrushFor"/>.</returns>
        public object Convert(object value, Type t, object p, CultureInfo c)
            => VerdictPalette.BrushFor(value as string);

        /// <summary>Not supported: the binding is one-way.</summary>
        /// <param name="value">Ignored.</param>
        /// <param name="t">Ignored.</param>
        /// <param name="p">Ignored.</param>
        /// <param name="c">Ignored.</param>
        /// <returns>Nothing; always throws NotSupportedException.</returns>
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }
}
