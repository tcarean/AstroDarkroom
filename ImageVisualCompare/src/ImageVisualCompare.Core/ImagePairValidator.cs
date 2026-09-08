namespace ImageVisualCompare.Core;

public readonly record struct ValidationResult(bool IsValid, string? Error)
{
    public static ValidationResult Success { get; } = new(true, null);

    public static ValidationResult Failure(string error) => new(false, error);
}

public static class ImagePairValidator
{
    /// <summary>
    /// A side-by-side comparison only means anything when both images cover the same pixels,
    /// so a mismatched pair is rejected outright — with both resolutions spelled out, because
    /// "wrong size" without the numbers leaves the user to go and check for themselves.
    /// </summary>
    public static ValidationResult Validate(ImageInfo before, ImageInfo after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (before.PixelWidth == after.PixelWidth && before.PixelHeight == after.PixelHeight)
        {
            return ValidationResult.Success;
        }

        return ValidationResult.Failure(
            string.Join(
                Environment.NewLine,
                "The two images must have identical dimensions.",
                $"A: {before.FileName} — {before.Dimensions}",
                $"B: {after.FileName} — {after.Dimensions}"));
    }
}
