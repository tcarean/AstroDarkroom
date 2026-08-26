// Core.cs - ce a mai ramas din motor: encoderul PNG pentru harta reziduala.
// Modurile s-au mutat in Analysis/ (calcul) si Report/ (text). Faza 7 muta si asta
// intr-un PngWriter dedicat.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace FlatCheck
{
    internal static class Core
    {
        // ========================================================= PNG (heatmap reziduu)
        // RdBu-like: albastru (jos) -> alb (1.0) -> rosu (sus). Scalat pe percentile 1..99.
        internal static void SaveResidualPng(string path, Img resid, TextWriter outw)
        {
            var vals = resid.D.ToList();
            double lo = Stats.Percentile(vals, 1), hi = Stats.Percentile(vals, 99);
            double pad = Math.Max(hi - lo, 1e-4) * 0.1;
            lo -= pad; hi += pad;
            double mid = 1.0;
            int h = resid.H, w = resid.W;
            var rgb = new byte[h * w * 3];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double v = resid.At(y, x);
                    (byte r, byte g, byte b) = Colormap(v, lo, mid, hi);
                    int p = (y * w + x) * 3;
                    rgb[p] = r; rgb[p + 1] = g; rgb[p + 2] = b;
                }
            WritePng(path, rgb, w, h);
            outw.WriteLine($"\n    -> harta salvata: {path}");
        }

        static (byte, byte, byte) Colormap(double v, double lo, double mid, double hi)
        {
            double t;
            byte r, g, b;
            if (v < mid)
            {
                t = (v - lo) / Math.Max(mid - lo, 1e-12); t = Math.Clamp(t, 0, 1);
                // albastru -> alb
                r = (byte)(255 * t); g = (byte)(255 * t); b = 255;
            }
            else
            {
                t = (v - mid) / Math.Max(hi - mid, 1e-12); t = Math.Clamp(t, 0, 1);
                // alb -> rosu
                r = 255; g = (byte)(255 * (1 - t)); b = (byte)(255 * (1 - t));
            }
            return (r, g, b);
        }

        static void WritePng(string path, byte[] rgb, int w, int h)
        {
            using var fs = File.Create(path);
            // semnatura
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            // IHDR
            var ihdr = new byte[13];
            WriteBE(ihdr, 0, w); WriteBE(ihdr, 4, h);
            ihdr[8] = 8;   // bit depth
            ihdr[9] = 2;   // color type RGB
            ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            WriteChunk(fs, "IHDR", ihdr);
            // IDAT: fiecare rand precedat de filter byte 0, apoi zlib-compresat
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
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(rawBytes, 0, rawBytes.Length);
                comp = ms.ToArray();
            }
            WriteChunk(fs, "IDAT", comp);
            WriteChunk(fs, "IEND", Array.Empty<byte>());
        }

        static void WriteBE(byte[] b, int o, int v)
        { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        static void WriteChunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len, 0, 4);
            var tb = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(tb, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = Crc32(tb, data);
            var cb = new byte[4]; WriteBE(cb, 0, (int)crc); s.Write(cb, 0, 4);
        }

        static uint[] _crcTable;
        static uint Crc32(byte[] type, byte[] data)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
                    _crcTable[n] = c;
                }
            }
            uint crc = 0xffffffff;
            foreach (byte bb in type) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            foreach (byte bb in data) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            return crc ^ 0xffffffff;
        }
    }
}
