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

// BarsCanvas.cs - a histogram drawn directly, with up to two vertical markers.
//
// An element of its own instead of a hundred bound rectangles: the L* histogram has 101 bins,
// and an ItemsControl with a hundred elements would lay itself out again at every resize.
// OnRender draws everything in one pass. The markers are paper black and the visibility
// threshold (both in L*), the second dashed so that it is told from the first when a paper's
// black sits near the threshold; how much of the histogram falls under each is seen at once.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// A bar chart of <see cref="Values"/>, one bar per value, scaled to the tallest, with up to
    /// two optional vertical markers, <see cref="Marker"/> solid and <see cref="SecondMarker"/>
    /// dashed; redrawn when any of its properties changes.
    /// </summary>
    public sealed class BarsCanvas : FrameworkElement
    {
        public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
            nameof(Values), typeof(IReadOnlyList<double>), typeof(BarsCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MarkerProperty = DependencyProperty.Register(
            nameof(Marker), typeof(double), typeof(BarsCanvas),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            nameof(Fill), typeof(Brush), typeof(BarsCanvas),
            new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MarkerBrushProperty = DependencyProperty.Register(
            nameof(MarkerBrush), typeof(Brush), typeof(BarsCanvas),
            new FrameworkPropertyMetadata(Brushes.IndianRed, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SecondMarkerProperty = DependencyProperty.Register(
            nameof(SecondMarker), typeof(double), typeof(BarsCanvas),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SecondMarkerBrushProperty = DependencyProperty.Register(
            nameof(SecondMarkerBrush), typeof(Brush), typeof(BarsCanvas),
            new FrameworkPropertyMetadata(Brushes.Goldenrod, FrameworkPropertyMetadataOptions.AffectsRender));

        public IReadOnlyList<double> Values { get => (IReadOnlyList<double>)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

        /// <summary>
        /// The marker's value on the bins' axis, where bin i is centred on i (so 0..Values.Count - 1
        /// spans the bins and a half marks a boundary between two). NaN = no marker.
        /// </summary>
        public double Marker { get => (double)GetValue(MarkerProperty); set => SetValue(MarkerProperty, value); }

        public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
        public Brush MarkerBrush { get => (Brush)GetValue(MarkerBrushProperty); set => SetValue(MarkerBrushProperty, value); }

        /// <summary>
        /// The second marker's value on the same axis as <see cref="Marker"/>, drawn dashed so that
        /// it is told from the first when the two nearly coincide. NaN = no second marker.
        /// </summary>
        public double SecondMarker { get => (double)GetValue(SecondMarkerProperty); set => SetValue(SecondMarkerProperty, value); }

        public Brush SecondMarkerBrush { get => (Brush)GetValue(SecondMarkerBrushProperty); set => SetValue(SecondMarkerBrushProperty, value); }

        /// <summary>
        /// Draws the bars over the element's whole size, each as tall as its value's share of the
        /// largest, then the two marker lines, the second dashed; draws nothing without a size,
        /// without values, or when no value is positive.
        /// </summary>
        /// <param name="dc">WPF's drawing context for this pass.</param>
        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            IReadOnlyList<double> v = Values;
            if (v == null || v.Count == 0) return;

            double max = 0;
            foreach (double x in v) if (x > max) max = x;
            if (max <= 0) return;

            double bw = w / v.Count;
            for (int i = 0; i < v.Count; i++)
            {
                double bh = v[i] / max * (h - 2);
                if (bh <= 0) continue;
                dc.DrawRectangle(Fill, null, new Rect(i * bw, h - bh, Math.Max(1, bw - 0.5), bh));
            }

            DrawMarker(dc, Marker, new Pen(MarkerBrush, 1.5), bw, h, v.Count);
            DrawMarker(dc, SecondMarker, new Pen(SecondMarkerBrush, 1.5) { DashStyle = DashStyles.Dash }, bw, h, v.Count);

            // A vertical line at the value m over the whole height; nothing when m is NaN or outside
            // the bins. The bins are centred on their index (the L* histogram rounds a value into
            // its bin), so m sits half a bin right of bin m's left edge; until the review of
            // 2026-10-24 the line stood at that edge, half a bin left of its value.
            static void DrawMarker(DrawingContext dc, double m, Pen pen, double bw, double h, int count)
            {
                if (double.IsNaN(m) || m < -0.5 || m > count - 0.5) return;
                double x = (m + 0.5) * bw;
                dc.DrawLine(pen, new Point(x, 0), new Point(x, h));
            }
        }
    }
}
