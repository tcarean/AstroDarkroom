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

// CropAnalyzer.cs - masuratoarea A3: ce se taie ca imaginea sa intre intr-un raport de
// aspect dat. Un calcul, nu o masuratoare - dar e in Analysis fiindca raportul si
// fereastra il consuma ca pe oricare alt rezultat, si fiindca decupajul centrat e
// EXACT cel pe care s-au masurat cifrele de acceptanta (5554 x 3888 din 5938 x 3888).

using System;

namespace AstroPhotoAnalyzer
{
    internal sealed record CropResult
    {
        internal bool Needed { get; init; }
        internal double NativeAspect { get; init; }
        internal double? TargetAspect { get; init; }

        /// <summary>Dreptunghiul pastrat, centrat. Fara tinta e toata imaginea.</summary>
        internal int X0 { get; init; }
        internal int Y0 { get; init; }
        internal int W { get; init; }
        internal int H { get; init; }

        /// <summary>"latime" sau "inaltime": pe ce axa se taie. Null cand nu se taie.</summary>
        internal string Axis { get; init; }
        internal int CutPerSidePx { get; init; }
        internal double CutPct { get; init; }
    }

    internal static class CropAnalyzer
    {
        internal static CropResult Analyze(int w, int h, double? targetAspect)
        {
            double native = w / (double)h;
            if (targetAspect == null || Math.Abs(targetAspect.Value - native) < 1e-6)
                return new CropResult { Needed = false, NativeAspect = native, TargetAspect = targetAspect, X0 = 0, Y0 = 0, W = w, H = h };

            double t = targetAspect.Value;
            if (t < native)
            {
                // Tinta e mai ingusta: se taie din latime, egal pe ambele parti.
                int newW = (int)Math.Round(h * t);
                int cut = (w - newW) / 2;
                return new CropResult
                {
                    Needed = true, NativeAspect = native, TargetAspect = t,
                    X0 = cut, Y0 = 0, W = newW, H = h,
                    Axis = "latime", CutPerSidePx = cut, CutPct = 100.0 * (w - newW) / w,
                };
            }
            else
            {
                int newH = (int)Math.Round(w / t);
                int cut = (h - newH) / 2;
                return new CropResult
                {
                    Needed = true, NativeAspect = native, TargetAspect = t,
                    X0 = 0, Y0 = cut, W = w, H = newH,
                    Axis = "inaltime", CutPerSidePx = cut, CutPct = 100.0 * (h - newH) / h,
                };
            }
        }
    }
}
