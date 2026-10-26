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

// Copied from DarksCheck (which copied LightsCheck, which copied FlatCheck); a fix goes there
// first (ADR 0007).

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CopyWcs.Wpf;

/// <summary>INotifyPropertyChanged with a Set that only raises on a change.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises <see cref="PropertyChanged"/> for one property.</summary>
    /// <param name="name">The property's name; the caller's member name when omitted.</param>
    protected void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Stores a value in a backing field and raises the change, only when it differs.
    /// </summary>
    /// <typeparam name="T">The property's type.</typeparam>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The new value, compared with the default equality comparer.</param>
    /// <param name="name">The property's name; the caller's member name when omitted.</param>
    /// <returns>True when the value changed and the event was raised; false when it was
    /// equal.</returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(name);
        return true;
    }
}
