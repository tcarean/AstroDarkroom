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

// ImageZoomViewModel.cs - the zoom state of a picture: fitted to the window, or a factor.
//
// The same pattern as MapScaleViewModel: it decides nothing, it holds a choice. What is its own
// is the STATE - fitted or manual, and the factor - so a test can drive it and the tab can reset
// it on a new file. Panning with the mouse, the wheel and the computation of "fitted" from the
// window's size belong to the ZoomableImage control: they are interaction, not decision, and need
// sizes only the control knows.
//
// EffectiveZoom is written by the control (a TwoWay binding): on "fitted" it is the factor the
// control computed, otherwise it is Zoom itself. It lives here so the label can say
// "potrivit (13 %)", and so a zoom-in started from "fitted" is a step, not a jump to 100 %.

using System;
using System.Windows.Input;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>A picture's zoom: fitted to its frame, or a manual factor between
    /// <see cref="Min"/> and <see cref="Max"/>, with the four commands of its toolbar.</summary>
    public sealed class ImageZoomViewModel : ObservableObject
    {
        internal const double Min = 0.1, Max = 8.0, Step = 1.25;

        bool _fit = true;
        double _zoom = 1.0;
        double _effective = 1.0;

        /// <summary>Starts fitted, with the manual factor at 100 %.</summary>
        public ImageZoomViewModel()
        {
            ZoomIn = new RelayCommand(() => Manual(EffectiveZoom * Step));
            ZoomOut = new RelayCommand(() => Manual(EffectiveZoom / Step));
            Fit = new RelayCommand(() => IsFit = true);
            Actual = new RelayCommand(() => Manual(1.0));
        }

        /// <summary>True: the control computes the factor so the whole picture fits.</summary>
        public bool IsFit
        {
            get => _fit;
            set { if (Set(ref _fit, value)) Raise(nameof(Label)); }
        }

        /// <summary>The manual factor, 1 = 100 %; it counts when IsFit is false. Clamped to
        /// [Min, Max].</summary>
        public double Zoom
        {
            get => _zoom;
            set
            {
                double v = Math.Clamp(value, Min, Max);
                if (Set(ref _zoom, v)) Raise(nameof(Label));
            }
        }

        /// <summary>What is really on screen. The control writes it; on manual it equals
        /// Zoom.</summary>
        public double EffectiveZoom
        {
            get => _effective;
            set { if (Set(ref _effective, value)) Raise(nameof(Label)); }
        }

        /// <summary>"potrivit (13 %)" or "125 %".</summary>
        public string Label => IsFit ? T($"fit ({EffectiveZoom * 100:0} %)", $"potrivit ({EffectiveZoom * 100:0} %)") : $"{Zoom * 100:0} %";

        public ICommand ZoomIn { get; }
        public ICommand ZoomOut { get; }
        public ICommand Fit { get; }
        public ICommand Actual { get; }

        /// <summary>
        /// Switches to manual at the given factor. The factor first, then leaving "fitted": that
        /// way the control applies the new value once, not the old one first.
        /// </summary>
        /// <param name="zoom">The factor, 1 = 100 %; clamped to [Min, Max].</param>
        internal void Manual(double zoom)
        {
            Zoom = zoom;
            IsFit = false;
        }

        /// <summary>Back to fitted; the tab calls it on a new file, NOT on a change of
        /// view.</summary>
        internal void Reset()
        {
            Zoom = 1.0;
            IsFit = true;
        }
    }
}
