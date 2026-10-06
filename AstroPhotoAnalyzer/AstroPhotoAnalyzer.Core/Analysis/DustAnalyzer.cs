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

// DustAnalyzer.cs - masuratoarea B5: unde sta praful, partea cea mai intunecata INTINSA a
// imaginii, si unde sta cerul, in L*.
//
// Nu pe pixeli: cei mai intunecati pixeli sunt zgomot si stropi izolati. Pe DALE de
// Thresholds.DustPatchPx x DustPatchPx pixeli ai imaginii, fiecare cu L*-ul luminantei ei
// medii LINIARE - ce integreaza ochiul pe o pata de 4 mm de hartie. Dalele se impart in
// CLASE, relativ, nu dupa un prag absolut, pe una din doua cai:
//   - cu WCS si cu banda Caii Lactee in cadru (ADR 0026): banda e geometrie, din latitudinea
//     galactica; cerul e un model neted din dalele din AFARA benzii; nivelul local al benzii
//     e o mediana pe fereastra; praful e dala cu DustDepthLstar sub nivelul local (sau sub
//     cerul modelat), norii luminosi cu BrightCloudLstar peste nivelul local, varfurile cele
//     mai luminoase PeakFraction dale, cerul restul din afara benzii;
//   - altfel (fara WCS, sau cadru fara banda ori in intregime in ea; ADR 0033): cerul e un
//     model neted din TOATE dalele, cu percentila SkyModelLowPercentile pe celula; pragurile
//     vin din imagine: nebuloasa e dala cu mai mult de SkyNoiseK sigma ale cerului peste
//     model, praful cu la fel de mult sub el, nebuloasa stralucitoare peste pragul Otsu al
//     cozii de nebuloasa (fara varfuri), varfurile ca mai sus, cerul restul.
// Pe orice cale, verdictul masoara la fel (Measure): cerul e mediana clasei cer; praful e zona
// intinsa cea mai intunecata, decisa O DATA pe imaginea asa cum e (DecideDustSource): percentila
// DustClassFraction a clasei praf cand clasa are DustClassMinFraction din dale (sub atat sunt
// nuclee sau artefacte) si e mai intunecata decat percentila SkyDarkestFraction a clasei cer,
// altfel aceasta din urma; la fiecare ridicare se remasoara aceeasi statistica, ca randurile
// scarii sa spuna acelasi praf ca definitia. Catalogul Lynds (DarkNebulaCatalogue) da NUMELE
// norilor si randul fiecaruia. Geometria (DustMap) se face o data; L*-urile se refac la fiecare
// ridicare.
//
// Lucreaza pe subesantionul tonal (pasul 4): o dala e 8 x 8 esantioane. Dalele partiale de
// la marginea din dreapta si de jos se lasa afara.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>Pe ce cale s-au facut clasele.</summary>
    internal enum DustMethod
    {
        /// <summary>WCS, banda si cer in afara ei: clasele benzii (ADR 0026).</summary>
        Band,

        /// <summary>Clasele relative la cerul modelat (ADR 0033); DustMap.SkyReason spune de ce nu banda.</summary>
        Sky,
    }

    /// <summary>De ce nu s-au facut clasele benzii.</summary>
    internal enum SkyReason
    {
        /// <summary>Clasele sunt ale benzii.</summary>
        None,

        /// <summary>Fisierul n-are WCS.</summary>
        NoWcs,

        /// <summary>WCS, dar banda galactica e sub BandMinFraction din cadru (deep-sky departe de plan).</summary>
        NoBand,

        /// <summary>WCS, dar cerul din afara benzii e sub BandMinFraction din cadru (deep-sky in plan).</summary>
        AllBand,
    }

    /// <summary>Un nor catalogat cu cel putin o dala in cadru: care dale sunt ale lui, si unde e pe imagine.</summary>
    internal sealed class DustCloud
    {
        internal DarkNebula Nebula;

        /// <summary>Indicii dalelor (in ordinea randurilor hartii) cu centrul in disc.</summary>
        internal int[] Patches;

        /// <summary>Centrul discului, in pixeli ai imaginii (randul 0 sus), si raza lui in pixeli.</summary>
        internal double CentreX, CentreY, RadiusPx;

        internal string Name => Nebula.Name;
    }

    /// <summary>
    /// Clasa unei dale. Band si BrightCloud sunt "banda" si "nori luminosi" pe calea benzii,
    /// "nebuloasa" si "nebuloasa stralucitoare" pe calea cerului (DustAnalyzer.ClassNames);
    /// None nu se da niciodata de Build.
    /// </summary>
    internal enum PatchClass : byte
    {
        None = 0,
        Dust = 1,
        Sky = 2,
        Band = 3,
        BrightCloud = 4,
        Peak = 5,
    }

    /// <summary>
    /// Geometria masuratorii: grila de dale, clasa fiecareia si norii catalogati. Se
    /// construieste o data pe imagine, pe L*-urile ei asa cum e, si se refoloseste la fiecare
    /// ridicare: clasele nu se muta cu ridicarea, doar valorile.
    /// </summary>
    internal sealed class DustMap
    {
        internal int Cols, Rows;

        /// <summary>Latura dalei in esantioane ale subesantionului, si pasul subesantionului.</summary>
        internal int Side, Stride;
        internal DustMethod Method;
        internal SkyReason SkyReason;

        /// <summary>Clasa fiecarei dale.</summary>
        internal PatchClass[] Classes;

        /// <summary>Latitudinea galactica a centrului fiecarei dale; null fara WCS.</summary>
        internal float[] GalacticLatitude;

        /// <summary>Cerul modelat, L* pe dala, asa cum e.</summary>
        internal float[] SkyModel;

        /// <summary>Nivelul local al benzii, L* pe dala, asa cum e; null pe calea cerului.</summary>
        internal float[] BandLevel;

        /// <summary>
        /// Pe calea cerului: sigma zgomotului dalelor de cer; NoiseThreshold = SkyNoiseK x sigma,
        /// distanta in L* fata de cerul modelat de la care o dala e nebuloasa (in sus) sau praf
        /// (in jos); BrightThreshold, in L* peste cerul modelat, de la care e nebuloasa
        /// stralucitoare (infinit cand niciuna nu e). NaN pe calea benzii.
        /// </summary>
        internal double SkySigma = double.NaN, NoiseThreshold = double.NaN, BrightThreshold = double.NaN;

        /// <summary>Cine da L*-ul prafului, decis o data pe imaginea asa cum e (DustAnalyzer.DecideDustSource).</summary>
        internal DustSource DustSource;

        /// <summary>Norii cu cel putin o dala in cadru, in ordinea catalogului; gol fara WCS.</summary>
        internal IReadOnlyList<DustCloud> Clouds;

        internal int PatchCount => Cols * Rows;

        /// <summary>Fractia dalelor din fiecare clasa, indexata cu PatchClass.</summary>
        internal double FractionOf(PatchClass c) => Classes.Count(x => x == c) / (double)PatchCount;

        /// <summary>Nicio dala de cer (toate sunt praf, nebuloasa sau varf): cerul verdictului e mediana tuturor dalelor.</summary>
        internal bool SkyClassEmpty => Array.IndexOf(Classes, PatchClass.Sky) < 0;

        /// <summary>Numele celor cinci clase pe calea hartii, in ordinea praf, cer, banda / nebuloasa, nori luminosi / nebuloasa stralucitoare, varfuri.</summary>
        internal IReadOnlyList<string> ClassNames => DustAnalyzer.ClassNames(Method);

        /// <summary>Centrul dalei (bx, by), in pixeli ai imaginii intregi.</summary>
        internal (double X, double Y) PatchCentre(int bx, int by)
            => ((bx + 0.5) * Side * Stride - 0.5, (by + 0.5) * Side * Stride - 0.5);
    }

    /// <summary>Ce da L*-ul prafului: clasa praf, sau cerul cel mai intunecat.</summary>
    internal enum DustSource
    {
        /// <summary>Percentila DustClassFraction a clasei praf, care are cel putin DustClassMinFraction din dale si e, asa cum e, mai intunecata decat cerul cel mai intunecat.</summary>
        DustClass,

        /// <summary>Percentila SkyDarkestFraction a clasei cer (a tuturor dalelor cand clasa cer e goala): clasa praf e goala, prea mica, sau mai luminoasa.</summary>
        DarkestSky,
    }

    /// <summary>Praful si cerul pe o harta, la o ridicare data.</summary>
    internal sealed record DustStats
    {
        internal double LstarDust { get; init; }
        internal double LstarSky { get; init; }

        /// <summary>Procentul dalelor cadrului cu L* sub negrul hartiei - "negru pur".</summary>
        internal double PctPatchesBelowPaperBlack { get; init; }

        /// <summary>L*-ul fiecarei dale, in ordinea randurilor hartii.</summary>
        internal float[] PatchLstars { get; init; }

        /// <summary>Percentila DustCloudFraction a dalelor fiecarui nor din DustMap.Clouds, in aceeasi ordine.</summary>
        internal double[] CloudLstars { get; init; }

        /// <summary>Cate dale are clasa praf.</summary>
        internal int DustPatchCount { get; init; }
    }

    internal static class DustAnalyzer
    {
        /// <summary>Latura dalei in esantioane ale subesantionului tonal.</summary>
        internal const int PatchSamples = Thresholds.DustPatchPx / Thresholds.TonalStride;

        static readonly string[] BandNames = { "praf", "cer", "banda", "nori luminosi", "varfuri" };
        static readonly string[] SkyNames = { "praf", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri" };

        /// <summary>Numele celor cinci clase, in ordinea praf, cer, banda / nebuloasa, nori luminosi / nebuloasa stralucitoare, varfuri: ce arata harta, tabelul curbelor si raportul.</summary>
        internal static IReadOnlyList<string> ClassNames(DustMethod method) => method == DustMethod.Band ? BandNames : SkyNames;

        /// <summary>
        /// Harta dalelor unei imagini de <paramref name="w"/> x <paramref name="h"/> pixeli,
        /// subesantionata cu <paramref name="stride"/>, pe L*-urile ei asa cum e
        /// (<paramref name="asIsPatchLstars"/>, de la PatchLstars pe aceeasi grila): cu WCS,
        /// latitudinea galactica a fiecarei dale si norii catalogati care o revendica dupa
        /// distanta pana la centrul lor; apoi clasele, ale benzii cand se pot face, altfel
        /// relative la cerul modelat.
        /// </summary>
        internal static DustMap Build(int w, int h, Wcs wcs, float[] asIsPatchLstars, IReadOnlyList<DarkNebula> catalogue = null, int stride = Thresholds.TonalStride)
        {
            var (cols, rows, side) = Grid(w, h, stride);
            var map = new DustMap
            {
                Cols = cols,
                Rows = rows,
                Side = side,
                Stride = stride,
                Classes = new PatchClass[cols * rows],
                Clouds = Array.Empty<DustCloud>(),
            };
            if (asIsPatchLstars.Length != cols * rows) throw new ArgumentException("L*-urile dalelor nu sunt pe grila hartii");
            if (wcs == null)
            {
                ClassifyBySky(map, asIsPatchLstars, SkyReason.NoWcs);
                map.DustSource = DecideDustSource(asIsPatchLstars, map);
                return map;
            }

            catalogue ??= DarkNebulaCatalogue.All;

            // Directia cerului si latitudinea galactica ale centrului fiecarei dale, o singura data.
            int n = cols * rows;
            var vx = new double[n]; var vy = new double[n]; var vz = new double[n];
            map.GalacticLatitude = new float[n];
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < cols; bx++)
                {
                    var (cx, cy) = map.PatchCentre(bx, by);
                    var (ra, dec) = wcs.PixelToSky(cx, cy);
                    var v = Astrometry.UnitVector(ra, dec);
                    int i = by * cols + bx;
                    vx[i] = v.X; vy[i] = v.Y; vz[i] = v.Z;
                    map.GalacticLatitude[i] = (float)Astrometry.GalacticLatitude(ra, dec);
                }

            map.Clouds = FindClouds(map, wcs, catalogue, w, h, vx, vy, vz);
            Classify(map, asIsPatchLstars);
            map.DustSource = DecideDustSource(asIsPatchLstars, map);
            return map;
        }

        /// <summary>Grila dalelor pentru o imagine intreaga: subesantionul ei, apoi SubsampleGrid.</summary>
        static (int Cols, int Rows, int Side) Grid(int w, int h, int stride)
            => SubsampleGrid((w + stride - 1) / stride, (h + stride - 1) / stride);

        /// <summary>Grila dalelor pe un subesantion de subW x subH: dale intregi de PatchSamples, sau una singura pe un raster mai mic.</summary>
        static (int Cols, int Rows, int Side) SubsampleGrid(int subW, int subH)
        {
            int side = Math.Max(1, Math.Min(PatchSamples, Math.Min(subW, subH)));
            return (subW / side, subH / side, side);
        }

        static List<DustCloud> FindClouds(DustMap map, Wcs wcs, IReadOnlyList<DarkNebula> catalogue, int w, int h, double[] vx, double[] vy, double[] vz)
        {
            int cols = map.Cols, rows = map.Rows;
            double patchPx = map.Side * map.Stride;
            var clouds = new List<DustCloud>();
            foreach (DarkNebula nebula in catalogue)
            {
                var (px, py) = wcs.SkyToPixel(nebula.Ra, nebula.Dec);
                if (double.IsNaN(px)) continue;
                double rpx = nebula.RadiusDeg / wcs.ScaleDegPerPx;
                // Discul nu atinge cadrul: afara. Altfel se testeaza doar dalele din dreptunghiul
                // lui, pe distanta unghiulara adevarata.
                if (px + rpx < 0 || py + rpx < 0 || px - rpx > w || py - rpx > h) continue;
                int bx0 = Math.Max(0, (int)Math.Floor((px - rpx) / patchPx)), bx1 = Math.Min(cols - 1, (int)Math.Ceiling((px + rpx) / patchPx));
                int by0 = Math.Max(0, (int)Math.Floor((py - rpx) / patchPx)), by1 = Math.Min(rows - 1, (int)Math.Ceiling((py + rpx) / patchPx));
                if (bx0 > bx1 || by0 > by1) continue;
                var c = Astrometry.UnitVector(nebula.Ra, nebula.Dec);
                double cosR = Math.Cos(nebula.RadiusDeg * Astrometry.Deg);
                var patches = new List<int>();
                for (int by = by0; by <= by1; by++)
                    for (int bx = bx0; bx <= bx1; bx++)
                    {
                        int i = by * cols + bx;
                        if (vx[i] * c.X + vy[i] * c.Y + vz[i] * c.Z >= cosR) patches.Add(i);
                    }
                if (patches.Count == 0) continue;
                clouds.Add(new DustCloud { Nebula = nebula, Patches = patches.ToArray(), CentreX = px, CentreY = py, RadiusPx = rpx });
            }
            return clouds;
        }

        /// <summary>Clasele benzii (ADR 0026) pe L*-urile asa cum e; sau calea cerului, cu motivul, cand banda ori cerul din afara ei lipsesc.</summary>
        static void Classify(DustMap map, float[] L)
        {
            int cols = map.Cols, rows = map.Rows, n = cols * rows;
            var band = new bool[n];
            int inBand = 0;
            for (int i = 0; i < n; i++)
            {
                band[i] = Math.Abs(map.GalacticLatitude[i]) < Thresholds.BandGalacticLatitudeDeg;
                if (band[i]) inBand++;
            }
            if (inBand < Thresholds.BandMinFraction * n) { ClassifyBySky(map, L, SkyReason.NoBand); return; }
            if (n - inBand < Thresholds.BandMinFraction * n) { ClassifyBySky(map, L, SkyReason.AllBand); return; }

            float[] sky = SkyModel(L, band, cols, rows, 50);
            float[] level = Smooth(MedianFilter(L, cols, rows, Thresholds.BandLocalWindowPatches), cols, rows, Thresholds.BandLocalSmoothPatches);
            float peak = PeakLevel(L);

            double k = Thresholds.DustDepthLstar, kb = Thresholds.BrightCloudLstar;
            var cls = map.Classes;
            for (int i = 0; i < n; i++)
            {
                float l = L[i];
                PatchClass c = band[i] ? PatchClass.Band : PatchClass.Sky;
                if (!band[i] && l > sky[i] + kb) c = PatchClass.Band;
                if ((band[i] && l < level[i] - k) || l < sky[i] - k) c = PatchClass.Dust;
                if (band[i] && l > level[i] + kb) c = PatchClass.BrightCloud;
                if (l > peak) c = PatchClass.Peak;
                cls[i] = c;
            }
            map.SkyModel = sky;
            map.BandLevel = level;
            map.Method = DustMethod.Band;
            map.SkyReason = SkyReason.None;
        }

        /// <summary>
        /// Clasele relative la cerul modelat (ADR 0033): cerul din toate dalele, cu percentila
        /// joasa pe celula; reziduul dala - cer, cu sigma robusta dupa o trecere de curatare la
        /// SkyNoiseClipK sigma in jurul medianei; praf sub cerul modelat cu SkyNoiseK sigma,
        /// nebuloasa peste el cu la fel de mult, nebuloasa stralucitoare peste pragul Otsu al
        /// cozii de nebuloasa luate FARA varfuri si cu cel putin BrightClassMinTailFraction din
        /// coada deasupra pragului (altfel doua stele ar fi "clasa" de sus si nebuloasa
        /// stralucitoare ar ramane goala; niciuna cand coada are prea putine dale), varfurile
        /// cele mai luminoase PeakFraction dale, cerul restul. Pragurile raman pe harta, pentru
        /// legenda si raport.
        /// Cu sigma 0 (un cer posterizat, mai mult de jumatate din dale la o valoare) pragul e
        /// 0 si clasa e semnul reziduului.
        /// </summary>
        static void ClassifyBySky(DustMap map, float[] L, SkyReason reason)
        {
            int cols = map.Cols, rows = map.Rows, n = cols * rows;
            float[] sky = SkyModel(L, new bool[n], cols, rows, Thresholds.SkyModelLowPercentile);
            var resid = new float[n];
            for (int i = 0; i < n; i++) resid[i] = L[i] - sky[i];

            double sigma = Stats.RobustSigma(resid, out double med);
            float[] kept = resid.Where(v => Math.Abs(v - med) <= Thresholds.SkyNoiseClipK * sigma).ToArray();
            if (kept.Length > 0) sigma = Stats.RobustSigma(kept, out _);

            double noise = Thresholds.SkyNoiseK * sigma;
            float peak = PeakLevel(L);
            var tail = new List<float>();
            for (int i = 0; i < n; i++) if (resid[i] > noise && L[i] <= peak) tail.Add(resid[i]);
            int minAbove = (int)Math.Ceiling(Thresholds.BrightClassMinTailFraction * tail.Count);
            double bright = Stats.OtsuThreshold(Stats.Sorted(tail.ToArray()), minAbove);
            if (double.IsNaN(bright)) bright = double.PositiveInfinity;

            var cls = map.Classes;
            for (int i = 0; i < n; i++)
            {
                double v = resid[i];
                PatchClass c = v < -noise ? PatchClass.Dust : v > bright ? PatchClass.BrightCloud : v > noise ? PatchClass.Band : PatchClass.Sky;
                if (L[i] > peak) c = PatchClass.Peak;
                cls[i] = c;
            }
            map.SkyModel = sky;
            map.BandLevel = null;
            map.Method = DustMethod.Sky;
            map.SkyReason = reason;
            map.SkySigma = sigma;
            map.NoiseThreshold = noise;
            map.BrightThreshold = bright;
        }

        /// <summary>L*-ul peste care o dala e varf: percentila (1 - PeakFraction) a dalelor.</summary>
        static float PeakLevel(float[] L) => (float)Stats.PercentileSorted(Stats.Sorted(L), 100 * (1 - Thresholds.PeakFraction));

        /// <summary>
        /// Cerul modelat: percentila <paramref name="percentile"/> a dalelor din afara benzii pe
        /// celule de SkyModelCellPatches, celulele fara destule dale de cer luate de la cea mai
        /// apropiata, interpolat liniar la rezolutia dalelor si netezit. Cu <paramref name="band"/>
        /// tot fals, fiecare celula are toate dalele ei.
        /// </summary>
        internal static float[] SkyModel(float[] L, bool[] band, int cols, int rows, double percentile)
        {
            int cell = Thresholds.SkyModelCellPatches;
            int gw = (cols + cell - 1) / cell, gh = (rows + cell - 1) / cell;
            var grid = new float[gw * gh];
            var known = new bool[gw * gh];
            for (int gy = 0; gy < gh; gy++)
                for (int gx = 0; gx < gw; gx++)
                {
                    var v = new List<float>();
                    for (int by = gy * cell; by < Math.Min(rows, (gy + 1) * cell); by++)
                        for (int bx = gx * cell; bx < Math.Min(cols, (gx + 1) * cell); bx++)
                            if (!band[by * cols + bx]) v.Add(L[by * cols + bx]);
                    if (v.Count >= Thresholds.SkyModelCellMinPatches) { grid[gy * gw + gx] = (float)Stats.PercentileSorted(Stats.Sorted(v.ToArray()), percentile); known[gy * gw + gx] = true; }
                }
            // Nicio celula cu destule dale (un raster mic): toate dalele de cer impreuna.
            if (!known.Any(k => k))
            {
                var all = new List<float>();
                for (int i = 0; i < L.Length; i++) if (!band[i]) all.Add(L[i]);
                float v = all.Count > 0 ? (float)Stats.PercentileSorted(Stats.Sorted(all.ToArray()), percentile) : 0;
                for (int g = 0; g < grid.Length; g++) { grid[g] = v; known[g] = true; }
            }
            // Celulele necunoscute iau valoarea celei mai apropiate cunoscute (distanta pe grila).
            for (int g = 0; g < grid.Length; g++)
            {
                if (known[g]) continue;
                int gx = g % gw, gy = g / gw; double best = double.MaxValue; float val = 0;
                for (int k2 = 0; k2 < grid.Length; k2++)
                {
                    if (!known[k2]) continue;
                    int kx = k2 % gw, ky = k2 / gw;
                    double d = (kx - gx) * (kx - gx) + (ky - gy) * (ky - gy);
                    if (d < best) { best = d; val = grid[k2]; }
                }
                grid[g] = val;
            }
            // Interpolare biliniara intre centrele celulelor, apoi netezire.
            var o = new float[cols * rows];
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < cols; bx++)
                {
                    double fx = (bx + 0.5) / cell - 0.5, fy = (by + 0.5) / cell - 0.5;
                    int x0 = Math.Clamp((int)Math.Floor(fx), 0, gw - 1), y0 = Math.Clamp((int)Math.Floor(fy), 0, gh - 1);
                    int x1 = Math.Min(x0 + 1, gw - 1), y1 = Math.Min(y0 + 1, gh - 1);
                    double tx = Math.Clamp(fx - x0, 0, 1), ty = Math.Clamp(fy - y0, 0, 1);
                    double top = grid[y0 * gw + x0] * (1 - tx) + grid[y0 * gw + x1] * tx;
                    double bottom = grid[y1 * gw + x0] * (1 - tx) + grid[y1 * gw + x1] * tx;
                    o[by * cols + bx] = (float)(top * (1 - ty) + bottom * ty);
                }
            return Smooth(o, cols, rows, Thresholds.SkyModelSmoothPatches);
        }

        /// <summary>Mediana pe o fereastra patrata de <paramref name="window"/> dale, taiata la margini.</summary>
        internal static float[] MedianFilter(float[] L, int cols, int rows, int window)
        {
            int r = window / 2;
            var o = new float[L.Length];
            var buf = new List<float>(window * window);
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < cols; bx++)
                {
                    buf.Clear();
                    for (int y = Math.Max(0, by - r); y <= Math.Min(rows - 1, by + r); y++)
                        for (int x = Math.Max(0, bx - r); x <= Math.Min(cols - 1, bx + r); x++)
                            buf.Add(L[y * cols + x]);
                    o[by * cols + bx] = (float)Stats.Median(buf.ToArray());
                }
            return o;
        }

        /// <summary>Media pe o fereastra de raza <paramref name="radius"/> dale, taiata la margini: netezirea.</summary>
        internal static float[] Smooth(float[] L, int cols, int rows, int radius)
        {
            var o = new float[L.Length];
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < cols; bx++)
                {
                    double sum = 0; int count = 0;
                    for (int y = Math.Max(0, by - radius); y <= Math.Min(rows - 1, by + radius); y++)
                        for (int x = Math.Max(0, bx - radius); x <= Math.Min(cols - 1, bx + radius); x++) { sum += L[y * cols + x]; count++; }
                    o[by * cols + bx] = (float)(sum / count);
                }
            return o;
        }

        /// <summary>
        /// L*-ul luminantei medii liniare al fiecarei dale intregi, in ordinea randurilor
        /// hartii, pe un raster DEJA subesantionat la pasul hartii (si, in simulare, deja
        /// ridicat). Grila e cea pe care o construieste Build pentru aceeasi imagine.
        /// </summary>
        internal static float[] PatchLstars(Raster sub)
        {
            var (cols, rows, side) = SubsampleGrid(sub.W, sub.H);
            var o = new float[cols * rows];
            bool color = sub.IsColor;
            float[] R = sub.Planes[0], G = color ? sub.Planes[1] : null, B = color ? sub.Planes[2] : null;
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < cols; bx++)
                {
                    double sum = 0;
                    for (int y = by * side; y < (by + 1) * side; y++)
                    {
                        int row = y * sub.W;
                        for (int x = bx * side; x < (bx + 1) * side; x++)
                        {
                            int i = row + x;
                            sum += color
                                ? ColorMath.Luminance(ColorMath.SrgbToLinear(R[i]), ColorMath.SrgbToLinear(G[i]), ColorMath.SrgbToLinear(B[i]))
                                : ColorMath.SrgbToLinear(R[i]);
                        }
                    }
                    o[by * cols + bx] = (float)ColorMath.LstarOfY(sum / (side * side));
                }
            return o;
        }

        /// <summary>Dalele clasei praf si ale clasei cer; cerul e toate dalele cand clasa cer e goala (DustMap.SkyClassEmpty).</summary>
        static (float[] DustSorted, float[] SkySorted) ClassSets(float[] patchLstars, DustMap map)
        {
            var dustL = new List<float>(); var skyL = new List<float>();
            for (int i = 0; i < patchLstars.Length; i++)
            {
                if (map.Classes[i] == PatchClass.Dust) dustL.Add(patchLstars[i]);
                else if (map.Classes[i] == PatchClass.Sky) skyL.Add(patchLstars[i]);
            }
            return (Stats.Sorted(dustL.ToArray()), Stats.Sorted(skyL.Count > 0 ? skyL.ToArray() : patchLstars));
        }

        /// <summary>
        /// Cine da L*-ul prafului, pe L*-urile asa cum e (ADR 0033): clasa praf cand are cel
        /// putin DustClassMinFraction din dale si percentila ei DustClassFraction e la sau sub
        /// percentila SkyDarkestFraction a clasei cer; altfel cerul cel mai intunecat. Sub prag
        /// clasa praf e nuclee sau artefacte si nu decide. Decis o data, ca fiecare rand al
        /// scarii sa masoare acelasi praf ca definitia.
        /// </summary>
        internal static DustSource DecideDustSource(float[] asIsPatchLstars, DustMap map)
        {
            var (dust, sky) = ClassSets(asIsPatchLstars, map);
            if (dust.Length < Thresholds.DustClassMinFraction * asIsPatchLstars.Length) return DustSource.DarkestSky;
            double dustClass = Stats.PercentileSorted(dust, Thresholds.DustClassFraction * 100);
            double darkestSky = Stats.PercentileSorted(sky, Thresholds.SkyDarkestFraction * 100);
            return dustClass <= darkestSky ? DustSource.DustClass : DustSource.DarkestSky;
        }

        /// <summary>
        /// Praful, cerul si "negru pur" pe L*-urile dalelor, pe clasele hartii, pe orice cale:
        /// cerul e mediana clasei cer (a tuturor dalelor cand clasa e goala); praful e statistica
        /// pe care a ales-o DecideDustSource pe imaginea asa cum e: percentila DustClassFraction
        /// a clasei praf, sau percentila SkyDarkestFraction a clasei cer.
        /// </summary>
        internal static DustStats Measure(float[] patchLstars, DustMap map, double paperBlackLstar)
        {
            int n = patchLstars.Length;
            int below = 0;
            for (int i = 0; i < n; i++) if (patchLstars[i] < paperBlackLstar) below++;

            var (dustSorted, skySorted) = ClassSets(patchLstars, map);
            double sky = Stats.PercentileSorted(skySorted, 50);
            double dust = map.DustSource == DustSource.DustClass
                ? Stats.PercentileSorted(dustSorted, Thresholds.DustClassFraction * 100)
                : Stats.PercentileSorted(skySorted, Thresholds.SkyDarkestFraction * 100);

            var cloudLstars = new double[map.Clouds.Count];
            for (int c = 0; c < map.Clouds.Count; c++)
            {
                int[] idx = map.Clouds[c].Patches;
                var v = new float[idx.Length];
                for (int k = 0; k < idx.Length; k++) v[k] = patchLstars[idx[k]];
                cloudLstars[c] = Stats.PercentileSorted(Stats.Sorted(v), Thresholds.DustCloudFraction * 100);
            }

            return new DustStats
            {
                LstarDust = dust,
                LstarSky = sky,
                PctPatchesBelowPaperBlack = 100.0 * below / n,
                PatchLstars = patchLstars,
                CloudLstars = cloudLstars,
                DustPatchCount = dustSorted.Length,
            };
        }
    }
}
