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

// PngWriter.cs - our own PNG encoder (no dependencies) for the residual map.
//
// The map is diverging: blue (below) -> white -> red (above), with the white FIXED at 1.0.
// Fixing it at 1.0 is valid for every caller of today - the structure residual is img/model ~ 1,
// the A/B ratio is normalised to median 1, and the --light map is 1 + radial/a. If you add a
// caller whose data are NOT centred on 1, the map will lie.
//
// The ENDS of the range come from outside, as MapRange, and are of two kinds:
//
//   - AutoRange - the 1/99 percentiles plus 10% padding. The behaviour of always, and the only
//     one the console and the file the window saves use.
//   - FixedRange - symmetric, +/- a percentage cap around 1.0.
//
// Why the second appeared: the automatic scale stretches the colour over each image's own
// range, so full red means nothing in particular - measured on two masters of the same lens,
// full red was +3% on one and +10% on the other, and nothing on screen said so. Two maps on the
// automatic scale can be compared only by their shape.
//
// The caps offered (FixedCapsPct) are NOT new numbers: they are the bounds of the residual
// scale, Th.Resid*. Do not duplicate them; if the scale is recalibrated, so are they.
//
// But the cap and the band are NOT the same thing, and it is worth saying exactly: the cap is a
// PIXEL's deviation from the model, the band is a peak-to-peak amplitude. A one-sided +10% spot
// reaches the 10% cap right at the bound of the BUN band; a 10% gradient (which goes from -5% to
// +5%) reaches only half of it. Measured: at a 10% gradient the map has p1/p99 at -4.9/+4.8%. So
// on the default +/-10% chip, saturation means "leaves BUN" for a spot and "has reached REFA"
// for a gradient. There is no cap that is exact for both, so it is explained in the legend and
// in the README, the figures are not changed.
//
// The encoder writes the signature + IHDR (8 bits, color type 2 = RGB) + a single IDAT (each row
// preceded by filter byte 0, deflated through ZLibStream) + IEND, with the CRC-32 computed by
// hand.

using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;

namespace FlatCheck
{
    /// <summary>
    /// Encodes RGB rasters as PNG, and turns a residual raster centred on 1.0 into the
    /// blue-white-red map, alone or several side by side in a labelled mosaic.
    /// </summary>
    internal static class PngWriter
    {
        /// <summary>The map's white. See the note at the top before you change it.</summary>
        internal const double Mid = 1.0;

        internal static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>
        /// The map's ends, in the raster's units (so around <see cref="Mid"/> = 1.0).
        /// </summary>
        /// <param name="Lo">The value drawn full blue; anything lower is clipped to it.</param>
        /// <param name="Hi">The value drawn full red; anything higher is clipped to it.</param>
        internal readonly record struct MapRange(double Lo, double Hi);

        /// <summary>
        /// The caps offered as a fixed scale: the bounds of the residual scale, in percent.
        /// See the note at the top - they are not new thresholds, they are Th.Resid* reused.
        /// </summary>
        internal static readonly double[] FixedCapsPct = { Th.ResidPerfect, Th.ResidUsable, Th.ResidCheck };

        /// <summary>
        /// The window's default cap: the band a normal flat is expected to live in, so two
        /// sessions are compared at a glance without pressing anything.
        /// </summary>
        internal const double DefaultCapPct = Th.ResidUsable;

        /// <summary>
        /// The automatic scale: the 1/99 percentiles plus 10% padding. The behaviour of always -
        /// the console and the saved file use only this.
        /// </summary>
        /// <param name="resid">The residual raster, centred on 1.0.</param>
        /// <returns>The ends, each padded by 10% of the spread and at least 1e-5.</returns>
        internal static MapRange AutoRange(Img resid)
        {
            // a single sort for both percentiles
            var sorted = Stats.Sorted(resid.D);
            double lo = Stats.OfSorted(sorted, 1), hi = Stats.OfSorted(sorted, 99);
            double pad = Math.Max(hi - lo, 1e-4) * 0.1;
            return new MapRange(lo - pad, hi + pad);
        }

