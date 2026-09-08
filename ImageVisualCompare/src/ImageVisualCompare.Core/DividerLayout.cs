namespace ImageVisualCompare.Core;

/// <summary>
/// Where every piece of the divider goes, for one viewport size. All of it is in
/// device-independent pixels, because the divider is a fraction of the window and never
/// touches image coordinates.
/// </summary>
/// <param name="LineX">Centre of the line.</param>
/// <param name="ClipRect">The region of the surface in which image A is shown.</param>
/// <param name="Handle">The grab handle, centred vertically on the line.</param>
public readonly record struct DividerLayout(
    double LineX,
    RectD ClipRect,
    RectD Handle,
    PointD BeforeBadgeOrigin,
    bool ShowBeforeBadge,
    PointD AfterBadgeOrigin,
    bool ShowAfterBadge);
