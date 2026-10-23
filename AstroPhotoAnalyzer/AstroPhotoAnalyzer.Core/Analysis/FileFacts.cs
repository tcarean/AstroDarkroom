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

// FileFacts.cs - the measurement A1: what the file is. It computes nothing from the pixels; it
// gathers what the reader said and the dimensions, in the form the report and the window read.

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// What the file is (A1): the image's dimensions and colour, and what its reader recorded.
    /// </summary>
    internal sealed record FileFactsResult
    {
        internal int W { get; init; }
        internal int H { get; init; }
        internal double Megapixels => W * (double)H / 1e6;
        internal double NativeAspect => W / (double)H;
        internal bool IsColor { get; init; }
        internal string Format { get; init; }
        internal int BitDepth { get; init; }
        internal bool IsFloat { get; init; }
        internal string Compression { get; init; }
        internal bool HasIcc { get; init; }
        internal string IccDescription { get; init; }
        internal long FileBytes { get; init; }
        internal double FileMegabytes => FileBytes / 1048576.0;
        internal string ReaderNote { get; init; }

        /// <summary>
        /// Is the profile sRGB, by its name? The tool measures assuming sRGB; a profile with
        /// another name (AdobeRGB, ProPhoto) is a fact the report states, not one it corrects.
        /// Without a profile the answer is null: it is not known.
        /// </summary>
        internal bool? IccLooksSrgb => IccDescription == null
            ? (bool?)null
            : IccDescription.IndexOf("srgb", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Gathers what the file is (A1).</summary>
    internal static class FileFacts
    {
        /// <summary>
        /// The facts of an image's file: its dimensions and colour, and what its reader recorded
        /// in <see cref="Raster.Source"/>.
        /// </summary>
        /// <param name="r">
        /// The image; one without a Source (built in memory) reads as format "memorie", 32-bit
        /// float.
        /// </param>
        /// <returns>The facts.</returns>
        internal static FileFactsResult Of(Raster r)
        {
            SourceInfo s = r.Source ?? new SourceInfo { Format = "memorie", BitDepth = 32, IsFloat = true, PlanesInFile = r.Planes.Length };
            return new FileFactsResult
            {
                W = r.W,
                H = r.H,
                IsColor = r.IsColor,
                Format = s.Format,
                BitDepth = s.BitDepth,
                IsFloat = s.IsFloat,
                Compression = s.Compression,
                HasIcc = s.Icc != null,
                IccDescription = s.IccDescription,
                FileBytes = s.FileBytes,
                ReaderNote = s.Note,
            };
        }
    }
}
