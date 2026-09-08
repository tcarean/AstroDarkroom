namespace ImageVisualCompare.Core;

public enum ImageScalingMode
{
    /// <summary>Interpolated. Correct everywhere the user is looking at the picture.</summary>
    Smooth,

    /// <summary>Nearest neighbour. Correct once the user is looking at individual pixels.</summary>
    Pixelated,
}

/// <param name="TranslateX">Translation to apply before scaling, in content units.</param>
/// <param name="TranslateY">Translation to apply before scaling, in content units.</param>
/// <param name="RenderScale">Scale in device-independent units, ready to hand to the UI.</param>
/// <param name="ScalingMode">How the bitmap should be resampled at this zoom.</param>
public readonly record struct RenderPlan(
    double TranslateX,
    double TranslateY,
    double RenderScale,
    ImageScalingMode ScalingMode);

public static class RenderPlanner
{
    /// <summary>
    /// Past this zoom the interpolation is smoothing away exactly what the user zoomed in to
    /// see, so the pixels are shown as the squares they are.
    /// </summary>
    public const double PixelatedThreshold = 4.0;

    /// <summary>
    /// Turns viewport state into the numbers the view puts on its transform.
    ///
    /// The division by <paramref name="deviceScale"/> is the whole reason this is worth
    /// isolating: the viewport counts in physical screen pixels so that 100% means one image
    /// pixel per screen pixel, while the UI lays out in device-independent pixels. Getting
    /// that conversion backwards is invisible on an unscaled display and wrong everywhere else.
    /// </summary>
    public static RenderPlan For(Viewport viewport, double deviceScale)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deviceScale);

        return new RenderPlan(
            -viewport.OffsetX,
            -viewport.OffsetY,
            viewport.Scale / deviceScale,
            viewport.Scale >= PixelatedThreshold ? ImageScalingMode.Pixelated : ImageScalingMode.Smooth);
    }
}
