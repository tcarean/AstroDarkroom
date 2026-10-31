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
// The code is LightsCheck's adaptation; the comments are DarksCheck's own (ADR 0019). The
// messages are English-or-Romanian T(...) pairs written here, LightsCheck's Romanian wording
// kept byte for byte beside its English; LightsCheck takes these English wordings back in its
// own language task (ADR 0021).

// RawReader.cs - camera raw files (NEF and its relatives) through libraw: the one place in
// the tool that touches an external library. Adapted from FlatCheck's reader (ADR 0003): the
// embedded preview is gone, the shot metadata the inventory needs is added (exposure, ISO,
// time).
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
//
// The diagnostic log (ADR 0025) gets libraw's own figures at Debug and the lens block that
// could not be read at Warn; the file's line is ImageLoader's, a failure's line its caller's.

using System.Runtime.InteropServices;
using NLog;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;
using static DarksCheck.Core.Ui;

namespace DarksCheck.Core
{
    /// <summary>
    /// Reads a camera raw through libraw: the visible CFA mosaic in ADU, the exact white and black
    /// levels, the CFA order and the shot metadata.
    /// </summary>
    internal sealed class RawReader : IFrameReader
    {
        // No field of this class may be of a Sdcb.LibRaw type: fields are resolved when the
        // type is loaded, and the type is loaded on every run, FITS included; method bodies
        // only at JIT time. Measured in FlatCheck, pinned by RawReaderTests.
        static readonly IReadOnlyDictionary<string, string> NoHeader = new Dictionary<string, string>();

        /// <summary>The diagnostic log: libraw's figures of a decode, the lens block that failed (R-101).</summary>
        static readonly Logger Log = LogManager.GetLogger(nameof(RawReader));

        // FlatCheck's six extensions plus .pef (Pentax; FlatCheck's R-108, 2026-10-26: a K-70
        // sample read fine through libraw and was refused by this list alone). Only the NEF is
        // run on real data here; the rest work as well as libraw knows them.
        static readonly string[] Extensions =
        { ".nef", ".cr2", ".cr3", ".arw", ".dng", ".raf", ".pef" };

        /// <summary>
        /// The extensions recognised as camera raws; the session folder filters by them.
        /// </summary>
        internal static IReadOnlyList<string> RawExtensions => Extensions;

        /// <summary>The dispatch gate: decided on the extension alone.</summary>
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
        /// libraw, the CFA order and the shot metadata; the header is empty. Throws
        /// FileNotFoundException for a missing file; InvalidOperationException when libraw cannot
        /// be loaded, or gives no 16-bit Bayer mosaic, invalid dimensions or white not above black;
        /// and, unwrapped, whatever libraw throws on a file it cannot open or unpack.
        /// </summary>
        /// <param name="path">The raw file to read.</param>
        /// <returns>The frame, its image filled.</returns>
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
        /// The metadata only: libraw fills the shot fields at OpenFile, before Unpack, so the
        /// file is opened and those are read. No pixels, no scale: Image null, Header empty.
        /// Tens of milliseconds on a D3300 NEF instead of seconds.
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

        /// <summary>Opens the file in libraw and reads the shot metadata, without Unpack.</summary>
        /// <param name="path">The raw file, known to exist.</param>
        /// <returns>The frame's metadata, its image null and its header empty.</returns>
        static LoadedFrame ReadInfo(string path)
        {
            using RawContext ctx = RawContext.OpenFile(path);
            LibRawData d = Marshal.PtrToStructure<LibRawData>(ctx.UnsafeGetHandle());
            var f = new LoadedFrame { Image = null, Header = NoHeader };
            FillShootingInfo(f, ctx, d.RawData.IParams);
            return f;
        }

        /// <summary>
        /// The error that replaces a failure to load libraw's native DLLs: it says, in the run's
        /// language, where they must sit and how to publish them.
        /// </summary>
        /// <param name="inner">The loader's exception, kept as the inner one.</param>
        /// <returns>The exception to throw.</returns>
        static InvalidOperationException Missing(Exception inner)
            => new InvalidOperationException(T(
                "cannot load libraw, so camera raw files cannot be read. The native DLLs "
              + "(raw_r.dll and its neighbours) must sit beside darkscheck.exe. FITS frames "
              + "work without them; for raws, publish with IncludeNativeLibrariesForSelfExtract "
              + "or copy the whole folder, not just the executable. (" + inner.Message + ")",
                "nu pot incarca libraw, deci nu pot citi fisiere raw de aparat. DLL-urile "
              + "native (raw_r.dll si vecinii) trebuie sa stea langa darkscheck.exe. Cadrele "
              + "FITS merg si fara ele; pentru raw, publica cu IncludeNativeLibrariesForSelfExtract "
              + "sau copiaza tot folderul, nu doar executabilul. (" + inner.Message + ")"), inner);

