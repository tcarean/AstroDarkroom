namespace FocalLenghChecker;

/// <summary>How many files fell into one configured range.</summary>
public sealed record RangeCount(FocalLengthRange Range, int Count, double Percentage);

/// <summary>How many files used one distinct focal length, shown both ways.</summary>
public sealed record FocalLengthCount(double DxMm, double FfMm, int Count, double Percentage);

/// <summary>Everything the report needs, with no I/O involved in producing it.</summary>
public sealed record AnalysisResult(
    bool SourceIsFullFrame,
    double CropFactor,
    IReadOnlyList<RangeCount> Ranges,
    int OutsideRangesCount,
    double OutsideRangesPercentage,
    IReadOnlyList<FocalLengthCount> FocalLengths,
    int FilesWithFocalLength,
    int FilesWithoutFocalLength)
{
    public int FilesScanned => FilesWithFocalLength + FilesWithoutFocalLength;
}
