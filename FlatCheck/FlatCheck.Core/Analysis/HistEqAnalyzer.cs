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

// HistEqAnalyzer.cs - mode 5: the frame seen through histogram equalisation, that is exactly
// what Siril 1.4.4 shows when you pick "Histogram" as the display mode.
//
// WHAT SIRIL DOES, read from the source (src/gui/image_display.c, remap(), the
// HISTEQ_DISPLAY branch; src/gui/histogram.c, computeHisto()):
//
//   - one histogram per LAYER, with 65536 bins, from which the pixels equal to zero are left out;
//   - LUT[i] = round_to_BYTE(cumulative(i) / pixel_count * 255), the cumulative including bin
//     i, LUT[0] = 0 and pixel_count = ALL the pixels, the zero ones included;
//   - the displayed pixel = LUT[value]; a float is first quantised with round(v * 65535);
//   - the lo/hi sliders are disabled in this mode.
//
// So it is not a graph: it is the image itself, passed through its own distribution function.
// Every grey level gets the same number of pixels. On a flat - a distribution narrow against
// the file's scale - the function is steep exactly over the vignetting's interval: measured on
// a synthetic one with 5 % vignetting, a ripple of 0.3 % is a third of a level in the linear
// display and ~15 levels equalised (ATinyRippleInvisibleInTheLinearViewBecomesManyGreyLevels).
// The price: the AMPLITUDE VANISHES by construction. A ripple of 0.5 % and
// a gradient of 10 % of the same shape come out the same, and a perfect flat shows its own
// noise at full contrast. The mode shows the SHAPE; the figures are in the structure mode. It
// never votes and has no threshold.
//
// WHAT DIFFERS from Siril, and why:
//
//   - The bins span [min, max] of the channel's non-zero pixels, not 0..65535
//     absolute: our rasters are doubles at whatever scale the file has (0..4095,
//     0..65535, 0..1). On integer data the mapping is identical - two different ADU values
//     never fall in the same bin, and the distribution at a value is the number of pixels up
//     to it inclusive, both ways. On float it is finer than Siril's quantisation to
//     65535 steps, which is the more exact variant. Gain-invariant by construction,
//     like every figure of the tool.
//   - The equalisation is done at full resolution, PER CHANNEL, then the 8-bit raster is
//     shrunk for display with the same k rule as Img.Prepare. The alternative - block-mean
//     first, equalisation after - gives a stronger stretch (the noise is averaged before the
//     distribution), but it is not what Siril shows, and the requirement was Siril's picture.
//   - The rounding is round_to_BYTE's: (byte)(x + 0.5), not Math.Round, which rounds
//     to even. A vector computed by hand pins it in the tests.
//
// THE VIEWS. On a CFA frame, the four positions are each equalised on their own distribution
// (Siril on a debayered image: every layer on its own histogram), and on offer are the RGB
// composite (R = R, G = the mean of the two equalised greens, B = B), each position as
// grey, and the "mosaic" - the whole frame equalised as a single layer, that is literally Siril
// on an undebayered file, the way it opens a raw by default. On mono, a single view.
//
// TWO RESOLUTIONS. Every raster exists both whole (Full, MosaicFull, CompositeFullRgb),
// for the window, which shows it directly as a bitmap - at 100 % a screen pixel is a
// sensor pixel - and shrunk by Img.Prepare's rule, for the console's PNG and
// for the tests. The shrunk one is the block mean of the whole one, not a second computation.
//
// MEASURED (Release, D3300, 6016 x 4016, on the same files as the modes compared against):
//   - float FITS master: ~2-3 s and 690 MB peak, against 3.3 s / 611 MB for the structure mode;
//   - NEF: ~2-3 s and 516 MB peak, against 3.2 s / 1.1 GB for --expo.
// The whole rasters kept cost ~18 MB on a 24 MP frame (it was 672 / 498 MB
// without them). Loading dominates (~0.5 s FITS, ~1 s libraw); the analysis itself is ~1 s: two
// passes over the data per position for the histogram and the table, one for the mosaic, and
// the shrinking. Two things were expensive in the first version and are noted where they are:
// the median by sorting (HistEqLut.Median) and the shrinking through double rasters (Shrink).

