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
// Png is the composite, the only view the console writes - or, with --rainbow, the one-layer
// view (the mosaic on CFA, the channel on mono) in Siril's false colours, shrunk first and
// coloured after. The others - one grey per position and the mosaic - are encoded by the
// window on demand, through the functions below, and cached per chip: they are up to five
// images of 1500x1000, and encoding them all in advance is exactly the problem the map cache
// exists for.

using System.IO;
using NLog;

namespace FlatCheck
{
    /// <summary>
    /// Mode 5, <c>--histeq</c>: loads a frame, equalises it as Siril's Histogram display does
    /// and writes the report; a picture, no verdict.
    /// </summary>
    internal static class HistEqRun
    {
        /// <summary>
        /// Loads the file, equalises it, writes the report and encodes the picture: the
        /// composite, or with <paramref name="rainbow"/> the one-layer view in false colours.
        /// Throws what <see cref="ImageLoader.Load"/> throws on an unreadable file.
        /// </summary>
        /// <param name="path">A raw or a FITS file.</param>
        /// <param name="report">Where the report's text is written.</param>
        /// <param name="rainbow">True for <c>--rainbow</c>: the picture is
        /// <see cref="EncodeRainbow"/> and the report carries the legend.</param>
        /// <returns>The equalised rasters and the picture as PNG; no second map.</returns>
        internal static RunOutcome<HistEqResult> Execute(string path, TextWriter report, bool rainbow = false)
        {
            using RunClock clock = RunClock.Start(Log, "histeq", path + " rainbow=" + LogText.Yes(rainbow));
            LoadedFrame f = ImageLoader.Load(path);
            clock.Lap("load");
            var r = HistEqAnalyzer.Analyze(f.Image, f.Cfa);
            clock.Lap("analyze");
            if (Log.IsDebugEnabled) Log.Debug("equalisation of " + path + ": " + LogText.HistEq(r));
            HistEqReport.Write(r, path, report, rainbow);
            clock.Lap("report");
            byte[] png = rainbow ? EncodeRainbow(r) : EncodeComposite(r);
            clock.Lap("map");
            clock.Done("no verdict, a picture");
            return new RunOutcome<HistEqResult>(r, png);
        }

        /// <summary>The diagnostic log: the run's lines and sizes (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(nameof(HistEqRun));

        /// <summary>
        /// Encodes the one-layer view in Siril's false colours: the mosaic on a CFA frame, the
        /// channel on mono, the SHRUNK raster coloured, so every pixel of the file is a level of
        /// <see cref="RainbowLut.Table"/> (ADR 0063).
        /// </summary>
        /// <param name="r">An equalisation result.</param>
        /// <returns>The coloured view as RGB PNG bytes, at the shrunk size.</returns>
        internal static byte[] EncodeRainbow(HistEqResult r)
        {
            var (grey, w, h) = r.OneLayer(full: false);
            return PngWriter.Encode(RainbowLut.Colour(grey), w, h);
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
