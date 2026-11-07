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

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DarksCheck.Core;
using DarksCheck.Tests;

namespace DarksCheck.Wpf.Tests;

/// <summary>A byte raster becomes a bitmap with no value conversion: the map the window shows is the PNG's.</summary>
public class RasterBitmapsTests
{
    [Fact]
    public void GreyKeepsEveryByteWhereItWas()
    {
        byte[] grey = [0, 17, 128, 255, 3, 200];

        BitmapSource b = RasterBitmaps.Grey(grey, 3, 2);

        Assert.Equal(PixelFormats.Gray8, b.Format);
        Assert.Equal((3, 2), (b.PixelWidth, b.PixelHeight));
        Assert.True(b.IsFrozen);
        byte[] back = new byte[6];
        b.CopyPixels(back, 3, 0);
        Assert.Equal(grey, back);
    }
}

/// <summary>
/// The colour of a verdict word is the first thing the eye reads; a mistake here shows a
/// warning green. The tests are on the ordering, not on RGB values.
/// </summary>
public class VerdictPaletteTests
{
    [Theory]
    [InlineData("OK", "OK (informativ)")]
    [InlineData("ATENTIE", "ATENTIE: ceva")]
    [InlineData("NEMASURAT", "")]
    [InlineData("REFA", "EROARE")]
    [InlineData("REDO: the camera", "REFA: aparatul")]
    public void WordsOfTheSameSeverityShareTheColour(string a, string b)
        => Assert.Equal(VerdictPalette.For(a), VerdictPalette.For(b));

    [Fact]
    public void TheFourVerdictWordsAndTheErrorGetFourDistinctColours()
    {
        Color[] colours = [VerdictPalette.For("OK"), VerdictPalette.For("ATENTIE"), VerdictPalette.For("REFA"), VerdictPalette.For("EROARE"), VerdictPalette.For("NEMASURAT")];
        Assert.Equal(4, colours.Distinct().Count());
        Assert.Equal(VerdictPalette.Good, colours[0]);
        Assert.Equal(VerdictPalette.Warn, colours[1]);
        Assert.Equal(VerdictPalette.Bad, colours[2]);
        Assert.Equal(VerdictPalette.Bad, colours[3]);
        Assert.Equal(VerdictPalette.Neutral, colours[4]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NEMASURAT: fara bias-uri")]
    public void InconclusiveVerdictsAreNeutralNotRed(string? word) => Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For(word));

    [Fact]
    public void TheVerdictIsReadCaseInsensitiveAndTrimmed() => Assert.Equal(VerdictPalette.Good, VerdictPalette.For("  ok "));

    [Fact]
    public void AnUnknownWordIsFair() => Assert.Equal(VerdictPalette.Fair, VerdictPalette.For("ceva"));

    [Fact]
    public void TheMatchVerdictsFollowTheSameOrder()
    {
        Assert.Equal(VerdictPalette.Good, VerdictPalette.For(MatchVerdict.Direct));
        Assert.Equal(VerdictPalette.Warn, VerdictPalette.For(MatchVerdict.WithOpt));
        Assert.Equal(VerdictPalette.Warn, VerdictPalette.For(MatchVerdict.CosmeticOnly));
        Assert.Equal(VerdictPalette.Bad, VerdictPalette.For(MatchVerdict.Unmatched));
        Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For(MatchVerdict.Unmeasured));
    }

    [Fact]
    public void BrushesAreFrozenSoTheyCanCrossThreads()
    {
        Brush b = VerdictPalette.BrushFor("OK");
        Assert.True(b.IsFrozen);
        Assert.Equal(VerdictPalette.Good, Assert.IsType<SolidColorBrush>(b).Color);
    }
}

