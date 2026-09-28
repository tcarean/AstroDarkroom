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

// SplitCost.cs - cat loc pe disc cere un split, inainte sa porneasca.
//
// Doua cifre: ce se COPIAZA (exact, din marimile fisierelor, date de apelant) si cat
// scrie Siril in process/ pentru fiecare parte (estimat). Estimarea e un model pe
// octeti per pixel, masurat pe 2026-09-28 pe Siril 1.4.4 cu OSC_Preprocessing_WithoutDark
// pe un D3300 (6016 x 4016 = 24.2 Mpx): un cadru convertit are 2 B/px (16 biti), unul
// calibrat 4 B/px (float), un light calibrat sau inregistrat 12 B/px (float, debayerizat
// pe trei canale), masterele 4 B/px, result.fit 12 B/px. Pe un sfert de sesiune (30
// biases, 10 flats, 2 lights) modelul da 4.6 GB fata de 4.35 GB masurati; "~" e cinstit.
// Timpul nu se estimeaza: depinde de masina si de Siril, nu de noi (decizia autorului).

namespace FlatCheck
{
    internal sealed class SplitCostEstimate
    {
        /// <summary>Octetii copiati in total, pe toate partile - exact.</summary>
        internal readonly long CopiedBytes;
        /// <summary>Octetii pe care Siril ii va scrie in process/, pe toate partile - estimat; 0 cand nu se stie marimea senzorului.</summary>
        internal readonly long ProcessBytes;

        internal SplitCostEstimate(long copiedBytes, long processBytes)
        {
            CopiedBytes = copiedBytes;
            ProcessBytes = processBytes;
        }

        internal long TotalBytes => CopiedBytes + ProcessBytes;
    }

    internal static class SplitCost
    {
        internal const int ConvertedBytesPerPx = 2;
        internal const int CalibratedBytesPerPx = 4;
        internal const int CalibratedLightBytesPerPx = 12;
        internal const int MasterBytesPerPx = 4;
        internal const int ResultBytesPerPx = 12;

        /// <summary>Pixelii unui cadru din headerul lui FITS (NAXIS1 x NAXIS2); 0 cand headerul nu-i are.</summary>
        internal static long Pixels(System.Collections.Generic.IReadOnlyDictionary<string, string> header)
            => header == null ? 0 : (long)FitsReader.HInt(header, "NAXIS1", 0) * FitsReader.HInt(header, "NAXIS2", 0);

        /// <param name="pixels">Pixelii unui cadru (senzorul), 0 cand nu se stiu.</param>
        /// <param name="parts">Cate parti.</param>
        /// <param name="wholeFrames">Cate cadre are fiecare parte in folderele copiate intregi (biases, darks); fiecare folder da si un master.</param>
        /// <param name="wholeFolders">Cate foldere copiate intregi.</param>
        /// <param name="flatsPerPart">Cate flats are fiecare parte.</param>
        /// <param name="lightsPerPart">Cate lights are fiecare parte.</param>
        /// <param name="copiedBytes">Octetii copiati pe toate partile, din marimile fisierelor.</param>
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
