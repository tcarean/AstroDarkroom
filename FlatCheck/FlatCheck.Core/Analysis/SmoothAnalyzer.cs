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

// SmoothAnalyzer.cs - modul 6: flat-ul NETEZIT. Pastreaza tot ce e neted in master si
// arunca tot ce nu e - textura difuzorului, umbrele de praf, petele - si intoarce un al
// doilea master, la rezolutie intreaga, plus masura a ce s-a scos.
//
// Pur: Img -> SmoothResult. Nu scrie nimic. Textul e in Report/SmoothReport.cs, fisierul
// il scrie Runs/SmoothRun.cs prin FitsWriter.
//
// CE FACE, pe fiecare pozitie Bayer separat (pe mono: pe tot cadrul):
//   1. mediana pe dale de k px (Metrics.TileSize: ~150 px pe senzor), cu dalele partiale
//      de la capat pastrate si cu centrul lor real (Img.TileMedians);
//   2. in fiecare dala, un fit PATRATIC local - 1, x, y, x2, xy, y2 - pe dalele vecine,
//      ponderat gaussian cu sigma_t dale, cu offseturile reale dintre centre;
//   3. robust: 3 runde de clipping bilateral la max(2.5 * MAD, 2 % din nivel) fata de
//      fitul local; o dala respinsa nu mai cantareste in fitul niciunei vecine;
//   4. pe fiecare pixel, media biliniara a predictiilor celor patru dale din jur, fiecare
//      evaluata la offsetul real al pixelului fata de centrul acelei dale.
//
// DE CE ASA, si nu altfel - masurat pe cinci mastere reale (2026-09-16, vezi
// docs/design/smooth-validation.md pentru cifre), fiindca doua variante mai evidente au picat:
//
//   - Un polinom global peste reziduul unui model radial NU urmareste structura de scara
//     medie pe care o are orice master real (petele monitorului la 1000-1500 px si +-3 %,
//     inelul descentrat al unui Nikkor, blob-urile unui LCD): reziduul pe dale scade pana
//     la gradul 10 fara podea, iar de la gradul 6 polinomul oscileaza in colturi.
//   - Un model radial pe inele (mediana pe inel, cum are modul de structura) PUNE INELE
//     IN FISIER: dincolo de cercul inscris inelele sunt arce partiale, orice structura
//     para ne-radiala face distributia pe inel bimodala si mediana sare de la un inel la
//     altul. Un camp local de 150 px nu poate compensa inele de 22 px, deci ajung in
//     iesire - vizibile ca cercuri concentrice in raportul dintre doua mastere netezite.
//     Modul de structura sufera de acelasi lucru doar pe harta; aici ar intra in flat.
//     Concluzia: impartirea in "radial" si "rest" e arbitrara, si singura cerinta e ca
//     iesirea sa fie neteda si sa urmareasca datele. Fara model radial, nimic de aici nu
//     stie de un centru, deci nu exista inele prin constructie.
//
// Un fit patratic local pe o fereastra de +-450 px urmareste curbura vignetarii (scara
// ~1800 px) pana in colt exact la ordinul al treilea, si extrapoleaza corect jumatatea
// de dala de dincolo de ultimul centru. Doua lectii care au costat cate o runda:
//   - clipping-ul relativ doar la MAD respingea 8 % din dale pe un master foarte curat
//     (Samyang, MAD 0.06 %): orice structura de 0.15 % devenea "outlier" si nu mai era
//     urmarita deloc. De aici podeaua absoluta de 2 % din nivel (ClipFloor);
//   - dala partiala de 8 px de pe marginea dreapta, fitata la o dala distanta de vecina
//     ei, punea o banda de eroare de 0.8 % de-a lungul marginii. De aici offseturile reale.
//
// SCARA. ScalePx e SIGMA nucleului gaussian, in pixeli de senzor (latimea la jumatate e
// ~2.35 sigma), implicit 300 px: celulele de tesatura masurate pe masterul Tokina au
// 290-340 px, si la sigma 300 un camp aleator cu lungimea aia de corelatie trece la
// ~30 % in RMS. Mai lat scoate mai mult din celule si mananca mai mult din structura
// reala de 300 px (inelul Nikkor-ului, 0.3-0.5 %, e luat partial la 300 si ramane la
// 225). E singurul buton.
//
// REZULTATE pe cele cinci mastere reale, la 300 px: verdictele si adancimea vignetarii
// neschimbate; componenta scoasa 0.12-0.39 % ptp pe cele patru curate (zgomot de pixel
// plus praful), 3.7 % pe cel cu tesatura, unde pata compacta a scazut de la +6.19 % la
// +0.62 % si textura din fisier de la +-3 % la +-0.3 %. Fara inele: +-0.02 %. Mozaicul
// CFA intact: nivelurile pozitiilor se misca sub 0.1 %. Pe un sintetic cu adevar
// cunoscut (vignetare la 55 % in colt, gradient 6 %, sa) eroarea maxima pe tot cadrul,
// colturi incluse, e 0.19 %.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    /// <summary>Ce a facut fitul pe o pozitie Bayer (sau pe tot cadrul, pe mono).</summary>
    internal sealed class SmoothPositionFit
    {
        internal int Tiles;
        internal int Rejected;
        /// <summary>RMS al dalelor pastrate fata de fitul local, ca fractie din nivel.</summary>
        internal double TileRms;
        /// <summary>Cate dale au avut un sistem singular si au cazut pe propria mediana.</summary>
        internal int FitFailed;
    }

    internal sealed class SmoothResult
    {
        /// <summary>Flat-ul netezit, la rezolutie intreaga, forma intrarii. Niciodata null.</summary>
        internal Img Smoothed;

        internal bool IsCfa;
        internal double CfaSpread;
        internal int SourceH, SourceW;

        /// <summary>Scara de netezire folosita: sigma nucleului, in pixeli de senzor.</summary>
        internal double ScalePx;
        /// <summary>Latura dalei in pixeli de senzor, si sigma nucleului in dale.</summary>
        internal int TilePx;
        internal double SigmaTiles;

        /// <summary>4 pe CFA, 1 pe mono.</summary>
        internal SmoothPositionFit[] Positions;

        /// <summary>Rasterul de analiza al componentei scoase: Prepare(intrare) / Prepare(netezit).</summary>
        internal Img Removed;
        internal int Width, Height;
        internal double RemovedP1Pct, RemovedP99Pct;
        internal double RemovedPtpPct;      // p95 - p5 pe dale
        internal double RemovedMaxTilePct;  // cea mai abatuta dala
        internal double RemovedSpotPct;     // cea mai adanca pata compacta, cu semn
        internal int RemovedSpotX, RemovedSpotY;
        /// <summary>RemovedMaxTilePct la sau peste Th.SmoothKeptWarn: s-a scos mai mult decat textura.</summary>
        internal bool RemovedFlagged;

        /// <summary>
        /// Textura fina a intrarii si a iesirii (Metrics.FineTexture pe rasterele de
        /// analiza), in procente. Prima spune daca era ceva de netezit, a doua daca a plecat;
        /// pe ele sta propunerea de scara (SmoothScaleAdvisor).
        /// </summary>
        internal double InputTexturePct, OutputTexturePct;

        /// <summary>Bateria de structura pe intrare si pe iesire, pe rasterele de analiza.</summary>
        internal StructureResult Before, After;
    }

    internal static class SmoothAnalyzer
    {
        /// <summary>Scara implicita, in pixeli de senzor. Calibrata pe un senzor de 6016 px.</summary>
        internal const double DefaultScalePx = 300;

        /// <summary>Clipping: k sigma din MAD, podeaua absoluta ca fractie din nivel, si rundele.</summary>
        internal const double ClipSigma = 2.5;
        internal const double ClipFloor = 0.02;
        internal const int ClipRounds = 3;

        /// <summary>
        /// Sigma nucleului in dale: scara e sigma in pixeli de SENZOR, <paramref name="tilePx"/>
        /// latura dalei tot in pixeli de senzor (pe CFA, de doua ori latura din rasterul
        /// pozitiei). Pe D3300 la 300 px: dale de 150, sigma 2 dale.
        /// </summary>
        internal static double SigmaTiles(double scalePx, int tilePx) => scalePx / tilePx;

        internal static SmoothResult Analyze(Img input, double scalePx = DefaultScalePx)
        {
            int h = input.H, w = input.W;

            // Regula CFA a lui --expo si --histeq: cele patru mediane de pozitie, pe un
            // esantion rar - ajunge ca sa decida, si nu costa o sortare de 6 M de valori.
            double[] meds = Enumerable.Range(0, 4)
                .Select(p => PositionMedian(input, p))
                .ToArray();
            double spread = meds.Max() / Math.Max(meds.Min(), 1e-12);
            bool cfa = Mosaic.LooksCfa(spread) && h >= 2 && w >= 2;

            int pitch = cfa ? 2 : 1;
            var fits = new List<SmoothPositionFit>();
            Img smoothed;
            int tilePx;
            double sigmaT;
            if (cfa)
            {
                var outputs = new Img[4];
                Img probe = Mosaic.Extract(input, 0);
                tilePx = Metrics.TileSize(probe) * pitch;
                sigmaT = SigmaTiles(scalePx, tilePx);
                for (int p = 0; p < 4; p++)
                {
                    Img pos = p == 0 ? probe : Mosaic.Extract(input, p);
                    outputs[p] = SmoothPosition(pos, sigmaT, out SmoothPositionFit fit);
                    fits.Add(fit);
                }
                smoothed = Mosaic.Merge(outputs, h, w);
            }
            else
            {
                tilePx = Metrics.TileSize(input);
                sigmaT = SigmaTiles(scalePx, tilePx);
                smoothed = SmoothPosition(input, sigmaT, out SmoothPositionFit fit);
                fits.Add(fit);
            }

            // Ce s-a scos, la rezolutia de analiza. Prepare e liniar, deci raportul
            // pregatitelor e pregatirea raportului, la o treizecime din memorie.
            Img prepIn = Img.Prepare(input);
            Img prepOut = Img.Prepare(smoothed);
            Img removed = Img.Divide(prepIn, prepOut);
            double[] sorted = Stats.Sorted(removed.D);
            double ptp = Metrics.LargeScalePtp(removed) * 100;
            double maxTile = Metrics.MaxTileDeviation(removed) * 100;
            var (spot, sx, sy) = Metrics.LocalSpots(removed);

            return new SmoothResult
            {
                Smoothed = smoothed,
                IsCfa = cfa,
                CfaSpread = spread,
                SourceH = h,
                SourceW = w,
                ScalePx = scalePx,
                TilePx = tilePx,
                SigmaTiles = sigmaT,
                Positions = fits.ToArray(),
                Removed = removed,
                Width = removed.W,
                Height = removed.H,
                RemovedP1Pct = (Stats.OfSorted(sorted, 1) - 1) * 100,
                RemovedP99Pct = (Stats.OfSorted(sorted, 99) - 1) * 100,
                RemovedPtpPct = ptp,
                RemovedMaxTilePct = maxTile,
                RemovedSpotPct = spot * 100,
                RemovedSpotX = sx,
                RemovedSpotY = sy,
                // !(x < prag), ca NaN sa cada pe partea atentionata - conventia Stats.Verdict
                RemovedFlagged = !(maxTile < Th.SmoothKeptWarn),
                InputTexturePct = Metrics.FineTexture(prepIn),
                OutputTexturePct = Metrics.FineTexture(prepOut),
                Before = StructureAnalyzer.AnalyzePrepared(prepIn),
                After = StructureAnalyzer.AnalyzePrepared(prepOut),
            };
        }

        /// <summary>Mediana unei pozitii Bayer pe un esantion din 8 in 8 pixeli.</summary>
        static double PositionMedian(Img a, int p)
        {
            int oy = p / 2, ox = p % 2;
            var vals = new List<double>();
            for (int y = oy; y < a.H; y += 16)
                for (int x = ox; x < a.W; x += 16)
                    vals.Add(a.At(y, x));
            return vals.Count == 0 ? 0 : Stats.Percentile(vals, 50);
        }

        /// <summary>Netezirea unui raster (o pozitie sau tot cadrul), la scara <paramref name="sigmaT"/> dale.</summary>
        internal static Img SmoothPosition(Img p, double sigmaT, out SmoothPositionFit fit)
        {
            int k = Metrics.TileSize(p);
            var (t, cy, cx) = Img.TileMedians(p, k);
            var (coef, keep, failed) = LocalQuadratic(t, cy, cx, k, sigmaT);

            int rejected = keep.Cast<bool>().Count(v => !v);
            double level = Stats.Percentile(t.D, 50);
            double ss = 0; int n = 0;
            for (int j = 0; j < t.H; j++)
                for (int i = 0; i < t.W; i++)
                    if (keep[j, i]) { double r = (t.At(j, i) - coef[j, i, 0]) / level; ss += r * r; n++; }
            fit = new SmoothPositionFit
            {
                Tiles = t.D.Length,
                Rejected = rejected,
                TileRms = n > 0 ? Math.Sqrt(ss / n) : 0,
                FitFailed = failed,
            };
            return Evaluate(coef, cy, cx, k, p.H, p.W);
        }

        // Monoamele fitului local: 1, x, y, x2, xy, y2.
        static readonly (int a, int b)[] Mono = { (0, 0), (1, 0), (0, 1), (2, 0), (1, 1), (0, 2) };

        /// <summary>
        /// Fitul patratic local pe grila de dale <paramref name="t"/>, cu clipping robust.
        /// Intoarce cei 6 coeficienti pe dala (in unitati de dala, fata de centrul ei),
        /// masca dalelor pastrate si cate sisteme au fost singulare.
        /// </summary>
        internal static (double[,,] Coef, bool[,] Keep, int Failed) LocalQuadratic(
            Img t, double[] cy, double[] cx, int k, double sigmaT)
        {
            int th = t.H, tw = t.W;
            int rad = (int)Math.Ceiling(3 * sigmaT);
            double radSq = (3 * sigmaT) * (3 * sigmaT) + 1;
            var keep = new bool[th, tw];
            for (int j = 0; j < th; j++) for (int i = 0; i < tw; i++) keep[j, i] = true;
            var coef = new double[th, tw, 6];
            double level = Stats.Percentile(t.D, 50);
            int failed = 0;

            var M = new double[6, 6];
            var rhs = new double[6];
            var m = new double[6];
            var res = new List<double>(th * tw);

            for (int round = 0; round <= ClipRounds; round++)
            {
                failed = 0;
                for (int j = 0; j < th; j++)
                    for (int i = 0; i < tw; i++)
                    {
                        Array.Clear(M, 0, M.Length);
                        Array.Clear(rhs, 0, rhs.Length);
                        for (int dy = -rad; dy <= rad; dy++)
                        {
                            int jj = j + dy;
                            if (jj < 0 || jj >= th) continue;
                            double oy = (cy[jj] - cy[j]) / k;
                            for (int dx = -rad; dx <= rad; dx++)
                            {
                                int ii = i + dx;
                                if (ii < 0 || ii >= tw) continue;
                                if (dx * dx + dy * dy > radSq) continue;
                                if (!keep[jj, ii]) continue;
                                double ox = (cx[ii] - cx[i]) / k;
                                double g = Math.Exp(-(ox * ox + oy * oy) / (2 * sigmaT * sigmaT));
                                for (int q = 0; q < 6; q++)
                                    m[q] = Math.Pow(ox, Mono[q].a) * Math.Pow(oy, Mono[q].b);
                                double z = t.At(jj, ii);
                                for (int q = 0; q < 6; q++)
                                {
                                    rhs[q] += g * z * m[q];
                                    for (int r = 0; r < 6; r++) M[q, r] += g * m[q] * m[r];
                                }
                            }
                        }
                        double[] sol = Stats.Solve(M, rhs, 6);
                        if (sol == null)
                        {
                            // toate vecinele respinse, sau o grila de 1x1: dala isi tine mediana
                            failed++;
                            sol = new double[6];
                            sol[0] = t.At(j, i);
                        }
                        for (int q = 0; q < 6; q++) coef[j, i, q] = sol[q];
                    }

                // Reziduul fata de nivelul local al fitului, relativ la nivelul cadrului.
                res.Clear();
                for (int j = 0; j < th; j++)
                    for (int i = 0; i < tw; i++)
                        if (keep[j, i]) res.Add((t.At(j, i) - coef[j, i, 0]) / level);
                double med = Stats.Percentile(res, 50);
                var dev = res.Select(r => Math.Abs(r - med)).ToList();
                double mad = Stats.Percentile(dev, 50) * 1.4826;
                if (mad <= 0) break;
                double cut = Math.Max(ClipSigma * mad, ClipFloor);
                for (int j = 0; j < th; j++)
                    for (int i = 0; i < tw; i++)
                        keep[j, i] = Math.Abs((t.At(j, i) - coef[j, i, 0]) / level - med) < cut;
            }
            return (coef, keep, failed);
        }

        /// <summary>
        /// Media biliniara a predictiilor celor patru dale din jurul fiecarui pixel, fiecare
        /// evaluata la offsetul real al pixelului fata de centrul acelei dale. Dincolo de
        /// ultimul centru ponderea e a dalei de margine si predictia ei se extrapoleaza.
        /// </summary>
        internal static Img Evaluate(double[,,] coef, double[] cy, double[] cx, int k, int h, int w)
        {
            int th = cy.Length, tw = cx.Length;
            var o = new Img(h, w);

            // pe coloane: indexul dalei din stanga, fractia, si offseturile fata de cele doua dale
            var i0 = new int[w]; var fu = new double[w]; var dx0 = new double[w]; var dx1 = new double[w];
            for (int x = 0; x < w; x++)
            {
                double px = x + 0.5;
                double u = FracIndex(px, cx);
                int i = Math.Clamp((int)Math.Floor(u), 0, Math.Max(0, tw - 2));
                i0[x] = i;
                fu[x] = tw < 2 ? 0 : Math.Clamp(u - i, 0, 1);
                dx0[x] = (px - cx[i]) / k;
                dx1[x] = tw < 2 ? 0 : (px - cx[i + 1]) / k;
            }
            for (int y = 0; y < h; y++)
            {
                double py = y + 0.5;
                double v = FracIndex(py, cy);
                int j = Math.Clamp((int)Math.Floor(v), 0, Math.Max(0, th - 2));
                double fv = th < 2 ? 0 : Math.Clamp(v - j, 0, 1);
                double dy0 = (py - cy[j]) / k;
                double dy1 = th < 2 ? 0 : (py - cy[j + 1]) / k;
                int j1 = Math.Min(j + 1, th - 1);
                for (int x = 0; x < w; x++)
                {
                    int i = i0[x], i1 = Math.Min(i + 1, tw - 1);
                    double a = Quad(coef, j, i, dx0[x], dy0);
                    double b = Quad(coef, j, i1, dx1[x], dy0);
                    double c = Quad(coef, j1, i, dx0[x], dy1);
                    double d = Quad(coef, j1, i1, dx1[x], dy1);
                    double top = a * (1 - fu[x]) + b * fu[x];
                    double bot = c * (1 - fu[x]) + d * fu[x];
                    o.Set(y, x, top * (1 - fv) + bot * fv);
                }
            }
            return o;
        }

        static double Quad(double[,,] c, int j, int i, double dx, double dy)
            => c[j, i, 0] + c[j, i, 1] * dx + c[j, i, 2] * dy
             + c[j, i, 3] * dx * dx + c[j, i, 4] * dx * dy + c[j, i, 5] * dy * dy;

        /// <summary>
        /// Indexul fractionar al pozitiei <paramref name="p"/> in sirul de centre: ca np.interp
        /// peste (centre, 0..n-1), cu prindere la capete.
        /// </summary>
        static double FracIndex(double p, double[] centres)
        {
            int n = centres.Length;
            if (n == 1 || p <= centres[0]) return 0;
            if (p >= centres[n - 1]) return n - 1;
            int lo = 0, hi = n - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (centres[mid] <= p) lo = mid; else hi = mid; }
            return lo + (p - centres[lo]) / (centres[hi] - centres[lo]);
        }
    }
}
