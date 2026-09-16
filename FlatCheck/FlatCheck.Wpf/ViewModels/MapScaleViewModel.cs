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

// MapScaleViewModel.cs - comutatorul de scara al hartii reziduale.
//
// DE CE EXISTA. Scara automata intinde culoarea pe intervalul propriu al fiecarei
// imagini, deci rosu plin nu spune nimic anume: masurat pe doua mastere ale aceluiasi
// obiectiv, rosu plin era +3% pe unul si +10% pe altul. Pe o scara fixa doua sesiuni
// se compara dintr-o privire, iar saturatia are un sens - "iese din banda".
//
// DE CE E O CLASA SEPARATA, folosita de doua taburi. Structura si Ratio arata amandoua
// un reziduu pe scara de reziduu, deci amandoua vor acelasi comutator. Doua copii ale
// listei de plafoane ar fi divergat la prima ajustare, si oricum lista NU e a ferestrei:
// vine din PngWriter.FixedCapsPct, care o ia din Th.Resid*.
//
// CE NU FACE: nu decide nimic si nu calculeaza nicio cifra. Traduce o alegere in
// PngWriter.MapRange si anunta tabul ca trebuie sa reencodeze. Verdictul nu se schimba
// niciodata cu scara - se schimba doar cat de tare arata culoarea.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// Un plafon oferit de comutator. <see cref="Label"/> si <see cref="IsSelected"/> sunt
    /// tot ce vede XAML-ul; plafonul insusi e internal, fiindca e un detaliu al nucleului
    /// si o clasa publica nu are ce sa expuna din el (vezi nota din TabViewModel).
    /// </summary>
    public sealed class MapScaleOption : ObservableObject
    {
        readonly MapScaleViewModel _owner;
        bool _selected;

        internal MapScaleOption(MapScaleViewModel owner, double? capPct)
        {
            _owner = owner;
            CapPct = capPct;
            Label = RunReport.MapScaleLabel(capPct);
        }

        /// <summary>Eticheta chipului: "auto" sau plafonul. Vine din nucleu, nu de aici.</summary>
        public string Label { get; }

        /// <summary>Plafonul in procente, sau null pentru scara automata.</summary>
        internal double? CapPct { get; }

        /// <summary>
        /// Legarea din XAML e TwoWay pe asta. Bifarea trece prin owner, ca sa se
        /// debifeze celelalte si in ViewModel - altfel exclusivitatea ar exista numai
        /// in grupul de RadioButton, si un test fara fereastra ar vedea doua bifate.
        /// </summary>
        public bool IsSelected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value) _owner.Select(this); }
        }

        /// <summary>Debifare fara sa reintre in owner - altfel Select ar recursa.</summary>
        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    /// <summary>Lista de plafoane plus cel ales. Un tab il tine ca proprietate si asculta <see cref="Changed"/>.</summary>
    public sealed class MapScaleViewModel : ObservableObject
    {
        MapScaleOption _selected;

        /// <param name="defaultCapPct">
        /// Plafonul bifat la inceput; implicit cel din nucleu. Tabul de netezire da
        /// Th.ResidPerfect: ce se scoate e mic, si la +-10 % harta lui ar fi alba.
        /// </param>
        public MapScaleViewModel(double? defaultCapPct = null)
        {
            // "auto" prima, apoi plafoanele in ordine crescatoare, ca in scara de reziduu.
            var options = new List<MapScaleOption> { new MapScaleOption(this, null) };
            options.AddRange(PngWriter.FixedCapsPct.Select(c => new MapScaleOption(this, c)));
            Options = options;

            // Implicit plafonul din nucleu, nu "auto": banda in care se asteapta sa
            // traiasca un flat normal, deci doua sesiuni se compara fara sa apesi nimic.
            double cap = defaultCapPct ?? PngWriter.DefaultCapPct;
            _selected = options.First(o => o.CapPct == cap);
            _selected.IsSelected = true;
        }

        public IReadOnlyList<MapScaleOption> Options { get; }

        /// <summary>Plafonul ales. Nu e null niciodata.</summary>
        public MapScaleOption Selected
        {
            get => _selected;
            private set => Set(ref _selected, value);
        }

        /// <summary>S-a schimbat scara: tabul reencodeaza harta din rasterul pe care il tine.</summary>
        internal event Action Changed;

        internal void Select(MapScaleOption option)
        {
            if (ReferenceEquals(option, _selected)) return;

            foreach (var o in Options)
                if (!ReferenceEquals(o, option)) o.Unselect();

            Selected = option;
            Changed?.Invoke();
        }

        /// <summary>Capetele cerute de alegerea curenta, pentru rasterul dat.</summary>
        internal PngWriter.MapRange RangeFor(Img resid)
            => Selected.CapPct is double cap
                ? PngWriter.FixedRange(cap)
                : PngWriter.AutoRange(resid);
    }
}
