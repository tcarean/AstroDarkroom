using Starter.Cli;
using Xunit;

namespace Starter.Tests;

/// <summary>
/// Black-box tests: arguments in, text and exit code out. They know nothing about Core's
/// types, so they survive a rewrite of the implementation unchanged. This is the part of
/// the test suite that makes a rebuild on another model or platform checkable.
/// </summary>
public class AcceptanceTests
{
    [Fact]
    public void R004_NoArguments_PrintsUsage_Exit2()
    {
        (int code, string stdout, string stderr) = RunCli();
        Assert.Equal(2, code);
        Assert.Equal(string.Empty, stdout);
        Assert.StartsWith("usage:", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void R004_GoodFile_PrintsThreeLines_Exit0()
    {
        (int code, string stdout, string stderr) = RunCli(TestData.Fixture("numbers-small.txt"));
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr);
        string[] lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["count  10", "median 5.5000", "sigma  3.7065"], lines);
    }

    [Fact]
    public void R004_BadFile_PrintsErrorWithLine_Exit1()
    {
        (int code, string stdout, string stderr) = RunCli(TestData.Fixture("numbers-bad.txt"));
        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout);
        // file line 4: the comment on line 1 counts, R-003 reports file lines, not data lines
        Assert.Contains("Line 4", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void R004_MissingFile_Exit1()
    {
        (int code, _, string stderr) = RunCli(Path.Combine(TestData.Root, "does-not-exist.txt"));
        Assert.Equal(1, code);
        Assert.StartsWith("error:", stderr, StringComparison.Ordinal);
    }

    private static (int Code, string Stdout, string Stderr) RunCli(params string[] args)
    {
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        int code = Program.Run(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }
}
