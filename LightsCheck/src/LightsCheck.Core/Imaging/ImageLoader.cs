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
// embedded preview is gone, the shot metadata a light needs is added, and the diagnostic log
// gets one line per file read here, the FITS reader being a copy left as it is (ADR 0029).

using System.Diagnostics;
using NLog;

namespace LightsCheck.Core
{
    /// <summary>
    /// The exact ADU scale a file carries: white and black, and where they were read.
    /// </summary>
    internal sealed class AduScale
    {
        internal readonly double White;
        internal readonly double Black;
        internal readonly string Source;

        /// <summary>Holds the scale as read.</summary>
        /// <param name="white">The white level, in ADU.</param>
        /// <param name="black">The black level, in ADU.</param>
        /// <param name="source">
        /// Where the two were read, for the report (e.g. "libraw (exact)").
        /// </param>
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

        /// <summary>
        /// The same cards, raw: the 80 characters of each, in file order, without END. Empty for a
        /// raw.
        /// </summary>
        internal IReadOnlyList<string> Cards = Array.Empty<string>();

        /// <summary>NAXIS3 of the HDU read; 1 for a 2D image and for a raw.</summary>
        internal int Planes = 1;

        /// <summary>BITPIX of the HDU read; 0 for a raw.</summary>
        internal int Bitpix;

        /// <summary>
        /// The exact ADU scale from the file, or null when the file does not carry one.
        /// </summary>
        internal AduScale Scale;

        /// <summary>
        /// What the file knows about the shot: focal length (FOCALLEN on FITS, libraw on raw),
        /// f-number (raw only), camera name (INSTRUME / libraw), exposure in seconds (EXPTIME /
        /// shutter), ISO (ISOSPEED / iso_speed), the time of capture (DATE-OBS, UTC as Siril
        /// writes it / libraw's timestamp, the camera's wall-clock), the pixel size in microns
        /// (XPIXSZ; a raw does not say it). Null when the file is silent.
        /// </summary>
        internal double? FocalMm;
        internal double? FNumber;
        internal string Camera;
        internal double? ExposureSeconds;
        internal double? Iso;
        internal DateTime? Timestamp;
        internal double? PixelSizeUm;

        /// <summary>
        /// The 35 mm-equivalent focal length the camera wrote (EXIF FocalLengthIn35mmFilm, through
        /// libraw's LensInfo), in mm, an integer: 202 for 135 mm on a D3300. Raw only; null when
        /// the file gives none or 0. With the shot's focal length and the mosaic's size it gives
        /// the pixel size (PixelPitch.FromCrop, R-066).
        /// </summary>
        internal double? FocalLength35Mm;

        /// <summary>
        /// The visible mosaic's width and height in pixels, as libraw reports them before any
        /// decode (6016 x 4016 on a D3300); 0 when unknown (a FITS, whose size is its Img's).
        /// </summary>
        internal int Width;
        internal int Height;

        /// <summary>
        /// The lens ID the raw carries, whichever the mount (raw only; R-065, FlatCheck's R-108):
        /// Nikon's LensIDNumber on a Nikon body (74 for a lens without CPU, 139 for the Tokina
        /// 11-20), the MakerNote's LensID elsewhere - Canon's lens type (198 = EF 50mm f/1.4 USM),
        /// Sony's (32831 = FE 16-35mm F2.8 GM), Pentax's two bytes as a*256+b (2263 = 8 215).
        /// Null when the file gives none; a FITS never has one. The inventory's lens line prints
        /// it with the mount.
        /// </summary>
        internal long? LensId;

        /// <summary>
        /// The camera's mount as libraw names it - "Nikon F", "Canon EF", "Sony E", "Pentax K" -
        /// so the inventory can say whose number the ID is; null when unknown (a FITS, always).
        /// </summary>
        internal string LensMount;

        /// <summary>
        /// The lens's name as the camera wrote it (EXIF LensModel: "EF50mm f/1.4 USM", "FE 16-35mm
        /// F2.8 GM"). Null when the file has none: a D3300 with any lens FlatCheck measured, a
        /// Pentax K-70, every FITS.
        /// </summary>
        internal string LensName;

