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

// Chip-urile de tip de senzor de pe tabul Sintetic, pe tiparul MapScaleViewModel: o
// alegere devine un crop in campul de text, si nimic altceva. Sunt rezerva pentru un
// aparat pe care baza lensfun nu-l are - calea principala ramane tabelul, care umple
// campul singur - ca omul sa nu tasteze o valoare obscura. Lista vine din nucleu
// (SensorFormats); aici nu se decide nimic.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    public sealed class SensorFormatOption : ObservableObject
    {
        readonly SensorFormatViewModel _owner;
        bool _selected;

        internal SensorFormatOption(SensorFormatViewModel owner, SensorFormat format)
        {
            _owner = owner;
            Format = format;
            Label = $"{format.Label} {format.CropText}";
            Tip = format.Note;
        }

        internal SensorFormat Format { get; }

        public string Label { get; }

        public string Tip { get; }

        public bool IsSelected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value) _owner.Select(this); }
        }

        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    public sealed class SensorFormatViewModel : ObservableObject
    {
        SensorFormatOption _selected;
        bool _following;

        public SensorFormatViewModel()
        {
            Options = SensorFormats.All.Select(f => new SensorFormatOption(this, f)).ToList();
        }

        public IReadOnlyList<SensorFormatOption> Options { get; }

        /// <summary>Chip-ul ales, sau null cand crop-ul a venit din tabel sau de la tastatura.</summary>
        public SensorFormatOption Selected
        {
            get => _selected;
            private set => Set(ref _selected, value);
        }

        internal event Action<SensorFormat> Chosen;

        internal void Select(SensorFormatOption option)
        {
            if (ReferenceEquals(option, _selected)) return;
            foreach (var o in Options)
                if (!ReferenceEquals(o, option)) o.Unselect();
            Selected = option;
            if (!_following) Chosen?.Invoke(option.Format);
        }

        /// <summary>
        /// Chip-urile arata formatul valorii din camp: se aprinde cel mai apropiat (in toleranta
        /// lui SensorFormats), fara sa anunte o alegere; o valoare fara format, sau niciuna,
        /// le stinge pe toate. Prima varianta le stingea la orice tastare, si 1.523 din tabel
        /// arata ca o valoare cazuta din cer.
        /// </summary>
        internal void Follow(double? crop)
        {
            SensorFormat f = crop.HasValue ? SensorFormats.Nearest(crop.Value) : null;
            if (f == null) { Clear(); return; }
            _following = true;
            try
            {
                SensorFormatOption option = Options.First(o => ReferenceEquals(o.Format, f));
                if (!ReferenceEquals(option, _selected)) option.IsSelected = true;
            }
            finally { _following = false; }
        }

        /// <summary>Niciun chip: crop-ul de pe ecran nu mai e al unui chip.</summary>
        internal void Clear()
        {
            foreach (var o in Options) o.Unselect();
            Selected = null;
        }
    }
}
