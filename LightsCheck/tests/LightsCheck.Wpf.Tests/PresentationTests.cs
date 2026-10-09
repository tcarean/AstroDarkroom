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

namespace LightsCheck.Wpf.Tests;

/// <summary>A byte raster becomes a bitmap with no value conversion.</summary>
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
/// trailed frame green. The tests are on the ordering, not on RGB values.
/// </summary>
public class VerdictPaletteTests
{
    private static readonly string[] FiveWords = ["BUN", "ceva", "LA LIMITA", "SLAB", "NEDETERMINAT"];

    [Theory]
    [InlineData("BUN", "OK")]
    [InlineData("BUNA", "NU")]
    [InlineData("SLAB", "INSUFICIENT")]
    [InlineData("LA LIMITA", "POSIBIL")]
    [InlineData("ATENTIE", "DA")]
    public void WordsOfTheSameSeverityShareTheColour(string a, string b)
        => Assert.Equal(VerdictPalette.For(a), VerdictPalette.For(b));

    [Fact]
    public void TheFourSeveritiesAndNeutralGetFiveDistinctColours()
    {
        List<Color> colours = FiveWords.Select(VerdictPalette.For).ToList();

        Assert.Equal(5, colours.Distinct().Count());
    }

    [Fact]
    public void TheFrameClassWordsMapToTheirColours()
    {
        Assert.Equal(VerdictPalette.Good, VerdictPalette.For("BUN"));
        Assert.Equal(VerdictPalette.Warn, VerdictPalette.For("LA LIMITA"));
        Assert.Equal(VerdictPalette.Bad, VerdictPalette.For("SLAB"));
        Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For("NECITIT"));
    }

    /// <summary>"Don't know" is not "bad": a series that could not be measured must not look like a broken one.</summary>
    [Theory]
    [InlineData("NEDETERMINAT")]
    [InlineData("NEMASURAT")]
    [InlineData("nemasurat (fara biases)")]
    [InlineData("")]
    [InlineData(null)]
    public void InconclusiveVerdictsAreNeutralNotRed(string? word)
    {
        Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For(word));
        Assert.NotEqual(VerdictPalette.Bad, VerdictPalette.For(word));
    }

    [Fact]
    public void TheVerdictIsReadCaseInsensitiveAndTrimmed()
        => Assert.Equal(VerdictPalette.For("SLAB"), VerdictPalette.For("  slab: 2 cadre "));

    /// <summary>A word the palette does not know is shown as fair, neither green nor red.</summary>
    [Fact]
    public void AnUnknownWordIsFair()
        => Assert.Equal(VerdictPalette.Fair, VerdictPalette.For("ceva-nou"));

    [Fact]
    public void BrushesAreFrozenSoTheyCanCrossThreads()
    {
        Brush b = VerdictPalette.BrushFor("BUN");

        Assert.True(b.IsFrozen);
        Assert.Equal(VerdictPalette.Good, Assert.IsType<SolidColorBrush>(b).Color);
    }
}

/// <summary>The converters the theme and the views use.</summary>
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

        SolidColorBrush b = Assert.IsType<SolidColorBrush>(c.Convert("SLAB", typeof(Brush), null!, Inv));
        Assert.Equal(VerdictPalette.Bad, b.Color);
    }
}

/// <summary>The folder from the command line: the first argument that is a folder, else none.</summary>
public class AppArgsTests
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
        string dir = Path.GetTempPath();

        Assert.Equal(dir, App.FolderFromArgs([dir]));
    }

    /// <summary>A path that is not a folder is ignored and the window asks for one, as FlatCheck does.</summary>
    [Fact]
    public void AMissingFolderIsIgnoredSoTheWindowAsks()
    {
        string missing = Path.Combine(Path.GetTempPath(), "lightscheck-missing-" + Guid.NewGuid().ToString("N"));

        Assert.Null(App.FolderFromArgs([missing, " "]));
        Assert.Equal(Path.GetTempPath(), App.FolderFromArgs([missing, Path.GetTempPath()]));
    }
}
