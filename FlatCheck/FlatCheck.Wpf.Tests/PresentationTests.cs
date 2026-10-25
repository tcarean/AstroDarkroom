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

namespace FlatCheck.Wpf.Tests;

/// <summary>A byte raster becomes a bitmap without any conversion of values.</summary>
public class RasterBitmapsTests
{
    [Fact]
    public void GreyKeepsEveryByteWhereItWas()
    {
        var grey = new byte[] { 0, 17, 128, 255, 3, 200 };

        BitmapSource b = RasterBitmaps.Grey(grey, 3, 2);

        Assert.Equal(PixelFormats.Gray8, b.Format);
        Assert.Equal((3, 2), (b.PixelWidth, b.PixelHeight));
        Assert.True(b.IsFrozen);
        var back = new byte[6];
        b.CopyPixels(back, 3, 0);
        Assert.Equal(grey, back);
    }

    [Fact]
    public void RgbKeepsTheChannelOrder()
    {
        var rgb = new byte[] { 10, 20, 30, 40, 50, 60 };

        BitmapSource b = RasterBitmaps.Rgb(rgb, 2, 1);

        Assert.Equal(PixelFormats.Rgb24, b.Format);
        Assert.Equal((2, 1), (b.PixelWidth, b.PixelHeight));
        var back = new byte[6];
        b.CopyPixels(back, 6, 0);
        Assert.Equal(rgb, back);
    }
}

/// <summary>
/// The colours and the converters. Small things, but a verdict's colour is the first thing
/// the user sees, and a mistake here shows green a flat that must be redone.
/// </summary>
public class VerdictPaletteTests
{
    /// <summary>
    /// The colour is DEDUCED from Stats.Severity. The test is written on the ordering, not on
    /// RGB values: if the palette changes tomorrow, the test must still pass, but if the order
    /// is overturned it must fail.
    /// </summary>
    [Theory]
    [InlineData("PERFECT", "OK")]
    [InlineData("OK", "PERFECT")]
    public void TheTwoVocabulariesShareTheColourForTheSameSeverity(string a, string b)
        => Assert.Equal(VerdictPalette.For(a), VerdictPalette.For(b));

    [Fact]
    public void TheFourBandsGetFourDistinctColours()
    {
        var colours = new[] { "PERFECT", "BUN", "ACCEPTABIL", "REFA" }
            .Select(VerdictPalette.For).ToList();

        Assert.Equal(4, colours.Distinct().Count());
    }

    [Fact]
    public void SuspectSharesTheWarningColourWithAcceptabil()
        => Assert.Equal(VerdictPalette.For("ACCEPTABIL"), VerdictPalette.For("SUSPECT"));

    [Fact]
    public void ProblemaSharesTheBadColourWithRefa()
        => Assert.Equal(VerdictPalette.For("REFA"), VerdictPalette.For("PROBLEMA"));

    /// <summary>
    /// "I do not know" is not "it is bad". In Stats both sit at a severity that exits non-zero,
    /// because there only the exit code matters; visually however they must differ,
    /// otherwise an inconclusive series looks like a broken flat.
    /// </summary>
    [Fact]
    public void InconclusiveVerdictsAreNeutralNotRed()
    {
        Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For(LightSeriesAnalyzer.Undetermined));
        Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For(Stats.NotApplicable));

        Assert.NotEqual(VerdictPalette.For("REFA"), VerdictPalette.For(Stats.NotApplicable));
        Assert.NotEqual(VerdictPalette.For("ACCEPTABIL"), VerdictPalette.For(Stats.NotApplicable));
    }

    /// <summary>
    /// An unknown verdict is treated as the worst - the same rule as in the core.
    /// </summary>
    [Fact]
    public void AnUnknownVerdictGetsTheWorstColour()
    {
        Assert.Equal(VerdictPalette.Bad, VerdictPalette.For("ceva-nou"));
        Assert.Equal(VerdictPalette.Bad, VerdictPalette.For(null));
    }

    [Fact]
    public void BrushesAreFrozenSoTheyCanCrossThreads()
    {
        Brush b = VerdictPalette.BrushFor("PERFECT");

        Assert.True(b.IsFrozen);
        Assert.Equal(VerdictPalette.Perfect, ((SolidColorBrush)b).Color);
    }
}

