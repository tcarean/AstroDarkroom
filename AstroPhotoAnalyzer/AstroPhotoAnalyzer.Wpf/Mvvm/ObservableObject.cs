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

// ObservableObject.cs - INotifyPropertyChanged, written by hand. No MVVM framework was brought
// in for twenty lines; the rule on dependencies is the rule "Package notices" in CLAUDE.md,
// with the reasons in docs/design/dependencies.md.
//
// Public, not internal, because the view-models inherit it, and they must be public for WPF's
// binding to see them. The consequence is useful: a public view-model CANNOT expose an internal
// type of the core, so what reaches the screen necessarily passes through simple fields.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The base of every view-model: raises <see cref="PropertyChanged"/> for the property that
    /// calls <see cref="Raise"/> or <see cref="Set{T}"/>.
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Raises <see cref="PropertyChanged"/> for one property.</summary>
        /// <param name="name">
        /// The property's name; left out, the compiler fills in the calling member's. Null, given
        /// on purpose, tells WPF that every property changed.
        /// </param>
        protected void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Stores a value in a backing field and raises <see cref="PropertyChanged"/> when it
        /// differs from the old one, by the type's default equality.
        /// </summary>
        /// <typeparam name="T">The property's type.</typeparam>
        /// <param name="field">The backing field.</param>
        /// <param name="value">The new value; null is allowed where the type allows it.</param>
        /// <param name="name">The property's name; left out, the calling member's.</param>
        /// <returns>
        /// True when the value changed and the event was raised; false when it was equal.
        /// </returns>
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}
