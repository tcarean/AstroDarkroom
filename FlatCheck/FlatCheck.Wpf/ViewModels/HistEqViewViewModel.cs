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

// HistEqViewViewModel.cs - comutatorul de vederi al tabului de histograma.
//
// Acelasi tipar ca MapScaleViewModel si HistogramSourceViewModel: chipuri radio, unul
// bifat, tabul asculta Changed. Ce e diferit e ca LISTA depinde de rezultat - patru
// pozitii plus compozit plus mozaic pe un cadru CFA, o singura vedere pe mono - deci se
// reface la fiecare rulare, pastrand chipul bifat daca mai exista unul cu aceeasi
// eticheta. Nu decide nimic: traduce o alegere in "ce raster se encodeaza".

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    internal enum HistEqViewKind { Composite, Channel, Mosaic }

    /// <summary>O vedere. XAML-ul vede eticheta si bifa; felul si indexul sunt ale ownerului.</summary>
    public sealed class HistEqViewOption : ObservableObject
    {
        readonly HistEqViewViewModel _owner;
        bool _selected;

        internal HistEqViewOption(HistEqViewViewModel owner, HistEqViewKind kind, int index, string label)
        {
            _owner = owner;
            Kind = kind;
            Index = index;
            Label = label;
        }

        public string Label { get; }

        internal HistEqViewKind Kind { get; }

        /// <summary>Indexul canalului pentru Kind = Channel; 0 altfel.</summary>
        internal int Index { get; }

        /// <summary>Legarea din XAML e TwoWay pe asta. Bifarea trece prin owner, ca sa se debifeze celelalte.</summary>
        public bool IsSelected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value) _owner.Select(this); }
        }

        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    public sealed class HistEqViewViewModel : ObservableObject
    {
        IReadOnlyList<HistEqViewOption> _options = Array.Empty<HistEqViewOption>();
        HistEqViewOption _selected;

        /// <summary>
        /// Lista se INLOCUIESTE la fiecare rulare, nu se goleste si se reumple - acelasi
        /// motiv ca la ExposureTabViewModel.Histograms: doua rulari suprapuse pe o colectie
        /// observabila lasa randuri duplicate.
        /// </summary>
        public IReadOnlyList<HistEqViewOption> Options
        {
            get => _options;
            private set { if (Set(ref _options, value)) Raise(nameof(IsVisible)); }
        }

        /// <summary>Vederea aleasa; null doar inainte de prima rulare.</summary>
        public HistEqViewOption Selected
        {
            get => _selected;
            private set => Set(ref _selected, value);
        }

        /// <summary>Un singur chip n-are ce comuta, deci randul nu se arata.</summary>
        public bool IsVisible => _options.Count > 1;

        /// <summary>S-a schimbat vederea: tabul reencodeaza din rezultatul pe care il tine.</summary>
        internal event Action Changed;

        /// <summary>
        /// Refacerea listei din rezultat. NU ridica Changed - tabul isi reface poza singur
        /// dupa ce a pus in cache ce a primit gata encodat de la rulare.
        /// </summary>
        internal void Rebuild(HistEqResult r)
        {
            var list = new List<HistEqViewOption>();
            if (r.IsCfa)
            {
                list.Add(new HistEqViewOption(this, HistEqViewKind.Composite, 0, "RGB"));
                for (int i = 0; i < r.Channels.Length; i++)
                    list.Add(new HistEqViewOption(this, HistEqViewKind.Channel, i, r.Channels[i].Label));
                list.Add(new HistEqViewOption(this, HistEqViewKind.Mosaic, 0, "Mozaic"));
            }
            else
            {
                list.Add(new HistEqViewOption(this, HistEqViewKind.Composite, 0, "mono"));
            }

            string keep = _selected?.Label;
            HistEqViewOption pick = list.FirstOrDefault(o => o.Label == keep) ?? list[0];

            Options = list;
            _selected = pick;
            pick.IsSelected = true;
            Raise(nameof(Selected));
        }

        internal void Select(HistEqViewOption option)
        {
            if (ReferenceEquals(option, _selected))
            {
                if (!option.IsSelected) option.IsSelected = true;
                return;
            }

            foreach (var o in Options)
                if (!ReferenceEquals(o, option)) o.Unselect();

            Selected = option;
            Changed?.Invoke();
        }
    }
}
