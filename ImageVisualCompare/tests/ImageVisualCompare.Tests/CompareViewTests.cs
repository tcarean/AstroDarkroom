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
/// the pointer on that screen pixel. The pair's every pixel carries its own coordinates in its
/// colour, so the bitmap says which image pixel was drawn where.
/// </summary>
public class CompareViewTests
{
    private const int SurfaceWidth = 764;
    private const int SurfaceHeight = 450;

    /// <summary>
    /// How close to an image pixel's edge, in physical pixels, the centre of a screen pixel may
    /// be before the rasteriser's own arithmetic, not the geometry, decides which of the two it
    /// paints. WPF's software rasteriser, which renders here, steps along a row in fixed point:
    /// on this surface it painted the next image pixel on screen pixels whose centre was up to
    /// 0.008 of a physical pixel short of the edge (measured 2026-11-13, ADR 0052).
    /// </summary>
    private const double UndecidedNearAnEdge = 1.0 / 32;

    /// <summary>
    /// CR-001 of the review of 2026-11-13. The first case is its part (a): on this surface the
    /// zoom leaves the horizontal offset of a 1854 pixel wide pair an ulp under 545. The others
    /// are its part (b): from 400 % up each image pixel is a square of screen pixels, and the
    /// square's edge falls anywhere inside a screen pixel.
    /// </summary>
    [Theory]
    [InlineData("fit then 100%")]
    [InlineData("400% at offsets 300.55, 200.45")]
    [InlineData("1600% at offsets 300.21, 200.77")]
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
                    break;
                case "400% at offsets 300.55, 200.45":
                    viewport.ZoomTo(4, 0, 0);
                    viewport.SetOffset(300.55, 200.45);
                    break;
                case "1600% at offsets 300.21, 200.77":
                    viewport.ZoomTo(16, 0, 0);
                    viewport.SetOffset(300.21, 200.77);
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
                PointD centre = viewport.ScreenToContent(x + 0.5, y + 0.5);
                if (NearAnEdge(centre.X) || NearAnEdge(centre.Y))
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
    /// A pair of one image twice, whose pixel (x, y) is red x mod 256, green y mod 256 and blue
    /// x / 256 in the low four bits and y / 256 in the high four.
    /// </summary>
    private static ImagePair CodedPair(int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int at = ((y * width) + x) * 4;
                pixels[at] = (byte)((x >> 8) | ((y >> 8) << 4));
                pixels[at + 1] = (byte)y;
                pixels[at + 2] = (byte)x;
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
