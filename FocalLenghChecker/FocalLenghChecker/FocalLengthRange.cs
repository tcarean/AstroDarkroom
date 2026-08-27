namespace FocalLenghChecker;

/// <summary>
/// A range of full-frame-equivalent focal lengths, in millimetres.
/// Written as inclusive whole millimetres (e.g. 16-23), which is how the
/// ranges are configured in <see cref="FocalLengthRanges"/>.
/// </summary>
public sealed record FocalLengthRange(int MinFf, int MaxFf)
{
    /// <summary>
    /// The exclusive upper bound actually used for matching.
    /// A range written as 16-23 matches everything up to (but not including) 24,
    /// so fractional focal lengths cannot fall into the gap between 23 and 24.
    /// A 15.5 mm DX lens, for instance, is 23.25 mm full-frame equivalent.
    /// </summary>
    public double UpperBoundExclusive => MaxFf + 1;

    /// <summary>Does this range contain the given full-frame-equivalent focal length?</summary>
    public bool Contains(double ffMillimetres) =>
        ffMillimetres >= MinFf && ffMillimetres < UpperBoundExclusive;

    /// <summary>The range as written, in full-frame millimetres: "16 - 23 mm".</summary>
    public string FfLabel => $"{MinFf} - {MaxFf} mm";

    /// <summary>The same range expressed in DX millimetres: "10.7 - 15.3 mm".</summary>
    public string DxLabel(double cropFactor) =>
        $"{FormatMm(MinFf / cropFactor)} - {FormatMm(MaxFf / cropFactor)} mm";

    internal static string FormatMm(double millimetres) =>
        millimetres.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
}
