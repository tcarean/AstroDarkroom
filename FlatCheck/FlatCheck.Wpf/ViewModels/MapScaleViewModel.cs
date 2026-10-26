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

// MapScaleViewModel.cs - the scale switch of the residual map.
//
// WHY IT EXISTS. The automatic scale stretches the colour over each image's own range, so full
// red says nothing in particular: measured on two masters of the same lens, full red was +3% on
// one and +10% on the other. On a fixed scale two sessions compare at a glance, and saturation
// has a meaning - "out of the band".
//
// WHY IT IS A CLASS OF ITS OWN, used by four tabs. Structure, Netezire, Sintetic and Split
// all show a residual on the residual scale, so all want the same switch. Copies of the list of
// caps would have diverged at the first adjustment, and the list is NOT the window's anyway: it
// comes from PngWriter.FixedCapsPct, which takes it from Th.Resid*.
//
// WHAT IT DOES NOT DO: it decides nothing and computes no figure. It translates a choice into
// PngWriter.MapRange and tells the tab it must re-encode. The verdict never changes with the
// scale - only how strong the colour looks does.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// A cap the switch offers. <see cref="Label"/> and <see cref="IsSelected"/> are all the XAML
    /// sees; the cap itself is internal, because it is a detail of the core and a public class
    /// has nothing of it to expose (see the note in TabViewModel).
    /// </summary>
    public sealed class MapScaleOption : ObservableObject
    {
        readonly MapScaleViewModel _owner;
        bool _selected;

        /// <summary>Makes the chip, its label worded by the core.</summary>
        /// <param name="owner">The switch the chip belongs to; told when it is ticked.</param>
        /// <param name="capPct">The cap in percent, or null for the automatic scale.</param>
        internal MapScaleOption(MapScaleViewModel owner, double? capPct)
        {
            _owner = owner;
            CapPct = capPct;
            Label = RunReport.MapScaleLabel(capPct);
        }

        /// <summary>The chip's label: "auto" or the cap. It comes from the core, not from
        /// here.</summary>
        public string Label { get; }

        /// <summary>The cap in percent, or null for the automatic scale.</summary>
        internal double? CapPct { get; }

        /// <summary>
        /// The XAML binding on this is TwoWay. Ticking goes through the owner, so the others are
        /// unticked in the view-model too - otherwise the exclusivity would exist only in the
        /// RadioButton group, and a test without a window would see two ticked.
        /// </summary>
        public bool IsSelected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value) _owner.Select(this); }
        }

        /// <summary>Unticks without re-entering the owner - otherwise Select would
        /// recurse.</summary>
        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    /// <summary>The list of caps plus the chosen one. A tab holds it as a property and listens
    /// to <see cref="Changed"/>.</summary>
    public sealed class MapScaleViewModel : ObservableObject
    {
        MapScaleOption _selected;

        /// <summary>Makes "auto" and the core's fixed caps, and ticks the default.</summary>
        /// <param name="defaultCapPct">
        /// The cap ticked at the start; by default the core's. The smoothing tab gives
        /// Th.ResidPerfect: what is removed is small, and at +-10 % its map would be white.
        /// </param>
        public MapScaleViewModel(double? defaultCapPct = null)
        {
            // "auto" first, then the caps in ascending order, as on the residual scale.
            var options = new List<MapScaleOption> { new MapScaleOption(this, null) };
            options.AddRange(PngWriter.FixedCapsPct.Select(c => new MapScaleOption(this, c)));
            Options = options;

            // By default the core's cap, not "auto": the band a normal flat is expected to live
            // in, so two sessions compare without pressing anything.
            double cap = defaultCapPct ?? PngWriter.DefaultCapPct;
            _selected = options.First(o => o.CapPct == cap);
            _selected.IsSelected = true;
        }

        public IReadOnlyList<MapScaleOption> Options { get; }

        /// <summary>The chosen cap. Never null.</summary>
        public MapScaleOption Selected
        {
            get => _selected;
            private set => Set(ref _selected, value);
        }

        /// <summary>The scale changed: the tab re-encodes the map from the raster it
        /// holds.</summary>
        internal event Action Changed;

        /// <summary>Makes the option the chosen one, unticks the others and raises
        /// <see cref="Changed"/>; nothing when it is already chosen.</summary>
        /// <param name="option">One of <see cref="Options"/>.</param>
        internal void Select(MapScaleOption option)
        {
            if (ReferenceEquals(option, _selected)) return;

            foreach (var o in Options)
                if (!ReferenceEquals(o, option)) o.Unselect();

            Selected = option;
            Changed?.Invoke();
        }

        /// <summary>The ends the current choice asks for, for the given raster.</summary>
        /// <param name="resid">The residual raster, in percent; read only on the automatic
        /// scale.</param>
        /// <returns>The fixed range of the chosen cap, or the raster's own automatic
        /// range.</returns>
        internal PngWriter.MapRange RangeFor(Img resid)
            => Selected.CapPct is double cap
                ? PngWriter.FixedRange(cap)
                : PngWriter.AutoRange(resid);
    }
}
