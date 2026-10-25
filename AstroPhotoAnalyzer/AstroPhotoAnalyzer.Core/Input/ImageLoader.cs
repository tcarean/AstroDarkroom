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

// ImageLoader.cs - THE ONLY place that decides which reader opens the analysed image (a --wcs
// file goes straight to FitsReader, in AnalysisRun).
//
// From here down the analyzers get a Raster with its SourceInfo and the reports get a result;
// the reader shows only through SourceInfo.Format, which the curve save checks for "FITS" to
// copy the source's header. If this decision were scattered through the code once more, the
// next difference in behaviour between formats would appear silently.
//
// Unlike FlatCheck, an unknown extension is an ERROR, not a fallback to FITS: here every
// format has an extension of its own, and a ".psd" fallen onto the FITS reader would come out
// as "header incomplet" instead of "format neacceptat".

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// Chooses the reader of a file by its extension, and reads the file with it.
    /// </summary>
    internal static class ImageLoader
    {
        static readonly IFrameReader[] Readers = { new FitsReader(), new ImageSharpReader() };

        /// <summary>
        /// Every extension accepted, for the messages and for the window's file filter.
        /// </summary>
        internal static IReadOnlyList<string> Extensions
            => Readers.SelectMany(r => r.Extensions).ToArray();

        /// <summary>
        /// Reads the image with the reader that claims its extension. Throws
        /// <see cref="FileNotFoundException"/> for a file that does not exist, an exception
        /// naming the accepted extensions for an unknown one, and whatever the reader throws.
        /// </summary>
        /// <param name="path">The image file.</param>
        /// <returns>The raster, 0..1, with its <see cref="SourceInfo"/>.</returns>
        internal static Raster Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(Tr.Pick("the file does not exist: ", "fisierul nu exista: ") + path, path);

            IFrameReader reader = Readers.FirstOrDefault(r => r.Handles(path));
            if (reader == null)
                throw new Exception(Tr.Pick($"unsupported format: '{Path.GetExtension(path)}'. Accepted: {string.Join(", ", Extensions)}.", $"format neacceptat: '{Path.GetExtension(path)}'. Se accepta {string.Join(", ", Extensions)}."));

            return reader.Load(path);
        }
    }
}