public class ConverterTests
{
    static object? Run(System.Windows.Data.IValueConverter c, object? v)
        => c.Convert(v, typeof(object), null!, CultureInfo.InvariantCulture);

    /// <summary>
    /// The map reaches the screen from memory, without a temporary file. The test uses a PNG
    /// produced by the tool's own encoder, so it also checks that the two understand each other.
    /// </summary>
    [Fact]
    public void PngBytesBecomeAnImageOfTheRightSize()
    {
        var img = new Img(7, 11);
        for (int i = 0; i < img.D.Length; i++) img.D[i] = 1.0 + i * 0.001;

        var bitmap = (BitmapImage)Run(new PngBytesToImageConverter(),
                                      PngWriter.EncodeResidualMap(img))!;

        Assert.Equal(11, bitmap.PixelWidth);
        Assert.Equal(7, bitmap.PixelHeight);
        Assert.True(bitmap.IsFrozen);
    }

    [Fact]
    public void NoBytesMeansNoImage()
    {
        var c = new PngBytesToImageConverter();

        Assert.Null(Run(c, null));
        Assert.Null(Run(c, Array.Empty<byte>()));
    }

    [Fact]
    public void OnlyTheFileNameIsShownInLists()
        => Assert.Equal("pp_flat_stacked_A.fit",
                        Run(new FileNameConverter(), @"C:\sesiune\process\pp_flat_stacked_A.fit"));

    [Fact]
    public void TheBoolConvertersAgreeOnWhatTrueMeans()
    {
        Assert.Equal(Visibility.Visible, Run(new BoolToVisibilityConverter(), true));
        Assert.Equal(Visibility.Collapsed, Run(new BoolToVisibilityConverter(), false));
        Assert.Equal(Visibility.Collapsed, Run(new InverseBoolToVisibilityConverter(), true));
        Assert.Equal(Visibility.Visible, Run(new InverseBoolToVisibilityConverter(), false));
        Assert.Equal(false, Run(new InverseBoolConverter(), true));
        Assert.Equal(true, Run(new InverseBoolConverter(), false));
    }

    /// <summary>
    /// The empty string counts as "nothing": the view-models clear the fields by setting "",
    /// not null, and a panel that stayed visible with empty text would leave an unexplained gap.
    /// </summary>
    [Fact]
    public void AnEmptyStringCountsAsNothingToShow()
    {
        var c = new NotNullToVisibilityConverter();

        Assert.Equal(Visibility.Collapsed, Run(c, null));
        Assert.Equal(Visibility.Collapsed, Run(c, ""));
        Assert.Equal(Visibility.Visible, Run(c, "ceva"));
    }

    // ---------------------------------------------------------------- the channels' colours

    /// <summary>
    /// The channels' colours are a second colour system on the same screen, so it is worth
    /// saying in the test what they are NOT: they do not go through Stats.Severity and do not
    /// mean a judgement. A red channel is not "worse" than a green one.
    /// </summary>
    [Fact]
    public void TheThreeChannelColoursAreDistinct()
    {
        Assert.NotEqual(ChannelPalette.For('R'), ChannelPalette.For('G'));
        Assert.NotEqual(ChannelPalette.For('G'), ChannelPalette.For('B'));
        Assert.NotEqual(ChannelPalette.For('R'), ChannelPalette.For('B'));
    }

    /// <summary>
    /// When the file did not give the order, every channel gets the same neutral colour: four
    /// curves coloured R/G/G/B on an unknown mosaic would look exactly like a measurement.
    /// </summary>
    [Theory]
    [InlineData('\0')]
    [InlineData('W')]
    [InlineData('r')]
    public void AnUnknownColourIsNeutral(char c)
        => Assert.Equal(ChannelPalette.Unknown, ChannelPalette.For(c));

    /// <summary>Bound from several threads, so they must be frozen.</summary>
    [Fact]
    public void TheChannelBrushesAreFrozen()
    {
        foreach (char c in new[] { 'R', 'G', 'B', '\0' })
            Assert.True(ChannelPalette.BrushFor(c).IsFrozen);
    }
}
