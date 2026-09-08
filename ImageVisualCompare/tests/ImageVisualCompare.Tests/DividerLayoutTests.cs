using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

public class DividerLayoutTests
{
    private const double Width = 800;
    private const double Height = 600;

    private static DividerLayout LayoutAt(double position) =>
        new DividerModel { Position = position }.Layout(Width, Height);

    [Fact]
    public void TheClipCoversEverythingLeftOfTheLine()
    {
        DividerLayout layout = LayoutAt(0.25);

        Assert.Equal(200, layout.LineX, 6);
        Assert.Equal(0, layout.ClipRect.X, 6);
        Assert.Equal(0, layout.ClipRect.Y, 6);
        Assert.Equal(200, layout.ClipRect.Width, 6);
        Assert.Equal(Height, layout.ClipRect.Height, 6);
    }

    [Fact]
    public void TheHandleIsCentredOnTheLineAndOnTheHeight()
    {
        DividerLayout layout = LayoutAt(0.5);

        Assert.Equal(400 - (DividerModel.HandleWidth / 2), layout.Handle.X, 6);
        Assert.Equal((Height - DividerModel.HandleHeight) / 2, layout.Handle.Y, 6);
        Assert.Equal(DividerModel.HandleWidth, layout.Handle.Width, 6);
        Assert.Equal(DividerModel.HandleHeight, layout.Handle.Height, 6);
    }

    [Fact]
    public void TheHandleTheLayoutDrawsIsTheHandleTheHitTestAccepts()
    {
        var divider = new DividerModel { Position = 0.5 };
        DividerLayout layout = divider.Layout(Width, Height);

        double insideX = layout.Handle.X + 1;
        double insideY = layout.Handle.Y + 1;
        Assert.True(divider.HitTest(insideX, insideY, Width, Height));

        // Just outside the handle horizontally, and far enough down the line to be clear of
        // the line tolerance as well.
        Assert.False(divider.HitTest(layout.Handle.Right + 1, layout.Handle.Bottom + 40, Width, Height));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void TheLayoutStaysWellFormedAtTheExtremes(double position)
    {
        DividerLayout layout = LayoutAt(position);

        Assert.InRange(layout.LineX, 0, Width);
        Assert.InRange(layout.ClipRect.Width, 0, Width);
        Assert.Equal(DividerModel.HandleHeight, layout.Handle.Height, 6);
    }
}
