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

using System.Reflection;
using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// Reading camera raws is the one place the tool leans on someone else's code, so what is
/// pinned here is the seam (R-007): which paths are routed to libraw, that a FITS never is,
/// that the assembly stays lazily loaded. A raw cannot live in the repo (the smallest real NEF
/// is 18 MB), so the decode itself is checked only when LIGHTSCHECK_NEF points at one.
/// </summary>
public class RawReaderTests
{
    private static readonly RawReader Reader = new();

    [Theory]
    [InlineData("light.NEF")]
    [InlineData("light.nef")]
    [InlineData("a/b/DSC_0060.Nef")]
    [InlineData("x.cr2")]
    [InlineData("x.cr3")]
    [InlineData("x.arw")]
    [InlineData("x.dng")]
    [InlineData("x.raf")]
    public void R007_RecognisesTheRawExtensions(string path) => Assert.True(Reader.Handles(path));

    [Theory]
    [InlineData("light_00001.fit")]
    [InlineData("x.fits")]
    [InlineData("x.fts")]
    [InlineData("noextension")]
    [InlineData("")]
    [InlineData(null)]
    public void R007_DoesNotClaimAnythingThatIsNotARaw(string? path) => Assert.False(Reader.Handles(path!));

    [Fact]
    public void AMissingRawFailsWithTheFileNameNotALibrawErrorCode()
    {
        string path = Path.Combine(Path.GetTempPath(), "lightscheck-nu-exista.NEF");
        FileNotFoundException e = Assert.Throws<FileNotFoundException>(() => Reader.Load(path));
        Assert.Contains("lightscheck-nu-exista.NEF", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatIsNotActuallyARawIsRefused()
    {
        using TempWorkspace ws = new();
        string path = ws.WriteFile("minte.NEF", new byte[4096]);
        Assert.ThrowsAny<Exception>(() => Reader.Load(path));
        Assert.ThrowsAny<Exception>(() => Reader.LoadInfo(path));
    }

    /// <summary>
    /// What libraw refuses becomes the reader's own error, the type a corrupt FITS gives, naming
    /// the file (R-007): the console and the window handle it; libraw's exception type does not
    /// leave the reader.
    /// </summary>
    [Fact]
    public void R007_AFileLibrawCannotOpenFailsWithInvalidDataNamingTheFile()
    {
        using TempWorkspace ws = new();
        string path = ws.WriteFile("minte.NEF", new byte[4096]);

        InvalidDataException full = Assert.Throws<InvalidDataException>(() => Reader.Load(path));
        InvalidDataException info = Assert.Throws<InvalidDataException>(() => Reader.LoadInfo(path));

        Assert.Contains("minte.NEF", full.Message, StringComparison.Ordinal);
        Assert.Contains("minte.NEF", info.Message, StringComparison.Ordinal);
        Assert.NotNull(full.InnerException);
        // Once: libraw's own message names the file too, and that copy is cut from the brackets.
        Assert.Equal(full.Message.IndexOf(path, StringComparison.Ordinal), full.Message.LastIndexOf(path, StringComparison.Ordinal));
    }

    /// <summary>
    /// The end-to-end proof, on a real file: LIGHTSCHECK_NEF=E:/.../DSC_0001.NEF. Does nothing
    /// without the variable. The mosaic is undebayered, the scale exact, the CFA order read,
    /// and the shot metadata filled; the metadata-only load agrees with the full one.
    /// </summary>
    [Fact]
    public void R007_ARealRawDecodesToAnUndebayeredMosaicWithItsScaleAndMetadata()
    {
        string? path = Environment.GetEnvironmentVariable("LIGHTSCHECK_NEF");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        LoadedFrame f = ImageLoader.Load(path);
        Assert.True(f.Image.H > 1000 && f.Image.W > 1000, $"{f.Image.W}x{f.Image.H}");
        Assert.Equal("libraw (exact)", f.Scale.Source);
        Assert.True(f.Scale.White > f.Scale.Black, $"white {f.Scale.White} black {f.Scale.Black}");
        Assert.NotNull(f.Header);
        Assert.Empty(f.Header);
        Assert.NotNull(f.Cfa);
        Assert.Equal("libraw", f.Cfa.Source);
        Assert.Equal(2, f.Cfa.Letters.Count(c => c == 'G'));

        // Undebayered: the four Bayer positions do not agree on a colour image.
        Img[] positions = Mosaic.Split(f.Image);
        double[] medians = positions.Select(p => Stats.Percentile(p.D, 50)).ToArray();
        Assert.True(medians.Max() / Math.Max(medians.Min(), 1) > 1.05, "the four positions agree: the frame was demosaiced");

        Assert.True(f.ExposureSeconds > 0);
        Assert.True(f.Iso > 0);
        Assert.NotNull(f.Timestamp);
        Assert.NotNull(f.Camera);

        LoadedFrame info = ImageLoader.LoadInfo(path);
        Assert.Null(info.Image);
        Assert.Equal(f.ExposureSeconds, info.ExposureSeconds);
        Assert.Equal(f.Iso, info.Iso);
        Assert.Equal(f.Timestamp, info.Timestamp);
        Assert.Equal(f.Camera, info.Camera);
        Assert.Equal(f.FocalMm, info.FocalMm);
        Assert.Equal(f.FNumber, info.FNumber);
        Assert.Equal(f.NikonLensId, info.NikonLensId);
    }

    /// <summary>
    /// A FITS run must keep working when Sdcb.LibRaw is absent entirely. That holds only while
    /// no field of the reader is a libraw type: fields are resolved when the type is loaded,
    /// which happens on every run since ImageLoader asks it Handles(); method bodies at JIT
    /// time. Measured in FlatCheck by deleting the DLL (its ADR 0009).
    /// </summary>
    [Fact]
    public void R007_HoldsNoLibrawTypedFields_SoTheAssemblyStaysLazilyLoaded()
    {
        FieldInfo[] fields = typeof(RawReader).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (FieldInfo f in fields)
        {
            string asm = f.FieldType.Assembly.GetName().Name ?? "";
            Assert.False(asm.StartsWith("Sdcb", StringComparison.Ordinal), $"field {f.Name} is of type {f.FieldType.FullName} from {asm}: that forces libraw to load on FITS runs too");
        }
    }
}
