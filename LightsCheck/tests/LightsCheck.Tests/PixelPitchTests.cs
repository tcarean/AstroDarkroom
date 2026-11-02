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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// The pixel size of a series (R-066, ADR 0026): derived from the raw's 35 mm-equivalent focal
/// length on the values libraw gave on 2026-10-27 for the two real sessions' D3300 raws, and
/// chosen among the files, the typed value, the raw and the D3300's, on fabricated frames.
/// </summary>
public class PixelPitchTests
{
    private static FrameInfo Frame(double? focal, double? pixelUm = null, double? focal35 = null, int? w = null, int? h = null, string? error = null)
        => new(1, "lights/x.raw", "Nikon D3300", focal, 2.8, 120.2, 1600, SessionFixture.FirstStart, pixelUm, focal35, w, h, null, null, null, null, null, null, error);

    [Fact]
    public void R066_ThePitchFollowsFromTheCropAndTheMosaicsDiagonal()
    {
        // the Veil session: 135 mm, FocalLengthIn35mmFormat 202, 6016 x 4016
        Assert.Equal(3.9976, PixelPitch.FromCrop(135, 202, 6016, 4016)!.Value, 3);
        // the Smida session: 20 mm, 30
        Assert.Equal(3.9878, PixelPitch.FromCrop(20, 30, 6016, 4016)!.Value, 3);
        // a full-frame body at its nominal crop of 1: the diagonal over the pixels
        Assert.Equal(1000 * PixelPitch.FullFrameDiagonalMm / Math.Sqrt(6000.0 * 6000 + 4000.0 * 4000), PixelPitch.FromCrop(50, 50, 6000, 4000)!.Value, 6);
        // a 4:3 sensor at crop 2 (17.3 x 13 mm, 4592 x 3448): the diagonal form gives the true 3.77, the width form would give 3.92
        Assert.Equal(3.77, PixelPitch.FromCrop(25, 50, 4592, 3448)!.Value, 2);
    }

    [Theory]
    [InlineData(null, 202.0, 6016, 4016)]      // no focal length
    [InlineData(135.0, null, 6016, 4016)]      // a FITS, or a body with no lens data entered
    [InlineData(135.0, 0.0, 6016, 4016)]       // libraw's 0
    [InlineData(135.0, 202.0, null, 4016)]
    [InlineData(135.0, 202.0, 6016, 0)]
    [InlineData(0.0, 202.0, 6016, 4016)]
    public void R066_ThePitchIsUnknownWhenAnyInputIsMissing(double? focal, double? focal35, int? w, int? h)
        => Assert.Null(PixelPitch.FromCrop(focal, focal35, w, h));

    [Fact]
    public void R066_TheFilesPixelSizeWinsOverEverything()
    {
        FrameInfo[] frames = [Frame(135, focal35: 202, w: 6016, h: 4016), Frame(135, pixelUm: 3.92)];

        Assert.Equal((3.92, PixelPitchSource.File), PixelPitch.Choose(frames, 4.5));
    }

    [Fact]
    public void R066_TheTypedSizeWinsOverTheRaw()
    {
        FrameInfo[] frames = [Frame(135, focal35: 202, w: 6016, h: 4016)];

        Assert.Equal((4.5, PixelPitchSource.Entered), PixelPitch.Choose(frames, 4.5));
    }

    [Fact]
    public void R066_TheRawDecidesWhenNothingWasTyped_TheFirstRawThatCan()
    {
        FrameInfo[] frames = [Frame(135, error: "stricat"), Frame(135), Frame(135, focal35: 202, w: 6016, h: 4016), Frame(20, focal35: 30, w: 6016, h: 4016)];

        (double um, PixelPitchSource source) = PixelPitch.Choose(frames, null);

        Assert.Equal(PixelPitchSource.Raw, source);
        Assert.Equal(3.9976, um, 3);
    }

    [Fact]
    public void R066_TheD3300IsTheLastFallback()
    {
        FrameInfo[] frames = [Frame(135), Frame(135, focal35: 202)];     // no size: a FITS, say

        Assert.Equal((SeriesOptions.D3300PixelUm, PixelPitchSource.Default), PixelPitch.Choose(frames, null));
        Assert.Equal((SeriesOptions.D3300PixelUm, PixelPitchSource.Default), PixelPitch.Choose([], null));
    }

    [Fact]
    public void R066_TheScaleLineNamesTheSource_InBothLanguages()
    {
        using SessionFixture s = new SessionFixture().WithLights(2, withStars: true);
        using (Ui.Use(UiLanguage.English))
        {
            SeriesResult r = SeriesRun.Run(s.Root, new SeriesOptions(Threads: 2));
            // the fixture's FITS carry XPIXSZ 3.92: the file wins, as the goldens pin
            Assert.Equal("written in the file (XPIXSZ)", r.PixelSizeSource);
            Assert.Equal(3.92, r.PixelSizeUm);
        }

        using (Ui.Use(UiLanguage.Romanian))
        {
            Assert.Equal("scris in fisier (XPIXSZ)", SeriesRun.Run(s.Root, new SeriesOptions(Threads: 2)).PixelSizeSource);
        }
    }
}