        /// <summary>
        /// The fixed, symmetric scale: +/- <paramref name="capPct"/> percent around 1.0.
        /// </summary>
        /// <param name="capPct">The cap, in percent.</param>
        /// <returns>The ends, <c>1 - cap/100</c> and <c>1 + cap/100</c>.</returns>
        internal static MapRange FixedRange(double capPct)
            => new MapRange(Mid - capPct / 100.0, Mid + capPct / 100.0);

        /// <summary>
        /// Scales the residual and converts it to RGB, on the automatic scale. It also returns
        /// the ends, so that they can be checked.
        /// </summary>
        /// <param name="resid">The residual raster, centred on 1.0.</param>
        /// <returns>The RGB bytes (H*W*3, interleaved) and the ends used.</returns>
        internal static (byte[] Rgb, double Lo, double Hi) MapResidual(Img resid)
            => MapResidual(resid, AutoRange(resid));

        /// <summary>
        /// Scales the residual and converts it to RGB, on ends given by the caller. It also
        /// returns the ends, so that they can be checked.
        /// </summary>
        /// <param name="resid">The residual raster, centred on 1.0.</param>
        /// <param name="range">The ends of the colour scale.</param>
        /// <returns>The RGB bytes (H*W*3, interleaved) and the ends used.</returns>
        internal static (byte[] Rgb, double Lo, double Hi) MapResidual(Img resid, MapRange range)
        {
            double lo = range.Lo, hi = range.Hi;

            int h = resid.H, w = resid.W;
            var rgb = new byte[h * w * 3];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var (r, g, b) = Colormap(resid.At(y, x), lo, Mid, hi);
                    int p = (y * w + x) * 3;
                    rgb[p] = r; rgb[p + 1] = g; rgb[p + 2] = b;
                }
            return (rgb, lo, hi);
        }

        /// <summary>
        /// The residual map ready encoded as PNG, in memory, on the automatic scale.
        ///
        /// That is the form both applications want: the console writes it to disk, the window
        /// gives it straight to a BitmapImage. A temporary file just so that the window can show
        /// an image would have been litter on disk and one more failure path.
        /// </summary>
        /// <param name="resid">The residual raster, centred on 1.0.</param>
        /// <returns>The PNG file's bytes.</returns>
        internal static byte[] EncodeResidualMap(Img resid)
            => EncodeResidualMap(resid, AutoRange(resid));

        /// <summary>
        /// The same map, on ends given by the caller. The window reuses it to show the same
        /// residual on the fixed scale, without rerunning the analysis.
        /// </summary>
        /// <param name="resid">The residual raster, centred on 1.0.</param>
        /// <param name="range">The ends of the colour scale.</param>
        /// <returns>The PNG file's bytes.</returns>
        internal static byte[] EncodeResidualMap(Img resid, MapRange range)
        {
            var (rgb, _, _) = MapResidual(resid, range);
            return Encode(rgb, resid.W, resid.H);
        }

        /// <summary>
        /// Writes the residual map to disk, on the automatic scale. The message for the user is
        /// the caller's business.
        /// </summary>
        /// <param name="path">The PNG file to write; overwritten if it exists.</param>
        /// <param name="resid">The residual raster, centred on 1.0.</param>
        internal static void WriteResidualMap(string path, Img resid)
            => File.WriteAllBytes(path, EncodeResidualMap(resid));

        // ------------------------------------------------------------------ the mosaic
        //
        // Several residual maps side by side, on ONE scale (given by the caller, usually
        // AutoRange over all of them at once), with a title band above and a label band above
        // each tile. The text is BitmapFont; the text's scale follows the tile's width, so that
        // a label fits even on a 128 px test tile. It decides nothing: the caller gives the
        // maps, the labels, the scale and the title (ADR 0054).

        internal const int MosaicGap = 8;
        internal const int MosaicTextScale = 3;
        internal const int MosaicPad = 8;
        /// <summary>
        /// The height of a text band (title, label), fixed: at the text's largest scale plus
        /// margins.
        /// </summary>
        internal static readonly int MosaicBandHeight = BitmapFont.Height(MosaicTextScale) + 2 * MosaicPad;
        internal static readonly (byte R, byte G, byte B) MosaicText = (255, 255, 255);
        internal static readonly (byte R, byte G, byte B) MosaicBand = (40, 40, 44);

