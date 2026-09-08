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

using System.IO;
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

    /// <summary>A file with the right extension and nothing a codec can make sense of.</summary>
    public string WriteCorrupt(string name)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02]);
        return path;
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

        string path = Path.Combine(_directory, name);
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }
}
