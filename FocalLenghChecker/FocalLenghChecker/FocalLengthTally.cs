namespace FocalLenghChecker;

/// <summary>
/// Counts how many times each distinct focal length was used, keyed by the value
/// the camera recorded (DX or full-frame depending on the --ff switch).
/// </summary>
public sealed class FocalLengthTally
{
    /// <summary>
    /// Focal lengths are rounded to this many decimals before being counted, so
    /// that the rational 180/10 and the rational 18/1 land in the same bucket.
    /// </summary>
    public const int Decimals = 1;

    private readonly SortedDictionary<double, int> _counts = new();

    public IReadOnlyDictionary<double, int> Counts => _counts;

    /// <summary>The number of focal lengths recorded, counting duplicates.</summary>
    public int Total { get; private set; }

    public void Add(double focalLengthMm)
    {
        if (!double.IsFinite(focalLengthMm) || focalLengthMm <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(focalLengthMm), focalLengthMm, "A focal length must be a positive, finite number.");

        var key = Math.Round(focalLengthMm, Decimals, MidpointRounding.AwayFromZero);

        _counts[key] = _counts.TryGetValue(key, out var existing) ? existing + 1 : 1;
        Total++;
    }
}
