// FlatCheck.cs - diagnostic cantitativ pentru master flats (port .NET al flatcheck.py)
// =====================================================================================
//
// CITESTE ASTA INTAI: CE FISIER II DAI
// ------------------------------------
// Cea mai probabila cauza de rezultat gresit nu e in cod, e in fisierul ales.
// Scriptul are nevoie de flat-uri din care s-a SCAZUT DEJA flat-dark-ul (offset-ul).
// Dupa scriptul Siril (OSC_Preprocessing / _WithoutDark), fisierul corect este:
//
//     process/pp_flat_stacked.fit        <- prefixul "pp_" = calibrat. ASTA.
//
// NU un .NEF brut, NU un stack necalibrat. Offset-ul (~150-600 ADU) aplatizeaza
// profilul, subraporteaza vignetarea cu ~1%, iar toate testele vor spune "OK"
// pentru ca profilul ramane simetric radial - doar amplitudinea e gresita.
//
// FORMATE ACCEPTATE: FITS (.fit/.fits/.fts), 16-bit int sau 32-bit float, mono sau
// color (ia media pe canale). Pentru NEF brut, lasa Siril sa produca FITS-ul - exact
// ce faci deja in workflow. (Un decoder RAW in .NET ar adauga o dependinta fragila
// fara castig, de vreme ce pipeline-ul tau trece oricum prin Siril.)
//
// IDEEA DE BAZA
// -------------
// Pentru un obiectiv fotografic fara tren optic (Samyang 135 f/2 pe D3300), un flat
// CORECT este, prin constructie, o functie *pur radiala* centrata pe axa optica.
// Orice abatere de la simetria radiala NU e vignetare - e eroare (sursa neuniforma,
// light leak, camera inclinata, ambient). "Flat bun" = "abaterea de la simetria
// radiala e sub zgomot". E o cifra, si o poti masura.
//
// TREI MODURI
// -----------
//   dotnet run -- process/pp_flat_stacked.fit                  # 1. STRUCTURA
//   dotnet run -- pp_flat_A.fit --ratio pp_flat_B.fit          # 2. ROTATIE 180
//   dotnet run -- process/pp_light_00001.fit --light           # 3. END-TO-END
//   dotnet run -- flat_brut.fit --expo                          # 4. EXPUNERE
//
// 4. EXPUNERE: pe un SINGUR flat BRUT NECALIBRAT (convertit in Siril, nu master -
//    stacking-ul normalizeaza si sterge nivelul). Spune cat de sus e CENTRUL cadrului
//    fata de saturatie si in ce directie sa modifici. Masoara CENTRUL (fereastra 20%),
//    nu mediana globala: un flat are distributie larga (centru luminos, colturi
//    intunecate = vignetarea), iar mediana globala e trasa in jos de colturi si te-ar
//    pacali sa supraexpui pana clipezi centrul. Tinta: centru la 50-65%. Histograma
//    camerei/ViewNX arata imaginea PROCESATA (debayer+WB+gamma) si pare "la limita"
//    cand datele brute sunt la ~40% - foloseste Siril pt histograma pe date brute.
//    Scala (white) auto-detectata; daca greseste, --sat (si --black). white-ul exact:
//    un cadru supraexpus, maxul de acolo E saturatia. (C# nu citeste NEF - flat Siril.)
//
// 1. STRUCTURA: e master flat-ul simetric radial? Ruleaza pe masterul final.
// 2. ROTATIE 180: valideaza SETUP-ul. Tragi 15 flats, rotesti camera 180 grade in
//    jurul axei optice, mai tragi 15, stackuiesti separat. Vignetarea+praful sunt
//    fixe pe senzor (identice in ambele); orice gradient extern se inverseaza -> apare
//    in A/B la ~2x amplitudine. PUNCT ORB: contaminare simetrica radial in jurul axei
//    (ex: cadere de backlight centrata pe monitor) trece nedetectata. Pt asta ai
//    testul de distanta: un set la 30cm, unul la 60cm, --ratio intre ele.
// 3. END-TO-END: un light CALIBRAT cu flat, dar FARA background extraction. Fondul nu
//    trebuie sa aiba componenta radiala. Gradientul de poluare (liniar) e separat de
//    vignetarea reziduala (radial) printr-un fit simultan plan+radial -> merge si pe
//    cer poluat. Semnul: fond care CADE spre margini = sub-corectie; care CRESTE =
//    supra-corectie (aproape sigur offset: flat-darks lipsa, sau lights necalibrate
//    cu bias). Modul 3 e singurul care inchide bucla; 1 si 2 sunt proxy.
//
// ISO-ul NU CONTEAZA: fiecare metrica e un raport, ISO e un scalar, se simplifica.
//
// PRAGURILE de mai jos sunt reguli empirice, nu constante fizice. Un punct de plecare.
// Ruleaza o data pe un setup despre care esti sigur si calibreaza-le pe cifrele tale.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace FlatCheck
{
    // ---------------- praguri (procente) ----------------
    static class Th
    {
        public const double CornerOk = 1.5, CornerBad = 4.0;
        public const double AzimOk = 1.5, AzimBad = 4.0;
        public const double CentroidOk = 4.0, CentroidBad = 10.0;
        public const double RatioOk = 1.0, RatioBad = 3.0;
        public const double ExpoLo = 50.0, ExpoHi = 60.0;  // tinta canal cel mai luminos
        public const double ExpoClip = 0.05;               // % pixeli saturati = clipping
    }

    // ---------------- imagine 2D simpla ----------------
    sealed class Img
    {
        public int H, W;
        public double[] D; // row-major, lungime H*W
        public Img(int h, int w) { H = h; W = w; D = new double[h * w]; }
        public double At(int y, int x) => D[y * W + x];
        public void Set(int y, int x, double v) => D[y * W + x] = v;
    }

    static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
            {
                Console.WriteLine("Utilizare:");
                Console.WriteLine("  flatcheck <flat.fit>                    # test structura");
                Console.WriteLine("  flatcheck <A.fit> --ratio <B.fit>       # test rotatie 180");
                Console.WriteLine("  flatcheck <light.fit> --light           # test end-to-end");
                Console.WriteLine("  flatcheck <flat_brut.fit> --expo        # verificare expunere");
                Console.WriteLine("  optional: -o <iesire.png> | --sat <white> --black <lvl> (pt --expo)");
                return 0;
            }

            string image = args[0];
            string ratio = GetOpt(args, "--ratio");
            bool light = args.Contains("--light");
            bool expo = args.Contains("--expo");
            string outPng = GetOpt(args, "-o") ?? GetOpt(args, "--out") ?? "flatcheck.png";
            double? sat = GetOptD(args, "--sat");
            double? black = GetOptD(args, "--black");

            try
            {
                if (expo) ModeExpo(image, sat, black);
                else if (ratio != null) ModeRatio(image, ratio, outPng);
                else if (light) ModeLight(image, outPng);
                else ModeStructure(image, outPng);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("EROARE: " + e.Message);
                return 1;
            }
            Console.WriteLine();
            return 0;
        }

        static string GetOpt(string[] a, string key)
        {
            int i = Array.IndexOf(a, key);
            return (i >= 0 && i + 1 < a.Length) ? a[i + 1] : null;
        }

        static double? GetOptD(string[] a, string key)
        {
            string s = GetOpt(a, key);
            if (s != null && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            return null;
        }

        // ========================================================= I/O FITS
        // FITS: header din carduri de 80 octeti, in blocuri de 2880. Apoi datele binare,
        // big-endian. BITPIX: 16=short, 32=int, -32=float, -64=double. Valoarea fizica =
        // BZERO + BSCALE * valoare_stocata. Ia primul HDU cu date (NAXIS>=2).
        static Img LoadFits(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            int pos = 0;
            while (pos < raw.Length)
            {
                var hdr = new Dictionary<string, string>();
                bool end = false;
                while (!end)
                {
                    if (pos + 2880 > raw.Length) throw new Exception("FITS: header incomplet.");
                    for (int c = 0; c < 36; c++)
                    {
                        string card = System.Text.Encoding.ASCII.GetString(raw, pos + c * 80, 80);
                        string key = card.Length >= 8 ? card.Substring(0, 8).Trim() : "";
                        if (key == "END") { end = true; }
                        if (card.Length > 10 && card[8] == '=')
                        {
                            string val = card.Substring(9);
                            int slash = val.IndexOf('/');
                            if (slash >= 0) val = val.Substring(0, slash);
                            hdr[key] = val.Trim().Trim('\'').Trim();
                        }
                    }
                    pos += 2880;
                }

                int bitpix = HInt(hdr, "BITPIX", 0);
                int naxis = HInt(hdr, "NAXIS", 0);
                if (naxis < 2)
                {
                    // HDU fara imagine (ex: primary gol) - sar peste datele lui (0) si continui
                    if (naxis == 0) continue;
                }
                int n1 = HInt(hdr, "NAXIS1", 0);
                int n2 = HInt(hdr, "NAXIS2", 0);
                int n3 = naxis >= 3 ? HInt(hdr, "NAXIS3", 1) : 1;
                double bzero = HDbl(hdr, "BZERO", 0.0);
                double bscale = HDbl(hdr, "BSCALE", 1.0);

                long npix = (long)n1 * n2 * Math.Max(1, n3);
                int bytesPer = Math.Abs(bitpix) / 8;
                long dataBytes = npix * bytesPer;
                long dataBlocks = (dataBytes + 2879) / 2880 * 2880;

                if (n1 > 0 && n2 > 0)
                {
                    var vals = new double[npix];
                    int p = pos;
                    for (long i = 0; i < npix; i++)
                    {
                        double v = ReadSample(raw, p, bitpix);
                        vals[i] = bzero + bscale * v;
                        p += bytesPer;
                    }
                    // color (n3 planuri, Siril = plane-sequential) -> media pe planuri
                    var img = new Img(n2, n1);
                    if (n3 <= 1)
                        Array.Copy(vals, img.D, n1 * n2);
                    else
                    {
                        long plane = (long)n1 * n2;
                        for (long i = 0; i < plane; i++)
                        {
                            double s = 0;
                            for (int c = 0; c < n3; c++) s += vals[c * plane + i];
                            img.D[i] = s / n3;
                        }
                    }
                    return img;
                }
                // HDU fara imagine 2D utila: sar peste blocurile lui de date si continui
                pos += (int)dataBlocks;
            }
            throw new Exception("FITS: niciun HDU cu imagine 2D gasit.");
        }

        static double ReadSample(byte[] b, int p, int bitpix)
        {
            switch (bitpix)
            {
                case 8: return b[p];
                case 16: return (short)((b[p] << 8) | b[p + 1]);
                case 32: return (int)((b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]);
                case -32:
                    {
                        int bits = (b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3];
                        return BitConverter.Int32BitsToSingle(bits);
                    }
                case -64:
                    {
                        long bits = 0;
                        for (int k = 0; k < 8; k++) bits = (bits << 8) | b[p + k];
                        return BitConverter.Int64BitsToDouble(bits);
                    }
                default: throw new Exception("BITPIX neacceptat: " + bitpix);
            }
        }

        static int HInt(Dictionary<string, string> h, string k, int def)
            => h.TryGetValue(k, out var v) && int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;
        static double HDbl(Dictionary<string, string> h, string k, double def)
            => h.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;

        // ========================================================= preprocesare
        static Img BlockMean(Img a, int k)
        {
            if (k <= 1) return a;
            int h = a.H / k, w = a.W / k;
            var o = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double s = 0;
                    for (int dy = 0; dy < k; dy++)
                        for (int dx = 0; dx < k; dx++)
                            s += a.At(y * k + dy, x * k + dx);
                    o.Set(y, x, s / (k * k));
                }
            return o;
        }

        // block-mean 2x2 (omoara CFA Bayer) apoi downsample la ~1200px pe latura lunga
        static Img Prepare(Img data, int targetLong = 1200)
        {
            Img img = BlockMean(data, 2);
            int k = Math.Max(1, (int)(Math.Max(img.H, img.W) / (double)targetLong));
            return BlockMean(img, k);
        }

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
            double[] coef = Solve(AtA, Atb, 6);
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

        // Gauss cu pivotare partiala; intoarce null daca singular.
        static double[] Solve(double[,] A, double[] b, int n)
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
                    prof.Add(Percentile(binVals[i], pct));
                }
            var ca = centers.ToArray();
            var pa = prof.ToArray();
            var model = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    model.Set(y, x, Interp(r, ca, pa));
                }
            return (ca, pa, model);
        }

        static double Interp(double x, double[] xs, double[] ys)
        {
            if (xs.Length == 0) return 0;
            if (x <= xs[0]) return ys[0];
            if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
            int lo = 0, hi = xs.Length - 1;
            while (hi - lo > 1) { int m = (lo + hi) / 2; if (xs[m] <= x) lo = m; else hi = m; }
            double t = (x - xs[lo]) / (xs[hi] - xs[lo]);
            return ys[lo] + t * (ys[hi] - ys[lo]);
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
                return Percentile(l, 50);
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
                for (int s = 0; s < nsect; s++) if (buckets[i, s].Count >= 8) meds.Add(Percentile(buckets[i, s], 50));
                if (meds.Count >= nsect - 1)
                {
                    double mn = meds.Min(), mx = meds.Max(), md = Percentile(meds, 50);
                    outl.Add(((edges[i] + edges[i + 1]) / 2 / rIns, (mx - mn) / md * 100));
                }
            }
            return outl;
        }

        // Structura de scala mare (izolata de zgomot prin mediere pe tile-uri).
        static double LargeScalePtp(Img resid, double cx, double cy, double rIns, int ntiles = 40)
        {
            int k = Math.Max(2, (int)(resid.W / (double)ntiles));
            Img t = BlockMean(resid, k);
            var inside = new List<double>();
            for (int y = 0; y < t.H; y++)
                for (int x = 0; x < t.W; x++)
                {
                    double tr = Math.Sqrt(((x + 0.5) * k - cx) * ((x + 0.5) * k - cx) + ((y + 0.5) * k - cy) * ((y + 0.5) * k - cy));
                    if (tr < rIns) inside.Add(t.At(y, x));
                }
            if (inside.Count < 10) return double.NaN;
            return Percentile(inside, 95) - Percentile(inside, 5);
        }

        static double RobustPtp(IEnumerable<double> a, double lo = 2, double hi = 98)
        {
            var l = a.ToList();
            return Percentile(l, hi) - Percentile(l, lo);
        }

        static double Percentile(List<double> data, double p)
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

        static string Verdict(double val, double ok, double bad)
            => val < ok ? "OK" : (val < bad ? "SUSPECT" : "PROBLEMA");
        static int Rank(string s) => s == "OK" ? 0 : (s == "SUSPECT" ? 1 : 2);

        // ========================================================= moduri
        static string ModeStructure(string path, string plotPath)
        {
            Img raw = LoadFits(path);
            Img img = Prepare(raw);
            int h = img.H, w = img.W;

            Console.WriteLine($"\n=== STRUCTURA: {path} ===");
            Console.WriteLine($"    dimensiune analiza : {w} x {h} (dupa 2x2 + downsample)");
            Console.WriteLine($"    median / max ADU   : {Percentile(raw.D.ToList(), 50):F0} / {raw.D.Max():F0}");

            double cx = w / 2.0, cy = h / 2.0;
            var (fx, fy) = FitCenter(img);
            double off = Math.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy)) / Math.Sqrt(cx * cx + cy * cy) * 100;

            var (cor, ctr) = CornerStats(img);
            double cmean = cor.Values.Average();
            double vignStops = Math.Log2(ctr / cmean);
            double spread = (cor.Values.Max() - cor.Values.Min()) / cmean * 100;

            var (rc, prof, model) = RadialModel(img, cx, cy);
            var resid = Divide(img, model);
            double rIns = Math.Min(Math.Min(cx, cy), Math.Min(w - cx, h - cy));
            double resPtp = LargeScalePtp(resid, cx, cy, rIns) * 100;

            var az = AzimuthalAsymmetry(img, cx, cy);
            double azMax = az.Count > 0 ? az.Max(v => v.ptp) : double.NaN;

            Console.WriteLine("\n  VIGNETARE (informativ, nu e un test)");
            Console.WriteLine($"    centru -> colt          : {vignStops:F2} stopuri (colt = {cmean / ctr * 100:F0}% din centru)");

            Console.WriteLine("\n  TESTE");
            Console.WriteLine($"    imprastiere 4 colturi   : {spread,5:F2} %   [{Verdict(spread, Th.CornerOk, Th.CornerBad)}]");
            foreach (var kv in cor)
                Console.WriteLine($"        {kv.Key,-14}: {kv.Value / cmean * 100,6:F2} % din media colturilor");
            Console.WriteLine($"    asimetrie azimutala max : {azMax,5:F2} %   [{Verdict(azMax, Th.AzimOk, Th.AzimBad)}]");
            Console.WriteLine($"    reziduu dupa model rad. : {resPtp,5:F2} %   [{Verdict(resPtp, Th.AzimOk, Th.AzimBad)}]");
            Console.WriteLine($"    offset centru iluminare : {off,5:F2} %   [{Verdict(off, Th.CentroidOk, Th.CentroidBad)}]");
            Console.WriteLine($"        varf iluminare = ({fx:F0}, {fy:F0})  vs  centru senzor = ({cx:F0}, {cy:F0})");

            string worst = new[] {
                Verdict(spread, Th.CornerOk, Th.CornerBad),
                Verdict(azMax, Th.AzimOk, Th.AzimBad),
                Verdict(resPtp, Th.AzimOk, Th.AzimBad),
                Verdict(off, Th.CentroidOk, Th.CentroidBad)
            }.OrderByDescending(Rank).First();

            Console.WriteLine($"\n  VERDICT: {worst}");
            if (worst != "OK")
            {
                Console.WriteLine("    Abatere de la simetria radiala => sursa neuniforma, light leak,");
                Console.WriteLine("    camera inclinata, sau ambient. NU e vignetare. Vezi harta reziduala.");
            }
            SaveResidualPng(plotPath, resid);
            return worst;
        }

        static string ModeRatio(string pathA, string pathB, string plotPath)
        {
            Img a = Prepare(LoadFits(pathA));
            Img b = Prepare(LoadFits(pathB));
            if (a.H != b.H || a.W != b.W) throw new Exception("Cele doua imagini au dimensiuni diferite.");
            var ratio = Divide(a, b);
            double med = Percentile(ratio.D.ToList(), 50);
            for (int i = 0; i < ratio.D.Length; i++) ratio.D[i] /= med;
            int h = ratio.H, w = ratio.W;

            Console.WriteLine($"\n=== TEST ROTATIE 180: {pathA}  /  {pathB} ===");
            Console.WriteLine("    Vignetarea si praful sunt fixe pe senzor -> se anuleaza in raport.");
            Console.WriteLine("    Orice gradient extern se inverseaza -> apare aici, la ~2x amplitudine.");

            double ptp = LargeScalePtp(ratio, w / 2.0, h / 2.0, Math.Sqrt((double)w * w + (double)h * h)) * 100;
            var (cor, _) = CornerStats(ratio);
            double cmean = cor.Values.Average();
            double spread = (cor.Values.Max() - cor.Values.Min()) / cmean * 100;

            Console.WriteLine($"\n    structura in raport A/B : {ptp,5:F2} %   [{Verdict(ptp, Th.RatioOk, Th.RatioBad)}]");
            Console.WriteLine($"    imprastiere 4 colturi   : {spread,5:F2} %   [{Verdict(spread, Th.RatioOk, Th.RatioBad)}]");
            Console.WriteLine("    (eroarea reala in fiecare master e ~jumatate din cifrele astea)");

            string worst = new[] { Verdict(ptp, Th.RatioOk, Th.RatioBad), Verdict(spread, Th.RatioOk, Th.RatioBad) }
                .OrderByDescending(Rank).First();
            Console.WriteLine($"\n  VERDICT: {worst}");
            Console.WriteLine(worst == "OK"
                ? "    Setup-ul de flats e curat. Poti construi biblioteca pe el."
                : "    Setup-ul contamineaza flat-ul. Repara sursa inainte de biblioteca.");
            SaveResidualPng(plotPath, ratio);
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
                    bg[idx] = Percentile(vals, pct);
                    bx[idx] = (tx + 0.5) * k;
                    by[idx] = (ty + 0.5) * k;
                }
            return (bg, bx, by, hh, ww, k);
        }

        static void ModeLight(string path, string plotPath)
        {
            Img raw = LoadFits(path);
            Img img = Prepare(raw);
            int h = img.H, w = img.W;
            double cx = w / 2.0, cy = h / 2.0;
            double rCorner = Math.Sqrt(cx * cx + cy * cy);

            Console.WriteLine($"\n=== TEST END-TO-END: {path} ===");
            Console.WriteLine("    Astept un stack CALIBRAT cu flat, dar FARA background extraction.");

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
                coef = Solve(AtA, Atb, 5);
                if (coef == null) break;
                var res = new double[n];
                var kept = new List<double>();
                for (int i = 0; i < n; i++)
                {
                    res[i] = V[i] - (coef[0] + coef[1] * X[i] + coef[2] * Y[i] + coef[3] * R2[i] + coef[4] * R2[i] * R2[i]);
                    if (keep[i]) kept.Add(res[i]);
                }
                double medr = Percentile(kept, 50);
                var absdev = kept.Select(x => Math.Abs(x - medr)).ToList();
                double s = 1.4826 * Percentile(absdev, 50);
                double thr = 2.5 * Math.Max(s, 1e-9);
                for (int i = 0; i < n; i++) keep[i] = res[i] < thr;
            }

            double a0 = coef[0], b0 = coef[1], c0 = coef[2], d0 = coef[3], e0 = coef[4];
            double radAtCorner = d0 + e0;
            double radPct = -radAtCorner / a0 * 100;
            double planePct = Math.Sqrt(b0 * b0 + c0 * c0) / a0 * 100;
            int keptCount = keep.Count(x => x);

            Console.WriteLine($"\n    tile-uri folosite       : {keptCount} / {n} (restul = nebulozitate/stele, respinse)");
            Console.WriteLine($"    gradient de cer (plan)  : {planePct,5:F2} %  <- poluare luminoasa, IGNORA");
            Console.WriteLine($"    componenta radiala      : {radPct,6:+0.00;-0.00} %   [{Verdict(Math.Abs(radPct), 2.0, 5.0)}]  <- ASTA e flat-ul");

            Console.WriteLine("\n  INTERPRETARE");
            if (Math.Abs(radPct) < 2.0)
            {
                Console.WriteLine("    Fondul n-are componenta radiala. Flat-ul face treaba.");
                Console.WriteLine("    Asta e testul care conteaza - toate celelalte sunt proxy pentru el.");
            }
            else if (radPct > 0)
            {
                Console.WriteLine("    Fondul CADE spre margini => SUB-corectie. Flat-ul nu prinde toata");
                Console.WriteLine("    vignetarea: alta diafragma decat la lights, sau flat contaminat.");
            }
            else
            {
                Console.WriteLine("    Fondul CRESTE spre margini => SUPRA-corectie. Cel mai probabil");
                Console.WriteLine("    flat-darks lipsa/gresite: imparti la un flat care contine offset.");
            }

            // harta: componenta radiala extrasa (fara plan)
            var radMap = new Img(th, tw);
            for (int i = 0; i < n; i++) radMap.D[i] = 1.0 + (d0 * R2[i] + e0 * R2[i] * R2[i]) / a0;
            SaveResidualPng(plotPath, radMap);
        }

        static Img Divide(Img a, Img b)
        {
            var o = new Img(a.H, a.W);
            for (int i = 0; i < a.D.Length; i++) o.D[i] = a.D[i] / Math.Max(b.D[i], 1e-12);
            return o;
        }

        // ========================================================= expunere
        // Ruleaza pe un SINGUR flat BRUT NECALIBRAT (FITS convertit in Siril, nu master).
        // Stacking-ul normalizeaza si sterge nivelul, deci pe master n-ar avea sens.
        // C# nu citeste NEF - foloseste un flat convertit in Siril. Scala (white) e
        // auto-detectata; daca greseste, dai --sat (si --black).
        static void ModeExpo(string path, double? satOverride, double? blackOverride)
        {
            Img data = LoadFits(path); // NU Prepare - vrem CFA brut, la scala originala
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
            var meds = ch.Select(c => Percentile(c, 50)).ToArray();
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
            double cenMed = (Percentile(cenVals, 50) - black) / rng * 100;    // decizie
            double globPct = (mmax - black) / rng * 100;                       // informativ
            double cenClip = cenVals.Count(v => v >= 0.98 * white) / (double)cenVals.Count * 100;

            Console.WriteLine($"\n=== EXPUNERE: {path} ===");
            Console.WriteLine($"    saturatie (white) : {white:F0}   black : {black:F0}   sursa: {src}");

            if (isCfa)
            {
                Console.WriteLine("    nivel median GLOBAL per canal CFA (informativ; nu stiu care e R/G/B):");
                string[] names = { "CFA[0,0]", "CFA[0,1]", "CFA[1,0]", "CFA[1,1]" };
                for (int i = 0; i < 4; i++)
                {
                    string mk = i == bright ? "  <- cel mai luminos" : "";
                    Console.WriteLine($"        {names[i]}: {(meds[i] - black) / rng * 100,5:F1}%{mk}");
                }
            }
            else
            {
                Console.WriteLine("    imagine mono/procesata (fara CFA)");
                // pt mono, centrul se ia pe intreaga imagine, nu pe sub-mozaic
                cenVals.Clear();
                int my0 = (int)(data.H * 0.4), my1 = (int)(data.H * 0.6);
                int mx0 = (int)(data.W * 0.4), mx1 = (int)(data.W * 0.6);
                for (int y = my0; y < my1; y++) for (int x = mx0; x < mx1; x++) cenVals.Add(data.At(y, x));
                cenMed = (Percentile(cenVals, 50) - black) / rng * 100;
                globPct = (Percentile(data.D.ToList(), 50) - black) / rng * 100;
                cenClip = cenVals.Count(v => v >= 0.98 * white) / (double)cenVals.Count * 100;
            }

            Console.WriteLine($"    canal cel mai luminos, mediana GLOBALA : {globPct,5:F1}%  (trasa in jos de colturi)");
            Console.WriteLine($"    canal cel mai luminos, CENTRU 20%      : {cenMed,5:F1}%  <- ASTA decide expunerea");
            Console.WriteLine($"    clipping in centru (>=98% white)       : {cenClip:F3}%");
            Console.WriteLine();

            const double cenLo = 50.0, cenHi = 65.0;   // tinta pt CENTRU
            if (cenClip > Th.ExpoClip || cenMed > 80)
            {
                double stops = cenMed > 0 ? Math.Log2(57.0 / cenMed) : -1;
                Console.WriteLine("    VERDICT: PREA SUS" + (cenClip > Th.ExpoClip ? " (CLIPPING in centru!)" : ""));
                Console.WriteLine($"    Scade expunerea cu ~{-stops:F1} stopuri.");
                if (cenClip > Th.ExpoClip)
                    Console.WriteLine("    Centrul clipeaza -> pierzi info de vignetare exact unde semnalul e maxim.");
            }
            else if (cenMed < 30)
            {
                Console.WriteLine("    VERDICT: PREA JOS");
                Console.WriteLine($"    Creste expunerea cu ~{Math.Log2(57.0 / cenMed):F1} stopuri.");
                Console.WriteLine("    Sub ~30% in centru masterul iese zgomotos (zgomot fixed-pattern).");
            }
            else if (cenMed < cenLo)
            {
                Console.WriteLine("    VERDICT: ACCEPTABIL, dar cam jos");
                Console.WriteLine($"    Ai putea creste cu ~{Math.Log2(57.0 / cenMed):F1} stopuri.");
            }
            else if (cenMed <= cenHi)
            {
                Console.WriteLine($"    VERDICT: OK  (centrul in tinta {cenLo:F0}-{cenHi:F0}%)");
            }
            else
            {
                Console.WriteLine("    VERDICT: ACCEPTABIL, dar cam sus");
                Console.WriteLine($"    Ai putea scadea cu ~{-Math.Log2(57.0 / cenMed):F1} stopuri.");
            }
        }

        // ========================================================= PNG (heatmap reziduu)
        // RdBu-like: albastru (jos) -> alb (1.0) -> rosu (sus). Scalat pe percentile 1..99.
        static void SaveResidualPng(string path, Img resid)
        {
            var vals = resid.D.ToList();
            double lo = Percentile(vals, 1), hi = Percentile(vals, 99);
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
            Console.WriteLine($"\n    -> harta salvata: {path}");
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