        /// <summary>
        /// The full decode behind <see cref="Load"/>: Unpack, the visible mosaic, the scale, the
        /// CFA order and the metadata, all read while the file is open once.
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

            // Foveon and some formats produce colour planes, not a 16-bit mosaic: refused.
            if (src == IntPtr.Zero)
                throw new InvalidOperationException(T(
                    $"{path}: libraw did not produce a 16-bit Bayer mosaic for this file "
                  + "(a non-Bayer sensor or a format it does not cover).",
                    $"{path}: libraw n-a produs un mozaic Bayer pe 16 biti pentru fisierul asta "
                  + "(senzor non-Bayer sau format neacoperit)."));

            int h = s.Height, w = s.Width;
            if (h <= 0 || w <= 0)
                throw new InvalidOperationException(T($"{path}: invalid raw dimensions ({w}x{h}).", $"{path}: dimensiuni raw invalide ({w}x{h})."));

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

            // Before the scale is judged, so a refused scale leaves its figures.
            if (Log.IsDebugEnabled)
                Log.Debug(FiguresLine(path, w, h, s.RawWidth, s.RawHeight, s.TopMargin, s.LeftMargin, s.RawPitch,
                                      ip.Filters, ip.Cdesc, cfa, white, c.Black, blacks, black));

            if (!(white > black))
                throw new InvalidOperationException(T(
                    $"{path}: libraw gives an impossible scale (white {white:F0} <= black {black:F0}).",
                    $"{path}: libraw da o scala imposibila (white {white:F0} <= black {black:F0})."));

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
        /// libraw's figures of one decode, as the diagnostic log writes them at Debug: the visible
        /// and raw sizes, the margins, the pitch, the filters mask and <c>cdesc</c>, the CFA order
        /// evaluated at the visible origin, the maximum and the blacks. It takes plain values only,
        /// no libraw type, so the tests can call it and no closure of a libraw local is ever made
        /// (see the note at the top).
        /// </summary>
        /// <param name="path">The raw file.</param>
        /// <param name="w">The visible width, in pixels.</param>
        /// <param name="h">The visible height, in pixels.</param>
        /// <param name="rawWidth">The raw width, margins included, in pixels.</param>
        /// <param name="rawHeight">The raw height, margins included, in pixels.</param>
        /// <param name="topMargin">The first visible row.</param>
        /// <param name="leftMargin">The first visible column.</param>
        /// <param name="rawPitch">libraw's row pitch, in bytes; 0 when libraw gives none.</param>
        /// <param name="filters">libraw's <c>filters</c> mask.</param>
        /// <param name="cdesc">libraw's colour description; null allowed.</param>
        /// <param name="cfa">The CFA order; null for a raw without a Bayer mosaic (mono, Leaf, X-Trans).</param>
        /// <param name="maximum">libraw's <c>color.maximum</c>, the white, in ADU.</param>
        /// <param name="black0">libraw's global <c>color.black</c>, in ADU.</param>
        /// <param name="blacks">The four channels' blacks, global plus per channel, in ADU.</param>
        /// <param name="black">Their median, the black the frame is read with, in ADU.</param>
        /// <returns>The line's text.</returns>
        internal static string FiguresLine(string path, int w, int h, int rawWidth, int rawHeight, int topMargin, int leftMargin,
                                           long rawPitch, uint filters, string cdesc, CfaPattern cfa, double maximum, double black0,
                                           List<double> blacks, double black)
            => "libraw on " + path + ": visible " + LogText.Int(w) + "x" + LogText.Int(h)
             + " raw " + LogText.Int(rawWidth) + "x" + LogText.Int(rawHeight)
             + " margins top " + LogText.Int(topMargin) + " left " + LogText.Int(leftMargin)
             + " pitch " + LogText.Int(rawPitch) + " filters 0x" + filters.ToString("X8")
             + " cdesc " + LogText.Str(cdesc) + " cfa " + (cfa != null ? cfa.Letters : "none")
             + " maximum " + LogText.Num(maximum) + " black " + LogText.Num(black0)
             + " per channel " + LogText.List(blacks.ConvertAll(LogText.Num)) + " median " + LogText.Num(black);

