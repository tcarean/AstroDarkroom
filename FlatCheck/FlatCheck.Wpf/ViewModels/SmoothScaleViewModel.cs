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

// SmoothScaleViewModel.cs - scara de netezire, ca trei chipuri: 150 / 300 / 600 px.
//
// Pe tiparul lui MapScaleViewModel: o lista de optiuni exclusive, una bifata, un
// eveniment. Decide nimic - traduce un click intr-un numar pe care il primeste
// SmoothAnalyzer. Implicitul e cel din nucleu (SmoothAnalyzer.DefaultScalePx), iar
// celelalte doua sunt jumatatea si dublul lui, ca sa nu existe o a doua lista de numere.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    public sealed class SmoothScaleOption : ObservableObject
    {
        readonly SmoothScaleViewModel _owner;
        bool _selected;

        internal SmoothScaleOption(SmoothScaleViewModel owner, double scalePx)
        {
            _owner = owner;
            ScalePx = scalePx;
            Label = $"{scalePx:F0} px";
        }

        public string Label { get; }

        /// <summary>Sigma nucleului, in pixeli de senzor. Internal: e un parametru al nucleului.</summary>
        internal double ScalePx { get; }

        public bool IsSelected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value) _owner.Select(this); }
        }

        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    public sealed class SmoothScaleViewModel : ObservableObject
    {
        SmoothScaleOption _selected;

        public SmoothScaleViewModel()
        {
            double d = SmoothAnalyzer.DefaultScalePx;
            Options = new[] { d / 2, d, d * 2 }.Select(s => new SmoothScaleOption(this, s)).ToList();
            _selected = Options.First(o => o.ScalePx == d);
            _selected.IsSelected = true;
        }

        public IReadOnlyList<SmoothScaleOption> Options { get; }

        /// <summary>Scara aleasa. Nu e null niciodata.</summary>
        public SmoothScaleOption Selected
        {
            get => _selected;
            private set => Set(ref _selected, value);
        }

        /// <summary>Sigma aleasa, in pixeli de senzor - ce primeste SmoothRun.</summary>
        internal double ScalePx => _selected.ScalePx;

        internal event Action Changed;

        internal void Select(SmoothScaleOption option)
        {
            if (ReferenceEquals(option, _selected)) return;
            foreach (var o in Options)
                if (!ReferenceEquals(o, option)) o.Unselect();
            Selected = option;
            Changed?.Invoke();
        }
    }
}
