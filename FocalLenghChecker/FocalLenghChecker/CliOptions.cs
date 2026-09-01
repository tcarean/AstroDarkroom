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

/// <summary>The validated command line, once parsing has succeeded.</summary>
public sealed record CliOptions(string SourceFolder, bool FullFrame);

/// <summary>The outcome of parsing a command line: options, an error, or a help request.</summary>
public sealed record CliParseResult(CliOptions? Options, string? Error, bool HelpRequested)
{
    public static CliParseResult Success(CliOptions options) => new(options, null, false);
    public static CliParseResult Failure(string error) => new(null, error, false);
    public static CliParseResult Help() => new(null, null, true);
}

public static class CommandLineParser
{
    /// <summary>
    /// The help text. It ends with the GPLv3 warranty disclaimer, which is why it is
    /// composed rather than being one literal.
    /// </summary>
    public static readonly string Usage = """
        FocalLenghChecker - counts the focal lengths used across a folder of Nikon .NEF files.

        Usage:
          FocalLenghChecker [SourceFolderPath] [--ff]

          SourceFolderPath  Folder to search recursively for .NEF files.
                            Defaults to the current directory.
          --ff              Treat the focal lengths recorded in the files as
                            already being full-frame. Without it they are treated
                            as DX and multiplied by the 1.5 crop factor.
          --help, -h        Show this help.


        """ + LicenseInfo.WarrantyNotice;

    /// <summary>
    /// Parses the command line. The folder and the flag may appear in either order.
    /// </summary>
    public static CliParseResult Parse(IReadOnlyList<string> args, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? folder = null;
        var fullFrame = false;

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
                continue;

            if (IsFlag(arg, "help", "h", "?"))
                return CliParseResult.Help();

            if (IsFlag(arg, "ff"))
            {
                if (fullFrame)
                    return CliParseResult.Failure("--ff was given more than once.");

                fullFrame = true;
                continue;
            }

            if (arg.StartsWith('-'))
                return CliParseResult.Failure($"Unknown option '{arg}'.");

            if (folder is not null)
                return CliParseResult.Failure(
                    $"Only one source folder may be given, but both '{folder}' and '{arg}' were supplied.");

            folder = arg;
        }

        return CliParseResult.Success(new CliOptions(folder ?? currentDirectory, fullFrame));
    }

    private static bool IsFlag(string arg, params string[] names)
    {
        var trimmed = arg.TrimStart('-');

        if (trimmed.Length == arg.Length)
            return false;

        return names.Any(name => string.Equals(trimmed, name, StringComparison.OrdinalIgnoreCase));
    }
}
