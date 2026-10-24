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

// RawReader.cs - reading camera raw files (.NEF and its relatives), through libraw.
//
// The only place in the tool that touches an external library. The FITS and the PNG stay
// written by hand; here it was not possible, because a NEF is not just a container: the pixels
// are Huffman-compressed with a predictor and passed through a linearisation curve from the
// MakerNote. There is no pure managed .NET decoder for that.
//
// What is asked of libraw is strictly the raw data, NOT a developed image:
//   - raw_image, cropped to the visible area  -> the CFA mosaic intact, exactly like
//     raw_image_visible of rawpy (which flatcheck.py uses)
//   - color.maximum                           -> white
//   - color.black + color.cblack[0..3]        -> black, per channel
//   - iparams.filters + iparams.cdesc         -> which colour each Bayer position has
// No interpolation, no white balance, no gamma: --expo measures the original ADU level, and any
// development would destroy it.
//
// The native DLLs come from the Sdcb.LibRaw.runtime.* packages and sit BESIDE the executable.
// If they are missing (someone copied only flatcheck.exe), the FITS modes go on unchanged - this
// type is loaded only when a raw file is asked for - and here the run ends with a message that
// says what is missing, not with a DllNotFoundException.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;

namespace FlatCheck
{
    /// <summary>
    /// Reads a camera raw through libraw: the visible CFA mosaic in ADU, the exact white and
    /// black levels, the CFA order, the embedded preview and the shooting metadata.
    /// </summary>
    internal sealed class RawReader : IFrameReader
    {
        // CAREFUL: no field of this class may be a type from Sdcb.LibRaw. The fields are
        // resolved when the type loads, and the type loads on every run, on FITS too - the method
        // bodies, by contrast, only at JIT. A value-type field from libraw would make a run on a
        // FITS crash when the DLLs are missing. Measured, not assumed; pinned by RawReaderTests.
        static readonly IReadOnlyDictionary<string, string> NoHeader = new Dictionary<string, string>();

        // The same extensions as in flatcheck.py. Only the NEF is tested on real data; the rest
        // work as well as libraw knows them, which is also what rawpy does.
        static readonly string[] Extensions =
        { ".nef", ".cr2", ".cr3", ".arw", ".dng", ".raf" };

        /// <summary>
        /// The extensions recognised as camera raws, exposed because not only the reader needs
        /// them: the WPF application filters the list of flats/ by them, and a second copy of
        /// the list would mean the next camera added here does not appear there.
        /// </summary>
        internal static IReadOnlyList<string> RawExtensions => Extensions;

        /// <summary>The dispatch gate: decided ONLY on the extension, as in flatcheck.py.</summary>
        /// <param name="path">The file's path; null or empty allowed.</param>
        /// <returns>True for one of the six raw extensions, any case.</returns>
        internal static bool IsRawPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(Extensions, ext) >= 0;
        }

        /// <inheritdoc/>
        public bool Handles(string path) => IsRawPath(path);

        /// <summary>
        /// Decodes the raw: the visible mosaic in ADU (no development), the exact scale from
        /// libraw, the CFA order, the best embedded preview and the shooting metadata. Throws
        /// FileNotFoundException for a missing file; InvalidOperationException when libraw cannot
        /// be loaded, or gives no 16-bit Bayer mosaic, invalid dimensions or an impossible scale;
        /// and, unwrapped, whatever libraw throws on a file it cannot open or unpack (a file
        /// that is not a raw at all).
        /// </summary>
        /// <param name="path">The raw file to read.</param>
        /// <returns>The frame, its image filled and its header empty.</returns>
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
        /// The metadata only: libraw fills the focal length, aperture, camera and LensInfo at
        /// OpenFile, before Unpack, so the file is opened and those are read. No pixels, no
        /// preview, no scale - Image null, Header empty. Measured on a D3300: tens of
        /// milliseconds instead of seconds, and not a hundredth of a gigabyte. The same
        /// lazy-loading guards as Load.
        /// </summary>
        /// <param name="path">The raw file to read.</param>
        /// <returns>The frame's metadata, its image null.</returns>
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

