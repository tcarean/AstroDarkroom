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

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf.Tests;

/// <summary>
/// R-113: BarsCanvas draws its two marker lines where their values are. The bins are centred
/// on their index (TonalAnalyzer rounds L* into bin i), so a marker at m falls at the centre of
/// bin m, not at its left edge; the second line is dashed. Rendered to a bitmap and read back by
/// column, since the smoke tests only dump the tab without looking at it.
/// </summary>
[Collection("fereastra")]
public class BarsCanvasTests
{
    readonly StaFixture _sta;

    public BarsCanvasTests(StaFixture sta) => _sta = sta;

    static bool Red(byte r, byte g, byte b) => r > 120 && g < 80 && b < 80;
    static bool Yellow(byte r, byte g, byte b) => r > 120 && g > 120 && b < 80;

    [Fact]
    public void R113_TheCanvasDrawsBothLinesAtTheCentreOfTheirBin()
    {
        _sta.Invoke(() =>
        {
            const int w = 1010, h = 140;   // 101 bins of 10 px each
            var canvas = new BarsCanvas
            {
                Values = Enumerable.Repeat(1.0, 101).ToList(),
                Marker = 9,
                SecondMarker = 15,
                Fill = Brushes.Black,
                MarkerBrush = Brushes.Red,
                SecondMarkerBrush = Brushes.Yellow,
            };
            canvas.Measure(new Size(w, h));
            canvas.Arrange(new Rect(0, 0, w, h));
            canvas.UpdateLayout();
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(canvas);

            // Bin 9 spans x 90..100, so the value 9 is at the column 95; a solid 1.5 px line
            // there covers most of the column. At 90, the bin's left edge, nothing is drawn.
            Assert.True(Count(bmp, 95, Red) > h * 0.9, $"red at 95: {Count(bmp, 95, Red)}");
            Assert.Equal(0, Count(bmp, 90, Red));
            // Bin 15: the column 155. The dashes (2 on, 2 off, in pen widths) leave about half
            // of the column yellow; the left edge, 150, stays empty.
            int yellow = Count(bmp, 155, Yellow);
            Assert.InRange(yellow, h * 0.3, h * 0.7);
            Assert.Equal(0, Count(bmp, 150, Yellow));
        });
    }

    /// <summary>How many pixels of the column x satisfy the predicate (r, g, b).</summary>
    static int Count(RenderTargetBitmap bmp, int x, Func<byte, byte, byte, bool> pixel)
    {
        var px = new byte[bmp.PixelHeight * 4];
        bmp.CopyPixels(new Int32Rect(x, 0, 1, bmp.PixelHeight), px, 4, 0);
        int n = 0;
        for (int i = 0; i < px.Length; i += 4)
            if (pixel(px[i + 2], px[i + 1], px[i])) n++;   // Pbgra32: b, g, r, a
        return n;
    }
}
