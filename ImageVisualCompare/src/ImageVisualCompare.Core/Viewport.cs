namespace ImageVisualCompare.Core;

/// <summary>
/// The single source of truth for zoom and pan across the image pair. Both images are
/// drawn through this one transform, so they cannot drift apart: the synchronisation the
/// comparison depends on is structural, not something the UI has to keep up to date.
///
/// Screen space is measured in device-independent pixels from the top-left corner of the
/// viewport. Content space is measured in image pixels.
/// </summary>
public sealed class Viewport
{
    /// <summary>Beyond roughly this point a pixel is a large flat square and more zoom buys nothing.</summary>
    public const double MaxScale = 16.0;

    private double _scale = 1.0;

    public double ContentWidth { get; private set; }

    public double ContentHeight { get; private set; }

    public double ViewportWidth { get; private set; }

    public double ViewportHeight { get; private set; }

    /// <summary>Content-space coordinate drawn at the left edge of the viewport.</summary>
    public double OffsetX { get; private set; }

    /// <summary>Content-space coordinate drawn at the top edge of the viewport.</summary>
    public double OffsetY { get; private set; }

    public double Scale => _scale;

    public bool HasContent =>
        ContentWidth > 0 && ContentHeight > 0 && ViewportWidth > 0 && ViewportHeight > 0;

    /// <summary>The scale at which the whole image is visible.</summary>
    public double FitScale =>
        HasContent ? Math.Min(ViewportWidth / ContentWidth, ViewportHeight / ContentHeight) : 1.0;

    /// <summary>
    /// Zooming out past <see cref="FitScale"/> only shrinks an already fully visible image,
    /// so that is the floor. An image smaller than the viewport is the exception: there the
    /// floor is 100%, otherwise the user could never see it at its own resolution.
    /// </summary>
    public double MinScale => Math.Min(FitScale, 1.0);

    public double VisibleContentWidth => _scale > 0 ? ViewportWidth / _scale : 0;

    public double VisibleContentHeight => _scale > 0 ? ViewportHeight / _scale : 0;

    public double MaxOffsetX => Math.Max(0, ContentWidth - VisibleContentWidth);

    public double MaxOffsetY => Math.Max(0, ContentHeight - VisibleContentHeight);

    /// <summary>True when the image overflows the viewport horizontally and a scrollbar is useful.</summary>
    public bool CanScrollHorizontally => MaxOffsetX > 0;

    public bool CanScrollVertically => MaxOffsetY > 0;

    /// <summary>Sets the size of the loaded image pair. The caller normally follows with <see cref="ZoomToFit"/>.</summary>
    public void SetContentSize(double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        ContentWidth = width;
        ContentHeight = height;
        ClampScale();
        ClampOffsets();
    }

    /// <summary>
    /// Tracks a window resize. The content point at the centre of the viewport is held
    /// stationary, so growing or shrinking the window does not appear to pan the image.
    /// </summary>
    public void SetViewportSize(double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        bool hadViewport = ViewportWidth > 0 && ViewportHeight > 0;
        PointD centre = hadViewport
            ? ScreenToContent(ViewportWidth / 2, ViewportHeight / 2)
            : default;

        ViewportWidth = width;
        ViewportHeight = height;
        ClampScale();

        if (hadViewport && HasContent)
        {
            OffsetX = centre.X - VisibleContentWidth / 2;
            OffsetY = centre.Y - VisibleContentHeight / 2;
        }

        ClampOffsets();
    }

    public PointD ScreenToContent(double screenX, double screenY) =>
        new(screenX / _scale + OffsetX, screenY / _scale + OffsetY);

    public PointD ContentToScreen(double contentX, double contentY) =>
        new((contentX - OffsetX) * _scale, (contentY - OffsetY) * _scale);

    /// <summary>
    /// The image pixel under a screen position, or null when that position falls on the empty
    /// margin around the image rather than on the image itself.
    /// </summary>
    public PointD? ContentPointInsideAt(double screenX, double screenY)
    {
        if (!HasContent)
        {
            return null;
        }

        PointD content = ScreenToContent(screenX, screenY);

        bool inside = content.X >= 0 && content.Y >= 0
                      && content.X < ContentWidth && content.Y < ContentHeight;

        return inside ? content : null;
    }

    /// <summary>
    /// Zooms to <paramref name="scale"/> while holding the content point currently under the
    /// anchor fixed on screen. Anchoring at the mouse is what makes wheel zoom feel like the
    /// image is being pulled towards the cursor rather than towards its own centre.
    /// </summary>
    public void ZoomTo(double scale, double anchorScreenX, double anchorScreenY)
    {
        if (!HasContent)
        {
            return;
        }

        PointD anchor = ScreenToContent(anchorScreenX, anchorScreenY);
        _scale = Math.Clamp(scale, MinScale, MaxScale);
        OffsetX = anchor.X - anchorScreenX / _scale;
        OffsetY = anchor.Y - anchorScreenY / _scale;
        ClampOffsets();
    }

    public void ZoomBy(double factor, double anchorScreenX, double anchorScreenY) =>
        ZoomTo(_scale * factor, anchorScreenX, anchorScreenY);

    /// <summary>Steps up to the next stop on <see cref="ZoomLadder"/>.</summary>
    public void ZoomIn(double anchorScreenX, double anchorScreenY) =>
        ZoomTo(ZoomLadder.Next(_scale), anchorScreenX, anchorScreenY);

    public void ZoomOut(double anchorScreenX, double anchorScreenY) =>
        ZoomTo(ZoomLadder.Previous(_scale), anchorScreenX, anchorScreenY);

    /// <summary>Zooms so the whole image is visible and centres it.</summary>
    public void ZoomToFit()
    {
        if (!HasContent)
        {
            return;
        }

        _scale = Math.Clamp(FitScale, MinScale, MaxScale);
        ClampOffsets();
    }

    /// <summary>Zooms to one screen pixel per image pixel, anchored at the viewport centre.</summary>
    public void ZoomToActualSize() => ZoomTo(1.0, ViewportWidth / 2, ViewportHeight / 2);

    /// <summary>
    /// Pans by a screen-space drag delta. Dragging right moves the image right, which means
    /// the content offset moves left — hence the subtraction.
    /// </summary>
    public void PanBy(double screenDeltaX, double screenDeltaY)
    {
        if (!HasContent)
        {
            return;
        }

        OffsetX -= screenDeltaX / _scale;
        OffsetY -= screenDeltaY / _scale;
        ClampOffsets();
    }

    /// <summary>Sets the offsets directly, for scrollbar and minimap driven panning.</summary>
    public void SetOffset(double offsetX, double offsetY)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        ClampOffsets();
    }

    /// <summary>Centres the viewport on a content point, used when the minimap is clicked.</summary>
    public void CentreOn(double contentX, double contentY) =>
        SetOffset(contentX - VisibleContentWidth / 2, contentY - VisibleContentHeight / 2);

    private void ClampScale()
    {
        if (HasContent)
        {
            _scale = Math.Clamp(_scale, MinScale, MaxScale);
        }
    }

    private void ClampOffsets()
    {
        OffsetX = ClampAxis(OffsetX, ContentWidth, VisibleContentWidth);
        OffsetY = ClampAxis(OffsetY, ContentHeight, VisibleContentHeight);

        static double ClampAxis(double offset, double content, double visible)
        {
            // When the axis fits entirely, centre it. The resulting offset is negative,
            // which leaves equal empty margins on both sides.
            if (visible >= content)
            {
                return (content - visible) / 2;
            }

            return Math.Clamp(offset, 0, content - visible);
        }
    }
}