        /// <summary>
        /// Opens the file in libraw and reads the shooting metadata, without Unpack.
        /// </summary>
        /// <param name="path">The raw file, known to exist.</param>
        /// <returns>The frame's metadata, its image null.</returns>
        static LoadedFrame ReadInfo(string path)
        {
            using RawContext ctx = RawContext.OpenFile(path);
            LibRawData d = Marshal.PtrToStructure<LibRawData>(ctx.UnsafeGetHandle());
            var (focalMm, fNumber, cameraName, nikonLensId, lensMin, lensMax, lensAp) = ShootingInfo(ctx, d.RawData.IParams);
            return new LoadedFrame
            {
                Image = null,
                Header = NoHeader,
                FocalMm = focalMm,
                FNumber = fNumber,
                Camera = cameraName,
                NikonLensId = nikonLensId,
                LensMinFocalMm = lensMin,
                LensMaxFocalMm = lensMax,
                LensMaxAperture = lensAp,
            };
        }

        /// <summary>
        /// The error that says libraw's native DLLs are missing beside the executable, and how
        /// to publish so that they are not.
        /// </summary>
        /// <param name="inner">The loader's own exception, kept as the inner one.</param>
        /// <returns>An InvalidOperationException with a Romanian message for the user.</returns>
        static Exception Missing(Exception inner)
            => new InvalidOperationException(
                "nu pot incarca libraw, deci nu pot citi fisiere raw de aparat. DLL-urile "
              + "native (raw_r.dll si vecinii) trebuie sa stea langa flatcheck.exe. Modurile "
              + "pe FITS merg si fara ele; pentru raw, publica cu IncludeNativeLibrariesForSelfExtract "
              + "sau copiaza tot folderul, nu doar executabilul. (" + inner.Message + ")", inner);

        /// <summary>
        /// The full decode behind <see cref="Load"/>: Unpack, the visible mosaic, the scale, the
        /// preview and the metadata, all read while the file is open once.
        /// </summary>
        /// <param name="path">The raw file, known to exist.</param>
        /// <returns>The frame, its image in ADU.</returns>
        static LoadedFrame Decode(string path)
        {
            using RawContext ctx = RawContext.OpenFile(path);
            ctx.Unpack();

            LibRawData d = Marshal.PtrToStructure<LibRawData>(ctx.UnsafeGetHandle());
            LibRawImageSizes s = d.RawData.Sizes;
            IntPtr src = d.RawData.RawImage;

            // Foveon and some formats do not produce a 16-bit mosaic, but colour planes
            // directly. --expo has nothing to do with them, so they are refused openly.
            if (src == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"{path}: libraw n-a produs un mozaic Bayer pe 16 biti pentru fisierul asta "
                  + "(senzor non-Bayer sau format neacoperit).");

            int h = s.Height, w = s.Width;
            if (h <= 0 || w <= 0)
                throw new InvalidOperationException($"{path}: dimensiuni raw invalide ({w}x{h}).");

            // The colour order, evaluated at the origin of the VISIBLE area - the same margins
            // the copy below skips. Locals only, no field: a field of a libraw type would force
            // the assembly to load on FITS runs too (see the note at the top and
            // RawReaderTests.HoldsNoLibrawTypedFields).
            LibRawImageParams ip = d.RawData.IParams;
            CfaPattern cfa = CfaPattern.FromLibRaw(ip.Filters, ip.Cdesc, s.TopMargin, s.LeftMargin);

            // The VISIBLE area, like raw_image_visible of rawpy: the masked margins (the
            // calibration pixels under the sensor's cover) are skipped, otherwise the medians
            // would be pulled down by a black band the camera does not consider image.
            int stride = s.RawPitch > 0 ? (int)(s.RawPitch / 2) : s.RawWidth;
            var img = new Img(h, w);
            var row = new short[w];
            for (int y = 0; y < h; y++)
            {
                long offset = ((long)(y + s.TopMargin) * stride + s.LeftMargin) * sizeof(ushort);
                Marshal.Copy(src + (nint)offset, row, 0, w);
                int o = y * w;
                // libraw gives ushort, Marshal.Copy knows only short - so it is read as short
                // and reinterpreted. Without the cast, a pixel above 32767 would come out negative.
                for (int x = 0; x < w; x++) img.D[o + x] = (ushort)row[x];
            }

            LibRawColorData c = d.RawData.Color;
            double white = c.Maximum;

            // The black level can be given globally (color.black) or per channel
            // (color.cblack), and libraw splits them as it likes: on Nikon NEFs it moves the
            // common part into black and leaves cblack at zero. The sum is what rawpy reads too,
            // and flatcheck.py takes the median of the four channels - so here as well.
            var blacks = new List<double>(4);
            for (int i = 0; i < 4; i++)
                blacks.Add(c.Black + (c.CBlack != null && i < c.CBlack.Length ? c.CBlack[i] : 0));
            double black = Stats.Percentile(blacks, 50);

            // If the scale read is degenerate, better an error than an inverted scale: a
            // negative rng would give absurd percentages, and the sanity guard would dress them
            // up as "the file does not look like a raw flat", which would send the person down
            // the wrong track.
            if (!(white > black))
                throw new InvalidOperationException(
                    $"{path}: libraw da o scala imposibila (white {white:F0} <= black {black:F0}). "
                  + "Foloseste --sat si --black daca stii valorile corecte.");

            // The camera's preview, chosen from the METADATA (the thumbnail list is already
            // marshalled in d) and only it unpacked. It is done here, while the file is open,
            // so that it is not opened once more for a few MB of bytes.
            EmbeddedPreview preview = TryCapturePreview(ctx, d.ThumbnailList);

            // What the raw knows about the shot, for the synthetic flat. On a D3300 with a lens
            // of another make libraw does not give the lens's NAME, but it gives the Nikon ID
            // (139 for the Tokina 11-20), which the Adobe profiles carry in AlternateLensIDs.
            var (focalMm, fNumber, cameraName, nikonLensId, lensMin, lensMax, lensAp) = ShootingInfo(ctx, ip);

            return new LoadedFrame
            {
                Image = img,
                // An empty header, not an absent one: the --light suitability guard reads it as
                // "unknown" and runs with a note, instead of refusing.
                Header = NoHeader,
                Scale = new ExposureScale(white, black, "libraw (exact)"),
                Cfa = cfa,
                Preview = preview,
                FocalMm = focalMm,
                FNumber = fNumber,
                Camera = cameraName,
                NikonLensId = nikonLensId,
                LensMinFocalMm = lensMin,
                LensMaxFocalMm = lensMax,
                LensMaxAperture = lensAp,
            };
        }

