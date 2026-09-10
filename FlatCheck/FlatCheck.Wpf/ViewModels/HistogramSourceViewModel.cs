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

// HistogramSourceViewModel.cs - comutatorul dintre cele doua surse ale histogramelor.
//
// Acelasi tipar ca MapScaleViewModel: doua chipuri radio, unul bifat, iar tabul asculta
// Changed si isi reface randurile. Nu decide nimic - traduce o alegere. Ce e diferit fata
// de scara hartii e ca a doua optiune poate LIPSI: un FITS n-are previzualizare, si nici
// un raw caruia nu i s-a putut decoda. Atunci comutatorul se stinge si alegerea revine
// pe datele brute, fiindca alea exista mereu.

using System;
using System.Collections.Generic;

namespace FlatCheck.Wpf
{
    /// <summary>O sursa de histograme. XAML-ul vede eticheta si bifa; restul e al ownerului.</summary>
    public sealed class HistogramSourceOption : ObservableObject
    {
        readonly HistogramSourceViewModel _owner;
        bool _selected;

        internal HistogramSourceOption(HistogramSourceViewModel owner, bool isPreview, string label)
        {
            _owner = owner;
            IsPreview = isPreview;
            Label = label;
        }

        public string Label { get; }

        /// <summary>true pentru previzualizarea aparatului, false pentru datele brute.</summary>
        internal bool IsPreview { get; }

        /// <summary>
        /// Legarea din XAML e TwoWay pe asta. Bifarea trece prin owner, ca sa se debifeze
        /// cealalta si in ViewModel, nu doar in grupul de RadioButton.
        /// </summary>
        public bool IsSelected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value) _owner.Select(this); }
        }

        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    /// <summary>Cele doua surse plus cea aleasa. Tabul o tine ca proprietate si asculta <see cref="Changed"/>.</summary>
    public sealed class HistogramSourceViewModel : ObservableObject
    {
        HistogramSourceOption _selected;
        bool _enabled;

        public HistogramSourceViewModel()
        {
            // Etichetele numesc ce E fiecare, nu formatul de fisier.
            Raw = new HistogramSourceOption(this, false, "Date brute (ce masoara unealta)");
            Preview = new HistogramSourceOption(this, true, "Ce a aratat aparatul pe ecran");
            Options = new[] { Raw, Preview };

            _selected = Raw;
            Raw.IsSelected = true;
        }

        internal HistogramSourceOption Raw { get; }
        internal HistogramSourceOption Preview { get; }

        public IReadOnlyList<HistogramSourceOption> Options { get; }

        /// <summary>Sursa aleasa. Nu e null niciodata.</summary>
        public HistogramSourceOption Selected
        {
            get => _selected;
            private set { if (Set(ref _selected, value)) Raise(nameof(IsPreviewSelected)); }
        }

        public bool IsPreviewSelected => _selected.IsPreview;

        /// <summary>
        /// false cand fisierul n-are previzualizare. Stingerea forteaza alegerea inapoi pe
        /// datele brute, ca tabul sa nu ramana pe o sursa care nu exista.
        /// </summary>
        public bool IsEnabled
        {
            get => _enabled;
            internal set
            {
                if (!Set(ref _enabled, value)) return;
                if (!value && _selected.IsPreview) Select(Raw);
            }
        }

        /// <summary>S-a schimbat sursa: tabul isi reface randurile din rezultatul pe care il tine.</summary>
        internal event Action Changed;

        internal void Select(HistogramSourceOption option)
        {
            if (ReferenceEquals(option, _selected))
            {
                if (!option.IsSelected) option.IsSelected = true;
                return;
            }
            if (option.IsPreview && !_enabled)
            {
                // Nu se poate: n-are ce arata. Bifa ramane pe brute.
                option.Unselect();
                Raw.IsSelected = true;
                return;
            }

            foreach (var o in Options)
                if (!ReferenceEquals(o, option)) o.Unselect();
            if (!option.IsSelected) option.IsSelected = true;

            Selected = option;
            Changed?.Invoke();
        }
    }
}