        /// <summary>
        /// The mosaic of residual maps as a PNG: the title band, then per tile its label band
        /// and its map, the tiles <see cref="MosaicGap"/> px apart. An empty list throws an
        /// ArgumentException.
        /// </summary>
        /// <param name="tiles">The maps, left to right, each with its label; at least one.</param>
        /// <param name="range">The one colour scale every map is drawn on.</param>
        /// <param name="title">The text of the title band.</param>
        /// <returns>The PNG file's bytes.</returns>
        internal static byte[] EncodeMosaic(IReadOnlyList<(Img Map, string Label)> tiles, MapRange range, string title)
        {
            if (tiles == null || tiles.Count == 0) throw new ArgumentException("mozaic fara dale", nameof(tiles));

            int w = 0, tileH = 0, minTileW = int.MaxValue;
            foreach (var (map, _) in tiles)
            {
                w += map.W;
                tileH = Math.Max(tileH, map.H);
                minTileW = Math.Min(minTileW, map.W);
            }
            w += MosaicGap * (tiles.Count - 1);
            int band = MosaicBandHeight;
            int h = 2 * band + tileH;
            int scale = Math.Clamp(minTileW / 400, 1, MosaicTextScale);

            var rgb = new byte[w * h * 3];
            for (int i = 0; i < w * h; i++)
            {
                rgb[i * 3] = MosaicBand.R; rgb[i * 3 + 1] = MosaicBand.G; rgb[i * 3 + 2] = MosaicBand.B;
            }

            BitmapFont.Draw(rgb, w, h, MosaicPad, MosaicPad, title, scale, MosaicText.R, MosaicText.G, MosaicText.B);

            int x0 = 0;
            foreach (var (map, label) in tiles)
            {
                BitmapFont.Draw(rgb, w, h, x0 + MosaicPad, band + MosaicPad, label, scale, MosaicText.R, MosaicText.G, MosaicText.B);
                var (tile, _, _) = MapResidual(map, range);
                for (int y = 0; y < map.H; y++)
                    Array.Copy(tile, y * map.W * 3, rgb, ((2 * band + y) * w + x0) * 3, map.W * 3);
                x0 += map.W + MosaicGap;
            }

            return Encode(rgb, w, h);
        }

        /// <summary>
        /// The colour of one value on the diverging scale: blue at <paramref name="lo"/>, white
        /// at <paramref name="mid"/>, red at <paramref name="hi"/>, linear in between and clipped
        /// outside.
        /// </summary>
        /// <param name="v">The value.</param>
        /// <param name="lo">The value drawn full blue.</param>
        /// <param name="mid">The value drawn white.</param>
        /// <param name="hi">The value drawn full red.</param>
        /// <returns>The colour's components, 0..255.</returns>
        internal static (byte r, byte g, byte b) Colormap(double v, double lo, double mid, double hi)
        {
            double t;
            byte r, g, b;
            if (v < mid)
            {
                t = (v - lo) / Math.Max(mid - lo, 1e-12); t = Math.Clamp(t, 0, 1);
                // blue -> white
                r = (byte)(255 * t); g = (byte)(255 * t); b = 255;
            }
            else
            {
                t = (v - mid) / Math.Max(hi - mid, 1e-12); t = Math.Clamp(t, 0, 1);
                // white -> red
                r = 255; g = (byte)(255 * (1 - t)); b = (byte)(255 * (1 - t));
            }
            return (r, g, b);
        }

        /// <summary>Writes an RGB raster to disk as a PNG.</summary>
        /// <param name="path">The PNG file to write; overwritten if it exists.</param>
        /// <param name="rgb">The pixels, w*h*3 bytes, interleaved R,G,B, rows top down.</param>
        /// <param name="w">The width in pixels.</param>
        /// <param name="h">The height in pixels.</param>
        internal static void Write(string path, byte[] rgb, int w, int h)
            => File.WriteAllBytes(path, Encode(rgb, w, h));

