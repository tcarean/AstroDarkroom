using System.Windows;
using System.Windows.Controls;
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
/// in device-independent pixels, and the conversion happens only in this control's input
/// handlers and in <see cref="Sync"/>. Keeping the viewport in physical pixels is what lets
/// 100% zoom honestly mean one image pixel per screen pixel on a scaled display.
///
/// The divider is the exception: it lives in device-independent pixels throughout, because
/// its position is a fraction of the window and never touches image coordinates.
/// </summary>
public partial class CompareView : UserControl
{
    private const double WheelZoomFactor = 1.15;

    /// <summary>Physical pixels scrolled per wheel notch.</summary>
    private const double WheelPanPixels = 140;

    /// <summary>Above this zoom the interpolation is hiding what the user came to look at.</summary>
    private const double NearestNeighbourThreshold = 4.0;

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

    public void SetPair(ImagePair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);

        BeforeImage.Source = pair.Before.Bitmap;
        AfterImage.Source = pair.After.Bitmap;

        // Sized explicitly in pixels rather than left to the natural size: WPF derives that
        // from the file's DPI metadata, so a 72 dpi export would otherwise lay out larger than
        // its own pixel count and quietly break the 1:1 zoom level.
        foreach (Image image in new[] { BeforeImage, AfterImage })
        {
            image.Width = pair.PixelWidth;
            image.Height = pair.PixelHeight;
        }

        BeforeBackdrop.Width = pair.PixelWidth;
        BeforeBackdrop.Height = pair.PixelHeight;
        BeforeBackdrop.Visibility = Visibility.Visible;

        Viewport.SetContentSize(pair.PixelWidth, pair.PixelHeight);
        UpdateViewportSize();
        Viewport.ZoomToFit();
        Divider.Reset();

        Minimap.Show(
            CreateThumbnail(pair.Before.Bitmap, ThumbnailMaxWidth, ThumbnailMaxHeight),
            pair.PixelWidth,
            pair.PixelHeight,
            Viewport);

