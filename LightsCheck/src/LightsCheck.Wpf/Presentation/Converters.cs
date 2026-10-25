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

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LightsCheck.Wpf;

// The converters the theme registers (a StaticResource inside a dictionary resolves in that
// dictionary, so they are declared there).

/// <summary>True shows the element, anything else collapses it.</summary>
internal sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>Visible for true, Collapsed for false, null or anything else.</summary>
    /// <param name="value">The bound value.</param>
    /// <param name="targetType">Not used.</param>
    /// <param name="parameter">Not used.</param>
    /// <param name="culture">Not used.</param>
    /// <returns>A <see cref="Visibility"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Not supported: the binding is one way.</summary>
    /// <param name="value">Not used.</param>
    /// <param name="targetType">Not used.</param>
    /// <param name="parameter">Not used.</param>
    /// <param name="culture">Not used.</param>
    /// <returns>Never returns; throws NotSupportedException.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True collapses the element, anything else shows it.</summary>
internal sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <summary>Collapsed for true, Visible for false, null or anything else.</summary>
    /// <param name="value">The bound value.</param>
    /// <param name="targetType">Not used.</param>
    /// <param name="parameter">Not used.</param>
    /// <param name="culture">Not used.</param>
    /// <returns>A <see cref="Visibility"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Not supported: the binding is one way.</summary>
    /// <param name="value">Not used.</param>
    /// <param name="targetType">Not used.</param>
    /// <param name="parameter">Not used.</param>
    /// <param name="culture">Not used.</param>
    /// <returns>Never returns; throws NotSupportedException.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverted bool, for IsEnabled, where a Visibility would make no sense.</summary>
internal sealed class InverseBoolConverter : IValueConverter
{
    /// <summary>False for true, true for false, null or anything else.</summary>
    /// <param name="value">The bound value.</param>
    /// <param name="targetType">Not used.</param>
    /// <param name="parameter">Not used.</param>
    /// <param name="culture">Not used.</param>
    /// <returns>A boxed bool.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    /// <summary>Not supported: the binding is one way.</summary>
    /// <param name="value">Not used.</param>
    /// <param name="targetType">Not used.</param>
    /// <param name="parameter">Not used.</param>
    /// <param name="culture">Not used.</param>
    /// <returns>Never returns; throws NotSupportedException.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Shows the element when the bound value is there: not null, not an empty string.
/// </summary>
internal sealed class NotNullToVisibilityConverter : IValueConverter
{
    /// <summary>Collapsed for null or an empty string, Visible for anything else.</summary>
    /// <param name="value">The bound value.</param>
    /// <param name="targetType">Not used.</param>
    /// <param name="parameter">Not used.</param>
    /// <param name="culture">Not used.</param>
    /// <returns>A <see cref="Visibility"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Not supported: the binding is one way.</summary>
    /// <param name="value">Not used.</param>
    /// <param name="targetType">Not used.</param>
    /// <param name="parameter">Not used.</param>
    /// <param name="culture">Not used.</param>
    /// <returns>Never returns; throws NotSupportedException.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>The colour of a verdict word, through <see cref="VerdictPalette"/>.</summary>
internal sealed class VerdictToBrushConverter : IValueConverter
{
    /// <summary>The frozen brush of the verdict word's colour.</summary>
    /// <param name="value">The verdict word; anything that is not a string counts as none.</param>
    /// <param name="targetType">Not used.</param>
    /// <param name="parameter">Not used.</param>
    /// <param name="culture">Not used.</param>
    /// <returns>A <see cref="System.Windows.Media.Brush"/>, neutral grey for no word.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => VerdictPalette.BrushFor(value as string);

    /// <summary>Not supported: the binding is one way.</summary>
    /// <param name="value">Not used.</param>
    /// <param name="targetType">Not used.</param>
    /// <param name="parameter">Not used.</param>
    /// <param name="culture">Not used.</param>
    /// <returns>Never returns; throws NotSupportedException.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
