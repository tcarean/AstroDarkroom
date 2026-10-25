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
// curve clamped at 1 meets it: there the chord cuts the corner (up to 7.8e-6 on synthetic
// anchors).
// Whether the curve may be suggested is judged on the UNCLAMPED spline at the table's steps:
// it must decrease nowhere and stay within 0..1 (R-140, ADR 0039). Until the review of
// 2026-10-23 the clamped table was judged, and a spline that rose over 1 and came back was kept,
// held at 1 by the clamp: the Maxima of V-009, V-011 and V-014 mapped 21 to 41 % of the input
// range to white that way.

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

        /// <summary>
        /// The unclamped spline decreases nowhere between the table's steps, by more than
        /// SplineTolerance.
        /// </summary>
        internal bool IsMonotone { get; }

        /// <summary>
        /// The unclamped spline stays within 0..1 (to SplineTolerance) at every step of the table:
        /// the clamp changes nothing of the curve applied.
        /// </summary>
        internal bool IsWithinRange { get; }

        /// <summary>
        /// Monotone and within range: what a curve must be to be suggested (R-140, ADR 0039). A
        /// curve that is not may still be built and applied; it is never offered.
        /// </summary>
        internal bool IsSuggestible => IsMonotone && IsWithinRange;

        /// <summary>
        /// Solves the natural spline through the points and fills the table the curve is applied
        /// through, judging its monotonicity and its range on the unclamped spline at the same
        /// steps.
        /// </summary>
        /// <param name="points">
        /// The control points: they must start at (0, 0), end at (1, 1), have x strictly
        /// increasing and y in 0..1; otherwise ArgumentException.
        /// </param>
        internal ToneCurve(IReadOnlyList<CurvePoint> points)
        {
            (_x, _y) = Validate(points);
            Points = points.ToArray();
            (_b, _c, _d) = NaturalSpline(_x, _y);
            _table = new float[TableSize];
            (IsMonotone, IsWithinRange) = Judge(_x, _y, _b, _c, _d, _table);
        }

        /// <summary>
        /// The tolerance of the two judgements: a decrease between two steps, or an excursion past
        /// 0 or 1, smaller than this is the double arithmetic's noise, not the spline's shape (a
        /// flat segment evaluates to equal values only up to rounding). Far under a 16-bit step
        /// (1.5e-5), so no applied value is affected.
        /// </summary>
        const double SplineTolerance = 1e-9;

        /// <summary>Checks the control points and returns their inputs and their outputs.</summary>
        /// <param name="points">
        /// The control points: they must start at (0, 0), end at (1, 1), have x strictly
        /// increasing and y in 0..1; otherwise ArgumentException.
        /// </param>
        /// <returns>The inputs and the outputs, one array each, in the points' order.</returns>
        static (double[] X, double[] Y) Validate(IReadOnlyList<CurvePoint> points)
        {
            if (points == null || points.Count < 2) throw new ArgumentException(Tr.Pick("a curve has at least two points", "o curba are cel putin doua puncte"));
            int n = points.Count;
            if (points[0].X != 0 || points[0].Y != 0) throw new ArgumentException(Tr.Pick("the curve must start at (0, 0)", "curba trebuie sa inceapa in (0, 0)"));
            if (points[n - 1].X != 1 || points[n - 1].Y != 1) throw new ArgumentException(Tr.Pick("the curve must end at (1, 1)", "curba trebuie sa se termine in (1, 1)"));
            for (int i = 0; i < n; i++)
            {
                if (double.IsNaN(points[i].X) || double.IsNaN(points[i].Y) || points[i].Y < 0 || points[i].Y > 1)
                    throw new ArgumentException(Tr.Pick("a control point outside 0..1", "punct de control in afara 0..1"));
                if (i > 0 && !(points[i].X > points[i - 1].X)) throw new ArgumentException(Tr.Pick("the x of the control points must increase strictly", "x-urile punctelor de control trebuie sa creasca strict"));
            }
            return (points.Select(p => p.X).ToArray(), points.Select(p => p.Y).ToArray());
        }

        /// <summary>
        /// Whether the curve through <paramref name="points"/> would be suggestible: the same
        /// judgement the constructor makes, without allocating or filling the table. CurveAdvisor's
        /// cap probes it up to forty times per factor.
        /// </summary>
        /// <param name="points">The control points, as the constructor requires them.</param>
        /// <returns><c>true</c> when the spline is monotone and stays within 0..1.</returns>
        internal static bool WouldBeSuggestible(IReadOnlyList<CurvePoint> points)
        {
            var (x, y) = Validate(points);
            var (b, c, d) = NaturalSpline(x, y);
            var (monotone, within) = Judge(x, y, b, c, d, null);
            return monotone && within;
        }

        /// <summary>
        /// Evaluates the unclamped spline at every step of the table, fills
        /// <paramref name="table"/> with the clamped values when one is given, and judges the
        /// spline: monotone when it never decreases between two steps by more than
        /// SplineTolerance, within range when it never leaves 0..1 by more than it.
        /// </summary>
        /// <param name="x">The points' inputs, strictly increasing.</param>
        /// <param name="y">The points' outputs.</param>
        /// <param name="b">The spline's linear coefficients, from NaturalSpline.</param>
        /// <param name="c">The quadratic coefficients.</param>
        /// <param name="d">The cubic coefficients.</param>
        /// <param name="table">The table to fill, of TableSize entries; null to judge alone.</param>
        /// <returns>Whether the spline is monotone, and whether it stays within 0..1.</returns>
        static (bool Monotone, bool Within) Judge(double[] x, double[] y, double[] b, double[] c, double[] d, float[] table)
        {
            bool monotone = true, within = true;
            double previous = 0;
            for (int i = 0; i < TableSize; i++)
            {
                double raw = Unclamped(x, y, b, c, d, i / (double)(TableSize - 1));
                if (table != null) table[i] = (float)Math.Clamp(raw, 0, 1);
                if (i > 0 && raw < previous - SplineTolerance) monotone = false;
                if (raw < -SplineTolerance || raw > 1 + SplineTolerance) within = false;
                previous = raw;
            }
            return (monotone, within);
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
        /// <param name="x">The points' inputs, strictly increasing.</param>
        /// <param name="v">The input, 0..1.</param>
        /// <returns>The index of the segment's first point, 0 to the point count - 2.</returns>
        static int Segment(double[] x, double v)
        {
            int i = Array.BinarySearch(x, v);
            if (i < 0) i = ~i - 1;
            return Math.Clamp(i, 0, x.Length - 2);
        }

        /// <summary>
        /// The spline at <paramref name="v"/>, clamped to 0..1; outside 0..1 it takes the end's
        /// value.
        /// </summary>
        /// <param name="v">The input; any value but NaN.</param>
        /// <returns>The output, 0..1; NaN for a NaN input.</returns>
        internal double Map(double v) => Math.Clamp(Unclamped(v), 0, 1);

        /// <summary>
        /// The spline itself at <paramref name="v"/>, before the clamp: what IsMonotone and
        /// IsWithinRange judge. Outside 0..1 it takes the end's value.
        /// </summary>
        /// <param name="v">The input; any value but NaN.</param>
        /// <returns>The spline's value, which may leave 0..1 between the points; NaN for NaN.</returns>
        double Unclamped(double v) => Unclamped(_x, _y, _b, _c, _d, v);

        /// <summary>The unclamped spline of the given points and coefficients at <paramref name="v"/>.</summary>
        /// <param name="x">The points' inputs, strictly increasing.</param>
        /// <param name="y">The points' outputs.</param>
        /// <param name="b">The spline's linear coefficients, from NaturalSpline.</param>
        /// <param name="c">The quadratic coefficients.</param>
        /// <param name="d">The cubic coefficients.</param>
        /// <param name="v">The input; any value but NaN.</param>
        /// <returns>The spline's value; the end's value outside 0..1; NaN for NaN.</returns>
        static double Unclamped(double[] x, double[] y, double[] b, double[] c, double[] d, double v)
        {
            if (v <= 0) return y[0];
            if (v >= 1) return y[y.Length - 1];
            int i = Segment(x, v);
            double t = v - x[i];
            return y[i] + t * (b[i] + t * (c[i] + t * d[i]));
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
            int i = Segment(_x, v);
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
