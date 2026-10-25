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

// Copied from LightsCheck (which copied FlatCheck); a fix goes there first (ADR 0009).

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DarksCheck.Wpf;

/// <summary>
/// True to visible, anything else to collapsed. The converters the theme registers (a
/// StaticResource inside a dictionary resolves in that dictionary, so they are declared there).
/// </summary>
internal sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>Visible for the boolean true, collapsed for anything else (false, null).</summary>
    /// <param name="value">The bound value.</param>
    /// <param name="targetType">The binding's target type; ignored.</param>
    /// <param name="parameter">The converter parameter; ignored.</param>
    /// <param name="culture">The binding's culture; ignored.</param>
    /// <returns>A boxed <see cref="Visibility"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Not supported: the binding is one way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True to collapsed, anything else to visible.</summary>
internal sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <summary>Collapsed for the boolean true, visible for anything else (false, null).</summary>
    /// <param name="value">The bound value.</param>
    /// <param name="targetType">The binding's target type; ignored.</param>
    /// <param name="parameter">The converter parameter; ignored.</param>
    /// <param name="culture">The binding's culture; ignored.</param>
    /// <returns>A boxed <see cref="Visibility"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Not supported: the binding is one way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverted bool, for IsEnabled, where a Visibility would make no sense.</summary>
internal sealed class InverseBoolConverter : IValueConverter
{
    /// <summary>False for the boolean true, true for anything else (false, null).</summary>
    /// <param name="value">The bound value.</param>
    /// <param name="targetType">The binding's target type; ignored.</param>
    /// <param name="parameter">The converter parameter; ignored.</param>
    /// <param name="culture">The binding's culture; ignored.</param>
    /// <returns>A boxed boolean.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    /// <summary>Not supported: the binding is one way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Null or an empty string to collapsed, anything else to visible.</summary>
internal sealed class NotNullToVisibilityConverter : IValueConverter
{
    /// <summary>Collapsed for null or an empty string, visible for anything else.</summary>
    /// <param name="value">The bound value.</param>
    /// <param name="targetType">The binding's target type; ignored.</param>
    /// <param name="parameter">The converter parameter; ignored.</param>
    /// <param name="culture">The binding's culture; ignored.</param>
    /// <returns>A boxed <see cref="Visibility"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Not supported: the binding is one way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>The colour of a verdict word, through <see cref="VerdictPalette"/>.</summary>
internal sealed class VerdictToBrushConverter : IValueConverter
{
    /// <summary>The frozen brush of the verdict word's colour.</summary>
    /// <param name="value">The verdict word; anything not a string counts as no verdict
    /// (neutral).</param>
    /// <param name="targetType">The binding's target type; ignored.</param>
    /// <param name="parameter">The converter parameter; ignored.</param>
    /// <param name="culture">The binding's culture; ignored.</param>
    /// <returns>A frozen <see cref="System.Windows.Media.Brush"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => VerdictPalette.BrushFor(value as string);

    /// <summary>Not supported: the binding is one way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
