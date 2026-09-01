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

// Metrics.cs - geometria si metricile pure. Fiecare functie e o transformare
// deterministica a rasterului; niciuna nu scrie nimic si niciuna nu are stare.
//
// ATENTIE la centrul folosit de apelanti: modelul radial si asimetria se calculeaza
// in jurul centrului SENZORULUI, nu al varfului fitat de FitCenter. Vezi CLAUDE.md -
// centrarea pe varf ar absorbi exact gradientul cautat.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    internal static class Metrics
    {
        // ========================================================= geometrie
        // Vertexul unui paraboloid fitat pe log(I) = centrul de iluminare.
        internal static (double cx, double cy) FitCenter(Img img)
        {
            int h = img.H, w = img.W;
            // normal equations pentru A=[1,xn,yn,xn^2,yn^2,xn*yn], 6 necunoscute
            int step = Math.Max(1, Math.Min(h, w) / 200);
            var AtA = new double[6, 6];
            var Atb = new double[6];
            for (int y = 0; y < h; y += step)
                for (int x = 0; x < w; x += step)
                {
                    double xn = (x - w / 2.0) / (w / 2.0);
                    double yn = (y - h / 2.0) / (h / 2.0);
                    double val = Math.Log(Math.Max(img.At(y, x), 1e-12));
                    double[] r = { 1, xn, yn, xn * xn, yn * yn, xn * yn };
                    for (int i = 0; i < 6; i++)
                    {
                        Atb[i] += r[i] * val;
                        for (int j = 0; j < 6; j++) AtA[i, j] += r[i] * r[j];
                    }
                }
            double[] coef = Stats.Solve(AtA, Atb, 6);
            if (coef == null) return (w / 2.0, h / 2.0);
            double b = coef[1], c = coef[2], d = coef[3], e = coef[4], f = coef[5];
            // M = [[2d, f],[f, 2e]]; sol = M^-1 * [-b,-c]
            double det = 2 * d * 2 * e - f * f;
            if (Math.Abs(det) < 1e-15) return (w / 2.0, h / 2.0);
            double sx = ((-b) * (2 * e) - f * (-c)) / det;
            double sy = ((2 * d) * (-c) - f * (-b)) / det;
            double cx = sx * (w / 2.0) + w / 2.0;
            double cy = sy * (h / 2.0) + h / 2.0;
            if (!(cx > 0 && cx < w && cy > 0 && cy < h)) return (w / 2.0, h / 2.0);
            return (cx, cy);
        }

        // Profil radial robust (percentila per inel) + model 2D reconstruit prin interpolare.
        internal static (double[] centers, double[] prof, Img model) RadialModel(Img img, double cx, double cy, int nbins = 80, double pct = 50)
        {
            int h = img.H, w = img.W;
            double rmax = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (r > rmax) rmax = r;
                }
            var binVals = new List<double>[nbins];
            for (int i = 0; i < nbins; i++) binVals[i] = new List<double>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    int idx = Math.Min((int)(r / rmax * nbins), nbins - 1);
                    binVals[idx].Add(img.At(y, x));
                }
            var centers = new List<double>();
            var prof = new List<double>();
            for (int i = 0; i < nbins; i++)
                if (binVals[i].Count >= 8)
                {
                    centers.Add((i + 0.5) / nbins * rmax);
                    prof.Add(Stats.Percentile(binVals[i], pct));
                }
            var ca = centers.ToArray();
            var pa = prof.ToArray();
            var model = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    model.Set(y, x, Stats.Interp(r, ca, pa));
                }
            return (ca, pa, model);
        }

        // ========================================================= metrici
        internal static (Dictionary<string, double> vals, double ctr) CornerStats(Img img, double frac = 0.05)
        {
            int h = img.H, w = img.W;
            int bh = Math.Max(4, (int)(h * frac)), bw = Math.Max(4, (int)(w * frac));
            double Med(int y0, int y1, int x0, int x1)
            {
                var l = new List<double>();
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) l.Add(img.At(y, x));
                return Stats.Percentile(l, 50);
            }
            var vals = new Dictionary<string, double>
            {
                ["stanga-sus"] = Med(0, bh, 0, bw),
                ["dreapta-sus"] = Med(0, bh, w - bw, w),
                ["stanga-jos"] = Med(h - bh, h, 0, bw),
                ["dreapta-jos"] = Med(h - bh, h, w - bw, w),
            };
            double ctr = Med(h / 2 - bh, h / 2 + bh, w / 2 - bw, w / 2 + bw);
            return (vals, ctr);
        }

        // Ptp intre sectoare, per inel. Doar in raza inscrisa (toate azimuturile esantionate).
        internal static List<(double r, double ptp)> AzimuthalAsymmetry(Img img, double cx, double cy, int nsect = 12, int nrings = 10)
        {
            int h = img.H, w = img.W;
            double rIns = Math.Min(Math.Min(cx, cy), Math.Min(w - cx, h - cy));
            var edges = new double[nrings + 1];
            for (int i = 0; i <= nrings; i++) edges[i] = 0.15 * rIns + (rIns - 0.15 * rIns) * i / nrings;
            var buckets = new List<double>[nrings, nsect];
            for (int i = 0; i < nrings; i++) for (int s = 0; s < nsect; s++) buckets[i, s] = new List<double>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double dx = x - cx, dy = y - cy;
                    double r = Math.Sqrt(dx * dx + dy * dy);
                    if (r < edges[0] || r >= edges[nrings]) continue;
                    double th = Math.Atan2(dy, dx);
                    int s = Math.Min((int)((th + Math.PI) / (2 * Math.PI) * nsect), nsect - 1);
                    int ri = -1;
                    for (int i = 0; i < nrings; i++) if (r >= edges[i] && r < edges[i + 1]) { ri = i; break; }
                    if (ri >= 0) buckets[ri, s].Add(img.At(y, x));
                }
            var outl = new List<(double, double)>();
            for (int i = 0; i < nrings; i++)
            {
                var meds = new List<double>();
                for (int s = 0; s < nsect; s++) if (buckets[i, s].Count >= 8) meds.Add(Stats.Percentile(buckets[i, s], 50));
                if (meds.Count >= nsect - 1)
                {
                    double mn = meds.Min(), mx = meds.Max(), md = Stats.Percentile(meds, 50);
                    outl.Add(((edges[i] + edges[i + 1]) / 2 / rIns, (mx - mn) / md * 100));
                }
            }
            return outl;
        }

        // Structura de scala mare (izolata de zgomot prin mediere pe tile-uri).
        internal static double LargeScalePtp(Img resid, double cx, double cy, double rIns, int ntiles = 40)
        {
            int k = Math.Max(2, (int)(resid.W / (double)ntiles));
            Img t = Img.BlockMean(resid, k);
            var inside = new List<double>();
            for (int y = 0; y < t.H; y++)
                for (int x = 0; x < t.W; x++)
                {
                    double tr = Math.Sqrt(((x + 0.5) * k - cx) * ((x + 0.5) * k - cx) + ((y + 0.5) * k - cy) * ((y + 0.5) * k - cy));
                    if (tr < rIns) inside.Add(t.At(y, x));
                }
            if (inside.Count < 10) return double.NaN;
            return Stats.Percentile(inside, 95) - Stats.Percentile(inside, 5);
        }

        // Fond local per tile (percentila mica = robust la stele/nebulozitate).
        internal static (double[] bg, double[] bx, double[] by, int th, int tw, int k) TileBackground(Img img, int ntiles = 40, double pct = 15)
        {
            int h = img.H, w = img.W;
            int k = Math.Max(4, (int)(w / (double)ntiles));
            int hh = h / k, ww = w / k;
            var bg = new double[hh * ww];
            var bx = new double[hh * ww];
            var by = new double[hh * ww];
            for (int ty = 0; ty < hh; ty++)
                for (int tx = 0; tx < ww; tx++)
                {
                    var vals = new List<double>();
                    for (int dy = 0; dy < k; dy++)
                        for (int dx = 0; dx < k; dx++)
                            vals.Add(img.At(ty * k + dy, tx * k + dx));
                    int idx = ty * ww + tx;
                    bg[idx] = Stats.Percentile(vals, pct);
                    bx[idx] = (tx + 0.5) * k;
                    by[idx] = (ty + 0.5) * k;
                }
            return (bg, bx, by, hh, ww, k);
        }
    }
}
