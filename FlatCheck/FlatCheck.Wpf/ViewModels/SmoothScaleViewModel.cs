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

// SmoothScaleViewModel.cs - the smoothing scale, as three chips: 150 / 300 / 600 px.
//
// On MapScaleViewModel's pattern: a list of exclusive options, one ticked, an event. It decides
// nothing - it translates a click into a number SmoothAnalyzer receives. The default is the
// core's (SmoothAnalyzer.DefaultScalePx), and the other two are its half and its double, so there
// is no second list of numbers.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>One smoothing-scale chip; its scale stays internal, the label and the tick are
    /// what the XAML sees.</summary>
    public sealed class SmoothScaleOption : ObservableObject
    {
        readonly SmoothScaleViewModel _owner;
        bool _selected;

        /// <summary>Makes the chip, labelled with the scale in whole pixels.</summary>
        /// <param name="owner">The switch the chip belongs to; told when it is ticked.</param>
        /// <param name="scalePx">The kernel's sigma, in sensor pixels.</param>
        internal SmoothScaleOption(SmoothScaleViewModel owner, double scalePx)
        {
            _owner = owner;
            ScalePx = scalePx;
            Label = $"{scalePx:F0} px";
        }

        public string Label { get; }

        /// <summary>The kernel's sigma, in sensor pixels. Internal: it is a parameter of the
        /// core.</summary>
        internal double ScalePx { get; }

        public bool IsSelected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value) _owner.Select(this); }
        }

        /// <summary>Unticks without re-entering the owner.</summary>
        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    /// <summary>The three smoothing-scale chips - half, once and twice the core's default -
    /// with the one ticked.</summary>
    public sealed class SmoothScaleViewModel : ObservableObject
    {
        SmoothScaleOption _selected;

        /// <summary>Makes the three chips and ticks the core's default.</summary>
        public SmoothScaleViewModel()
        {
            double d = SmoothAnalyzer.DefaultScalePx;
            Options = new[] { d / 2, d, d * 2 }.Select(s => new SmoothScaleOption(this, s)).ToList();
            _selected = Options.First(o => o.ScalePx == d);
            _selected.IsSelected = true;
        }

        public IReadOnlyList<SmoothScaleOption> Options { get; }

        /// <summary>The chosen scale. Never null.</summary>
        public SmoothScaleOption Selected
        {
            get => _selected;
            private set => Set(ref _selected, value);
        }

        /// <summary>The chosen sigma, in sensor pixels - what SmoothRun receives.</summary>
        internal double ScalePx => _selected.ScalePx;

        internal event Action Changed;

        /// <summary>Ticks the chip with the given scale (the advisor's proposal); no effect when
        /// it is not a rung.</summary>
        /// <param name="scalePx">The scale, in sensor pixels.</param>
        internal void SelectScale(double scalePx)
        {
            SmoothScaleOption o = Options.FirstOrDefault(x => Math.Abs(x.ScalePx - scalePx) < 1e-9);
            if (o != null) o.IsSelected = true;
        }

        /// <summary>Makes the option the chosen one, unticks the others and raises
        /// <see cref="Changed"/>; nothing when it is already chosen.</summary>
        /// <param name="option">One of <see cref="Options"/>.</param>
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
