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
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageVisualCompare.Tests;

/// <summary>
/// Writes throwaway image files in every format the loader claims to handle, so the imaging
/// tests exercise the real WIC codecs rather than a stand-in. Generating them beats checking
/// binary fixtures into the repository, and it keeps the formats under test visible in code.
/// </summary>
public sealed class TempImages : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ivc-tests-" + Guid.NewGuid().ToString("N"));

    public TempImages() => Directory.CreateDirectory(_directory);

    public string WritePng(string name, PixelFormat format, int width, int height) =>
        Write(name, format, width, height, new PngBitmapEncoder());

    public string WriteTiff(string name, PixelFormat format, int width, int height) =>
        Write(name, format, width, height, new TiffBitmapEncoder());

    /// <summary>A 16-bit RGB PNG of the given samples, red, green, blue for each pixel, top row first.</summary>
    public string WritePng(string name, ushort[] rgb48, int width, int height)
    {
        BitmapSource source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Rgb48, null, rgb48, width * 6);
        return Save(name, source, new PngBitmapEncoder());
    }

    /// <summary>A file of exactly these bytes: a FITS built by <see cref="FitsFixture"/>, whole or cut short.</summary>
    public string WriteBytes(string name, byte[] bytes)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>A file with the right extension and nothing a codec can make sense of.</summary>
    public string WriteCorrupt(string name)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02]);
        return path;
    }

    /// <summary>
    /// A gray PNG whose header declares this size over a few rows of data: it probes as that
    /// size, and a decoder that believes the header fails when it sets aside the pixels.
    /// </summary>
    public string WritePngWithHeaderSize(string name, int width, int height)
    {
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // 8 bits a sample; colour type 0, gray; no interlace
        WriteChunk(png, "IHDR", header);

        using var deflated = new MemoryStream();
        using (var zlib = new ZLibStream(deflated, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            for (int row = 0; row < Math.Min(height, 8); row++)
            {
                zlib.Write(new byte[1 + Math.Min(width, 64)]);
            }
        }

        WriteChunk(png, "IDAT", deflated.ToArray());
        WriteChunk(png, "IEND", []);
        return WriteBytes(name, png.ToArray());
    }

    /// <summary>
    /// A 16 x 16 WebP with a valid container and frame header and a garbage bitstream, as the
    /// code review of 2026-10-21 built it. With Windows' WebP codec the lossless one is refused
    /// when it is probed, and the lossy one probes and is refused when it is decoded; without the
    /// codec both are refused as an unknown format.
    /// </summary>
    public string WriteDamagedWebP(string name, bool lossless)
    {
        var frame = new List<byte>();
        byte[] number = new byte[4];
        if (lossless)
        {
            // The signature, then 14 bits of the width less 1 and 14 of the height less 1.
            frame.Add(0x2F);
            BinaryPrimitives.WriteInt32LittleEndian(number, 15 | (15 << 14));
            frame.AddRange(number);
        }
        else
        {
            // A key frame's tag, the start code, the width and the height.
            frame.AddRange([0x50, 0x01, 0x00, 0x9D, 0x01, 0x2A, 0x10, 0x00, 0x10, 0x00]);
        }

        byte[] garbage = new byte[40];
        new Random(lossless ? 3 : 7).NextBytes(garbage);
        frame.AddRange(garbage);

        var webp = new List<byte>();
        webp.AddRange(Encoding.ASCII.GetBytes("RIFF"));
        BinaryPrimitives.WriteInt32LittleEndian(number, 4 + 8 + frame.Count);
        webp.AddRange(number);
        webp.AddRange(Encoding.ASCII.GetBytes(lossless ? "WEBPVP8L" : "WEBPVP8 "));
        BinaryPrimitives.WriteInt32LittleEndian(number, frame.Count);
        webp.AddRange(number);
        webp.AddRange(frame);
        return WriteBytes(name, [.. webp]);
    }

    public string PathFor(string name) => Path.Combine(_directory, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }

    private string Write(string name, PixelFormat format, int width, int height, BitmapEncoder encoder)
    {
        int stride = (width * format.BitsPerPixel + 7) / 8;
        byte[] pixels = new byte[stride * height];
        new Random(name.GetHashCode(StringComparison.Ordinal)).NextBytes(pixels);

        BitmapPalette? palette = format == PixelFormats.Indexed8 ? BitmapPalettes.Halftone256 : null;
        BitmapSource source = BitmapSource.Create(width, height, 96, 96, format, palette, pixels, stride);
        return Save(name, source, encoder);
    }

    private string Save(string name, BitmapSource source, BitmapEncoder encoder)
    {
        string path = Path.Combine(_directory, name);
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    /// <summary>Appends a PNG chunk: its length, its type, its data and their CRC.</summary>
    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        byte[] number = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        png.Write(number);
        byte[] typeAndData = [.. Encoding.ASCII.GetBytes(type), .. data];
        png.Write(typeAndData);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32(typeAndData));
        png.Write(number);
    }

    /// <summary>The CRC-32 PNG puts after every chunk.</summary>
    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        return ~crc;
    }
}
