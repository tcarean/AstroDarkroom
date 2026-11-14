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

// CropRun.cs - what the Crop with aspect ratio tab asks of the core beside the rectangle itself
// (FrameCrop): the reduced copy it shows, the kind of file a crop of this input is written as,
// and the save (R-181 to R-183, ADR 0093).
//
// The crop is the tool's third export and the only one that changes no sample: the file
// written is of the input's own kind and depth. A FITS is cropped on its bytes (FitsWriter.
// SaveCrop); a TIFF or a PNG from the analysed raster, which holds every 8- and 16-bit level
// exactly (R-013); a JPEG becomes an 8-bit TIFF, since a JPEG written again is compressed
// again. Like the two other exports it runs under CurveExport.Guarded: the input is never
// opened for writing.

using System;
using NLog;

namespace AstroPhotoAnalyzer
{
    /// <summary>The kind of file a crop is written as.</summary>
    internal enum CropFileKind
    {
        Fits,
        Tiff,
        Png,
    }

    /// <summary>What the crop of an input is written as, or why it cannot be (R-181).</summary>
    internal sealed record CropOutput
    {
        internal CropFileKind Kind { get; init; }

        /// <summary>The bits per sample of the file written: the input's, for the kinds the crop keeps.</summary>
        internal int Bits { get; init; }

        /// <summary>A FITS of real numbers (a negative BITPIX).</summary>
        internal bool IsFloat { get; init; }

        /// <summary>
        /// The extension the proposed name takes instead of the input's, with its point; null
        /// when the input's own is right.
        /// </summary>
        internal string Extension { get; init; }

        /// <summary>The input is a JPEG, written as a TIFF.</summary>
        internal bool FromJpeg { get; init; }

        /// <summary>Why this input cannot be cropped without loss; null when it can.</summary>
        internal string Problem { get; init; }
    }

    /// <summary>The reduced copy the Crop with aspect ratio tab shows (R-183).</summary>
    internal static class CropRun
    {
        /// <summary>
        /// The image reduced for the tab's area by the galleries' rule
        /// (<see cref="CurveRun.PreviewStride"/>): every stride-th pixel, at least as large as
        /// the area shows it, never over <see cref="Thresholds.CurvePreviewMaxPixels"/>.
        /// </summary>
        /// <param name="a">The analysis, with its raster.</param>
        /// <param name="maxWidth">The area's width, in screen pixels.</param>
        /// <param name="maxHeight">The area's height, in screen pixels.</param>
        /// <returns>The reduced raster; the image itself when it is smaller than the area.</returns>
        internal static Raster Preview(AnalysisResult a, int maxWidth, int maxHeight)
            => RasterOps.Subsample(a.Raster, CurveRun.PreviewStride(a.Raster, maxWidth, maxHeight));
    }

    /// <summary>Saves the crop of the analysed image as a new file of the input's kind (R-181).</summary>
    internal static class CropExport
    {
        /// <summary>The diagnostic log: every file written, with its size and kind (R-171, ADR 0069).</summary>
        static readonly Logger Log = LogManager.GetLogger("CropExport");

        /// <summary>
        /// The kind and depth the crop of an input is written as: a FITS as a FITS of its own
        /// BITPIX; a TIFF of 8 or 16 bits as a TIFF of that depth; a PNG as a PNG of 16 bits
        /// when it has 16 and of 8 otherwise; a JPEG as an 8-bit TIFF. A TIFF of another depth,
        /// which the reader only gets converted, and a file of another format have a
        /// <see cref="CropOutput.Problem"/>.
        /// </summary>
        /// <param name="s">
        /// What the reader said of the input; null for a raster from memory, which is written
        /// as a fresh 32-bit float FITS.
        /// </param>
        /// <returns>The output, or the problem.</returns>
        internal static CropOutput OutputOf(SourceInfo s)
        {
            if (s == null) return new CropOutput { Kind = CropFileKind.Fits, Bits = 32, IsFloat = true, Extension = ".fit" };
            switch (s.Format)
            {
                case "FITS":
                    return new CropOutput { Kind = CropFileKind.Fits, Bits = s.BitDepth, IsFloat = s.IsFloat, Extension = ExtensionFor(s.Path, ".fit", ".fits", ".fts") };
                case "TIFF":
                    return s.BitDepth == 8 || s.BitDepth == 16
                        ? new CropOutput { Kind = CropFileKind.Tiff, Bits = s.BitDepth, Extension = ExtensionFor(s.Path, ".tif", ".tiff") }
                        : new CropOutput { Kind = CropFileKind.Tiff, Bits = s.BitDepth, Problem = CropTexts.TiffDepthRefused(s.BitDepth) };
                case "PNG":
                    return new CropOutput { Kind = CropFileKind.Png, Bits = s.BitDepth == 16 ? 16 : 8, Extension = ExtensionFor(s.Path, ".png") };
                case "JPEG":
                    return new CropOutput { Kind = CropFileKind.Tiff, Bits = 8, Extension = ".tif", FromJpeg = true };
                default:
                    return new CropOutput { Kind = CropFileKind.Tiff, Bits = 8, Problem = CropTexts.FormatRefused(s.Format) };
            }
        }