        /// <summary>
        /// The focal length, aperture, camera name, Nikon lens ID and the lens's range (minimum
        /// / maximum focal length, maximum aperture at the minimum focal length), each null when
        /// libraw gives them zero or empty. Locals only, as everywhere in this file.
        ///
        /// The range comes from LensInfo.MakerNotes, not from the top fields of LensInfo: on a
        /// D3300 with the Tokina 11-20, the top MinFocal / MaxFocal are 0, while MakerNotes gives
        /// 11, 20 and 2.8 (LensData from the Nikon MakerNote, decoded by libraw). Measured, not
        /// assumed.
        /// </summary>
        /// <param name="ctx">The open libraw context.</param>
        /// <param name="ip">The image parameters, for the make and model.</param>
        /// <returns>
        /// The focal length (mm), the f-number, the camera ("make model", the make once), the
        /// Nikon lens ID, and the lens's minimum and maximum focal length (mm) and maximum
        /// aperture; each null when unknown.
        /// </returns>
        static (double? FocalMm, double? FNumber, string Camera, long? NikonLensId, double? LensMinFocal, double? LensMaxFocal, double? LensMaxAperture)
            ShootingInfo(RawContext ctx, LibRawImageParams ip)
        {
            LibRawImageOtherParams other = ctx.ImageOtherParams;
            double? focal = other.FocalLength > 0 ? other.FocalLength : null;
            double? fnum = other.Aperture > 0 ? other.Aperture : null;
            string make = (ip.Make ?? "").Trim(), model = (ip.Model ?? "").Trim();
            string camera = model.Length == 0 ? null : model.StartsWith(make, StringComparison.OrdinalIgnoreCase) || make.Length == 0 ? model : make + " " + model;
            long? lensId = null;
            double? lensMin = null, lensMax = null, lensAp = null;
            try
            {
                long id = ctx.LensInfo.Nikon.LensIDNumber;
                if (id > 0) lensId = id;
                var notes = ctx.LensInfo.MakerNotes;
                if (notes.MinFocal > 0) lensMin = notes.MinFocal;
                if (notes.MaxFocal > 0) lensMax = notes.MaxFocal;
                if (notes.MaxAperture4MinFocal > 0) lensAp = notes.MaxAperture4MinFocal;
            }
            catch (Exception) { lensId = null; lensMin = lensMax = lensAp = null; }
            return (focal, fnum, camera, lensId, lensMin, lensMax, lensAp);
        }