using System;
using System.Linq;

namespace FlatCheck
{
    /// <summary>An equalised channel: the raster to display and where it came from.</summary>
    internal sealed class HistEqChannel
    {
        /// <summary>0..3 = the Bayer position, in Mosaic.PositionOf's order; 0 on mono.</summary>
        internal int Position;

        /// <summary>
        /// "R", "G1", "G2", "B" when the order is known; "CFA[0,1]" when not; "mono" on mono.
        /// </summary>
        internal string Label;

        /// <summary>
        /// 'R', 'G' or 'B'; '\0' when the file did not say the order, or on mono.
        /// </summary>
        internal char Colour;

        /// <summary>The size of the shrunk raster - the one the console writes.</summary>
        internal int H, W;

        /// <summary>H*W grey levels, row by row; the block mean of Full.</summary>
        internal byte[] Equalised;

        /// <summary>
        /// The size of the raster at full resolution (the position's, i.e. H/2 x W/2 of the sensor
        /// on CFA).
        /// </summary>
        internal int FullH, FullW;

        /// <summary>
        /// The equalised raster at full resolution, FullH*FullW. The window shows this one,
        /// directly as a bitmap, so that at 100 % a screen pixel is a sensor pixel;
        /// Equalised is derived from it.
        /// </summary>
        internal byte[] Full;

        /// <summary>The input interval of the non-zero pixels, in the file's units.</summary>
        internal double Min, Max;

        /// <summary>
        /// How many pixels entered the histogram (Siril skips the zeros; so do we).
        /// </summary>
        internal long NonZero;

        /// <summary>
        /// How many of the 256 output levels got at least one pixel, before shrinking.
        /// </summary>
        internal int LevelsUsed;
    }

    /// <summary>
    /// What <see cref="HistEqAnalyzer.Analyze"/> produced: the equalised channels, the
    /// composite and the mosaic, each shrunk and at full resolution.
    /// </summary>
    internal sealed class HistEqResult
    {
        internal bool IsCfa;
        internal double CfaSpread;
        internal CfaPattern Cfa;
        internal int SourceH, SourceW;

        /// <summary>The shrink factor of every channel (1 = not shrunk).</summary>
        internal int Shrink;

        /// <summary>4 on a CFA frame, 1 on a mono one. Never null.</summary>
        internal HistEqChannel[] Channels;

        /// <summary>The common size of all the shrunk views.</summary>
        internal int H, W;

        /// <summary>
        /// H*W*3. RGB on CFA with a known order; grey (tripled) on mono or an unknown order.
        /// </summary>
        internal byte[] CompositeRgb;

        /// <summary>
        /// The mosaic equalised as a single layer, H*W grey; null on mono (there the channel IS the
        /// mosaic).
        /// </summary>
        internal byte[] Mosaic;

        /// <summary>The full-resolution size of the channels and of the composite.</summary>
        internal int FullH, FullW;

        /// <summary>The composite at full resolution, FullH*FullW*3.</summary>
        internal byte[] CompositeFullRgb;

        /// <summary>The mosaic at full resolution, SourceH*SourceW; null on mono.</summary>
        internal byte[] MosaicFull;

        internal int ChannelCount => Channels.Length;
    }

    /// <summary>
    /// The transfer table of one channel, built exactly as in Siril. It applies to double
    /// values: zero stays zero (Siril skips it in the histogram), the rest goes through its
    /// bin.
    /// </summary>
    internal sealed class HistEqLut
    {
        internal const int Bins = 65536;

        internal readonly byte[] Table = new byte[Bins];
        internal readonly double Min, Max;
        internal readonly long NonZero, Total;

        /// <summary>The histogram the table was built from; kept for Median().</summary>
        internal readonly long[] Counts = new long[Bins];

        /// <summary>
        /// An empty table over an input interval; only <see cref="Build"/> fills one.
        /// </summary>
        /// <param name="min">The smallest non-zero value, in the file's units.</param>
        /// <param name="max">The largest value, in the file's units.</param>
        /// <param name="nonZero">How many values are not zero.</param>
        /// <param name="total">How many values there are, the zeros included.</param>
        HistEqLut(double min, double max, long nonZero, long total)
        {
            Min = min; Max = max; NonZero = nonZero; Total = total;
        }

