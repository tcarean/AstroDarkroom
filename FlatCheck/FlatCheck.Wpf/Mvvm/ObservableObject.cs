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

// ObservableObject.cs - INotifyPropertyChanged, scris de mana.
//
// Publica, nu internal, fiindca o mostenesc ViewModel-urile, care trebuie sa fie
// publice ca sa le vada legarea WPF. Vezi nota din TabViewModel.
//
// Nu s-a adus un framework MVVM pentru douazeci de linii. Unealta asta are exact doua
// dependente externe (libraw, pentru raw-uri de aparat, si ImageSharp, pentru previzualizarea
// JPEG din raw), si fiecare a trebuit sa isi argumenteze existenta - vezi "The rule" din
// docs/design/dependencies.md. Un pachet care genereaza proprietati nu trece acelasi prag.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlatCheck.Wpf
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Pune valoarea si anunta, doar daca s-a schimbat ceva. Intoarce true cand chiar
        /// s-a schimbat, ca apelantul sa poata inlantui efecte (de exemplu: "s-a ales alt
        /// fisier, deci recalculeaza").
        /// </summary>
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}
