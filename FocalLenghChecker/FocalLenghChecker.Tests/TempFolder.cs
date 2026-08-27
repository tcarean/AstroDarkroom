namespace FocalLenghChecker.Tests;

/// <summary>A throwaway directory tree that deletes itself at the end of a test.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"FocalLenghChecker-{Guid.NewGuid():N}");

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Creates an empty file at a path relative to the folder, making parents as needed.</summary>
    public string AddFile(string relativePath)
    {
        var full = System.IO.Path.Combine(Path, relativePath);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, Array.Empty<byte>());

        return full;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }
    }
}
