using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataDirectory = MetadataExtractor.Directory;

namespace FocalLenghChecker;

/// <summary>The focal length read from one file, or the reason it could not be read.</summary>
public readonly record struct FocalLengthReadResult(double? FocalLengthMm, string? Error)
{
    public bool Succeeded => FocalLengthMm.HasValue;

    public static FocalLengthReadResult Success(double focalLengthMm) => new(focalLengthMm, null);
    public static FocalLengthReadResult Failure(string error) => new(null, error);
}

public interface IFocalLengthReader
{
    /// <summary>Reads the focal length, in millimetres as recorded by the camera.</summary>
    FocalLengthReadResult Read(string path);
}

/// <summary>
/// Reads the EXIF FocalLength tag (0x920A) out of a Nikon .NEF file.
///
/// The EXIF FocalLengthIn35mmFilm tag (0xA405) is deliberately ignored even when
/// present: the --ff switch is the declared source of truth for whether the
/// recorded focal lengths are DX or full-frame, and mixing the two sources would
/// give inconsistent results across a folder.
/// </summary>
public sealed class NefFocalLengthReader : IFocalLengthReader
{
    public FocalLengthReadResult Read(string path)
    {
        try
        {
            return FromDirectories(ImageMetadataReader.ReadMetadata(path));
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException or UnauthorizedAccessException)
        {
            return FocalLengthReadResult.Failure(ex.Message);
        }
    }

    /// <summary>Reads from an already-open stream. Used by the tests.</summary>
    public FocalLengthReadResult Read(Stream stream)
    {
        try
        {
            return FromDirectories(ImageMetadataReader.ReadMetadata(stream));
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException)
        {
            return FocalLengthReadResult.Failure(ex.Message);
        }
    }

    private static FocalLengthReadResult FromDirectories(IReadOnlyList<MetadataDirectory> directories)
    {
        // A .NEF holds several IFDs (thumbnail, preview, full raw), so take the
        // first directory that actually carries a usable focal length.
        foreach (var directory in directories.OfType<ExifDirectoryBase>())
        {
            if (!directory.ContainsTag(ExifDirectoryBase.TagFocalLength))
                continue;

            if (TryReadMillimetres(directory, out var millimetres))
                return FocalLengthReadResult.Success(millimetres);
        }

        return FocalLengthReadResult.Failure("No EXIF focal length was recorded.");
    }

    private static bool TryReadMillimetres(MetadataDirectory directory, out double millimetres)
    {
        millimetres = 0;

        if (directory.TryGetRational(ExifDirectoryBase.TagFocalLength, out var rational))
        {
            if (rational.Denominator == 0)
                return false;

            millimetres = rational.ToDouble();
        }
        else if (!directory.TryGetDouble(ExifDirectoryBase.TagFocalLength, out millimetres))
        {
            return false;
        }

        return double.IsFinite(millimetres) && millimetres > 0;
    }
}
