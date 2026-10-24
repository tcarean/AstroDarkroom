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

// SplitCost.cs - how much disk space a split needs, before it starts.
//
// Two figures: what is COPIED (exact, from the file sizes, given by the caller) and how much
// Siril writes into process/ for each part (estimated). The estimate is a bytes-per-pixel
// model, measured on 2026-09-28 on Siril 1.4.4 with OSC_Preprocessing_WithoutDark on a D3300
// (6016 x 4016 = 24.2 Mpx): a converted frame takes 2 B/px (16 bits), a calibrated one 4 B/px
// (float), a calibrated or registered light 12 B/px (float, debayered on three channels), the
// masters 4 B/px, result.fit 12 B/px. On a quarter of a session (30 biases, 10 flats, 2
// lights) the model gives 4.6 GB against 4.35 GB measured; the "~" is honest.
// Time is not estimated: it depends on the machine and on Siril, not on us (the author's
// decision).

namespace FlatCheck
{
    /// <summary>
    /// The disk cost of a split, in bytes: what is copied (exact) and what Siril will write
    /// into <c>process/</c> (estimated by <see cref="SplitCost.Estimate"/>).
    /// </summary>
    internal sealed class SplitCostEstimate
    {
        /// <summary>The bytes copied in total, over all parts - exact.</summary>
        internal readonly long CopiedBytes;
        /// <summary>
        /// The bytes Siril will write into process/, over all parts - estimated; 0 when the
        /// sensor size is unknown.
        /// </summary>
        internal readonly long ProcessBytes;

        /// <summary>Holds the two figures.</summary>
        /// <param name="copiedBytes">The bytes copied over all parts, exact.</param>
        /// <param name="processBytes">The bytes Siril will write over all parts, estimated;
        /// 0 when unknown.</param>
        internal SplitCostEstimate(long copiedBytes, long processBytes)
        {
            CopiedBytes = copiedBytes;
            ProcessBytes = processBytes;
        }

        internal long TotalBytes => CopiedBytes + ProcessBytes;
    }

    /// <summary>
    /// The bytes-per-pixel model of what a split costs on disk (ADR 0054), measured on Siril
    /// 1.4.4; the words and units are <c>SplitReport.CostLine</c>'s.
    /// </summary>
    internal static class SplitCost
    {
        internal const int ConvertedBytesPerPx = 2;
        internal const int CalibratedBytesPerPx = 4;
        internal const int CalibratedLightBytesPerPx = 12;
        internal const int MasterBytesPerPx = 4;
        internal const int ResultBytesPerPx = 12;

        /// <summary>
        /// The pixels of a frame from its FITS header (NAXIS1 x NAXIS2); 0 when the header
        /// lacks them.
        /// </summary>
        /// <param name="header">The FITS header cards; null allowed (gives 0).</param>
        /// <returns>The pixel count of one frame, or 0 when unknown.</returns>
        internal static long Pixels(System.Collections.Generic.IReadOnlyDictionary<string, string> header)
            => header == null ? 0 : (long)FitsReader.HInt(header, "NAXIS1", 0) * FitsReader.HInt(header, "NAXIS2", 0);

        /// <summary>
        /// Estimates what a split costs: the copied bytes as given, plus what Siril writes per
        /// part, from the bytes-per-pixel constants above.
        /// </summary>
        /// <param name="pixels">The pixels of one frame (the sensor), 0 when unknown.</param>
        /// <param name="parts">How many parts.</param>
        /// <param name="wholeFrames">How many frames each part has in the folders copied whole
        /// (biases, darks); each folder also yields a master.</param>
        /// <param name="wholeFolders">How many folders are copied whole.</param>
        /// <param name="flatsPerPart">How many flats each part has.</param>
        /// <param name="lightsPerPart">How many lights each part has.</param>
        /// <param name="copiedBytes">The bytes copied over all parts, from the file
        /// sizes.</param>
        /// <returns>The two figures; the process share is 0 when <paramref name="pixels"/> or
        /// <paramref name="parts"/> is not positive.</returns>
        internal static SplitCostEstimate Estimate(long pixels, int parts, int wholeFrames, int wholeFolders,
                                                   int flatsPerPart, int lightsPerPart, long copiedBytes)
        {
            if (pixels <= 0 || parts <= 0) return new SplitCostEstimate(copiedBytes, 0);

            long perPart = pixels * (
                (long)ConvertedBytesPerPx * wholeFrames + (long)MasterBytesPerPx * wholeFolders
                + (long)(ConvertedBytesPerPx + CalibratedBytesPerPx) * flatsPerPart + MasterBytesPerPx
                + (long)(ConvertedBytesPerPx + 2 * CalibratedLightBytesPerPx) * lightsPerPart
                + (lightsPerPart > 0 ? ResultBytesPerPx : 0));
            return new SplitCostEstimate(copiedBytes, perPart * parts);
        }
    }
}
