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

using System;

namespace FlatCheck
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
        /// Numele pozitiei <paramref name="i"/>: culoarea cand fisierul a spus ordinea,
        /// pozitia cand nu. Nu se ghiceste - ordinea difera de la aparat la aparat.
        /// </summary>
        internal static string Label(CfaPattern cfa, int i) => cfa?.Name(i) ?? $"CFA[{i / 2},{i % 2}]";

        /// <summary>'R', 'G' sau 'B' pentru pozitia <paramref name="i"/>; '\0' cand nu se stie.</summary>
        internal static char ColourOf(CfaPattern cfa, int i) => cfa?.Colours[i] ?? '\0';
    }
}
