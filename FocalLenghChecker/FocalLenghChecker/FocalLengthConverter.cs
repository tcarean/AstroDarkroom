namespace FocalLenghChecker;

/// <summary>Converts between DX and full-frame-equivalent focal lengths.</summary>
public static class FocalLengthConverter
{
    /// <summary>
    /// The full-frame equivalent of a focal length as recorded by the camera.
    /// When the recorded values are already full-frame it is returned unchanged;
    /// otherwise it is a DX value and gets multiplied by the crop factor.
    /// </summary>
    public static double ToFullFrame(double recordedMm, bool sourceIsFullFrame, double cropFactor)
    {
        EnsureValidCropFactor(cropFactor);
        return sourceIsFullFrame ? recordedMm : recordedMm * cropFactor;
    }

    /// <summary>The DX equivalent of a focal length as recorded by the camera.</summary>
    public static double ToDx(double recordedMm, bool sourceIsFullFrame, double cropFactor)
    {
        EnsureValidCropFactor(cropFactor);
        return sourceIsFullFrame ? recordedMm / cropFactor : recordedMm;
    }

    private static void EnsureValidCropFactor(double cropFactor)
    {
        if (!double.IsFinite(cropFactor) || cropFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(cropFactor), cropFactor, "The crop factor must be positive.");
    }
}
