using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

public class ScrollBarStateTests
{
    private static Viewport Create(
        double contentWidth = 1000,
        double contentHeight = 1000,
        double viewportWidth = 200,
        double viewportHeight = 200)
    {
        var viewport = new Viewport();
        viewport.SetContentSize(contentWidth, contentHeight);
        viewport.SetViewportSize(viewportWidth, viewportHeight);
        return viewport;
    }

    [Fact]
    public void TheRangeIsTheScrollableOffsetAndTheThumbIsTheVisibleExtent()
    {
        Viewport viewport = Create();
        viewport.SetOffset(300, 500);

        ScrollBarState horizontal = ScrollBarStates.Horizontal(viewport);
        ScrollBarState vertical = ScrollBarStates.Vertical(viewport);

        Assert.Equal(800, horizontal.Maximum, 6);
        Assert.Equal(200, horizontal.ViewportSize, 6);
        Assert.Equal(300, horizontal.Value, 6);
        Assert.True(horizontal.IsVisible);

        Assert.Equal(500, vertical.Value, 6);
        Assert.True(vertical.IsVisible);
    }

    [Fact]
    public void AnImageThatFitsNeedsNoBars()
    {
        Viewport viewport = Create();
        viewport.ZoomToFit();

        Assert.False(ScrollBarStates.Horizontal(viewport).IsVisible);
        Assert.False(ScrollBarStates.Vertical(viewport).IsVisible);
        Assert.Equal(0, ScrollBarStates.Horizontal(viewport).Maximum, 6);
    }

    [Fact]
    public void OnlyTheOverflowingAxisGetsABar()
    {
        // Wide and short: the width overflows a square viewport, the height does not.
        Viewport viewport = Create(contentWidth: 2000, contentHeight: 100, viewportWidth: 200, viewportHeight: 200);
        viewport.ZoomTo(1.0, 100, 100);

        Assert.True(ScrollBarStates.Horizontal(viewport).IsVisible);
        Assert.False(ScrollBarStates.Vertical(viewport).IsVisible);
    }

    [Fact]
    public void TheValueNeverEscapesTheRangeTheBarAdvertises()
    {
        // At fit the offset is negative on the axis with spare room, which a scroll bar
        // cannot represent.
        Viewport viewport = Create(contentWidth: 2000, contentHeight: 100, viewportWidth: 200, viewportHeight: 200);
        viewport.ZoomTo(1.0, 100, 100);

        ScrollBarState vertical = ScrollBarStates.Vertical(viewport);

        Assert.True(viewport.OffsetY < 0);
        Assert.InRange(vertical.Value, 0, Math.Max(vertical.Maximum, 0));
    }
}
