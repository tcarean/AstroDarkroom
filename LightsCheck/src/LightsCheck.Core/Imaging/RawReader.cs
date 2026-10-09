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

// RawReader.cs - camera raw files (NEF and its relatives) through libraw: the one place in
// the tool that touches an external library. Adapted from FlatCheck's reader (ADR 0003): the
// embedded preview is gone, the shot metadata a light needs is added (exposure, ISO, time).
//
// What is asked of libraw is the raw data, never a developed image:
//   - raw_image, cropped to the visible area   -> the CFA mosaic, as rawpy's raw_image_visible
//   - color.maximum                            -> white
//   - color.black + color.cblack[0..3]         -> black, per channel
//   - iparams.filters + iparams.cdesc          -> the colour of each Bayer position
//   - other (shutter, iso_speed, timestamp, focal_len, aperture) and lens info -> metadata
// No interpolation, no white balance, no gamma.
//
// The native DLLs come from the Sdcb.LibRaw.runtime.* packages and sit beside the executable.
// When they are missing, a FITS run is unaffected (this type is loaded only when a raw is
// asked for) and a raw run fails with a message naming what is missing.

using System.Runtime.InteropServices;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;

namespace LightsCheck.Core
{
    internal sealed class RawReader : IFrameReader
    {
        // No field of this class may be of a Sdcb.LibRaw type: fields are resolved when the
        // type is loaded, and the type is loaded on every run, FITS included; method bodies
        // only at JIT time. Measured in FlatCheck, pinned by RawReaderTests.
        static readonly IReadOnlyDictionary<string, string> NoHeader = new Dictionary<string, string>();

        static readonly string[] Extensions =
        { ".nef", ".cr2", ".cr3", ".arw", ".dng", ".raf" };

        /// <summary>The extensions recognised as camera raws; the session folder filters by them.</summary>
        internal static IReadOnlyList<string> RawExtensions => Extensions;

        /// <summary>The dispatch gate: decided on the extension alone.</summary>
        internal static bool IsRawPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(Extensions, ext) >= 0;
        }

        public bool Handles(string path) => IsRawPath(path);

