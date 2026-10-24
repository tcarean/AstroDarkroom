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

// PreviewReader.cs - decoding the JPEG preview, through ImageSharp.
//
// The ONLY file that names a type from SixLabors.ImageSharp, exactly as RawReader.cs is the only
// one that names one from Sdcb.LibRaw. The same isolation, for the same reason - and here it is
// also the swap point should the package's licence ever move: the measured alternative
// (StbImageSharp, Unlicense/MIT) gave identical medians on the same previews, so the replacement
// would be one file. See THIRD-PARTY-NOTICES.md.
//
// Four things the decoding has to get right, checked in ImageSharp's source, not in its
// documentation:
//   - The embedded ICC profile is NOT applied to the pixels. A converted preview is no longer
//     what was on the screen, and the difference would show in no test. On 3.1.x this is the
//     only possible behaviour - the decoder keeps the profile as metadata and that is all. If
//     the package is ever raised to 4.x, there DecoderOptions.ColorProfileHandling exists with
//     a Convert member, and it must be set EXPLICITLY to Preserve. (4.x also brings an MSBuild
//     task that asks for a licence key on Release; see FlatCheck.Core.csproj.)
//   - TargetSize scales during the IDCT, not after. It is used only when the file offers only a
//     large preview (see RawReader.TryCapturePreview); the medium-sized one is decoded at its
//     native size, so that no resampling touches the pixels.
//   - It decodes into Rgb24, so that a CMYK/YCCK JPEG still arrives as RGB.
//   - Any failure returns null. A preview that does not decode is no reason for the run to fail
//     - it only means the window's switch stays off.
//
// Measured on a D3300, with ImageSharp 4.1.1, the preview of DSC_0091.NEF:
//   1620x1080, native            16 ms,  5 MB allocated
//   6000x4000, native           460 ms, 69 MB
//   6000x4000, TargetSize 2000  485 ms,  9 MB
// So TargetSize cuts the MEMORY, not the time - the entropy is decoded anyway, only the IDCT
// comes out smaller. What makes the decoding cheap is the choice of the medium preview, in
// RawReader; TargetSize is the safety net for the cameras that have only a large one.

using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace FlatCheck
{
    /// <summary>Turns an <see cref="EmbeddedPreview"/> into pixels, or into null.</summary>
    internal static class PreviewReader
    {
        // CAREFUL: no field of an ImageSharp type here, for the same reason as in RawReader -
        // the fields are resolved when the type loads, the method bodies only at JIT. Pinned by
        // PreviewReaderTests.HoldsNoImageSharpTypedFields.

        /// <summary>
        /// The long side a preview that is too large is brought to. Above it, time and memory
        /// are paid for pixels a histogram cannot tell apart.
        /// </summary>
        internal const int TargetLongSide = 2000;

        /// <summary>
        /// The preview as pixels. A bitmap passes straight through; a JPEG goes through
        /// ImageSharp. Null on any failure, including when the ImageSharp assembly is missing
        /// beside the executable - then it degrades to "no preview", the tab does not crash.
        /// </summary>
        /// <param name="p">The captured preview; null allowed.</param>
        /// <returns>The pixels, or null.</returns>
        internal static PreviewImage Decode(EmbeddedPreview p)
        {
            if (p == null || p.Data == null || p.Data.Length == 0) return null;

            if (!p.IsJpeg)
            {
                if (p.W <= 0 || p.H <= 0 || p.Data.Length != p.W * p.H * 3) return null;
                return new PreviewImage(p.H, p.W, p.Data, p.Source);
            }

            try
            {
                return DecodeJpeg(p);
            }
            catch (ImageFormatException) { return null; }
            catch (DllNotFoundException) { return null; }
            catch (FileNotFoundException) { return null; }
            catch (BadImageFormatException) { return null; }
            catch (TypeInitializationException) { return null; }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// Decodes a JPEG preview into RGB, shrunk to about <see cref="TargetLongSide"/> only
        /// when its long side is over 1.5 times that. Throws on a corrupt stream;
        /// <see cref="Decode"/> catches it.
        /// </summary>
        /// <param name="p">A JPEG preview with bytes.</param>
        /// <returns>The pixels.</returns>
        static PreviewImage DecodeJpeg(EmbeddedPreview p)
        {
            // Only when it really is too large: on a medium-sized preview nothing is resampled.
            // The 20% window and the medians need no more than ~2000 px on the long side.
            Size? target = null;
            int longSide = Math.Max(p.W, p.H);
            if (longSide > TargetLongSide * 3 / 2 && p.W > 0 && p.H > 0)
            {
                double k = TargetLongSide / (double)longSide;
                target = new Size(Math.Max(1, (int)(p.W * k)), Math.Max(1, (int)(p.H * k)));
            }

            // SkipMetadata: EXIF and ICC are not read at all - we do not need them, and this way
            // there is not even the temptation (or, on 4.x, the option) to apply the profile to
            // the pixels.
            var options = new DecoderOptions
            {
                SkipMetadata = true,
                TargetSize = target,
            };

            using Image<Rgb24> img = Image.Load<Rgb24>(options, p.Data);
            var rgb = new byte[img.Width * img.Height * 3];
            img.CopyPixelDataTo(rgb);
            return new PreviewImage(img.Height, img.Width, rgb, p.Source);
        }
    }
}
