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

#nullable disable // adapted from FlatCheck 2.3.0, which compiles without nullable annotations (ADR 0003)

// ImageLoader.cs - the ONE place that decides whether a file is FITS or a camera raw. From
// here down nothing knows where the pixels came from. Adapted from FlatCheck (ADR 0003): the
// embedded preview is gone, the shot metadata a light needs is added.

namespace LightsCheck.Core
{
    /// <summary>The exact ADU scale a file carries: white and black, and where they were read.</summary>
    internal sealed class AduScale
    {
        internal readonly double White;
        internal readonly double Black;
        internal readonly string Source;

        internal AduScale(double white, double black, string source)
        {
            White = white; Black = black; Source = source;
        }
    }

    /// <summary>
    /// A loaded frame, with everything the file can say about it. A missing field is not an
    /// error but the right answer for that format: a raw has no FITS header, a FITS has no ADU
    /// scale written by the camera.
    /// </summary>
    internal sealed class LoadedFrame
    {
        internal Img Image;

        /// <summary>The FITS cards; EMPTY (not null) for a raw.</summary>
        internal IReadOnlyDictionary<string, string> Header;

        /// <summary>The same cards, raw: the 80 characters of each, in file order, without END. Empty for a raw.</summary>
        internal IReadOnlyList<string> Cards = Array.Empty<string>();

        /// <summary>NAXIS3 of the HDU read; 1 for a 2D image and for a raw.</summary>
        internal int Planes = 1;

        /// <summary>BITPIX of the HDU read; 0 for a raw.</summary>
        internal int Bitpix;

        /// <summary>The exact ADU scale from the file, or null when the file does not carry one.</summary>
        internal AduScale Scale;

        /// <summary>
        /// What the file knows about the shot: focal length (FOCALLEN on FITS, libraw on raw),
        /// f-number (raw only), camera name (INSTRUME / libraw), exposure in seconds (EXPTIME /
        /// shutter), ISO (ISOSPEED / iso_speed), the time of capture (DATE-OBS, UTC as Siril
        /// writes it / libraw's timestamp, the camera's wall-clock), the pixel size in microns
        /// (XPIXSZ; a raw does not say it) and the Nikon lens ID (raw only). Null when the file
        /// is silent.
        /// </summary>
        internal double? FocalMm;
        internal double? FNumber;
        internal string Camera;
        internal double? ExposureSeconds;
        internal double? Iso;
        internal DateTime? Timestamp;
        internal double? PixelSizeUm;
        internal long? NikonLensId;

        /// <summary>The lens range from the raw's MakerNote, as libraw decodes it; null when zero.</summary>
        internal double? LensMinFocalMm;
        internal double? LensMaxFocalMm;
        internal double? LensMaxAperture;

        /// <summary>The colour order of the CFA mosaic, or null when the file does not say it.</summary>
        internal CfaPattern Cfa;
    }

    internal static class ImageLoader
    {
        static readonly FitsReader Fits = new FitsReader();
        static readonly IFrameReader[] Readers = { new RawReader(), Fits };

        internal static LoadedFrame Load(string path)
        {
            foreach (IFrameReader r in Readers)
                if (r.Handles(path)) return r.Load(path);

            // What nobody claims goes to FITS: a .jpg given by mistake fails with a FITS
            // parse error that names the file, instead of "unrecognised format".
            return Fits.Load(path);
        }

        internal static Img LoadImage(string path) => Load(path).Image;

        /// <summary>The metadata only; the same dispatch as <see cref="Load"/>. <see cref="LoadedFrame.Image"/> is null.</summary>
        internal static LoadedFrame LoadInfo(string path)
        {
            foreach (IFrameReader r in Readers)
                if (r.Handles(path)) return r.LoadInfo(path);
            return Fits.LoadInfo(path);
        }
    }
}
