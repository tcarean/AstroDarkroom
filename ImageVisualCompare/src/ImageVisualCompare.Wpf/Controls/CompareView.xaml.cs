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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf.Imaging;

namespace ImageVisualCompare.Wpf.Controls;

/// <summary>
/// The comparison surface: image A on the left of the divider, image B on the right, both
/// under a single shared transform.
///
/// Coordinates come in two flavours here and mixing them up would be a real bug, so the rule
/// is fixed: <see cref="Core.Viewport"/> works entirely in physical screen pixels, WPF works
/// in device-independent pixels, and the conversion happens only in this control: wherever it
/// hands the viewport a position or a size (the input handlers, <see cref="ZoomAtCentre"/> and
/// <see cref="UpdateViewportSize"/>), and in <see cref="Sync"/>. Keeping the viewport in
/// physical pixels is what lets 100% zoom honestly mean one image pixel per screen pixel on a
/// scaled display.
///
/// The scale is the one WPF reports, and the application declares no DPI awareness of its
/// own, so it runs system-DPI-aware: on a monitor scaled differently from the system scale,
/// WPF still reports the system's, Windows stretches the window, and 100% is not one to one
/// there.
///
/// The divider is the exception: it lives in device-independent pixels throughout, because
/// its position is a fraction of the window and never touches image coordinates.
/// </summary>
public partial class CompareView : UserControl
{
    private const double WheelZoomFactor = 1.15;

    /// <summary>Physical pixels scrolled per wheel notch.</summary>
    private const double WheelPanPixels = 140;

    private const int ThumbnailMaxWidth = 380;
    private const int ThumbnailMaxHeight = 300;

    private readonly TranslateTransform _translate = new();
    private readonly ScaleTransform _scale = new();
    private readonly TransformGroup _transform = new();

    private double _deviceScale = 1.0;
    private Point? _panAnchor;
    private bool _draggingDivider;
    private bool _updatingScrollBars;
    private PointD? _cursorContentPoint;

    /// <summary>Builds the surface, empty until <see cref="SetPair"/>.</summary>
    public CompareView()
    {
        InitializeComponent();

        _transform.Children.Add(_translate);
        _transform.Children.Add(_scale);

        // Deliberately the same transform object on both images rather than two configured
        // alike: there is then no state to keep in step, and no way for a future change to
        // update one and forget the other.
        BeforeImage.RenderTransform = _transform;
        AfterImage.RenderTransform = _transform;
        BeforeBackdrop.RenderTransform = _transform;
        AfterBackdrop.RenderTransform = _transform;

        Canvas.SetRight(ZoomBadge, 10);
        Canvas.SetTop(ZoomBadge, 10);

        Minimap.CentreRequested += (_, point) =>
        {
            Viewport.CentreOn(point.X, point.Y);
            Sync();
        };

        VerticalBar.Scroll += (_, _) => OnScrollBarMoved();
        HorizontalBar.Scroll += (_, _) => OnScrollBarMoved();

        Loaded += (_, _) => ReadDeviceScale();
        SizeChanged += (_, _) =>
        {
            UpdateViewportSize();
            Sync();
        };
    }

    public Viewport Viewport { get; } = new();

    public DividerModel Divider { get; } = new();

    /// <summary>Raised whenever zoom, pan or the divider moves, for the status bar and toolbar.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>
    /// The image pixel under the pointer, or null when the pointer is off the image. Drives
    /// the pixel readout, which is often the only way to see a difference too small to notice
    /// by eye.
    /// </summary>
    public event EventHandler<PointD?>? CursorContentPointChanged;

    /// <summary>
    /// Shows a loaded pair: each image at its own pixel size on the shared canvas, zoomed to
    /// fit, the divider back in the middle, and the minimap's thumbnail drawn.
    /// </summary>
    /// <param name="pair">The pair, both images frozen Bgra32.</param>
    public void SetPair(ImagePair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);

        BeforeImage.Source = pair.Before.Bitmap;
        AfterImage.Source = pair.After.Bitmap;

