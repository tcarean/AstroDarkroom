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

using System.Text.RegularExpressions;
using CopyWcs.Tests;
using NLog;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The window's side of the diagnostic log (R-043, ADR 0009): its start line, a header whose facts
/// could not be read, and a copy that writes what the console's copy writes. Without a window,
/// like <see cref="MainViewModelTests"/>.
/// </summary>
public class WindowLoggingTests
{
    [Fact]
    public void R043_TheStartLineNamesTheLanguageAndTheFilesFromTheCommandLine()
    {
        using WindowFixture f = new();

        Assert.Equal("language=ro source from the command line: " + f.Source + " dest: " + f.Dest,
                     App.StartLine(App.Parse(["--lang", "ro", f.Source, f.Dest])));
        Assert.Equal("language=en source from the command line: none dest: none", App.StartLine(App.Parse([])));
    }

    /// <summary>The facts' quiet catch is a Debug line with the reader's exception; the box stays empty.</summary>
    [Fact]
    public void R043_AFileWhoseFactsCannotBeReadIsADebugLineWithTheException()
    {
        using WindowFixture f = new();
        string notFits = f.FilePath("notes.fit");
        File.WriteAllText(notFits, "not a FITS file, only some text");
        using var capture = LogCapture.Start();

        MainViewModel vm = new() { Source = notFits };

        Assert.Equal("", vm.SourceFacts);
        LogCapture.Captured line = Assert.Single(capture.Events, e => e.Logger == "Window");
        Assert.Equal(LogLevel.Debug, line.Level);
        Assert.IsType<InvalidDataException>(line.Exception);
        Assert.Equal("no facts under the source box: the header of " + notFits + " could not be read", line.Message);
    }

    /// <summary>The window's copy goes through the core's run, so its lines are the console's for the same request.</summary>
    [Fact]
    public async Task R043_ACopyFromTheWindowWritesTheLinesOfTheConsolesCopy()
    {
        using WindowFixture window = new();
        using WindowFixture console = new();
        MainViewModel vm = new() { Source = window.Source, Dest = window.Dest };

        IReadOnlyList<string> fromWindow;
        using (var capture = LogCapture.Start())
        {
            await vm.Copy();
            fromWindow = OfTheRun(capture, window.Dir);
        }

        IReadOnlyList<string> fromConsole;
        using (var capture = LogCapture.Start())
        {
            Assert.Equal(0, CopyWcsCli.Run([console.Source, console.Dest], new StringWriter(), new StringWriter()));
            fromConsole = OfTheRun(capture, console.Dir);
        }

        Assert.NotEmpty(fromWindow);
        Assert.Equal(fromConsole, fromWindow);
    }

    /// <summary>A run's messages from <c>copy start</c> to <c>copy done</c>, the folder and the times made neutral.</summary>
    private static List<string> OfTheRun(LogCapture capture, string dir)
    {
        List<string> lines = capture.Events.Where(e => e.Logger is "WcsCopy" or "FitsHeader").Select(e => e.Message).ToList();
        int start = lines.FindIndex(m => m.StartsWith("copy start ", StringComparison.Ordinal));
        int done = lines.FindIndex(m => m.StartsWith("copy done ", StringComparison.Ordinal));
        Assert.True(start >= 0 && done > start, "no run between copy start and copy done");
        return lines.GetRange(start, done - start + 1)
                    .Select(m => Regex.Replace(m.Replace(dir, "<dir>", StringComparison.Ordinal), @"\d+ ms\b", "# ms"))
                    .ToList();
    }
}
