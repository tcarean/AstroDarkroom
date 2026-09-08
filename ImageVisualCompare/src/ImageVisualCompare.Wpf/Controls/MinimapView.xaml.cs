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
using System.Windows.Media;
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

    public MinimapView()
    {
        InitializeComponent();

        // The first Show lands before layout has given the control a size, so the viewport
        // rectangle has to be drawn again once ActualWidth is real.
        SizeChanged += (_, _) => Refresh();
    }

    /// <summary>Raised with a content-space point the user wants at the centre of the view.</summary>
    public event EventHandler<PointD>? CentreRequested;

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

    public void Hide()
    {
        _viewport = null;
        Thumbnail.Source = null;
        Visibility = Visibility.Collapsed;
    }

    /// <summary>Redraws the viewport rectangle after a zoom or pan.</summary>
    public void Refresh()
    {
        if (_viewport is not { HasContent: true } viewport || ActualWidth <= 0)
        {
            return;
        }

        MinimapLayout layout = MinimapGeometry.Layout(viewport, ActualWidth, ActualHeight);

        Canvas.SetLeft(ViewportRect, layout.ViewportRect.X);
        Canvas.SetTop(ViewportRect, layout.ViewportRect.Y);
        ViewportRect.Width = layout.ViewportRect.Width;
        ViewportRect.Height = layout.ViewportRect.Height;

        // Nothing to point at when the whole image is already on screen.
        ViewportRect.Visibility = layout.ShowViewportRect ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        _dragging = true;
        CaptureMouse();
        RequestCentre(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging)
        {
            RequestCentre(e.GetPosition(this));
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        EndDrag();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndDrag();
    }

    private void EndDrag()
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
        }
    }

    private void RequestCentre(Point position)
    {
        if (_viewport is not { HasContent: true } viewport || ActualWidth <= 0)
        {
            return;
        }

        CentreRequested?.Invoke(
            this,
            MinimapGeometry.ContentPointAt(viewport, position.X, position.Y, ActualWidth, ActualHeight));
    }
}
