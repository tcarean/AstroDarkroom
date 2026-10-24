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

// ObservableObject.cs - INotifyPropertyChanged, written by hand.
//
// Public, not internal, because the view-models inherit it, and they must be public for the WPF
// binding to see them. See the note in TabViewModel.
//
// No MVVM framework was brought in for twenty lines. This tool has exactly two external
// dependencies (libraw, for camera raws, and ImageSharp, for the JPEG preview in the raw), and
// each had to argue for its existence - see "The rule" in docs/design/dependencies.md. A package
// that generates properties does not clear the same bar.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlatCheck.Wpf
{
    /// <summary>The base of every view-model: raises <see cref="PropertyChanged"/> for the
    /// WPF binding.</summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Announces that a property changed.</summary>
        /// <param name="name">The property's name; the caller's own name when left
        /// out.</param>
        protected void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Sets the value and announces it, only if something changed. Returns true when it
        /// really changed, so the caller can chain effects (for example: "another file was
        /// chosen, so recompute").
        /// </summary>
        /// <typeparam name="T">The property's type.</typeparam>
        /// <param name="field">The backing field.</param>
        /// <param name="value">The new value.</param>
        /// <param name="name">The property's name; the caller's own name when left
        /// out.</param>
        /// <returns>True when the value changed and was announced.</returns>
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}
