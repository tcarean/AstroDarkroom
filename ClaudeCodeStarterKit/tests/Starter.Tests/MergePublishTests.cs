using Xunit;

namespace Starter.Tests;

/// <summary>
/// scripts/merge-publish.ps1 puts one published folder into another and refuses to overwrite a
/// file with other bytes (the kit's ADR 0036): release.ps1 merges every publish of a bundle
/// through it, so two runtimes or two versions of a library never overwrite each other unseen.
/// </summary>
public sealed class MergePublishTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "starter-merge", Guid.NewGuid().ToString("N"));
    private readonly string _from;
    private readonly string _into;

    public MergePublishTests()
    {
        _from = Path.Combine(_dir, "from");
        _into = Path.Combine(_dir, "into");
        Directory.CreateDirectory(_from);
        Directory.CreateDirectory(_into);
    }

    [Fact]
    public void EqualFiles_AreSkipped_AndTheRestCopied_WithTheirSubfolders()
    {
        Write(_from, "runtime.dll", "same");
        Write(_into, "runtime.dll", "same");
        Write(_from, "lensfun/db.xml", "new");
        Write(_from, "app.exe", "app");

        (int exitCode, string output) = Merge();
        Assert.True(exitCode == 0, output);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_into, "lensfun", "db.xml")));
        Assert.Equal("app", File.ReadAllText(Path.Combine(_into, "app.exe")));
        Assert.Contains("2 file(s) copied, 1 already there", output);
    }

    [Fact]
    public void AnIntoFolderThatDoesNotExist_IsCreated()
    {
        Directory.Delete(_into);
        Write(_from, "a.txt", "a");

        (int exitCode, string output) = Merge();
        Assert.True(exitCode == 0, output);
        Assert.Equal("a", File.ReadAllText(Path.Combine(_into, "a.txt")));
    }

    /// <summary>One differing file stops the whole merge: the files that do not collide are not copied either.</summary>
    [Fact]
    public void ADifferingFile_IsReportedByItsRelativePath_AndNothingIsCopied()
    {
        Write(_from, "runtimes/coreclr.dll", "net8");
        Write(_into, "runtimes/coreclr.dll", "net9");
        Write(_from, "LICENSE", "same");
        Write(_into, "LICENSE", "same");
        Write(_from, "NOTICES.md", "short");
        Write(_into, "NOTICES.md", "a longer text");
        Write(_from, "app.exe", "app");

        (int exitCode, string output) = Merge();
        Assert.Equal(1, exitCode);
        Assert.Contains("collision  runtimes/coreclr.dll  (", output);
        Assert.Contains("collision  NOTICES.md  (", output);
        Assert.DoesNotContain("collision  LICENSE", output);
        Assert.False(File.Exists(Path.Combine(_into, "app.exe")), "a file was copied despite the collision");
        Assert.Equal("net9", File.ReadAllText(Path.Combine(_into, "runtimes", "coreclr.dll")));
    }

    /// <summary>A copy that fails midway exits 2 and takes back the files this run copied, so -Into is left as it was.</summary>
    [Fact]
    public void AFailedCopy_RemovesWhatThisRunCopied_AndExitsTwo()
    {
        Write(_from, "first.txt", "copied before the failure");
        Write(_from, "lensfun/db.xml", "cannot go under a file");
        Write(_into, "lensfun", "a file where the folder should be");

        (int exitCode, string output) = Merge();
        Assert.Equal(2, exitCode);
        Assert.Contains("merge-publish:", output);
        Assert.False(File.Exists(Path.Combine(_into, "first.txt")), "the file copied before the failure was left behind");
        Assert.Equal("a file where the folder should be", File.ReadAllText(Path.Combine(_into, "lensfun")));
    }

    /// <summary>An error that is not a collision exits 2, so release.ps1 does not give the collision advice for it.</summary>
    [Fact]
    public void AMissingFromFolder_ExitsTwo_AndIsNoCollision()
    {
        Directory.Delete(_from);

        (int exitCode, string output) = Merge();
        Assert.Equal(2, exitCode);
        Assert.Contains("is not a folder", output);
        Assert.DoesNotContain("collision", output);
    }

    public void Dispose()
    {
        ScriptRunner.DeleteTree(_dir);
    }

    private (int ExitCode, string Output) Merge()
    {
        string script = Path.Combine(TestData.FindRepoRoot(), "scripts", "merge-publish.ps1");
        return ScriptRunner.RunScript(_dir, script, "-From", _from, "-Into", _into);
    }

    private static void Write(string folder, string relative, string text)
    {
        string path = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