        Sync();
    }

    public void Clear()
    {
        BeforeImage.Source = null;
        AfterImage.Source = null;
        BeforeBackdrop.Visibility = Visibility.Collapsed;
        Minimap.Hide();
        Viewport.SetContentSize(0, 0);
        Sync();
    }

    public void ZoomIn() => ZoomAtCentre(step: 1);

    public void ZoomOut() => ZoomAtCentre(step: -1);

    public void ZoomToFit()
    {
        Viewport.ZoomToFit();
        Sync();
    }

    public void ZoomToActualSize()
    {
        Viewport.ZoomToActualSize();
        Sync();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ReadDeviceScale();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (!Viewport.HasContent)
        {
            return;
        }

        Point position = ToDevice(e.GetPosition(this));

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            double factor = e.Delta > 0 ? WheelZoomFactor : 1 / WheelZoomFactor;
            Viewport.ZoomBy(factor, position.X, position.Y);
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            Viewport.PanBy(Math.Sign(e.Delta) * WheelPanPixels, 0);
        }
        else
        {
            Viewport.PanBy(0, Math.Sign(e.Delta) * WheelPanPixels);
        }

        Sync();
        e.Handled = true;
    }

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

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        SetCursorContentPoint(null);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        EndDrag();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndDrag();
    }

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
    /// Renders a thumbnail once, up front, rather than letting the minimap scale the full
    /// image on every repaint.
    /// </summary>
    private static BitmapSource CreateThumbnail(BitmapSource source, int maxWidth, int maxHeight)
    {
        double scale = Math.Min(maxWidth / (double)source.PixelWidth, maxHeight / (double)source.PixelHeight);
        int width = Math.Max(1, (int)Math.Round(source.PixelWidth * scale));
        int height = Math.Max(1, (int)Math.Round(source.PixelHeight * scale));

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawImage(source, new Rect(0, 0, width, height));
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

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

    private void OnScrollBarMoved()
    {
        if (_updatingScrollBars)
        {
            return;
        }

        Viewport.SetOffset(HorizontalBar.Value, VerticalBar.Value);
        Sync();
    }

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

    private void UpdateCursorContentPoint(Point position)
    {
        PointD content = Viewport.ScreenToContent(position.X * _deviceScale, position.Y * _deviceScale);

        bool inside = content.X >= 0 && content.Y >= 0
                      && content.X < Viewport.ContentWidth && content.Y < Viewport.ContentHeight;

        SetCursorContentPoint(inside ? content : null);
    }

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

    private Point ToDevice(Point deviceIndependent) =>
        new(deviceIndependent.X * _deviceScale, deviceIndependent.Y * _deviceScale);

    private void ReadDeviceScale()
    {
        _deviceScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        UpdateViewportSize();
        Sync();
    }

    private void UpdateViewportSize() =>
        Viewport.SetViewportSize(ActualWidth * _deviceScale, ActualHeight * _deviceScale);

    private void Sync()
    {
        _translate.X = -Viewport.OffsetX;
        _translate.Y = -Viewport.OffsetY;

        double renderScale = Viewport.Scale / _deviceScale;
        _scale.ScaleX = renderScale;
        _scale.ScaleY = renderScale;

        BitmapScalingMode mode = Viewport.Scale >= NearestNeighbourThreshold
            ? BitmapScalingMode.NearestNeighbor
            : BitmapScalingMode.HighQuality;
        RenderOptions.SetBitmapScalingMode(BeforeImage, mode);
        RenderOptions.SetBitmapScalingMode(AfterImage, mode);

        UpdateOverlay();
        UpdateScrollBars();
        Minimap.Refresh();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateScrollBars()
    {
        _updatingScrollBars = true;
        try
        {
            HorizontalBar.Minimum = 0;
            HorizontalBar.Maximum = Viewport.MaxOffsetX;
            HorizontalBar.ViewportSize = Viewport.VisibleContentWidth;
            HorizontalBar.Value = Math.Max(0, Viewport.OffsetX);
            HorizontalBar.Visibility = Viewport.CanScrollHorizontally ? Visibility.Visible : Visibility.Collapsed;

            VerticalBar.Minimum = 0;
            VerticalBar.Maximum = Viewport.MaxOffsetY;
            VerticalBar.ViewportSize = Viewport.VisibleContentHeight;
            VerticalBar.Value = Math.Max(0, Viewport.OffsetY);
            VerticalBar.Visibility = Viewport.CanScrollVertically ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _updatingScrollBars = false;
        }
    }

    private void UpdateOverlay()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        Overlay.Visibility = BeforeImage.Source is null ? Visibility.Collapsed : Visibility.Visible;
        ZoomBadgeText.Text = $"{Viewport.Scale * 100:F0}%";

        // The clip is applied to the layer, which fills the viewport, rather than to the image
        // itself: a clip on the image would be measured in image coordinates and would travel
        // with the pan instead of staying put on screen.
        double x = Divider.ScreenX(ActualWidth);
        BeforeLayer.Clip = new RectangleGeometry(new Rect(0, 0, x, ActualHeight));

        DividerLine.Height = ActualHeight;
        Canvas.SetLeft(DividerLine, x - DividerLine.Width / 2);
        Canvas.SetTop(DividerLine, 0);

        Canvas.SetLeft(DividerHandle, x - DividerHandle.Width / 2);
        Canvas.SetTop(DividerHandle, (ActualHeight - DividerHandle.Height) / 2);

        Canvas.SetLeft(BeforeBadge, 10);
        Canvas.SetTop(BeforeBadge, 10);
        BeforeBadge.Visibility = x > 70 ? Visibility.Visible : Visibility.Collapsed;

        Canvas.SetLeft(AfterBadge, x + 10);
        Canvas.SetTop(AfterBadge, 10);
        AfterBadge.Visibility = ActualWidth - x > 140 ? Visibility.Visible : Visibility.Collapsed;
    }
}
