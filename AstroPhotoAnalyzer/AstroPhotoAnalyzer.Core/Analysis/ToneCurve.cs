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

// ToneCurve.cs - a tone curve: control points and the NATURAL cubic spline through them (R-140,
// ADR 0032).
//
// It is the spline that the Curves Transformation tool of Siril computes in "cubic spline" mode
// (curve_transform.c: second derivative zero at the ends, a tridiagonal system, the result
// clamped to 0..1) - which is why the table of points can be redone by hand there and gives the
// same image. It is applied alike on every plane, value by value.
//
// Applying it goes through a table of 65 536 steps with linear interpolation between them: on a
// 16-bit source it is the spline at every level to float precision (within 1.5e-7 on the curves
// measured), on float the difference is under 1e-6, except within a step of the corner where a
// curve clamped at 1 meets it: there the chord cuts the corner (1.6e-6 on V-014's Maxima, up to
// 7.8e-6 on synthetic anchors).
// The monotonicity is judged on the same table, after clamping: it is the curve that is
// actually applied.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// A control point; the label is its class ("praf", "cer", ...) or null at the ends (0, 0)
    /// and (1, 1) - unless the dust's point is itself (0, 0), and keeps its label.
    /// </summary>
    /// <param name="X">The input, 0..1.</param>
    /// <param name="Y">The output, 0..1.</param>
    /// <param name="Label">
    /// The name of the point's class, as the map's path names it; or null.
    /// </param>
    internal readonly record struct CurvePoint(double X, double Y, string Label);

    /// <summary>
    /// A tone curve: its control points, the natural cubic spline through them clamped to 0..1,
    /// and the 65 536-entry table it is applied through.
    /// </summary>
    internal sealed class ToneCurve
    {
        const int TableSize = 65536;

        readonly double[] _x, _y, _b, _c, _d;
        readonly float[] _table;

        internal IReadOnlyList<CurvePoint> Points { get; }

        /// <summary>The clamped table decreases nowhere.</summary>
        internal bool IsMonotone { get; }

        /// <summary>
        /// Solves the natural spline through the points and fills the table the curve is applied
        /// through, judging its monotonicity on it.
        /// </summary>
        /// <param name="points">
        /// The control points: they must start at (0, 0), end at (1, 1), have x strictly
        /// increasing and y in 0..1; otherwise ArgumentException.
        /// </param>
        internal ToneCurve(IReadOnlyList<CurvePoint> points)
        {
            if (points == null || points.Count < 2) throw new ArgumentException("o curba are cel putin doua puncte");
            int n = points.Count;
            if (points[0].X != 0 || points[0].Y != 0) throw new ArgumentException("curba trebuie sa inceapa in (0, 0)");
            if (points[n - 1].X != 1 || points[n - 1].Y != 1) throw new ArgumentException("curba trebuie sa se termine in (1, 1)");
            for (int i = 0; i < n; i++)
            {
                if (double.IsNaN(points[i].X) || double.IsNaN(points[i].Y) || points[i].Y < 0 || points[i].Y > 1)
                    throw new ArgumentException("punct de control in afara 0..1");
                if (i > 0 && !(points[i].X > points[i - 1].X)) throw new ArgumentException("x-urile punctelor de control trebuie sa creasca strict");
            }
            Points = points.ToArray();
            _x = points.Select(p => p.X).ToArray();
            _y = points.Select(p => p.Y).ToArray();
            (_b, _c, _d) = NaturalSpline(_x, _y);

            _table = new float[TableSize];
            bool monotone = true;
            for (int i = 0; i < TableSize; i++)
            {
                _table[i] = (float)Map(i / (double)(TableSize - 1));
                if (i > 0 && _table[i] < _table[i - 1]) monotone = false;
            }
            IsMonotone = monotone;
        }

        /// <summary>
        /// The per-segment coefficients of the natural spline: y = y_i + b t + c t^2 + d t^3,
        /// t = x - x_i.
        /// </summary>
        /// <param name="x">The points' inputs, strictly increasing; at least two.</param>
        /// <param name="y">The points' outputs, one per input.</param>
        /// <returns>
        /// b, c and d, one per point, for the segment that starts at it; the last point's are 0.
        /// </returns>
        static (double[] B, double[] C, double[] D) NaturalSpline(double[] x, double[] y)
        {
            int n = x.Length;
            var h = new double[n - 1];
            for (int i = 0; i < n - 1; i++) h[i] = x[i + 1] - x[i];
            var alpha = new double[n];
            for (int i = 1; i < n - 1; i++) alpha[i] = 3 / h[i] * (y[i + 1] - y[i]) - 3 / h[i - 1] * (y[i] - y[i - 1]);

            var l = new double[n]; var mu = new double[n]; var z = new double[n];
            l[0] = 1;
            for (int i = 1; i < n - 1; i++)
            {
                l[i] = 2 * (x[i + 1] - x[i - 1]) - h[i - 1] * mu[i - 1];
                mu[i] = h[i] / l[i];
                z[i] = (alpha[i] - h[i - 1] * z[i - 1]) / l[i];
            }
            l[n - 1] = 1;

            var b = new double[n]; var c = new double[n]; var d = new double[n];
            for (int j = n - 2; j >= 0; j--)
            {
                c[j] = z[j] - mu[j] * c[j + 1];
                b[j] = (y[j + 1] - y[j]) / h[j] - h[j] * (c[j + 1] + 2 * c[j]) / 3;
                d[j] = (c[j + 1] - c[j]) / (3 * h[j]);
            }
            return (b, c, d);
        }

        /// <summary>
        /// The segment that holds <paramref name="v"/>: the last point at or under it, kept within
        /// the segments.
        /// </summary>
        /// <param name="v">The input, 0..1.</param>
        /// <returns>The index of the segment's first point, 0 to the point count - 2.</returns>
        int Segment(double v)
        {
            int i = Array.BinarySearch(_x, v);
            if (i < 0) i = ~i - 1;
            return Math.Clamp(i, 0, _x.Length - 2);
        }

        /// <summary>
        /// The spline at <paramref name="v"/>, clamped to 0..1; outside 0..1 it takes the end's
        /// value.
        /// </summary>
        /// <param name="v">The input; any value but NaN.</param>
        /// <returns>The output, 0..1; NaN for a NaN input.</returns>
        internal double Map(double v)
        {
            if (v <= 0) return _y[0];
            if (v >= 1) return _y[_y.Length - 1];
            int i = Segment(v);
            double t = v - _x[i];
            return Math.Clamp(_y[i] + t * (_b[i] + t * (_c[i] + t * _d[i])), 0, 1);
        }

        /// <summary>
        /// The slope of the (unclamped) spline at <paramref name="v"/>: how many times a small
        /// difference grows there, and so the noise - where the curve is not clamped; where the
        /// clamp holds it at 0 or 1 the applied slope is 0, and this one is not.
        /// </summary>
        /// <param name="v">The input; clamped to 0..1, NaN left as it is.</param>
        /// <returns>The spline's derivative at the input; NaN for a NaN input.</returns>
        internal double Slope(double v)
        {
            v = Math.Clamp(v, 0, 1);
            int i = Segment(v);
            double t = v - _x[i];
            return _b[i] + t * (2 * _c[i] + 3 * t * _d[i]);
        }

        /// <summary>
        /// The curve on a value of the raster, through the table, with linear interpolation
        /// between the steps.
        /// </summary>
        /// <param name="v">
        /// The sample, 0..1; at or under 0, and NaN, give the table's first entry, at or over 1
        /// its last.
        /// </param>
        /// <returns>The curved sample, 0..1.</returns>
        internal float MapSample(float v)
        {
            if (!(v > 0)) return _table[0];
            if (v >= 1) return _table[TableSize - 1];
            float f = v * (TableSize - 1);
            int i = Math.Min((int)f, TableSize - 2);
            float lo = _table[i];
            return lo + (f - i) * (_table[i + 1] - lo);
        }

        /// <summary>
        /// A new raster with the curve applied to every plane; the one received stays untouched.
        /// </summary>
        /// <param name="r">The raster, 0..1.</param>
        /// <returns>
        /// A new raster of the same size and planes, every sample through
        /// <see cref="MapSample"/>, with the same <see cref="Raster.Source"/>.
        /// </returns>
        internal Raster Apply(Raster r)
        {
            var o = new Raster(r.H, r.W, r.Planes.Length) { Source = r.Source };
            for (int p = 0; p < r.Planes.Length; p++)
            {
                float[] src = r.Planes[p], dst = o.Planes[p];
                for (int i = 0; i < src.Length; i++) dst[i] = MapSample(src[i]);
            }
            return o;
        }

        /// <summary>
        /// The curve sampled uniformly over 0..<paramref name="xMax"/>, for a chart.
        /// </summary>
        /// <param name="xMax">The right end of the range; past 1 the curve stays at 1.</param>
        /// <param name="count">The number of samples, at least 2.</param>
        /// <returns>
        /// The points (x, <see cref="Map"/>(x)), x from 0 to <paramref name="xMax"/>.
        /// </returns>
        internal IReadOnlyList<(double X, double Y)> Sample(double xMax, int count)
        {
            var o = new (double, double)[count];
            for (int i = 0; i < count; i++)
            {
                double x = xMax * i / (count - 1);
                o[i] = (x, Map(x));
            }
            return o;
        }
    }
}
