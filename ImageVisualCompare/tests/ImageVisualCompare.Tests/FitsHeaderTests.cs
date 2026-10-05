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
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

public class FitsHeaderTests
{
    private static FitsImageLayout Read(byte[] file) => FitsHeader.ReadLayout(new MemoryStream(file));

    [Fact]
    public void R014_IsFits_GoesByTheFirstBytesNotByTheName()
    {
        byte[] fits = FitsFixture.Image(8, 1, 1, 1, [0]);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02];

        Assert.True(FitsHeader.IsFits(new MemoryStream(fits)));
        Assert.False(FitsHeader.IsFits(new MemoryStream(png)));
        Assert.False(FitsHeader.IsFits(new MemoryStream([.. "SIMPLE"u8])));
        Assert.False(FitsHeader.IsFits(new MemoryStream()));
    }

    [Theory]
    [InlineData(8, 1, false, 8)]
    [InlineData(16, 3, false, 16)]
    [InlineData(32, 1, false, 32)]
    [InlineData(-32, 3, true, 32)]
    [InlineData(-64, 1, true, 64)]
    public void R014_ReadLayout_ReadsTheSizeThePlanesAndTheSampleType(int bitPix, int planes, bool isFloat, int bits)
    {
        FitsImageLayout layout = Read(FitsFixture.Image(bitPix, 5, 3, planes, new double[5 * 3 * planes]));

        Assert.Equal(5, layout.Width);
        Assert.Equal(3, layout.Height);
        Assert.Equal(planes, layout.Planes);
        Assert.Equal(bitPix, layout.BitPix);
        Assert.Equal(isFloat, layout.IsFloat);
        Assert.Equal(bits, layout.BitsPerChannel);
        Assert.Equal(2880, layout.DataOffset);
        Assert.Equal(5L * 3 * planes * bits / 8, layout.DataBytes);
    }

    [Fact]
    public void R014_ReadLayout_ReadsBZeroAndBScale_AndDefaultsThemToZeroAndOne()
    {
        FitsImageLayout unsigned = Read(FitsFixture.Image(
            16, 1, 1, 1, [0], FitsFixture.Card("BZERO", "32768."), FitsFixture.Card("BSCALE", "1.0D0")));
        FitsImageLayout plain = Read(FitsFixture.Image(16, 1, 1, 1, [0]));

        Assert.Equal(32768, unsigned.BZero);
        Assert.Equal(1, unsigned.BScale);
        Assert.Equal(0, plain.BZero);
        Assert.Equal(1, plain.BScale);
    }

    [Fact]
    public void R014_ReadLayout_AThirdAxisOfOneIsOnePlane()
    {
        byte[] file =
        [
            .. FitsFixture.Header(
            [
                FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 3),
                FitsFixture.Card("NAXIS1", 2), FitsFixture.Card("NAXIS2", 2), FitsFixture.Card("NAXIS3", 1),
            ]),
            .. FitsFixture.Data(8, [1, 2, 3, 4]),
        ];

        Assert.Equal(1, Read(file).Planes);
    }

    [Fact]
    public void R014_ReadLayout_StepsOverAnEmptyPrimaryAndATableToTheFirstImage()
    {
        byte[] file =
        [
            .. FitsFixture.Header([FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 0)]),
            .. Table(),
            .. FitsFixture.Header([FitsFixture.Text("XTENSION", "IMAGE"), .. FitsFixture.ImageCards(16, 4, 2, 1)]),
            .. FitsFixture.Data(16, new double[8]),
        ];

        FitsImageLayout layout = Read(file);

        Assert.Equal(4, layout.Width);
        Assert.Equal(2, layout.Height);
        Assert.Equal(16, layout.BitPix);

        // The empty primary's header, the table's header and its one block of rows, then the
        // image's header: the samples start in the fifth block.
        Assert.Equal(4 * 2880, layout.DataOffset);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("BOTTOM-UP", true)]
    [InlineData("TOP-DOWN", false)]
    public void R017_RowOrder_IsBottomUpUnlessTheFileSaysTopDown(string? rowOrder, bool bottomUp)
    {
        string[] cards = rowOrder is null ? [] : [FitsFixture.Text("ROWORDER", rowOrder)];

        FitsImageLayout layout = Read(FitsFixture.Image(8, 2, 4, 1, new double[8], cards));

        Assert.Equal(bottomUp, layout.BottomUp);
        Assert.Equal(bottomUp ? 3 : 0, layout.DisplayRow(0));
        Assert.Equal(bottomUp ? 0 : 3, layout.DisplayRow(3));
    }

    [Fact]
    public void R018_Describe_ReportsTheDepthAndThePlanes()
    {
        FitsImageLayout rgb = Read(FitsFixture.Image(-32, 2, 2, 3, new double[12]));
        FitsImageLayout gray = Read(FitsFixture.Image(16, 2, 2, 1, new double[4]));

        Assert.Equal("32-bit float RGB", rgb.Describe(@"C:\a.fit").FormatDescription);
        Assert.Equal("16-bit gray", gray.Describe(@"C:\b.fit").FormatDescription);
        Assert.Equal(
            "32-bit float RGB (0..65535)",
            rgb.Describe(@"C:\a.fit", FitsScale.SixteenBitRange).FormatDescription);
        Assert.Equal("2 × 2", rgb.Describe(@"C:\a.fit").Dimensions);
    }

    [Fact]
    public void R019_AFileWithNoImage_IsRefusedWithItsOwnMessage()
    {
        byte[] emptyPrimary = FitsFixture.Header(
            [FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 0)]);

        var onlyPrimary = Assert.Throws<NotSupportedException>(() => Read(emptyPrimary));
        var onlyATable = Assert.Throws<NotSupportedException>(() => Read([.. emptyPrimary, .. Table()]));

        Assert.Equal("FITS: no image found in the file.", onlyPrimary.Message);
        Assert.Equal("FITS: no image found in the file.", onlyATable.Message);
    }

    [Fact]
    public void R019_FourAxes_AreRefused()
    {
        byte[] file = FitsFixture.Header(
        [
            FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 4),
            FitsFixture.Card("NAXIS1", 2), FitsFixture.Card("NAXIS2", 2), FitsFixture.Card("NAXIS3", 3),
            FitsFixture.Card("NAXIS4", 2),
        ]);

        var ex = Assert.Throws<NotSupportedException>(() => Read(file));

        Assert.Equal("FITS: an image with 4 axes is not read; 2, or 3 with 1 or 3 planes, are.", ex.Message);
    }

    [Fact]
    public void R019_TwoPlanes_AreRefused()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Read(FitsFixture.Image(8, 2, 2, 2, new double[8])));

        Assert.Equal("FITS: an image with 2 planes is not read; 1 or 3 are.", ex.Message);
    }

    [Fact]
    public void R019_ASampleTypeOutsideTheFive_IsRefused()
    {
        byte[] file = FitsFixture.Header([FitsFixture.Card("SIMPLE", "T"), .. FitsFixture.ImageCards(64, 2, 2, 1)]);

        var ex = Assert.Throws<NotSupportedException>(() => Read(file));

        Assert.Equal("FITS: BITPIX 64 is not read.", ex.Message);
    }

    [Fact]
    public void R019_ATileCompressedImage_IsRefused()
    {
        byte[] file =
        [
            .. FitsFixture.Header([FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 0)]),
            .. Table(FitsFixture.Card("ZIMAGE", "T")),
        ];

        var ex = Assert.Throws<NotSupportedException>(() => Read(file));

        Assert.Equal("FITS: a tile-compressed image is not read.", ex.Message);
    }

    [Fact]
    public void R019_AHeaderCutShort_IsRefused()
    {
        byte[] file = FitsFixture.Image(8, 2, 2, 1, new double[4])[..1000];

        var ex = Assert.Throws<EndOfStreamException>(() => Read(file));

        Assert.Equal("FITS: the header ends before its END card.", ex.Message);
    }

    [Fact]
    public void R019_DataCutShort_IsFoundFromTheLengthBeforeAnySampleIsRead()
    {
        // 100 x 100 floats in three planes are 120 000 bytes after a 2880-byte header.
        byte[] file = FitsFixture.Image(-32, 100, 100, 3, new double[30000])[..10000];

        var ex = Assert.Throws<EndOfStreamException>(() => Read(file));

        Assert.Equal("FITS: the file ends before the image data does.", ex.Message);
    }

    /// <summary>
    /// What reading the file throws, or null. On a worker with a time limit, because the
    /// defects these tests were written for did not throw: they never returned.
    /// </summary>
    private static Exception? Failure(byte[] file)
    {
        Task<Exception?> reading = Task.Run(() =>
        {
            try
            {
                Read(file);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        Assert.True(reading.Wait(TimeSpan.FromSeconds(5)), "ReadLayout did not return within 5 s");
        return reading.Result;
    }

    private static byte[] EmptyPrimary() => FitsFixture.Header(
        [FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 0)]);

    [Fact]
    public void R019_ANegativeAxis_IsAnInvalidHeader_NotAnEndlessLoop()
    {
        // 8 bits x -2 x 2880 is -5760 bytes of data: stepping over it lands back on this header.
        byte[] file =
        [
            .. FitsFixture.Header(
            [
                FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 2),
                FitsFixture.Card("NAXIS1", -2), FitsFixture.Card("NAXIS2", 2880),
            ]),
            .. new byte[2880],
        ];

        Exception? ex = Failure(file);

        Assert.IsType<NotSupportedException>(ex);
        Assert.Equal("FITS: the header is not valid.", ex.Message);
    }

    [Fact]
    public void R019_ANegativeHeapSize_IsAnInvalidHeader_NotAnEndlessLoop()
    {
        byte[] file = [.. EmptyPrimary(), .. Table(FitsFixture.Card("PCOUNT", -5790))];

        Exception? ex = Failure(file);

        Assert.IsType<NotSupportedException>(ex);
        Assert.Equal("FITS: the header is not valid.", ex.Message);
    }

    [Fact]
    public void R019_ANegativeAxisThatPointsBeforeTheFile_IsAnInvalidHeader()
    {
        byte[] file = FitsFixture.Header(
        [
            FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 2),
            FitsFixture.Card("NAXIS1", -1), FitsFixture.Card("NAXIS2", 99999999),
        ]);

        Exception? ex = Failure(file);

        Assert.IsType<NotSupportedException>(ex);
        Assert.Equal("FITS: the header is not valid.", ex.Message);
    }

    [Fact]
    public void R019_MoreAxesThanTheStandardAllows_IsAnInvalidHeader_NotAnEndlessLoop()
    {
        byte[] file = FitsFixture.Header(
        [
            FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 2147483647),
            FitsFixture.Card("NAXIS1", 0),
        ]);

        Exception? ex = Failure(file);

        Assert.IsType<NotSupportedException>(ex);
        Assert.Equal("FITS: the header is not valid.", ex.Message);
    }

    [Theory]
    [InlineData(1073741824, 1073741824)]
    [InlineData(2147483647, 2147483647)]
    [InlineData(300000000, 2)]
    public void R019_AnImageWhoseSizeOverflows_IsTooLarge_NotAFileThatPassesTheLengthCheck(int width, int height)
    {
        // 1073741824 x 1073741824 doubles are 2^63 bytes: as a signed 64-bit count, negative.
        byte[] file = FitsFixture.Header([FitsFixture.Card("SIMPLE", "T"), .. FitsFixture.ImageCards(-64, width, height, 1)]);

        Exception? ex = Failure(file);

        Assert.IsType<NotSupportedException>(ex);
        Assert.Equal($"FITS: an image of {width} × {height} is too large to show.", ex.Message);
    }

    [Fact]
    public void R019_BytesAfterTheLastUnit_AreNotAHeaderCutShort()
    {
        byte[] strayBytes = [.. EmptyPrimary(), .. new byte[100]];
        byte[] zeroBlock = [.. EmptyPrimary(), .. new byte[2880]];

        Assert.Equal("FITS: no image found in the file.", Failure(strayBytes)?.Message);
        Assert.Equal("FITS: no image found in the file.", Failure(zeroBlock)?.Message);
    }

    [Fact]
    public void R019_ATableHeapLargerThanTheFile_IsNotSteppedOverAsIfItWereEmpty()
    {
        // 3 000 000 000 does not fit 32 bits; read as 0, the image after the table is "found".
        byte[] file =
        [
            .. EmptyPrimary(),
            .. Table(FitsFixture.Card("PCOUNT", 3000000000)),
            .. FitsFixture.Header([FitsFixture.Text("XTENSION", "IMAGE"), .. FitsFixture.ImageCards(8, 2, 2, 1)]),
            .. FitsFixture.Data(8, [1, 2, 3, 4]),
        ];

        Assert.Equal("FITS: no image found in the file.", Failure(file)?.Message);
    }

    [Fact]
    public void R014_ReadLayout_StepsOverRandomGroups_WhoseFirstAxisIsZero()
    {
        // Random groups: NAXIS1 is 0 and does not count; 100 x 100 bytes are four blocks.
        byte[] file =
        [
            .. FitsFixture.Header(
            [
                FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 3),
                FitsFixture.Card("NAXIS1", 0), FitsFixture.Card("NAXIS2", 100), FitsFixture.Card("NAXIS3", 100),
                FitsFixture.Card("GROUPS", "T"), FitsFixture.Card("PCOUNT", 0), FitsFixture.Card("GCOUNT", 1),
            ]),
            .. new byte[4 * 2880],
            .. FitsFixture.Header([FitsFixture.Text("XTENSION", "IMAGE"), .. FitsFixture.ImageCards(8, 2, 2, 1)]),
            .. FitsFixture.Data(8, [1, 2, 3, 4]),
        ];

        Assert.Equal(6 * 2880, Read(file).DataOffset);
    }

    [Fact]
    public void R014_ReadLayout_ReadsValuesThatCarryComments_AsEveryRealFileWritesThem()
    {
        byte[] file =
        [
            .. FitsFixture.Header(
            [
                Commented("SIMPLE", "T", "file does conform to FITS standard"),
                Commented("BITPIX", "16", "number of bits per data pixel"),
                Commented("NAXIS", "2", "number of data axes"),
                Commented("NAXIS1", "3", "length of data axis 1"),
                Commented("NAXIS2", "2", "length of data axis 2"),
                Commented("BZERO", "32768.", "offset to unsigned short, 1/2"),
                Commented("BSCALE", "1.", "default scaling factor"),
                "OBJECT  = 'M 31 / core'       / a slash inside the quotes is not a comment".PadRight(80),
                "ROWORDER= 'TOP-DOWN'           / Order of the rows in image array".PadRight(80),
            ]),
            .. FitsFixture.Data(16, new double[6]),
        ];

        FitsImageLayout layout = Read(file);

        Assert.Equal(3, layout.Width);
        Assert.Equal(2, layout.Height);
        Assert.Equal(16, layout.BitPix);
        Assert.Equal(32768, layout.BZero);
        Assert.Equal(1, layout.BScale);
        Assert.False(layout.BottomUp);
    }

    [Fact]
    public void R014_ReadLayout_ReadsAHeaderLongerThanOneBlock()
    {
        // 36 cards fill a block: with 50 HISTORY cards the row order and END are in the second.
        IEnumerable<string> history = Enumerable.Range(1, 50).Select(i => $"HISTORY step {i}".PadRight(80));
        byte[] file =
        [
            .. FitsFixture.Header(
            [
                FitsFixture.Card("SIMPLE", "T"), .. FitsFixture.ImageCards(8, 2, 2, 1), .. history,
                FitsFixture.Text("ROWORDER", "TOP-DOWN"),
            ]),
            .. FitsFixture.Data(8, [1, 2, 3, 4]),
        ];

        FitsImageLayout layout = Read(file);

        Assert.False(layout.BottomUp);
        Assert.Equal(2 * 2880, layout.DataOffset);
    }

    private static string Commented(string key, string value, string comment) =>
        (key.PadRight(8) + "= " + value.PadLeft(20) + " / " + comment).PadRight(80);

    /// <summary>A binary table of three 10-byte rows: two axes and 8 bits, as an image has, and not one.</summary>
    private static byte[] Table(params string[] cards) =>
    [
        .. FitsFixture.Header(
        [
            FitsFixture.Text("XTENSION", "BINTABLE"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 2),
            FitsFixture.Card("NAXIS1", 10), FitsFixture.Card("NAXIS2", 3), FitsFixture.Card("PCOUNT", 0),
            FitsFixture.Card("GCOUNT", 1), .. cards,
        ]),
        .. new byte[2880],
    ];
}