        /// <summary>
        /// Fills <paramref name="f"/> with what libraw knows about the shot: focal length,
        /// f-number, camera name, exposure, ISO, time of capture, and the lens - its ID and mount
        /// (<see cref="LensIdOf"/>, <see cref="MountLabel"/>), its name (LensInfo.Lens, the EXIF
        /// LensModel: empty on a D3300, "EF50mm f/1.4 USM" on a Canon) and its range
        /// (<see cref="RangeOf"/>). Each stays null when libraw gives zero or empty. libraw types
        /// in locals only, as everywhere in this file; a failure in the lens block leaves the lens
        /// fields null and the rest as read. The lens block is FlatCheck's hunk of 2026-10-26
        /// (its R-108, ADR 0023).
        ///
        /// The time is libraw's time_t: EXIF DateTimeOriginal run through mktime on this
        /// machine, so converting it back through the local zone gives the wall-clock the camera
        /// wrote, to the second (EXIF SubSecTime is not exposed).
        /// </summary>
        /// <param name="f">The frame whose shot and lens fields are overwritten.</param>
        /// <param name="ctx">The open libraw context.</param>
        /// <param name="ip">libraw's image parameters, for the make and the model.</param>
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
                LibRawLensInfo li = ctx.LensInfo;
                LibRawLensMakerNotes notes = li.MakerNotes;
                // The lens's mount, and the camera's only when the lens's is unknown: the two agree
                // on every known body FlatCheck measured, and on a body newer than libraw (an EOS
                // R5 Mark II read by 0.21.1) CameraMount said Canon EF while LensMount said Canon
                // RF, rightly.
                int mount = MountOf(notes.LensMount, notes.CameraMount);
                f.LensId = LensIdOf(li.Nikon.LensIDNumber, notes.LensID, mount);
                f.LensMount = MountLabel(mount);
                string name = (li.Lens ?? "").Trim();
                f.LensName = name.Length == 0 ? null : name;
                (f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture) =
                    RangeOf(notes.MinFocal, notes.MaxFocal, notes.MaxAperture4MinFocal, notes.MaxAperture, li.MinFocal, li.MaxFocal, li.MaxAperture4MinFocal,
                            other.FocalLength);
            }
            catch (Exception e)
            {
                Log.Warn(e, "the raw's lens block could not be read; the lens stays unknown");
                f.LensId = null;
                f.LensMount = null;
                f.LensName = null;
                f.LensMinFocalMm = f.LensMaxFocalMm = f.LensMaxAperture = null;
            }
        }

        /// <summary>
        /// The lens ID a raw carries, whichever the mount (FlatCheck's R-108): Nikon's own
        /// <c>LensIDNumber</c> when it is above 0 (74 for a lens without CPU, 139 for the Tokina
        /// 11-20); otherwise the MakerNote's <c>LensID</c>, which libraw fills with the maker's
        /// lens type on Canon (198 = EF 50mm f/1.4 USM), Sony (32831 = FE 16-35mm F2.8 GM) and
        /// Pentax (its two bytes as a*256+b: 2263 = 8 215), taken only when the MakerNote names a
        /// mount that is not Nikon's and the value is above 0 and fits 31 bits. On a Nikon body
        /// that field holds the 8-byte LensData signature (5356031691206446338 on a D3300), never
        /// an ID, and with an unknown mount nothing is taken. FlatCheck measured it on 2026-10-26
        /// on a D3300 and on four raw.pixls.us samples.
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
        /// The camera's mount as libraw names it: "Nikon F", "Canon EF", "Sony E", "Pentax K" and
        /// the other mounts spelled out below; null for an unknown mount (0), for a mount this
        /// table does not spell and for a value libraw does not define - a programming name
        /// ("HasselbladXcd") is not a label.
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
        /// is unknown (0). The two agree on every known body FlatCheck measured; on a body newer
        /// than libraw (an EOS R5 Mark II read by 0.21.1) the camera's said Canon EF while the
        /// lens's said Canon RF, rightly (FlatCheck's R-108).
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
        /// such raw was measured, so it is not taken. FlatCheck measured it on 2026-10-26 (its
        /// R-108).
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
                && (Close(min.Value, max.Value) || (currentFocal > 0 && Close(currentFocal, min.Value)));
            double? ap = notesAp4Min > 0 ? notesAp4Min
                       : topAp4Min > 0 ? topAp4Min
                       : notesMaxAp > 0 && atShortEnd ? notesMaxAp
                       : null;
            return (min, max, ap);
        }

        /// <summary>
        /// Whether two focal lengths agree: FlatCheck's <c>LensSpec.Close</c> with its two
        /// tolerances, inlined because DarksCheck has no lens profiles (ADR 0023).
        /// </summary>
        /// <param name="a">One focal length, in mm.</param>
        /// <param name="b">The other, in mm.</param>
        /// <returns>True within 1 mm or 5 % of the larger, whichever is larger.</returns>
        static bool Close(double a, double b) => Math.Abs(a - b) <= Math.Max(1.0, 0.05 * Math.Max(a, b));
    }
}
