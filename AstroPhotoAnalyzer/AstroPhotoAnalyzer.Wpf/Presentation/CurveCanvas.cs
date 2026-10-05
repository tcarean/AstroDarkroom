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

// CurveCanvas.cs - graficul unei curbe de ton (R-147), desenat direct, ca BarsCanvas.
//
// Primeste curba deja esantionata de nucleu, punctele de control si pozitiile claselor;
// deseneaza cadrul, grila, diagonala "fara schimbare", curba, punctele si cate o linie
// verticala cu eticheta pentru fiecare clasa. XMax alege cat din axa intrarilor se vede:
// 1 pentru curba intreaga, mai putin pentru graficul umbrelor. Axa iesirilor merge pana
// la cea mai mare valoare desenata, ca umbrele marite sa nu iasa din cadru.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf
{
    public sealed class CurveCanvas : FrameworkElement
    {
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

        /// <summary>Capatul din dreapta al axei intrarilor.</summary>
        public double XMax { get => (double)GetValue(XMaxProperty); set => SetValue(XMaxProperty, value); }

        public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
        public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
        public Brush TextBrush { get => (Brush)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

        FormattedText Text(string s, double size = 11) => new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, TextBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

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

            // Diagonala: fara schimbare.
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
