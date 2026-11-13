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
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf.Controls;
using ImageVisualCompare.Wpf.Imaging;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The comparison surface on the WPF test thread (ADR 0032), laid out at a size, never shown, and
/// rendered into a bitmap: what it draws on a screen pixel against the image pixel it reports for
/// the pointer on that screen pixel, and against the pixel stored there. The pair's every pixel
/// carries its own coordinates in its colour, or is one square of a checkerboard, so the bitmap
/// says which image pixel was drawn where, and whether it was drawn alone.
/// </summary>
public class CompareViewTests
{
    private const int SurfaceWidth = 764;
    private const int SurfaceHeight = 450;

    /// <summary>
    /// How close to an image pixel's edge, in physical pixels, the centre of a screen pixel may
    /// be before the rasteriser's own arithmetic, not the geometry, decides which of the two it
    /// paints. WPF's software rasteriser, which renders here, paints a centre exactly on an edge
    /// from the pixel after it, where the display adapter, which the read-out follows, paints it
    /// from the pixel before; and it steps along a row in fixed point: on this surface it
    /// painted the next image pixel on screen pixels whose centre was up to 0.008 of a physical
    /// pixel short of the edge (measured 2026-11-13, ADR 0052).
    /// </summary>
    private const double UndecidedNearAnEdge = 1.0 / 32;

    /// <summary>
    /// CR-001 of the review of 2026-11-13. The first case was its part (a): on this surface the
    /// zoom leaves the pan of a 1854 pixel wide pair an ulp under 545, and the image is drawn
    /// at 545 (R-022). The others are its part (b), at scales that are not whole, so that the
    /// image is drawn at the offsets given: each image pixel is a square of screen pixels, and
    /// in each case some of the squares' edges fall in the first half of a screen pixel, where
    /// a read-out taken at the screen pixel's corner names the pixel before. The two at whole
    /// scales have the image drawn a fraction of a pixel from its pan (R-022): the read-out has
    /// to go by where it is drawn.
    /// </summary>
    [Theory]
    [InlineData("fit then 100%")]
    [InlineData("450% at offsets 300.5, 200.3")]
    [InlineData("1550% at offsets 300.79, 200.36")]
    [InlineData("400% at a pan of 300.55, 200.45")]
    [InlineData("1600% at a pan of 300.79, 200.36")]
    [InlineData("ten wheel notches from 100%")]
    public async Task R028_TheSurfaceReportsThePixelItDraws(string state)
    {
        var wrong = new List<string>();
        int compared = 0;

        await WpfThread.RunAsync(() =>
        {
            ImagePair pair = CodedPair(1854, 800);
            var view = new CompareView();
            view.Measure(new Size(SurfaceWidth, SurfaceHeight));
            view.Arrange(new Rect(0, 0, SurfaceWidth, SurfaceHeight));
            view.UpdateLayout();
            view.SetPair(pair);

            ImagePixel? reported = null;
            view.CursorPixelChanged += (_, pixel) => reported = pixel;

            Viewport viewport = view.Viewport;
            switch (state)
            {
                case "fit then 100%":
                    view.ZoomToActualSize();
                    Assert.Equal(545, viewport.OffsetX, 9);
                    Assert.Equal(545, viewport.DrawnOffsetX);
                    break;
                case "450% at offsets 300.5, 200.3":
                    viewport.ZoomTo(4.5, 0, 0);
                    viewport.SetOffset(300.5, 200.3);
                    break;
                case "1550% at offsets 300.79, 200.36":
                    viewport.ZoomTo(15.5, 0, 0);
                    viewport.SetOffset(300.79, 200.36);
                    break;
                case "400% at a pan of 300.55, 200.45":
                    viewport.ZoomTo(4, 0, 0);
                    viewport.SetOffset(300.55, 200.45);
                    Assert.Equal((300.5, 200.5), (viewport.DrawnOffsetX, viewport.DrawnOffsetY));
                    break;
                case "1600% at a pan of 300.79, 200.36":
                    viewport.ZoomTo(16, 0, 0);
                    viewport.SetOffset(300.79, 200.36);
                    Assert.Equal((300.8125, 200.375), (viewport.DrawnOffsetX, viewport.DrawnOffsetY));
                    break;
                default:
                    view.ZoomToActualSize();
                    for (int notch = 0; notch < 10; notch++)
                    {
                        viewport.ZoomBy(1.15, 201, 133);
                    }

                    break;
            }

            // The line is moved right, clear of the pixels compared; showing the pair again is
            // the surface's public way of putting the viewport's state on screen.
            view.Divider.Position = 0.93;
            view.ReplacePair(pair);
            view.UpdateLayout();

            var target = new RenderTargetBitmap(SurfaceWidth, SurfaceHeight, 96, 96, PixelFormats.Pbgra32);
            target.Render(view);
            byte[] drawn = new byte[SurfaceWidth * SurfaceHeight * 4];
            target.CopyPixels(drawn, SurfaceWidth * 4, 0);

            // One row of screen pixels and one column, clear of the minimap at the bottom left,
            // the zoom badge at the top right, the scrollbars and the line. With no window the
            // display scale is 1: a device-independent pixel is a physical one.
            IEnumerable<(int X, int Y)> row = Enumerable.Range(10, 590).Select(x => (x, 60));
            IEnumerable<(int X, int Y)> column = Enumerable.Range(50, 340).Select(y => (300, y));
            foreach ((int x, int y) in row.Concat(column))
            {
                // The image point at the screen pixel's centre, as the image is drawn.
                if (NearAnEdge(((x + 0.5) / viewport.Scale) + viewport.DrawnOffsetX)
                    || NearAnEdge(((y + 0.5) / viewport.Scale) + viewport.DrawnOffsetY))
                {
                    continue;
                }

                int at = ((y * SurfaceWidth) + x) * 4;
                var shown = new ImagePixel(
                    drawn[at + 2] + (256 * (drawn[at] & 15)),
                    drawn[at + 1] + (256 * (drawn[at] >> 4)));

                view.ReportPointer(new Point(x, y));
                compared++;
                if (reported != shown)
                {
                    wrong.Add($"screen ({x}, {y}): drawn {shown}, reported {reported}");
                }
            }

            bool NearAnEdge(double coordinate) =>
                Math.Abs(coordinate - Math.Round(coordinate)) * viewport.Scale < UndecidedNearAnEdge;
        });

        // 930 screen pixels, less the few whose centre is on an edge.
        Assert.InRange(compared, 900, 930);
        Assert.True(wrong.Count == 0, $"{wrong.Count} of {compared} screen pixels; the first: {string.Join("; ", wrong.Take(3))}");
    }