        // Each image keeps its own pixel size, and both sit at the origin of a canvas as big
        // as the pair needs. That is the whole of the padding: a smaller image simply does not
        // reach the far edge, and the backdrop behind it is what fills the gap. No bitmap is
        // ever copied into a larger buffer — at 84 MB an image, that copy would be the most
        // expensive thing the application does.
        //
        // Sized explicitly in pixels rather than left to the natural size: WPF derives that
        // from the file's DPI metadata, so a 72 dpi export would otherwise lay out larger than
        // its own pixel count and quietly break the 1:1 zoom level.
        SetPixelSize(BeforeImage, pair.Before.Info.PixelWidth, pair.Before.Info.PixelHeight);
        SetPixelSize(AfterImage, pair.After.Info.PixelWidth, pair.After.Info.PixelHeight);

        SetPixelSize(BeforeBackdrop, pair.PixelWidth, pair.PixelHeight);
        SetPixelSize(AfterBackdrop, pair.PixelWidth, pair.PixelHeight);
        BeforeBackdrop.Visibility = Visibility.Visible;
        AfterBackdrop.Visibility = Visibility.Visible;

        Viewport.SetContentSize(pair.PixelWidth, pair.PixelHeight);
        UpdateViewportSize();
        Viewport.ZoomToFit();
        Divider.Reset();

        Minimap.Show(
            CreateThumbnail(pair.Before.Bitmap, pair.PixelWidth, pair.PixelHeight, ThumbnailMaxWidth, ThumbnailMaxHeight),
            pair.PixelWidth,
            pair.PixelHeight,
            Viewport);