        /// <summary>
        /// The lens range as libraw decodes it, from the raw's MakerNote or the top of its
        /// LensInfo (RawReader.RangeOf); null when every source gives zero.
        /// </summary>
        internal double? LensMinFocalMm;
        internal double? LensMaxFocalMm;
        internal double? LensMaxAperture;

        /// <summary>
        /// The colour order of the CFA mosaic, or null when the file does not say it.
        /// </summary>
        internal CfaPattern Cfa;
    }

    /// <summary>
    /// Loads a frame through the reader that claims its file, and falls back to the FITS reader
    /// for any file no reader claims.
    /// </summary>
    internal static class ImageLoader
    {
        static readonly FitsReader Fits = new FitsReader();
        static readonly IFrameReader[] Readers = { new RawReader(), Fits };

        /// <summary>
        /// The diagnostic log: one line per file read (R-071), the FITS keywords that decide the
        /// reading at Debug and every card at Trace (R-072). A file that does not load writes
        /// nothing here: its caller logs the exception.
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger(nameof(ImageLoader));

        /// <summary>Loads a frame with its pixels and everything the file says about it.</summary>
        /// <param name="path">
        /// The file to read: a camera raw, a FITS, or anything else, which is read as a FITS.
        /// </param>
        /// <returns>The frame, its image filled.</returns>
        internal static LoadedFrame Load(string path)
        {
            var clock = Stopwatch.StartNew();
            IFrameReader reader = ReaderFor(path);
            LoadedFrame f = reader.Load(path);
            if (Log.IsInfoEnabled)
                Log.Info("loaded " + LogText.Frame(path, f, NameOf(reader)) + " in " + LogText.Ms(clock.ElapsedMilliseconds));
            LogHeader(path, f);
            return f;
        }

        /// <summary>When all that is needed is the pixels.</summary>
        /// <param name="path">The file to read, as for <see cref="Load"/>.</param>
        /// <returns>The frame's image.</returns>
        internal static Img LoadImage(string path) => Load(path).Image;

        /// <summary>
        /// The metadata only; the same dispatch as <see cref="Load"/>.
        /// <see cref="LoadedFrame.Image"/> is null.
        /// </summary>
        /// <param name="path">The file to read, as for <see cref="Load"/>.</param>
        /// <returns>The frame's metadata, its image null.</returns>
        internal static LoadedFrame LoadInfo(string path)
        {
            var clock = Stopwatch.StartNew();
            IFrameReader reader = ReaderFor(path);
            LoadedFrame f = reader.LoadInfo(path);
            if (Log.IsDebugEnabled)
                Log.Debug("read the header of " + LogText.Frame(path, f, NameOf(reader)) + " in " + LogText.Ms(clock.ElapsedMilliseconds));
            LogHeader(path, f);
            return f;
        }

        /// <summary>The reader that claims a file.</summary>
        /// <param name="path">The file to read.</param>
        /// <returns>The first reader whose Handles says so; the FITS reader for any other file.</returns>
        static IFrameReader ReaderFor(string path)
        {
            foreach (IFrameReader r in Readers)
                if (r.Handles(path)) return r;

            // What nobody claims goes to FITS: a .jpg given by mistake fails with a FITS
            // parse error that names the file, instead of "unrecognised format".
            return Fits;
        }

        /// <summary>A reader's name in the log.</summary>
        /// <param name="reader">The reader that read the file.</param>
        /// <returns><c>raw</c> or <c>fits</c>.</returns>
        static string NameOf(IFrameReader reader) => reader is RawReader ? "raw" : "fits";

        /// <summary>
        /// Writes a FITS header to the log: the keywords that decide the reading at Debug, every
        /// card at Trace (the cards are kept by a full load only). A raw has neither.
        /// </summary>
        /// <param name="path">The file read.</param>
        /// <param name="f">The frame read from it.</param>
        static void LogHeader(string path, LoadedFrame f)
        {
            if (f.Header != null && f.Header.Count > 0 && Log.IsDebugEnabled)
                Log.Debug("header of " + path + ": " + LogText.FitsKeys(f.Header));
            if (Log.IsTraceEnabled)
                foreach (string card in f.Cards)
                    Log.Trace("card " + card.TrimEnd());
        }
    }
}