        /// <summary>
        /// The median of the non-zero pixels, read from the histogram, to the width of a bin
        /// (under one ADU on any 12- or 16-bit file). It exists because the CFA decision needs
        /// four medians per position, and Stats.Percentile would have sorted each - four
        /// copies of 6 M doubles on a 24 MP frame, measured at ~2 s of the analysis' ~2.5 s.
        /// The histogram is built anyway; the median from it costs one pass over
        /// 65536 bins.
        /// </summary>
        /// <returns>The median, in the file's units; 0 when every value is zero.</returns>
        internal double Median()
        {
            if (NonZero == 0) return 0;
            long half = (NonZero + 1) / 2, cum = 0;
            for (int b = 0; b < Bins; b++)
            {
                cum += Counts[b];
                if (cum >= half) return Min + b * (Max - Min) / (Bins - 1);
            }
            return Max;
        }

        /// <summary>Siril's round_to_BYTE: (BYTE)(x + 0.5), cut at 255.</summary>
        /// <param name="x">The value, 0 or more.</param>
        /// <returns>The value rounded half up, at most 255.</returns>
        internal static byte RoundToByte(double x) => (byte)Math.Min(255, (int)(x + 0.5));

        /// <summary>The bin of a value on the table's interval.</summary>
        /// <param name="v">The value, in the file's units.</param>
        /// <returns>The bin, 0..<see cref="Bins"/> - 1; 0 when the interval is empty.</returns>
        internal int BinOf(double v)
        {
            if (Max <= Min) return 0;
            int b = (int)((v - Min) / (Max - Min) * (Bins - 1));
            return b < 0 ? 0 : b >= Bins ? Bins - 1 : b;
        }

        /// <summary>The displayed grey level of a value.</summary>
        /// <param name="v">The value, in the file's units.</param>
        /// <returns>0 for zero; else the table's level for the value's bin, 0..255.</returns>
        internal byte Apply(double v) => v == 0.0 ? (byte)0 : Table[BinOf(v)];

        /// <summary>Builds the table of one channel as Siril does.</summary>
        /// <param name="values">The channel's pixels, in the file's units; zeros are
        /// skipped.</param>
        /// <returns>The table; an all-zero one when every value is zero.</returns>
        internal static HistEqLut Build(double[] values)
        {
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            long nonZero = 0;
            foreach (double v in values)
            {
                if (v == 0.0) continue;
                nonZero++;
                if (v < min) min = v;
                if (v > max) max = v;
            }
            if (nonZero == 0) return new HistEqLut(0, 0, 0, values.Length);

            var lut = new HistEqLut(min, max, nonZero, values.Length);
            long[] counts = lut.Counts;
            foreach (double v in values)
                if (v != 0.0) counts[lut.BinOf(v)]++;

            // The cumulative includes the current bin, as in Siril's loop; the denominator is ALL
            // the pixels, the zeros included - that is why a frame with many dead pixels never
            // reaches 255, and neither do we.
            long cum = 0;
            double n = values.Length;
            for (int i = 0; i < Bins; i++)
            {
                cum += counts[i];
                lut.Table[i] = RoundToByte(cum / n * 255.0);
            }
            return lut;
        }
    }

    /// <summary>
    /// Mode 5, <c>--histeq</c>: equalises a frame as Siril's "Histogram" display does, per
    /// Bayer position on a CFA frame. A picture, no verdict.
    /// </summary>
    internal static class HistEqAnalyzer
    {
        /// <summary>
        /// The target long side of the displayed raster: the same as Img.Prepare's.
        /// </summary>
        internal const int TargetLong = 1200;

