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

using NLog;

namespace CopyWcs;

/// <summary>
/// The console, testable in process: the banner, the language, the options, the copy run of
/// the core and the printing of its outcome, with the exit code. It configures no log: the
/// entry point does (ADR 0009).
/// </summary>
internal static class CopyWcsCli
{
    /// <summary>The console's own lines in the diagnostic log: a refused <c>--lang</c>, a usage error, the help (R-043); the run logs itself.</summary>
    private static readonly Logger Log = LogManager.GetLogger("Cli");

    /// <summary>
    /// Runs the console once: prints the banner and the copyright, reads <c>--lang</c>, parses
    /// the options, runs the copy and prints what happened.
    /// </summary>
    /// <param name="args">The command line without the executable; not null.</param>
    /// <param name="stdout">Receives the banner, the copyright, the usage, the warnings and the result lines.</param>
    /// <param name="stderr">Receives the one line of a bad <c>--lang</c>, a parse error or a refusal.</param>
    /// <returns>0 on success or a dry run, 1 when the copy was refused or <c>--lang</c> names no language, 2 on a usage error.</returns>
    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        // The version, on the first line of every run, --help and the errors included, so that
        // a pasted output says on its own which version produced it. Under it, the copyright
        // notice GPLv3 asks for (see LICENSE).
        stdout.WriteLine(VersionInfo.Banner);
        stdout.WriteLine(LicenseInfo.Copyright);

        // --lang first, so the usage and every error after it are in the language (R-020); a
        // value it does not know is said in English (R-022). With no --lang the run keeps the
        // process's language, English (Ui.Default, R-021).
        var lang = LanguageArgument.Extract(args);
        if (lang.BadValue is not null)
        {
            string message = LanguageArgument.BadValueMessage(lang.BadValue);
            stderr.WriteLine("ERROR: " + message);
            Log.Warn("refused (exit 1): " + message);
            return 1;
        }

        using (lang.Language is UiLanguage chosen ? Ui.Use(chosen) : null)
        {
            return RunInLanguage(lang.Rest, stdout, stderr);
        }
    }

    /// <summary>The console after the banner and <c>--lang</c>: the options, the run, the printing.</summary>
    /// <param name="args">The arguments without <c>--lang</c> and its value; not null.</param>
    /// <param name="stdout">Receives the usage, the warnings and the result lines.</param>
    /// <param name="stderr">Receives the one line of a parse error or a refusal.</param>
    /// <returns>0 on success or a dry run, 1 when the copy was refused, 2 on a usage error.</returns>
    private static int RunInLanguage(string[] args, TextWriter stdout, TextWriter stderr)
    {
        Options opt;
        try
        {
            opt = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            stderr.WriteLine(ex.Message);
            // The line as the user saw it, in the run's language (R-046).
            Log.Warn("usage error (exit 2): " + ex.Message);
            Options.PrintUsage(stdout);
            return 2;
        }

        if (opt.ShowHelp)
        {
            Options.PrintUsage(stdout);
            Log.Info("help shown (exit 0)");
            return 0;
        }

        var outcome = WcsCopy.Run(new WcsCopyRequest(
            opt.Source, opt.Dest, opt.Output,
            IncludeOptical: !opt.NoOptical, Force: opt.Force, DryRun: opt.DryRun));

        foreach (var warning in outcome.Warnings)
        {
            stdout.WriteLine(warning);
        }

        switch (outcome.Kind)
        {
            case WcsCopyKind.Refused:
                stderr.WriteLine(outcome.Error);
                return 1;

            case WcsCopyKind.Previewed:
                foreach (var note in outcome.Notes)
                {
                    stdout.WriteLine(note);
                }

                foreach (var card in outcome.Cards)
                {
                    stdout.WriteLine("  " + card.TrimEnd(' '));
                }

                // The keys the copy would drop, under the cards it would write (R-016).
                if (outcome.Dropped is not null)
                {
                    stdout.WriteLine(outcome.Dropped);
                }

                return 0;

            default:
                // The keys dropped first, so the count and the reminder stay the last two lines (R-012, R-016).
                if (outcome.Dropped is not null)
                {
                    stdout.WriteLine(outcome.Dropped);
                }

                foreach (var note in outcome.Notes)
                {
                    stdout.WriteLine(note);
                }

                return 0;
        }
    }
}
