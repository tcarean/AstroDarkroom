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

// HistogramSourceViewModel.cs - the switch between the histograms' two sources.
//
// The same pattern as MapScaleViewModel: two radio chips, one ticked, and the tab listens to
// Changed and rebuilds its rows. It decides nothing - it translates a choice. What differs from
// the map's scale is that the second option can be MISSING: a FITS has no preview, and neither
// does a raw whose preview could not be decoded. Then the switch goes off and the choice returns
// to the raw data, because that always exists.

using System;
using System.Collections.Generic;

namespace FlatCheck.Wpf
{
    /// <summary>One histogram source. The XAML sees the label and the tick; the rest is the
    /// owner's.</summary>
    public sealed class HistogramSourceOption : ObservableObject
    {
        readonly HistogramSourceViewModel _owner;
        bool _selected;

        /// <summary>Makes the chip.</summary>
        /// <param name="owner">The switch the chip belongs to; told when it is ticked.</param>
        /// <param name="isPreview">True for the camera's preview, false for the raw
        /// data.</param>
        /// <param name="label">The chip's text, Romanian.</param>
        internal HistogramSourceOption(HistogramSourceViewModel owner, bool isPreview, string label)
        {
            _owner = owner;
            IsPreview = isPreview;
            Label = label;
        }

        public string Label { get; }

        /// <summary>True for the camera's preview, false for the raw data.</summary>
        internal bool IsPreview { get; }

        /// <summary>
        /// The XAML binding on this is TwoWay. Ticking goes through the owner, so the other one
        /// is unticked in the view-model too, not only in the RadioButton group.
        /// </summary>
        public bool IsSelected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value) _owner.Select(this); }
        }

        /// <summary>Unticks without re-entering the owner.</summary>
        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    /// <summary>The two sources plus the chosen one. The tab holds it as a property and listens
    /// to <see cref="Changed"/>.</summary>
    public sealed class HistogramSourceViewModel : ObservableObject
    {
        HistogramSourceOption _selected;
        bool _enabled;

        /// <summary>Makes the two chips, the raw data ticked; disabled until a file with a
        /// preview arrives.</summary>
        public HistogramSourceViewModel()
        {
            // The labels name what each one IS, not the file format.
            Raw = new HistogramSourceOption(this, false, "Date brute (ce masoara unealta)");
            Preview = new HistogramSourceOption(this, true, "Ce a aratat aparatul pe ecran");
            Options = new[] { Raw, Preview };

            _selected = Raw;
            Raw.IsSelected = true;
        }

        internal HistogramSourceOption Raw { get; }
        internal HistogramSourceOption Preview { get; }

        public IReadOnlyList<HistogramSourceOption> Options { get; }

        /// <summary>The chosen source. Never null.</summary>
        public HistogramSourceOption Selected
        {
            get => _selected;
            private set { if (Set(ref _selected, value)) Raise(nameof(IsPreviewSelected)); }
        }

        public bool IsPreviewSelected => _selected.IsPreview;

        /// <summary>
        /// False when the file has no preview. Turning it off forces the choice back to the raw
        /// data, so the tab does not stay on a source that does not exist.
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

        /// <summary>The source changed: the tab rebuilds its rows from the result it
        /// holds.</summary>
        internal event Action Changed;

        /// <summary>Makes the option the chosen one and raises <see cref="Changed"/>; the
        /// preview, while the switch is disabled, is refused and the tick goes back to the raw
        /// data.</summary>
        /// <param name="option"><see cref="Raw"/> or <see cref="Preview"/>.</param>
        internal void Select(HistogramSourceOption option)
        {
            if (ReferenceEquals(option, _selected))
            {
                if (!option.IsSelected) option.IsSelected = true;
                return;
            }
            if (option.IsPreview && !_enabled)
            {
                // Not possible: there is nothing to show. The tick stays on the raw data.
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
