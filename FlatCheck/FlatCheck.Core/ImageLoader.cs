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

// ImageLoader.cs - the ONLY place that decides whether a file is a FITS or a camera raw.
//
// That is the whole difference between the two formats, and it matters that it stays so: from
// here down nothing knows any more where the pixels came from. The analyzers get an Img, the
// reports get a result, and --expo gets the same scale whether it read it from the NEF or
// guessed it from the FITS. If this decision spreads through the code once more, the next
// difference in behaviour between the formats will appear silently.
//
// It mirrors load_image_with_header of flatcheck.py, including the choice NOT to validate the
// extension: anything other than the six raw extensions goes to the FITS reader and fails there
// as a parse error (see docs/design/known-issues.md, point 2).

using System.Collections.Generic;

namespace FlatCheck
{
    /// <summary>
    /// A loaded frame, with everything the file can say about it. The missing fields are not
    /// errors, they are the right answer for that format: a raw has no FITS header, and a FITS
    /// has no ADU scale written by the camera.
    /// </summary>
    internal sealed class LoadedFrame
    {
        internal Img Image;

        /// <summary>The FITS cards; EMPTY (not null) for a raw - see LightSuitability.</summary>
        internal IReadOnlyDictionary<string, string> Header;

        /// <summary>
        /// The same cards, but RAW - the 80 characters of each, in the file's order, without
        /// END. Empty for a raw. It exists for the smoothing mode, which writes a second master
        /// and wants to give it the input's header untouched: the dictionary above loses the
        /// comments and cuts the strings at the first '/', so it cannot be the source of a header
        /// written back.
        /// </summary>
        internal IReadOnlyList<string> Cards = System.Array.Empty<string>();

        /// <summary>NAXIS3 of the HDU read; 1 for a 2D image and for a raw.</summary>
        internal int Planes = 1;

        /// <summary>BITPIX of the HDU read; 0 for a raw, which has no such thing.</summary>
        internal int Bitpix;

        /// <summary>
        /// The exact ADU scale from the file, or null if the file does not carry it.
        /// </summary>
        internal ExposureScale Scale;

        /// <summary>
        /// What the file knows about the shot, for the synthetic flat: the focal length
        /// (FOCALLEN on a FITS, libraw on a raw), the aperture (raw only - Siril does not write
        /// the f-number), the camera's name (INSTRUME / libraw) and the Nikon lens ID (raw only;
        /// it is what the Adobe profiles carry too, in LensID / AlternateLensIDs). Null when the
        /// file is silent - the same principle as for Scale.
        /// </summary>
        internal double? FocalMm;
        internal double? FNumber;
        internal string Camera;
        internal long? NikonLensId;

        /// <summary>
        /// The lens's range from the raw's MakerNote, as libraw decodes it: the minimum and
        /// maximum focal length, and the maximum aperture at the minimum focal length. One Nikon
        /// ID is carried by several lenses (139: Tokina 11-20 f/2.8, Nikkor 18-200 f/3.5-5.6,
        /// Sigma 18-35 f/1.8), and the range is what tells them apart. Null when libraw gives
        /// zero.
        /// </summary>
        internal double? LensMinFocalMm;
        internal double? LensMaxFocalMm;
        internal double? LensMaxAperture;

        /// <summary>
        /// The order of the colours in the CFA mosaic, or null when the file does not say. The
        /// same principle as for <see cref="Scale"/>: null is the right answer for a file that is
        /// silent, not an error - the order differs from camera to camera and is not guessed.
        /// </summary>
        internal CfaPattern Cfa;

        /// <summary>
        /// The embedded preview, UNDECODED, or null when the format does not carry one (FITS,
        /// always). It is copied while the file is open anyway - a few MB at most - and decoded
        /// only if someone asks for it; see ExposureRun and PreviewReader.
        /// </summary>
        internal EmbeddedPreview Preview;
    }

    /// <summary>
    /// Loads a frame through the reader that claims its file, and falls back to the FITS reader
    /// for any file no reader claims.
    /// </summary>
    internal static class ImageLoader
    {
        // The readers, one stateless instance each, created once at the first load.
        // ImageLoader knows the API of none of them - only the contract.
        static readonly IFrameReader Fits = new FitsReader();
        static readonly IFrameReader[] Readers = { new RawReader(), Fits };

        /// <summary>Loads a frame with its pixels and everything the file says about it.</summary>
        /// <param name="path">The file to read: a camera raw, a FITS, or anything else, which is
        /// read as a FITS.</param>
        /// <returns>The frame, its image filled.</returns>
        internal static LoadedFrame Load(string path)
        {
            foreach (IFrameReader r in Readers)
                if (r.Handles(path)) return r.Load(path);

            // What no one claims still goes to FITS. It is a decision, not an oversight: a .jpg
            // given by mistake fails with a FITS parse error, which still points at the guilty
            // file, instead of "unrecognised format" (see docs/design/known-issues.md, point 2).
            // It sits explicitly here, not as the last element of the list, so that a reordering
            // of the list cannot break it silently.
            return Fits.Load(path);
        }

        /// <summary>
        /// When all you need is the pixels - structure, the --light series.
        /// </summary>
        /// <param name="path">The file to read, as for <see cref="Load"/>.</param>
        /// <returns>The frame's image.</returns>
        internal static Img LoadImage(string path) => Load(path).Image;

        /// <summary>
        /// When all you need is the metadata - identifying the lens, prefilling the Synthetic
        /// tab. The same dispatch as <see cref="Load"/>, the fall onto FITS included, so that the
        /// two cannot reach different readers for the same file. <see cref="LoadedFrame.Image"/>
        /// is null.
        /// </summary>
        /// <param name="path">The file to read, as for <see cref="Load"/>.</param>
        /// <returns>The frame's metadata.</returns>
        internal static LoadedFrame LoadInfo(string path)
        {
            foreach (IFrameReader r in Readers)
                if (r.Handles(path)) return r.LoadInfo(path);
            return Fits.LoadInfo(path);
        }
    }
}