        /// <summary>
        /// A grey raster, written as RGB with the three components equal. The encoder keeps a
        /// single colour type; adding type 0 for the equalised views would have meant a second
        /// path through IHDR and through the tests' decoder, for bytes no one counts.
        /// </summary>
        /// <param name="grey">The pixels, w*h bytes, rows top down.</param>
        /// <param name="w">The width in pixels.</param>
        /// <param name="h">The height in pixels.</param>
        /// <returns>The PNG file's bytes.</returns>
        internal static byte[] EncodeGrey(byte[] grey, int w, int h)
        {
            var rgb = new byte[grey.Length * 3];
            for (int i = 0; i < grey.Length; i++)
            {
                rgb[i * 3] = grey[i]; rgb[i * 3 + 1] = grey[i]; rgb[i * 3 + 2] = grey[i];
            }
            return Encode(rgb, w, h);
        }

        /// <summary>
        /// Encodes in memory - the real entry point, so that it can be tested without a disk.
        /// </summary>
        /// <param name="rgb">The pixels, w*h*3 bytes, interleaved R,G,B, rows top down.</param>
        /// <param name="w">The width in pixels.</param>
        /// <param name="h">The height in pixels.</param>
        /// <returns>The PNG file's bytes: 8-bit RGB, no filtering, one IDAT.</returns>
        internal static byte[] Encode(byte[] rgb, int w, int h)
        {
            using var outp = new MemoryStream();
            outp.Write(Signature, 0, Signature.Length);

            var ihdr = new byte[13];
            WriteBE(ihdr, 0, w); WriteBE(ihdr, 4, h);
            ihdr[8] = 8;   // bit depth
            ihdr[9] = 2;   // color type RGB
            ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;   // deflate, adaptive filtering, not interlaced
            WriteChunk(outp, "IHDR", ihdr);

            // IDAT: each row preceded by filter byte 0 (None), then zlib
            using var raw = new MemoryStream();
            for (int y = 0; y < h; y++)
            {
                raw.WriteByte(0);
                raw.Write(rgb, y * w * 3, w * 3);
            }
            byte[] rawBytes = raw.ToArray();
            byte[] comp;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                    z.Write(rawBytes, 0, rawBytes.Length);
                comp = ms.ToArray();
            }
            WriteChunk(outp, "IDAT", comp);
            WriteChunk(outp, "IEND", Array.Empty<byte>());
            return outp.ToArray();
        }

        /// <summary>Writes a 32-bit integer big-endian into a buffer.</summary>
        /// <param name="b">The buffer.</param>
        /// <param name="o">The offset of the first byte.</param>
        /// <param name="v">The value.</param>
        static void WriteBE(byte[] b, int o, int v)
        { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        /// <summary>Writes one PNG chunk: length, type, data, CRC.</summary>
        /// <param name="s">Where the chunk goes.</param>
        /// <param name="type">The four-letter chunk type.</param>
        /// <param name="data">The chunk's data; may be empty.</param>
        static void WriteChunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len, 0, 4);
            var tb = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(tb, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = Crc32(tb, data);
            var cb = new byte[4]; WriteBE(cb, 0, (int)crc); s.Write(cb, 0, 4);
        }

        // Built once, when the type initialises, and published already filled. It used to be a
        // lazily initialised field ("if null, fill it"), which was a real race, not a theoretical
        // one: the field was assigned BEFORE it was filled, so a second thread coming in at that
        // moment saw the table non-null, read it with zeros and wrote a wrong CRC - that is, a
        // corrupt PNG. Reproduced: 8 threads, 2000 rounds, 5261 wrong CRCs. In the window it is
        // really possible, because the Ratio tab can encode while Structure is still running. A
        // static initialiser has the CLR's guarantee that no one sees the table until it is ready.
        static readonly uint[] _crcTable = BuildCrcTable();

        /// <summary>The 256-entry table of the PNG CRC-32 (polynomial 0xedb88320).</summary>
        /// <returns>The table.</returns>
        static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        /// <summary>The CRC-32 of a chunk, over its type and its data, as PNG defines it.</summary>
        /// <param name="type">The chunk type's four bytes.</param>
        /// <param name="data">The chunk's data.</param>
        /// <returns>The CRC.</returns>
        internal static uint Crc32(byte[] type, byte[] data)
        {
            uint crc = 0xffffffff;
            foreach (byte bb in type) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            foreach (byte bb in data) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            return crc ^ 0xffffffff;
        }
    }
}
