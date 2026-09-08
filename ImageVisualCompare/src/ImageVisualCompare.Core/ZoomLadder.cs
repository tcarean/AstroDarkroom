namespace ImageVisualCompare.Core;

/// <summary>
/// The discrete zoom stops used by the -/+ buttons and keyboard shortcuts. The mouse
/// wheel zooms continuously instead, so an arbitrary scale can be current at any time;
/// the ladder therefore steps to the neighbouring stop rather than to a fixed index.
/// </summary>
public static class ZoomLadder
{
    private const double Epsilon = 1e-6;

    public static IReadOnlyList<double> Steps { get; } =
    [
        0.02, 0.03, 0.05, 0.08, 0.125, 0.17, 0.25, 0.33, 0.5, 0.67,
        1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 8.0, 12.0, 16.0
    ];

    /// <summary>The lowest stop strictly above <paramref name="scale"/>, or the highest stop.</summary>
    public static double Next(double scale)
    {
        foreach (double step in Steps)
        {
            if (step > scale + Epsilon)
            {
                return step;
            }
        }

        return Steps[^1];
    }

    /// <summary>The highest stop strictly below <paramref name="scale"/>, or the lowest stop.</summary>
    public static double Previous(double scale)
    {
        for (int i = Steps.Count - 1; i >= 0; i--)
        {
            if (Steps[i] < scale - Epsilon)
            {
                return Steps[i];
            }
        }

        return Steps[0];
    }
}
