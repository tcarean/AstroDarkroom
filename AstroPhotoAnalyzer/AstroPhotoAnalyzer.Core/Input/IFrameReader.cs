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

// IFrameReader.cs - the contract every input format implements.
//
// The implementations are the only code that reads a format into a raster (FitsWriter, Wcs and
// the curve save also know FITS, and AnalysisRun reads a --wcs header). ImageLoader names them
// only in its list of readers and chooses among them through this contract, so a new format is
// added to that list without touching the dispatch - and, more importantly, without any
// difference in behaviour between formats being able to slip into it.

using System.Collections.Generic;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// A reader of one family of image formats, chosen by <see cref="ImageLoader"/> through
    /// <see cref="Handles"/>.
    /// </summary>
    internal interface IFrameReader
    {
        /// <summary>
        /// Whether this reader claims the file. The decision is on the EXTENSION, not on the
        /// content: if the bytes were sniffed, a damaged file could fall silently to another
        /// reader and come out of it as noise instead of an error.
        /// </summary>
        /// <param name="path">
        /// The file's path; only its extension is read, in any case. Null or empty is claimed by
        /// no reader.
        /// </param>
        /// <returns><c>true</c> when the extension is one of <see cref="Extensions"/>.</returns>
        bool Handles(string path);

        /// <summary>
        /// The extensions claimed, with the dot, in lower case: for the messages and for the
        /// window's file filters.
        /// </summary>
        IReadOnlyList<string> Extensions { get; }

        /// <summary>
        /// Reads the image into a raster normalised to 0..1, with its <see cref="Raster.Source"/>
        /// filled in. Throws when the file cannot be read or holds no image the reader accepts;
        /// the message is the reader's, or for some failures the platform's or the library's.
        /// </summary>
        /// <param name="path">A file this reader handles.</param>
        /// <returns>
        /// The raster: 1 or 3 planes, row 0 the image's first row as the reader takes it (a FITS
        /// is always flipped, its file's first row last, as Siril shows it whatever ROWORDER
        /// says; an EXIF or TIFF orientation tag is not applied).
        /// </returns>
        Raster Load(string path);
    }
}
