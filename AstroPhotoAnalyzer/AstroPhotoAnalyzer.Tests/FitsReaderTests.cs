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
    /// <summary>R-013: a NaN in a float FITS is a clipped value, 0, and counted; it never reaches the raster.</summary>
    [Fact]
    public void R013_ANaNSampleIsClippedToZeroAndCounted()
    {
        byte[] fits = FitsTestData.Build(8, 4, 1, (x, y, p) => x == 3 && y == 1 ? double.NaN : 0.25, bitpix: -32);

        var (raster, _, _, _, note) = FitsReader.LoadFrom(new MemoryStream(fits));

        Assert.Equal(0f, raster.At(0, 2, 3));     // the file's row 1 is the raster's row 4 - 1 - 1 (ADR 0049)
        Assert.Equal(0.25f, raster.At(0, 0, 0));
        Assert.All(raster.Planes[0], v => Assert.False(float.IsNaN(v)));
        Assert.Contains("1 valori in afara [0, 1] au fost taiate", note);
    }

    /// <summary>
    /// CR-004: a float FITS on a 0 to 65535 scale was clipped to 1 everywhere and analysed as a
    /// white frame, the count an Ok-level note. More than half of the samples over 1 is a file
    /// on another scale: refused, with the share and the largest value.
    /// </summary>
    [Fact]
    public void R013_AFitsWithMostValuesOverOneIsRefusedNotReadAsWhite()
    {
        byte[] fits = FitsTestData.Build(64, 64, 3, (x, y, p) => 1000 + 60000.0 * x / 64, bitpix: -32);

        var e = Assert.Throws<RefusedImageException>(() => FitsReader.LoadFrom(new MemoryStream(fits)));

        Assert.Equal("valorile din fisier nu sunt intre 0 si 1: 100.0 % din ele trec de 1 (cea mai mare e 60062.5). "
            + "Unealta citeste un FITS pe o scara de la 0 la 1. Salveaza-l din Siril pe 32 de biti pe scara 0..1, sau pe 16 biti, si analizeaza fisierul acela.", e.Message);
    }

    /// <summary>
    /// R-013: the refusal starts over half of the samples; up to half, the values over 1 are
    /// clipped and noted as before (a highlight a process pushed past 1 is no other scale).
    /// </summary>
    [Fact]
    public void R013_UpToHalfOfTheValuesOverOneAreStillClippedAndNoted()
    {
        // 32 samples: 16 over 1 is half, kept; 17 is over half, refused.
        byte[] half = FitsTestData.Build(8, 4, 1, (x, y, p) => y * 8 + x < 16 ? 1.5 : 0.25, bitpix: -32);
        byte[] over = FitsTestData.Build(8, 4, 1, (x, y, p) => y * 8 + x < 17 ? 1.5 : 0.25, bitpix: -32);

        var (raster, _, _, _, note) = FitsReader.LoadFrom(new MemoryStream(half));

        Assert.Equal(16, raster.Planes[0].Count(v => v == 1f));
        Assert.Contains("16 valori in afara [0, 1] au fost taiate", note);
        var e = Assert.Throws<RefusedImageException>(() => FitsReader.LoadFrom(new MemoryStream(over)));
        Assert.Contains("53.1 % din ele trec de 1 (cea mai mare e 1.5)", e.Message);
    }

    static Raster Load(byte[] fits, string name = "t.fit")
    {
        using var ws = new TempWorkspace();
        return new FitsReader().Load(ws.WriteFile(name, fits));
    }

    [Fact]
    public void Int16WithBzeroIsScaledToUnitRange()
    {
        // The physical values 0, 32768, 65535 - with the implicit BZERO 32768, stored as
        // -32768, 0, 32767.
        byte[] fits = FitsTestData.Build2D(3, 1, (x, y) => x == 0 ? 0 : x == 1 ? 32768 : 65535);
        Raster r = Load(fits);

        Assert.False(r.IsColor);
        Assert.Equal(0f, r.At(0, 0, 0));
        Assert.Equal(32768f / 65535f, r.At(0, 0, 1), 6);
        Assert.Equal(1f, r.At(0, 0, 2));
        Assert.Equal(16, r.Source.BitDepth);
        Assert.False(r.Source.IsFloat);
    }

    /// <summary>
    /// CR-007: .NET does not parse the D exponent the FITS standard allows, and an unparsed
    /// BZERO was read as its default, 0: the samples were taken as signed, everything from
    /// mid-scale up was clipped, and the report was about another image.
    /// </summary>
    [Fact]
    public void R011_ARealWithADExponentIsReadAsFitsWritesIt()
    {
        byte[] plain = FitsTestData.Build2D(3, 1, (x, y) => x == 0 ? 0 : x == 1 ? 32768 : 65535);
        byte[] withD = FitsTestData.WithCard(plain, "BZERO", "3.2768D+04");
        byte[] wholeReal = FitsTestData.WithCard(plain, "BITPIX", "16.0");

        Raster r = Load(withD);

        Assert.Equal(0f, r.At(0, 0, 0));
        Assert.Equal(32768f / 65535f, r.At(0, 0, 1), 6);
        Assert.Equal(1f, r.At(0, 0, 2));
        Assert.Null(r.Source.Note);
        // A whole number some writers put as a real stays readable.
        Assert.Equal(1f, Load(wholeReal).At(0, 0, 2));
    }

    /// <summary>
    /// CR-007: a number that is present and cannot be read refuses the file; before, it was read
    /// as its default or, through NumberStyles.Any, as another number ("0,5" as 5, "1,024" as
    /// 1024, "(5)" as -5).
    /// </summary>
    [Theory]
    [InlineData("BZERO", "3.2768X+04", "BZERO din header nu se poate citi ca numar: '3.2768X+04'")]
    [InlineData("BZERO", "(5)", "BZERO din header nu se poate citi ca numar: '(5)'")]
    [InlineData("BSCALE", "0,5", "BSCALE din header nu se poate citi ca numar: '0,5'")]
    [InlineData("NAXIS1", "1,024", "NAXIS1 din header nu se poate citi ca numar intreg: '1,024'")]
    [InlineData("BITPIX", "16.5", "BITPIX din header nu se poate citi ca numar intreg: '16.5'")]
    public void R011_AHeaderNumberThatCannotBeReadRefusesTheFile(string key, string value, string expected)
    {
        byte[] sound = FitsTestData.Build2D(3, 1, (x, y) => 20000 * x, bscale: 2);
        byte[] fits = FitsTestData.WithCard(sound, key, value);

        var e = Assert.ThrowsAny<Exception>(() => FitsReader.LoadFrom(new MemoryStream(fits)));

        Assert.Equal("FITS: " + expected + ".", e.Message);
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
    public void R011_BottomUpRowOrderIsFlippedSoThatRowZeroIsTheTop()
    {
        // Siril writes the rows bottom to top: the file's first row is the image's BOTTOM one.
        // The rows hold 0, 0.5 and 2000: one row over 1, a third of the samples, which is clipped
        // and noted; with two rows over 1 the file would be on another scale and refused (R-013;
        // the middle row was 1000 until 2026-11-14, changed at the owner's word, ADR 0096).
        byte[] fits = FitsTestData.Build2D(2, 3, (x, y) => y == 2 ? 2000 : y * 0.5, bitpix: -32, bzero: 0,
            extraCards: new[] { FitsTestData.StringCard("ROWORDER", "BOTTOM-UP") });
        Raster r = Load(fits);

        Assert.Equal(1f, r.At(0, 0, 0));      // top row = the file's last row (2000, clipped to 1)
        Assert.Equal(0f, r.At(0, 2, 0));      // the bottom row = the file's first row
        Assert.DoesNotContain("ROWORDER", r.Source.Note);   // the usual case: only the clipping is remarked
    }

    [Fact]
    public void R011_WithoutRowOrderTheRowsAreFlippedToo()
    {
        // Siril shows every FITS with the file's first row at the bottom, keyword or not (ADR 0049).
        byte[] fits = FitsTestData.Build2D(2, 3, (x, y) => y * 0.1, bitpix: -32);
        Raster r = Load(fits);

        Assert.Equal(0.2f, r.At(0, 0, 0), 6);
        Assert.Equal(0f, r.At(0, 2, 0));
        Assert.Null(r.Source.Note);
    }

    [Fact]
    public void R011_TopDownIsIgnoredAndNoted()
    {
        // A file saying TOP-DOWN over bottom-up data exists (a SyQon step in Siril writes one);
        // Siril ignores the keyword on display, so does the reader, and the report says so.
        byte[] fits = FitsTestData.Build2D(2, 3, (x, y) => y * 0.1, bitpix: -32,
            extraCards: new[] { FitsTestData.StringCard("ROWORDER", "TOP-DOWN") });
        Raster r = Load(fits);

        Assert.Equal(0.2f, r.At(0, 0, 0), 6);
        Assert.Equal(0f, r.At(0, 2, 0));
        Assert.Contains("TOP-DOWN", r.Source.Note);
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
