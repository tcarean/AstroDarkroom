// Stats.cs - primitive numerice pure: percentile, interpolare, solver, verdicte.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    internal static class Stats
    {
        internal static double Percentile(List<double> data, double p)
        {
            if (data.Count == 0) return 0;
            var s = data.ToArray();
            Array.Sort(s);
            if (s.Length == 1) return s[0];
            double idx = p / 100.0 * (s.Length - 1);
            int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
            double frac = idx - lo;
            return s[lo] * (1 - frac) + s[hi] * frac; // interpolare liniara, ca numpy
        }

        // MOARTA: nimeni n-o cheama. LargeScalePtp are p5/p95 inline, cu praguri
        // diferite de default-ul p2/p98 de aici. De sters in faza 8.
        internal static double RobustPtp(IEnumerable<double> a, double lo = 2, double hi = 98)
        {
            var l = a.ToList();
            return Percentile(l, hi) - Percentile(l, lo);
        }

        internal static double Interp(double x, double[] xs, double[] ys)
        {
            if (xs.Length == 0) return 0;
            if (x <= xs[0]) return ys[0];
            if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
            int lo = 0, hi = xs.Length - 1;
            while (hi - lo > 1) { int m = (lo + hi) / 2; if (xs[m] <= x) lo = m; else hi = m; }
            double t = (x - xs[lo]) / (xs[hi] - xs[lo]);
            return ys[lo] + t * (ys[hi] - ys[lo]);
        }

        // Gauss cu pivotare partiala; intoarce null daca singular.
        internal static double[] Solve(double[,] A, double[] b, int n)
        {
            var M = new double[n, n + 1];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) M[i, j] = A[i, j]; M[i, n] = b[i]; }
            for (int col = 0; col < n; col++)
            {
                int piv = col; double best = Math.Abs(M[col, col]);
                for (int r = col + 1; r < n; r++) if (Math.Abs(M[r, col]) > best) { best = Math.Abs(M[r, col]); piv = r; }
                if (best < 1e-15) return null;
                if (piv != col) for (int j = 0; j <= n; j++) { var t = M[col, j]; M[col, j] = M[piv, j]; M[piv, j] = t; }
                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double factor = M[r, col] / M[col, col];
                    for (int j = col; j <= n; j++) M[r, j] -= factor * M[col, j];
                }
            }
            var x = new double[n];
            for (int i = 0; i < n; i++) x[i] = M[i, n] / M[i, i];
            return x;
        }

        internal static string Verdict(double val, double ok, double bad)
            => val < ok ? "OK" : (val < bad ? "SUSPECT" : "PROBLEMA");
        internal static int Rank(string s) => s == "OK" ? 0 : (s == "SUSPECT" ? 1 : 2);
    }
}
