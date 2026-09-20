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

// BitDepthAnalyzer.cs - masuratoarea A2: sunt cei 16 biti reali, sau un 8-bit scalat?
//
// Un fisier de 8 biti salvat ca 16 are fiecare valoare egala cu octetul ei inalt
// inmultit cu 257 (n * 65535 / 255). Pe 16 biti reali coincidenta asta se intampla la
// ~1 din 256 pixeli (0.4 %); pe un upscale, la toti. Se numara si nivelurile distincte
// pe canal - un 8-bit deghizat n-are mai mult de 256.
//
// Se masoara pe TOTI pixelii, nu pe subesantion: e o numaratoare, nu o statistica, si
// costa o trecere prin memorie.

using System.Collections.Generic;

namespace AstroPhotoAnalyzer
{
    internal sealed record BitDepthResult
    {
        /// <summary>Fals pe surse de 8 biti si pe FITS float - acolo intrebarea n-are sens.</summary>
        internal bool Applicable { get; init; }

        /// <summary>Niveluri distincte pe fiecare plan, din 65536 posibile.</summary>
        internal IReadOnlyList<int> DistinctLevels { get; init; }

        /// <summary>Fractia esantioanelor (toate planurile) cu valoarea == octet_inalt * 257.</summary>
        internal double UpscaledFraction { get; init; }

        internal bool IsUpscaled => Applicable && UpscaledFraction > Thresholds.UpscaledFraction;
    }

    internal static class BitDepthAnalyzer
    {
        internal static BitDepthResult Analyze(Raster r)
        {
            SourceInfo s = r.Source;
            bool applicable = s != null && !s.IsFloat && s.BitDepth == 16;
            if (!applicable)
                return new BitDepthResult { Applicable = false, DistinctLevels = new int[r.Planes.Length], UpscaledFraction = 0 };

            var distinct = new int[r.Planes.Length];
            long coincidences = 0, total = 0;
            var seen = new bool[65536];
            for (int p = 0; p < r.Planes.Length; p++)
            {
                System.Array.Clear(seen, 0, seen.Length);
                int count = 0;
                float[] plane = r.Planes[p];
                for (int i = 0; i < plane.Length; i++)
                {
                    int v = RasterOps.Level16(plane[i]);
                    if (!seen[v]) { seen[v] = true; count++; }
                    if (v == (v >> 8) * 257) coincidences++;
                }
                distinct[p] = count;
                total += plane.Length;
            }
            return new BitDepthResult
            {
                Applicable = true,
                DistinctLevels = distinct,
                UpscaledFraction = coincidences / (double)total,
            };
        }
    }
}
