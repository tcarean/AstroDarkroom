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
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf.Controls;

/// <summary>
/// A thumbnail of the whole image with the visible region drawn on it.
///
/// At 21 megapixels and 400% zoom the viewport shows about a thousandth of the picture, and
/// without this there is no way to tell which thousandth. Clicking or dragging inside it
/// recentres the view, which beats panning across the frame a windowful at a time.
/// </summary>
public partial class MinimapView : UserControl
{
    private const double MaxThumbnailWidth = 190;
    private const double MaxThumbnailHeight = 150;

    private Viewport? _viewport;
    private bool _dragging;

    /// <summary>
    /// Builds the minimap, which redraws its rectangle whenever its own size changes.
    /// </summary>
    public MinimapView()
    {
        InitializeComponent();

        // The first Show lands before layout has given the control a size, so the viewport
        // rectangle has to be drawn again once the overlay's size is real.
        SizeChanged += (_, _) => Refresh();
    }

    /// <summary>Raised with a content-space point the user wants at the centre of the view.</summary>
    public event EventHandler<PointD>? CentreRequested;

    /// <summary>
    /// Shows a thumbnail of the canvas at the largest size with the canvas's aspect ratio that
    /// fits the minimap's box, and draws the visible region on it.
    /// </summary>
    /// <param name="thumbnail">The canvas, already rendered small.</param>
    /// <param name="contentWidth">The canvas width, in image pixels.</param>
    /// <param name="contentHeight">The canvas height, in image pixels.</param>
    /// <param name="viewport">
    /// The view whose visible region the rectangle shows, read at every <see cref="Refresh"/>.
    /// </param>
    public void Show(BitmapSource thumbnail, double contentWidth, double contentHeight, Viewport viewport)
    {
        _viewport = viewport;
        Thumbnail.Source = thumbnail;

        SizeD box = AspectFit.Fit(contentWidth, contentHeight, MaxThumbnailWidth, MaxThumbnailHeight);
        Width = box.Width;
        Height = box.Height;

        Visibility = Visibility.Visible;
        Refresh();
    }

    /// <summary>Hides the minimap and lets go of its thumbnail and its viewport.</summary>
    public void Hide()
    {
        _viewport = null;
        Thumbnail.Source = null;
        Visibility = Visibility.Collapsed;
    }

    /// <summary>Redraws the viewport rectangle after a zoom or pan.</summary>
    public void Refresh()
    {
        if (_viewport is not { HasContent: true } viewport || Overlay.ActualWidth <= 0)
        {
            return;
        }

        // The thumbnail sits inside the border, and so does the overlay the rectangle is drawn
        // on: the rectangle is laid out in the overlay's size, not the minimap's.
        MinimapLayout layout = MinimapGeometry.Layout(viewport, Overlay.ActualWidth, Overlay.ActualHeight);

        Canvas.SetLeft(ViewportRect, layout.ViewportRect.X);
        Canvas.SetTop(ViewportRect, layout.ViewportRect.Y);
        ViewportRect.Width = layout.ViewportRect.Width;
        ViewportRect.Height = layout.ViewportRect.Height;

        // Nothing to point at when the whole image is already on screen.
        ViewportRect.Visibility = layout.ShowViewportRect ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Starts a drag, and asks for the view to be centred on the point pressed.</summary>
    /// <param name="e">The press; its position is read, and it is marked handled.</param>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        _dragging = true;
        CaptureMouse();
        RequestCentre(e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>
    /// While a drag is on, keeps asking for the view to be centred on the pointer.
    /// </summary>
    /// <param name="e">The move; its position is read.</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging)
        {
            RequestCentre(e.GetPosition(this));
        }
    }

    /// <summary>Ends the drag.</summary>
    /// <param name="e">The release; marked handled.</param>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        EndDrag();
        e.Handled = true;
    }

    /// <summary>Ends a drag whose mouse capture was taken away.</summary>
    /// <param name="e">The event; not read.</param>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndDrag();
    }

    /// <summary>Ends a drag, if one is on, and releases the mouse.</summary>
    private void EndDrag()
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// Raises <see cref="CentreRequested"/> with the image point a position on the minimap
    /// stands for, measured on the thumbnail inside the border; does nothing without a viewport
    /// that has content, or before the minimap has a size.
    /// </summary>
    /// <param name="position">
    /// The pointer, in device-independent pixels from the minimap's own top-left corner, as its
    /// mouse handlers read it; the border is taken off here.
    /// </param>
    internal void RequestCentre(Point position)
    {
        if (_viewport is not { HasContent: true } viewport || Overlay.ActualWidth <= 0)
        {
            return;
        }

        Point onThumbnail = TranslatePoint(position, Overlay);
        CentreRequested?.Invoke(
            this,
            MinimapGeometry.ContentPointAt(
                viewport, onThumbnail.X, onThumbnail.Y, Overlay.ActualWidth, Overlay.ActualHeight));
    }
}
