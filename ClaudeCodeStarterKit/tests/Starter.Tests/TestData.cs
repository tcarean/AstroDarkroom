namespace Starter.Tests;

/// <summary>Where the fixtures are at run time. Large files are resolved through STARTER_TESTDATA_LARGE.</summary>
internal static class TestData
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "testdata");

    public static string Fixture(string name) => Path.Combine(Root, "fixtures", name);

    /// <summary>
    /// A large file that is not in the repo. Returns null when it is absent, so the test can
    /// Skip with a clear message instead of failing on a machine that does not have the data.
    /// </summary>
    public static string? Large(string name)
    {
        string dir = Environment.GetEnvironmentVariable("STARTER_TESTDATA_LARGE")
                     ?? Path.Combine(FindRepoRoot(), "tests", "testdata", "large");
        string path = Path.Combine(dir, name);
        return File.Exists(path) ? path : null;
    }

    /// <summary>The folder holding Starter.sln; the build output folder when there is none.</summary>
    public static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Starter.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
