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
using NLog;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;
using static FlatCheck.Ui;

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

        /// <summary>The diagnostic log: what libraw gave, and the failures this reader
        /// recovers from (R-110). An NLog type, not a libraw one, so the rule above
        /// holds.</summary>
        static readonly Logger Log = LogManager.GetLogger(nameof(RawReader));

        // The six extensions of flatcheck.py, plus .pef (Pentax, 2026-10-26: a K-70 sample read
        // fine through libraw and was refused by this list alone, R-108). Only the NEF, CR2, CR3,
        // ARW and PEF are tested on real data (the masters and docs/validation.md V-041 to V-048);
        // the rest work as well as libraw knows them, which is also what rawpy does.
        static readonly string[] Extensions =
        { ".nef", ".cr2", ".cr3", ".arw", ".dng", ".raf", ".pef" };

        /// <summary>
        /// The extensions recognised as camera raws, exposed because not only the reader needs
        /// them: the WPF application filters the list of flats/ by them, and a second copy of
        /// the list would mean the next camera added here does not appear there.
        /// </summary>
        internal static IReadOnlyList<string> RawExtensions => Extensions;

        /// <summary>The dispatch gate: decided ONLY on the extension, as in flatcheck.py.</summary>
        /// <param name="path">The file's path; null or empty allowed.</param>
        /// <returns>True for one of the seven raw extensions, any case.</returns>
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
                throw new FileNotFoundException(T($"cannot find the file {path}", $"nu gasesc fisierul {path}"), path);

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
                throw new FileNotFoundException(T($"cannot find the file {path}", $"nu gasesc fisierul {path}"), path);

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
            var frame = new LoadedFrame { Image = null, Header = NoHeader };
            FillShootingInfo(ctx, d.RawData.IParams, frame);
            return frame;
        }

        /// <summary>
        /// The error that says libraw's native DLLs are missing beside the executable, and how
        /// to publish so that they are not.
        /// </summary>
        /// <param name="inner">The loader's own exception, kept as the inner one.</param>
        /// <returns>An InvalidOperationException with a message for the user.</returns>
        static Exception Missing(Exception inner)
            => new InvalidOperationException(T(
                "cannot load libraw, so camera raw files cannot be read. The native "
              + "DLLs (raw_r.dll and its neighbours) must sit beside flatcheck.exe. The FITS "
              + "modes work without them; for raw, publish with IncludeNativeLibrariesForSelfExtract "
              + "or copy the whole folder, not only the executable. (",
                "nu pot incarca libraw, deci nu pot citi fisiere raw de aparat. DLL-urile "
              + "native (raw_r.dll si vecinii) trebuie sa stea langa flatcheck.exe. Modurile "
              + "pe FITS merg si fara ele; pentru raw, publica cu IncludeNativeLibrariesForSelfExtract "
              + "sau copiaza tot folderul, nu doar executabilul. (") + inner.Message + ")", inner);

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
                throw new InvalidOperationException(T(
                    $"{path}: libraw did not produce a 16-bit Bayer mosaic for this file "
                  + "(a non-Bayer sensor or a format not covered).",
                    $"{path}: libraw n-a produs un mozaic Bayer pe 16 biti pentru fisierul asta "
                  + "(senzor non-Bayer sau format neacoperit)."));

            int h = s.Height, w = s.Width;
            if (h <= 0 || w <= 0)
                throw new InvalidOperationException(T($"{path}: invalid raw dimensions ({w}x{h}).",
                                                      $"{path}: dimensiuni raw invalide ({w}x{h})."));

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
                throw new InvalidOperationException(T(
                    $"{path}: libraw gives an impossible scale (white {white:F0} <= black {black:F0}). "
                  + "Use --sat and --black if you know the right values.",
                    $"{path}: libraw da o scala imposibila (white {white:F0} <= black {black:F0}). "
                  + "Foloseste --sat si --black daca stii valorile corecte."));

            // The camera's preview, chosen from the METADATA (the thumbnail list is already
            // marshalled in d) and only it unpacked. It is done here, while the file is open,
            // so that it is not opened once more for a few MB of bytes.
            EmbeddedPreview preview = TryCapturePreview(ctx, d.ThumbnailList);

            // What the raw knows about the shot, for the synthetic flat. On a D3300 with a lens
            // of another make libraw does not give the lens's NAME, but it gives the Nikon ID
            // (139 for the Tokina 11-20), which the Adobe profiles carry in AlternateLensIDs; a
            // Canon or a Sony gives the name too, a Pentax the maker's ID alone (R-108).
            var frame = new LoadedFrame
            {
                Image = img,
                // An empty header, not an absent one: the --light suitability guard reads it as
                // "unknown" and runs with a note, instead of refusing.
                Header = NoHeader,
                Scale = new ExposureScale(white, black, "libraw (exact)"),
                Cfa = cfa,
                Preview = preview,
            };
            FillShootingInfo(ctx, ip, frame);
            // Copied out before the lambda below: a lambda that captured c would move the libraw
            // struct into a compiler-generated class, a field of a libraw type by another name
            // (the rule at the top of this class, checked by HoldsNoLibrawTypedFields).
            double black0 = c.Black;
            if (Log.IsDebugEnabled)
                Log.Debug("libraw on " + path + ": visible " + LogText.Int(w) + "x" + LogText.Int(h)
                          + " of " + LogText.Int(s.RawWidth) + "x" + LogText.Int(s.RawHeight)
                          + " margins top=" + LogText.Int(s.TopMargin) + " left=" + LogText.Int(s.LeftMargin)
                          + " filters=0x" + ip.Filters.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)
                          + " cdesc=" + LogText.Str(ip.Cdesc) + " maximum=" + LogText.Num(white)
                          + " black=" + LogText.Num(black0) + " cblack=" + LogText.List(blacks.ConvertAll(b => LogText.Num(b - black0)))
                          + " thumbnails=" + LogText.Int(d.ThumbnailList.ThumbCount));
            return frame;
        }

        /// <summary>
        /// Fills <paramref name="frame"/> with what libraw knows about the shot: the focal length,
        /// the aperture, the camera ("make model", the make once) and the lens - its ID and mount
        /// (<see cref="LensIdOf"/>, <see cref="MountLabel"/>; on a Canon RF body libraw's ID is
        /// Canon's RF lens type, 266 for the RF 70-200 F2.8, which no Adobe file carries, so there
        /// the name is the key), its name (<c>LensInfo.Lens</c>, the EXIF LensModel: empty on a
        /// D3300 with third-party glass and on a Pentax K-70, "EF50mm f/1.4 USM" on a Canon, "FE
        /// 16-35mm F2.8 GM" on a Sony, "RF70-200mm F2.8 L IS USM" on an EOS R6) and its range
        /// (<see cref="RangeOf"/>). Each stays null when libraw gives zero or empty. libraw types
        /// in locals only, as everywhere in this file; a failure in the lens block leaves the lens
        /// fields null and the rest as read. Measured 2026-10-26 (R-108).
        /// </summary>
        /// <param name="ctx">The open libraw context.</param>
        /// <param name="ip">The image parameters, for the make and model.</param>
        /// <param name="frame">The frame to fill; its shot and lens fields are overwritten.</param>
        static void FillShootingInfo(RawContext ctx, LibRawImageParams ip, LoadedFrame frame)
        {
            LibRawImageOtherParams other = ctx.ImageOtherParams;
            frame.FocalMm = other.FocalLength > 0 ? other.FocalLength : null;
            frame.FNumber = other.Aperture > 0 ? other.Aperture : null;
            string make = (ip.Make ?? "").Trim(), model = (ip.Model ?? "").Trim();
            frame.Camera = model.Length == 0 ? null : model.StartsWith(make, StringComparison.OrdinalIgnoreCase) || make.Length == 0 ? model : make + " " + model;
            try
            {
                LibRawLensInfo li = ctx.LensInfo;
                LibRawLensMakerNotes notes = li.MakerNotes;
                // The lens's mount, and the camera's only when the lens's is unknown: the two agree
                // on every known body measured, and on a body newer than libraw (an EOS R5 Mark II
                // read by 0.21.1) CameraMount said Canon EF while LensMount said Canon RF, rightly.
                int mount = MountOf(notes.LensMount, notes.CameraMount);
                frame.LensId = LensIdOf(li.Nikon.LensIDNumber, notes.LensID, mount);
                frame.LensMount = MountLabel(mount);
                string name = (li.Lens ?? "").Trim();
                frame.LensName = name.Length == 0 ? null : name;
                (frame.LensMinFocalMm, frame.LensMaxFocalMm, frame.LensMaxAperture) =
                    RangeOf(notes.MinFocal, notes.MaxFocal, notes.MaxAperture4MinFocal, notes.MaxAperture, li.MinFocal, li.MaxFocal, li.MaxAperture4MinFocal,
                            other.FocalLength);
            }
            catch (Exception e)
            {
                Log.Warn(e, "the raw's lens block could not be read; the lens stays unknown");
                frame.LensId = null;
                frame.LensMount = null;
                frame.LensName = null;
                frame.LensMinFocalMm = frame.LensMaxFocalMm = frame.LensMaxAperture = null;
            }
        }

        /// <summary>
        /// The lens ID a raw carries, whichever the mount (R-108): Nikon's own <c>LensIDNumber</c>
        /// when it is above 0 (74 for a lens without CPU, 139 for the Tokina 11-20); otherwise the
        /// MakerNote's <c>LensID</c>, which libraw fills with the maker's lens type on Canon (198 =
        /// EF 50mm f/1.4 USM), Sony (32831 = FE 16-35mm F2.8 GM) and Pentax (its two bytes as
        /// a*256+b: 2263 = 8 215), taken only when the MakerNote names a mount that is not Nikon's
        /// and the value is above 0 and fits 31 bits. On a Nikon body that field holds the 8-byte
        /// LensData signature (5356031691206446338 on a D3300), never an ID, and with an unknown
        /// mount nothing is taken. Measured 2026-10-26 on a D3300 and on the four raw.pixls.us
        /// samples of docs/validation.md.
        /// </summary>
        /// <param name="nikonLensIdNumber">libraw's <c>LensInfo.Nikon.LensIDNumber</c>; 0 when none.</param>
        /// <param name="makerNoteLensId">libraw's <c>LensInfo.MakerNotes.LensID</c>; 0 when none.</param>
        /// <param name="mountCode">libraw's <c>LensInfo.MakerNotes.LensMount</c> (the camera's
        /// when the lens's is 0), a value of its camera-mounts enumeration; 0 when unknown.</param>
        /// <returns>The lens ID, or null when the raw gives none.</returns>
        internal static long? LensIdOf(long nikonLensIdNumber, ulong makerNoteLensId, int mountCode)
        {
            if (nikonLensIdNumber > 0) return nikonLensIdNumber;
            var mount = (CameraMount)mountCode;
            bool nikon = mount == CameraMount.NikonF || mount == CameraMount.NikonZ || mount == CameraMount.NikonCx;
            if (mount == CameraMount.Unknown || nikon) return null;
            return makerNoteLensId > 0 && makerNoteLensId <= int.MaxValue ? (long)makerNoteLensId : null;
        }

        /// <summary>
        /// The camera's mount as libraw names it, for the reports: "Nikon F", "Canon EF", "Sony E",
        /// "Pentax K" and the other mounts spelled out below; null for an unknown mount (0), for a
        /// mount this table does not spell and for a value libraw does not define - a programming
        /// name ("HasselbladXcd") is not a label, and the parenthesis then carries the range alone.
        /// </summary>
        /// <param name="mountCode">libraw's <c>LensInfo.MakerNotes.LensMount</c> (the camera's when
        /// the lens's is 0); 0 when unknown.</param>
        /// <returns>The mount's label, or null.</returns>
        internal static string MountLabel(int mountCode)
        {
            var mount = (CameraMount)mountCode;
            return mount switch
            {
                CameraMount.Unknown => null,
                CameraMount.NikonF => "Nikon F",
                CameraMount.NikonZ => "Nikon Z",
                CameraMount.NikonCx => "Nikon 1",
                CameraMount.CanonEf => "Canon EF",
                CameraMount.CanonEfS => "Canon EF-S",
                CameraMount.CanonEfM => "Canon EF-M",
                CameraMount.CanonRf => "Canon RF",
                CameraMount.SonyE => "Sony E",
                CameraMount.MinoltaA => "Sony A",
                CameraMount.PentaxK => "Pentax K",
                CameraMount.Pentax645 => "Pentax 645",
                CameraMount.PentaxQ => "Pentax Q",
                CameraMount.FujiX => "Fujifilm X",
                CameraMount.FujiGf => "Fujifilm G",
                CameraMount.Mft => "Micro 4/3",
                CameraMount.Ft => "Four Thirds",
                CameraMount.LeicaM => "Leica M",
                CameraMount.LeicaSl => "Leica L",
                CameraMount.SamsungNx => "Samsung NX",
                CameraMount.HasselbladXcd => "Hasselblad XCD",
                CameraMount.LeicaTl => "Leica TL",
                CameraMount.FujiGx => "Fujifilm GX",
                _ => null,
            };
        }

        /// <summary>
        /// The mount the lens fields are read for: the lens's, and the camera's only when the lens's
        /// is unknown (0). The two agree on every known body measured; on a body newer than libraw
        /// (an EOS R5 Mark II read by 0.21.1) the camera's said Canon EF while the lens's said
        /// Canon RF, rightly (R-108).
        /// </summary>
        /// <param name="lensMount">libraw's <c>LensInfo.MakerNotes.LensMount</c>; 0 when unknown.</param>
        /// <param name="cameraMount">libraw's <c>LensInfo.MakerNotes.CameraMount</c>; 0 when unknown.</param>
        /// <returns>The mount code to use; 0 when both are unknown.</returns>
        internal static int MountOf(int lensMount, int cameraMount) => lensMount != 0 ? lensMount : cameraMount;

        /// <summary>
        /// The lens's range from libraw, each part null when every source gives 0: the minimum and
        /// maximum focal length from the MakerNote, else from the top of <c>LensInfo</c> (a D3300
        /// fills the MakerNote only, a Canon or a Sony both, a Pentax K-70 neither); the maximum
        /// aperture from the MakerNote's <c>MaxAperture4MinFocal</c>, else the top field, else the
        /// MakerNote's <c>MaxAperture</c> - but that one only when it is the aperture at the short
        /// end: on a prime (a Canon writes 0 and 1.414 for the EF 50 f/1.4, 2.828 for the RF
        /// 70-200 f/2.8 at 200 mm) or when the shot's focal length is the lens's shortest. Canon's
        /// field is the maximum aperture at the CURRENT focal length, and on a variable-aperture
        /// zoom at its long end it would contradict every name that states the short end's; no
        /// such raw was measured, so it is not taken. Measured 2026-10-26 (R-108).
        /// </summary>
        /// <param name="notesMin">The MakerNote's minimum focal length, mm; 0 when none.</param>
        /// <param name="notesMax">The MakerNote's maximum focal length, mm; 0 when none.</param>
        /// <param name="notesAp4Min">The MakerNote's maximum aperture at the minimum focal length, an f-number; 0 when none.</param>
        /// <param name="notesMaxAp">The MakerNote's maximum aperture, an f-number; 0 when none.</param>
        /// <param name="topMin">The top-level minimum focal length, mm; 0 when none.</param>
        /// <param name="topMax">The top-level maximum focal length, mm; 0 when none.</param>
        /// <param name="topAp4Min">The top-level maximum aperture at the minimum focal length; 0 when none.</param>
        /// <param name="currentFocal">The shot's focal length, mm (libraw's <c>FocalLength</c>); 0 when unknown.</param>
        /// <returns>The minimum and maximum focal length (mm) and the maximum aperture, each null when unknown.</returns>
        internal static (double? Min, double? Max, double? Aperture) RangeOf(double notesMin, double notesMax, double notesAp4Min, double notesMaxAp,
                                                                           double topMin, double topMax, double topAp4Min, double currentFocal)
        {
            double? min = notesMin > 0 ? notesMin : topMin > 0 ? topMin : null;
            double? max = notesMax > 0 ? notesMax : topMax > 0 ? topMax : null;
            bool atShortEnd = min.HasValue && max.HasValue
                && (LensSpec.Close(min.Value, max.Value) || (currentFocal > 0 && LensSpec.Close(currentFocal, min.Value)));
            double? ap = notesAp4Min > 0 ? notesAp4Min
                       : topAp4Min > 0 ? topAp4Min
                       : notesMaxAp > 0 && atShortEnd ? notesMaxAp
                       : null;
            return (min, max, ap);
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
            catch (Exception e)
            {
                Log.Warn(e, "the raw's preview could not be captured; the frame goes on without one");
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
                    T($"JPEG preview {t.Width}x{t.Height} from the file", $"previzualizare JPEG {t.Width}x{t.Height} din fisier"));
            }

            if (t.ImageType == ProcessedImageType.Bitmap && t.Bits == 8 && t.Channels == 3
                && t.DataSize == t.Width * t.Height * 3)
            {
                byte[] data = t.AsSpan<byte>().ToArray();
                return new EmbeddedPreview(false, data, t.Width, t.Height, flip,
                    T($"RGB thumbnail {t.Width}x{t.Height} from the file", $"miniatura RGB {t.Width}x{t.Height} din fisier"));
            }

            // Anything else (another bit depth, another number of channels) is not reinterpreted.
            return null;
        }
    }
}
