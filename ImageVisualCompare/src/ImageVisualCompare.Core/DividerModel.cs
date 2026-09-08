namespace ImageVisualCompare.Core;

/// <summary>
/// The vertical split line between the two images.
///
/// Its position is stored as a fraction of the viewport width — screen space, not content
/// space — so it stays where the user put it while they zoom and pan underneath. That is
/// the behaviour of a before/after slider: the line belongs to the window, not to the image.
/// </summary>
public sealed class DividerModel
{
    /// <summary>Width of the centred drag handle, in device-independent pixels.</summary>
    public const double HandleWidth = 26;

    /// <summary>Height of the centred drag handle, in device-independent pixels.</summary>
    public const double HandleHeight = 56;

    /// <summary>How far from the line a press still counts as grabbing it.</summary>
    public const double LineGrabTolerance = 8;

    /// <summary>Inset of the BEFORE and AFTER badges from the line and from the top edge.</summary>
    public const double BadgeMargin = 10;

    private const double BeforeBadgeMinSpace = 70;
    private const double AfterBadgeMinSpace = 140;

    private double _position = 0.5;

    /// <summary>Position as a fraction of the viewport width, 0 at the left edge and 1 at the right.</summary>
    public double Position
    {
        get => _position;
        set => _position = Math.Clamp(value, 0, 1);
    }

    public double ScreenX(double viewportWidth) => _position * viewportWidth;

    public double HandleCentreY(double viewportHeight) => viewportHeight / 2;

    public void Reset() => _position = 0.5;

    /// <summary>Moves the line to a screen position, used while dragging.</summary>
    public void MoveTo(double screenX, double viewportWidth)
    {
        Position = viewportWidth > 0 ? screenX / viewportWidth : 0.5;
    }

    /// <summary>Shifts the line by a screen-space amount, used by the arrow keys.</summary>
    public void NudgeBy(double screenDelta, double viewportWidth)
    {
        if (viewportWidth > 0)
        {
            Position = _position + screenDelta / viewportWidth;
        }
    }

    /// <summary>
    /// Places the line, the clip region for image A, the handle and the two badges for a
    /// viewport of this size.
    ///
    /// The badges disappear rather than being clipped in half when the line is dragged close
    /// to an edge; the AFTER side needs more room than the BEFORE side because the zoom badge
    /// occupies the top-right corner.
    /// </summary>
    public DividerLayout Layout(double viewportWidth, double viewportHeight)
    {
        double x = ScreenX(viewportWidth);

        return new DividerLayout(
            LineX: x,
            ClipRect: new RectD(0, 0, x, viewportHeight),
            Handle: new RectD(
                x - (HandleWidth / 2),
                (viewportHeight - HandleHeight) / 2,
                HandleWidth,
                HandleHeight),
            BeforeBadgeOrigin: new PointD(BadgeMargin, BadgeMargin),
            ShowBeforeBadge: x > BeforeBadgeMinSpace,
            AfterBadgeOrigin: new PointD(x + BadgeMargin, BadgeMargin),
            ShowAfterBadge: viewportWidth - x > AfterBadgeMinSpace);
    }

    /// <summary>
    /// True when a press at this point should start a divider drag rather than a pan.
    /// The line can be grabbed anywhere along its length within <see cref="LineGrabTolerance"/>,
    /// and the handle offers a wider target for anyone aiming at the visible grip.
    /// </summary>
    public bool HitTest(double screenX, double screenY, double viewportWidth, double viewportHeight)
    {
        double distanceFromLine = Math.Abs(screenX - ScreenX(viewportWidth));

        if (distanceFromLine <= LineGrabTolerance)
        {
            return true;
        }

        return distanceFromLine <= HandleWidth / 2
               && Math.Abs(screenY - HandleCentreY(viewportHeight)) <= HandleHeight / 2;
    }
}
