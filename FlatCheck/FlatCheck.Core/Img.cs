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

// Img.cs - rasterul 2D si transformarile pure pe el.

using System;

namespace FlatCheck
{
    // ---------------- imagine 2D simpla ----------------
    sealed class Img
    {
        public int H, W;
        public double[] D; // row-major, lungime H*W
        public Img(int h, int w) { H = h; W = w; D = new double[h * w]; }
        public double At(int y, int x) => D[y * W + x];
        public void Set(int y, int x, double v) => D[y * W + x] = v;

        // ========================================================= preprocesare
        internal static Img BlockMean(Img a, int k)
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
        internal static Img Prepare(Img data, int targetLong = 1200)
        {
            Img img = BlockMean(data, 2);
            int k = Math.Max(1, (int)(Math.Max(img.H, img.W) / (double)targetLong));
            return BlockMean(img, k);
        }

        // Imparte doua rastere, cu prag pe numitor (ca np.maximum(b, 1e-12)).
        internal static Img Divide(Img a, Img b)
        {
            var o = new Img(a.H, a.W);
            for (int i = 0; i < a.D.Length; i++) o.D[i] = a.D[i] / Math.Max(b.D[i], 1e-12);
            return o;
        }

        /// <summary>
        /// Taie un dreptunghi centrat, aruncand egal de la ambele capete.
        ///
        /// Exista pentru modul de atribuire, singurul care roteste rasterul cu 180 de
        /// grade si compara rezultatul cu originalul. <see cref="BlockMean"/> arunca
        /// blocurile partiale doar de la CAPAT, deci grila lui e ancorata sus-stanga:
        /// rotita cu 180 nu mai cade peste ea insasi, ci decalata cu pana la o dala.
        /// Pe un gradient neted eroarea e mica, dar e sistematica - masurata, muta
        /// raportul par/impar cu ~20%. Taierea centrata inainte de dale o elimina.
        ///
        /// NU se foloseste in LargeScalePtp: acolo grila ancorata sus-stanga e ce face
        /// flatcheck.py, iar paritatea numerica e testata.
        /// </summary>
        internal static Img CropCentred(Img a, int h, int w)
        {
            if (h == a.H && w == a.W) return a;
            int oy = (a.H - h) / 2, ox = (a.W - w) / 2;
            var o = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    o.Set(y, x, a.At(oy + y, ox + x));
            return o;
        }

        /// <summary>Rasterul rotit cu 180 de grade in jurul centrului lui.</summary>
        internal static Img Rotate180(Img a)
        {
            var o = new Img(a.H, a.W);
            for (int y = 0; y < a.H; y++)
                for (int x = 0; x < a.W; x++)
                    o.Set(y, x, a.At(a.H - 1 - y, a.W - 1 - x));
            return o;
        }

        /// <summary>
        /// Media dintre raster si rotitul lui cu 180 de grade.
        ///
        /// Anuleaza EXACT partea impara - deci orice gradient liniar - si lasa neatinsa
        /// partea para. Un profil radial nu poate fi impar, deci modelul radial nu pierde
        /// nimic din ce are voie sa contina, si se scoate exact ce il facea sa sara.
        ///
        /// DE CE E NEVOIE, fiindca nu e evident: dincolo de cercul inscris, un inel de
        /// raza constanta nu mai e un inel intreg, sunt arce parciale. Pe un cadru cu
        /// gradient, distributia pixelilor din arcele alea e BIMODALA - un capat intunecat,
        /// celalalt luminos - iar mediana cade in golul rarefiat dintre cele doua moduri,
        /// deci sare de la un inel la urmatorul cu o amplitudine data de gradient. Modelul
        /// interpolat mostenea salturile, si img/model le arata ca inele concentrice plus
        /// o margine dura pe cercul inscris. Reprodus pe un raster sintetic care e doar
        /// vignetare neteda ori un gradient de 11%: inelele apar fara sa existe nicio
        /// margine in date.
        ///
        /// Cifrele de verdict nu depindeau de asta - LargeScalePtp e restrans la cercul
        /// inscris, unde mediana era deja exacta (masurat: reziduul se muta cu sub 0.05
        /// puncte) - dar HARTA mintea, si harta e singurul lucru din care se citeste forma
        /// unei probleme.
        ///
        /// Alternativa era media pe inel in loc de mediana, care vindeca bimodalitatea la
        /// fel de bine (masurat, aceleasi cifre), dar renunta la robustetea medianei fata
        /// de stele, pixeli fierbinti si praf - iar analizorul primeste si cadre unice.
        /// </summary>
        internal static Img Symmetrise(Img a)
        {
            var o = new Img(a.H, a.W);
            for (int y = 0; y < a.H; y++)
                for (int x = 0; x < a.W; x++)
                    o.Set(y, x, (a.At(y, x) + a.At(a.H - 1 - y, a.W - 1 - x)) / 2.0);
            return o;
        }
    }
}
