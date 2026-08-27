namespace FocalLenghChecker;

/// <summary>A file whose focal length could not be read, and why.</summary>
public sealed record UnreadableFile(string Path, string Error);

/// <summary>The outcome of scanning a folder.</summary>
public sealed record FolderAnalysis(
    AnalysisResult Result,
    IReadOnlyList<UnreadableFile> Unreadable,
    int FilesFound);

/// <summary>
/// Walks a folder, reads each .NEF and hands the tally to <see cref="RangeAnalyzer"/>.
/// The file system and the metadata reader both come in through interfaces, so the
/// whole flow can be exercised without touching a disk.
/// </summary>
public sealed class FolderAnalyzer(INefFileFinder finder, IFocalLengthReader reader)
{
    private readonly INefFileFinder _finder = finder ?? throw new ArgumentNullException(nameof(finder));
    private readonly IFocalLengthReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    /// <param name="onProgress">Called with (filesRead, filesFound) after each file.</param>
    public FolderAnalysis Analyze(
        string folder,
        bool sourceIsFullFrame,
        IReadOnlyList<FocalLengthRange> ranges,
        double cropFactor,
        Action<int, int>? onProgress = null)
    {
        var files = _finder.Find(folder).ToList();
        var tally = new FocalLengthTally();
        var unreadable = new List<UnreadableFile>();

        for (var i = 0; i < files.Count; i++)
        {
            var result = _reader.Read(files[i]);

            if (result.FocalLengthMm is { } millimetres)
                tally.Add(millimetres);
            else
                unreadable.Add(new UnreadableFile(files[i], result.Error ?? "Unknown error."));

            onProgress?.Invoke(i + 1, files.Count);
        }

        var analysis = RangeAnalyzer.Analyze(tally, sourceIsFullFrame, ranges, cropFactor, unreadable.Count);

        return new FolderAnalysis(analysis, unreadable, files.Count);
    }
}