        /// <summary>
        /// Equalises the frame: per Bayer position when it is a CFA mosaic (the same rule as
        /// <c>--expo</c>), as a single channel otherwise, plus the composite and, on CFA, the
        /// whole mosaic as one layer.
        /// </summary>
        /// <param name="data">The frame to display, as loaded.</param>
        /// <param name="cfa">
        /// The colour order read from the file, or null when the file does not say it. It names
        /// the channels and decides whether the composite can be in colour; it changes no raster.
        /// </param>
        /// <param name="targetLong">The long side of the displayed raster, in pixels; the frame is
        /// shrunk by a whole factor to reach about it.</param>
        /// <returns>The equalised views; never null.</returns>
        internal static HistEqResult Analyze(Img data, CfaPattern cfa, int targetLong = TargetLong)
        {
            bool canSplit = data.H >= 2 && data.W >= 2;
            Img[] positions = canSplit ? Mosaic.Split(data) : null;

            // The per-position tables are built first, because the CFA decision is taken from
            // their medians - the same rule as for --expo (Mosaic.LooksCfa on the max/min of the
            // four), with the median read from the histogram instead of by sorting. On CFA
            // the same tables then equalise the channels; on mono they are thrown away, and
            // they cost two passes over the data.
            double spread = 1.0;
            HistEqLut[] positionLuts = null;
            if (canSplit)
            {
                positionLuts = positions.Select(p => HistEqLut.Build(p.D)).ToArray();
                var meds = positionLuts.Select(l => l.Median()).ToArray();
                spread = meds.Max() / Math.Max(meds.Min(), 1e-9);
            }
            bool isCfa = canSplit && Mosaic.LooksCfa(spread);

            Img[] sources = isCfa ? positions : new[] { data };
            int k = ShrinkFactor(sources[0].H, sources[0].W, targetLong);

            var channels = new HistEqChannel[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                int h = sources[i].H, w = sources[i].W;
                HistEqLut lut = isCfa ? positionLuts[i] : HistEqLut.Build(sources[i].D);
                byte[] full = ApplyLut(lut, sources[i].D);
                // The position's raster is not needed after this; on CFA it is a copy of the
                // data, and four copies held to the end would sit idle beside the mosaic.
                if (isCfa) sources[i] = null;
                channels[i] = new HistEqChannel
                {
                    Position = i,
                    Label = isCfa ? Mosaic.Label(cfa, i) : "mono",
                    Colour = isCfa ? Mosaic.ColourOf(cfa, i) : '\0',
                    Min = lut.Min,
                    Max = lut.Max,
                    NonZero = lut.NonZero,
                    LevelsUsed = LevelsUsed(full),
                    Full = full,
                    FullH = h,
                    FullW = w,
                };
                (channels[i].Equalised, channels[i].H, channels[i].W) = Shrink(full, h, w, k);
            }

            var r = new HistEqResult
            {
                IsCfa = isCfa,
                CfaSpread = spread,
                Cfa = cfa,
                SourceH = data.H,
                SourceW = data.W,
                Shrink = k,
                Channels = channels,
                H = channels[0].H,
                W = channels[0].W,
                FullH = channels[0].FullH,
                FullW = channels[0].FullW,
            };
            r.CompositeRgb = Composite(r, full: false);
            r.CompositeFullRgb = Composite(r, full: true);

            if (isCfa)
            {
                // The whole frame as a single layer, shrunk by 2k to come out the size of the
                // channels: floor(floor(H/2)/k) == floor(H/(2k)), so the sizes really match.
                var lut = HistEqLut.Build(data.D);
                r.MosaicFull = ApplyLut(lut, data.D);
                (r.Mosaic, _, _) = Shrink(r.MosaicFull, data.H, data.W, 2 * k);
            }
            return r;
        }

        /// <summary>Img.Prepare's rule, with its truncation: 3000 -> 2, 2399 -> 1.</summary>
        /// <param name="h">The raster's height, pixels.</param>
        /// <param name="w">The raster's width, pixels.</param>
        /// <param name="targetLong">The target long side, pixels.</param>
        /// <returns>The whole shrink factor, at least 1.</returns>
        internal static int ShrinkFactor(int h, int w, int targetLong)
            => Math.Max(1, (int)(Math.Max(h, w) / (double)targetLong));

        /// <summary>Passes every value through the table.</summary>
        /// <param name="lut">The channel's table.</param>
        /// <param name="values">The channel's pixels, in the file's units.</param>
        /// <returns>The grey levels, one per value, in the same order.</returns>
        static byte[] ApplyLut(HistEqLut lut, double[] values)
        {
            var o = new byte[values.Length];
            for (int i = 0; i < values.Length; i++) o[i] = lut.Apply(values[i]);
            return o;
        }

