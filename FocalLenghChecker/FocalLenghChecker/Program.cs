// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

namespace FocalLenghChecker;

public static class ExitCodes
{
    public const int Success = 0;
    public const int BadArguments = 1;
    public const int FolderNotFound = 2;
    public const int NoFilesFound = 3;
    public const int BadConfiguration = 4;
}

internal static class Program
{
    /// <summary>How many unreadable files to name before summarising the rest.</summary>
    private const int MaxListedFailures = 5;

    private static int Main(string[] args)
    {
        // The version, on the first line of any run - help and failures included - so
        // that a report pasted somewhere else says on its own which version produced it.
        // Under it, the copyright notice GPLv3 asks every copy to carry, which says whose
        // tool that was. The warranty disclaimer is in --help.
        Console.WriteLine(VersionInfo.Banner);
        Console.WriteLine(LicenseInfo.Copyright);
        Console.WriteLine();

        var parsed = CommandLineParser.Parse(args, Environment.CurrentDirectory);

        if (parsed.HelpRequested)
        {
            Console.WriteLine(CommandLineParser.Usage);
            return ExitCodes.Success;
        }

        if (parsed.Options is not { } options)
        {
            Console.Error.WriteLine(parsed.Error);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CommandLineParser.Usage);
            return ExitCodes.BadArguments;
        }

        try
        {
            FocalLengthRanges.Validate(FocalLengthRanges.Default);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"The focal length ranges in FocalLengthRanges.cs are not usable: {ex.Message}");
            return ExitCodes.BadConfiguration;
        }

        string folder;

        try
        {
            folder = Path.GetFullPath(options.SourceFolder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"'{options.SourceFolder}' is not a valid folder path: {ex.Message}");
            return ExitCodes.BadArguments;
        }

        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"Folder not found: {folder}");
            return ExitCodes.FolderNotFound;
        }

        var analyzer = new FolderAnalyzer(new NefFileFinder(), new NefFocalLengthReader());
        var analysis = analyzer.Analyze(
            folder,
            options.FullFrame,
            FocalLengthRanges.Default,
            FocalLengthRanges.CropFactor,
            ReportProgress);

        ClearProgress();

        if (analysis.FilesFound == 0)
        {
            Console.Error.WriteLine($"No .NEF files were found under {folder}");
            return ExitCodes.NoFilesFound;
        }

        Console.Out.Write(ReportFormatter.Format(analysis.Result, folder));
        WriteFailures(analysis.Unreadable);

        return ExitCodes.Success;
    }

    private static void ReportProgress(int read, int total)
    {
        if (Console.IsErrorRedirected || total == 0)
            return;

        // Only repaint occasionally; writing per file is slower than reading one.
        if (read != total && read % 25 != 0)
            return;

        Console.Error.Write($"\rReading {read:N0} of {total:N0} .NEF files...");
    }

    private static void ClearProgress()
    {
        if (Console.IsErrorRedirected)
            return;

        Console.Error.Write("\r" + new string(' ', 48) + "\r");
    }

    private static void WriteFailures(IReadOnlyList<UnreadableFile> unreadable)
    {
        if (unreadable.Count == 0)
            return;

        foreach (var failure in unreadable.Take(MaxListedFailures))
            Console.Error.WriteLine($"  {failure.Path}: {failure.Error}");

        if (unreadable.Count > MaxListedFailures)
            Console.Error.WriteLine($"  ...and {unreadable.Count - MaxListedFailures:N0} more.");
    }
}
