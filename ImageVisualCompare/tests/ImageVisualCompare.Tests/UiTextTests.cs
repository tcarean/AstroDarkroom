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
using System.Reflection;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf;
using ImageVisualCompare.Wpf.Imaging;
using ImageVisualCompare.Wpf.Services;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The Romanian interface, read where each text is written: the Core records, the loaders, the
/// version texts and the main window. The English texts are pinned by the tests of each area.
/// </summary>
[Collection(UiLanguageCollection.Name)]
public sealed class UiTextTests : IDisposable
{
    private readonly TempImages _images = new();

    public void Dispose() => _images.Dispose();

    [Fact]
    public void R101_TheLanguageIsEnglish_UntilTheStartUpChoosesAnother()
    {
        Assert.Same(UiText.English, UiText.Current);
        Assert.Same(UiText.English, UiText.For(UiLanguage.English));
        Assert.Same(UiText.Romanian, UiText.For(UiLanguage.Romanian));
    }

    [Theory]
    [InlineData("--lang=ro")]
    [InlineData("--lang ro")]
    public void R100_TheStartUp_SetsTheLanguageFromTheCommandLine(string option)
    {
        UiText previous = UiText.Current;
        try
        {
            App.ApplyCommandLine([.. option.Split(' '), @"C:
ot-here.png", @"C:
ot-here.png"]);

            Assert.Same(UiText.Romanian, UiText.Current);
            Assert.Null(App.TakeUnknownLanguage());
            Assert.Null(App.TakeStartupSelection());
        }
        finally
        {
            UiText.Current = previous;
        }
    }

    [Fact]
    public void R102_TheStartUp_KeepsAnUnknownLanguageForTheWindow_AndStaysEnglish()
    {
        UiText previous = UiText.Current;
        try
        {
            App.ApplyCommandLine(["--lang=de"]);

            Assert.Same(UiText.English, UiText.Current);
            Assert.Equal("de", App.TakeUnknownLanguage());
            Assert.Null(App.TakeUnknownLanguage());
        }
        finally
        {
            UiText.Current = previous;
        }
    }

    [Fact]
    public void R101_ThePaddingQuestion_InRomanian()
    {
        ImageInfo before = new(@"C:\astro\de_printat.png", 5938, 3888, 16, ColorChannels.Rgb);
        ImageInfo after = new(@"C:\astro\de_printat_50x35_TIFF8.tif", 5554, 3888, 8, ColorChannels.Rgb);
        PadPlan plan = ImagePairValidator.Inspect(before, after);

        using var romanian = new InRomanian();
        string message = ImagePairValidator.DescribeMismatch(before, after, plan);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "Cele două imagini au dimensiuni diferite.",
                "A: de_printat.png — 5938 × 3888",
                "B: de_printat_50x35_TIFF8.tif — 5554 × 3888",
                string.Empty,
                "Amândouă pot fi completate cu negru până la 5938 × 3888,",
                "ancorate în stânga-sus și comparate pe acea pânză.",
                "Fișierele imaginilor nu sunt modificate.",
                string.Empty,
                "Continui?"),
            message);
    }

    [Theory]
    [InlineData(16, false, ColorChannels.Rgb, null, "RGB pe 16 biți")]
    [InlineData(8, false, ColorChannels.Rgb, null, "RGB pe 8 biți")]
    [InlineData(16, false, ColorChannels.Gray, null, "gri pe 16 biți")]
    [InlineData(16, false, ColorChannels.GrayAlpha, null, "gri + alfa pe 16 biți")]
    [InlineData(8, false, ColorChannels.Rgba, null, "RGBA pe 8 biți")]
    [InlineData(8, false, ColorChannels.Indexed, null, "paletizat pe 8 biți")]
    [InlineData(32, true, ColorChannels.Rgb, null, "RGB float pe 32 de biți")]
    [InlineData(32, true, ColorChannels.Gray, "0..65535", "gri float pe 32 de biți (0..65535)")]
    [InlineData(64, true, ColorChannels.Rgb, null, "RGB float pe 64 de biți")]
    [InlineData(96, false, ColorChannels.Unknown, null, "96 de biți")]
    [InlineData(1, false, ColorChannels.Unknown, null, "1 bit")]
    public void R101_TheFormat_InRomanian(int bits, bool isFloat, ColorChannels channels, string? range, string expected)
    {
        var info = new ImageInfo(@"C:\a.fit", 2, 2, bits, channels, isFloat, range);

        using var romanian = new InRomanian();

        Assert.Equal(expected, info.FormatDescription);
    }

    [Fact]
    public void R101_TheFitsMessages_InRomanian()
    {
        byte[] noImage = FitsFixture.Header(
            [FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 0)]);
        byte[] cutShort = FitsFixture.Image(-32, 100, 100, 3, new double[30000])[..10000];
        byte[] twoPlanes = FitsFixture.Image(8, 2, 2, 2, new double[8]);

        using var romanian = new InRomanian();

        Assert.Equal(
            "FITS: fișierul nu conține nicio imagine.",
            Assert.Throws<NotSupportedException>(() => FitsHeader.ReadLayout(new MemoryStream(noImage))).Message);
        Assert.Equal(
            "FITS: datele imaginii sunt incomplete: fișierul se termină prea devreme.",
            Assert.Throws<EndOfStreamException>(() => FitsHeader.ReadLayout(new MemoryStream(cutShort))).Message);
        Assert.Equal(
            "FITS: o imagine cu NAXIS3 = 2 nu e suportată; sunt suportate cele cu 1 sau 3 planuri.",
            Assert.Throws<NotSupportedException>(() => FitsHeader.ReadLayout(new MemoryStream(twoPlanes))).Message);

