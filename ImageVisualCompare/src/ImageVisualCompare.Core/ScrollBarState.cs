namespace ImageVisualCompare.Core;

/// <param name="Maximum">Highest offset the bar can scroll to; the minimum is always zero.</param>
/// <param name="ViewportSize">Visible extent, which is what sets the thumb's proportion.</param>
/// <param name="Value">Current offset, clamped into range.</param>
/// <param name="IsVisible">False when the image fits and there is nothing to scroll.</param>
public readonly record struct ScrollBarState(
    double Maximum,
    double ViewportSize,
    double Value,
    bool IsVisible);

public static class ScrollBarStates
{
    public static ScrollBarState Horizontal(Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        return new ScrollBarState(
            viewport.MaxOffsetX,
            viewport.VisibleContentWidth,
            Math.Clamp(viewport.OffsetX, 0, viewport.MaxOffsetX),
            viewport.CanScrollHorizontally);
    }

    public static ScrollBarState Vertical(Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        return new ScrollBarState(
            viewport.MaxOffsetY,
            viewport.VisibleContentHeight,
            Math.Clamp(viewport.OffsetY, 0, viewport.MaxOffsetY),
            viewport.CanScrollVertically);
    }
}
