namespace Starter.Core;

/// <summary>
/// Sample domain logic. Replace with the real project; keep the shape: pure functions, no I/O,
/// every public method tied to a requirement ID in docs/requirements.md and to at least one test.
/// </summary>
public static class Stats
{
    /// <summary>R-001: median of a non-empty sequence; the mean of the two middle values when even.</summary>
    public static double Median(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("Median of an empty sequence is undefined.", nameof(values));
        }

        double[] sorted = values.ToArray();
        Array.Sort(sorted);
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    /// <summary>R-002: median absolute deviation scaled by 1.4826, so it estimates sigma for a normal.</summary>
    public static double RobustSigma(IReadOnlyList<double> values)
    {
        double median = Median(values);
        double[] deviations = values.Select(v => Math.Abs(v - median)).ToArray();
        return 1.4826 * Median(deviations);
    }
}
