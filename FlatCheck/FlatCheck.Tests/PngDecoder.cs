using System.IO.Compression;
using System.Text;

namespace FlatCheck.Tests;

internal sealed class PngChunk
{
    internal string Type = "";
    internal byte[] Data = Array.Empty<byte>();
    internal uint DeclaredCrc;
    internal uint ComputedCrc;
    internal bool CrcValid => DeclaredCrc == ComputedCrc;
}

internal sealed class DecodedPng
{
    internal int Width, Height;
    internal int BitDepth, ColorType, Compression, Filter, Interlace;
    internal List<PngChunk> Chunks = new();
    internal List<byte> RowFilters = new();
    internal byte[] Rgb = Array.Empty<byte>();   // width*height*3, row-major

    internal (byte R, byte G, byte B) At(int x, int y)
    {
        int p = (y * Width + x) * 3;
        return (Rgb[p], Rgb[p + 1], Rgb[p + 2]);
    }
}

/// <summary>
/// A minimal PNG reader, written here rather than pulled in, so the encoder is checked
/// by something independent of it. Deliberately NOT System.Drawing: that is Windows-only
/// and obsolete on .NET 8, so it would make the suite non-portable.
///
/// Strict on purpose - it only accepts what FlatCheck actually emits (8-bit truecolour,
/// filter 0, non-interlaced), so a change in any of those shows up as a decode failure
/// rather than being silently tolerated.
/// </summary>
internal static class PngDecoder
{
    static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    internal static DecodedPng Decode(byte[] png)
    {
        for (int i = 0; i < Signature.Length; i++)
            if (png[i] != Signature[i])
                throw new InvalidDataException($"bad PNG signature at byte {i}");

        var result = new DecodedPng();
        var idat = new MemoryStream();
        int pos = Signature.Length;

        while (pos < png.Length)
        {
            int len = ReadBE(png, pos);
            string type = Encoding.ASCII.GetString(png, pos + 4, 4);
            var data = new byte[len];
            Array.Copy(png, pos + 8, data, 0, len);
            uint declared = (uint)ReadBE(png, pos + 8 + len);

            var chunk = new PngChunk
            {
                Type = type,
                Data = data,
                DeclaredCrc = declared,
                ComputedCrc = Crc32(Encoding.ASCII.GetBytes(type), data),
            };
            result.Chunks.Add(chunk);

            if (type == "IHDR")
            {
                result.Width = ReadBE(data, 0);
                result.Height = ReadBE(data, 4);
                result.BitDepth = data[8];
                result.ColorType = data[9];
                result.Compression = data[10];
                result.Filter = data[11];
                result.Interlace = data[12];
            }
            else if (type == "IDAT")
            {
                idat.Write(data, 0, data.Length);
            }

            pos += 12 + len;
        }

        if (result.BitDepth != 8 || result.ColorType != 2)
            throw new InvalidDataException(
                $"decoder only handles 8-bit truecolour, got depth {result.BitDepth} type {result.ColorType}");
        if (result.Interlace != 0)
            throw new InvalidDataException("decoder does not handle interlacing");

        // inflate, then strip the per-row filter byte
        idat.Position = 0;
        using var inflated = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
            z.CopyTo(inflated);
        byte[] raw = inflated.ToArray();

        int stride = result.Width * 3;
        int expected = (stride + 1) * result.Height;
        if (raw.Length != expected)
            throw new InvalidDataException($"expected {expected} raw bytes, got {raw.Length}");

        result.Rgb = new byte[stride * result.Height];
        for (int y = 0; y < result.Height; y++)
        {
            byte filter = raw[y * (stride + 1)];
            result.RowFilters.Add(filter);
            if (filter != 0)
                throw new InvalidDataException($"row {y} uses filter {filter}; only None is supported");
            Array.Copy(raw, y * (stride + 1) + 1, result.Rgb, y * stride, stride);
        }
        return result;
    }

    static int ReadBE(byte[] b, int o)
        => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

    /// <summary>Independent CRC-32 (no table), so it cannot share a bug with the encoder.</summary>
    internal static uint Crc32(params byte[][] parts)
    {
        uint crc = 0xffffffff;
        foreach (var part in parts)
            foreach (byte b in part)
            {
                crc ^= b;
                for (int k = 0; k < 8; k++)
                    crc = (crc & 1) != 0 ? 0xedb88320 ^ (crc >> 1) : crc >> 1;
            }
        return crc ^ 0xffffffff;
    }
}