    /// <summary>
    /// CR-002 of the review of 2026-11-13. On a surface of odd width and height the pan after
    /// <c>Fit</c> then <c>100%</c> ends in a half on both axes; drawn there, every screen pixel
    /// would be four image pixels mixed, and on a checkerboard an even grey.
    /// </summary>
    [Fact]
    public async Task R022_AtOneHundredPercent_EveryScreenPixelShowsOneStoredPixel()
    {
        const int width = 765;
        const int height = 451;
        var wrong = new List<string>();
        int compared = 0;

        await WpfThread.RunAsync(() =>
        {
            ImagePair pair = Pair(1200, 800, Checker);
            var view = new CompareView();
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();
            view.SetPair(pair);
            view.ZoomToActualSize();

            Viewport viewport = view.Viewport;
            Assert.Equal(217.5, viewport.OffsetX, 9);
            Assert.Equal(174.5, viewport.OffsetY, 9);

            view.Divider.Position = 0.93;
            view.ReplacePair(pair);
            view.UpdateLayout();

            var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(view);
            byte[] drawn = new byte[width * height * 4];
            target.CopyPixels(drawn, width * 4, 0);

            // An area clear of the minimap, the zoom badge, the scrollbars and the line.
            for (int y = 50; y < height - 40; y++)
            {
                for (int x = 220; x < 650; x++)
                {
                    int at = ((y * width) + x) * 4;
                    byte stored = Checker(x + (int)viewport.DrawnOffsetX, y + (int)viewport.DrawnOffsetY).Red;
                    compared++;
                    if ((drawn[at], drawn[at + 1], drawn[at + 2]) != (stored, stored, stored) && wrong.Count < 3)
                    {
                        wrong.Add($"screen ({x}, {y}): drawn {drawn[at + 2]} {drawn[at + 1]} {drawn[at]}, stored {stored}");
                    }
                    else if ((drawn[at], drawn[at + 1], drawn[at + 2]) != (stored, stored, stored))
                    {
                        wrong.Add(string.Empty);
                    }
                }
            }
        });

        Assert.Equal(430 * 361, compared);
        Assert.True(wrong.Count == 0, $"{wrong.Count} of {compared} screen pixels; the first: {string.Join("; ", wrong.Take(3))}");
    }

    /// <summary>
    /// The colour that carries a pixel's coordinates: red x mod 256, green y mod 256 and blue
    /// x / 256 in the low four bits and y / 256 in the high four.
    /// </summary>
    private static (byte Blue, byte Green, byte Red) Coded(int x, int y) =>
        ((byte)((x >> 8) | ((y >> 8) << 4)), (byte)y, (byte)x);

    /// <summary>A checkerboard of one pixel a square: white where x + y is even, black elsewhere.</summary>
    private static (byte Blue, byte Green, byte Red) Checker(int x, int y)
    {
        byte value = (byte)(((x + y) & 1) == 0 ? 255 : 0);
        return (value, value, value);
    }

    /// <summary>A pair of one image twice, whose pixel (x, y) carries its coordinates in its colour.</summary>
    private static ImagePair CodedPair(int width, int height) => Pair(width, height, Coded);

    /// <summary>A pair of one opaque image twice, each pixel the colour the function gives it.</summary>
    private static ImagePair Pair(int width, int height, Func<int, int, (byte Blue, byte Green, byte Red)> colour)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int at = ((y * width) + x) * 4;
                (pixels[at], pixels[at + 1], pixels[at + 2]) = colour(x, y);
                pixels[at + 3] = 255;
            }
        }

        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();

        var info = new ImageInfo("coded.png", width, height, 8, ColorChannels.Rgb);
        var image = new LoadedImage(info, bitmap);
        return new ImagePair(image, image, ImagePairValidator.Inspect(info, info));
    }
}
