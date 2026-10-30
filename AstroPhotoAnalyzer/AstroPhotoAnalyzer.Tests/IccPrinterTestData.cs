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

using System.Buffers.Binary;
using System.Text;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// Printer profiles built by hand, with a device black whose L* the test chose: the smallest
/// profile each of the two table formats allows. The v2 one carries a lut16 table (a 2-point
/// cube); the v4 one a lutAToB table made of a matrix only. Both give, relative to paper white,
/// L* = black + (100 - black) x the mean of the device channels, neutral; the perceptual table
/// (A2B0) puts black at L* 0, as real profiles do, so a reader that takes the wrong intent
/// reads a different black. Real profiles are other people's and stay outside the repository.
/// </summary>
internal static class IccPrinterTestData
{
    /// <summary>
    /// A v2 printer profile (RGB or CMYK device, Lab PCS) whose colorimetric black is
    /// <paramref name="blackLstar"/>. With <paramref name="withB2A"/> it also carries the tables
    /// towards the printer, the inverses of the A2B ones on L* alone (a* and b* are ignored, so
    /// every colour prints as the grey of its L*): B2A1 with the kink at the black, B2A0 and
    /// B2A2 with the black at L* 0; without them a reader that needs the printer's direction
    /// refuses the profile. <paramref name="white"/> is the media white (wtpt) in XYZ, D50 times
    /// <paramref name="whiteY"/> by default; <paramref name="withPerceptualB2A"/> false leaves
    /// B2A0 out while keeping B2A1.
    /// </summary>
    internal static byte[] V2Lut16(string description, double blackLstar, string deviceClass = "prtr", string space = "RGB ", double whiteY = 1.0,
        bool withB2A = true, double[]? white = null, bool withPerceptualB2A = true)
    {
        int channels = space == "CMYK" ? 4 : 3;
        bool inkIsDark = space == "CMYK";
        white ??= new[] { 0.9642 * whiteY, whiteY, 0.8249 * whiteY };
        var tags = new List<(string, byte[])>
        {
            ("desc", DescV2(description)),
            ("wtpt", Xyz(white[0], white[1], white[2])),
            ("A2B0", Lut16(channels, 0, inkIsDark)),
            ("A2B1", Lut16(channels, blackLstar, inkIsDark)),
            ("A2B2", Lut16(channels, 0, inkIsDark)),
        };
        if (withB2A)
        {
            if (withPerceptualB2A) tags.Add(("B2A0", Lut16Inverse(channels, 0, inkIsDark)));
            tags.Add(("B2A1", Lut16Inverse(channels, blackLstar, inkIsDark)));
            tags.Add(("B2A2", Lut16Inverse(channels, 0, inkIsDark)));
        }
        return Assemble(0x02400000, deviceClass, space, tags.ToArray());
    }

    // lut16Type towards the printer: Lab in, the device out. The input table of L* turns the
    // legacy 16-bit encoding (L* 100 at 0xFF00) into (L* - black) / (100 - black), clamped to
    // 0..1, over 4096 entries, so the kink at the black is within 0.025 L* of its place; a* and
    // b* pass through and are then ignored by a 2-point cube whose every output channel is the
    // L* axis (1 - it on CMYK, where ink is dark). The output tables are the identity.
    static byte[] Lut16Inverse(int channels, double blackLstar, bool inkIsDark)
    {
        const int entries = 4096;
        var t = new byte[52 + 3 * entries * 2 + 8 * channels * 2 + channels * 2 * 2];
        Ascii(t, 0, "mft2");
        t[8] = 3; t[9] = (byte)channels; t[10] = 2;
        S15(t, 12, 1); S15(t, 28, 1); S15(t, 44, 1);   // the identity matrix
        U16(t, 48, entries); U16(t, 50, 2);
        int p = 52;
        for (int i = 0; i < entries; i++)
        {
            double lstar = i / (entries - 1.0) * 65535 / 652.8;
            double u = Math.Clamp((lstar - blackLstar) / (100 - blackLstar), 0, 1);
            U16(t, p, (int)Math.Round(u * 65535)); p += 2;
        }
        for (int c = 1; c < 3; c++)
            for (int i = 0; i < entries; i++) { U16(t, p, (int)Math.Round(i / (entries - 1.0) * 65535)); p += 2; }
        for (int n = 0; n < 8; n++)
        {
            int l = (n >> 2) & 1;   // the first input channel, L*, varies slowest
            int v = (inkIsDark ? 1 - l : l) * 65535;
            for (int c = 0; c < channels; c++) { U16(t, p, v); p += 2; }
        }
        for (int c = 0; c < channels; c++) { U16(t, p, 0); U16(t, p + 2, 65535); p += 4; }
        return t;
    }

    /// <summary>A v4 RGB printer profile whose colorimetric black is <paramref name="blackLstar"/>, as a lutAToB of M curves, matrix and B curves.</summary>
    internal static byte[] V4LutAToB(string description, double blackLstar)
        => Assemble(0x04200000, "prtr", "RGB ", new (string, byte[])[]
        {
            ("desc", DescV4(description)),
            ("wtpt", Xyz(0.9642, 1.0, 0.8249)),
            ("A2B0", LutAToB(0)),
            ("A2B1", LutAToB(blackLstar)),
            ("A2B2", LutAToB(0)),
        });