        UiText ro = UiText.Romanian;
        Assert.Equal("FITS: o imagine cu NAXIS = 4 nu e suportată; sunt suportate cele cu 2 axe sau cu 3 axe și 1 sau 3 planuri.", ro.FitsAxes(4));
        Assert.Equal("FITS: BITPIX 64 nu e suportat.", ro.FitsBitPix(64));
        Assert.Equal("FITS: o imagine comprimată pe dale (tile-compressed) nu e suportată.", ro.FitsTileCompressed);
        Assert.Equal("FITS: antetul nu este valid.", ro.FitsInvalidHeader);
        Assert.Equal("FITS: antetul se termină înainte de cardul END.", ro.FitsHeaderEndsEarly);
        Assert.Equal("FITS: o imagine de 300000000 × 2 este prea mare ca să fie afișată.", ro.FitsImageTooLarge(300000000, 2));
    }

    [Fact]
    public async Task R101_AFileThatCannotBeRead_InRomanian()
    {
        string huge = _images.WritePngWithHeaderSize("huge.png", 40000, 30000);

        using var romanian = new InRomanian();
        ImagePairLoadResult result = await new ImagePairLoader(new WicImageLoader()).LoadAsync(huge, huge);

        Assert.Equal(
            "Fișierul huge.png nu poate fi citit." + Environment.NewLine +
            "O imagine de 40000 × 30000 este prea mare ca să fie afișată.",
            result.Error);
    }

    [Fact]
    public void R101_TheMemory_InRomanian()
    {
        var snapshot = new MemorySnapshot(2L * 1024 * 1024, 560L * 1024 * 1024);

        using var romanian = new InRomanian();

        Assert.Equal("memorie .NET 2 MB, proces 560 MB", snapshot.Describe());
    }

    [Fact]
    public void R101_TheAboutText_InRomanian()
    {
        using var romanian = new InRomanian();

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                VersionInfo.Banner,
                VersionInfo.Copyright,
                string.Empty,
                "Acest program vine fără ABSOLUT NICIO GARANȚIE.",
                "Este software liber și îl poți redistribui în condițiile",
                "Licenței Publice Generale GNU, versiunea 3 sau ulterioară.",
                string.Empty,
                "Textul complet al licenței e în fișierul LICENSE de lângă program și la",
                "https://www.gnu.org/licenses/gpl-3.0.html"),
            VersionInfo.About);
    }

    [Fact]
    public void R101_TheDialogs_InRomanian()
    {
        UiText ro = UiText.Romanian;

        Assert.Equal("Alege imaginea A  —  afișată în stânga liniei", ro.SelectImageA);
        Assert.Equal("Alege imaginea B  —  afișată în dreapta liniei", ro.SelectImageB);
        Assert.StartsWith("Imagini (*.png;*.tif;*.tiff;*.fit;*.fits;*.fts)|", ro.FileFilter);
        Assert.EndsWith("|Toate fișierele (*.*)|*.*", ro.FileFilter);
        Assert.Equal("Fișierele nu pot fi comparate", ro.CannotCompareTitle);
        Assert.Equal("Imaginile au dimensiuni diferite", ro.DifferentSizesTitle);
        Assert.Equal("Despre Image Visual Compare 1.1.1", ro.AboutTitle("Image Visual Compare 1.1.1"));
        Assert.Equal(("OK", "Da", "Nu"), (ro.Ok, ro.Yes, ro.No));
        Assert.Equal(("Se deschide…", "Se decodează de_printat.png…", "Aproape gata…"), (ro.Opening, ro.Decoding("de_printat.png"), ro.AlmostThere));
        Assert.Equal("pânză 5938 × 3888", ro.PaddedCanvas("5938 × 3888"));
    }

    [Fact]
    public async Task R101_TheMainWindow_InRomanian()
    {
        using var romanian = new InRomanian();

        await WpfThread.RunAsync(() =>
        {
            var window = new MainWindow(new NoDialogs(), new ImagePairLoader(new WicImageLoader()));

            Assert.Equal("Navigare", window.NavigationLabel.Text);
            Assert.Equal("Încadrează", window.FitButton.Content);
            Assert.Equal("100%", window.ActualSizeButton.Content);
            Assert.Equal("Deschide…", window.OpenButton.Content);
            Assert.Equal("Despre", window.AboutButton.Content);
            Assert.Equal(
                "rotița: zoom · trage linia: o muți · trage în afara liniei: pan · ←/→: mută linia fin · Ctrl+O: deschide",
                window.HintText.Text);
            Assert.Equal(VersionInfo.Banner, window.Title);
        });
    }

    [Fact]
    public void R101_EveryText_IsWordedInBothLanguages()
    {
        // A Romanian text equal to its English one is a text left untranslated; "OK" is the one
        // word the two languages share.
        PropertyInfo[] texts = typeof(UiText)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .ToArray();

        Assert.NotEmpty(texts);
        foreach (PropertyInfo text in texts)
        {
            string english = (string)text.GetValue(UiText.English)!;
            string romanian = (string)text.GetValue(UiText.Romanian)!;

            Assert.False(string.IsNullOrWhiteSpace(english), text.Name);
            Assert.False(string.IsNullOrWhiteSpace(romanian), text.Name);
            if (text.Name != nameof(UiText.Ok))
            {
                Assert.True(english != romanian, $"{text.Name} is the same in both languages: {english}");
            }
        }
    }

    /// <summary>Never asked: the window is only built.</summary>
    private sealed class NoDialogs : IFileDialogService
    {
        public ImagePairSelection? PickImagePair(string? initialDirectory = null) => null;
    }
}
