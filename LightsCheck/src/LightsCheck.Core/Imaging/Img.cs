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

#nullable disable // copied from FlatCheck 2.3.0 unchanged, which compiles without nullable annotations (ADR 0003)

// Img.cs - rasterul 2D si transformarile pure pe el.

namespace LightsCheck.Core
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

        /// <summary>
        /// Grila de mediane pe dale de <paramref name="k"/> px, CU dalele partiale de la
        /// capat, si centrul real al fiecarei dale in pixeli.
        ///
        /// Deliberat NU grila lui <see cref="BlockMean"/>, care arunca blocurile partiale:
        /// netezirea trebuie sa ajunga pana la ultimul pixel, iar daca ultimele 58 de
        /// randuri ale unui senzor de 4016 n-au dala, valoarea lor se extrapoleaza. Si
        /// centrul dalei partiale se intoarce ca atare, nu ca "inca un pas": o dala de 8
        /// px tratata ca una intreaga (fitata la o dala distanta de vecina ei, cand e la
        /// jumatate) a pus o banda de eroare de 0.8 % de-a lungul marginii drepte pe
        /// sinteticul cu adevar cunoscut. Cu centrele reale banda a disparut (0.19 % pe
        /// tot cadrul).
        ///
        /// Mediana, nu media: o dala cu un pixel fierbinte sau cu o umbra de praf mica
        /// isi tine valoarea tipica.
        /// </summary>
        internal static (Img Tiles, double[] CentreY, double[] CentreX) TileMedians(Img a, int k)
        {
            int th = (a.H + k - 1) / k, tw = (a.W + k - 1) / k;
            var t = new Img(th, tw);
            var cy = new double[th];
            var cx = new double[tw];
            var buf = new double[k * k];
            for (int i = 0; i < tw; i++)
                cx[i] = (i * k + Math.Min(a.W, (i + 1) * k)) / 2.0;
            for (int j = 0; j < th; j++)
            {
                int y0 = j * k, y1 = Math.Min(a.H, (j + 1) * k);
                cy[j] = (y0 + y1) / 2.0;
                for (int i = 0; i < tw; i++)
                {
                    int x0 = i * k, x1 = Math.Min(a.W, (i + 1) * k);
                    int n = 0;
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                            buf[n++] = a.At(y, x);
                    var vals = new double[n];
                    Array.Copy(buf, vals, n);
                    Array.Sort(vals);
                    t.Set(j, i, Stats.OfSorted(vals, 50));
                }
            }
            return (t, cy, cx);
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
        /// Cifrele de verdict nu depindeau de asta la vremea aceea - LargeScalePtp era
        /// restrans la cercul inscris, unde mediana era deja exacta (masurat: reziduul se
        /// muta cu sub 0.05 puncte) - dar HARTA mintea, si harta e singurul lucru din care
        /// se citeste forma unei probleme. Odata modelul valid pana in colt, restrictia a
        /// fost scoasa; vezi Metrics.LargeScalePtp.
        ///
        /// Alternativa era media pe inel in loc de mediana, care vindeca bimodalitatea la
        /// fel de bine (masurat, aceleasi cifre), dar renunta la robustetea medianei fata
        /// de stele, pixeli fierbinti si praf - iar analizorul primeste si cadre unice.
        ///
        /// Simetrizarea scoate DOAR partea impara. Partea para neradiala - o sa, o cadere mai
        /// abrupta pe o axa - trece prin ea neatinsa si are aceeasi soarta pe arcele partiale;
        /// pe aceea o trateaza Metrics.RadialModel, pe inel, cu o corectie 2t. Vezi acolo.
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
