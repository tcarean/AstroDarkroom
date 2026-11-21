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
using ImageVisualCompare.Wpf.Logging;
using NLog;

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
/// The scale is the one WPF reports for the monitor the window is on: the application is
/// per-monitor DPI aware (its manifest, ADR 0034), so a move to a monitor scaled differently
/// raises <see cref="OnDpiChanged"/>, and the scale is read again there.
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

    /// <summary>
    /// The pair set on the surface and the zoom each command left, in the diagnostic log (R-121);
    /// nothing per wheel notch, drag or scroll, which come many times a second (ADR 0043).
    /// </summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Surface);

    private readonly TranslateTransform _translate = new();
    private readonly ScaleTransform _scale = new();
    private readonly TransformGroup _transform = new();

    private double _deviceScale = 1.0;
    private Point? _panAnchor;
    private bool _draggingDivider;
    private bool _updatingScrollBars;
    private ImagePixel? _cursorPixel;

    /// <summary>
    /// Where the pointer was last seen on the surface, in device-independent pixels from its
    /// top-left corner; null before the first move and once the pointer has left. Kept so that
    /// the pixel under a pointer at rest is reported again when the view or the pair changes
    /// under it (R-051).
    /// </summary>
    private Point? _pointer;

    /// <summary>
    /// True while a repeat of a held key is handled: the zoom it steps is not logged, since a held
    /// <c>+</c> repeats some thirty times a second.
    /// </summary>
    private bool _keyRepeat;

    /// <summary>
    /// True once a repeat of a held zoom key has stepped the zoom without a line: the key's release
    /// writes the zoom the repeats left.
    /// </summary>
    private bool _zoomHeld;

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
    /// by eye. Raised when the pointer reaches another pixel, when the zoom or the pan brings
    /// another pixel under a pointer at rest, and when a pair is shown, whatever the pixel
    /// (R-051).
    /// </summary>
    public event EventHandler<ImagePixel?>? CursorPixelChanged;

    /// <summary>
    /// Shows a loaded pair: each image at its own pixel size on the shared canvas, zoomed to
    /// fit, the divider back in the middle, and the minimap's thumbnail drawn. The pixel under a
    /// pointer at rest is reported again (<see cref="CursorPixelChanged"/>), since it is a pixel
    /// of other images now.
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
        ApplyOverlaysHidden();

        Sync(pairChanged: true);
        LogView("pair set");
    }

    /// <summary>
    /// Puts another rendering on the same canvas in place of the one shown: the same files in
    /// another display mode (R-113), or one file changed with the canvas size unchanged (R-065).
    /// The two bitmaps and the minimap's thumbnail change, the zoom, the pan and the divider stay
    /// where they are. The caller guarantees the canvas size: neither the backdrops nor the
    /// viewport's content size are touched here. The pixel under a pointer at rest is reported
    /// again (<see cref="CursorPixelChanged"/>), though it is the same pixel: its values are
    /// those of other bitmaps now.
    /// </summary>
    /// <param name="pair">The pair decoded again, both images frozen Bgra32.</param>
    public void ReplacePair(ImagePair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);

        BeforeImage.Source = pair.Before.Bitmap;
        AfterImage.Source = pair.After.Bitmap;
        SetPixelSize(BeforeImage, pair.Before.Info.PixelWidth, pair.Before.Info.PixelHeight);
        SetPixelSize(AfterImage, pair.After.Info.PixelWidth, pair.After.Info.PixelHeight);

        Minimap.Show(
            CreateThumbnail(pair.Before.Bitmap, pair.PixelWidth, pair.PixelHeight, ThumbnailMaxWidth, ThumbnailMaxHeight),
            pair.PixelWidth,
            pair.PixelHeight,
            Viewport);
        ApplyOverlaysHidden();

        Sync(pairChanged: true);
        LogView("pair replaced, the view kept");
    }

    /// <summary>
    /// Whether the minimap and the zoom badge, the two things drawn over the corners of the
    /// image, are hidden (R-067). It holds through <see cref="SetPair"/> and
    /// <see cref="ReplacePair"/>.
    /// </summary>
    public bool OverlaysHidden { get; private set; }

    /// <summary>
    /// Hides the minimap and the zoom badge, or shows them again when they are hidden (R-067):
    /// from 100% up the image's bottom-left corner can only be brought under the one and its
    /// top-right corner under the other (ADR 0059). The scrollbars and the divider stay.
    /// </summary>
    public void ToggleOverlays()
    {
        OverlaysHidden = !OverlaysHidden;
        ApplyOverlaysHidden();
    }

    /// <summary>
    /// Puts <see cref="OverlaysHidden"/> on the minimap and the zoom badge. Called at every
    /// toggle, and after <c>MinimapView.Show</c>, which makes the minimap visible for the pair it
    /// is given. With no pair on the surface the minimap is left as it is, hidden: it has no
    /// thumbnail to show.
    /// </summary>
    private void ApplyOverlaysHidden()
    {
        Visibility visibility = OverlaysHidden ? Visibility.Collapsed : Visibility.Visible;
        ZoomBadge.Visibility = visibility;

        if (BeforeImage.Source is not null)
        {
            Minimap.Visibility = visibility;

            // Its rectangle was not laid out while it had no size.
            Minimap.Refresh();
        }
    }

    /// <summary>
    /// Zooms in one step as <see cref="Core.Viewport.ZoomIn"/> does, anchored at the centre of
    /// the view.
    /// </summary>
    public void ZoomIn()
    {
        ZoomAtCentre(step: 1);
        LogZoom("in");
    }

    /// <summary>
    /// Zooms out one step as <see cref="Core.Viewport.ZoomOut"/> does, anchored at the centre of
    /// the view; when no stop is below the scale it goes to the fit, never in.
    /// </summary>
    public void ZoomOut()
    {
        ZoomAtCentre(step: -1);
        LogZoom("out");
    }

    /// <summary>Zooms so the whole canvas is visible, and centres it.</summary>
    public void ZoomToFit()
    {
        Viewport.ZoomToFit();
        Sync();
        LogZoom("to fit");
    }

    /// <summary>
    /// Zooms to 100%, one image pixel per physical pixel of the monitor the window is on,
    /// anchored at the centre of the view.
    /// </summary>
    public void ZoomToActualSize()
    {
        Viewport.ZoomToActualSize();
        Sync();
        LogZoom("to 100%");
    }

    /// <summary>
    /// Reads the display's scale again when WPF reports that the scale this control is drawn at
    /// has changed, as it does when the window moves to a monitor scaled differently.
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
    /// Takes the keyboard focus at a press of the left button anywhere on the surface, the
    /// minimap and the scrollbars included (R-063). On the press's way down to the part under
    /// the pointer, not in <see cref="OnMouseLeftButtonDown"/>: the minimap handles its own
    /// press and a scrollbar's thumb its own, and WPF does not call that method for a press
    /// already handled, so a drag in either left the focus where it was, on a toolbar button
    /// reached by Tab for instance.
    /// </summary>
    /// <param name="e">The press; not read, and not marked handled.</param>
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        Focus();
    }

    /// <summary>
    /// Once a pair is shown, starts a drag: of the divider when the press is on it, otherwise a
    /// pan. The focus was taken on the press's way down (<see cref="OnPreviewMouseLeftButtonDown"/>).
    /// </summary>
    /// <param name="e">
    /// The press; its position decides which drag, and once a pair is shown it is marked
    /// handled.
    /// </param>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

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
    /// Keeps the pointer's position at every move over the surface or while one of its parts
    /// holds the mouse (R-051). On the way down to the part under the pointer, not in
    /// <see cref="OnMouseMove"/>: a scrollbar's thumb marks the moves of its own drag handled,
    /// so they never reach that method, while each of them scrolls and ends in
    /// <see cref="Sync"/>, which reports the pixel under the position kept here.
    /// </summary>
    /// <param name="e">The move; its position is read.</param>
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        _pointer = e.GetPosition(this);
    }

    /// <summary>
    /// Moves the divider or pans while a drag is on; otherwise shows the resize cursor over the
    /// divider. Either way the image pixel now under the pointer is reported: by the drag's
    /// <see cref="Sync"/>, or here.
    /// </summary>
    /// <param name="e">The move; its position is read.</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // The same position OnPreviewMouseMove has just kept for this move.
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

        SetCursorPixel(PixelUnderPointer());
    }

    /// <summary>
    /// Forgets the pointer's position and reports that it is off the image, which clears the
    /// pixel readout.
    /// </summary>
    /// <param name="e">The event; not read.</param>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _pointer = null;
        SetCursorPixel(null);
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
    /// divider nudge (<see cref="DividerModel.NudgeBy"/>); Up and Down are taken and do nothing
    /// (R-063); any other key is left to the window.
    /// </summary>
    /// <param name="e">The key; marked handled when it is one of these.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        double nudge = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;

        _keyRepeat = e.IsRepeat;
        try
        {
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
                case Key.Up or Key.Down:
                    // Nothing to do, but taken: WPF's arrow-key navigation runs for an arrow
                    // nothing handled and moves the keyboard focus from the surface to a toolbar
                    // button, where Left and Right then walk the buttons and the surface's
                    // shortcuts are dead until the image is clicked. They sit beside Left and
                    // Right, so they are pressed by a slip (CR-014 of the review of 2026-11-13).
                    break;
                default:
                    return;
            }
        }
        finally
        {
            _keyRepeat = false;
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

    /// <summary>
    /// Takes a pointer position, which is kept as the pointer's last, and reports the image pixel
    /// drawn under it (<see cref="PixelUnderPointer"/>). Separate from the mouse handler so a
    /// test can give it a position.
    /// </summary>
    /// <param name="position">
    /// The pointer, in device-independent pixels from the surface's top-left corner, as the mouse
    /// handlers read it: the top-left corner of the physical pixel the pointer is on. It may lie
    /// off the surface, as during a drag that holds the mouse; it is then on no pixel.
    /// </param>
    internal void ReportPointer(Point position)
    {
        _pointer = position;
        SetCursorPixel(PixelUnderPointer());
    }

    /// <summary>
    /// Works out the image pixel drawn under the pointer's last position
    /// (<see cref="Core.Viewport.PixelAt"/>).
    /// </summary>
    /// <returns>
    /// The pixel; null when the pointer has left or was never seen, when its position is off the
    /// surface, and when it is on no pixel of the image.
    /// </returns>
    private ImagePixel? PixelUnderPointer()
    {
        // A drag holds the mouse, so its positions go on past the surface's edges, where the
        // image is not drawn though the viewport's arithmetic would still name a pixel.
        if (_pointer is not { } pointer
            || pointer.X < 0 || pointer.Y < 0 || pointer.X >= ActualWidth || pointer.Y >= ActualHeight)
        {
            return null;
        }

        return Viewport.PixelAt(pointer.X * _deviceScale, pointer.Y * _deviceScale);
    }

    /// <summary>
    /// Raises <see cref="CursorPixelChanged"/> when the pointer has moved onto another image
    /// pixel, onto the image or off it, or whatever the pixel when the caller says so.
    /// </summary>
    /// <param name="pixel">
    /// The image pixel under the pointer; null when the pointer is off the image.
    /// </param>
    /// <param name="evenIfTheSame">
    /// True to raise the event for an unchanged pixel too: the images under it have changed.
    /// </param>
    private void SetCursorPixel(ImagePixel? pixel, bool evenIfTheSame = false)
    {
        // Only a change of pixel is reported: the readout shows nothing finer, and a mouse move
        // inside one pixel should not make it flicker.
        if (_cursorPixel == pixel && !evenIfTheSame)
        {
            return;
        }

        _cursorPixel = pixel;
        CursorPixelChanged?.Invoke(this, pixel);
    }

    /// <summary>Converts a WPF position to physical pixels.</summary>
    /// <param name="deviceIndependent">The position, in device-independent pixels.</param>
    /// <returns>The same position, in the viewport's physical pixels.</returns>
    private Point ToDevice(Point deviceIndependent) =>
        new(deviceIndependent.X * _deviceScale, deviceIndependent.Y * _deviceScale);

    /// <summary>
    /// Reads the scale WPF draws this control at, device pixels per device-independent pixel
    /// of the monitor it is on, then gives the viewport its size again and redraws.
    /// </summary>
    private void ReadDeviceScale()
    {
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (scale != _deviceScale)
        {
            Log.Debug("display scale " + LogText.Num(_deviceScale) + " -> " + LogText.Num(scale));
        }

        _deviceScale = scale;
        UpdateViewportSize();
        Sync();
    }

    /// <summary>
    /// Writes what the surface shows, at <c>Debug</c>: the canvas, the view in physical pixels,
    /// the display's scale and the zoom, as the viewport holds them.
    /// </summary>
    /// <param name="what">What just happened, in English: <c>pair set</c>.</param>
    private void LogView(string what)
    {
        if (Log.IsDebugEnabled)
        {
            Log.Debug(what + ": canvas " + LogText.Num(Viewport.ContentWidth) + "x" + LogText.Num(Viewport.ContentHeight)
                + ", view " + LogText.Num(Viewport.ViewportWidth) + "x" + LogText.Num(Viewport.ViewportHeight)
                + " physical px at display scale " + LogText.Num(_deviceScale) + ", zoom " + LogText.Percent(Viewport.Scale));
        }
    }

    /// <summary>
    /// Writes the zoom a command left, at <c>Debug</c>; nothing before a pair is shown, and, for a
    /// repeat of a held key, nothing until the key is released (<see cref="OnKeyUp"/>).
    /// </summary>
    /// <param name="what">The command, in English: <c>in</c>, <c>out</c>, <c>to fit</c>, <c>to 100%</c>.</param>
    private void LogZoom(string what)
    {
        if (!Viewport.HasContent || !Log.IsDebugEnabled)
        {
            return;
        }

        if (_keyRepeat)
        {
            _zoomHeld = true;
            return;
        }

        Log.Debug("zoom " + what + " -> " + LogText.Percent(Viewport.Scale));
    }

    /// <summary>
    /// Writes, when a held zoom key is released, the zoom its repeats left, which no line of theirs
    /// gave (R-121).
    /// </summary>
    /// <param name="e">The key released; not read.</param>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_zoomHeld)
        {
            _zoomHeld = false;
            LogZoom("after the held key");
        }
    }

    /// <summary>Gives the viewport the surface's size, converted to physical pixels.</summary>
    private void UpdateViewportSize() =>
        Viewport.SetViewportSize(ActualWidth * _deviceScale, ActualHeight * _deviceScale);

    /// <summary>
    /// Puts the viewport's state on screen: the shared transform and the resampling mode, the
    /// overlay, the scrollbars and the minimap; then raises <see cref="ViewChanged"/>, and
    /// reports the image pixel that is now under the pointer's last position, since a zoom or a
    /// pan moves the image under a pointer that does not move (R-051).
    /// </summary>
    /// <param name="pairChanged">
    /// True when other images were just put on the surface: the pixel under the pointer is then
    /// reported even when it is the same one, for its values to be read again.
    /// </param>
    private void Sync(bool pairChanged = false)
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
        SetCursorPixel(PixelUnderPointer(), evenIfTheSame: pairChanged);
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
