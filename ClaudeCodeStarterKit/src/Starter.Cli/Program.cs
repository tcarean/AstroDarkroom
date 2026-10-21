using System.Globalization;
using Starter.Core;

namespace Starter.Cli;

/// <summary>
/// The command-line entry point: prints the count, median and robust sigma of a numbers file.
/// </summary>
internal static class Program
{
    /// <summary>Runs the CLI on the process's own console streams.</summary>
    /// <param name="args">The command-line arguments; exactly one, the path of a numbers file.</param>
    /// <returns>The exit code: 0 on success, 1 on bad input, 2 on a usage error (R-004).</returns>
    private static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>
    /// The whole CLI, testable: the acceptance tests call this with a StringWriter and compare
    /// the text. R-004: exit 0 on success, 1 on bad input, 2 on usage error.
    /// </summary>
    /// <param name="args">The command-line arguments; exactly one, the path of a numbers file.</param>
    /// <param name="stdout">Receives the three result lines: count, median and sigma.</param>
    /// <param name="stderr">Receives the usage line or the error message.</param>
    /// <returns>The exit code: 0 on success, 1 on bad input, 2 on a usage error.</returns>
    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length != 1)
        {
            stderr.WriteLine("usage: starter <numbers-file>");
            return 2;
        }

        try
        {
            using StreamReader reader = File.OpenText(args[0]);
            IReadOnlyList<double> values = NumberFile.Parse(reader);
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"count  {values.Count}"));
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"median {Stats.Median(values):F4}"));
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"sigma  {Stats.RobustSigma(values):F4}"));
            return 0;
        }
        catch (Exception ex) when (ex is IOException or FormatException or ArgumentException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }
}
