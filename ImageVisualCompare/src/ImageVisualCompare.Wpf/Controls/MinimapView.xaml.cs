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

        double scale = Math.Min(MaxThumbnailWidth / contentWidth, MaxThumbnailHeight / contentHeight);
        Width = contentWidth * scale;
        Height = contentHeight * scale;

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

        double scaleX = ActualWidth / viewport.ContentWidth;
        double scaleY = ActualHeight / viewport.ContentHeight;

        // The rectangle is clamped to the thumbnail because the viewport legitimately extends
        // past the image whenever the image is smaller than the window.
        double left = Math.Max(0, viewport.OffsetX * scaleX);
        double top = Math.Max(0, viewport.OffsetY * scaleY);
        double right = Math.Min(ActualWidth, (viewport.OffsetX + viewport.VisibleContentWidth) * scaleX);
        double bottom = Math.Min(ActualHeight, (viewport.OffsetY + viewport.VisibleContentHeight) * scaleY);

        Canvas.SetLeft(ViewportRect, left);
        Canvas.SetTop(ViewportRect, top);
        ViewportRect.Width = Math.Max(2, right - left);
        ViewportRect.Height = Math.Max(2, bottom - top);

        // Nothing to point at when the whole image is already on screen.
        ViewportRect.Visibility = viewport.CanScrollHorizontally || viewport.CanScrollVertically
            ? Visibility.Visible
            : Visibility.Collapsed;
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

        CentreRequested?.Invoke(this, new PointD(
            position.X / ActualWidth * viewport.ContentWidth,
            position.Y / ActualHeight * viewport.ContentHeight));
    }
}