        /// <summary>How many distinct grey levels a raster uses.</summary>
        /// <param name="grey">The grey levels.</param>
        /// <returns>The count, 0..256.</returns>
        static int LevelsUsed(byte[] grey)
        {
            var seen = new bool[256];
            foreach (byte b in grey) seen[b] = true;
            return seen.Count(s => s);
        }

        /// <summary>
        /// Shrinking by the mean of k x k blocks, directly on bytes - the same arithmetic as
        /// Img.BlockMean (truncated sizes, sum / k^2, the integer sums are exact in
        /// double), without the temporary double raster. The first version went through Img and
        /// was measured: on the 24 MP master the memory peak was 1.1 GB, against 611 MB for
        /// the structure mode on the same file, because the whole mosaic became a raster
        /// of 24 M doubles beside the loaded data. On bytes it no longer exists.
        /// </summary>
        /// <param name="grey">The grey levels, h*w, row by row.</param>
        /// <param name="h">The height, pixels.</param>
        /// <param name="w">The width, pixels.</param>
        /// <param name="k">The block side; 1 or less returns the input unchanged.</param>
        /// <returns>The shrunk levels and their size, floor(h/k) x floor(w/k).</returns>
        static (byte[] Grey, int H, int W) Shrink(byte[] grey, int h, int w, int k)
        {
            if (k <= 1) return (grey, h, w);

            int oh = h / k, ow = w / k;
            var o = new byte[oh * ow];
            double inv = 1.0 / (k * k);
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    int s = 0;
                    for (int dy = 0; dy < k; dy++)
                    {
                        int row = (y * k + dy) * w + x * k;
                        for (int dx = 0; dx < k; dx++) s += grey[row + dx];
                    }
                    o[y * ow + x] = HistEqLut.RoundToByte(s * inv);
                }
            return (o, oh, ow);
        }

        /// <summary>
        /// The composite: R from the red position, G the mean of the two greens, B from the blue
        /// one - looked up BY COLOUR, not by position, because RGGB is only the most frequent
        /// order. Without a known order, the mean of the four: an assignment of colours would be
        /// a guess, and the tool does not guess mosaics. On mono, the grey tripled.
        /// </summary>
        /// <param name="r">The result, with its channels filled.</param>
        /// <param name="full">True for the full-resolution rasters, false for the shrunk
        /// ones.</param>
        /// <returns>The interleaved RGB bytes, three per pixel.</returns>
        static byte[] Composite(HistEqResult r, bool full)
        {
            // The same computation on the shrunk rasters (console) and on the whole ones
            // (window); the choice of source is the only difference.
            byte[] Of(HistEqChannel c) => full ? c.Full : c.Equalised;
            int n = full ? r.FullH * r.FullW : r.H * r.W;
            var rgb = new byte[n * 3];

            if (!r.IsCfa)
            {
                byte[] g = Of(r.Channels[0]);
                for (int i = 0; i < n; i++) { rgb[i * 3] = g[i]; rgb[i * 3 + 1] = g[i]; rgb[i * 3 + 2] = g[i]; }
                return rgb;
            }

            if (r.Cfa == null)
            {
                var all = r.Channels.Select(Of).ToArray();
                for (int i = 0; i < n; i++)
                {
                    int s = 0;
                    foreach (byte[] c in all) s += c[i];
                    byte m = HistEqLut.RoundToByte(s / 4.0);
                    rgb[i * 3] = m; rgb[i * 3 + 1] = m; rgb[i * 3 + 2] = m;
                }
                return rgb;
            }

            byte[] red = Of(r.Channels.First(c => c.Colour == 'R'));
            byte[] blue = Of(r.Channels.First(c => c.Colour == 'B'));
            var greens = r.Channels.Where(c => c.Colour == 'G').Select(Of).ToArray();
            for (int i = 0; i < n; i++)
            {
                rgb[i * 3] = red[i];
                rgb[i * 3 + 1] = HistEqLut.RoundToByte((greens[0][i] + greens[1][i]) / 2.0);
                rgb[i * 3 + 2] = blue[i];
            }
            return rgb;
        }
    }
}
