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
// CLASE (ADR 0026), relativ, nu dupa un prag absolut:
//   - banda Caii Lactee e geometrie, din WCS: dalele cu |b| < BandGalacticLatitudeDeg;
//   - cerul e un model neted, din dalele din AFARA benzii (grila de celule, mediana pe
//     celula, interpolare, netezire), ca gradientul sa nu para praf;
//   - nivelul local al benzii e mediana pe o fereastra de BandLocalWindowPatches, netezita:
//     cat de luminoasa ar fi banda acolo;
//   - praful e dala cu DustDepthLstar sub nivelul local (sau sub cerul modelat, oriunde),
//     "mai intunecat decat ar fi banda acolo"; norii luminosi, cu BrightCloudLstar peste
//     nivelul local; varfurile, cele mai luminoase PeakFraction dale; cerul, restul din
//     afara benzii (o dala din afara benzii dar mult peste cerul modelat e banda).
// Praful verdictului e percentila DustClassFraction a dalelor din clasa praf; cerul,
// mediana dalelor din clasa cer. Fara WCS, fara banda sau fara cer in afara ei
// (BandMinFraction), raman regula pe tot cadrul (DustAreaFraction, mediana) si o nota.
// Catalogul Lynds (DarkNebulaCatalogue) nu mai da praful: da NUMELE norilor si randul
// fiecaruia. Geometria (DustMap) se face o data; L*-urile se refac la fiecare ridicare.
//
// Lucreaza pe subesantionul tonal (pasul 4): o dala e 8 x 8 esantioane. Dalele partiale de
// la marginea din dreapta si de jos se lasa afara.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>Cum s-au ales praful si cerul.</summary>
    internal enum DustMethod
    {
        /// <summary>WCS, banda si cer in afara ei: clasele.</summary>
        Classes,

        /// <summary>Fisierul n-are WCS: regula pe tot cadrul.</summary>
        NoWcs,

        /// <summary>WCS, dar banda galactica e sub BandMinFraction din cadru (deep-sky departe de plan): regula pe tot cadrul.</summary>
        NoBand,

        /// <summary>WCS, dar cerul din afara benzii e sub BandMinFraction din cadru (deep-sky in plan): regula pe tot cadrul.</summary>
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

    /// <summary>Clasa unei dale (ADR 0026); None doar fara clase, pentru dalele care nu intra in nicio statistica.</summary>
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

        /// <summary>Clasa fiecarei dale; toate None fara clase.</summary>
        internal PatchClass[] Classes;

        /// <summary>Latitudinea galactica a centrului fiecarei dale; null fara WCS.</summary>
        internal float[] GalacticLatitude;

        /// <summary>Cerul modelat si nivelul local al benzii, L* pe dala, asa cum e; null fara clase.</summary>
        internal float[] SkyModel, BandLevel;

        /// <summary>Norii cu cel putin o dala in cadru, in ordinea catalogului; gol fara WCS.</summary>
        internal IReadOnlyList<DustCloud> Clouds;

        internal int PatchCount => Cols * Rows;
        internal bool UsesClasses => Method == DustMethod.Classes;

        /// <summary>Fractia dalelor din fiecare clasa, indexata cu PatchClass; goala fara clase.</summary>
        internal double FractionOf(PatchClass c) => Classes.Count(x => x == c) / (double)PatchCount;

        /// <summary>Centrul dalei (bx, by), in pixeli ai imaginii intregi.</summary>
        internal (double X, double Y) PatchCentre(int bx, int by)
            => ((bx + 0.5) * Side * Stride - 0.5, (by + 0.5) * Side * Stride - 0.5);
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

        /// <summary>Cate dale au intrat in statistica prafului (clasa praf, sau toate).</summary>
        internal int DustPatchCount { get; init; }
    }

    internal static class DustAnalyzer
    {
        /// <summary>Latura dalei in esantioane ale subesantionului tonal.</summary>
        internal const int PatchSamples = Thresholds.DustPatchPx / Thresholds.TonalStride;

        /// <summary>
        /// Harta dalelor unei imagini de <paramref name="w"/> x <paramref name="h"/> pixeli,
        /// subesantionata cu <paramref name="stride"/>, pe L*-urile ei asa cum e
        /// (<paramref name="asIsPatchLstars"/>, de la PatchLstars pe aceeasi grila): cu WCS,
        /// latitudinea galactica a fiecarei dale, clasele, si norii catalogati care o
        /// revendica dupa distanta pana la centrul lor; fara WCS, o grila goala cu metoda NoWcs.
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
                Method = DustMethod.NoWcs,
            };
            if (wcs == null) return map;
            if (asIsPatchLstars.Length != cols * rows) throw new ArgumentException("L*-urile dalelor nu sunt pe grila hartii");

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
            return map;
        }

        static (int Cols, int Rows, int Side) Grid(int w, int h, int stride)
        {
            int subW = (w + stride - 1) / stride, subH = (h + stride - 1) / stride;
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

        /// <summary>Clasele (ADR 0026) pe L*-urile asa cum e; sau metoda NoBand / AllBand cand nu se pot face.</summary>
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
            if (inBand < Thresholds.BandMinFraction * n) { map.Method = DustMethod.NoBand; return; }
            if (n - inBand < Thresholds.BandMinFraction * n) { map.Method = DustMethod.AllBand; return; }

            float[] sky = SkyModel(L, band, cols, rows);
            float[] level = Smooth(MedianFilter(L, cols, rows, Thresholds.BandLocalWindowPatches), cols, rows, Thresholds.BandLocalSmoothPatches);
            float peak = (float)Stats.PercentileSorted(Stats.Sorted(L), 100 * (1 - Thresholds.PeakFraction));

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
            map.Method = DustMethod.Classes;
        }

        /// <summary>
        /// Cerul modelat: mediana dalelor din afara benzii pe celule de SkyModelCellPatches,
        /// celulele fara destule dale de cer luate de la cea mai apropiata, interpolat liniar
        /// la rezolutia dalelor si netezit.
        /// </summary>
        internal static float[] SkyModel(float[] L, bool[] band, int cols, int rows)
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
                    if (v.Count >= Thresholds.SkyModelCellMinPatches) { grid[gy * gw + gx] = (float)Stats.Median(v.ToArray()); known[gy * gw + gx] = true; }
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
        internal static float[] PatchLstars(Raster sub, int stride = Thresholds.TonalStride)
        {
            var (cols, rows, side) = Grid(sub.W * stride, sub.H * stride, stride);
            // Grid presupune latimea intreaga; pe un subesantion deja facut, dimensiunile lui sunt
            // chiar subW/subH, deci se recalculeaza direct.
            cols = sub.W / side; rows = sub.H / side;
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

        /// <summary>Praful, cerul si "negru pur" pe L*-urile dalelor, dupa metoda hartii.</summary>
        internal static DustStats Measure(float[] patchLstars, DustMap map, double paperBlackLstar)
        {
            int n = patchLstars.Length;
            int below = 0;
            for (int i = 0; i < n; i++) if (patchLstars[i] < paperBlackLstar) below++;

            double dust, sky;
            int dustCount;
            if (map.UsesClasses)
            {
                var dustL = new List<float>(); var skyL = new List<float>();
                for (int i = 0; i < n; i++)
                {
                    if (map.Classes[i] == PatchClass.Dust) dustL.Add(patchLstars[i]);
                    else if (map.Classes[i] == PatchClass.Sky) skyL.Add(patchLstars[i]);
                }
                // Clasele exista doar cu banda si cer; praful poate lipsi (o banda fara nori):
                // atunci praful e cea mai intunecata dala a benzii, nu o eroare.
                dust = dustL.Count > 0
                    ? Stats.PercentileSorted(Stats.Sorted(dustL.ToArray()), Thresholds.DustClassFraction * 100)
                    : Stats.Min(patchLstars);
                sky = skyL.Count > 0 ? Stats.Median(skyL.ToArray()) : Stats.Median(patchLstars);
                dustCount = dustL.Count;
            }
            else
            {
                float[] sorted = Stats.Sorted(patchLstars);
                dust = Stats.PercentileSorted(sorted, Thresholds.DustAreaFraction * 100);
                sky = Stats.PercentileSorted(sorted, 50);
                dustCount = n;
            }

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
                DustPatchCount = dustCount,
            };
        }

        /// <summary>
        /// Clasele pentru harta ferestrei: cele ale hartii cand exista; fara ele, praful e
        /// dalele la sau sub L*-ul prafului (cele mai intunecate 1 %) si cerul dalele la sau sub
        /// mediana, restul None.
        /// </summary>
        internal static PatchClass[] Classes(DustMap map, DustStats asIs)
        {
            if (map.UsesClasses) return (PatchClass[])map.Classes.Clone();
            var o = new PatchClass[map.PatchCount];
            for (int i = 0; i < o.Length; i++)
            {
                float l = asIs.PatchLstars[i];
                o[i] = l <= asIs.LstarDust ? PatchClass.Dust : l <= asIs.LstarSky ? PatchClass.Sky : PatchClass.None;
            }
            return o;
        }
    }
}
