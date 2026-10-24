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

// The sensor-format chips of the Sintetic tab, on the MapScaleViewModel pattern: a choice becomes
// a crop in the text field, and nothing else. They are the fallback for a camera the lensfun
// database does not have - the main path stays the table, which fills the field by itself - so
// the user does not type an obscure value. The list comes from the core (SensorFormats); nothing
// is decided here.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>One sensor-format chip: its label and crop ("APS-C 1.5") and the core's note
    /// as its tooltip.</summary>
    public sealed class SensorFormatOption : ObservableObject
    {
        readonly SensorFormatViewModel _owner;
        bool _selected;

        /// <summary>Makes the chip for one of the core's formats.</summary>
        /// <param name="owner">The chip row it belongs to; told when it is ticked.</param>
        /// <param name="format">The format, from <c>SensorFormats.All</c>.</param>
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

        /// <summary>Unticks without re-entering the owner.</summary>
        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    /// <summary>The row of sensor-format chips behind the crop field: a click announces a
    /// format through <see cref="Chosen"/>; a typed crop only lights the matching
    /// chip.</summary>
    public sealed class SensorFormatViewModel : ObservableObject
    {
        SensorFormatOption _selected;
        bool _following;

        /// <summary>One chip per format of the core, none ticked.</summary>
        public SensorFormatViewModel()
        {
            Options = SensorFormats.All.Select(f => new SensorFormatOption(this, f)).ToList();
        }

        public IReadOnlyList<SensorFormatOption> Options { get; }

        /// <summary>The chosen chip, or null when the crop came from the table or the
        /// keyboard.</summary>
        public SensorFormatOption Selected
        {
            get => _selected;
            private set => Set(ref _selected, value);
        }

        internal event Action<SensorFormat> Chosen;

        /// <summary>Makes the option the chosen one and unticks the others; announces it through
        /// <see cref="Chosen"/> unless the tick came from <see cref="Follow"/>.</summary>
        /// <param name="option">One of <see cref="Options"/>.</param>
        internal void Select(SensorFormatOption option)
        {
            if (ReferenceEquals(option, _selected)) return;
            foreach (var o in Options)
                if (!ReferenceEquals(o, option)) o.Unselect();
            Selected = option;
            if (!_following) Chosen?.Invoke(option.Format);
        }

        /// <summary>
        /// The chips show the format of the field's value: the nearest one lights up (within
        /// SensorFormats' tolerance), without announcing a choice; a value with no format, or
        /// none, turns them all off. The first version turned them off on any keystroke, and
        /// 1.523 from the table looked like a value fallen from the sky.
        /// </summary>
        /// <param name="crop">The crop factor in the field, or null when it holds none.</param>
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

        /// <summary>No chip: the crop on screen is no longer a chip's.</summary>
        internal void Clear()
        {
            foreach (var o in Options) o.Unselect();
            Selected = null;
        }
    }
}
