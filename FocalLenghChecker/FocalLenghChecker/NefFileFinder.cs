namespace FocalLenghChecker;

public interface INefFileFinder
{
    /// <summary>Finds every .NEF file beneath <paramref name="folder"/>, recursively.</summary>
    IEnumerable<string> Find(string folder);
}

public sealed class NefFileFinder : INefFileFinder
{
    public const string NefExtension = ".nef";

    public IEnumerable<string> Find(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        // Enumerated with "*" and filtered by extension rather than matched with
        // "*.nef", because on Windows the latter also matches 8.3 short names and
        // longer extensions such as ".nef_backup".
        return Directory.EnumerateFiles(folder, "*", options)
            .Where(IsNef)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsNef(string path) =>
        string.Equals(Path.GetExtension(path), NefExtension, StringComparison.OrdinalIgnoreCase);
}
