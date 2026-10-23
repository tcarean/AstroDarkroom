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

// BitDepthAnalyzer.cs - the measurement A2: are the 16 bits real, or an 8-bit upscale?
//
// An 8-bit file saved as 16 has every value equal to its high byte times 257
// (n * 65535 / 255). On real 16 bits this coincidence happens on ~1 in 256 samples (0.4 %); on an
// upscale, on all of them. The distinct levels per channel are counted too - a disguised 8-bit
// has no more than 256.
//
// It is measured on ALL the pixels, not on a subsample: it is a count, not a statistic, and it
// costs one pass through memory.

using System.Collections.Generic;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// Whether the 16 bits of an integer source are real or an 8-bit upscale (A2).
    /// </summary>
    internal sealed record BitDepthResult
    {
        /// <summary>
        /// True only on a 16-bit integer source; false on 8-bit sources, on float FITS, on 32-bit
        /// integer FITS and without a Source - there the question makes no sense.
        /// </summary>
        internal bool Applicable { get; init; }

        /// <summary>The distinct levels in each plane, of 65536 possible.</summary>
        internal IReadOnlyList<int> DistinctLevels { get; init; }

        /// <summary>
        /// The fraction of the samples (all planes) with value == high_byte * 257.
        /// </summary>
        internal double UpscaledFraction { get; init; }

        internal bool IsUpscaled => Applicable && UpscaledFraction > Thresholds.UpscaledFraction;
    }

    /// <summary>Tells real 16-bit data from an 8-bit file saved as 16 bits (A2).</summary>
    internal static class BitDepthAnalyzer
    {
        /// <summary>
        /// On a 16-bit integer source, counts on every sample the distinct levels per plane and
        /// the samples equal to their high byte times 257.
        /// </summary>
        /// <param name="r">The image, 0..1; its Source says whether the question applies.</param>
        /// <returns>
        /// The counts; not applicable, with zero counts, unless the source is 16-bit integer.
        /// </returns>
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