        /// <summary>
        /// The long side above which a preview is no longer "medium-sized". On a D3300 it picks
        /// the 1620x1080 entry instead of the 6000x4000 one: the same medians within 0.4 points,
        /// at a twelfth of the time and a thirteenth of the memory.
        /// </summary>
        internal const int PreferredMaxLongSide = 3000;

        /// <summary>
        /// The most suitable preview in the file, or null. The rule, in order: the largest JPEG
        /// with a long side under <see cref="PreferredMaxLongSide"/>; otherwise the largest JPEG
        /// of any size (PreviewReader will scale it at decoding); otherwise a bitmap already
        /// decoded by libraw, however small.
        ///
        /// The choice is made from the metadata list - ThumbCount entries, each with format and
        /// dimensions - so at no cost for what is not chosen. The indices are NOT stable between
        /// cameras, which is why they are enumerated and not assumed.
        ///
        /// It never throws: Load worked for months without a preview, and an odd thumbnail on
        /// some camera may not break it now. Its absence only leaves the window's switch off.
        /// </summary>
        /// <param name="ctx">The open, unpacked libraw context.</param>
        /// <param name="list">The thumbnail list libraw read.</param>
        /// <returns>The preview's bytes, or null.</returns>
        static EmbeddedPreview TryCapturePreview(RawContext ctx, LibRawThumbnailList list)
        {
            try
            {
                int count = Math.Min(list.ThumbCount, list.ThumbList?.Length ?? 0);
                int bestMid = -1, bestAny = -1, bestOther = -1;
                long areaMid = 0, areaAny = 0, areaOther = 0;

                for (int i = 0; i < count; i++)
                {
                    LibRawThumbnailItem t = list.ThumbList[i];
                    long area = (long)t.Width * t.Height;
                    if (t.Width == 0 || t.Height == 0) continue;

                    if (t.Format == InternalThumbnailFormat.Jpeg)
                    {
                        if (Math.Max(t.Width, t.Height) <= PreferredMaxLongSide && area > areaMid) { bestMid = i; areaMid = area; }
                        if (area > areaAny) { bestAny = i; areaAny = area; }
                    }
                    else if (area > areaOther) { bestOther = i; areaOther = area; }
                }

                // Rules 1 and 2: a JPEG, medium if possible.
                int jpegIdx = bestMid >= 0 ? bestMid : bestAny;
                if (jpegIdx >= 0)
                {
                    EmbeddedPreview j = Export(ctx, jpegIdx, list.ThumbList[jpegIdx].Flip);
                    if (j != null) return j;
                }

                // Rule 3: what is left, if libraw gives it as a decoded bitmap.
                return bestOther >= 0 ? Export(ctx, bestOther, list.ThumbList[bestOther].Flip) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Unpacks a single entry and copies its bytes. ProcessedImage wraps native memory and
        /// is IDisposable: the copy is made inside the using and no span leaves it.
        /// </summary>
        /// <param name="ctx">The open libraw context.</param>
        /// <param name="index">The thumbnail's index in libraw's list.</param>
        /// <param name="flip">libraw's orientation code for the entry, kept with the bytes.</param>
        /// <returns>
        /// The JPEG stream or the 8-bit RGB bitmap, or null for an empty entry or any other
        /// layout.
        /// </returns>
        static EmbeddedPreview Export(RawContext ctx, int index, int flip)
        {
            ctx.UnpackThumbnail(index);
            using ProcessedImage t = ctx.ExportThumbnail(index);
            if (t.Width <= 0 || t.Height <= 0 || t.DataSize <= 0) return null;

            if (t.ImageType == ProcessedImageType.Jpeg)
            {
                // On JPEG entries libraw reports Bits = 0 (measured), so the depth is not
                // checked here - the bytes are the compressed stream, not pixels.
                byte[] data = t.AsSpan<byte>().ToArray();
                return new EmbeddedPreview(true, data, t.Width, t.Height, flip,
                    $"previzualizare JPEG {t.Width}x{t.Height} din fisier");
            }

            if (t.ImageType == ProcessedImageType.Bitmap && t.Bits == 8 && t.Channels == 3
                && t.DataSize == t.Width * t.Height * 3)
            {
                byte[] data = t.AsSpan<byte>().ToArray();
                return new EmbeddedPreview(false, data, t.Width, t.Height, flip,
                    $"miniatura RGB {t.Width}x{t.Height} din fisier");
            }

            // Anything else (another bit depth, another number of channels) is not reinterpreted.
            return null;
        }
    }
}
