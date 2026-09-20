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

// ObservableObject.cs - INotifyPropertyChanged, scris de mana. Nu s-a adus un framework
// MVVM pentru douazeci de linii; regula dependentelor e in CLAUDE.md.
//
// Publica, nu internal, fiindca o mostenesc ViewModel-urile, care trebuie sa fie publice
// ca sa le vada legarea WPF. Consecinta e utila: un ViewModel public NU poate expune un
// tip internal din nucleu, deci ce se vede pe ecran trece obligatoriu prin campuri simple.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AstroPhotoAnalyzer.Wpf
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}
