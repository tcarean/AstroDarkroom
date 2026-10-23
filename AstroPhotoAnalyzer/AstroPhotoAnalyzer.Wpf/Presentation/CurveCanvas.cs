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

// CurveCanvas.cs - the chart of a tone curve (R-147), drawn directly, like BarsCanvas.
//
// It receives the curve already sampled by the core, the control points and the positions of
// the classes; it draws the frame, the grid, the "no change" diagonal, the curve, the points and
// a vertical line with a label for each class. XMax chooses how much of the input axis is seen:
// 1 for the whole curve, less for the shadow chart. The output axis goes up to the largest
// value drawn, so that the magnified shadows do not leave the frame.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The chart of a tone curve on 0..<see cref="XMax"/>: grid with tick labels, the diagonal,
    /// the sampled <see cref="Points"/>, the <see cref="ControlPoints"/> as discs and a dotted
    /// vertical with a rotated label per <see cref="CurveMarker"/>; redrawn when any property
    /// changes.
    /// </summary>
    public sealed class CurveCanvas : FrameworkElement
    {
        /// <summary>
        /// Registers a dependency property of this element that triggers a redraw.
        /// </summary>
        /// <typeparam name="T">The property's type.</typeparam>
        /// <param name="name">The property's name, as nameof gives it.</param>
        /// <param name="fallback">The default value; null is allowed for a reference type.</param>
        /// <returns>The registered property.</returns>
        static DependencyProperty Register<T>(string name, T fallback) => DependencyProperty.Register(
            name, typeof(T), typeof(CurveCanvas), new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PointsProperty = Register<IReadOnlyList<Point>>(nameof(Points), null);
        public static readonly DependencyProperty ControlPointsProperty = Register<IReadOnlyList<Point>>(nameof(ControlPoints), null);
        public static readonly DependencyProperty MarkersProperty = Register<IReadOnlyList<CurveMarker>>(nameof(Markers), null);
        public static readonly DependencyProperty XMaxProperty = Register(nameof(XMax), 1.0);
        public static readonly DependencyProperty StrokeProperty = Register<Brush>(nameof(Stroke), Brushes.SteelBlue);
        public static readonly DependencyProperty GridBrushProperty = Register<Brush>(nameof(GridBrush), Brushes.DimGray);
        public static readonly DependencyProperty TextBrushProperty = Register<Brush>(nameof(TextBrush), Brushes.Gainsboro);

        public IReadOnlyList<Point> Points { get => (IReadOnlyList<Point>)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
        public IReadOnlyList<Point> ControlPoints { get => (IReadOnlyList<Point>)GetValue(ControlPointsProperty); set => SetValue(ControlPointsProperty, value); }
        public IReadOnlyList<CurveMarker> Markers { get => (IReadOnlyList<CurveMarker>)GetValue(MarkersProperty); set => SetValue(MarkersProperty, value); }

        /// <summary>The right end of the input axis.</summary>
        public double XMax { get => (double)GetValue(XMaxProperty); set => SetValue(XMaxProperty, value); }

        public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
        public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
        public Brush TextBrush { get => (Brush)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

        /// <summary>A label in the chart's font, at this element's DPI.</summary>
        /// <param name="s">The text.</param>
        /// <param name="size">The font size in device-independent units.</param>
        /// <returns>The formatted text, in <see cref="TextBrush"/>.</returns>
        FormattedText Text(string s, double size = 11) => new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, TextBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        /// <summary>
        /// Draws the whole chart in one pass; draws nothing without a plot area, with fewer than
        /// two points, or with an <see cref="XMax"/> that is not positive. The output axis ends at
        /// <see cref="XMax"/> or at the largest output of a point within it, whichever is larger.
        /// </summary>
        /// <param name="dc">WPF's drawing context for this pass.</param>
        protected override void OnRender(DrawingContext dc)
        {
            const double left = 44, bottom = 24, top = 8, right = 10;
            double w = ActualWidth - left - right, h = ActualHeight - top - bottom;
            IReadOnlyList<Point> pts = Points;
            double xMax = XMax;
            if (w <= 0 || h <= 0 || pts == null || pts.Count < 2 || !(xMax > 0)) return;

            double yMax = xMax;
            foreach (Point p in pts) if (p.X <= xMax && p.Y > yMax) yMax = p.Y;
            Point At(double x, double y) => new(left + x / xMax * w, top + h - y / yMax * h);

            var grid = new Pen(GridBrush, 0.5);
            for (int i = 0; i <= 4; i++)
            {
                double gx = xMax * i / 4, gy = yMax * i / 4;
                dc.DrawLine(grid, At(gx, 0), At(gx, yMax));
                dc.DrawLine(grid, At(0, gy), At(xMax, gy));
                FormattedText tx = Text(gx.ToString("0.###", CultureInfo.InvariantCulture));
                dc.DrawText(tx, new Point(At(gx, 0).X - tx.Width / 2, top + h + 4));
                FormattedText ty = Text(gy.ToString("0.###", CultureInfo.InvariantCulture));
                dc.DrawText(ty, new Point(left - ty.Width - 6, At(0, gy).Y - ty.Height / 2));
            }

            // The diagonal: no change.
            double d = Math.Min(xMax, yMax);
            dc.DrawLine(new Pen(GridBrush, 1.2), At(0, 0), At(d, d));

            dc.PushClip(new RectangleGeometry(new Rect(left, top, w, h)));
            IReadOnlyList<CurveMarker> markers = Markers;
            if (markers != null)
                foreach (CurveMarker m in markers)
                {
                    if (m.X > xMax) continue;
                    var dash = new Pen(GridBrush, 1) { DashStyle = DashStyles.Dot };
                    dc.DrawLine(dash, At(m.X, 0), At(m.X, yMax));
                    FormattedText t = Text(m.Label);
                    dc.PushTransform(new RotateTransform(-90, At(m.X, 0).X + 3, top + 4 + t.Width));
                    dc.DrawText(t, new Point(At(m.X, 0).X + 3, top + 4 + t.Width));
                    dc.Pop();
                }

            var geometry = new StreamGeometry();
            using (StreamGeometryContext g = geometry.Open())
            {
                g.BeginFigure(At(pts[0].X, pts[0].Y), false, false);
                for (int i = 1; i < pts.Count; i++) g.LineTo(At(pts[i].X, pts[i].Y), true, true);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, new Pen(Stroke, 2), geometry);

            IReadOnlyList<Point> control = ControlPoints;
            if (control != null)
                foreach (Point p in control)
                    if (p.X <= xMax) dc.DrawEllipse(Stroke, null, At(p.X, p.Y), 4, 4);
            dc.Pop();

            dc.DrawRectangle(null, new Pen(GridBrush, 1), new Rect(left, top, w, h));
        }
    }
}