        public LoadedFrame Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"nu gasesc fisierul {path}", path);

            try
            {
                return Decode(path);
            }
            catch (DllNotFoundException e) { throw Missing(e); }
            catch (FileNotFoundException e) when (e.FileName != path) { throw Missing(e); }
            catch (BadImageFormatException e) { throw Missing(e); }
            catch (TypeInitializationException e) { throw Missing(e); }
        }

        /// <summary>
        /// The metadata only: libraw fills the shot fields at OpenFile, before Unpack, so the
        /// file is opened and those are read. No pixels, no scale: Image null, Header empty.
        /// Tens of milliseconds on a D3300 NEF instead of seconds.
        /// </summary>
        public LoadedFrame LoadInfo(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"nu gasesc fisierul {path}", path);

            try
            {
                return ReadInfo(path);
            }
            catch (DllNotFoundException e) { throw Missing(e); }
            catch (FileNotFoundException e) when (e.FileName != path) { throw Missing(e); }
            catch (BadImageFormatException e) { throw Missing(e); }
            catch (TypeInitializationException e) { throw Missing(e); }
        }

        static LoadedFrame ReadInfo(string path)
        {
            using RawContext ctx = RawContext.OpenFile(path);
            LibRawData d = Marshal.PtrToStructure<LibRawData>(ctx.UnsafeGetHandle());
            var f = new LoadedFrame { Image = null, Header = NoHeader };
            FillShootingInfo(f, ctx, d.RawData.IParams);
            return f;
        }

        static InvalidOperationException Missing(Exception inner)
            => new InvalidOperationException(
                "nu pot incarca libraw, deci nu pot citi fisiere raw de aparat. DLL-urile "
              + "native (raw_r.dll si vecinii) trebuie sa stea langa lightscheck.exe. Cadrele "
              + "FITS merg si fara ele; pentru raw, publica cu IncludeNativeLibrariesForSelfExtract "
              + "sau copiaza tot folderul, nu doar executabilul. (" + inner.Message + ")", inner);

        static LoadedFrame Decode(string path)
        {
            using RawContext ctx = RawContext.OpenFile(path);
            ctx.Unpack();

            LibRawData d = Marshal.PtrToStructure<LibRawData>(ctx.UnsafeGetHandle());
            LibRawImageSizes s = d.RawData.Sizes;
            IntPtr src = d.RawData.RawImage;

            // Foveon and some formats produce colour planes, not a 16-bit mosaic: refused.
            if (src == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"{path}: libraw n-a produs un mozaic Bayer pe 16 biti pentru fisierul asta "
                  + "(senzor non-Bayer sau format neacoperit).");

            int h = s.Height, w = s.Width;
            if (h <= 0 || w <= 0)
                throw new InvalidOperationException($"{path}: dimensiuni raw invalide ({w}x{h}).");

            // The colour order, evaluated at the origin of the VISIBLE area, the same margins
            // the copy below skips. Locals only, never fields (see the note at the top).
            LibRawImageParams ip = d.RawData.IParams;
            CfaPattern cfa = CfaPattern.FromLibRaw(ip.Filters, ip.Cdesc, s.TopMargin, s.LeftMargin);

            // The visible area, as rawpy's raw_image_visible: the masked calibration margins
            // are skipped, or the medians would be pulled down by a black band.
            int stride = s.RawPitch > 0 ? (int)(s.RawPitch / 2) : s.RawWidth;
            var img = new Img(h, w);
            var row = new short[w];
            for (int y = 0; y < h; y++)
            {
                long offset = ((long)(y + s.TopMargin) * stride + s.LeftMargin) * sizeof(ushort);
                Marshal.Copy(src + (nint)offset, row, 0, w);
                int o = y * w;
                // libraw gives ushort, Marshal.Copy knows short: read as short, reinterpret.
                for (int x = 0; x < w; x++) img.D[o + x] = (ushort)row[x];
            }

            LibRawColorData c = d.RawData.Color;
            double white = c.Maximum;

            // The black level is global (color.black) or per channel (color.cblack), as libraw
            // splits it; the sum is what rawpy reads, and the median of the four is taken.
            var blacks = new List<double>(4);
            for (int i = 0; i < 4; i++)
                blacks.Add(c.Black + (c.CBlack != null && i < c.CBlack.Length ? c.CBlack[i] : 0));
            double black = Stats.Percentile(blacks, 50);

            if (!(white > black))
                throw new InvalidOperationException(
                    $"{path}: libraw da o scala imposibila (white {white:F0} <= black {black:F0}).");

            var f = new LoadedFrame
            {
                Image = img,
                // Empty, not absent: a raw has no FITS header.
                Header = NoHeader,
                Scale = new AduScale(white, black, "libraw (exact)"),
                Cfa = cfa,
            };
            FillShootingInfo(f, ctx, ip);
            return f;
        }

        /// <summary>
        /// Focal length, f-number, camera name, exposure, ISO, time of capture, the Nikon lens
        /// ID and the lens range, each null when libraw gives zero or empty. Locals only.
        ///
        /// The time is libraw's time_t: EXIF DateTimeOriginal run through mktime on this
        /// machine, so converting it back through the local zone gives the wall-clock the camera
        /// wrote, to the second (EXIF SubSecTime is not exposed). The lens range comes from
        /// LensInfo.MakerNotes, not from LensInfo's top fields, which are 0 on a D3300 with a
        /// third-party lens (measured in FlatCheck).
        /// </summary>
        static void FillShootingInfo(LoadedFrame f, RawContext ctx, LibRawImageParams ip)
        {
            LibRawImageOtherParams other = ctx.ImageOtherParams;
            f.FocalMm = other.FocalLength > 0 ? other.FocalLength : null;
            f.FNumber = other.Aperture > 0 ? other.Aperture : null;
            f.ExposureSeconds = other.Shutter > 0 ? other.Shutter : null;
            f.Iso = other.IsoSpeed > 0 ? other.IsoSpeed : null;
            f.Timestamp = other.Timestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(other.Timestamp).ToLocalTime().DateTime
                : null;

            // "NIKON CORPORATION" + "NIKON D3300" reads "NIKON D3300": the model repeats the
            // make's first word; "SONY" + "ILCE-7M3" reads "SONY ILCE-7M3".
            string make = (ip.Make ?? "").Trim(), model = (ip.Model ?? "").Trim();
            string makeWord = make.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            f.Camera = model.Length == 0 ? null
                : makeWord.Length == 0 || model.StartsWith(makeWord, StringComparison.OrdinalIgnoreCase) ? model
                : make + " " + model;

            try
            {
                long id = ctx.LensInfo.Nikon.LensIDNumber;
                if (id > 0) f.NikonLensId = id;
                var notes = ctx.LensInfo.MakerNotes;
                if (notes.MinFocal > 0) f.LensMinFocalMm = notes.MinFocal;
                if (notes.MaxFocal > 0) f.LensMaxFocalMm = notes.MaxFocal;
                if (notes.MaxAperture4MinFocal > 0) f.LensMaxAperture = notes.MaxAperture4MinFocal;
            }
            catch (Exception)
            {
                f.NikonLensId = null;
                f.LensMinFocalMm = f.LensMaxFocalMm = f.LensMaxAperture = null;
            }
        }
    }
}