        Sync();
    }

    /// <summary>
    /// Takes the pair off the surface, so that nothing here refers to its bitmaps any more, and
    /// empties the viewport.
    /// </summary>
    public void Clear()
    {
        BeforeImage.Source = null;
        AfterImage.Source = null;
        BeforeBackdrop.Visibility = Visibility.Collapsed;
        AfterBackdrop.Visibility = Visibility.Collapsed;
        Minimap.Hide();
        Viewport.SetContentSize(0, 0);
        Sync();
    }

    /// <summary>
    /// Zooms in one step as <see cref="Core.Viewport.ZoomIn"/> does, anchored at the centre of
    /// the view.
    /// </summary>
    public void ZoomIn() => ZoomAtCentre(step: 1);

    /// <summary>
    /// Zooms out one step as <see cref="Core.Viewport.ZoomOut"/> does, anchored at the centre of
    /// the view; when no stop is below the scale it goes to the fit, never in.
    /// </summary>
    public void ZoomOut() => ZoomAtCentre(step: -1);

    /// <summary>Zooms so the whole canvas is visible, and centres it.</summary>
    public void ZoomToFit()
    {
        Viewport.ZoomToFit();
        Sync();
    }

    /// <summary>
    /// Zooms to 100%, one image pixel per physical pixel of the viewport (see the class summary
    /// on monitors scaled differently), anchored at the centre of the view.
    /// </summary>
    public void ZoomToActualSize()
    {
        Viewport.ZoomToActualSize();
        Sync();
    }

    /// <summary>
    /// Reads the display's scale again when WPF reports that the scale this control is drawn at
    /// has changed.
    /// </summary>
    /// <param name="oldDpi">The scale before; not read.</param>
    /// <param name="newDpi">
    /// The scale after; not read: the control asks <see cref="VisualTreeHelper"/>.
    /// </param>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ReadDeviceScale();
    }

    /// <summary>
    /// Zooms by a fixed factor per notch, anchored at the cursor; with Shift held, pans sideways
    /// instead. Does nothing until a pair is shown.
    /// </summary>
    /// <param name="e">
    /// The wheel event: the sign of its delta is the direction, and its position the anchor.
    /// Once a pair is shown, it is marked handled.
    /// </param>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (!Viewport.HasContent)
        {
            return;
        }

        Point position = ToDevice(e.GetPosition(this));

        // The wheel zooms, anchored at the cursor. On a comparison view what you want from
        // the wheel is almost always "look closer at what is under the pointer", and panning
        // is already covered by dragging, the scroll bars and the minimap. Ctrl+wheel zooms
        // too, so the habit from every other image viewer keeps working.
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            Viewport.PanBy(Math.Sign(e.Delta) * WheelPanPixels, 0);
        }
        else
        {
            double factor = e.Delta > 0 ? WheelZoomFactor : 1 / WheelZoomFactor;
            Viewport.ZoomBy(factor, position.X, position.Y);
        }

        Sync();
        e.Handled = true;
    }

    /// <summary>
    /// Takes the keyboard focus and, once a pair is shown, starts a drag: of the divider when the
    /// press is on it, otherwise a pan.
    /// </summary>
    /// <param name="e">
    /// The press; its position decides which drag, and once a pair is shown it is marked
    /// handled.
    /// </param>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();

        if (!Viewport.HasContent)
        {
            return;
        }

        Point position = e.GetPosition(this);

        // Grabbing the divider takes precedence over panning, so the line can be adjusted
        // without first having to aim at a mode button.
        if (Divider.HitTest(position.X, position.Y, ActualWidth, ActualHeight))
        {
            _draggingDivider = true;
            Cursor = Cursors.SizeWE;
        }
        else
        {
            _panAnchor = position;
            Cursor = Cursors.SizeAll;
        }

        CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// Moves the divider or pans while a drag is on; otherwise shows the resize cursor over the
    /// divider and reports the image pixel under the pointer.
    /// </summary>
    /// <param name="e">The move; its position is read.</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        Point position = e.GetPosition(this);

        if (_draggingDivider)
        {
            Divider.MoveTo(position.X, ActualWidth);
            Sync();
            return;
        }

        if (_panAnchor is { } anchor)
        {
            Viewport.PanBy((position.X - anchor.X) * _deviceScale, (position.Y - anchor.Y) * _deviceScale);
            _panAnchor = position;
            Sync();
            return;
        }

        Cursor = Divider.HitTest(position.X, position.Y, ActualWidth, ActualHeight)
            ? Cursors.SizeWE
            : Cursors.Arrow;

        UpdateCursorContentPoint(position);
    }

    /// <summary>
    /// Reports that the pointer is off the image, which clears the pixel readout.
    /// </summary>
    /// <param name="e">The event; not read.</param>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        SetCursorContentPoint(null);
    }

    /// <summary>Ends a divider drag or a pan.</summary>
    /// <param name="e">The release; not read.</param>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        EndDrag();
    }

    /// <summary>Ends a drag whose mouse capture was taken away, by a dialog for instance.</summary>
    /// <param name="e">The event; not read.</param>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndDrag();
    }

    /// <summary>
    /// Handles the surface's own keys: the fit and 100% shortcuts, the zoom steps, and the
    /// divider nudge (<see cref="DividerModel.NudgeBy"/>); any other key is left to the window.
    /// </summary>
    /// <param name="e">The key; marked handled when it is one of these.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        double nudge = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;

        switch (e.Key)
        {
            case Key.D0 or Key.NumPad0 when control:
                ZoomToFit();
                break;
            case Key.D1 or Key.NumPad1 when control:
                ZoomToActualSize();
                break;
            case Key.OemPlus or Key.Add:
                ZoomIn();
                break;
            case Key.OemMinus or Key.Subtract:
                ZoomOut();
                break;
            case Key.Left:
                Divider.NudgeBy(-nudge, ActualWidth);
                Sync();
                break;
            case Key.Right:
                Divider.NudgeBy(nudge, ActualWidth);
                Sync();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Sizes an element in WPF units equal to its image pixels; the shared transform does all of
    /// the scaling.
    /// </summary>
    /// <param name="element">An image or a backdrop.</param>
    /// <param name="width">The width, in image pixels.</param>
    /// <param name="height">The height, in image pixels.</param>
    private static void SetPixelSize(FrameworkElement element, double width, double height)
    {
        element.Width = width;
        element.Height = height;
    }

    /// <summary>
    /// Renders a thumbnail once, up front, rather than letting the minimap scale the full
    /// image on every repaint.
    /// </summary>
    /// <remarks>
    /// The thumbnail is of the shared canvas, not of the image: the minimap draws the viewport
    /// rectangle against the content size, so a padded pair whose thumbnail had the image's own
    /// aspect ratio would put that rectangle in the wrong place.
    /// </remarks>
    /// <param name="source">Image A, drawn at the canvas's top-left corner.</param>
    /// <param name="contentWidth">The canvas width, in image pixels.</param>
    /// <param name="contentHeight">The canvas height, in image pixels.</param>
    /// <param name="maxWidth">The widest the thumbnail may be, in pixels.</param>
    /// <param name="maxHeight">The tallest the thumbnail may be, in pixels.</param>
    /// <returns>
    /// A frozen bitmap of the canvas at the largest size that fits, at least 1 pixel each way,
    /// black where image A does not reach.
    /// </returns>
    private static BitmapSource CreateThumbnail(
        BitmapSource source, double contentWidth, double contentHeight, int maxWidth, int maxHeight)
    {
        SizeD box = AspectFit.Fit(contentWidth, contentHeight, maxWidth, maxHeight);
        int width = Math.Max(1, (int)Math.Round(box.Width));
        int height = Math.Max(1, (int)Math.Round(box.Height));

        double scale = box.Width / contentWidth;

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
            context.DrawImage(source, new Rect(0, 0, source.PixelWidth * scale, source.PixelHeight * scale));
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    /// <summary>
    /// Ends a divider drag or a pan, if one is on: releases the mouse and puts the arrow cursor
    /// back.
    /// </summary>
    private void EndDrag()
    {
        if (!_draggingDivider && _panAnchor is null)
        {
            return;
        }

        _draggingDivider = false;
        _panAnchor = null;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
    }

    /// <summary>
    /// Pans to where the user scrolled a scrollbar: by dragging its thumb, clicking it, or its
    /// context menu.
    /// </summary>
    private void OnScrollBarMoved()
    {
        if (_updatingScrollBars)
        {
            return;
        }

        Viewport.SetOffset(HorizontalBar.Value, VerticalBar.Value);
        Sync();
    }

    /// <summary>Steps the zoom ladder, anchored at the centre of the view.</summary>
    /// <param name="step">Above 0 steps up a stop; 0 or below steps down one.</param>
    private void ZoomAtCentre(int step)
    {
        double centreX = ActualWidth * _deviceScale / 2;
        double centreY = ActualHeight * _deviceScale / 2;

        if (step > 0)
        {
            Viewport.ZoomIn(centreX, centreY);
        }
        else
        {
            Viewport.ZoomOut(centreX, centreY);
        }

        Sync();
    }

    /// <summary>Works out the image pixel under a pointer position, and reports it.</summary>
    /// <param name="position">
    /// The pointer, in device-independent pixels from the surface's top-left corner.
    /// </param>
    private void UpdateCursorContentPoint(Point position) =>
        SetCursorContentPoint(
            Viewport.ContentPointInsideAt(position.X * _deviceScale, position.Y * _deviceScale));

    /// <summary>
    /// Raises <see cref="CursorContentPointChanged"/> when the pointer has moved onto another
    /// whole pixel, onto the image or off it.
    /// </summary>
    /// <param name="point">
    /// The image point under the pointer, in image pixels; null when the pointer is off the
    /// image.
    /// </param>
    private void SetCursorContentPoint(PointD? point)
    {
        // Only report whole-pixel changes: the readout cannot show more than that, and a
        // mouse move of a fraction of a pixel should not make it flicker.
        if (Same(_cursorContentPoint, point))
        {
            return;
        }

        _cursorContentPoint = point;
        CursorContentPointChanged?.Invoke(this, point);

        static bool Same(PointD? a, PointD? b) => (a, b) switch
        {
            (null, null) => true,
            (not null, not null) => (int)a.Value.X == (int)b.Value.X && (int)a.Value.Y == (int)b.Value.Y,
            _ => false,
        };
    }

    /// <summary>Converts a WPF position to physical pixels.</summary>
    /// <param name="deviceIndependent">The position, in device-independent pixels.</param>
    /// <returns>The same position, in the viewport's physical pixels.</returns>
    private Point ToDevice(Point deviceIndependent) =>
        new(deviceIndependent.X * _deviceScale, deviceIndependent.Y * _deviceScale);

    /// <summary>
    /// Reads the scale WPF draws this control at, device pixels per device-independent pixel
    /// (the system's scale on every monitor; see the class summary), then gives the viewport
    /// its size again and redraws.
    /// </summary>
    private void ReadDeviceScale()
    {
        _deviceScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        UpdateViewportSize();
        Sync();
    }

    /// <summary>Gives the viewport the surface's size, converted to physical pixels.</summary>
    private void UpdateViewportSize() =>
        Viewport.SetViewportSize(ActualWidth * _deviceScale, ActualHeight * _deviceScale);

    /// <summary>
    /// Puts the viewport's state on screen: the shared transform and the resampling mode, the
    /// overlay, the scrollbars and the minimap; then raises <see cref="ViewChanged"/>.
    /// </summary>
    private void Sync()
    {
        RenderPlan plan = RenderPlanner.For(Viewport, _deviceScale);

        _translate.X = plan.TranslateX;
        _translate.Y = plan.TranslateY;
        _scale.ScaleX = plan.RenderScale;
        _scale.ScaleY = plan.RenderScale;

        BitmapScalingMode mode = plan.ScalingMode == ImageScalingMode.Pixelated
            ? BitmapScalingMode.NearestNeighbor
            : BitmapScalingMode.HighQuality;
        RenderOptions.SetBitmapScalingMode(BeforeImage, mode);
        RenderOptions.SetBitmapScalingMode(AfterImage, mode);

        UpdateOverlay();
        UpdateScrollBars();
        Minimap.Refresh();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sets both scrollbars from the viewport. Setting them raises no Scroll event, which WPF
    /// raises only for the user's scrolling, so this never pans the viewport back.
    /// </summary>
    private void UpdateScrollBars()
    {
        _updatingScrollBars = true;
        try
        {
            Apply(HorizontalBar, ScrollBarStates.Horizontal(Viewport));
            Apply(VerticalBar, ScrollBarStates.Vertical(Viewport));
        }
        finally
        {
            _updatingScrollBars = false;
        }

        static void Apply(ScrollBar bar, ScrollBarState state)
        {
            bar.Minimum = 0;
            bar.Maximum = state.Maximum;
            bar.ViewportSize = state.ViewportSize;
            bar.Value = state.Value;
            bar.Visibility = state.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Lays out what is drawn over the images: the zoom badge, the divider line and its handle,
    /// and the clip that shows image A left of the line. Does nothing before the surface has a
    /// size.
    /// </summary>
    private void UpdateOverlay()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        Overlay.Visibility = BeforeImage.Source is null ? Visibility.Collapsed : Visibility.Visible;
        ZoomBadgeText.Text = $"{Viewport.Scale * 100:F0}%";

        DividerLayout layout = Divider.Layout(ActualWidth, ActualHeight);

        // The clip is applied to the layer, which fills the viewport, rather than to the image
        // itself: a clip on the image would be measured in image coordinates and would travel
        // with the pan instead of staying put on screen.
        BeforeLayer.Clip = new RectangleGeometry(ToRect(layout.ClipRect));

        DividerLine.Height = ActualHeight;
        Canvas.SetLeft(DividerLine, layout.LineX - (DividerLine.Width / 2));
        Canvas.SetTop(DividerLine, 0);

        DividerHandle.Width = layout.Handle.Width;
        DividerHandle.Height = layout.Handle.Height;
        Canvas.SetLeft(DividerHandle, layout.Handle.X);
        Canvas.SetTop(DividerHandle, layout.Handle.Y);
    }

    /// <summary>Converts a Core rectangle to a WPF one.</summary>
    /// <param name="rect">The rectangle, in any unit; the unit is kept.</param>
    /// <returns>The same rectangle, as a <see cref="Rect"/>.</returns>
    private static Rect ToRect(RectD rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
