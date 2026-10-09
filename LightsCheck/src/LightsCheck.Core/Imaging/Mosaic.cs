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

// Mosaic.cs - regula comuna a mozaicului CFA: care pixel e in care pozitie Bayer, cand
// un cadru "arata a CFA", si cum se numeste o pozitie.
//
// A stat inline in ExposureAnalyzer pana la modul de egalizare de histograma, care avea
// nevoie de exact aceleasi trei lucruri. Doua copii ale lui "(y & 1) * 2 + (x & 1)" si
// ale lui "1.15" ar fi divergat la prima ajustare, si divergenta s-ar fi vazut abia cand
// un canal se numea R intr-un tab si G in altul, pe acelasi fisier.
//
// ExposureAnalyzer NU foloseste Split: el umple histogramele in aceeasi trecere in care
// citeste pixelii, iar o a doua copie a datelor ar costa memorie degeaba. Foloseste
// PositionOf, LooksCfa si Label - regula, nu bufferele.

namespace LightsCheck.Core
{
    internal static class Mosaic
    {
        /// <summary>Pozitia Bayer a pixelului (y, x): 0..3, in ordinea (y&amp;1)*2 + (x&amp;1).</summary>
        internal static int PositionOf(int y, int x) => (y & 1) * 2 + (x & 1);

        /// <summary>
        /// Pragul de la care cele patru mediane spun "mozaic intact". Sub el, canalele
        /// sunt prea apropiate ca sa fie filtre de culoare diferite: fisierul e mono sau
        /// debayerizat. Empiric, ca toate pragurile; un raw sub lumina foarte neutra poate
        /// cadea sub el, si atunci se trateaza ca mono - ceea ce e o alegere sigura.
        /// </summary>
        internal const double CfaSpreadMin = 1.15;

        /// <summary><paramref name="spread"/> = max/min peste cele patru mediane de pozitie.</summary>
        internal static bool LooksCfa(double spread) => spread > CfaSpreadMin;

        /// <summary>
        /// Cele patru pozitii ca patru rastere de H/2 x W/2 (impartire intreaga: un rand sau
        /// o coloana in plus se pierde, ca la BlockMean). Indexul din vector e pozitia.
        /// </summary>
        internal static Img[] Split(Img a)
        {
            int h = a.H / 2, w = a.W / 2;
            var o = new Img[4];
            for (int i = 0; i < 4; i++) o[i] = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    o[0].Set(y, x, a.At(y * 2, x * 2));
                    o[1].Set(y, x, a.At(y * 2, x * 2 + 1));
                    o[2].Set(y, x, a.At(y * 2 + 1, x * 2));
                    o[3].Set(y, x, a.At(y * 2 + 1, x * 2 + 1));
                }
            return o;
        }

        /// <summary>
        /// O singura pozitie, ca raster de H/2 x W/2 - aceeasi decupare ca <see cref="Split"/>,
        /// dar pentru un singur <paramref name="p"/>. Exista pentru modul de netezire, care
        /// lucreaza pozitiile pe rand si n-are de ce sa tina toate patru copiile deodata:
        /// pe un cadru de 24 MP asta e 190 MB in plus, degeaba.
        /// </summary>
        internal static Img Extract(Img a, int p)
        {
            int h = a.H / 2, w = a.W / 2;
            int oy = p / 2, ox = p % 2;
            var o = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    o.Set(y, x, a.At(y * 2 + oy, x * 2 + ox));
            return o;
        }

        /// <summary>
        /// Inversul lui <see cref="Split"/>: cele patru pozitii, reimpletite intr-un raster
        /// de marimea ORIGINALA <paramref name="h"/> x <paramref name="w"/>.
        ///
        /// Split arunca un rand sau o coloana impara de la capat; aici linia lipsa se umple
        /// copiind cea mai apropiata linie para, ca iesirea sa aiba mereu forma intrarii si
        /// ultimul rand al unui cadru de 4017 sa nu fie zero - un flat cu o linie de zerouri
        /// ar imparti lights-urile la zero pe acea linie.
        /// </summary>
        internal static Img Merge(Img[] positions, int h, int w)
        {
            var o = new Img(h, w);
            int ph = h / 2, pw = w / 2;
            for (int y = 0; y < h; y++)
            {
                int sy = Math.Min(y / 2, ph - 1);
                for (int x = 0; x < w; x++)
                {
                    int sx = Math.Min(x / 2, pw - 1);
                    // pe linia impara de la capat, randul y (sau coloana x) cade in afara
                    // pozitiilor: se ia pozitia cu ACEEASI paritate - deci aceeasi culoare
                    // pe un CFA - de pe ultima linie para, prin sy/sx prinse mai sus
                    int p = PositionOf(y, x);
                    o.Set(y, x, positions[p].At(sy, sx));
                }
            }
            return o;
        }

        /// <summary>
        /// Numele pozitiei <paramref name="i"/>: culoarea cand fisierul a spus ordinea,
        /// pozitia cand nu. Nu se ghiceste - ordinea difera de la aparat la aparat.
        /// </summary>
        internal static string Label(CfaPattern cfa, int i) => cfa?.Name(i) ?? $"CFA[{i / 2},{i % 2}]";

        /// <summary>'R', 'G' sau 'B' pentru pozitia <paramref name="i"/>; '\0' cand nu se stie.</summary>
        internal static char ColourOf(CfaPattern cfa, int i) => cfa?.Colours[i] ?? '\0';
    }
}