        /// <summary>
        /// The extension the proposed name must take for a kind of file: none when the input's
        /// own is one of the kind's, otherwise the kind's first. A file whose name says one
        /// format and whose content is another (a PNG named .tif, which the reader notes) is
        /// written as its content, so its crop is named after the content too.
        /// </summary>
        /// <param name="inputPath">The input's path; null when it is not known, which keeps the input's extension.</param>
        /// <param name="own">The extensions of the kind written, with their points, the one to propose first.</param>
        /// <returns>Null to keep the input's extension, or the extension to propose.</returns>
        static string ExtensionFor(string inputPath, params string[] own)
        {
            if (inputPath == null) return null;
            string ext = System.IO.Path.GetExtension(inputPath);
            foreach (string e in own)
                if (ext.Equals(e, StringComparison.OrdinalIgnoreCase)) return null;
            return own[0];
        }

        /// <summary>
        /// Writes to <paramref name="targetPath"/> the rectangle of the analysed image as a new
        /// file of the kind <see cref="OutputOf"/> gives, whatever the target's extension, under
        /// the guards of <see cref="CurveExport.Guarded"/>: the input is never opened for
        /// writing. Throws, with the sentence for the user, when the input cannot be cropped
        /// without loss or a guard refuses; a rectangle that is not inside the image is the
        /// caller's mistake and throws an <see cref="ArgumentException"/> before any write.
        /// </summary>
        /// <param name="a">The analysis whose image is cropped.</param>
        /// <param name="rect">The rectangle to keep, in the raster's pixels.</param>
        /// <param name="frameW">The frame's width, in cm, for the file's own notes.</param>
        /// <param name="frameH">The frame's height, in cm, for the file's own notes.</param>
        /// <param name="targetPath">The file to write, relative or full; replaced when it exists.</param>
        internal static void Save(AnalysisResult a, CropRect rect, double frameW, double frameH, string targetPath)
        {
            Raster r = a.Raster;
            if (!rect.FitsIn(r.W, r.H)) throw new ArgumentException("the crop rectangle " + rect + " is not inside the image " + r.W + " x " + r.H, nameof(rect));
            CropOutput kind = OutputOf(r.Source);
            if (kind.Problem != null) throw new Exception(kind.Problem);

            CurveExport.Guarded(a, targetPath, Log, (target, sourceFits) =>
            {
                string what = LogText.Int(rect.W) + "x" + LogText.Int(rect.H) + " at " + LogText.Int(rect.X) + "," + LogText.Int(rect.Y)
                    + " of " + LogText.Int(r.W) + "x" + LogText.Int(r.H) + " frame=" + LogText.Num(frameW) + "x" + LogText.Num(frameH) + " cm";
                switch (kind.Kind)
                {
                    case CropFileKind.Fits when sourceFits != null:
                        FitsWriter.SaveCrop(sourceFits, rect, r.W, r.H, r.Planes.Length, CropTexts.History(rect, r.W, r.H, frameW, frameH), target);
                        Log.Info("wrote " + target + " (" + LogText.FileMiB(target) + ") fits crop " + what + " bytes from=" + LogText.Str(sourceFits));
                        break;
                    case CropFileKind.Fits:
                        // A raster from memory: no file whose bytes could be copied.
                        var identity = new ToneCurve(new[] { new CurvePoint(0, 0, null), new CurvePoint(1, 1, null) });
                        FitsWriter.Save(RasterOps.Crop(r, rect), identity, null, CropTexts.History(rect, r.W, r.H, frameW, frameH), target);
                        Log.Info("wrote " + target + " (" + LogText.FileMiB(target) + ") fits crop " + what + " fresh 32-bit float");
                        break;
                    case CropFileKind.Tiff:
                        TiffWriter.Save(RasterOps.Crop(r, rect), r.Source?.Icc, CropTexts.Description(rect, frameW, frameH), target, kind.Bits);
                        Log.Info("wrote " + target + " (" + LogText.FileMiB(target) + ") tiff " + LogText.Int(kind.Bits) + "-bit crop " + what + " icc=" + LogText.Yes(r.Source?.Icc != null));
                        break;
                    default:
                        PngWriter.Save(RasterOps.Crop(r, rect), r.Source?.Icc, target, kind.Bits);
                        Log.Info("wrote " + target + " (" + LogText.FileMiB(target) + ") png " + LogText.Int(kind.Bits) + "-bit crop " + what + " icc=" + LogText.Yes(r.Source?.Icc != null));
                        break;
                }
            });
        }
    }
}
