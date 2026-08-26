// Core.cs - motorul numeric al FlatCheck: preprocesare, metrici, moduri, PNG.
// Vezi Program.cs pentru documentatia de utilizare si FlatCheckCli.cs pentru dispecerat.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace FlatCheck
{
    internal static class Core
    {
        // ========================================================= geometrie
        // Vertexul unui paraboloid fitat pe log(I) = centrul de iluminare.
        static (double cx, double cy) FitCenter(Img img)
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
        static (double[] centers, double[] prof, Img model) RadialModel(Img img, double cx, double cy, int nbins = 80, double pct = 50)
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
        static (Dictionary<string, double> vals, double ctr) CornerStats(Img img, double frac = 0.05)
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
        static List<(double r, double ptp)> AzimuthalAsymmetry(Img img, double cx, double cy, int nsect = 12, int nrings = 10)
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
        static double LargeScalePtp(Img resid, double cx, double cy, double rIns, int ntiles = 40)
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

        // ========================================================= moduri
        internal static string ModeStructure(string path, string plotPath, TextWriter outw)
        {
            Img raw = FitsReader.Load(path);
            Img img = Img.Prepare(raw);
            int h = img.H, w = img.W;

            outw.WriteLine($"\n=== STRUCTURA: {path} ===");
            outw.WriteLine($"    dimensiune analiza : {w} x {h} (dupa 2x2 + downsample)");
            outw.WriteLine($"    median / max ADU   : {Stats.Percentile(raw.D.ToList(), 50):F0} / {raw.D.Max():F0}");

            double cx = w / 2.0, cy = h / 2.0;
            var (fx, fy) = FitCenter(img);
            double off = Math.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy)) / Math.Sqrt(cx * cx + cy * cy) * 100;

            var (cor, ctr) = CornerStats(img);
            double cmean = cor.Values.Average();
            double vignStops = Math.Log2(ctr / cmean);
            double spread = (cor.Values.Max() - cor.Values.Min()) / cmean * 100;

            var (rc, prof, model) = RadialModel(img, cx, cy);
            var resid = Img.Divide(img, model);
            double rIns = Math.Min(Math.Min(cx, cy), Math.Min(w - cx, h - cy));
            double resPtp = LargeScalePtp(resid, cx, cy, rIns) * 100;

            var az = AzimuthalAsymmetry(img, cx, cy);
            double azMax = az.Count > 0 ? az.Max(v => v.ptp) : double.NaN;

            outw.WriteLine("\n  VIGNETARE (informativ, nu e un test)");
            outw.WriteLine($"    centru -> colt          : {vignStops:F2} stopuri (colt = {cmean / ctr * 100:F0}% din centru)");

            outw.WriteLine("\n  TESTE");
            outw.WriteLine($"    imprastiere 4 colturi   : {spread,5:F2} %   [{Stats.Verdict(spread, Th.CornerOk, Th.CornerBad)}]");
            foreach (var kv in cor)
                outw.WriteLine($"        {kv.Key,-14}: {kv.Value / cmean * 100,6:F2} % din media colturilor");
            outw.WriteLine($"    asimetrie azimutala max : {azMax,5:F2} %   [{Stats.Verdict(azMax, Th.AzimOk, Th.AzimBad)}]");
            outw.WriteLine($"    reziduu dupa model rad. : {resPtp,5:F2} %   [{Stats.Verdict(resPtp, Th.AzimOk, Th.AzimBad)}]");
            outw.WriteLine($"    offset centru iluminare : {off,5:F2} %   [{Stats.Verdict(off, Th.CentroidOk, Th.CentroidBad)}]");
            outw.WriteLine($"        varf iluminare = ({fx:F0}, {fy:F0})  vs  centru senzor = ({cx:F0}, {cy:F0})");

            string worst = new[] {
                Stats.Verdict(spread, Th.CornerOk, Th.CornerBad),
                Stats.Verdict(azMax, Th.AzimOk, Th.AzimBad),
                Stats.Verdict(resPtp, Th.AzimOk, Th.AzimBad),
                Stats.Verdict(off, Th.CentroidOk, Th.CentroidBad)
            }.OrderByDescending(Stats.Rank).First();

            outw.WriteLine($"\n  VERDICT: {worst}");
            if (worst != "OK")
            {
                outw.WriteLine("    Abatere de la simetria radiala => sursa neuniforma, light leak,");
                outw.WriteLine("    camera inclinata, sau ambient. NU e vignetare. Vezi harta reziduala.");
            }
            SaveResidualPng(plotPath, resid, outw);
            return worst;
        }

        internal static string ModeRatio(string pathA, string pathB, string plotPath, TextWriter outw)
        {
            Img a = Img.Prepare(FitsReader.Load(pathA));
            Img b = Img.Prepare(FitsReader.Load(pathB));
            if (a.H != b.H || a.W != b.W) throw new Exception("Cele doua imagini au dimensiuni diferite.");
            var ratio = Img.Divide(a, b);
            double med = Stats.Percentile(ratio.D.ToList(), 50);
            for (int i = 0; i < ratio.D.Length; i++) ratio.D[i] /= med;
            int h = ratio.H, w = ratio.W;

            outw.WriteLine($"\n=== TEST ROTATIE 180: {pathA}  /  {pathB} ===");
            outw.WriteLine("    Vignetarea si praful sunt fixe pe senzor -> se anuleaza in raport.");
            outw.WriteLine("    Orice gradient extern se inverseaza -> apare aici, la ~2x amplitudine.");

            double ptp = LargeScalePtp(ratio, w / 2.0, h / 2.0, Math.Sqrt((double)w * w + (double)h * h)) * 100;
            var (cor, _) = CornerStats(ratio);
            double cmean = cor.Values.Average();
            double spread = (cor.Values.Max() - cor.Values.Min()) / cmean * 100;

            outw.WriteLine($"\n    structura in raport A/B : {ptp,5:F2} %   [{Stats.Verdict(ptp, Th.RatioOk, Th.RatioBad)}]");
            outw.WriteLine($"    imprastiere 4 colturi   : {spread,5:F2} %   [{Stats.Verdict(spread, Th.RatioOk, Th.RatioBad)}]");
            outw.WriteLine("    (eroarea reala in fiecare master e ~jumatate din cifrele astea)");

            string worst = new[] { Stats.Verdict(ptp, Th.RatioOk, Th.RatioBad), Stats.Verdict(spread, Th.RatioOk, Th.RatioBad) }
                .OrderByDescending(Stats.Rank).First();
            outw.WriteLine($"\n  VERDICT: {worst}");
            outw.WriteLine(worst == "OK"
                ? "    Setup-ul de flats e curat. Poti construi biblioteca pe el."
                : "    Setup-ul contamineaza flat-ul. Repara sursa inainte de biblioteca.");
            SaveResidualPng(plotPath, ratio, outw);
            return worst;
        }

        // Fond local per tile (percentila mica = robust la stele/nebulozitate).
        static (double[] bg, double[] bx, double[] by, int th, int tw, int k) TileBackground(Img img, int ntiles = 40, double pct = 15)
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

        internal static void ModeLight(string path, string plotPath, TextWriter outw)
        {
            Img raw = FitsReader.Load(path);
            Img img = Img.Prepare(raw);
            int h = img.H, w = img.W;
            double cx = w / 2.0, cy = h / 2.0;
            double rCorner = Math.Sqrt(cx * cx + cy * cy);

            outw.WriteLine($"\n=== TEST END-TO-END: {path} ===");
            outw.WriteLine("    Astept un stack CALIBRAT cu flat, dar FARA background extraction.");

            var (bg, bx, by, th, tw, k) = TileBackground(img);
            int n = bg.Length;
            // fit simultan: fond = a + b*X + c*Y + d*R2 + e*R2^2 (plan absoarbe poluarea,
            // R2/R2^2 capteaza vignetare reziduala). Sigma-clip iterativ pt nebulozitate.
            var X = new double[n]; var Y = new double[n]; var R2 = new double[n]; var V = bg;
            for (int i = 0; i < n; i++)
            {
                X[i] = (bx[i] - cx) / rCorner;
                Y[i] = (by[i] - cy) / rCorner;
                R2[i] = X[i] * X[i] + Y[i] * Y[i];
            }
            var keep = new bool[n];
            for (int i = 0; i < n; i++) keep[i] = true;
            double[] coef = null;
            for (int iter = 0; iter < 4; iter++)
            {
                var AtA = new double[5, 5]; var Atb = new double[5];
                for (int i = 0; i < n; i++)
                {
                    if (!keep[i]) continue;
                    double[] r = { 1, X[i], Y[i], R2[i], R2[i] * R2[i] };
                    for (int a = 0; a < 5; a++) { Atb[a] += r[a] * V[i]; for (int b = 0; b < 5; b++) AtA[a, b] += r[a] * r[b]; }
                }
                coef = Stats.Solve(AtA, Atb, 5);
                if (coef == null) break;
                var res = new double[n];
                var kept = new List<double>();
                for (int i = 0; i < n; i++)
                {
                    res[i] = V[i] - (coef[0] + coef[1] * X[i] + coef[2] * Y[i] + coef[3] * R2[i] + coef[4] * R2[i] * R2[i]);
                    if (keep[i]) kept.Add(res[i]);
                }
                double medr = Stats.Percentile(kept, 50);
                var absdev = kept.Select(x => Math.Abs(x - medr)).ToList();
                double s = 1.4826 * Stats.Percentile(absdev, 50);
                double thr = 2.5 * Math.Max(s, 1e-9);
                for (int i = 0; i < n; i++) keep[i] = res[i] < thr;
            }

            double a0 = coef[0], b0 = coef[1], c0 = coef[2], d0 = coef[3], e0 = coef[4];
            double radAtCorner = d0 + e0;
            double radPct = -radAtCorner / a0 * 100;
            double planePct = Math.Sqrt(b0 * b0 + c0 * c0) / a0 * 100;
            int keptCount = keep.Count(x => x);

            outw.WriteLine($"\n    tile-uri folosite       : {keptCount} / {n} (restul = nebulozitate/stele, respinse)");
            outw.WriteLine($"    gradient de cer (plan)  : {planePct,5:F2} %  <- poluare luminoasa, IGNORA");
            outw.WriteLine($"    componenta radiala      : {radPct,6:+0.00;-0.00} %   [{Stats.Verdict(Math.Abs(radPct), 2.0, 5.0)}]  <- ASTA e flat-ul");

            outw.WriteLine("\n  INTERPRETARE");
            if (Math.Abs(radPct) < 2.0)
            {
                outw.WriteLine("    Fondul n-are componenta radiala. Flat-ul face treaba.");
                outw.WriteLine("    Asta e testul care conteaza - toate celelalte sunt proxy pentru el.");
            }
            else if (radPct > 0)
            {
                outw.WriteLine("    Fondul CADE spre margini => SUB-corectie. Flat-ul nu prinde toata");
                outw.WriteLine("    vignetarea: alta diafragma decat la lights, sau flat contaminat.");
            }
            else
            {
                outw.WriteLine("    Fondul CRESTE spre margini => SUPRA-corectie. Cel mai probabil");
                outw.WriteLine("    flat-darks lipsa/gresite: imparti la un flat care contine offset.");
            }

            // harta: componenta radiala extrasa (fara plan)
            var radMap = new Img(th, tw);
            for (int i = 0; i < n; i++) radMap.D[i] = 1.0 + (d0 * R2[i] + e0 * R2[i] * R2[i]) / a0;
            SaveResidualPng(plotPath, radMap, outw);
        }

        // ========================================================= expunere
        // Ruleaza pe un SINGUR flat BRUT NECALIBRAT (FITS convertit in Siril, nu master).
        // Stacking-ul normalizeaza si sterge nivelul, deci pe master n-ar avea sens.
        // C# nu citeste NEF - foloseste un flat convertit in Siril. Scala (white) e
        // auto-detectata; daca greseste, dai --sat (si --black).
        internal static void ModeExpo(string path, double? satOverride, double? blackOverride, bool sky, TextWriter outw)
        {
            Img data = FitsReader.Load(path); // NU Prepare - vrem CFA brut, la scala originala
            double mx = data.D.Max();
            double white;
            string src;
            if (satOverride.HasValue) { white = satOverride.Value; src = $"--sat={white}"; }
            else
            {
                if (mx <= 1.5) white = 1.0;
                else if (mx <= 4096 * 1.05) white = 4095.0;
                else if (mx <= 16384 * 1.05) white = 16383.0;
                else white = 65535.0;
                src = $"auto-detectat din max={mx:F0} (foloseste --sat/--black daca e gresit)";
            }
            double black = blackOverride ?? 0.0;
            double rng = white - black;

            // canale CFA (4 pozitii Bayer). Canalul cel mai luminos clipeaza primul.
            var ch = new List<double>[4];
            for (int i = 0; i < 4; i++) ch[i] = new List<double>();
            for (int y = 0; y < data.H; y++)
                for (int x = 0; x < data.W; x++)
                    ch[(y & 1) * 2 + (x & 1)].Add(data.At(y, x));
            var meds = ch.Select(c => Stats.Percentile(c, 50)).ToArray();
            double mmax = meds.Max(), mmin = meds.Min();
            bool isCfa = mmax / Math.Max(mmin, 1e-9) > 1.15;
            int bright = Array.IndexOf(meds, mmax);

            // CENTRUL cadrului, nu mediana globala. Un flat are prin definitie o
            // distributie larga (asta E vignetarea): centru luminos, colturi intunecate.
            // Mediana globala e trasa in jos de colturi -> daca o urmezi, supraexpui si
            // clipezi centrul. Ce conteaza pt clipping e CENTRUL, unde satureaza intai.
            // Masor o fereastra centrala 20%x20% din canalul cel mai luminos.
            // (Refac canalul cel mai luminos ca sub-imagine 2D ca sa pot lua centrul.)
            int chH = data.H / 2, chW = data.W / 2;
            int oy = bright / 2, ox = bright % 2;   // offset in mozaicul 2x2
            var cenVals = new List<double>();
            int cy0 = (int)(chH * 0.4), cy1 = (int)(chH * 0.6);
            int cx0 = (int)(chW * 0.4), cx1 = (int)(chW * 0.6);
            for (int y = cy0; y < cy1; y++)
                for (int x = cx0; x < cx1; x++)
                    cenVals.Add(data.At(y * 2 + oy, x * 2 + ox));
            double cenMed = (Stats.Percentile(cenVals, 50) - black) / rng * 100;    // decizie
            double globPct = (mmax - black) / rng * 100;                       // informativ
            double cenClip = cenVals.Count(v => v >= 0.98 * white) / (double)cenVals.Count * 100;

            outw.WriteLine($"\n=== EXPUNERE: {path} ===");
            outw.WriteLine($"    saturatie (white) : {white:F0}   black : {black:F0}   sursa: {src}");

            if (isCfa)
            {
                outw.WriteLine("    nivel median GLOBAL per canal CFA (informativ; nu stiu care e R/G/B):");
                string[] names = { "CFA[0,0]", "CFA[0,1]", "CFA[1,0]", "CFA[1,1]" };
                for (int i = 0; i < 4; i++)
                {
                    string mk = i == bright ? "  <- cel mai luminos" : "";
                    outw.WriteLine($"        {names[i]}: {(meds[i] - black) / rng * 100,5:F1}%{mk}");
                }
            }
            else
            {
                outw.WriteLine("    imagine mono/procesata (fara CFA)");
                // pt mono, centrul se ia pe intreaga imagine, nu pe sub-mozaic
                cenVals.Clear();
                int my0 = (int)(data.H * 0.4), my1 = (int)(data.H * 0.6);
                int mx0 = (int)(data.W * 0.4), mx1 = (int)(data.W * 0.6);
                for (int y = my0; y < my1; y++) for (int x = mx0; x < mx1; x++) cenVals.Add(data.At(y, x));
                cenMed = (Stats.Percentile(cenVals, 50) - black) / rng * 100;
                globPct = (Stats.Percentile(data.D.ToList(), 50) - black) / rng * 100;
                cenClip = cenVals.Count(v => v >= 0.98 * white) / (double)cenVals.Count * 100;
            }

            outw.WriteLine($"    canal cel mai luminos, mediana GLOBALA : {globPct,5:F1}%  (trasa in jos de colturi)");
            outw.WriteLine($"    canal cel mai luminos, CENTRU 20%      : {cenMed,5:F1}%  <- ASTA decide expunerea");
            outw.WriteLine($"    clipping in centru (>=98% white)       : {cenClip:F3}%");

            // SANITY: --expo are nevoie de flat BRUT necalibrat (mozaic Bayer intact,
            // scala ADU originala). Doua semne ca fisierul NU e brut:
            //   1. nivel non-fizic (<1%) -> flat-dark deja scazut / normalizat
            //   2. cele 4 pozitii CFA quasi-identice -> debayerizat
            double cfaSpread = mmax / Math.Max(mmin, 1e-9);
            if (cenMed < 1.0 || cfaSpread < 1.02)
            {
                outw.WriteLine();
                outw.WriteLine("    !! FISIERUL NU PARE UN FLAT BRUT !!");
                if (cenMed < 1.0)
                {
                    outw.WriteLine($"    Nivelul e ~{cenMed:F1}% (langa zero/negativ) -> flat-dark deja scazut,");
                    outw.WriteLine("    sau imagine normalizata. --expo are nevoie de nivelul ADU original.");
                }
                if (cfaSpread < 1.02)
                {
                    outw.WriteLine($"    Cele 4 canale CFA sunt quasi-identice (spread {cfaSpread:F3}) -> fisierul");
                    outw.WriteLine("    e DEBAYERIZAT. --expo are nevoie de mozaicul Bayer intact.");
                }
                outw.WriteLine();
                outw.WriteLine("    Cauza tipica: ai deschis NEF-ul in Siril si ai dat Save (debayerizeaza).");
                outw.WriteLine("    SOLUTIE: produ un FITS brut cu tab-ul Conversion din Siril (NU Open+Save),");
                outw.WriteLine("    care pastreaza mozaicul CFA nedebayerizat. (Sau ruleaza pe NEF in Python.)");
                return;
            }

            outw.WriteLine();

            const double cenLo = 50.0, cenHi = 65.0;   // tinta pt CENTRU (panou)
            if (sky)
            {
                // SKY FLAT: cerul se intuneca continuu si e plin de stele slabe. Nu
                // impinge in sus - SNR-ul masterului vine din NUMARUL de cadre, nu din a
                // urca un cadru la 55%. Tinta pt canalul dominant: 15-50%.
                const double skyLo = 15.0, skyHi = 50.0;
                outw.WriteLine("    [mod --sky: praguri pt flats pe cer, nu pe panou]");
                if (cenClip > Th.ExpoClip || cenMed > 60)
                {
                    outw.WriteLine("    VERDICT: PREA SUS" + (cenClip > Th.ExpoClip ? " (CLIPPING in centru!)" : " (risc stele in cadru)"));
                    outw.WriteLine("    Scade expunerea.");
                }
                else if (cenMed < skyLo)
                {
                    outw.WriteLine("    VERDICT: PREA JOS pentru sky flat");
                    outw.WriteLine($"    Creste cu ~{Math.Log2(30.0 / cenMed):F1} stopuri (dar repede, cerul se stinge).");
                }
                else if (cenMed <= skyHi)
                {
                    outw.WriteLine($"    VERDICT: OK pentru sky flat  (centru {skyLo:F0}-{skyHi:F0}%)");
                    outw.WriteLine("    La sky flat nivelul modest e corect. SNR-ul vine din numarul de cadre,");
                    outw.WriteLine("    nu din a urca expunerea (ai risca stele). NU forta mai sus.");
                }
                else
                {
                    outw.WriteLine("    VERDICT: cam sus, dar acceptabil");
                    outw.WriteLine("    Sub 60% e ok; peste, risti stele. Nu urca mai mult.");
                }
            }
            else if (cenClip > Th.ExpoClip || cenMed > 80)
            {
                double stops = cenMed > 0 ? Math.Log2(57.0 / cenMed) : -1;
                outw.WriteLine("    VERDICT: PREA SUS" + (cenClip > Th.ExpoClip ? " (CLIPPING in centru!)" : ""));
                outw.WriteLine($"    Scade expunerea cu ~{-stops:F1} stopuri.");
                if (cenClip > Th.ExpoClip)
                    outw.WriteLine("    Centrul clipeaza -> pierzi info de vignetare exact unde semnalul e maxim.");
            }
            else if (cenMed < 30)
            {
                outw.WriteLine("    VERDICT: PREA JOS");
                outw.WriteLine($"    Creste expunerea cu ~{Math.Log2(57.0 / cenMed):F1} stopuri.");
                outw.WriteLine("    Sub ~30% in centru masterul iese zgomotos (zgomot fixed-pattern).");
            }
            else if (cenMed < cenLo)
            {
                outw.WriteLine("    VERDICT: ACCEPTABIL, dar cam jos");
                outw.WriteLine($"    Ai putea creste cu ~{Math.Log2(57.0 / cenMed):F1} stopuri.");
            }
            else if (cenMed <= cenHi)
            {
                outw.WriteLine($"    VERDICT: OK  (centrul in tinta {cenLo:F0}-{cenHi:F0}%)");
            }
            else
            {
                outw.WriteLine("    VERDICT: ACCEPTABIL, dar cam sus");
                outw.WriteLine($"    Ai putea scadea cu ~{-Math.Log2(57.0 / cenMed):F1} stopuri.");
            }
        }

        // ========================================================= PNG (heatmap reziduu)
        // RdBu-like: albastru (jos) -> alb (1.0) -> rosu (sus). Scalat pe percentile 1..99.
        internal static void SaveResidualPng(string path, Img resid, TextWriter outw)
        {
            var vals = resid.D.ToList();
            double lo = Stats.Percentile(vals, 1), hi = Stats.Percentile(vals, 99);
            double pad = Math.Max(hi - lo, 1e-4) * 0.1;
            lo -= pad; hi += pad;
            double mid = 1.0;
            int h = resid.H, w = resid.W;
            var rgb = new byte[h * w * 3];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double v = resid.At(y, x);
                    (byte r, byte g, byte b) = Colormap(v, lo, mid, hi);
                    int p = (y * w + x) * 3;
                    rgb[p] = r; rgb[p + 1] = g; rgb[p + 2] = b;
                }
            WritePng(path, rgb, w, h);
            outw.WriteLine($"\n    -> harta salvata: {path}");
        }

        static (byte, byte, byte) Colormap(double v, double lo, double mid, double hi)
        {
            double t;
            byte r, g, b;
            if (v < mid)
            {
                t = (v - lo) / Math.Max(mid - lo, 1e-12); t = Math.Clamp(t, 0, 1);
                // albastru -> alb
                r = (byte)(255 * t); g = (byte)(255 * t); b = 255;
            }
            else
            {
                t = (v - mid) / Math.Max(hi - mid, 1e-12); t = Math.Clamp(t, 0, 1);
                // alb -> rosu
                r = 255; g = (byte)(255 * (1 - t)); b = (byte)(255 * (1 - t));
            }
            return (r, g, b);
        }

        static void WritePng(string path, byte[] rgb, int w, int h)
        {
            using var fs = File.Create(path);
            // semnatura
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            // IHDR
            var ihdr = new byte[13];
            WriteBE(ihdr, 0, w); WriteBE(ihdr, 4, h);
            ihdr[8] = 8;   // bit depth
            ihdr[9] = 2;   // color type RGB
            ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            WriteChunk(fs, "IHDR", ihdr);
            // IDAT: fiecare rand precedat de filter byte 0, apoi zlib-compresat
            using var raw = new MemoryStream();
            for (int y = 0; y < h; y++)
            {
                raw.WriteByte(0);
                raw.Write(rgb, y * w * 3, w * 3);
            }
            byte[] rawBytes = raw.ToArray();
            byte[] comp;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(rawBytes, 0, rawBytes.Length);
                comp = ms.ToArray();
            }
            WriteChunk(fs, "IDAT", comp);
            WriteChunk(fs, "IEND", Array.Empty<byte>());
        }

        static void WriteBE(byte[] b, int o, int v)
        { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        static void WriteChunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len, 0, 4);
            var tb = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(tb, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = Crc32(tb, data);
            var cb = new byte[4]; WriteBE(cb, 0, (int)crc); s.Write(cb, 0, 4);
        }

        static uint[] _crcTable;
        static uint Crc32(byte[] type, byte[] data)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
                    _crcTable[n] = c;
                }
            }
            uint crc = 0xffffffff;
            foreach (byte bb in type) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            foreach (byte bb in data) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            return crc ^ 0xffffffff;
        }
    }
}
