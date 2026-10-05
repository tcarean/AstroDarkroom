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

// ColorMath.cs - sRGB, luminanta si L*, functiile pure pe care sta tot ce tine de hartie.
//
// Imaginea e presupusa sRGB (asa o scrie Siril, si asa era si cea de acceptanta). Un
// profil diferit se raporteaza ca fapt (SourceInfo.IccDescription), dar nu se
// converteste: unealta masoara ce a scris procesarea, nu o interpretare a ei.
//
// Formulele sunt cele standard (IEC 61966-2-1 pentru sRGB, CIE 1976 pentru L*), scrise
// aici o singura data ca sa nu apara doua versiuni cu constante rotunjite diferit.

using System;

namespace AstroPhotoAnalyzer
{
    internal static class ColorMath
    {
        /// <summary>sRGB codat (0..1) -> luminanta liniara (0..1).</summary>
        internal static double SrgbToLinear(double c)
            => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        /// <summary>Luminanta liniara -> sRGB codat. Inversa exacta a lui SrgbToLinear.</summary>
        internal static double LinearToSrgb(double l)
            => l <= 0.0031308 ? 12.92 * l : 1.055 * Math.Pow(l, 1 / 2.4) - 0.055;

        /// <summary>Luminanta Y din componente LINIARE, cu ponderile Rec. 709 / sRGB.</summary>
        internal static double Luminance(double rLin, double gLin, double bLin)
            => 0.2126 * rLin + 0.7152 * gLin + 0.0722 * bLin;

        /// <summary>
        /// L* (CIE 1976) din luminanta relativa Y (0..1, alb = 1). Segmentul liniar de sub
        /// 0.008856 e cel din standard; fara el, L* de la Y foarte mic ar fi negativ.
        /// </summary>
        internal static double LstarOfY(double y)
            => y > 0.008856 ? 116 * Math.Cbrt(y) - 16 : 903.3 * y;

        /// <summary>Inversa lui LstarOfY: luminanta relativa Y (0..1) a unui L*.</summary>
        internal static double YOfLstar(double l)
            => l > 903.3 * 0.008856 ? Math.Pow((l + 16) / 116, 3) : l / 903.3;

        /// <summary>
        /// Nivelul de 8 biti (0..255, fractionar) al unui gri R = G = B cu L*-ul dat - ce
        /// citesti pe histograma din Siril pentru un prag exprimat in L*.
        /// </summary>
        internal static double GrayLevelOfLstar(double l)
            => LinearToSrgb(YOfLstar(l)) * 255;

        /// <summary>L*-ul unui gri R = G = B la nivelul de 8 biti dat.</summary>
        internal static double LstarOfGrayLevel(double level)
            => LstarOfY(SrgbToLinear(level / 255));

        /// <summary>
        /// L*-ul unui pixel sRGB. Pe mono, cele trei componente sunt aceeasi valoare si
        /// ponderile se aduna la 1, deci formula e aceeasi.
        /// </summary>
        internal static double LstarOfSrgb(double r, double g, double b)
            => LstarOfY(Luminance(SrgbToLinear(r), SrgbToLinear(g), SrgbToLinear(b)));

        /// <summary>
        /// Cel mai negru negru pe care il poate reda un mediu cu densitatea maxima data:
        /// reflectanta 10^-Dmax, in L*. Gloss baryta (Dmax 2.4) -> L* ~3.6; canvas mat
        /// (1.5) -> ~21; C-print sub acril (2.2) -> ~6. Tot ce e sub pragul asta in
        /// fisier se printeaza cu ACELASI negru - de aici pleaca recomandarea de fundal.
        /// </summary>
        internal static double PaperBlackLstar(double dmax)
            => LstarOfY(Math.Pow(10, -dmax));
    }
}