    static byte[] Assemble(uint version, string deviceClass, string space, (string Sig, byte[] Data)[] tags)
    {
        int offset = 128 + 4 + 12 * tags.Length;
        int total = offset + tags.Sum(t => Pad(t.Data.Length));
        var icc = new byte[total];
        U32(icc, 0, (uint)total);
        U32(icc, 8, version);
        Ascii(icc, 12, deviceClass);
        Ascii(icc, 16, space);
        Ascii(icc, 20, "Lab ");
        U16(icc, 24, 2026); U16(icc, 26, 10); U16(icc, 28, 5);   // a date: the package refuses a header without one
        Ascii(icc, 36, "acsp");
        S15(icc, 68, 0.9642); S15(icc, 72, 1.0); S15(icc, 76, 0.8249);   // the PCS illuminant, D50
        U32(icc, 128, (uint)tags.Length);
        for (int i = 0; i < tags.Length; i++)
        {
            Ascii(icc, 132 + 12 * i, tags[i].Sig);
            U32(icc, 136 + 12 * i, (uint)offset);
            U32(icc, 140 + 12 * i, (uint)tags[i].Data.Length);
            tags[i].Data.CopyTo(icc, offset);
            offset += Pad(tags[i].Data.Length);
        }
        return icc;
    }

    // lut16Type: a cube of 2 points per channel between device black and device white. On an RGB
    // device black is all zeros; on CMYK it is all ones, so the cube is filled the other way.
    static byte[] Lut16(int channels, double blackLstar, bool inkIsDark)
    {
        int nodes = 1 << channels;
        var t = new byte[52 + channels * 4 + nodes * 6 + 12];
        Ascii(t, 0, "mft2");
        t[8] = (byte)channels; t[9] = 3; t[10] = 2;
        S15(t, 12, 1); S15(t, 28, 1); S15(t, 44, 1);   // the identity matrix
        U16(t, 48, 2); U16(t, 50, 2);
        int p = 52;
        for (int c = 0; c < channels; c++) { U16(t, p, 0); U16(t, p + 2, 65535); p += 4; }
        for (int n = 0; n < nodes; n++)
        {
            // The first channel varies slowest: node index bit (channels - 1 - c) is channel c.
            double sum = 0;
            for (int c = 0; c < channels; c++) sum += (n >> (channels - 1 - c)) & 1;
            double light = inkIsDark ? 1 - sum / channels : sum / channels;
            double lstar = blackLstar + (100 - blackLstar) * light;
            U16(t, p, (ushort)Math.Round(lstar / 100 * 65280)); U16(t, p + 2, 128 * 256); U16(t, p + 4, 128 * 256);
            p += 6;
        }
        for (int c = 0; c < 3; c++) { U16(t, p, 0); U16(t, p + 2, 65535); p += 4; }
        return t;
    }

    // lutAToBType with no cube: M curves (identity), a 3 x 3 matrix with its offsets, B curves
    // (identity). PCS Lab on 0..1: L* / 100, (a* + 128) / 255, (b* + 128) / 255.
    static byte[] LutAToB(double blackLstar)
    {
        const int head = 32, curves = 3 * 12, matrix = 12 * 4;
        var t = new byte[head + curves + matrix + curves];
        Ascii(t, 0, "mAB ");
        t[8] = 3; t[9] = 3;
        int b = head, m = b + curves, mc = m + matrix;
        U32(t, 12, (uint)b); U32(t, 16, (uint)m); U32(t, 20, (uint)mc);   // no CLUT (24), no A curves (28)
        for (int c = 0; c < 3; c++) { Ascii(t, b + 12 * c, "curv"); Ascii(t, mc + 12 * c, "curv"); }   // count 0: identity
        double k = (100 - blackLstar) / 100 / 3;
        S15(t, m, k); S15(t, m + 4, k); S15(t, m + 8, k);                 // L from the mean of R, G, B
        S15(t, m + 36, blackLstar / 100); S15(t, m + 40, 128.0 / 255); S15(t, m + 44, 128.0 / 255);
        return t;
    }

    static byte[] Xyz(double x, double y, double z)
    {
        var t = new byte[20];
        Ascii(t, 0, "XYZ ");
        S15(t, 8, x); S15(t, 12, y); S15(t, 16, z);
        return t;
    }

    static byte[] DescV2(string s)
    {
        byte[] text = Encoding.ASCII.GetBytes(s + "\0");
        var t = new byte[12 + text.Length + 4 + 4 + 2 + 1 + 67];   // ASCII, then empty Unicode and ScriptCode parts
        Ascii(t, 0, "desc");
        U32(t, 8, (uint)text.Length);
        text.CopyTo(t, 12);
        return t;
    }

    static byte[] DescV4(string s)
    {
        byte[] text = Encoding.BigEndianUnicode.GetBytes(s);
        var t = new byte[28 + text.Length];
        Ascii(t, 0, "mluc");
        U32(t, 8, 1); U32(t, 12, 12);
        Ascii(t, 16, "enUS");
        U32(t, 20, (uint)text.Length); U32(t, 24, 28);
        text.CopyTo(t, 28);
        return t;
    }

    static int Pad(int n) => (n + 3) / 4 * 4;
    static void Ascii(byte[] b, int at, string s) => Encoding.ASCII.GetBytes(s).CopyTo(b, at);
    static void U16(byte[] b, int at, int v) => BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(at, 2), (ushort)v);
    static void U32(byte[] b, int at, uint v) => BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(at, 4), v);
    static void S15(byte[] b, int at, double v) => BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(at, 4), (int)Math.Round(v * 65536));
}
