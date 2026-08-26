namespace CopyWcs.Tests;

/// <summary>Director temporar unic per test, sters recursiv la Dispose (xUnit ruleaza claselele in paralel).</summary>
internal sealed class TempWorkspace : IDisposable
{
    public string Dir { get; }

    public TempWorkspace()
    {
        Dir = Path.Combine(Path.GetTempPath(), "CopyWcsTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Dir);
    }

    public string FilePath(string fileName) => Path.Combine(Dir, fileName);

    public string WriteFile(string fileName, byte[] content)
    {
        var path = FilePath(fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Dir, recursive: true); } catch { /* best effort */ }
    }
}
