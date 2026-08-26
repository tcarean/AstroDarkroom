namespace FlatCheck.Tests;

/// <summary>A throwaway directory for tests that need real files on disk.</summary>
internal sealed class TempWorkspace : IDisposable
{
    internal string Dir { get; }

    internal TempWorkspace()
    {
        Dir = Path.Combine(Path.GetTempPath(), "flatcheck-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
    }

    /// <summary>Writes bytes into the workspace and returns the full path.</summary>
    internal string WriteFile(string name, byte[] bytes)
    {
        string p = Path.Combine(Dir, name);
        File.WriteAllBytes(p, bytes);
        return p;
    }

    /// <summary>A path inside the workspace, for output files that do not exist yet.</summary>
    internal string PathTo(string name) => Path.Combine(Dir, name);

    public void Dispose()
    {
        try { Directory.Delete(Dir, recursive: true); }
        catch (IOException) { /* a leaked temp dir must never fail a test */ }
        catch (UnauthorizedAccessException) { }
    }
}
