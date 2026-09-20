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

namespace AstroPhotoAnalyzer.Tests;

public class FitsReaderTests
{
    static Raster Load(byte[] fits, string name = "t.fit")
    {
        using var ws = new TempWorkspace();
        return new FitsReader().Load(ws.WriteFile(name, fits));
    }

    [Fact]
    public void Int16WithBzeroIsScaledToUnitRange()
    {
        // Valorile fizice 0, 32768, 65535 - cu BZERO implicit 32768, stocate ca -32768, 0, 32767.
        byte[] fits = FitsTestData.Build2D(3, 1, (x, y) => x == 0 ? 0 : x == 1 ? 32768 : 65535);
        Raster r = Load(fits);

        Assert.False(r.IsColor);
        Assert.Equal(0f, r.At(0, 0, 0));
        Assert.Equal(32768f / 65535f, r.At(0, 0, 1), 6);
        Assert.Equal(1f, r.At(0, 0, 2));
        Assert.Equal(16, r.Source.BitDepth);
        Assert.False(r.Source.IsFloat);
    }

    [Fact]
    public void Float32CubeKeepsItsThreePlanes()
    {
        byte[] fits = FitsTestData.Build(4, 3, 3, (x, y, p) => 0.1 * (p + 1) + 0.01 * x, bitpix: -32);
        Raster r = Load(fits);

        Assert.True(r.IsColor);
        Assert.Equal(4, r.W);
        Assert.Equal(3, r.H);
        Assert.Equal(0.1f, r.At(0, 0, 0), 6);
        Assert.Equal(0.2f + 0.02f, r.At(1, 1, 2), 6);
        Assert.Equal(0.3f + 0.03f, r.At(2, 2, 3), 6);
        Assert.Equal(32, r.Source.BitDepth);
        Assert.True(r.Source.IsFloat);
        Assert.Equal(3, r.Source.PlanesInFile);
        Assert.Equal("FITS", r.Source.Format);
        Assert.True(r.Source.FileBytes > 0);
    }

    [Fact]
    public void BottomUpRowOrderIsFlippedSoThatRowZeroIsTheTop()
    {
        // Siril scrie randurile de jos in sus: primul rand din fisier e cel de JOS al imaginii.
        byte[] fits = FitsTestData.Build2D(2, 3, (x, y) => y * 1000, bitpix: -32, bzero: 0,
            extraCards: new[] { FitsTestData.StringCard("ROWORDER", "BOTTOM-UP") });
        Raster r = Load(fits);

        Assert.Equal(1f, r.At(0, 0, 0));      // randul de sus al rasterului = ultimul rand din fisier (2000, taiat la 1)
        Assert.Equal(0f, r.At(0, 2, 0));      // randul de jos = primul rand din fisier
        Assert.Contains("ROWORDER", r.Source.Note);
    }

    [Fact]
    public void WithoutRowOrderTheFileOrderIsKept()
    {
        byte[] fits = FitsTestData.Build2D(2, 3, (x, y) => y * 0.1, bitpix: -32);
        Raster r = Load(fits);

        Assert.Equal(0f, r.At(0, 0, 0));
        Assert.Equal(0.2f, r.At(0, 2, 0), 6);
        Assert.Null(r.Source.Note);
    }

    [Fact]
    public void FloatValuesOutsideUnitRangeAreClippedAndNoted()
    {
        byte[] fits = FitsTestData.Build2D(3, 1, (x, y) => x == 0 ? -0.5 : x == 1 ? 0.5 : 1.5, bitpix: -32);
        Raster r = Load(fits);

        Assert.Equal(0f, r.At(0, 0, 0));
        Assert.Equal(0.5f, r.At(0, 0, 1));
        Assert.Equal(1f, r.At(0, 0, 2));
        Assert.Contains("2 valori", r.Source.Note);
    }

    [Fact]
    public void FourPlanesAreRefusedWithAMessage()
    {
        byte[] fits = FitsTestData.Build(2, 2, 4, (x, y, p) => 0.5, bitpix: -32);

        var e = Assert.Throws<Exception>(() => Load(fits));
        Assert.Contains("4 planuri", e.Message);
    }

    [Fact]
    public void AnEmptyPrimaryHduIsSkipped()
    {
        byte[] image = FitsTestData.Build2D(2, 2, (x, y) => 0.25, bitpix: -32);
        Raster r = Load(FitsTestData.WithEmptyPrimary(image));

        Assert.Equal(2, r.W);
        Assert.Equal(0.25f, r.At(0, 1, 1));
    }

    [Fact]
    public void AStringCardWithASlashKeepsTheSlash()
    {
        byte[] fits = FitsTestData.Build2D(1, 1, (x, y) => 0.5, bitpix: -32,
            extraCards: new[] { FitsTestData.StringCard("OBJECT", "M 42 / core", "comentariu") });

        using var ms = new MemoryStream(fits);
        var (_, hdr, _, _, _) = FitsReader.LoadFrom(ms);

        Assert.Equal("M 42 / core", hdr["OBJECT"]);
    }

    [Fact]
    public void Int8AndInt32AreScaledByTheirOwnRange()
    {
        Raster r8 = Load(FitsTestData.Build2D(2, 1, (x, y) => x == 0 ? 0 : 255, bitpix: 8));
        Assert.Equal(0f, r8.At(0, 0, 0));
        Assert.Equal(1f, r8.At(0, 0, 1));

        Raster r32 = Load(FitsTestData.Build2D(2, 1, (x, y) => x == 0 ? 0 : 4294967295.0, bitpix: 32));
        Assert.Equal(0f, r32.At(0, 0, 0));
        Assert.Equal(1f, r32.At(0, 0, 1), 6);
        Assert.Equal(32, r32.Source.BitDepth);
        Assert.False(r32.Source.IsFloat);
    }

    [Fact]
    public void ATruncatedFileIsAnErrorNotGarbage()
    {
        byte[] fits = FitsTestData.Build2D(64, 64, (x, y) => 0.5, bitpix: -32);
        byte[] cut = fits.Take(fits.Length - 3000).ToArray();

        var e = Assert.Throws<Exception>(() => Load(cut));
        Assert.Contains("trunchiat", e.Message);
    }

    [Fact]
    public void HandlesOnlyFitsExtensions()
    {
        var f = new FitsReader();

        Assert.True(f.Handles("a.fit"));
        Assert.True(f.Handles("a.FITS"));
        Assert.True(f.Handles("a.fts"));
        Assert.False(f.Handles("a.tif"));
        Assert.False(f.Handles(""));
    }
}
