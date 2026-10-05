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

// ToneCurve.cs - o curba de ton: puncte de control si spline-ul cubic NATURAL prin ele
// (R-140, ADR 0031).
//
// E spline-ul pe care il calculeaza unealta Curves Transformation din Siril in modul
// "cubic spline" (curve_transform.c: derivata a doua zero la capete, sistem tridiagonal,
// rezultatul taiat la 0..1) - de aceea tabelul de puncte se poate reface de mana acolo si
// da aceeasi imagine. Se aplica la fel pe fiecare plan, valoare cu valoare.
//
// Aplicarea trece printr-o tabela de 65 536 de trepte cu interpolare liniara intre ele: pe
// o sursa de 16 biti e spline-ul exact in fiecare nivel, pe float diferenta e sub 1e-6.
// Monotonia se judeca pe aceeasi tabela, dupa taiere: e curba care se aplica de fapt.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>Un punct de control; eticheta e clasa lui ("praf", "cer", ...) sau null la capete.</summary>
    internal readonly record struct CurvePoint(double X, double Y, string Label);

    internal sealed class ToneCurve
    {
        const int TableSize = 65536;

        readonly double[] _x, _y, _b, _c, _d;
        readonly float[] _table;

        internal IReadOnlyList<CurvePoint> Points { get; }

        /// <summary>Tabela taiata nu scade nicaieri.</summary>
        internal bool IsMonotone { get; }

        /// <summary>
        /// Punctele trebuie sa inceapa in (0, 0), sa se termine in (1, 1), sa aiba x strict
        /// crescator si y in 0..1; altfel ArgumentException.
        /// </summary>
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

        /// <summary>Coeficientii pe segment ai spline-ului natural: y = y_i + b t + c t^2 + d t^3, t = x - x_i.</summary>
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

        int Segment(double v)
        {
            int i = Array.BinarySearch(_x, v);
            if (i < 0) i = ~i - 1;
            return Math.Clamp(i, 0, _x.Length - 2);
        }

        /// <summary>Spline-ul in <paramref name="v"/>, taiat la 0..1; in afara 0..1 ia valoarea capatului.</summary>
        internal double Map(double v)
        {
            if (v <= 0) return _y[0];
            if (v >= 1) return _y[_y.Length - 1];
            int i = Segment(v);
            double t = v - _x[i];
            return Math.Clamp(_y[i] + t * (_b[i] + t * (_c[i] + t * _d[i])), 0, 1);
        }

        /// <summary>Panta spline-ului (netaiat) in <paramref name="v"/>: de cate ori creste acolo o diferenta mica, deci si zgomotul.</summary>
        internal double Slope(double v)
        {
            v = Math.Clamp(v, 0, 1);
            int i = Segment(v);
            double t = v - _x[i];
            return _b[i] + t * (2 * _c[i] + 3 * t * _d[i]);
        }

        /// <summary>Curba pe o valoare a rasterului, prin tabela, cu interpolare liniara intre trepte.</summary>
        internal float MapSample(float v)
        {
            if (!(v > 0)) return _table[0];
            if (v >= 1) return _table[TableSize - 1];
            float f = v * (TableSize - 1);
            int i = Math.Min((int)f, TableSize - 2);
            float lo = _table[i];
            return lo + (f - i) * (_table[i + 1] - lo);
        }

        /// <summary>Un raster nou cu curba aplicata pe fiecare plan; cel primit ramane neatins.</summary>
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

        /// <summary>Curba esantionata uniform pe 0..<paramref name="xMax"/>, pentru un grafic.</summary>
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
