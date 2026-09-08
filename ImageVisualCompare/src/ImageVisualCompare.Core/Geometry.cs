namespace ImageVisualCompare.Core;

/// <summary>A width and height, in whatever space the caller is working in.</summary>
public readonly record struct SizeD(double Width, double Height);

/// <summary>
/// An axis-aligned rectangle. Declared here rather than reusing <c>System.Windows.Rect</c>
/// so the layout maths stays free of any UI framework type and testable without a WPF host.
/// </summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

public static class AspectFit
{
    /// <summary>
    /// The largest box with the content's aspect ratio that fits inside the given bounds.
    /// Used both for the on-screen minimap and for the thumbnail bitmap behind it, which is
    /// why it is a function rather than being written out twice.
    /// </summary>
    public static SizeD Fit(double contentWidth, double contentHeight, double maxWidth, double maxHeight)
    {
        if (contentWidth <= 0 || contentHeight <= 0 || maxWidth <= 0 || maxHeight <= 0)
        {
            return default;
        }

        double scale = Math.Min(maxWidth / contentWidth, maxHeight / contentHeight);
        return new SizeD(contentWidth * scale, contentHeight * scale);
    }
}
