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
/// The built-in 5x7 bitmap font that labels the split mosaic (R-093). There is no
/// external font: the PNG writer is hand-rolled (ADR 0008) and a label needs only
/// digits, capitals and a few signs.
/// </summary>
public class BitmapFontTests
{
    static byte[] Canvas(int w, int h) => new byte[w * h * 3];

    static bool Set(byte[] rgb, int w, int x, int y) => rgb[(y * w + x) * 3] == 255;

    [Fact]
    public void EveryDigitCapitalAndSignOfTheLabelsHasAGlyph()
    {
        foreach (char c in "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ .-/:%()+,")
            Assert.True(BitmapFont.Has(c), $"'{c}'");
        Assert.True(BitmapFont.Has('a'));      // drawn as its capital
        Assert.False(BitmapFont.Has('#'));
        Assert.False(BitmapFont.Has('ă'));
    }

    [Fact]
    public void DrawingAOneAtScaleOnePaintsTheExpectedPixels()
    {
        byte[] rgb = Canvas(5, 7);

        BitmapFont.Draw(rgb, 5, 7, 0, 0, "1", 1, 255, 255, 255);

        for (int y = 0; y < 7; y++) Assert.True(Set(rgb, 5, 2, y), $"stem row {y}");
        Assert.True(Set(rgb, 5, 1, 1));        // the flag
        Assert.True(Set(rgb, 5, 1, 6));        // the foot
        Assert.True(Set(rgb, 5, 3, 6));
        Assert.False(Set(rgb, 5, 0, 0));
        Assert.False(Set(rgb, 5, 4, 0));
        Assert.False(Set(rgb, 5, 0, 3));
    }

    [Fact]
    public void ScaleThreeTriplesEveryPixelAndTheAdvance()
    {
        Assert.Equal(5, BitmapFont.Width("1", 1));
        Assert.Equal(11, BitmapFont.Width("11", 1));      // 5 + 1 gap + 5
        Assert.Equal(15, BitmapFont.Width("1", 3));
        Assert.Equal(21, BitmapFont.Height(3));

        byte[] rgb = Canvas(15, 21);
        BitmapFont.Draw(rgb, 15, 21, 0, 0, "1", 3, 255, 255, 255);

        for (int y = 0; y < 21; y++)
            for (int x = 6; x < 9; x++)
                Assert.True(Set(rgb, 15, x, y), $"({x}, {y})");
        Assert.False(Set(rgb, 15, 0, 0));
    }

    [Fact]
    public void LowercaseIsDrawnAsCapital_AnUnknownCharacterDrawsNothingButAdvances()
    {
        byte[] lower = Canvas(5, 7), upper = Canvas(5, 7), unknown = Canvas(5, 7);

        BitmapFont.Draw(lower, 5, 7, 0, 0, "a", 1, 255, 255, 255);
        BitmapFont.Draw(upper, 5, 7, 0, 0, "A", 1, 255, 255, 255);
        BitmapFont.Draw(unknown, 5, 7, 0, 0, "#", 1, 255, 255, 255);

        Assert.Equal(upper, lower);
        Assert.All(unknown, b => Assert.Equal(0, b));
        Assert.Equal(BitmapFont.Width("1", 1), BitmapFont.Width("#", 1));
    }

    [Fact]
    public void TextIsClippedAtTheCanvasEdgeInsteadOfThrowing()
    {
        byte[] rgb = Canvas(8, 4);

        BitmapFont.Draw(rgb, 8, 4, 5, 1, "88", 1, 255, 255, 255);

        Assert.True(Set(rgb, 8, 6, 1));       // the top row of an 8 is .###., so its second pixel is set
        Assert.False(Set(rgb, 8, 0, 0));
    }
}
