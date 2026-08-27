namespace FocalLenghChecker;

/// <summary>
/// Turns a tally of focal lengths into the counts and percentages for each
/// configured range. Pure: no files, no console, no clock.
/// </summary>
public static class RangeAnalyzer
{
    public static AnalysisResult Analyze(
        FocalLengthTally tally,
        bool sourceIsFullFrame,
        IReadOnlyList<FocalLengthRange> ranges,
        double cropFactor,
        int filesWithoutFocalLength = 0)
    {
        ArgumentNullException.ThrowIfNull(tally);
        FocalLengthRanges.Validate(ranges);
        ArgumentOutOfRangeException.ThrowIfNegative(filesWithoutFocalLength);

        var total = tally.Total;
        var rangeCounts = new int[ranges.Count];
        var outside = 0;
        var focalLengths = new List<FocalLengthCount>(tally.Counts.Count);

        foreach (var (recordedMm, count) in tally.Counts)
        {
            var ff = FocalLengthConverter.ToFullFrame(recordedMm, sourceIsFullFrame, cropFactor);
            var dx = FocalLengthConverter.ToDx(recordedMm, sourceIsFullFrame, cropFactor);

            focalLengths.Add(new FocalLengthCount(dx, ff, count, Percentage(count, total)));

            var matched = false;

            for (var i = 0; i < ranges.Count; i++)
            {
                if (!ranges[i].Contains(ff))
                    continue;

                rangeCounts[i] += count;
                matched = true;
                break;
            }

            if (!matched)
                outside += count;
        }

        var rows = ranges
            .Select((range, i) => new RangeCount(range, rangeCounts[i], Percentage(rangeCounts[i], total)))
            .ToList();

        // Most-used focal length first; ties broken by the shorter focal length.
        focalLengths.Sort(static (a, b) =>
            b.Count != a.Count ? b.Count.CompareTo(a.Count) : a.FfMm.CompareTo(b.FfMm));

        return new AnalysisResult(
            sourceIsFullFrame,
            cropFactor,
            rows,
            outside,
            Percentage(outside, total),
            focalLengths,
            total,
            filesWithoutFocalLength);
    }

    private static double Percentage(int count, int total) =>
        total == 0 ? 0 : count * 100.0 / total;
}
