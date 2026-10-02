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
// medii LINIARE - ce integreaza ochiul pe o pata de 4 mm de hartie. Dalele sunt in doua
// feluri (ADR 0025):
//   - cu WCS si catalog: o dala e a unui nor cand centrul ei cade in discul de aceeasi arie
//     al norului; praful e percentila DustCloudFraction a dalelor tuturor norilor din cadru
//     la un loc, cerul mediana dalelor din afara oricarui nor, si fiecare nor cu destule
//     dale primeste un rand al lui;
//   - fara WCS, sau fara nor catalogat in cadru: praful e percentila DustAreaFraction a
//     tuturor dalelor, cerul mediana tuturor; raportul spune ca asa s-a masurat.
// Geometria (DustMap: ce dala e a cui) se face o data pe imagine; L*-ul dalelor se refac
// la fiecare ridicare a simularii, pe aceeasi harta.
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
        /// <summary>WCS si cel putin un nor catalogat in cadru.</summary>
        Catalogue,

        /// <summary>Fisierul n-are WCS: regula pe tot cadrul.</summary>
        NoWcs,

        /// <summary>WCS, dar niciun nor catalogat nu are o dala in cadru: regula pe tot cadrul.</summary>
        NoClouds,
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

    /// <summary>Clasa unei dale pe harta ferestrei: in ce statistica intra.</summary>
    internal enum PatchClass : byte
    {
        None = 0,
        Dust = 1,
        Sky = 2,
    }

    /// <summary>
    /// Geometria masuratorii: grila de dale si apartenenta fiecareia la nori. Nu stie
    /// nimic despre valorile pixelilor - se construieste o data si se refoloseste la
    /// fiecare ridicare.
    /// </summary>
    internal sealed class DustMap
    {
        internal int Cols, Rows;

        /// <summary>Latura dalei in esantioane ale subesantionului, si pasul subesantionului.</summary>
        internal int Side, Stride;
        internal DustMethod Method;

        /// <summary>Pe dala: e in discul cel putin unui nor catalogat?</summary>
        internal bool[] InCloud;

        /// <summary>Norii cu cel putin o dala in cadru, in ordinea catalogului.</summary>
        internal IReadOnlyList<DustCloud> Clouds;

        internal int PatchCount => Cols * Rows;
        internal int CloudPatchCount;
        internal bool UsesCatalogue => Method == DustMethod.Catalogue;

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

        /// <summary>Cate dale au intrat in statistica prafului (ale norilor, sau toate).</summary>
        internal int DustPatchCount { get; init; }
    }

    internal static class DustAnalyzer
    {
        /// <summary>Latura dalei in esantioane ale subesantionului tonal.</summary>
        internal const int PatchSamples = Thresholds.DustPatchPx / Thresholds.TonalStride;

        /// <summary>
        /// Harta dalelor unei imagini de <paramref name="w"/> x <paramref name="h"/> pixeli,
        /// subesantionata cu <paramref name="stride"/>: cu WCS, fiecare dala isi afla (RA, Dec)
        /// si norii catalogati o revendica dupa distanta pana la centrul lor; fara WCS,
        /// o grila goala cu metoda NoWcs.
        /// </summary>
        internal static DustMap Build(int w, int h, Wcs wcs, IReadOnlyList<DarkNebula> catalogue = null, int stride = Thresholds.TonalStride)
        {
            int subW = (w + stride - 1) / stride, subH = (h + stride - 1) / stride;
            int side = Math.Max(1, Math.Min(PatchSamples, Math.Min(subW, subH)));
            int cols = subW / side, rows = subH / side;
            var map = new DustMap
            {
                Cols = cols,
                Rows = rows,
                Side = side,
                Stride = stride,
                InCloud = new bool[cols * rows],
                Clouds = Array.Empty<DustCloud>(),
                Method = DustMethod.NoWcs,
            };
            if (wcs == null) return map;

            catalogue ??= DarkNebulaCatalogue.All;

            // Vectorul unitate al centrului fiecarei dale, o singura data.
            var vx = new double[cols * rows]; var vy = new double[cols * rows]; var vz = new double[cols * rows];
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < cols; bx++)
                {
                    var (cx, cy) = map.PatchCentre(bx, by);
                    var (ra, dec) = wcs.PixelToSky(cx, cy);
                    var v = Astrometry.UnitVector(ra, dec);
                    int i = by * cols + bx;
                    vx[i] = v.X; vy[i] = v.Y; vz[i] = v.Z;
                }

            double patchPx = side * stride;
            var clouds = new List<DustCloud>();
            foreach (DarkNebula n in catalogue)
            {
                var (px, py) = wcs.SkyToPixel(n.Ra, n.Dec);
                if (double.IsNaN(px)) continue;
                double rpx = n.RadiusDeg / wcs.ScaleDegPerPx;
                // Discul nu atinge cadrul: afara. Altfel se testeaza doar dalele din dreptunghiul
                // lui, pe distanta unghiulara adevarata.
                if (px + rpx < 0 || py + rpx < 0 || px - rpx > w || py - rpx > h) continue;
                int bx0 = Math.Max(0, (int)Math.Floor((px - rpx) / patchPx)), bx1 = Math.Min(cols - 1, (int)Math.Ceiling((px + rpx) / patchPx));
                int by0 = Math.Max(0, (int)Math.Floor((py - rpx) / patchPx)), by1 = Math.Min(rows - 1, (int)Math.Ceiling((py + rpx) / patchPx));
                if (bx0 > bx1 || by0 > by1) continue;
                var c = Astrometry.UnitVector(n.Ra, n.Dec);
                double cosR = Math.Cos(n.RadiusDeg * Astrometry.Deg);
                var patches = new List<int>();
                for (int by = by0; by <= by1; by++)
                    for (int bx = bx0; bx <= bx1; bx++)
                    {
                        int i = by * cols + bx;
                        if (vx[i] * c.X + vy[i] * c.Y + vz[i] * c.Z >= cosR) patches.Add(i);
                    }
                if (patches.Count == 0) continue;
                foreach (int i in patches) map.InCloud[i] = true;
                clouds.Add(new DustCloud { Nebula = n, Patches = patches.ToArray(), CentreX = px, CentreY = py, RadiusPx = rpx });
            }
            map.Clouds = clouds;
            map.CloudPatchCount = map.InCloud.Count(x => x);
            map.Method = clouds.Count == 0 ? DustMethod.NoClouds : DustMethod.Catalogue;
            return map;
        }

        /// <summary>
        /// L*-ul luminantei medii liniare al fiecarei dale intregi, in ordinea randurilor
        /// hartii, pe un raster DEJA subesantionat la pasul hartii (si, in simulare, deja
        /// ridicat).
        /// </summary>
        internal static float[] PatchLstars(Raster sub, DustMap map)
        {
            int side = map.Side, cols = map.Cols, rows = map.Rows;
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
            var cloudLstars = new double[map.Clouds.Count];
            if (map.UsesCatalogue)
            {
                var inside = new List<float>(map.CloudPatchCount);
                var outside = new List<float>(n - map.CloudPatchCount);
                for (int i = 0; i < n; i++) (map.InCloud[i] ? inside : outside).Add(patchLstars[i]);
                dust = Stats.PercentileSorted(Stats.Sorted(inside.ToArray()), Thresholds.DustCloudFraction * 100);
                // Un cadru acoperit in intregime de nori n-are dale de cer: mediana tuturor.
                sky = outside.Count > 0 ? Stats.Median(outside.ToArray()) : Stats.Median(patchLstars);
                dustCount = inside.Count;
                for (int c = 0; c < map.Clouds.Count; c++)
                {
                    int[] idx = map.Clouds[c].Patches;
                    var v = new float[idx.Length];
                    for (int k = 0; k < idx.Length; k++) v[k] = patchLstars[idx[k]];
                    cloudLstars[c] = Stats.PercentileSorted(Stats.Sorted(v), Thresholds.DustCloudFraction * 100);
                }
            }
            else
            {
                float[] sorted = Stats.Sorted(patchLstars);
                dust = Stats.PercentileSorted(sorted, Thresholds.DustAreaFraction * 100);
                sky = Stats.PercentileSorted(sorted, 50);
                dustCount = n;
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
        /// Clasa fiecarei dale pentru harta ferestrei: dalele care DAU cifra, nu toate cele
        /// care intra in statistica. Praful e dalele din care se ia L*-ul prafului (ale
        /// norilor cu catalog, toate fara) aflate la sau sub L*-ul prafului - nucleele, nu
        /// discul intreg; cerul e dalele din care se ia L*-ul cerului (din afara norilor cu
        /// catalog, toate fara) aflate la sau sub mediana lor - cerul curat, nu banda Caii
        /// Lactee, care e peste mediana. Restul: None. (Prima forma, discul intreg ca praf si
        /// tot restul ca cer, arata norii ca praf si banda ca cer; proprietarul a respins-o
        /// pe 2026-10-02.)
        /// </summary>
        internal static PatchClass[] Classes(DustMap map, DustStats asIs)
        {
            var o = new PatchClass[map.PatchCount];
            for (int i = 0; i < o.Length; i++)
            {
                float l = asIs.PatchLstars[i];
                bool dustSide = !map.UsesCatalogue || map.InCloud[i];
                bool skySide = !map.UsesCatalogue || !map.InCloud[i];
                o[i] = dustSide && l <= asIs.LstarDust ? PatchClass.Dust
                    : skySide && l <= asIs.LstarSky ? PatchClass.Sky
                    : PatchClass.None;
            }
            return o;
        }
    }
}
