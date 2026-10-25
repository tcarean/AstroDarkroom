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

// HistEqViewViewModel.cs - the view switch of the histogram tab.
//
// The same pattern as MapScaleViewModel and HistogramSourceViewModel: radio chips, one ticked,
// the tab listens to Changed. What differs is that the LIST depends on the result - four
// positions plus the composite plus the mosaic on a CFA frame, a single view on mono - so it is
// rebuilt on every run, keeping the ticked chip if there is still one with the same label. It
// decides nothing: it translates a choice into "which raster is encoded".

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>What a histogram-equalised view shows: the composite (RGB, or the mono
    /// frame), one CFA position, or the undebayered mosaic.</summary>
    internal enum HistEqViewKind { Composite, Channel, Mosaic }

    /// <summary>One view. The XAML sees the label and the tick; the kind and the index are the
    /// owner's.</summary>
    public sealed class HistEqViewOption : ObservableObject
    {
        readonly HistEqViewViewModel _owner;
        bool _selected;

        /// <summary>Makes the chip.</summary>
        /// <param name="owner">The switch the chip belongs to; told when it is ticked.</param>
        /// <param name="kind">What the view shows.</param>
        /// <param name="index">The channel's index for <see cref="HistEqViewKind.Channel"/>; 0
        /// otherwise.</param>
        /// <param name="label">The chip's text.</param>
        internal HistEqViewOption(HistEqViewViewModel owner, HistEqViewKind kind, int index, string label)
        {
            _owner = owner;
            Kind = kind;
            Index = index;
            Label = label;
        }

        public string Label { get; }

        internal HistEqViewKind Kind { get; }

        /// <summary>The channel's index for Kind = Channel; 0 otherwise.</summary>
        internal int Index { get; }

        /// <summary>The XAML binding on this is TwoWay. Ticking goes through the owner, so the
        /// others are unticked.</summary>
        public bool IsSelected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value) _owner.Select(this); }
        }

        /// <summary>Unticks without re-entering the owner.</summary>
        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    /// <summary>The histogram tab's view chips, rebuilt from each result, with the one
    /// ticked.</summary>
    public sealed class HistEqViewViewModel : ObservableObject
    {
        IReadOnlyList<HistEqViewOption> _options = Array.Empty<HistEqViewOption>();
        HistEqViewOption _selected;

        /// <summary>
        /// The list is REPLACED on every run, not cleared and refilled - the same reason as
        /// ExposureTabViewModel.Histograms: two overlapping runs on an observable collection leave
        /// duplicated rows.
        /// </summary>
        public IReadOnlyList<HistEqViewOption> Options
        {
            get => _options;
            private set { if (Set(ref _options, value)) Raise(nameof(IsVisible)); }
        }

        /// <summary>The chosen view; null only before the first run.</summary>
        public HistEqViewOption Selected
        {
            get => _selected;
            private set => Set(ref _selected, value);
        }

        /// <summary>A single chip has nothing to switch, so the row is not shown.</summary>
        public bool IsVisible => _options.Count > 1;

        /// <summary>The view changed: the tab re-encodes from the result it holds.</summary>
        internal event Action Changed;

        /// <summary>
        /// Rebuilds the list from the result. Does NOT raise Changed - the tab rebuilds its
        /// picture by itself after caching what it received already encoded from the run.
        /// </summary>
        /// <param name="r">The run's result: a CFA frame gets mosaic, RGB and one chip per
        /// position, a mono frame a single "mono" chip.</param>
        internal void Rebuild(HistEqResult r)
        {
            var list = new List<HistEqViewOption>();
            if (r.IsCfa)
            {
                // The mosaic first, so the default: it is the view that matches Siril on a raw
                // opened undebayered, that is the one the user has read patterns from so far.
                // The composite is richer, but it is not the familiar one.
                list.Add(new HistEqViewOption(this, HistEqViewKind.Mosaic, 0, "Mozaic"));
                list.Add(new HistEqViewOption(this, HistEqViewKind.Composite, 0, "RGB"));
                for (int i = 0; i < r.Channels.Length; i++)
                    list.Add(new HistEqViewOption(this, HistEqViewKind.Channel, i, r.Channels[i].Label));
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

        /// <summary>Makes the option the chosen one, unticks the others and raises
        /// <see cref="Changed"/>; nothing but the tick when it is already chosen.</summary>
        /// <param name="option">One of <see cref="Options"/>.</param>
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
