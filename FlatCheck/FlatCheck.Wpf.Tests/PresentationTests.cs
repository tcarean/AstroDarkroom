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

/// <summary>
/// Culorile si convertoarele. Marunte, dar culoarea unui verdict e primul lucru pe care
/// il vede omul, si o greseala aici arata verde un flat de refacut.
/// </summary>
public class VerdictPaletteTests
{
    /// <summary>
    /// Culoarea se DEDUCE din Stats.Severity. Testul e scris pe ordonare, nu pe valori
    /// RGB: daca maine paleta se schimba, testul trebuie sa treaca in continuare, dar
    /// daca ordinea se rastoarna trebuie sa pice.
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
    /// "Nu stiu" nu e "e prost". In Stats amandoua stau la severitate care iese din zero
    /// fiindca acolo conteaza doar codul de iesire; vizual insa trebuie sa se deosebeasca,
    /// altfel o serie neconcludenta arata ca un flat stricat.
    /// </summary>
    [Fact]
    public void InconclusiveVerdictsAreNeutralNotRed()
    {
        Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For(LightSeriesAnalyzer.Undetermined));
        Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For(Stats.NotApplicable));

        Assert.NotEqual(VerdictPalette.For("REFA"), VerdictPalette.For(Stats.NotApplicable));
        Assert.NotEqual(VerdictPalette.For("ACCEPTABIL"), VerdictPalette.For(Stats.NotApplicable));
    }

    /// <summary>Un verdict necunoscut e tratat ca cel mai rau - aceeasi regula ca in nucleu.</summary>
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
    /// Harta ajunge pe ecran din memorie, fara fisier temporar. Testul foloseste un PNG
    /// produs chiar de encoderul uneltei, deci verifica si ca cei doi se inteleg.
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
        => Assert.Equal("pp_flat_A.fit",
                        Run(new FileNameConverter(), @"C:\sesiune\process\pp_flat_A.fit"));

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
    /// Sirul gol conteaza ca "nimic": ViewModel-urile golesc campurile punand "", nu null,
    /// iar un panou care ar ramane vizibil cu text gol ar lasa un spatiu inexplicabil.
    /// </summary>
    [Fact]
    public void AnEmptyStringCountsAsNothingToShow()
    {
        var c = new NotNullToVisibilityConverter();

        Assert.Equal(Visibility.Collapsed, Run(c, null));
        Assert.Equal(Visibility.Collapsed, Run(c, ""));
        Assert.Equal(Visibility.Visible, Run(c, "ceva"));
    }
}
