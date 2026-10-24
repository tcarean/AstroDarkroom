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

// HistEqRun.cs - mode 5 end to end: file -> equalised rasters + text + PNG.
//
// Png is the composite, the only view the console writes. The others - one grey per position
// and the mosaic - are encoded by the window on demand, through the two functions below, and
// cached per chip: they are up to five images of 1500x1000, and encoding them all in advance is
// exactly the problem the map cache exists for.
// RunOutcome.SecondaryPng stays null here.

using System.IO;

namespace FlatCheck
{
    /// <summary>
    /// Mode 5, <c>--histeq</c>: loads a frame, equalises it as Siril's Histogram display does
    /// and writes the report; a picture, no verdict.
    /// </summary>
    internal static class HistEqRun
    {
        /// <summary>
        /// Loads the file, equalises it, writes the report and encodes the composite. Throws
        /// what <see cref="ImageLoader.Load"/> throws on an unreadable file.
        /// </summary>
        /// <param name="path">A raw or a FITS file.</param>
        /// <param name="report">Where the report's text is written.</param>
        /// <returns>The equalised rasters and the composite as PNG; no second map.</returns>
        internal static RunOutcome<HistEqResult> Execute(string path, TextWriter report)
        {
            LoadedFrame f = ImageLoader.Load(path);
            var r = HistEqAnalyzer.Analyze(f.Image, f.Cfa);
            HistEqReport.Write(r, path, report);
            return new RunOutcome<HistEqResult>(r, EncodeComposite(r));
        }

        /// <summary>Encodes the composite view, RGB on a CFA frame of known order.</summary>
        /// <param name="r">An equalisation result.</param>
        /// <returns>The composite as PNG bytes, at the shrunk size.</returns>
        internal static byte[] EncodeComposite(HistEqResult r)
            => PngWriter.Encode(r.CompositeRgb, r.W, r.H);

        /// <summary>Encodes one channel's equalised grey view.</summary>
        /// <param name="r">An equalisation result.</param>
        /// <param name="i">The channel index, 0 to <c>ChannelCount - 1</c> (one per Bayer
        /// position on a CFA frame, 0 alone on mono).</param>
        /// <returns>The channel as grey PNG bytes, at the shrunk size.</returns>
        internal static byte[] EncodeChannel(HistEqResult r, int i)
            => PngWriter.EncodeGrey(r.Channels[i].Equalised, r.W, r.H);

        /// <summary>
        /// Encodes the mosaic equalised as a single layer. Null on mono: there is no mosaic
        /// apart from the channel there.
        /// </summary>
        /// <param name="r">An equalisation result.</param>
        /// <returns>The mosaic as grey PNG bytes, or null on a mono frame.</returns>
        internal static byte[] EncodeMosaic(HistEqResult r)
            => r.Mosaic == null ? null : PngWriter.EncodeGrey(r.Mosaic, r.W, r.H);
    }
}