public class ConverterTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Fact]
    public void BoolToVisibilityShowsOnTrueAndCollapsesOtherwise()
    {
        BoolToVisibilityConverter c = new();
        Assert.Equal(Visibility.Visible, c.Convert(true, typeof(Visibility), null!, Inv));
        Assert.Equal(Visibility.Collapsed, c.Convert(false, typeof(Visibility), null!, Inv));
        Assert.Equal(Visibility.Collapsed, c.Convert(null!, typeof(Visibility), null!, Inv));
    }

    [Fact]
    public void InverseBoolToVisibilityIsTheOpposite()
    {
        InverseBoolToVisibilityConverter c = new();
        Assert.Equal(Visibility.Collapsed, c.Convert(true, typeof(Visibility), null!, Inv));
        Assert.Equal(Visibility.Visible, c.Convert(false, typeof(Visibility), null!, Inv));
    }

    [Fact]
    public void InverseBoolFlips()
    {
        InverseBoolConverter c = new();
        Assert.Equal(false, c.Convert(true, typeof(bool), null!, Inv));
        Assert.Equal(true, c.Convert(false, typeof(bool), null!, Inv));
    }

    [Fact]
    public void NotNullToVisibilityHidesNullAndEmpty()
    {
        NotNullToVisibilityConverter c = new();
        Assert.Equal(Visibility.Collapsed, c.Convert(null!, typeof(Visibility), null!, Inv));
        Assert.Equal(Visibility.Collapsed, c.Convert("", typeof(Visibility), null!, Inv));
        Assert.Equal(Visibility.Visible, c.Convert("x", typeof(Visibility), null!, Inv));
    }

    [Fact]
    public void VerdictToBrushUsesThePalette()
    {
        VerdictToBrushConverter c = new();
        SolidColorBrush b = Assert.IsType<SolidColorBrush>(c.Convert("ATENTIE", typeof(Brush), null!, Inv));
        Assert.Equal(VerdictPalette.Warn, b.Color);
    }
}

/// <summary>The folder from the command line: an existing folder is taken, anything else is ignored and the window asks (R-070).</summary>
public class FolderFromArgsTests
{
    [Fact]
    public void NoArgumentsMeansNoFolder()
    {
        Assert.Null(App.FolderFromArgs(null));
        Assert.Null(App.FolderFromArgs([]));
    }

    [Fact]
    public void AnExistingFolderIsTaken()
    {
        using TempWorkspace ws = new();
        Assert.Equal(ws.Dir, App.FolderFromArgs(["--x", ws.Dir]));
    }

    [Fact]
    public void AMissingFolderIsIgnoredSoTheWindowAsks()
    {
        using TempWorkspace ws = new();
        Assert.Null(App.FolderFromArgs([Path.Combine(ws.Dir, "nu-exista"), ""]));
    }
}

/// <summary>The report rows the window binds (R-071): the verdict word is read off the value, the rest follows it.</summary>
public class ReportRowTests
{
    [Theory]
    [InlineData("OK: scadere directa (raport 1.00)", "OK", "scadere directa (raport 1.00)")]
    [InlineData("OK (informativ): 20 pixeli", "OK", "(informativ): 20 pixeli")]
    [InlineData("ATENTIE: doar 4 cadre", "ATENTIE", "doar 4 cadre")]
    [InlineData("NEMASURAT", "NEMASURAT", "")]
    [InlineData("NEMASURAT: fara bias-uri", "NEMASURAT", "fara bias-uri")]
    [InlineData("OKAY, not a verdict", null, "OKAY, not a verdict")]
    [InlineData("pas 2 ADU langa negru", null, "pas 2 ADU langa negru")]
    public void TheVerdictWordAndTheRestAreReadOffTheValue(string value, string? verdict, string rest)
    {
        ReportRow row = new("x", value);
        Assert.Equal(verdict, row.Verdict);
        Assert.Equal(rest, row.Rest);
    }

    /// <summary>The warnings about failed frames are labelled ATENTIE with the count as the value: the label is the chip.</summary>
    [Fact]
    public void AnAttentionLabelIsTheChipAndTheValueStaysWhole()
    {
        ReportRow row = new("ATENTIE", "2 lights nu s-au putut decoda (primul: x.fit: rupt)");
        Assert.Equal("ATENTIE", row.Verdict);
        Assert.Equal(row.Value, row.Rest);
    }
}
