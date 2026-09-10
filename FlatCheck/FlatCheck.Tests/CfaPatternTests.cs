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

namespace FlatCheck.Tests;

/// <summary>
/// The mosaic order is read from the file and never guessed, so what is pinned here is
/// the decoding: the dcraw FC macro, the crop offsets, and the one trap that a plausible
/// implementation falls into - libraw's second green can sit at cdesc index 3, not 1.
/// </summary>
public class CfaPatternTests
{
    // ---------------------------------------------------------------- libraw masks

    /// <summary>The four textbook Bayer masks, as dcraw and libraw define them.</summary>
    [Theory]
    [InlineData(0x94949494u, "RGGB")]
    [InlineData(0x16161616u, "BGGR")]
    [InlineData(0x61616161u, "GRBG")]
    [InlineData(0x49494949u, "GBRG")]
    public void DecodesTheTextbookMasks(uint filters, string expected)
        => Assert.Equal(expected, CfaPattern.FromLibRaw(filters, "RGBG", 0, 0).Letters);

    /// <summary>
    /// The case this decoder exists for. Measured on a real Nikon D3300 NEF: libraw
    /// reports filters=0xB4B4B4B4 with cdesc="RGBG", and FC gives 0, 1, *3*, 2 - the
    /// second green is index 3. A decoder that assumes 1 = G reads that position as
    /// something else and mislabels half the mosaic, confidently.
    /// </summary>
    [Fact]
    public void DecodesTheNikonMaskWhoseSecondGreenIsCdescIndexThree()
    {
        var p = CfaPattern.FromLibRaw(0xB4B4B4B4u, "RGBG", 0, 0);

        Assert.Equal("RGGB", p.Letters);
        Assert.Equal("libraw", p.Source);
        Assert.Equal(new[] { "R", "G1", "G2", "B" }, new[] { p.Name(0), p.Name(1), p.Name(2), p.Name(3) });
    }

    /// <summary>
    /// The visible area is cropped off the sensor, so the pattern must be evaluated at
    /// that origin. An odd margin shifts the mosaic phase by one position - which would
    /// swap R and G with no sign that anything went wrong.
    /// </summary>
    [Theory]
    [InlineData(0, 0, "RGGB")]
    [InlineData(1, 0, "GBRG")]
    [InlineData(0, 1, "GRBG")]
    [InlineData(1, 1, "BGGR")]
    [InlineData(2, 2, "RGGB")]
    public void TheCropOriginShiftsTheMosaicPhase(int row, int col, string expected)
        => Assert.Equal(expected, CfaPattern.FromLibRaw(0x94949494u, "RGBG", row, col).Letters);

    /// <summary>Masks that mean "not a 2x2 Bayer mosaic" are refused, not decoded into nonsense.</summary>
    [Theory]
    [InlineData(0u)]    // no CFA at all: mono, Foveon
    [InlineData(1u)]    // Leaf Catchlight
    [InlineData(9u)]    // X-Trans, a 6x6 mosaic
    public void RefusesTheNonBayerMasks(uint filters)
        => Assert.Null(CfaPattern.FromLibRaw(filters, "RGBG", 0, 0));

    [Theory]
    [InlineData("RGB")]
    [InlineData("")]
    [InlineData(null)]
    public void RefusesAnUnusableCdesc(string cdesc)
        => Assert.Null(CfaPattern.FromLibRaw(0x94949494u, cdesc, 0, 0));

    // ---------------------------------------------------------------- Parse

    [Theory]
    [InlineData("RGGB")]
    [InlineData("BGGR")]
    [InlineData("GRBG")]
    [InlineData("GBRG")]
    public void ParsesTheFourBayerOrders(string letters)
        => Assert.Equal(letters, CfaPattern.Parse(letters, "BAYERPAT").Letters);

    /// <summary>Siril writes it upper case, but a lenient read costs nothing.</summary>
    [Fact]
    public void ParseIsCaseInsensitiveAndTrims()
    {
        Assert.Equal("RGGB", CfaPattern.Parse("rggb", "BAYERPAT").Letters);
        Assert.Equal("RGGB", CfaPattern.Parse(" RGGB ", "BAYERPAT").Letters);
    }

    /// <summary>
    /// Anything that is not a Bayer mosaic with exactly two greens is "unknown", which is
    /// the honest answer. A CFA with a white filter, or a truncated card, must not be
    /// bent into something that looks measured.
    /// </summary>
    [Theory]
    [InlineData("RGGG")]
    [InlineData("RGB")]
    [InlineData("RGBW")]
    [InlineData("RGGBG")]
    [InlineData("")]
    [InlineData(null)]
    public void RefusesAnythingThatIsNotABayerOrder(string letters)
        => Assert.Null(CfaPattern.Parse(letters, "BAYERPAT"));

    [Fact]
    public void ParseKeepsTheSourceForTheReport()
        => Assert.Equal("BAYERPAT", CfaPattern.Parse("RGGB", "BAYERPAT").Source);

    // ---------------------------------------------------------------- Shift

    [Theory]
    [InlineData(1, 0, "GBRG")]
    [InlineData(0, 1, "GRBG")]
    [InlineData(1, 1, "BGGR")]
    [InlineData(2, 2, "RGGB")]
    [InlineData(0, 0, "RGGB")]
    public void ShiftMovesThePhase(int dy, int dx, string expected)
        => Assert.Equal(expected, CfaPattern.Parse("RGGB", "BAYERPAT").Shift(dy, dx).Letters);

    [Fact]
    public void ShiftKeepsTheSource()
        => Assert.Equal("BAYERPAT", CfaPattern.Parse("RGGB", "BAYERPAT").Shift(1, 1).Source);

    // ---------------------------------------------------------------- names

    /// <summary>
    /// The two greens have to be told apart on screen: on a correct mosaic they sit at
    /// almost the same level, and "almost" is exactly what the histogram shows.
    /// </summary>
    [Theory]
    [InlineData("RGGB", "R", "G1", "G2", "B")]
    [InlineData("GRBG", "G1", "R", "B", "G2")]
    [InlineData("BGGR", "B", "G1", "G2", "R")]
    [InlineData("GBRG", "G1", "B", "R", "G2")]
    public void NamesTheFourPositions(string letters, string n0, string n1, string n2, string n3)
    {
        var p = CfaPattern.Parse(letters, "BAYERPAT");

        Assert.Equal(new[] { n0, n1, n2, n3 }, new[] { p.Name(0), p.Name(1), p.Name(2), p.Name(3) });
    }
}
