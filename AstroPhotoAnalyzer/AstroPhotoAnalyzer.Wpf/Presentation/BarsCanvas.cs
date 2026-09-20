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

// BarsCanvas.cs - o histograma desenata direct, cu un marcaj vertical optional.
//
// Un element propriu in loc de o suta de dreptunghiuri legate: histograma L* are 101
// cosuri, iar un ItemsControl cu o suta de elemente s-ar reaseza la fiecare redimensionare.
// OnRender deseneaza tot intr-o trecere. Marcajul e negrul hartiei (in L*), ca sa se vada
// pe loc cat din histograma cade sub el.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf
{
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

        public IReadOnlyList<double> Values { get => (IReadOnlyList<double>)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

        /// <summary>Pozitia marcajului, in indici de cos (0..Values.Count). NaN = fara marcaj.</summary>
        public double Marker { get => (double)GetValue(MarkerProperty); set => SetValue(MarkerProperty, value); }

        public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
        public Brush MarkerBrush { get => (Brush)GetValue(MarkerBrushProperty); set => SetValue(MarkerBrushProperty, value); }

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

            double m = Marker;
            if (!double.IsNaN(m) && m >= 0 && m <= v.Count)
            {
                double x = m * bw;
                dc.DrawLine(new Pen(MarkerBrush, 1.5), new Point(x, 0), new Point(x, h));
            }
        }
    }
}
